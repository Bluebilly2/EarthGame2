using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Shared;
using UnityEngine;

namespace EarthGame.Client
{
    public sealed partial class Recorder
    {
        /// <summary>The world changes (BF.3 promise 7): see <see cref="RunChanges"/>.</summary>
        public const string ChangesScenario = "changes";

        /// <summary>How far round the wake the scenario looks for a trunk or a tuft to work on, m, and walks to it.</summary>
        private const double ChangesSearchM = 40.0;
        private const double ChangesWalkSeconds = 40.0;
        /// <summary>How long a work is given to end before the scenario calls it an error, s: the strip's thirty seconds plus room.</summary>
        private const double ChangesWorkSeconds = 60.0;
        /// <summary>A felling cut the scenario waits out, s; a longer one is begun, held for a minute and let go, its progress kept.</summary>
        private const double ChangesCutMostSeconds = 600.0, ChangesCutHoldSeconds = 60.0;
        private const double ChangesAimSeconds = 4.0, ChangesArriveSeconds = 6.0;

        private readonly List<TrunkNearby> _changesTrunks = new List<TrunkNearby>();
        private readonly List<UnderstoreyTuft> _changesTufts = new List<UnderstoreyTuft>();
        private EntityView _changesThing;

        /// <summary>
        /// The world changes (BF.3 promise 7), run by <c>-eg-scenario changes</c> in a development game. At the wake with a full
        /// body: the nearest trunk whose bark strips is walked to and stripped ("changes-stripped"); a keen flake is set down by
        /// the panel's deed and taken up, the nearest sedge clump is walked to and its fibre cut ("changes-fibre"), two strips
        /// are taken up and laid into cord ("changes-cord"); with empty hands a tussock or frond within reach is pulled, and the
        /// cell under the crosshair is cleared ("changes-cleared"); a stick of the litter is pointed with the flake, taken up,
        /// and the cleared cell is dug with it ("changes-dug"); a chopper is set down and taken up, and the nearest small tree is
        /// cut: through, when the offer is under ten minutes ("changes-felled"), else for a minute and let go, the cut kept
        /// ("changes-cut"). Every work is a <c>work</c> record with the offer's seconds, the answer and the end's words; every
        /// change the client is told is a <c>change</c> record. The exit is 0 when the strip, the fibre, the cord, the clearing
        /// and the dig were done, every frame written and nothing logged as an error; the felling is recorded either way.
        /// </summary>
        private IEnumerator RunChanges()
        {
            if (_client == null || _script == null || _verbs == null || _player == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the changes scenario has no client, no script, no verbs or no founder to drive"));
                _running = false;
                Finish(1);
                yield break;
            }
            double from = T;
            while (_ready != null && !_ready() && T < from + ReadyTimeoutSeconds) yield return null;
            // A development game's leave is the server's answer (M1.E), which comes after the join: it is waited for.
            double leaveFrom = T;
            while (!_player.FlightAllowed && T < leaveFrom + 6.0) yield return null;
            if (!_player.FlightAllowed)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the changes scenario needs a development game (-eg-dev), and the server gave no leave"));
                _running = false;
                Finish(1);
                yield break;
            }
            yield return Wait(1.5);
            WorkStateMessage ended = default;
            bool got = false;
            void OnState(WorkStateMessage s)
            {
                if (s.Ended == WorkStateMessage.Running) return;
                ended = s;
                got = true;
            }
            void OnChange(CellChange c) => _log.Record(T, Tick, "change", new JsonObject().With("row", c.Row).With("col", c.Col).With("layers", (int)c.Layers)
                .With("tufts", (int)c.Tufts).With("trunk_flags", (int)c.TrunkFlags).With("trunk_cut", (int)c.TrunkCut).With("ground_flags", (int)c.GroundFlags).With("dug_cm", (int)c.DugCm));
            _client.WorkStateChanged += OnState;
            _client.ChangesChanged += OnChange;
            _client.SendDevSetting(DevSettings.StandAtWake, 0.0);
            _client.SendDevSetting(DevSettings.FounderWater, 1.0);
            _client.SendDevSetting(DevSettings.ClockScale, 1.0);
            double toldFrom = T;
            while (_client.LastWater01 < 0.999 && T < toldFrom + 3.0) yield return null;
            _script.PitchTargetDeg = 0f;
            yield return Wait(1.0);
            int captures = 0;
            bool stripped = false, fibre = false, corded = false, pulled = false, cleared = false, dug = false, felled = false;
            int cutKept = 0;
            (int Row, int Col)? clearedCell = null;

            // 0. The ground drawn is the one ground (BF.4): the Terrain met by rays round the founder, beside the function the
            // tiles give and the raster under it.
            _log.Record(T, Tick, "ground", GroundProbe());

            // 1. The nearest trunk whose bark strips, walked to and stripped with empty hands.
            TrunkNearby? bark = NearestTrunk(t => t.Species != null && t.Species.StrippableBarkM > 0.0 && (_client.Changes.TrunkOf(t.Row, t.Col).Flags & TrunkChange.BarkTaken) == 0, shortest: false);
            if (bark == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "no trunk whose bark strips within " + ChangesSearchM + " m of the wake"));
            }
            else
            {
                TrunkNearby trunk = bark.Value;
                yield return WalkTo(trunk.East, trunk.North, 2.2, ChangesWalkSeconds, "the trunk to strip");
                got = false;
                yield return BeginTrunkWork(WorkKind.StripTrunk, trunk);
                double until = T + ChangesWorkSeconds;
                while (T < until && !got) yield return null;
                stripped = got && ended.Ended == WorkStateMessage.Done;
                _log.Record(T, Tick, "work", WorkRecord("strip-trunk", StandingWords(trunk), got ? ended : default, got));
                yield return Wait(1.0);
                yield return Capture("changes-stripped");
                captures++;
            }

            // 2. A keen flake from the panel, set down a quarter turn from the trunk and taken up.
            _script.YawTargetDeg += 90f;
            _script.PitchTargetDeg = 0f;
            yield return Wait(0.8);
            yield return SetDownThing(DevSettings.SpawnKeenFlake, DefinitionCatalogue.FlakeOf(StoneType.Silcrete), "a keen flake");
            EntityView flake = _changesThing;
            if (flake != null) yield return TakeUp(flake, "the keen flake");

            // 3. The nearest sedge clump, its fibre cut with the flake.
            UnderstoreyTuft? clump = flake != null && Carries(flake.Id.Value) ? NearestTuft(t => t.Shape == TuftShape.Clump) : null;
            if (clump == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", flake == null ? "no keen flake to cut with" : "no sedge clump within " + ChangesSearchM + " m of the founder"));
            }
            else
            {
                UnderstoreyTuft tuft = clump.Value;
                yield return WalkTo(tuft.East, tuft.North, 2.0, ChangesWalkSeconds, "the sedge clump");
                got = false;
                yield return BeginTuftWork(WorkKind.CutFibre, tuft);
                double until = T + ChangesWorkSeconds;
                while (T < until && !got) yield return null;
                fibre = got && ended.Ended == WorkStateMessage.Done;
                _log.Record(T, Tick, "work", WorkRecord("cut-fibre", Tufts.NameOf(tuft.Shape), got ? ended : default, got));
                yield return Wait(1.0);
                yield return Capture("changes-fibre");
                captures++;

                // 4. Two strips of fibre into the hands, and laid into cord: the hand holds one, the other lies in a place of its own.
                List<EntityView> strips = NearestOf(Substance.Fibre, 2);
                foreach (EntityView strip in strips) yield return TakeUp(strip, "a fibre strip");
                byte held = strips.Count > 0 ? (byte)PlaceOf(strips[0].Id.Value) : (byte)0;
                byte other = strips.Count > 1 ? (byte)PlaceOf(strips[1].Id.Value) : (byte)0;
                if (held != 0 && other != 0)
                {
                    _client.SendIntent(new IntentMessage { Verb = Verb.Hold, Place = held });
                    yield return Wait(0.4);
                    got = false;
                    uint sequence = _client.SendIntent(new IntentMessage { Verb = Verb.Work, Kind = WorkKind.Twist, Target = IntentMessage.TargetPlace, Place = other });
                    until = T + ChangesWorkSeconds;
                    while (T < until && !got) yield return null;
                    corded = got && ended.Ended == WorkStateMessage.Done && HoldsA(Substance.Cord);
                    _log.Record(T, Tick, "work", new JsonObject().With("work", "twist-fibre").With("sequence", (long)sequence).With("seconds", (double)_client.LastIntentResult.Seconds)
                        .With("ended", got ? (int)ended.Ended : -1).With("words", got ? ended.Note : string.Empty).With("held", HeldWords()));
                    yield return Wait(0.6);
                    yield return Capture("changes-cord");
                    captures++;
                }
                else
                {
                    _errors++;
                    _log.Record(T, Tick, "error", new JsonObject().With("message", "two fibre strips were not taken into the hands: " + strips.Count + " found"));
                }
            }

            // 5. With empty hands, a tussock or a frond within reach pulled up whole.
            _client.SendIntent(new IntentMessage { Verb = Verb.Hold, Place = 0 });
            yield return Wait(0.4);
            UnderstoreyTuft? pull = NearestTuft(t => Tufts.Pullable(t.Shape) && t.Shape != TuftShape.Clump);
            if (pull != null)
            {
                UnderstoreyTuft tuft = pull.Value;
                yield return WalkTo(tuft.East, tuft.North, 2.0, ChangesWalkSeconds, "the tuft to pull");
                got = false;
                yield return BeginTuftWork(WorkKind.PullTuft, tuft);
                double until = T + ChangesWorkSeconds;
                while (T < until && !got) yield return null;
                pulled = got && ended.Ended == WorkStateMessage.Done;
                _log.Record(T, Tick, "work", WorkRecord("pull-tuft", Tufts.NameOf(tuft.Shape), got ? ended : default, got));
            }

            // 6. The ground under the crosshair, a pace ahead and a quarter turn from the bundle just pulled, cleared of its tufts with empty hands.
            _script.YawTargetDeg += 90f;
            _script.PitchTargetDeg = 40f;
            double aimFrom = T;
            while (T < aimFrom + ChangesAimSeconds && !_verbs.GroundCell.HasValue) yield return null;
            if (_verbs.GroundCell.HasValue)
            {
                (int Row, int Col) cell = _verbs.GroundCell.Value;
                got = false;
                uint sequence = _client.SendIntent(new IntentMessage { Verb = Verb.Work, Kind = WorkKind.ClearGround, Target = IntentMessage.TargetGround, Row = cell.Row, Col = cell.Col });
                double until = T + ChangesWorkSeconds;
                while (T < until && !got) yield return null;
                cleared = got && ended.Ended == WorkStateMessage.Done;
                if (cleared) clearedCell = cell;
                _log.Record(T, Tick, "work", new JsonObject().With("work", "clear-ground").With("row", cell.Row).With("col", cell.Col).With("sequence", (long)sequence)
                    .With("seconds", (double)_client.LastIntentResult.Seconds).With("answer", (int)_client.LastIntentResult.Outcome)
                    .With("ended", got ? (int)ended.Ended : -1).With("words", got ? ended.Note : string.Empty));
                yield return Wait(1.0);
                yield return Capture("changes-cleared");
                captures++;
            }
            else
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the crosshair found no cell of the ground within reach; the line was '" + _verbs.Line + "'"));
            }

            // 7. A stick of the litter pointed with the flake, taken up; 8. the cleared cell dug with it.
            _script.PitchTargetDeg = 0f;
            if (flake != null && Carries(flake.Id.Value))
            {
                _client.SendIntent(new IntentMessage { Verb = Verb.Hold, Place = (byte)PlaceOf(flake.Id.Value) });
                yield return Wait(0.4);
                LyingNearby? stick = NearestPointable();
                if (stick == null)
                {
                    _errors++;
                    _log.Record(T, Tick, "error", new JsonObject().With("message", "no stick of the litter thin enough to point lay within " + ChangesSearchM + " m"));
                }
                else
                {
                    yield return WalkTo(stick.Value.Instance.East, stick.Value.Instance.North, 2.0, ChangesWalkSeconds, "the stick to point");
                    stick = NearestPointable();
                    got = false;
                    if (stick != null) yield return BeginLyingWork(WorkKind.Point, stick.Value.Thing);
                    double until = T + ChangesWorkSeconds;
                    while (T < until && !got) yield return null;
                    if (stick != null) _log.Record(T, Tick, "work", WorkRecord("point", stick.Value.Thing, got ? ended : default, got));
                    yield return Wait(0.8);
                    EntityView pointed = NearestPointedStick();
                    if (pointed != null)
                    {
                        yield return TakeUp(pointed, "the pointed stick");
                        if (Carries(pointed.Id.Value))
                        {
                            _client.SendIntent(new IntentMessage { Verb = Verb.Hold, Place = (byte)PlaceOf(pointed.Id.Value) });
                            yield return Wait(0.4);
                            (int Row, int Col)? target = clearedCell;
                            if (target == null)
                            {
                                _script.PitchTargetDeg = 40f;
                                aimFrom = T;
                                while (T < aimFrom + ChangesAimSeconds && !_verbs.GroundCell.HasValue) yield return null;
                                target = _verbs.GroundCell;
                            }
                            else
                            {
                                _script.PitchTargetDeg = 40f;
                                yield return Wait(0.6);
                            }
                            if (target != null)
                            {
                                got = false;
                                uint sequence = _client.SendIntent(new IntentMessage { Verb = Verb.Work, Kind = WorkKind.Dig, Target = IntentMessage.TargetGround, Row = target.Value.Row, Col = target.Value.Col });
                                until = T + ChangesWorkSeconds;
                                while (T < until && !got) yield return null;
                                dug = got && ended.Ended == WorkStateMessage.Done;
                                _log.Record(T, Tick, "work", new JsonObject().With("work", "dig").With("row", target.Value.Row).With("col", target.Value.Col).With("sequence", (long)sequence)
                                    .With("seconds", (double)_client.LastIntentResult.Seconds).With("answer", (int)_client.LastIntentResult.Outcome)
                                    .With("ended", got ? (int)ended.Ended : -1).With("words", got ? ended.Note : string.Empty).With("dug_cm", (int)_client.Changes.GroundOf(target.Value.Row, target.Value.Col).DugCm));
                                yield return Wait(1.0);
                                if (dug) _log.Record(T, Tick, "hollow", HollowProbe(target.Value.Row, target.Value.Col));
                                yield return Capture("changes-dug");
                                captures++;
                            }
                        }
                    }
                    else
                    {
                        _errors++;
                        _log.Record(T, Tick, "error", new JsonObject().With("message", "the pointed stick was not found lying after the work"));
                    }
                }
            }

            // 9. A chopper from the panel, set down a quarter turn from the hole, taken up; the nearest small tree cut through, or cut for a minute and the cut kept.
            _script.YawTargetDeg += 90f;
            _script.PitchTargetDeg = 0f;
            yield return Wait(0.8);
            yield return SetDownThing(DevSettings.SpawnChopper, DefinitionCatalogue.CobbleOf(StoneType.Silcrete), "a chopper");
            EntityView chopper = _changesThing;
            if (chopper != null) yield return TakeUp(chopper, "the chopper");
            TrunkNearby? small = chopper != null && Carries(chopper.Id.Value) ? NearestTrunk(t => (_client.Changes.TrunkOf(t.Row, t.Col).Flags & TrunkChange.Felled) == 0, shortest: true) : null;
            if (small == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", chopper == null ? "no chopper to cut with" : "no trunk within " + ChangesSearchM + " m to cut"));
            }
            else
            {
                _client.SendIntent(new IntentMessage { Verb = Verb.Hold, Place = (byte)PlaceOf(chopper.Id.Value) });
                yield return Wait(0.4);
                TrunkNearby trunk = small.Value;
                yield return WalkTo(trunk.East, trunk.North, 2.2, ChangesWalkSeconds, "the tree to cut");
                got = false;
                yield return BeginTrunkWork(WorkKind.CutTrunk, trunk);
                float offered = _client.LastIntentResult.Seconds;
                bool through = _client.LastIntentResult.Outcome == VerbOutcome.Done && offered <= ChangesCutMostSeconds;
                double until = T + (through ? offered + 20.0 : ChangesCutHoldSeconds);
                while (T < until && !got) yield return null;
                if (!through && !got)
                {
                    _client.SendIntent(new IntentMessage { Verb = Verb.StopWork });
                    double stopFrom = T;
                    while (T < stopFrom + 3.0 && !got) yield return null;
                }
                felled = got && ended.Ended == WorkStateMessage.Done;
                cutKept = _client.Changes.TrunkOf(trunk.Row, trunk.Col).Cut;
                _log.Record(T, Tick, "work", WorkRecord("cut-trunk", StandingWords(trunk), got ? ended : default, got)
                    .With("offered_seconds", (double)offered).With("through", through).With("cut_kept", cutKept).With("felled", felled));
                yield return Wait(1.0);
                yield return Capture(felled ? "changes-felled" : "changes-cut");
                captures++;
            }

            _client.WorkStateChanged -= OnState;
            _client.ChangesChanged -= OnChange;
            _log.Record(T, Tick, "end", WithFeet(new JsonObject().With("frames", _frames).With("errors", _errors)
                .With("stripped", stripped).With("fibre", fibre).With("corded", corded).With("pulled", pulled).With("cleared", cleared).With("dug", dug)
                .With("cut_kept", cutKept).With("felled", felled).With("changes_held", _client.Changes.Count + _client.Taken.Count)
                .With("corrections", _player.Corrections).With("moves_sent", (int)_player.MovesSent)
                .With("east", _player.State.East).With("up", _player.State.Up).With("north", _player.State.North)));
            _running = false;
            Finish(_errors == 0 && stripped && fibre && corded && cleared && dug && _frames == captures * Sizes.Length ? 0 : 1);
        }

        /// <summary>A trunk and the changes it carries, in the words the server would use.</summary>
        private string StandingWords(TrunkNearby t)
        {
            (byte flags, byte cut) = _client.Changes.TrunkOf(t.Row, t.Col);
            return ThingWords.TrunkWords(t.Species, t.HeightM, (flags & TrunkChange.BarkTaken) != 0, cut);
        }

        private JsonObject WorkRecord(string kind, string on, WorkStateMessage ended, bool got) =>
            new JsonObject().With("work", kind).With("on", on).With("seconds", (double)_client.LastIntentResult.Seconds).With("answer", (int)_client.LastIntentResult.Outcome)
                .With("ended", got ? (int)ended.Ended : -1).With("words", got ? ended.Note : string.Empty);

        /// <summary>The nearest trunk within the search of the founder that passes a test, or the shortest such; null for none.</summary>
        private TrunkNearby? NearestTrunk(Func<TrunkNearby, bool> fit, bool shortest)
        {
            _changesTrunks.Clear();
            TrunksNear.Find(_player.State.East, _player.State.North, ChangesSearchM, TrunkBodies.MeetsAtM, _client.Tiles, _client.Grid, _changesTrunks, Fine);
            TrunkNearby? best = null;
            double bestKey = double.MaxValue;
            foreach (TrunkNearby t in _changesTrunks)
            {
                if (!fit(t)) continue;
                double dx = t.East - _player.State.East, dz = t.North - _player.State.North;
                double key = shortest ? t.HeightM * 1000.0 + Math.Sqrt(dx * dx + dz * dz) : Math.Sqrt(dx * dx + dz * dz);
                if (key >= bestKey) continue;
                bestKey = key;
                best = t;
            }
            return best;
        }

        /// <summary>The nearest tuft within the search of the founder that passes a test and still stands; null for none.</summary>
        private UnderstoreyTuft? NearestTuft(Func<UnderstoreyTuft, bool> fit)
        {
            _changesTufts.Clear();
            Understorey.Find(_player.State.East, _player.State.North, ChangesSearchM, _client.Tiles, _client.Grid, _changesTufts, Fine);
            UnderstoreyTuft? best = null;
            double bestD = double.MaxValue;
            foreach (UnderstoreyTuft t in _changesTufts)
            {
                if (t.Index >= WorldChanges.MostTufts || !fit(t) || _client.Changes.IsTuftTaken(t.Row, t.Col, t.Index)) continue;
                if ((_client.Changes.GroundOf(t.Row, t.Col).Flags & GroundChange.Cleared) != 0) continue;
                double dx = t.East - _player.State.East, dz = t.North - _player.State.North;
                double d = Math.Sqrt(dx * dx + dz * dz);
                if (d >= bestD) continue;
                bestD = d;
                best = t;
            }
            return best;
        }

        /// <summary>Faces a trunk at the chest until the crosshair has it, then begins a work on it.</summary>
        /// <summary>How many rays the ground probe casts, and how far round the founder (BF.4).</summary>
        private const int GroundProbePoints = 400;
        private const double GroundProbeM = 30.0;

        /// <summary>The Terrain's height at a point, met by a ray down onto it alone; NaN where it is not met.</summary>
        private static double TerrainAt(double east, double north, double above)
        {
            Vector3 from = new Vector3((float)east, (float)(above + 40.0), (float)north);
            return Physics.Raycast(from, Vector3.down, out RaycastHit hit, 200f, 1 << Layers.Terrain, QueryTriggerInteraction.Ignore) ? hit.point.y : double.NaN;
        }

        /// <summary>
        /// The ground drawn set beside the one ground (BF.4): rays down onto the Terrain at points round the founder, each beside
        /// the function the tiles give and the raster under it. An error when the Terrain strays further than its triangles may.
        /// </summary>
        private JsonObject GroundProbe()
        {
            ClientGround fine = Fine;
            TileHeightfield raster = GetComponent<ClientRuntime>()?.Ground;
            int points = 0, missed = 0;
            double worst = 0.0, sum = 0.0, relief = 0.0;
            System.Random rng = new System.Random(7);
            for (int i = 0; i < GroundProbePoints; i++)
            {
                double a = rng.NextDouble() * 2.0 * System.Math.PI, r = System.Math.Sqrt(rng.NextDouble()) * GroundProbeM;
                double east = _player.State.East + r * System.Math.Cos(a), north = _player.State.North + r * System.Math.Sin(a);
                double one = fine != null ? fine.HeightAt(east, north) : double.NaN;
                double terrain = double.IsNaN(one) ? double.NaN : TerrainAt(east, north, one);
                if (double.IsNaN(terrain))
                {
                    missed++;
                    continue;
                }
                points++;
                double off = System.Math.Abs(terrain - one);
                sum += off;
                if (off > worst) worst = off;
                double under = raster != null ? raster.HeightAt(east, north) : double.NaN;
                if (!double.IsNaN(under)) relief = System.Math.Max(relief, System.Math.Abs(one - under));
            }
            double bound = Relief.TerrainStrayM + 0.01;
            if (points < GroundProbePoints / 2 || worst > bound)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the Terrain strays " + worst.ToString("0.000", CultureInfo.InvariantCulture) + " m from the one ground at "
                                                                     + points + " point(s), " + missed + " missed; " + bound.ToString("0.000", CultureInfo.InvariantCulture) + " m allowed"));
            }
            return new JsonObject().With("points", points).With("missed", missed).With("terrain_off_max_m", worst).With("terrain_off_mean_m", points > 0 ? sum / points : 0.0)
                .With("relief_max_m", relief).With("allowed_m", bound);
        }

        /// <summary>
        /// A dug cell set beside the one ground (BF.4): the ground at its centre down from the undug ground by the depth the world
        /// holds, and the Terrain there on it as near as its posts can carry a cone three metres round: a post of the Terrain
        /// stands up to 1.4 m from the cone's point, where the cone is half as deep (the first run's 10 cm hole met the Terrain
        /// 3.8 cm above its point). An error when either is not so.
        /// </summary>
        private JsonObject HollowProbe(int row, int col)
        {
            ClientGround fine = Fine;
            byte dugCm = _client.Changes.GroundOf(row, col).DugCm;
            ReceivedTile any = null;
            foreach (ReceivedTile t in _client.Tiles.Held.Values)
            {
                any = t;
                break;
            }
            if (fine == null || any == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "no ground held to measure the hole in"));
                return new JsonObject().With("row", row).With("col", col);
            }
            StandLayout.CellCentre(row, col, any.CellM, _client.Grid.ExtentM, out double east, out double north);
            double one = fine.HeightAt(east, north), undug = fine.UndugAt(east, north), terrain = TerrainAt(east, north, one);
            double hollow = undug - one;
            double allowed = Relief.TerrainStrayM + 0.5 * dugCm / 100.0;
            bool right = System.Math.Abs(hollow - dugCm / 100.0) <= 0.005 && System.Math.Abs(terrain - one) <= allowed;
            if (!right)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the hole at (" + row + ", " + col + ") is " + hollow.ToString("0.000", CultureInfo.InvariantCulture)
                                                                     + " m deep in the one ground for " + dugCm + " cm dug, and the Terrain is " + (terrain - one).ToString("0.000", CultureInfo.InvariantCulture) + " m off it"));
            }
            return new JsonObject().With("row", row).With("col", col).With("dug_cm", (int)dugCm).With("ground_m", one).With("undug_m", undug)
                .With("hollow_m", hollow).With("terrain_m", terrain).With("allowed_m", allowed).With("right", right);
        }

        private IEnumerator BeginTrunkWork(WorkKind kind, TrunkNearby trunk)
        {
            Face(new Double3(trunk.East, trunk.Up + TreeGeometries.BreastHeightM, trunk.North));
            double until = T + ChangesAimSeconds;
            while (T < until && !(_verbs.TargetTrunk.HasValue && _verbs.TargetTrunk.Value.Row == trunk.Row && _verbs.TargetTrunk.Value.Col == trunk.Col)) yield return null;
            _log.Record(T, Tick, "aim", new JsonObject().With("at", "trunk").With("row", trunk.Row).With("col", trunk.Col)
                .With("aimed", _verbs.TargetTrunk.HasValue && _verbs.TargetTrunk.Value.Row == trunk.Row && _verbs.TargetTrunk.Value.Col == trunk.Col).With("line", _verbs.Line));
            _client.SendIntent(new IntentMessage { Verb = Verb.Work, Kind = kind, Target = IntentMessage.TargetTrunk, Row = trunk.Row, Col = trunk.Col });
            yield return Wait(0.3);
        }

        /// <summary>Faces a tuft until the crosshair has it, then begins a work on it.</summary>
        private IEnumerator BeginTuftWork(WorkKind kind, UnderstoreyTuft tuft)
        {
            Face(new Double3(tuft.East, tuft.Up + 0.5 * tuft.HeightM, tuft.North));
            double until = T + ChangesAimSeconds;
            while (T < until && !(_verbs.TargetTuft.HasValue && SameTuft(_verbs.TargetTuft.Value, tuft))) yield return null;
            _log.Record(T, Tick, "aim", new JsonObject().With("at", "tuft").With("row", tuft.Row).With("col", tuft.Col).With("index", tuft.Index)
                .With("aimed", _verbs.TargetTuft.HasValue && SameTuft(_verbs.TargetTuft.Value, tuft)).With("line", _verbs.Line));
            _client.SendIntent(new IntentMessage { Verb = Verb.Work, Kind = kind, Target = IntentMessage.TargetTuft, Row = tuft.Row, Col = tuft.Col, Index = tuft.Index });
            yield return Wait(0.3);
        }

        private static bool SameTuft(UnderstoreyTuft a, UnderstoreyTuft b) => a.Row == b.Row && a.Col == b.Col && a.Index == b.Index;

        /// <summary>The panel's deed asked for, and the thing of that kind that was not there before, when it lies; null with an error otherwise.</summary>
        private IEnumerator SetDownThing(string deed, Definition kind, string what)
        {
            _changesThing = null;
            HashSet<ulong> before = new HashSet<ulong>();
            foreach (EntityView view in _client.Entities.Views.Values)
                if (ReferenceEquals(view.Definition, kind)) before.Add(view.Id.Value);
            _client.SendDevSetting(deed, 0.0);
            double until = T + ChangesArriveSeconds;
            while (T < until && _changesThing == null)
            {
                foreach (EntityView view in _client.Entities.Views.Values)
                {
                    if (!ReferenceEquals(view.Definition, kind) || before.Contains(view.Id.Value) || !view.Item.Resting) continue;
                    _changesThing = view;
                    break;
                }
                if (_changesThing != null) break;
                yield return null;
            }
            if (_changesThing == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", what + " was asked for and none lay there within " + ChangesArriveSeconds + " s"));
                yield break;
            }
            _log.Record(T, Tick, "set_down", new JsonObject().With("what", kind.Key).With("id", _changesThing.Id.Value.ToString())
                .With("edge", (double)_changesThing.Item.State.Edge01).With("mass_kg", (double)ThingWords.MassOf(kind, _changesThing.Item.State)));
            yield return Wait(0.5);
        }

        /// <summary>The nearest stick of the litter within the search thin enough to point with the flake in hand, or null.</summary>
        private LyingNearby? NearestPointable()
        {
            _near.Clear();
            LyingNear.Find(_player.State.East, _player.State.North, ChangesSearchM, _client.Tiles, _client.Grid, _client.Taken, _near, Fine);
            Double3 eye = _player.Eye;
            CarryingMessage carrying = _client.Carrying;
            Definition tool = null;
            ThingState toolState = default;
            if (carrying.Things != null)
                foreach (CarriedThing t in carrying.Things) if (t.Place == carrying.Hand) { tool = t.Definition; toolState = t.Item.State; }
            LyingNearby? best = null;
            double bestD = double.MaxValue;
            foreach (LyingNearby n in _near)
            {
                if (n.Thing.Kind != StandLayout.Kind.Stick) continue;
                LyingSite site = LyingSiteReader.Of(_client.Tiles, _client.Grid, n.Thing);
                WorkOffer offer = Work.Judge(WorkKind.Point, tool, toolState, LyingProperties.DefinitionOf(n.Thing.Kind, site), LyingProperties.StateOf(n.Thing, site));
                if (!offer.Possible) continue;
                double d = Double3.Distance(eye, At(n));
                if (d >= bestD) continue;
                bestD = d;
                best = n;
            }
            return best;
        }

        /// <summary>The nearest stick lying in the world that carries the pointed mark, or null.</summary>
        private EntityView NearestPointedStick()
        {
            EntityView best = null;
            double bestD = double.MaxValue;
            Double3 eye = _player.Eye;
            foreach (EntityView v in _client.Entities.Views.Values)
            {
                if (v.Definition == null || v.Definition.Substance != Substance.Wood || !v.HasItem || (v.Item.State.Marks & ThingMarks.Pointed) == 0) continue;
                double d = Double3.Distance(eye, v.Position);
                if (d >= bestD) continue;
                bestD = d;
                best = v;
            }
            return best;
        }
    }
}
