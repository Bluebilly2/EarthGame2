using System;
using System.Collections.Generic;
using System.Globalization;
using EarthGame.Engine;

namespace EarthGame.Almanac
{
    /// <summary>
    /// Fits the weather's settings to a station's record and measures what the model does with them (M1.8c, 2026-09-16), so
    /// the constants in <see cref="Synoptic"/> and the fronts' widening in <see cref="Climate"/>'s station are numbers a tool
    /// found and prints, not numbers typed in. Every candidate is run through the engine's own formulas
    /// (<see cref="Synoptic.RainRateMmPerHour(double, in Synoptic.RainSettings)"/> and the like), so the tool holds no copy
    /// of them. <c>+fit rain|cloud|depth|widening|all</c>, with <c>+first</c> and <c>+seeds</c> for the world seeds (5000 and
    /// 128 by default: the tests read 1 to 256 and the check 1000 to 1255, and a fit must not see the seeds that judge it).
    ///
    /// <para>The record is the Bureau of Meteorology's table for Point Perpendicular Lighthouse (068034), all years, as
    /// <c>Data/stations/</c> holds it; the tool restates the twelve months it fits to and prints the model's beside them.
    /// The fit is Nelder and Mead's simplex over the relative errors of the months.</para>
    /// </summary>
    public static class Fit
    {
        // ---- the record, restated: Point Perpendicular Lighthouse, 068034, all years (Data/stations) ----
        private static readonly double[] RainMm = { 97.2, 98.5, 122.4, 131.8, 134.1, 128.8, 107.2, 89.7, 79.2, 85.3, 84.3, 83.1 };
        private static readonly double[] RainDays1 = { 9.2, 9.1, 10.1, 9.5, 10.2, 9.8, 8.9, 8.3, 8.6, 9.1, 9.1, 8.6 };
        private static readonly double[] RainDaysAny = { 11.7, 11.5, 12.7, 11.9, 12.2, 11.6, 10.6, 10.1, 10.4, 11.2, 11.3, 11.3 };
        private static readonly double[] RainDays10 = { 2.7, 2.5, 3.2, 3.4, 3.5, 3.6, 3.2, 2.5, 2.1, 2.5, 2.4, 2.4 };
        private static readonly double[] Cloud9amOktas = { 5.0, 5.0, 4.8, 4.4, 4.3, 4.3, 3.8, 3.7, 3.9, 4.4, 4.7, 4.9 };
        private static readonly double[] Cloud3pmOktas = { 4.6, 4.5, 4.3, 4.2, 4.4, 4.2, 3.9, 3.8, 3.8, 4.3, 4.6, 4.5 };
        /// <summary>August's daily minima, the ninth decile over the first, 1946–2004, °C: what the cold snap's depth is set by.</summary>
        private const double AugustMinimaSpreadC = 12.2 - 7.0;
        /// <summary>The cool season's drying since 1994 (State of the Climate 2024), nine per cent of April to October's rain, and the record's years inside it.</summary>
        private const double CoolSeasonDrying = 0.09;
        private const int RecordFirstYear = 1899, RecordLastYear = 2004, DryingSinceYear = 1994, April = 3, October = 9;
        private static readonly int[] DaysInMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
        private static readonly string[] Names = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        private const int August = 7;
        private const int ReadingsADay = 48, ObservationStep = 18;

        public static int Run(Dictionary<string, string> a, ulong firstSeed, int seeds)
        {
            string what = a["fit"];
            if (what == "true") what = "all";
            Synoptic[] synoptics = new Synoptic[seeds];
            for (int s = 0; s < seeds; s++) synoptics[s] = new Synoptic(firstSeed + (ulong)s);
            Console.WriteLine("fitting on " + seeds + " world seeds from " + firstSeed);
            bool any = false;
            if (what == "rain" || what == "all") { FitRain(synoptics); any = true; }
            if (what == "cloud" || what == "all") { FitCloud(synoptics); any = true; }
            if (what == "depth" || what == "all") { FitDepth(synoptics); any = true; }
            if (what == "widening" || what == "all") { MeasureWidening(synoptics); any = true; }
            if (!any)
            {
                Console.Error.WriteLine("usage: +fit rain|cloud|depth|widening|all [+first seed] [+seeds N]");
                return 2;
            }
            return 0;
        }

        // ---- the rain ----

        private static double PreHumanRainMm(int month)
        {
            if (month < April || month > October) return RainMm[month];
            double inside = (RecordLastYear - DryingSinceYear + 1) / (double)(RecordLastYear - RecordFirstYear + 1);
            return RainMm[month] / (1.0 - CoolSeasonDrying * inside);
        }

        /// <summary>Each seed's rain on each day of the year, mm, from a reading at the start of each hour, under a candidate.</summary>
        private static void DailyRain(Synoptic[] synoptics, in Synoptic.RainSettings s, double[,] daily)
        {
            for (int k = 0; k < synoptics.Length; k++)
                for (int day = 0; day < 365; day++)
                {
                    double mm = 0.0;
                    for (int hour = 0; hour < 24; hour++) mm += synoptics[k].RainRateMmPerHour(day + hour / 24.0, s);
                    daily[k, day] = mm;
                }
        }

        private static void Months(double[,] daily, double[] mm, double[] days1, double[] daysAny, double[] days10)
        {
            int seeds = daily.GetLength(0);
            int first = 0;
            for (int m = 0; m < 12; m++)
            {
                double sum = 0.0, d1 = 0.0, dAny = 0.0, d10 = 0.0;
                for (int k = 0; k < seeds; k++)
                    for (int day = first; day < first + DaysInMonth[m]; day++)
                    {
                        double v = daily[k, day];
                        sum += v;
                        if (v >= 1.0) d1++;
                        if (v >= 0.2) dAny++;
                        if (v >= 10.0) d10++;
                    }
                mm[m] = sum / seeds;
                days1[m] = d1 / seeds;
                daysAny[m] = dAny / seeds;
                days10[m] = d10 / seeds;
                first += DaysInMonth[m];
            }
        }

        private static void FitRain(Synoptic[] synoptics)
        {
            double[,] daily = new double[synoptics.Length, 365];
            double[] mm = new double[12], d1 = new double[12], dAny = new double[12], d10 = new double[12];
            // The two yearly waves are searched as a cosine part and a sine part each, not as an amplitude and a day: an
            // amplitude near zero makes the day mean nothing and the simplex stalls there (the first fit did, and lost the
            // cool season's heavier rain). The engine's settings are made from the parts at each step.
            Func<double[], Synoptic.RainSettings> settingsOf = p => new Synoptic.RainSettings(p[0], p[1],
                Math.Sqrt(p[2] * p[2] + p[3] * p[3]), Wrap365(Math.Atan2(p[3], p[2]) * 365.0 / (2.0 * Math.PI)),
                Math.Sqrt(p[4] * p[4] + p[5] * p[5]), Wrap365(Math.Atan2(p[5], p[4]) * 365.0 / (2.0 * Math.PI)));
            Func<double[], double> cost = p =>
            {
                Synoptic.RainSettings s = settingsOf(p);
                if (s.Threshold < 0.3 || s.Threshold > 3.5 || s.ScaleMmPerHour <= 0.0 || s.FrequencyAmplitude > 0.95 || s.HeavierAmplitude > 0.95) return 1e9;
                DailyRain(synoptics, s, daily);
                Months(daily, mm, d1, dAny, d10);
                double c = 0.0;
                for (int m = 0; m < 12; m++)
                {
                    c += 1.5 * Square((mm[m] - PreHumanRainMm(m)) / PreHumanRainMm(m));
                    c += Square((d1[m] - RainDays1[m]) / RainDays1[m]);
                    c += 0.7 * Square((dAny[m] - RainDaysAny[m]) / RainDaysAny[m]);
                    c += 0.5 * Square((d10[m] - RainDays10[m]) / RainDays10[m]);
                }
                return c;
            };
            Synoptic.RainSettings start = Synoptic.RainSettings.Default;
            double fAngle = start.MostFrontsDay * 2.0 * Math.PI / 365.0, hAngle = start.HeaviestRainDay * 2.0 * Math.PI / 365.0;
            double[] x0 = { start.Threshold, start.ScaleMmPerHour, start.FrequencyAmplitude * Math.Cos(fAngle), start.FrequencyAmplitude * Math.Sin(fAngle),
                            start.HeavierAmplitude * Math.Cos(hAngle), start.HeavierAmplitude * Math.Sin(hAngle) };
            double[] step = { 0.15, 1.0, 0.1, 0.1, 0.2, 0.2 };
            double[] best = NelderMead(cost, x0, step, 800, out double bestCost);
            // A second simplex from the best, with smaller steps, in case the first stopped on a shelf.
            double[] fine = { 0.05, 0.3, 0.03, 0.03, 0.06, 0.06 };
            best = NelderMead(cost, best, fine, 400, out bestCost);
            Synoptic.RainSettings fitted = settingsOf(best);
            Console.WriteLine("rain: cost " + bestCost.ToString("0.0000", CultureInfo.InvariantCulture) + " (started " + cost(x0).ToString("0.0000", CultureInfo.InvariantCulture) + ")");
            Console.WriteLine("  RainThreshold = " + F4(fitted.Threshold) + "; RainScaleMmPerHour = " + F4(fitted.ScaleMmPerHour) + "; FrequencyAmplitude = " + F4(fitted.FrequencyAmplitude)
                              + "; MostFrontsDay = " + F2(fitted.MostFrontsDay) + "; HeavierAmplitude = " + F4(fitted.HeavierAmplitude) + "; HeaviestRainDay = " + F2(fitted.HeaviestRainDay));
            cost(best);
            Console.WriteLine("  month   mm model/pre-human   days>=1 model/record   days>=0.2 model/record   days>=10 model/record");
            double ymm = 0, yt = 0, y1 = 0, y1t = 0, ya = 0, yat = 0, y10 = 0, y10t = 0;
            for (int m = 0; m < 12; m++)
            {
                Console.WriteLine("  " + Names[m] + "   " + F1(mm[m]) + "/" + F1(PreHumanRainMm(m)) + "   " + F2(d1[m]) + "/" + F1(RainDays1[m]) + "   " + F2(dAny[m]) + "/" + F1(RainDaysAny[m]) + "   " + F2(d10[m]) + "/" + F1(RainDays10[m]));
                ymm += mm[m]; yt += PreHumanRainMm(m); y1 += d1[m]; y1t += RainDays1[m]; ya += dAny[m]; yat += RainDaysAny[m]; y10 += d10[m]; y10t += RainDays10[m];
            }
            Console.WriteLine("  year  " + F1(ymm) + "/" + F1(yt) + "   " + F1(y1) + "/" + F1(y1t) + "   " + F1(ya) + "/" + F1(yat) + "   " + F1(y10) + "/" + F1(y10t));
        }

        // ---- the cloud ----

        private static void FitCloud(Synoptic[] synoptics)
        {
            double[] at9 = new double[12], at15 = new double[12];
            Func<double[], double> cost = p =>
            {
                if (p[0] < 0.0 || p[0] > 1.0 || p[1] < 0.0 || p[1] > 0.5) return 1e9;
                Synoptic.CloudSettings c = new Synoptic.CloudSettings(p[0], p[1], p[2], Synoptic.CloudIndexGain);
                MonthlyCloud(synoptics, c, at9, at15);
                double sum = 0.0;
                for (int m = 0; m < 12; m++) sum += Square(at9[m] - Cloud9amOktas[m] / 8.0) + Square(at15[m] - Cloud3pmOktas[m] / 8.0);
                return sum;
            };
            Synoptic.CloudSettings start = Synoptic.CloudSettings.Default;
            double[] x0 = { start.Mean, start.Amplitude, start.CloudiestDay };
            double[] step = { 0.1, 0.05, 40.0 };
            double[] best = NelderMead(cost, x0, step, 300, out double bestCost);
            best[2] = Wrap365(best[2]);
            Console.WriteLine("cloud: cost " + bestCost.ToString("0.00000", CultureInfo.InvariantCulture) + " (started " + cost(x0).ToString("0.00000", CultureInfo.InvariantCulture) + ")");
            Console.WriteLine("  CloudMean = " + F4(best[0]) + "; CloudAmplitude = " + F4(best[1]) + "; CloudiestDay = " + F2(best[2]));
            cost(best);
            Console.WriteLine("  month   9am model/record oktas   3pm model/record oktas");
            for (int m = 0; m < 12; m++)
                Console.WriteLine("  " + Names[m] + "   " + F2(at9[m] * 8.0) + "/" + F1(Cloud9amOktas[m]) + "   " + F2(at15[m] * 8.0) + "/" + F1(Cloud3pmOktas[m]));
        }

        private static void MonthlyCloud(Synoptic[] synoptics, in Synoptic.CloudSettings c, double[] at9, double[] at15)
        {
            int first = 0;
            for (int m = 0; m < 12; m++)
            {
                double s9 = 0.0, s15 = 0.0;
                for (int k = 0; k < synoptics.Length; k++)
                    for (int day = first; day < first + DaysInMonth[m]; day++)
                    {
                        s9 += synoptics[k].CloudCover01(day + 9.0 / 24.0, c);
                        s15 += synoptics[k].CloudCover01(day + 15.0 / 24.0, c);
                    }
                at9[m] = s9 / (synoptics.Length * DaysInMonth[m]);
                at15[m] = s15 / (synoptics.Length * DaysInMonth[m]);
                first += DaysInMonth[m];
            }
        }

        // ---- the cold snap's depth, from the deciles ----

        /// <summary>
        /// August's daily extremes at the lighthouse's height over the Bureau's windows (the lowest in the 24 hours to 9 am,
        /// the highest in the 24 hours from it), for every seed and day, with the anomaly at a depth: the curve and the
        /// front's term added as <see cref="Weather.At"/> adds them.
        /// </summary>
        private static void AugustExtremes(Synoptic[] synoptics, double depthC, List<double> minima, List<double> maxima)
        {
            Climate climate = Climate.ForRegion(Region.Bherwerre);
            double offsetHours = WorldClock.OffsetHours(Region.Bherwerre.CentreLongitudeDeg);
            double height = climate.StationElevationM;
            int firstDay = 0;
            for (int m = 0; m < August; m++) firstDay += DaysInMonth[m];
            minima.Clear();
            maxima.Clear();
            for (int k = 0; k < synoptics.Length; k++)
            {
                // From 9 am on the last day of July to 9 am on the first of September.
                int startStep = (firstDay - 1) * ReadingsADay + ObservationStep, endStep = (firstDay + DaysInMonth[August]) * ReadingsADay + ObservationStep;
                double high = double.MinValue, low = double.MaxValue;
                int window = 0;
                for (int step = startStep; step < endStep; step++)
                {
                    WorldClock clock = new WorldClock((step + 0.5) * 24.0 / ReadingsADay - offsetHours);
                    SolarClock sun = SolarClock.ForRegion(Region.Bherwerre, clock);
                    double local = clock.LocalHours(sun.LongitudeDeg) / 24.0;
                    double air = climate.AirTemperatureC(sun.DayOfYear, sun.HourOfDay, height) + synoptics[k].AirAnomalyC(local, depthC);
                    high = Math.Max(high, air);
                    low = Math.Min(low, air);
                    if ((step + 1 - startStep) % ReadingsADay == 0)
                    {
                        // A 9 am window closed: its high is a day's maximum from 9 am, its low the next day's minimum to 9 am.
                        window++;
                        if (window <= DaysInMonth[August]) maxima.Add(high);
                        if (window >= 1 && window <= DaysInMonth[August]) minima.Add(low);
                        high = double.MinValue;
                        low = double.MaxValue;
                    }
                }
            }
        }

        private static double Decile(List<double> values, double share)
        {
            values.Sort();
            double position = share * (values.Count - 1);
            int below = (int)Math.Floor(position);
            int above = Math.Min(values.Count - 1, below + 1);
            return values[below] + (values[above] - values[below]) * (position - below);
        }

        private static void FitDepth(Synoptic[] synoptics)
        {
            List<double> minima = new List<double>(), maxima = new List<double>();
            Func<double, double> spread = depth =>
            {
                AugustExtremes(synoptics, depth, minima, maxima);
                return Decile(minima, 0.9) - Decile(minima, 0.1);
            };
            double current = spread(Synoptic.ColdSnapDepthC);
            Console.WriteLine("depth: at the current " + F2(Synoptic.ColdSnapDepthC) + " °C, August's minima spread " + F2(current) + " °C from the first decile to the ninth (record " + F1(AugustMinimaSpreadC) + "); maxima " + F2(Decile(maxima, 0.9) - Decile(maxima, 0.1)) + " (record 6.1)");
            double lo = 0.5, hi = 6.0;
            for (int i = 0; i < 24; i++)
            {
                double mid = 0.5 * (lo + hi);
                if (spread(mid) < AugustMinimaSpreadC) lo = mid; else hi = mid;
            }
            double depth = 0.5 * (lo + hi);
            double s = spread(depth);
            Console.WriteLine("  ColdSnapDepthC = " + F2(depth) + "  (minima spread " + F2(s) + "; first decile " + F2(Decile(minima, 0.1)) + ", ninth " + F2(Decile(minima, 0.9))
                              + "; maxima spread " + F2(Decile(maxima, 0.9) - Decile(maxima, 0.1)) + ", first " + F2(Decile(maxima, 0.1)) + ", ninth " + F2(Decile(maxima, 0.9)) + ")");
        }

        // ---- the fronts' widening of the day's extremes ----

        /// <summary>
        /// Month by month, how far the weather's fronts lift the mean of the day's highest reading above the curve's own and
        /// lower the mean of the lowest below the curve's, °C, at the depth the engine has: what the station's arrays should
        /// hold. The curve is the engine's, so the arrays converge in one or two passes.
        /// </summary>
        private static void MeasureWidening(Synoptic[] synoptics)
        {
            Climate climate = Climate.ForRegion(Region.Bherwerre);
            double offsetHours = WorldClock.OffsetHours(Region.Bherwerre.CentreLongitudeDeg);
            double height = climate.StationElevationM;
            double[] raise = new double[12], lower = new double[12];
            int[] highs = new int[12], lows = new int[12];
            for (int k = 0; k < synoptics.Length; k++)
            {
                double curveHigh = double.MinValue, skyHigh = double.MinValue, curveLow = double.MaxValue, skyLow = double.MaxValue;
                int windowStartDay = 0;
                for (int step = ObservationStep; step < 365 * ReadingsADay; step++)
                {
                    WorldClock clock = new WorldClock((step + 0.5) * 24.0 / ReadingsADay - offsetHours);
                    SolarClock sun = SolarClock.ForRegion(Region.Bherwerre, clock);
                    double curve = climate.AirTemperatureC(sun.DayOfYear, sun.HourOfDay, height);
                    double sky = curve + synoptics[k].AirAnomalyC(clock.LocalHours(sun.LongitudeDeg) / 24.0);
                    curveHigh = Math.Max(curveHigh, curve);
                    skyHigh = Math.Max(skyHigh, sky);
                    curveLow = Math.Min(curveLow, curve);
                    skyLow = Math.Min(skyLow, sky);
                    if ((step + 1 - ObservationStep) % ReadingsADay == 0)
                    {
                        // The window from 9 am on windowStartDay closed: the high is that day's, the low the next day's.
                        int nextDay = windowStartDay + 1;
                        if (nextDay < 365)
                        {
                            int mHigh = MonthOf(windowStartDay), mLow = MonthOf(nextDay);
                            raise[mHigh] += skyHigh - curveHigh;
                            highs[mHigh]++;
                            lower[mLow] += curveLow - skyLow;
                            lows[mLow]++;
                        }
                        curveHigh = skyHigh = double.MinValue;
                        curveLow = skyLow = double.MaxValue;
                        windowStartDay++;
                    }
                }
            }
            string r = "", l = "";
            for (int m = 0; m < 12; m++)
            {
                r += (m == 0 ? "" : ", ") + F2(raise[m] / highs[m]);
                l += (m == 0 ? "" : ", ") + F2(lower[m] / lows[m]);
            }
            Console.WriteLine("widening at depth " + F2(Synoptic.ColdSnapDepthC) + ":");
            Console.WriteLine("  frontsRaiseMaxC = { " + r + " }");
            Console.WriteLine("  frontsLowerMinC = { " + l + " }");
        }

        private static int MonthOf(int dayIndex)
        {
            int first = 0;
            for (int m = 0; m < 12; m++)
            {
                if (dayIndex < first + DaysInMonth[m]) return m;
                first += DaysInMonth[m];
            }
            return 11;
        }

        // ---- Nelder and Mead's simplex ----

        private static double[] NelderMead(Func<double[], double> f, double[] x0, double[] step, int iterations, out double bestCost)
        {
            int n = x0.Length;
            double[][] x = new double[n + 1][];
            double[] fx = new double[n + 1];
            for (int i = 0; i <= n; i++)
            {
                x[i] = (double[])x0.Clone();
                if (i > 0) x[i][i - 1] += step[i - 1];
                fx[i] = f(x[i]);
            }
            for (int it = 0; it < iterations; it++)
            {
                Array.Sort(fx, x);
                if (Math.Abs(fx[n] - fx[0]) < 1e-9 * Math.Max(1e-12, Math.Abs(fx[0]))) break;
                double[] centroid = new double[n];
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++) centroid[j] += x[i][j] / n;
                double[] reflected = Combine(centroid, x[n], 1.0);
                double fr = f(reflected);
                if (fr < fx[0])
                {
                    double[] expanded = Combine(centroid, x[n], 2.0);
                    double fe = f(expanded);
                    if (fe < fr) { x[n] = expanded; fx[n] = fe; } else { x[n] = reflected; fx[n] = fr; }
                }
                else if (fr < fx[n - 1])
                {
                    x[n] = reflected;
                    fx[n] = fr;
                }
                else
                {
                    double[] contracted = Combine(centroid, x[n], fr < fx[n] ? 0.5 : -0.5);
                    double fc = f(contracted);
                    if (fc < Math.Min(fr, fx[n])) { x[n] = contracted; fx[n] = fc; }
                    else
                    {
                        for (int i = 1; i <= n; i++)
                        {
                            for (int j = 0; j < n; j++) x[i][j] = x[0][j] + 0.5 * (x[i][j] - x[0][j]);
                            fx[i] = f(x[i]);
                        }
                    }
                }
            }
            Array.Sort(fx, x);
            bestCost = fx[0];
            return x[0];
        }

        /// <summary>centroid + t (centroid − worst): the reflection (1), the expansion (2), the contractions (±0.5).</summary>
        private static double[] Combine(double[] centroid, double[] worst, double t)
        {
            double[] p = new double[centroid.Length];
            for (int j = 0; j < p.Length; j++) p[j] = centroid[j] + t * (centroid[j] - worst[j]);
            return p;
        }

        private static double Wrap365(double day) => ((day % 365.0) + 365.0) % 365.0;
        private static double Square(double v) => v * v;
        private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        private static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        private static string F4(double v) => v.ToString("0.0000", CultureInfo.InvariantCulture);
    }
}
