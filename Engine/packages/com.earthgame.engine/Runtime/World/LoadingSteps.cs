using System;
using System.Collections.Generic;

namespace EarthGame.Engine
{
    /// <summary>One step a world's loading reports, its share of the loading's time, and the phase of the loading it is part of.</summary>
    public readonly struct LoadingStep
    {
        public readonly string Name;

        /// <summary>The step's share of the loading's time, in any unit; the table's shares are read over their sum.</summary>
        public readonly double Share;

        /// <summary>The phase the loading screen shows it under, a row of its own; a table's steps of a phase stand together.</summary>
        public readonly string Phase;

        public LoadingStep(string name, double share, string phase)
        {
            Name = name;
            Share = share;
            Phase = phase;
        }
    }

    /// <summary>
    /// The steps a world's loading reports through its progress callback, in the order it reports them, each with its share of
    /// the loading's time, so the loading screen can show how far it has got and about how long is left (William, 2026-09-25:
    /// "can you include something to show progress? not just a bar oscillating side to side"). The names are the reports of
    /// <c>WorldPreparation</c>, <c>WorldCreation</c> and <see cref="WorldLayers"/>; <c>LoadingStepsTests</c> makes a world and
    /// opens it again and holds what each reported to these tables, so a step added, renamed or moved in the loading fails a
    /// test until its row is here.
    ///
    /// <para>The shares are the seconds each step took in a recorded loading of the game itself (the loading recorder's
    /// <c>stage_times</c>), dated at each table. The time left is judged from the pace the loading keeps
    /// (<see cref="LoadingProgress"/>), so the shares need only be right in proportion.</para>
    /// </summary>
    public static class LoadingSteps
    {
        /// <summary>Each layer a new world saves is reported as this and the layer's name, all of them within <see cref="Saving"/>.</summary>
        public const string SavedPrefix = "Saved ";

        /// <summary>The step the saved layers' reports stand in: its share is spread over <see cref="SavedLayers"/> of them.</summary>
        public const string Saving = "Saving the layers";

        /// <summary>The last step before a world is in hand, reported by the game once the server and the client are started.</summary>
        public const string PreparingGround = "Preparing the ground around you";

        // The phases, each a row on the loading screen (William chose the phases' bars and a map, 2026-09-25): the making's
        // steps grouped by what they work out, in the order they run.
        public const string ReadingTheLand = "Reading the land";
        public const string WaterAndDrainage = "Water and drainage";
        public const string SoilSlopesAndShore = "Soil, slopes and shore";
        public const string PlantsStoneAndTrees = "Plants, stone and trees";
        public const string AnimalsAndTheWake = "Animals and where you wake";
        public const string SavingTheWorld = "Saving the world";
        public const string ReadingTheWorld = "Reading the saved world";
        public const string RestoringTheWorld = "Restoring the world";
        /// <summary>The last phase of either loading, its one step the game's own.</summary>
        public const string TheGround = "The ground around you";

        /// <summary>
        /// A new world made: read the bake, run the chain, choose the wake, save the layers, read them back, save the world,
        /// and prepare the ground round the wake. The shares are seconds of the game's own making of the whole Kangaroo Valley
        /// recorded on 2026-09-25 (`Artefacts/loading/20260925T031205631131Z/new`: 347.6 s from the loading recorder's
        /// stage_times, and the ground in hand 6.6 s after the connection, as the player's log has it), a step too quick to
        /// time given a hundredth of a second. Bherwerre's making, 18.4 s the same afternoon, spends its time in nearly the same
        /// proportions: the plants 34 per cent against 37, the saving 17 against 14, the soil 10 against 12.
        /// </summary>
        public static readonly IReadOnlyList<LoadingStep> Making = new[]
        {
            new LoadingStep("Reading world", 0.01, ReadingTheLand),
            new LoadingStep("Reading landscape", 2.04, ReadingTheLand),
            new LoadingStep("Reading lakes and wetlands", 0.72, ReadingTheLand),
            new LoadingStep("Finding lakes and wetlands", 7.56, WaterAndDrainage),
            new LoadingStep("Finding where the lakes spill", 16.01, WaterAndDrainage),
            new LoadingStep("Tracing drainage", 28.49, WaterAndDrainage),
            new LoadingStep("Forming soil", 40.21, SoilSlopesAndShore),
            new LoadingStep("Reading slopes and wind exposure", 1.18, SoilSlopesAndShore),
            new LoadingStep("Measuring the shore", 5.58, SoilSlopesAndShore),
            new LoadingStep("Preparing the sea floor", 0.26, SoilSlopesAndShore),
            new LoadingStep("Preparing water", 5.85, SoilSlopesAndShore),
            new LoadingStep("Reading landforms", 4.58, SoilSlopesAndShore),
            new LoadingStep("Growing plant communities", 128.59, PlantsStoneAndTrees),
            new LoadingStep("Reading the ground cover", 0.98, PlantsStoneAndTrees),
            new LoadingStep("Finding stone", 0.21, PlantsStoneAndTrees),
            new LoadingStep("Standing the trees", 16.21, PlantsStoneAndTrees),
            new LoadingStep("Laying what lies on the ground", 3.09, PlantsStoneAndTrees),
            new LoadingStep("Calculating animal habitat", 4.81, AnimalsAndTheWake),
            new LoadingStep("Choosing the wake", 19.01, AnimalsAndTheWake),
            new LoadingStep(Saving, 49.05, SavingTheWorld),
            new LoadingStep("Writing the census", 0.01, SavingTheWorld),
            new LoadingStep("Reading prepared terrain", 2.26, SavingTheWorld),
            new LoadingStep("Reading what the ground feeds", 10.50, SavingTheWorld),
            new LoadingStep("Saving the world", 0.02, SavingTheWorld),
            new LoadingStep("World ready", 0.01, SavingTheWorld),
            new LoadingStep(PreparingGround, 6.62, TheGround),
        };

        /// <summary>
        /// A saved world opened: read its layers, restore it, and prepare the ground round where the founder rests. The shares
        /// are seconds of the game's own opening of the whole Kangaroo Valley recorded the same afternoon
        /// (`Artefacts/loading/20260925T031205631131Z/continue`: 21.2 s, and the ground in hand 5.8 s after the connection).
        /// </summary>
        public static readonly IReadOnlyList<LoadingStep> Opening = new[]
        {
            new LoadingStep("Reading world", 0.01, ReadingTheWorld),
            new LoadingStep("Reading saved terrain", 2.04, ReadingTheWorld),
            new LoadingStep("Reading the water", 3.02, ReadingTheWorld),
            new LoadingStep("Reading what stands and lies on the ground", 3.27, ReadingTheWorld),
            new LoadingStep("Reading what the ground feeds", 4.46, ReadingTheWorld),
            new LoadingStep("Restoring the world", 8.01, RestoringTheWorld),
            new LoadingStep("World ready", 0.01, RestoringTheWorld),
            new LoadingStep(PreparingGround, 5.83, TheGround),
        };

        /// <summary>The layers a new world saves one by one (<c>WorldCreation.Create</c>): 21 of its own and a habitat layer a kind of animal.</summary>
        public static int SavedLayers => 21 + AnimalSpecies.All.Count;

        /// <summary>A table's phases in the order they run, each once.</summary>
        public static IReadOnlyList<string> PhasesOf(IReadOnlyList<LoadingStep> table)
        {
            var phases = new List<string>();
            foreach (LoadingStep step in table)
                if (phases.Count == 0 || phases[phases.Count - 1] != step.Phase) phases.Add(step.Phase);
            return phases;
        }

        /// <summary>The step a report stands for: a saved layer's report is the saving step, any other report itself.</summary>
        public static string StepOf(string report)
        {
            if (report == null) return null;
            return report.StartsWith(SavedPrefix, StringComparison.Ordinal) ? Saving : report;
        }
    }
}
