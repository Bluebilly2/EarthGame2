using EarthGame.ClientCore;
using NUnit.Framework;

namespace EarthGame.Tests.Client
{
    /// <summary>
    /// When a game left alone stops (M1.E promise 1, CANON ruling 38). The watch is engine-free so the rule can be
    /// asserted without a window: the client feeds it the seconds, whether any key or button moved, and whether the
    /// window has the focus, and it says whether the game is asleep.
    /// </summary>
    public sealed class IdleWatchTests
    {
        private static IdleWatch Awake() => new IdleWatch(allowed: true);

        [Test]
        public void AGameTouchedStaysAwake()
        {
            IdleWatch watch = Awake();
            for (int i = 0; i < 100; i++)
            {
                watch.Notice(seconds: 1.0, touched: true, focused: true);
                Assert.That(watch.Asleep, Is.False, "touched every second and still fell asleep");
            }
        }

        [Test]
        public void AGameLeftAloneSleepsAtTheStatedMinute()
        {
            // The minute is the promise, not just the shape of it: a test that measures against the constant alone
            // follows it wherever it goes and catches nothing (found by sabotage, 2026-09-20).
            Assert.That(IdleWatch.AfterSeconds, Is.EqualTo(60.0), "M1.E promises a minute");
            IdleWatch watch = Awake();
            watch.Notice(IdleWatch.AfterSeconds - 0.5, touched: false, focused: true);
            Assert.That(watch.Asleep, Is.False, "half a second early");
            watch.Notice(1.0, touched: false, focused: true);
            Assert.That(watch.Asleep, Is.True, "a minute alone and still awake");
        }

        [Test]
        public void LosingTheWindowSleepsAtOnce()
        {
            IdleWatch watch = Awake();
            watch.Notice(0.1, touched: false, focused: false);
            Assert.That(watch.Asleep, Is.True, "he looked at something else; the game should have stopped");
        }

        [Test]
        public void AnyTouchOrTheWindowComingBackWakesIt()
        {
            IdleWatch watch = Awake();
            watch.Notice(IdleWatch.AfterSeconds + 1.0, touched: false, focused: true);
            Assert.That(watch.Asleep, Is.True);
            watch.Notice(0.1, touched: true, focused: true);
            Assert.That(watch.Asleep, Is.False, "a key did not wake it");

            IdleWatch second = Awake();
            second.Notice(0.1, touched: false, focused: false);
            Assert.That(second.Asleep, Is.True);
            second.Notice(0.1, touched: false, focused: true);
            Assert.That(second.Asleep, Is.False, "the window came back and it did not wake");
        }

        /// <summary>
        /// A scenario presses its buttons on its own schedule and waits minutes between them; the corpus's soak would
        /// send itself to sleep and its own timings would be nonsense. A recorded run never sleeps.
        /// </summary>
        [Test]
        public void ARecordedRunNeverSleeps()
        {
            IdleWatch watch = new IdleWatch(allowed: false);
            watch.Notice(IdleWatch.AfterSeconds * 10.0, touched: false, focused: false);
            Assert.That(watch.Asleep, Is.False, "a recorded run must not pause itself");
        }

        /// <summary>The waking is reported once, so the client can start the world again on that edge alone.</summary>
        [Test]
        public void TheEdgesAreReportedOnce()
        {
            IdleWatch watch = Awake();
            Assert.That(watch.Notice(IdleWatch.AfterSeconds + 1.0, touched: false, focused: true), Is.EqualTo(IdleChange.FellAsleep));
            Assert.That(watch.Notice(1.0, touched: false, focused: true), Is.EqualTo(IdleChange.None), "asleep is not news twice");
            Assert.That(watch.Notice(0.1, touched: true, focused: true), Is.EqualTo(IdleChange.WokeUp));
            Assert.That(watch.Notice(0.1, touched: true, focused: true), Is.EqualTo(IdleChange.None), "awake is not news twice");
        }
    }
}
