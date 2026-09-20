namespace EarthGame.ClientCore
{
    /// <summary>What the watch has just decided, so a client acts on the edge and not on the state.</summary>
    public enum IdleChange
    {
        None = 0,
        FellAsleep = 1,
        WokeUp = 2,
    }

    /// <summary>
    /// Whether a game has been left alone (CANON ruling 38, 2026-09-20). William: "if the game is ever open, and idle,
    /// i am not playing, i was looking at something or somethings … the game is paused, time is paused. then everything
    /// is resumed when the player refocuses the game window, or a button or key is pressed."
    ///
    /// <para>A game asleep takes no step, so the world's clock stands still and a founder cannot thirst or freeze while
    /// nobody is there, and it draws a few frames a second instead of the six hundred an idle game drew before this
    /// (2026-09-20), which is a GPU the machine can spend on a build or a measured run.</para>
    ///
    /// <para>The rule is here, engine-free, so it can be asserted without a window; the client feeds it the seconds
    /// between frames, whether any key, button or stick moved, and whether the window has the focus. A recorded run is
    /// never allowed to sleep: a scenario presses its buttons on its own schedule and waits minutes between them, and
    /// the corpus's soak would send itself to sleep and make nonsense of its own timings.</para>
    /// </summary>
    public sealed class IdleWatch
    {
        /// <summary>How long a game may be left untouched before it sleeps, s.</summary>
        public const double AfterSeconds = 60.0;

        private readonly bool _allowed;
        private double _untouchedFor;
        private bool _wasFocused = true;

        public IdleWatch(bool allowed)
        {
            _allowed = allowed;
        }

        /// <summary>True while the game is asleep: nothing is stepped and the pause is shown.</summary>
        public bool Asleep { get; private set; }

        /// <summary>How long the game has gone untouched, s; zero while it is being played.</summary>
        public double UntouchedFor => _untouchedFor;

        /// <summary>
        /// Takes this frame's seconds and what happened in it, and answers what changed. Losing the window sleeps at
        /// once — he is looking at something else — while being untouched sleeps only after <see cref="AfterSeconds"/>,
        /// since a player may stand still and watch the country.
        /// </summary>
        public IdleChange Notice(double seconds, bool touched, bool focused)
        {
            if (!_allowed)
            {
                _untouchedFor = 0.0;
                Asleep = false;
                _wasFocused = focused;
                return IdleChange.None;
            }

            bool was = Asleep;
            // The window coming back is itself an act: he has looked at the game again.
            bool focusReturned = focused && !_wasFocused;
            _wasFocused = focused;

            if (touched || focusReturned)
            {
                _untouchedFor = 0.0;
                Asleep = false;
            }
            else if (!focused)
            {
                Asleep = true;
            }
            else
            {
                _untouchedFor += seconds > 0.0 ? seconds : 0.0;
                // A game already asleep stays so until it is touched: only the count decides the falling.
                if (!was) Asleep = _untouchedFor >= AfterSeconds;
            }

            if (was && !Asleep) return IdleChange.WokeUp;
            if (!was && Asleep) return IdleChange.FellAsleep;
            return IdleChange.None;
        }
    }
}
