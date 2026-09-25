namespace EarthGame.Engine
{
    /// <summary>
    /// What a world's making lets a watcher see as it goes (the loading screen's map, William's choice of 2026-09-25): the
    /// layers as soon as they exist, their arrays filled step by step in the order the making reports its steps
    /// (<see cref="LoadingSteps.Making"/>), and the wake once it is chosen. Called on the making's own thread. A watcher keeps what
    /// it is handed and reads an array only once the step that fills it has ended, which the next step's report says, and
    /// never while it is being written; the report crosses between threads through the caller's queue, which orders the
    /// writes before it.
    /// </summary>
    public interface IMakingWatcher
    {
        /// <summary>The making's layers, before anything is worked out: their heights are the bake's, the rest filled as it goes.</summary>
        void Began(WorldLayers layers);

        /// <summary>Where the founder will wake, metres east and north of the region's centre.</summary>
        void WakeChosen(double east, double north);
    }
}
