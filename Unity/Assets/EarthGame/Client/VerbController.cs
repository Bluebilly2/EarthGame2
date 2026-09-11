using System;
using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Shared;
using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>
    /// CANON's verb rule (2026-08-26) for the verbs a founder has so far (M1.5a): what the right mouse does comes from what
    /// the crosshair is on and what is in hand. On a thing lying within reach it picks it up; with something in hand and
    /// the ground within reach under the crosshair it puts it down there; 1–9 and the wheel choose the hand; Tab opens the
    /// carrying window. The client only asks; the server commits every verb and answers (<see cref="IntentMessage"/>), and
    /// the verb line under the crosshair says what the right mouse would do, or for a moment why it did not.
    ///
    /// <para>Reach is measured as the server measures it, from the body's eye (<see cref="PlayerController.Eye"/>) with the
    /// engine's own <see cref="Hands.ReachM"/>, so the line never offers what the server would refuse; the ray is the
    /// camera's, whose eye is the body's smoothed.</para>
    /// </summary>
    public sealed class VerbController
    {
        /// <summary>How long an answer other than done, or a note, stays on the verb line, s.</summary>
        public const float AnswerSeconds = 2f;

        /// <summary>How far past the reach the crosshair looks, m, so a thing a little beyond it hides the ground behind it rather than offering it.</summary>
        private const float LookBeyondM = 1f;

        private readonly PlayerController _player;
        private readonly Camera _camera;
        private readonly HudController _hud;
        private readonly HandView _hand;
        private GameClient _client;
        private EntityViews _entities;
        private string _answer = string.Empty;
        private float _answerUntil;
        private float _now;

        /// <summary>The thing under the crosshair within reach, or null.</summary>
        public EntityView Target { get; private set; }

        /// <summary>Where the crosshair meets the ground within reach, when it does and no thing is in front of it.</summary>
        public Vector3? Ground { get; private set; }

        /// <summary>What the verb line says now.</summary>
        public string Line { get; private set; } = string.Empty;

        public VerbController(GameClient client, EntityViews entities, PlayerController player, Camera camera, HudController hud, HandView hand)
        {
            _player = player;
            _camera = camera;
            _hud = hud;
            _hand = hand;
            Rebind(client, entities);
        }

        /// <summary>A new connection (a rejoin): its answers and its hands, and the things it shows.</summary>
        public void Rebind(GameClient client, EntityViews entities)
        {
            if (_client != null)
            {
                _client.IntentAnswered -= OnAnswered;
                _client.CarryingChanged -= OnCarrying;
            }
            _client = client;
            _entities = entities;
            if (_client == null) return;
            _client.IntentAnswered += OnAnswered;
            _client.CarryingChanged += OnCarrying;
            OnCarrying(_client.Carrying);
        }

        public void Dispose() => Rebind(null, null);

        /// <summary>A word on the verb line for a moment, for something the founder did that no verb answers.</summary>
        public void Note(string text)
        {
            _answer = text ?? string.Empty;
            _answerUntil = _now + AnswerSeconds;
        }

        private void OnAnswered(IntentResultMessage result)
        {
            if (result.Outcome == VerbOutcome.Done) _answer = string.Empty;
            else Note(Say(result.Outcome));
        }

        private void OnCarrying(CarryingMessage carrying)
        {
            _hud?.SetCarrying(carrying.Hand, carrying.Things);
            if (_hand == null) return;
            if (TryInHand(carrying, out CarriedThing held)) _hand.Show(held.Definition, held.Id);
            else _hand.Hide();
        }

        /// <summary>One frame: the presses taken from the controls acted on, the crosshair's target found, the verb line said.</summary>
        public void Tick(in ControlsFrame presses, float now)
        {
            _now = now;
            if (_client == null || _player == null || _camera == null) return;
            bool person = !(_player.Input is ScriptedInputSource) && !Application.isBatchMode;
            if (person && presses.Menu)
            {
                bool captured = Cursor.lockState == CursorLockMode.Locked;
                Cursor.lockState = captured ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = captured;
                return;
            }
            if (!_player.HoldsView)
            {
                // A click on the window takes the mouse back, and does nothing else.
                if (person && (presses.Use || presses.Work))
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                Target = null;
                Ground = null;
                Line = string.Empty;
                _hud?.SetVerb(Line);
                return;
            }
            if (presses.Carrying) _hud?.ToggleCarrying();
            CarryingMessage carrying = _client.Carrying;
            if (presses.HandPlace > 0) _client.SendIntent(new IntentMessage { Verb = Verb.Hold, Place = (byte)presses.HandPlace });
            else if (presses.HandStep != 0) _client.SendIntent(new IntentMessage { Verb = Verb.Hold, Place = Step(carrying, presses.HandStep) });

            Aim();
            if (presses.Use)
            {
                if (Target != null) _client.SendIntent(new IntentMessage { Verb = Verb.PickUp, EntityId = Target.Id.Value });
                else if (Ground.HasValue && TryInHand(carrying, out _))
                {
                    Vector3 at = Ground.Value;
                    _client.SendIntent(new IntentMessage { Verb = Verb.PutDown, East = at.x, Up = at.y, North = at.z });
                }
            }
            Line = now < _answerUntil && _answer.Length > 0 ? _answer : Offer(carrying);
            _hud?.SetVerb(Line);
        }

        /// <summary>What the crosshair is on within reach: the nearest thing lying, or else the ground.</summary>
        private void Aim()
        {
            Target = null;
            Ground = null;
            Transform eye = _camera.transform;
            Ray ray = new Ray(eye.position, eye.forward);
            float within = (float)Hands.ReachM + LookBeyondM;
            bool onGround = Physics.Raycast(ray, out RaycastHit hit, within, Layers.Mask(Layers.Terrain), QueryTriggerInteraction.Ignore);
            float groundAtM = onGround ? hit.distance : within;
            Double3 body = _player.Eye;
            if (_entities != null && _entities.Pick(ray, groundAtM, out EntityView thing, out _))
            {
                // A thing in front of the ground hides it, whether or not the thing is in reach.
                if (Double3.Distance(body, thing.Position) <= Hands.ReachM + thing.Definition.RadiusM) Target = thing;
                return;
            }
            if (onGround && Double3.Distance(body, new Double3(hit.point.x, hit.point.y, hit.point.z)) <= Hands.ReachM) Ground = hit.point;
        }

        /// <summary>What the right mouse would do now, in words.</summary>
        private string Offer(CarryingMessage carrying)
        {
            if (Target != null)
                return Target.Definition.DisplayName + " — " + (carrying.Things != null && carrying.Things.Length >= Hands.Places ? "your hands are full" : "pick up");
            if (Ground.HasValue && TryInHand(carrying, out CarriedThing held))
                return "put down " + The(held.Definition.DisplayName);
            return string.Empty;
        }

        /// <summary>The place the wheel moves the hand to: through the places that hold something, in order, and the empty hand after the last.</summary>
        private static byte Step(CarryingMessage carrying, int step)
        {
            List<byte> ring = new List<byte>();
            if (carrying.Things != null)
                foreach (CarriedThing t in carrying.Things) ring.Add(t.Place);
            ring.Sort();
            ring.Add(0);
            int at = ring.IndexOf(TryInHand(carrying, out _) ? carrying.Hand : (byte)0);
            int n = ring.Count;
            return ring[((at + step) % n + n) % n];
        }

        private static bool TryInHand(CarryingMessage carrying, out CarriedThing thing)
        {
            if (carrying.Things != null && carrying.Hand != 0)
                foreach (CarriedThing t in carrying.Things)
                    if (t.Place == carrying.Hand)
                    {
                        thing = t;
                        return true;
                    }
            thing = default;
            return false;
        }

        /// <summary>"a stick" as "the stick".</summary>
        private static string The(string displayName)
        {
            if (displayName.StartsWith("a ", StringComparison.Ordinal)) return "the " + displayName.Substring(2);
            if (displayName.StartsWith("an ", StringComparison.Ordinal)) return "the " + displayName.Substring(3);
            return displayName;
        }

        private static string Say(VerbOutcome outcome)
        {
            switch (outcome)
            {
                case VerbOutcome.OutOfReach: return "out of reach";
                case VerbOutcome.NotThere: return "it is not there now";
                case VerbOutcome.HandsFull: return "your hands are full";
                case VerbOutcome.NothingInHand: return "nothing in your hand";
                case VerbOutcome.NoSuchPlace: return "no such place";
                default: return "not now";
            }
        }
    }
}
