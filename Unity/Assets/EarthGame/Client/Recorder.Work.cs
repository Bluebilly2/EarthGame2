using System.Collections;
using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;

namespace EarthGame.Client
{
    public sealed partial class Recorder
    {
        public const string WorkScenario = "work";

        /// <summary>How long a work is given to end before the scenario calls it an error, s: the longest strip plus room.</summary>
        private const double WorkWaitSeconds = 40.0;

        /// <summary>
        /// Work (BF.2 promise 6), run by <c>-eg-scenario work</c>: a stick of the litter within reach whose bark strips is
        /// stripped where it lies ("work-stripped"), two of its strips are taken up and laid into cord ("work-cord"), and a
        /// stick thin enough to break over the knee is broken when one lies within reach ("work-broken"). Each work's words, its
        /// seconds and how it ended go into the run's log. The point is proved by the server's tests and waits for the fire
        /// kit's scenario (DEBTS).
        /// </summary>
        private IEnumerator RunWork()
        {
            if (_client == null || _verbs == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the work scenario has no client or no verbs to drive"));
                _running = false;
                Finish(1);
                yield break;
            }
            double from = T;
            while (_ready != null && !_ready() && T < from + ReadyTimeoutSeconds) yield return null;
            WorkStateMessage ended = default;
            bool got = false;
            void OnState(WorkStateMessage s)
            {
                if (s.Ended == WorkStateMessage.Running) return;
                ended = s;
                got = true;
            }
            _client.WorkStateChanged += OnState;
            int captures = 0;

            // The strip: the nearest stick of the litter whose bark comes away.
            LyingNearby? found = null;
            double until = T + 20.0;
            while ((found = NearestWorkable(WorkKind.Strip)) == null && T < until) yield return null;
            bool stripped = false, corded = false, broken = false;
            string brokeNote = "none thin enough within reach";
            if (found == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "no stick of a tree whose bark strips lay within reach"));
            }
            else
            {
                LyingThing stick = found.Value.Thing;
                got = false;
                yield return BeginLyingWork(WorkKind.Strip, stick);
                until = T + WorkWaitSeconds;
                while (T < until && !got) yield return null;
                stripped = got && ended.Ended == WorkStateMessage.Done;
                _log.Record(T, Tick, "work", WorkRecord("strip", stick, got ? ended : default, got));
                yield return Wait(1.0);
                yield return Capture("work-stripped");
                captures++;

                // Two strips into the hands, then laid into cord.
                List<EntityView> strips = NearestOf(Substance.Bark, 2);
                foreach (EntityView strip in strips)
                {
                    Face(strip.Position);
                    until = T + 4.0;
                    while (T < until && !(_verbs.Target != null && _verbs.Target.Id.Value == strip.Id.Value)) yield return null;
                    _script.Use();
                    until = T + 4.0;
                    while (T < until && !Carries(strip.Id.Value)) yield return null;
                }
                byte other = OtherPlaceWith(Substance.Bark);
                if (strips.Count == 2 && other != 0)
                {
                    got = false;
                    uint sequence = _client.SendIntent(new IntentMessage { Verb = Verb.Work, Kind = WorkKind.Twist, Target = IntentMessage.TargetPlace, Place = other });
                    until = T + WorkWaitSeconds;
                    while (T < until && !got) yield return null;
                    corded = got && ended.Ended == WorkStateMessage.Done && HoldsA(Substance.Cord);
                    _log.Record(T, Tick, "work", new JsonObject().With("work", "twist").With("sequence", (long)sequence).With("seconds", (double)_client.LastIntentResult.Seconds)
                        .With("ended", got ? (int)ended.Ended : -1).With("words", got ? ended.Note : string.Empty).With("held", HeldWords()));
                    yield return Wait(0.6);
                    yield return Capture("work-cord");
                    captures++;
                }
                else
                {
                    _errors++;
                    _log.Record(T, Tick, "error", new JsonObject().With("message", "two strips were not taken into the hands: " + strips.Count + " found, other place " + other));
                }
            }

            // The break: a stick thin enough, when one lies within reach.
            LyingNearby? thin = NearestWorkable(WorkKind.Break);
            if (thin != null)
            {
                got = false;
                yield return BeginLyingWork(WorkKind.Break, thin.Value.Thing);
                until = T + WorkWaitSeconds;
                while (T < until && !got) yield return null;
                broken = got && ended.Ended == WorkStateMessage.Done;
                brokeNote = got ? ended.Note : "no end came";
                _log.Record(T, Tick, "work", WorkRecord("break", thin.Value.Thing, got ? ended : default, got));
                yield return Wait(1.0);
                yield return Capture("work-broken");
                captures++;
            }
            _client.WorkStateChanged -= OnState;
            _log.Record(T, Tick, "end", WithFeet(new JsonObject().With("frames", _frames).With("errors", _errors)
                .With("stripped", stripped).With("corded", corded).With("broken", broken).With("break_note", brokeNote)
                .With("corrections", _player.Corrections).With("moves_sent", (int)_player.MovesSent)
                .With("east", _player.State.East).With("up", _player.State.Up).With("north", _player.State.North)));
            _running = false;
            Finish(_errors == 0 && stripped && corded && _frames == captures * Sizes.Length ? 0 : 1);
        }

        /// <summary>Faces a lying thing until the crosshair has it, then begins a work on it.</summary>
        private IEnumerator BeginLyingWork(WorkKind kind, LyingThing thing)
        {
            LyingNearby? near = Find(thing);
            if (near != null) Face(At(near.Value));
            double until = T + 4.0;
            while (T < until && !(_verbs.TargetLying.HasValue && _verbs.TargetLying.Value.Equals(thing))) yield return null;
            _client.SendIntent(new IntentMessage { Verb = Verb.Work, Kind = kind, Target = IntentMessage.TargetLying, Lying = thing });
            yield return Wait(0.3);
        }

        private JsonObject WorkRecord(string kind, LyingThing thing, WorkStateMessage ended, bool got)
        {
            LyingSite site = LyingSiteReader.Of(_client.Tiles, _client.Grid, thing);
            string on = ThingWords.Describe(LyingProperties.DefinitionOf(thing.Kind, site), LyingProperties.StateOf(thing, site));
            return new JsonObject().With("work", kind).With("on", on).With("seconds", (double)_client.LastIntentResult.Seconds).With("answer", (int)_client.LastIntentResult.Outcome)
                .With("ended", got ? (int)ended.Ended : -1).With("words", got ? ended.Note : string.Empty);
        }

        /// <summary>The nearest stick of the litter within reach that this work can be done to, by the client's own judgement, or null.</summary>
        private LyingNearby? NearestWorkable(WorkKind kind)
        {
            _near.Clear();
            LyingNear.Find(_player.State.East, _player.State.North, Hands.ReachM + 0.5, _client.Tiles, _client.Grid, _client.Taken, _near);
            Double3 eye = _player.Eye;
            LyingNearby? best = null;
            double bestD = double.MaxValue;
            foreach (LyingNearby n in _near)
            {
                if (n.Thing.Kind != StandLayout.Kind.Stick) continue;
                LyingSite site = LyingSiteReader.Of(_client.Tiles, _client.Grid, n.Thing);
                WorkOffer offer = Work.Judge(kind, null, default, LyingProperties.DefinitionOf(n.Thing.Kind, site), LyingProperties.StateOf(n.Thing, site));
                if (!offer.Possible) continue;
                double d = Double3.Distance(eye, At(n));
                if (d > Hands.ReachM - 0.3 || d >= bestD) continue;
                bestD = d;
                best = n;
            }
            return best;
        }

        private LyingNearby? Find(LyingThing thing)
        {
            _near.Clear();
            LyingNear.Find(_player.State.East, _player.State.North, Hands.ReachM + 0.5, _client.Tiles, _client.Grid, _client.Taken, _near);
            foreach (LyingNearby n in _near) if (n.Thing.Equals(thing)) return n;
            return null;
        }

        /// <summary>The nearest things of a substance lying in the world, up to a count.</summary>
        private List<EntityView> NearestOf(Substance substance, int count)
        {
            List<EntityView> all = new List<EntityView>();
            Double3 eye = _player.Eye;
            foreach (EntityView v in _client.Entities.Views.Values)
                if (v.Definition != null && v.Definition.Substance == substance) all.Add(v);
            all.Sort((a, b) => Double3.Distance(eye, a.Position).CompareTo(Double3.Distance(eye, b.Position)));
            if (all.Count > count) all.RemoveRange(count, all.Count - count);
            return all;
        }

        private bool HoldsA(Substance substance)
        {
            CarryingMessage carrying = _client.Carrying;
            if (carrying.Things == null) return false;
            foreach (CarriedThing t in carrying.Things) if (t.Place == carrying.Hand && t.Definition.Substance == substance) return true;
            return false;
        }

        /// <summary>A place of the hands other than the hand holding a thing of a substance, or 0.</summary>
        private byte OtherPlaceWith(Substance substance)
        {
            CarryingMessage carrying = _client.Carrying;
            if (carrying.Things == null) return 0;
            foreach (CarriedThing t in carrying.Things) if (t.Place != carrying.Hand && t.Definition.Substance == substance) return t.Place;
            return 0;
        }
    }
}
