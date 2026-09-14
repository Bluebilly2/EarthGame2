using System;
using System.Collections.Generic;
using EarthGame.Engine;

namespace EarthGame.Protocol
{
    /// <summary>
    /// One setting a developer's panel can move on a development server (M1.D, CANON ruling 30): its name on the wire, what
    /// the panel calls it, the least and most a server holds it to, and where it starts; or a deed, which the panel shows as
    /// a button and the server does once. One table, read by the panel for its sliders and by the server for what it takes,
    /// so the two cannot disagree about what exists: a new setting is a row here and the code that applies it.
    /// </summary>
    public sealed class DevSetting
    {
        public DevSetting(string name, string says, double least, double most, double initial, bool deed = false)
        {
            Name = name;
            Says = says;
            Least = least;
            Most = most;
            Initial = initial;
            IsDeed = deed;
        }

        /// <summary>The name on the wire.</summary>
        public string Name { get; }
        /// <summary>What the panel calls it.</summary>
        public string Says { get; }
        public double Least { get; }
        public double Most { get; }
        /// <summary>Where the panel's slider starts, or NaN for one read from the world when the panel opens (the clock).</summary>
        public double Initial { get; }
        /// <summary>A deed rather than a number: the panel shows a button, and the number sent is nothing.</summary>
        public bool IsDeed { get; }
    }

    public static class DevSettings
    {
        /// <summary>How near a founder the animals' groups are stood up, m (<see cref="AnimalStandUp.StandUpRadiusM"/>).</summary>
        public const string AnimalsStandUpM = "animals.stand_up_m";
        /// <summary>Beyond how far from every founder a standing group is taken away, m (<see cref="AnimalStandUp.TakeAwayRadiusM"/>).</summary>
        public const string AnimalsTakeAwayM = "animals.take_away_m";
        /// <summary>The local hour at the region's centre the world's clock is set to, on the same day.</summary>
        public const string ClockLocalHour = "clock.local_hour";
        /// <summary>The local day of the year the world's clock is set to, at the same hour.</summary>
        public const string ClockDayOfYear = "clock.day_of_year";
        /// <summary>How many times faster than the game's rate the clock runs (<see cref="WorldClock.Scale"/>); nought holds the sky still.</summary>
        public const string ClockScale = "clock.scale";
        /// <summary>The kangaroos' flight (M1.7c, <see cref="AnimalFlightRules"/>): how near a founder sends a mob running, how far and how fast it runs.</summary>
        public const string KangarooFleeWithinM = "animals.kangaroo.flee_within_m";
        public const string KangarooRunM = "animals.kangaroo.run_m";
        public const string KangarooRunMs = "animals.kangaroo.run_ms";
        /// <summary>The oystercatchers' flight (M1.7c): the same three.</summary>
        public const string OystercatcherFleeWithinM = "animals.oystercatcher.flee_within_m";
        public const string OystercatcherRunM = "animals.oystercatcher.run_m";
        public const string OystercatcherRunMs = "animals.oystercatcher.run_ms";
        /// <summary>A deed: the founder who asks is stood at the world's wake.</summary>
        public const string StandAtWake = "founder.stand_at_wake";
        /// <summary>A deed: a stick is set on the ground two metres ahead of the founder who asks.</summary>
        public const string SpawnStick = "spawn.stick";
        /// <summary>A deed: a cobble is set on the ground two metres ahead of the founder who asks.</summary>
        public const string SpawnCobble = "spawn.cobble";

        public static readonly IReadOnlyList<DevSetting> All = new[]
        {
            new DevSetting(StandAtWake, "Stand at the wake", 0.0, 0.0, 0.0, deed: true),
            new DevSetting(ClockLocalHour, "Local hour", 0.0, 24.0, double.NaN),
            new DevSetting(ClockDayOfYear, "Day of the year", 1.0, 365.0, double.NaN),
            new DevSetting(ClockScale, "Clock runs at, times the game's rate", 0.0, 60.0, 1.0),
            new DevSetting(AnimalsStandUpM, "Animals stood up within, m", 100.0, 1500.0, AnimalStandUp.DefaultStandUpRadiusM),
            new DevSetting(AnimalsTakeAwayM, "Animals taken away beyond, m", 100.0, 1600.0, AnimalStandUp.DefaultTakeAwayRadiusM),
            new DevSetting(KangarooFleeWithinM, "Kangaroos run when a founder is within, m", 5.0, 400.0, AnimalFlightRules.KangarooFleeWithinM),
            new DevSetting(KangarooRunM, "Kangaroos run for, m", 10.0, 1000.0, AnimalFlightRules.KangarooRunM),
            new DevSetting(KangarooRunMs, "Kangaroos run at, m/s", 1.0, 20.0, AnimalFlightRules.KangarooRunMs),
            new DevSetting(OystercatcherFleeWithinM, "Oystercatchers fly when a founder is within, m", 5.0, 400.0, AnimalFlightRules.OystercatcherFleeWithinM),
            new DevSetting(OystercatcherRunM, "Oystercatchers fly for, m", 10.0, 1000.0, AnimalFlightRules.OystercatcherRunM),
            new DevSetting(OystercatcherRunMs, "Oystercatchers fly at, m/s", 1.0, 30.0, AnimalFlightRules.OystercatcherRunMs),
            new DevSetting(SpawnStick, "A stick, two metres ahead", 0.0, 0.0, 0.0, deed: true),
            new DevSetting(SpawnCobble, "A cobble, two metres ahead", 0.0, 0.0, 0.0, deed: true),
        };

        /// <summary>The setting of a name, or null for a name this build's table lacks.</summary>
        public static DevSetting Find(string name)
        {
            for (int i = 0; i < All.Count; i++)
                if (string.Equals(All[i].Name, name, StringComparison.Ordinal)) return All[i];
            return null;
        }

        /// <summary>A number held to a setting's range.</summary>
        public static double Held(DevSetting setting, double value) => Math.Min(setting.Most, Math.Max(setting.Least, value));
    }
}
