using System;
using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Shared;
using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>What the client last asked of a blow on stone (FP.3), for a scenario to hold the answer against.</summary>
    public struct KnapAsked
    {
        /// <summary>The intent's sequence, which the answer names.</summary>
        public uint Sequence;
        /// <summary>The wind-up as it went on the wire, 0 to 1 in 255 steps: what the server put into the swing.</summary>
        public double WindUp01;
        public ulong HammerId;
        /// <summary>The core as an item, or 0 when it was one of the litter.</summary>
        public ulong CoreId;
        public LyingThing? CoreLying;
    }

    /// <summary>
    /// CANON's verb rule (2026-08-26) for the verbs a founder has so far (M1.5a): what the right mouse does comes from what
    /// the crosshair is on and what is in hand. On a thing lying within reach it picks it up; with something in hand and
    /// the ground within reach under the crosshair it puts it down there; 1–9 and the wheel choose the hand; Tab opens the
    /// carrying window. Since FP.3 the left mouse works on stone: with a stone in hand and another under the crosshair, a
    /// tap strikes lightly and a press held winds the arm up to a full swing, let go when the button is. The client only
    /// asks; the server commits every verb and answers (<see cref="IntentMessage"/>), and the verb line under the
    /// crosshair says what the mouse would do, or for a moment why it did not, or what the stone did in the physics' own
    /// words.
    ///
    /// <para>Reach is measured as the server measures it, from the body's eye (<see cref="PlayerController.Eye"/>) with the
    /// engine's own <see cref="Hands.ReachM"/>, so the line never offers what the server would refuse; the ray is the
    /// camera's, whose eye is the body's smoothed.</para>
    /// </summary>
    public sealed class VerbController
    {
        /// <summary>How long an answer other than done, or a note, stays on the verb line, s.</summary>
        public const float AnswerSeconds = 2f;

        /// <summary>How long what the stone did stays on the verb line, s: a sentence to read, not a word.</summary>
        public const float NoteSeconds = 3.5f;

        /// <summary>
        /// How long the work button must be held to wind the arm up to a full swing, s (FP.3). A tap is a light blow: the
        /// wind-up is the hold's share of this, and the server puts <see cref="Knapping.SwingEnergyJ"/> of it behind the stone.
        /// </summary>
        public const float WindUpSeconds = 1f;

        /// <summary>A gap in the ticking longer than this (the panel open) lets a held work button go without a blow, s.</summary>
        private const float WorkGapSeconds = 0.5f;

        /// <summary>How far past the reach the crosshair looks, m, so a thing a little beyond it hides the ground behind it rather than offering it.</summary>
        private const float LookBeyondM = 1f;

        /// <summary>How much farther than the reach the litter is searched, m: a thing lies up to half a 4 m cell's diagonal off its cell's centre.</summary>
        private const float LitterMarginM = 3f;

        private readonly List<LyingNearby> _near = new List<LyingNearby>();

        private readonly PlayerController _player;
        private readonly Camera _camera;
        private readonly HudController _hud;
        private readonly HandView _hand;
        private readonly IHeightSource _ground;
        private readonly StreamedWater _water;
        private GameClient _client;
        private EntityViews _entities;
        private string _answer = string.Empty;
        private float _answerUntil;
        private float _now;
        private bool _workHeld;
        private float _workSince;

        /// <summary>The thing under the crosshair within reach, or null.</summary>
        public EntityView Target { get; private set; }

        /// <summary>The blow last asked of the server (FP.3), or null before any.</summary>
        public KnapAsked? LastKnap { get; private set; }

        /// <summary>How far the arm is wound up now, 0 to 1: the work button's hold against <see cref="WindUpSeconds"/>, nothing when it is not held.</summary>
        public float WindUp01 { get; private set; }

        /// <summary>The thing of the litter under the crosshair within reach (M1.5b), when no entity is nearer, or null.</summary>
        public LyingThing? TargetLying { get; private set; }

        /// <summary>Where the crosshair meets the ground within reach, when it does and no thing is in front of it.</summary>
        public Vector3? Ground { get; private set; }

        /// <summary>Where the crosshair meets the streamed water's surface within reach (FP.1), when no thing is in front of it.</summary>
        public Vector3? WaterAt { get; private set; }

        /// <summary>What the verb line says now.</summary>
        public string Line { get; private set; } = string.Empty;

        public VerbController(GameClient client, EntityViews entities, PlayerController player, Camera camera, HudController hud, HandView hand,
                              IHeightSource ground = null, StreamedWater water = null)
        {
            _player = player;
            _camera = camera;
            _hud = hud;
            _hand = hand;
            _ground = ground;
            _water = water;
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
        public void Note(string text) => Note(text, AnswerSeconds);

        private void Note(string text, float seconds)
        {
            _answer = text ?? string.Empty;
            _answerUntil = _now + seconds;
        }

        private void OnAnswered(IntentResultMessage result)
        {
            // What the stone did comes in the server's own words (FP.3); a refusal in the client's; done says nothing.
            if (!string.IsNullOrEmpty(result.Note)) Note(result.Note, NoteSeconds);
            else if (result.Outcome == VerbOutcome.Done) _answer = string.Empty;
            else Note(Say(result.Outcome));
        }

        private void OnCarrying(CarryingMessage carrying)
        {
            _hud?.SetCarrying(carrying.Hand, carrying.Things);
            if (_hand == null) return;
            if (TryInHand(carrying, out CarriedThing held)) _hand.Show(held.Definition, held.Id, held.Item.State);
            else _hand.Hide();
        }

        /// <summary>One frame: the presses taken from the controls acted on, the crosshair's target found, the verb line said.</summary>
        public void Tick(in ControlsFrame presses, float now)
        {
            // A hold that spans a gap in the ticking (the developer's panel open, the hands resting) is let go, not struck.
            if (_workHeld && now - _now > WorkGapSeconds) _workHeld = false;
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
                TargetLying = null;
                Ground = null;
                Line = string.Empty;
                _workHeld = false;
                WindUp01 = 0f;
                _hud?.SetVerb(Line);
                return;
            }
            if (presses.Carrying) _hud?.ToggleCarrying();
            CarryingMessage carrying = _client.Carrying;
            if (presses.HandPlace > 0) _client.SendIntent(new IntentMessage { Verb = Verb.Hold, Place = (byte)presses.HandPlace });
            else if (presses.HandStep != 0) _client.SendIntent(new IntentMessage { Verb = Verb.Hold, Place = Step(carrying, presses.HandStep) });

            Aim();
            // The work button (FP.3): held, the arm winds up; let go, the stone in hand comes down on the stone aimed at.
            if (presses.Work && !_workHeld) _workSince = now;
            WindUp01 = presses.Work ? Mathf.Clamp01((now - _workSince) / WindUpSeconds) : 0f;
            if (!presses.Work && _workHeld) Strike(carrying, Mathf.Clamp01((now - _workSince) / WindUpSeconds));
            _workHeld = presses.Work;
            _hand?.WindUp(WindUp01);
            if (presses.Use)
            {
                // The punch on use (M1.5c): the hand swings whether or not there is anything for it to do.
                _hand?.Strike();
                if (Target != null) _client.SendIntent(new IntentMessage { Verb = Verb.PickUp, Target = IntentMessage.TargetEntity, EntityId = Target.Id.Value });
                else if (TargetLying.HasValue) _client.SendIntent(new IntentMessage { Verb = Verb.PickUp, Target = IntentMessage.TargetLying, Lying = TargetLying.Value });
                else if (WaterAt.HasValue)
                {
                    // A drink (FP.1): the server says whether the water gives, and why the sea does not.
                    Vector3 at = WaterAt.Value;
                    _client.SendIntent(new IntentMessage { Verb = Verb.Drink, East = at.x, Up = at.y, North = at.z });
                }
                else if (Ground.HasValue && TryInHand(carrying, out _))
                {
                    Vector3 at = Ground.Value;
                    _client.SendIntent(new IntentMessage { Verb = Verb.PutDown, East = at.x, Up = at.y, North = at.z });
                }
            }
            Line = now < _answerUntil && _answer.Length > 0 ? _answer : Offer(carrying);
            _hud?.SetVerb(Line);
        }

        /// <summary>
        /// What the crosshair is on within reach: the nearest thing, an entity or one of the litter (M1.5b), or else the
        /// ground. A thing in front of the ground hides it, whether or not the thing is in reach.
        /// </summary>
        private void Aim()
        {
            Target = null;
            TargetLying = null;
            Ground = null;
            WaterAt = null;
            Transform eye = _camera.transform;
            Ray ray = new Ray(eye.position, eye.forward);
            float within = (float)Hands.ReachM + LookBeyondM;
            bool onGround = Physics.Raycast(ray, out RaycastHit hit, within, Layers.Mask(Layers.Terrain), QueryTriggerInteraction.Ignore);
            float nearestM = onGround ? hit.distance : within;
            Double3 body = _player.Eye;
            EntityView entity = null;
            if (_entities != null && _entities.Pick(ray, nearestM, out EntityView picked, out float pickedM))
            {
                entity = picked;
                nearestM = pickedM;
            }
            if (PickLying(ray, body, ref nearestM, out LyingNearby lying))
            {
                Definition kind = lying.Thing.Kind == StandLayout.Kind.Stick ? DefinitionCatalogue.Stick : DefinitionCatalogue.Cobble;
                Double3 at = new Double3(lying.Instance.East, lying.Instance.Up, lying.Instance.North);
                if (Double3.Distance(body, at) <= Hands.ReachM + kind.RadiusM) TargetLying = lying.Thing;
                return;
            }
            if (entity != null)
            {
                if (Double3.Distance(body, entity.Position) <= Hands.ReachM + entity.Definition.RadiusM) Target = entity;
                return;
            }
            if (onGround && Double3.Distance(body, new Double3(hit.point.x, hit.point.y, hit.point.z)) <= Hands.ReachM) Ground = hit.point;

            // Water under the crosshair within reach (FP.1): the eye's ray walked out a quarter of a metre at a time until it
            // goes under the streamed water's surface, or under the ground first, which is the bank.
            if (_water == null || _ground == null) return;
            Vector3 origin = eye.position, direction = eye.forward;
            for (float along = 0.25f; along <= (float)Hands.ReachM; along += 0.25f)
            {
                Vector3 p = origin + direction * along;
                double ground = _ground.HeightAt(p.x, p.z);
                double surface = _water.HeightAt(p.x, p.z);
                if (!double.IsNaN(surface) && surface > ground + WorldState.StandingWaterM && p.y <= surface)
                {
                    WaterAt = new Vector3(p.x, (float)surface, p.z);
                    return;
                }
                if (!double.IsNaN(ground) && p.y < ground) return;
            }
        }

        /// <summary>
        /// The blow (FP.3): the stone in hand on the stone under the crosshair, an item or one of the litter, with the wind-up
        /// the hold earned. Nothing is sent when there is no stone in hand or none aimed at; the hand swings whatever the
        /// server makes of it, as a use does.
        /// </summary>
        private void Strike(CarryingMessage carrying, float windUp01)
        {
            if (!TryInHand(carrying, out CarriedThing held) || !KnappingItems.IsHammer(held.Definition)) return;
            IntentMessage intent = new IntentMessage { Verb = Verb.Knap, WindUp = IntentMessage.WindUpOf(windUp01) };
            KnapAsked asked = new KnapAsked { WindUp01 = intent.WindUp01, HammerId = held.Id };
            if (Target != null && KnappingItems.IsStone(Target.Definition))
            {
                intent.Target = IntentMessage.TargetEntity;
                intent.EntityId = Target.Id.Value;
                asked.CoreId = Target.Id.Value;
            }
            else if (TargetLying.HasValue && TargetLying.Value.Kind == StandLayout.Kind.Cobble)
            {
                intent.Target = IntentMessage.TargetLying;
                intent.Lying = TargetLying.Value;
                asked.CoreLying = TargetLying.Value;
            }
            else return;
            _hand?.Strike();
            asked.Sequence = _client.SendIntent(intent);
            LastKnap = asked;
        }

        /// <summary>
        /// The nearest thing of the litter the ray meets before a distance, by its mesh's bounds where it is drawn (M1.5b);
        /// the distance becomes where it was met.
        /// </summary>
        private bool PickLying(Ray ray, Double3 body, ref float nearestM, out LyingNearby best)
        {
            best = default;
            _near.Clear();
            LyingNear.Find(body.X, body.Z, Hands.ReachM + LitterMarginM, _client.Tiles, _client.Grid, _client.Taken, _near);
            bool found = false;
            foreach (LyingNearby n in _near)
            {
                ItemLooks.LyingLook(n.Thing.Kind, n.Instance.Variant, out Mesh mesh, out float scale);
                Matrix4x4 toLocal = Matrix4x4.TRS(new Vector3(n.Instance.East, n.Instance.Up, n.Instance.North), Quaternion.Euler(0f, n.Instance.YawDeg, 0f),
                                                  Vector3.one * scale).inverse;
                if (!EntityViews.Meets(ray, mesh.bounds, toLocal, out float metres) || metres > nearestM) continue;
                nearestM = metres;
                best = n;
                found = true;
            }
            return found;
        }

        /// <summary>
        /// What the mouse would do now, in words: the right button's use, and with a stone in hand on stone, the left button's
        /// blow (FP.3). The thing is named by what it is (BF.1, <see cref="ThingWords"/>): a lying thing by what its place says
        /// of it, read from the tiles by the server's own rule, and a thing in the world by the state it carries.
        /// </summary>
        private string Offer(CarryingMessage carrying)
        {
            bool full = carrying.Things != null && carrying.Things.Length >= Hands.Places;
            bool hammer = TryInHand(carrying, out CarriedThing inHand) && KnappingItems.IsHammer(inHand.Definition);
            if (Target != null)
            {
                string name = ThingWords.Describe(Target.Definition, Target.HasItem ? Target.Item.State : default);
                if (hammer && KnappingItems.IsStone(Target.Definition)) return name + " — knap (hold to strike harder)" + (full ? "" : ", or pick up");
                return name + " — " + (full ? "your hands are full" : "pick up");
            }
            if (TargetLying.HasValue)
            {
                LyingThing thing = TargetLying.Value;
                LyingSite site = LyingSiteReader.Of(_client.Tiles, _client.Grid, thing);
                string name = ThingWords.Describe(LyingProperties.DefinitionOf(thing.Kind, site), LyingProperties.StateOf(thing, site));
                if (hammer && thing.Kind == StandLayout.Kind.Cobble) return name + " — knap (hold to strike harder)" + (full ? "" : ", or pick up");
                return name + " — " + (full ? "your hands are full" : "pick up");
            }
            if (WaterAt.HasValue) return "water — drink";
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
                case VerbOutcome.Salt: return "the sea will not drink: salt";
                case VerbOutcome.NoWater: return "nothing to drink there";
                // A blow's own outcomes come with the server's words; these are the two refusals that have none.
                case VerbOutcome.NoHammer: return "nothing in hand to strike with";
                case VerbOutcome.NotStone: return "that is no stone to knap";
                default: return "not now";
            }
        }
    }
}
