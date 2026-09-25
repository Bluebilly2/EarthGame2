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
        private readonly TrunkBodies _trunks;
        /// <summary>The rocks' bodies (BF.4 stage three): what the crosshair met, when it met one of them.</summary>
        private readonly RockBodies _rocks;
        private readonly UnderstoreyViews _understorey;
        /// <summary>The one ground (BF.4): what the litter lies on as it is drawn, so the crosshair meets a stick where it is.</summary>
        private readonly ClientGround _fine;
        private GameClient _client;
        private EntityViews _entities;
        private string _answer = string.Empty;
        private float _answerUntil;
        private float _now;
        private bool _workHeld;
        private float _workSince;
        /// <summary>Whether a work (BF.2) was begun on the press and not yet let go or ended.</summary>
        private bool _working;

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

        /// <summary>The trunk under the crosshair within reach (BF.3), met through its body, or null.</summary>
        public TrunkNearby? TargetTrunk { get; private set; }

        /// <summary>The rock that stands under the crosshair within reach (BF.4 stage three): named, and a thing in hand put down on it.</summary>
        public StandingRock? TargetRock { get; private set; }

        /// <summary>The tuft of the understorey under the crosshair within reach (BF.3), or null.</summary>
        public UnderstoreyTuft? TargetTuft { get; private set; }

        /// <summary>The cell of the ground the crosshair meets within reach (BF.3), when it meets the ground, or null.</summary>
        public (int Row, int Col)? GroundCell { get; private set; }

        /// <summary>How much further than the reach a tuft's own place may lie: it spreads that far (the server's own allowance).</summary>
        private const double TuftReachM = 0.5;

        /// <summary>The soil's depth a client assumes under a cell it looks at, m: it holds no soil tile, so the offer is the server's to refuse.</summary>
        private const double AssumedSoilM = 1.0;

        /// <summary>Where the crosshair meets the streamed water's surface within reach (FP.1), when no thing is in front of it.</summary>
        public Vector3? WaterAt { get; private set; }

        /// <summary>What the verb line says now.</summary>
        public string Line { get; private set; } = string.Empty;

        public VerbController(GameClient client, EntityViews entities, PlayerController player, Camera camera, HudController hud, HandView hand,
                              IHeightSource ground = null, StreamedWater water = null, TrunkBodies trunks = null, UnderstoreyViews understorey = null, ClientGround fine = null,
                              RockBodies rocks = null)
        {
            _player = player;
            _camera = camera;
            _hud = hud;
            _hand = hand;
            _ground = ground;
            _water = water;
            _trunks = trunks;
            _rocks = rocks;
            _understorey = understorey;
            _fine = fine;
            Rebind(client, entities);
        }

        /// <summary>A new connection (a rejoin): its answers and its hands, and the things it shows.</summary>
        public void Rebind(GameClient client, EntityViews entities)
        {
            if (_client != null)
            {
                _client.IntentAnswered -= OnAnswered;
                _client.CarryingChanged -= OnCarrying;
                _client.WorkStateChanged -= OnWorkState;
            }
            _client = client;
            _entities = entities;
            if (_client == null) return;
            _client.IntentAnswered += OnAnswered;
            _client.CarryingChanged += OnCarrying;
            _client.WorkStateChanged += OnWorkState;
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

        /// <summary>A work's end (BF.2): done or stopped, the server's words are said; while it runs the line is its bar.</summary>
        private void OnWorkState(WorkStateMessage state)
        {
            if (state.Ended == WorkStateMessage.Running) return;
            _working = false;
            if (!string.IsNullOrEmpty(state.Note)) Note(state.Note, NoteSeconds);
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
                TargetTrunk = null;
                TargetRock = null;
                TargetTuft = null;
                GroundCell = null;
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
            // The work button: with a stone in hand on stone it is the knap's wind-up (FP.3): held, the arm winds up; let go, the
            // stone comes down. On anything with an offer it is work (BF.2): begun on the press, let go on the release, run and
            // timed by the server, which says how it went.
            bool knapping = KnapApplies(carrying);
            if (presses.Work && !_workHeld)
            {
                _workSince = now;
                if (!knapping && TryWorkIntent(carrying, out IntentMessage start))
                {
                    _client.SendIntent(start);
                    _working = true;
                }
            }
            WindUp01 = presses.Work && knapping ? Mathf.Clamp01((now - _workSince) / WindUpSeconds) : 0f;
            if (!presses.Work && _workHeld)
            {
                if (knapping) Strike(carrying, Mathf.Clamp01((now - _workSince) / WindUpSeconds));
                if (_working)
                {
                    _client.SendIntent(new IntentMessage { Verb = Verb.StopWork });
                    _working = false;
                }
            }
            _workHeld = presses.Work;
            _hand?.WindUp(knapping ? WindUp01 : _working ? 0.35f + 0.25f * Mathf.PingPong(now * 2.5f, 1f) : 0f);
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
            WorkStateMessage work = _client.WorkState;
            Line = _working && work.Kind != WorkKind.None && work.Ended == WorkStateMessage.Running ? WorkLine(work)
                 : now < _answerUntil && _answer.Length > 0 ? _answer : Offer(carrying);
            _hud?.SetVerb(Line);
        }

        /// <summary>Whether the work button is the knap's (FP.3): a stone in hand and stone aimed at.</summary>
        private bool KnapApplies(CarryingMessage carrying) =>
            TryInHand(carrying, out CarriedThing held) && KnappingItems.IsHammer(held.Definition)
            && ((Target != null && KnappingItems.IsStone(Target.Definition)) || (TargetLying.HasValue && TargetLying.Value.Kind == StandLayout.Kind.Cobble));

        /// <summary>The first work that can be done to a thing with what is in hand, by the engine's own judgement (BF.2), or none.</summary>
        private WorkOffer? WorkOfferFor(CarryingMessage carrying, Definition target, in ThingState targetState)
        {
            Definition tool = null;
            ThingState toolState = default;
            if (TryInHand(carrying, out CarriedThing held))
            {
                tool = held.Definition;
                toolState = held.Item.State;
            }
            return Work.First(Work.Offers(tool, toolState, target, targetState));
        }

        /// <summary>With a strip or a cord in hand, the twist offered on a strip in another place of the hands (BF.2).</summary>
        private bool TryTwist(CarryingMessage carrying, out byte place, out WorkOffer offer)
        {
            place = 0;
            offer = default;
            if (!TryInHand(carrying, out CarriedThing held) || (held.Definition.Substance != Substance.Bark && held.Definition.Substance != Substance.Cord)) return false;
            foreach (CarriedThing t in carrying.Things)
            {
                if (t.Place == carrying.Hand || t.Definition.Substance != Substance.Bark) continue;
                offer = Work.Judge(WorkKind.Twist, held.Definition, held.Item.State, t.Definition, t.Item.State);
                if (!offer.Possible) continue;
                place = t.Place;
                return true;
            }
            return false;
        }

        /// <summary>The cover byte of a cell, read off the cover tile the client holds for it; zero when none is held.</summary>
        private byte CoverCodeAt(int row, int col)
        {
            if (_client?.Tiles == null || _client.Grid == null) return 0;
            ReceivedTile here = _client.Tiles.Holding(TileLayer.GroundCover, _client.Grid.ForPosition(_player.State.East, _player.State.North));
            double cellM = here != null ? here.CellM : 4.0;
            StandLayout.CellCentre(row, col, cellM, _client.Grid.ExtentM, out double east, out double north);
            ReceivedTile cover = _client.Tiles.Holding(TileLayer.GroundCover, _client.Grid.ForPosition(east, north));
            if (cover?.Codes == null) return 0;
            int x = (int)Math.Round((east - cover.OriginEast) / cover.CellM);
            int z = (int)Math.Round((north - cover.OriginNorth) / cover.CellM);
            if (x < 0 || z < 0 || x >= cover.Posts || z >= cover.Posts) return 0;
            return cover.Codes[z, x];
        }

        /// <summary>The cell of the world's raster a point on the ground lies in, by the cover tile's cell; false when no cover tile is held there.</summary>
        private bool TryCellOf(Vector3 point, out int row, out int col)
        {
            row = col = -1;
            if (_client?.Tiles == null || _client.Grid == null) return false;
            ReceivedTile cover = _client.Tiles.Holding(TileLayer.GroundCover, _client.Grid.ForPosition(point.x, point.z));
            if (cover == null || !(cover.CellM > 0.0)) return false;
            TileCodec.CellOf(_client.Grid.ExtentM, cover.CellM, point.x, point.z, out row, out col);
            return row >= 0 && col >= 0;
        }

        /// <summary>A trunk as the work model's target (BF.3): its plant's definition and the state the server would make of it, from the tiles and the changes the client holds.</summary>
        private void TrunkTarget(in TrunkNearby trunk, out Definition definition, out ThingState state)
        {
            (byte flags, byte cut) = _client.Changes.TrunkOf(trunk.Row, trunk.Col);
            definition = StandingThings.TrunkDefinition(trunk.Species);
            state = StandingThings.TrunkState(trunk.Species, trunk.HeightM, flags, cut, GroundCovers.QuarterOf(CoverCodeAt(trunk.Row, trunk.Col)));
        }

        /// <summary>A tuft as the work model's target (BF.3): the shape's representative plant (the client holds no understorey layer) and its state.</summary>
        private bool TuftTarget(in UnderstoreyTuft tuft, out Definition definition, out ThingState state)
        {
            definition = StandingThings.TuftDefinition(tuft.Shape, null);
            state = StandingThings.TuftState(tuft.HeightM, GroundCovers.QuarterOf(CoverCodeAt(tuft.Row, tuft.Col)));
            return definition != null;
        }

        /// <summary>The cell of the ground under the crosshair as a work on it reads it (BF.3), from the tiles and the changes the client holds; the soil's depth is assumed, the server's to refuse.</summary>
        private bool TryGroundSite(out GroundSite site)
        {
            site = default;
            if (!GroundCell.HasValue || !Ground.HasValue) return false;
            (int row, int col) = GroundCell.Value;
            byte code = CoverCodeAt(row, col);
            if (code == 0) return false;
            ReceivedTile here = _client.Tiles.Holding(TileLayer.GroundCover, _client.Grid.ForPosition(_player.State.East, _player.State.North));
            double cellM = here != null ? here.CellM : 4.0;
            site.Row = row;
            site.Col = col;
            site.CoverCode = code;
            site.Cover = GroundCovers.CoverOf(code);
            site.Quarter = GroundCovers.QuarterOf(code);
            site.Tufts = Tufts.OnCell(code, cellM, row, col);
            site.CellM = cellM;
            if (_client.Changes.TryGet(row, col, out CellChange change))
            {
                site.TuftsTaken = change.Tufts;
                site.Cleared = (change.GroundFlags & GroundChange.Cleared) != 0;
                site.DugCm = change.DugCm;
            }
            site.SoilDepthM = AssumedSoilM;
            // A boulder on the cell (BF.4 stage three), as the server decides it: nothing digs under one.
            site.RockStands = _fine != null && ClientRocks.TryOfCell(_client.Tiles.Holding, _client.Grid, _fine.Undug, cellM, row, col, out _);
            Vector3 at = Ground.Value;
            if (_water != null && _ground != null)
            {
                double surface = _water.HeightAt(at.x, at.z), ground = _ground.HeightAt(at.x, at.z);
                site.WaterDepthM = !double.IsNaN(surface) && !double.IsNaN(ground) && surface > ground + WorldState.StandingWaterM ? surface - ground : 0.0;
            }
            StandLayout.CellCentre(row, col, cellM, _client.Grid.ExtentM, out double east, out double north);
            site.Centre = new Double3(east, at.y, north);
            return true;
        }

        /// <summary>The first work on the ground under the crosshair that can be done with what is in hand (BF.3), or none.</summary>
        private WorkOffer? GroundOfferFor(CarryingMessage carrying)
        {
            if (!TryGroundSite(out GroundSite site)) return null;
            Definition tool = null;
            ThingState toolState = default;
            if (TryInHand(carrying, out CarriedThing held))
            {
                tool = held.Definition;
                toolState = held.Item.State;
            }
            return Work.First(Work.GroundOffers(tool, toolState, site));
        }

        /// <summary>
        /// The work intent the press begins, in the order the verb line offers them: the thing aimed at (an item, one of the
        /// litter); else, with a strip or a cord in hand, the twist on a strip carried in another place (BF.2), which is about
        /// the hands and not the eye, so a tuft or a trunk in view does not take it (review, 2026-09-23); else the trunk; else
        /// the tuft; else, when the tuft has nothing to be done to it, the ground under the crosshair.
        /// </summary>
        private bool TryWorkIntent(CarryingMessage carrying, out IntentMessage intent)
        {
            intent = default;
            if (Target != null)
            {
                WorkOffer? offer = WorkOfferFor(carrying, Target.Definition, Target.HasItem ? Target.Item.State : default);
                if (!offer.HasValue) return false;
                intent = new IntentMessage { Verb = Verb.Work, Kind = offer.Value.Kind, Target = IntentMessage.TargetEntity, EntityId = Target.Id.Value };
                return true;
            }
            if (TargetLying.HasValue)
            {
                LyingThing thing = TargetLying.Value;
                LyingSite site = LyingSiteReader.Of(_client.Tiles, _client.Grid, thing);
                WorkOffer? offer = WorkOfferFor(carrying, LyingProperties.DefinitionOf(thing.Kind, site), LyingProperties.StateOf(thing, site));
                if (!offer.HasValue) return false;
                intent = new IntentMessage { Verb = Verb.Work, Kind = offer.Value.Kind, Target = IntentMessage.TargetLying, Lying = thing };
                return true;
            }
            if (TryTwist(carrying, out byte place, out _))
            {
                intent = new IntentMessage { Verb = Verb.Work, Kind = WorkKind.Twist, Target = IntentMessage.TargetPlace, Place = place };
                return true;
            }
            if (TargetTrunk.HasValue)
            {
                TrunkNearby trunk = TargetTrunk.Value;
                TrunkTarget(trunk, out Definition kind, out ThingState state);
                WorkOffer? offer = WorkOfferFor(carrying, kind, state);
                if (!offer.HasValue) return false;
                intent = new IntentMessage { Verb = Verb.Work, Kind = offer.Value.Kind, Target = IntentMessage.TargetTrunk, Row = trunk.Row, Col = trunk.Col };
                return true;
            }
            if (TargetTuft.HasValue)
            {
                UnderstoreyTuft tuft = TargetTuft.Value;
                if (TuftTarget(tuft, out Definition kind, out ThingState state))
                {
                    WorkOffer? offer = WorkOfferFor(carrying, kind, state);
                    if (offer.HasValue)
                    {
                        intent = new IntentMessage { Verb = Verb.Work, Kind = offer.Value.Kind, Target = IntentMessage.TargetTuft, Row = tuft.Row, Col = tuft.Col, Index = tuft.Index };
                        return true;
                    }
                }
                // A tuft with nothing to be done to it (a woody heath bush) leaves the work button to the ground it stands on.
            }
            if (GroundCell.HasValue)
            {
                WorkOffer? ground = GroundOfferFor(carrying);
                if (!ground.HasValue) return false;
                intent = new IntentMessage { Verb = Verb.Work, Kind = ground.Value.Kind, Target = IntentMessage.TargetGround, Row = GroundCell.Value.Row, Col = GroundCell.Value.Col };
                return true;
            }
            return false;
        }

        /// <summary>The line while a work runs: its words, a bar of ten, and the seconds left at the body's capacity.</summary>
        private static string WorkLine(WorkStateMessage state)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(state.Progress01 * 10f), 0, 10);
            return state.Note + "  " + new string('|', filled) + new string('.', 10 - filled) + "  " + Mathf.CeilToInt(state.SecondsLeft) + " s";
        }

        /// <summary>
        /// What the crosshair is on within reach: the nearest thing, an entity or one of the litter (M1.5b), or else the
        /// ground. A thing in front of the ground hides it, whether or not the thing is in reach; a trunk (BF.3) hides it too,
        /// and a tuft of the understorey does not.
        /// </summary>
        private void Aim()
        {
            Target = null;
            TargetLying = null;
            TargetTrunk = null;
            TargetRock = null;
            TargetTuft = null;
            GroundCell = null;
            Ground = null;
            WaterAt = null;
            Transform eye = _camera.transform;
            Ray ray = new Ray(eye.position, eye.forward);
            float within = (float)Hands.ReachM + LookBeyondM;
            // The ground, or a trunk's body (BF.3), whichever the ray meets first; then the things in front of either.
            bool hitAny = Physics.Raycast(ray, out RaycastHit hit, within, Layers.Mask(Layers.Terrain) | Layers.Mask(Layers.Props), QueryTriggerInteraction.Ignore);
            bool onGround = hitAny && hit.collider != null && hit.collider.gameObject.layer == Layers.Terrain;
            TrunkNearby trunkHit = default;
            bool onTrunk = hitAny && !onGround && _trunks != null && _trunks.TryTrunkOf(hit.collider, out trunkHit);
            StandingRock rockHit = default;
            bool onRock = hitAny && !onGround && !onTrunk && _rocks != null && _rocks.TryRockOf(hit.collider, out rockHit);
            float nearestM = hitAny ? hit.distance : within;
            int which = onTrunk ? 4 : onRock ? 6 : onGround ? 5 : 0;
            Double3 body = _player.Eye;
            EntityView entity = null;
            if (_entities != null && _entities.Pick(ray, nearestM, out EntityView picked, out float pickedM))
            {
                entity = picked;
                nearestM = pickedM;
                which = 1;
            }
            LyingNearby lying = default;
            if (PickLying(ray, body, ref nearestM, out lying)) which = 2;
            // A tuft is met by its bounds, which are a metre across on a bracken floor and hide whatever lies among them: a thing,
            // an item or one of the litter, is offered before a tuft whenever the ray meets one within reach, so what was put down
            // in the understorey can be taken up again; a tuft in front of a trunk is offered before it.
            UnderstoreyTuft tuftHit = default;
            if (which != 1 && which != 2 && _understorey != null && _understorey.Pick(ray, (float)Hands.ReachM, ref nearestM, out tuftHit)) which = 3;
            switch (which)
            {
                case 1:
                    if (Double3.Distance(body, entity.Position) <= Hands.ReachM + entity.Definition.RadiusM) Target = entity;
                    return;
                case 2:
                {
                    Definition kind = lying.Thing.Kind == StandLayout.Kind.Stick ? DefinitionCatalogue.Stick : DefinitionCatalogue.Cobble;
                    Double3 at = new Double3(lying.Instance.East, lying.Instance.Up, lying.Instance.North);
                    if (Double3.Distance(body, at) <= Hands.ReachM + kind.RadiusM) TargetLying = lying.Thing;
                    return;
                }
                case 3:
                {
                    // A tuft hides nothing (review, 2026-09-23): its bounds are a box of air round a few blades, and a founder looking
                    // through the grass at the ground or the water means them. The tuft takes the work button; use still puts a thing
                    // down on the ground or drinks the water behind it. A trunk behind the tuft hides what lies beyond, as ever.
                    Double3 at = new Double3(tuftHit.East, tuftHit.Up, tuftHit.North);
                    if (Double3.Distance(body, at) <= Hands.ReachM + TuftReachM) TargetTuft = tuftHit;
                    if (onTrunk) return;
                    break;
                }
                case 4:
                {
                    Double3 at = new Double3(hit.point.x, hit.point.y, hit.point.z);
                    if (Double3.Distance(body, at) <= Hands.ReachM + 0.1) TargetTrunk = trunkHit;
                    return;
                }
                case 6:
                {
                    // A rock that stands (BF.4 stage three) hides the ground behind it; it is named, and a thing in hand is put down
                    // where the crosshair meets it, which the server lets rest on its top. No work on the ground is offered on it.
                    Double3 at = new Double3(hit.point.x, hit.point.y, hit.point.z);
                    if (Double3.Distance(body, at) <= Hands.ReachM + 0.1)
                    {
                        TargetRock = rockHit;
                        Ground = hit.point;
                    }
                    return;
                }
            }
            if (onGround && Double3.Distance(body, new Double3(hit.point.x, hit.point.y, hit.point.z)) <= Hands.ReachM)
            {
                Ground = hit.point;
                if (TryCellOf(hit.point, out int row, out int col)) GroundCell = (row, col);
            }

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
            LyingNear.Find(body.X, body.Z, Hands.ReachM + LitterMarginM, _client.Tiles, _client.Grid, _client.Taken, _near, _fine);
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
                ThingState state = Target.HasItem ? Target.Item.State : default;
                string name = ThingWords.Describe(Target.Definition, state);
                if (hammer && KnappingItems.IsStone(Target.Definition)) return name + " — knap (hold to strike harder)" + (full ? "" : ", or pick up");
                WorkOffer? work = WorkOfferFor(carrying, Target.Definition, state);
                if (work.HasValue) return name + " — hold to " + work.Value.Words + (full ? "" : "; use to pick up");
                return name + " — " + (full ? "your hands are full" : "pick up");
            }
            if (TargetLying.HasValue)
            {
                LyingThing thing = TargetLying.Value;
                LyingSite site = LyingSiteReader.Of(_client.Tiles, _client.Grid, thing);
                Definition kind = LyingProperties.DefinitionOf(thing.Kind, site);
                ThingState state = LyingProperties.StateOf(thing, site);
                string name = ThingWords.Describe(kind, state);
                if (hammer && thing.Kind == StandLayout.Kind.Cobble) return name + " — knap (hold to strike harder)" + (full ? "" : ", or pick up");
                WorkOffer? work = WorkOfferFor(carrying, kind, state);
                if (work.HasValue) return name + " — hold to " + work.Value.Words + (full ? "" : "; use to pick up");
                return name + " — " + (full ? "your hands are full" : "pick up");
            }
            // What use does where the crosshair meets water or the ground, which a tuft does not hide (review, 2026-09-23).
            string use = WaterAt.HasValue ? "use to drink"
                       : Ground.HasValue && TryInHand(carrying, out CarriedThing toPut) ? "use to put down " + The(toPut.Definition.DisplayName) : string.Empty;
            if (TryTwist(carrying, out _, out WorkOffer twist)) return Join("hold to " + twist.Words, use);
            // The standing world (BF.3): a trunk or a tuft named by what it is, with the first work that can be done to it.
            if (TargetTrunk.HasValue)
            {
                TrunkNearby trunk = TargetTrunk.Value;
                (byte flags, byte cut) = _client.Changes.TrunkOf(trunk.Row, trunk.Col);
                string name = ThingWords.TrunkWords(trunk.Species, trunk.HeightM, (flags & TrunkChange.BarkTaken) != 0, cut);
                TrunkTarget(trunk, out Definition kind, out ThingState state);
                WorkOffer? work = WorkOfferFor(carrying, kind, state);
                return work.HasValue ? name + " — hold to " + work.Value.Words : name;
            }
            // A rock that stands (BF.4 stage three), named by its stone and form; a thing in hand can be put down on it.
            if (TargetRock.HasValue)
            {
                string name = ThingWords.RockWords(TargetRock.Value);
                return use.Length > 0 ? name + " — " + use : name;
            }
            // The ground (BF.3): cleared with empty hands, dug with a pointed stick; a thing in hand is put down on it.
            WorkOffer? ground = GroundCell.HasValue ? GroundOfferFor(carrying) : null;
            if (TargetTuft.HasValue)
            {
                UnderstoreyTuft tuft = TargetTuft.Value;
                WorkOffer? work = TuftTarget(tuft, out Definition kind, out ThingState state) ? WorkOfferFor(carrying, kind, state) : null;
                // A tuft with nothing to be done to it leaves the work button to the ground it stands on.
                string hold = work.HasValue ? "hold to " + work.Value.Words : ground.HasValue ? "hold to " + ground.Value.Words : string.Empty;
                string both = Join(hold, use);
                return both.Length > 0 ? Tufts.NameOf(tuft.Shape) + " — " + both : Tufts.NameOf(tuft.Shape);
            }
            if (WaterAt.HasValue) return "water — drink";
            if (Ground.HasValue && TryInHand(carrying, out CarriedThing held))
                return (ground.HasValue ? "the ground — hold to " + ground.Value.Words + "; " : "") + "put down " + The(held.Definition.DisplayName);
            if (ground.HasValue) return "the ground — hold to " + ground.Value.Words;
            return string.Empty;
        }

        /// <summary>Two parts of the verb line, joined by a semicolon, either left out when empty.</summary>
        private static string Join(string a, string b) => a.Length == 0 ? b : b.Length == 0 ? a : a + "; " + b;

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
                case VerbOutcome.TooHeavy: return "too heavy to lift";
                default: return "not now";
            }
        }
    }
}
