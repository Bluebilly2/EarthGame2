using System;
using EarthGame.Engine;
using UnityEngine;
using UnityEngine.UIElements;

namespace EarthGame.Client
{
    /// <summary>
    /// Startup stays visible until the terrain and snapshot are ready. Actual work stages are named, and the bar fills by how
    /// far the loading has got, with the step it is on and about how long is left (<see cref="LoadingProgress"/>, from the
    /// steps' measured shares of the time: William, 2026-09-25, "not just a bar oscillating side to side"; until then a moving
    /// mark said only that the screen was alive). A join to another's world, which reports no steps to follow, keeps the
    /// moving mark. A separate panel prevents the loading overlay from sharing render targets with the gameplay HUD.
    /// </summary>
    public sealed class LoadingController : MonoBehaviour
    {
        private UIDocument _document;
        private PanelSettings _panel;
        private Label _stage, _elapsed, _title, _place, _detail;
        private VisualElement _activity, _fill;
        private readonly LoadingProgress _progress = new LoadingProgress();
        private VisualElement _phaseList;
        private readonly System.Collections.Generic.List<PhaseRow> _rows = new System.Collections.Generic.List<PhaseRow>();

        /// <summary>One phase's row: its dot, its name, its bar, its time, and under it the step in hand while it works.</summary>
        private sealed class PhaseRow
        {
            public string Name;
            public VisualElement Dot, Fill;
            public Label Title, Time, Step;
        }

        private static readonly Color Bright = new Color(0.93f, 0.9f, 0.82f), Plain = new Color(0.8f, 0.84f, 0.81f),
            Dim = new Color(0.45f, 0.5f, 0.49f), Green = new Color(0.55f, 0.75f, 0.64f), Track = new Color(0.18f, 0.23f, 0.22f);

        /// <summary>The bar's width, px.</summary>
        private const float TrackWidth = 420f;
        private Button _back;
        private double _started;
        private bool _failed, _canReturn;
        public event Action Back;
        public PanelSettings Panel => _panel;
        public string Stage => _stage != null ? _stage.text : "";
        public int Updates { get; private set; }
        /// <summary>The fraction of the loading the bar last showed, 0 to 1 (<see cref="LoadingProgress"/>).</summary>
        public double Fraction { get; private set; }
        public bool Failed => _failed;

        /// <summary>Names the place being prepared, as its region calls itself.</summary>
        public void SetPlace(string displayName)
        {
            if (_place != null && !string.IsNullOrEmpty(displayName)) _place.text = "EARTHGAME2  /  " + displayName.ToUpperInvariant();
        }

        public void Build()
        {
            _started = Time.realtimeSinceStartupAsDouble;
            _panel = Instantiate(Resources.Load<PanelSettings>(HudController.PanelResource));
            _panel.sortingOrder = 100;
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = _panel;
            VisualElement root = _document.rootVisualElement;
            root.style.flexGrow = 1;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            // The place is named once the world's own region is known (SetPlace); until then the game's name alone. It said
            // Bherwerre for every world until 2026-09-25, when William watched it prepare the whole Kangaroo Valley under that name.
            _place = new Label("EARTHGAME2");
            _place.style.fontSize = 14;
            _place.style.color = new Color(0.65f, 0.7f, 0.68f);
            _place.style.marginBottom = 22;
            root.Add(_place);
            _title = new Label("Preparing your world");
            _title.style.fontSize = 38;
            _title.style.color = new Color(0.93f, 0.9f, 0.82f);
            root.Add(_title);
            _stage = new Label("Starting");
            _stage.style.fontSize = 20;
            _stage.style.color = new Color(0.8f, 0.84f, 0.81f);
            _stage.style.marginTop = 20;
            _stage.style.width = Length.Percent(80);
            _stage.style.maxWidth = 800;
            _stage.style.unityTextAlign = TextAnchor.MiddleCenter;
            _stage.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_stage);
            VisualElement track = new VisualElement();
            track.style.width = TrackWidth;
            track.style.height = 6;
            track.style.marginTop = 28;
            track.style.backgroundColor = new Color(0.18f, 0.23f, 0.22f);
            root.Add(track);
            _fill = new VisualElement();
            _fill.style.position = Position.Absolute;
            _fill.style.left = 0;
            _fill.style.width = 0;
            _fill.style.height = 6;
            _fill.style.backgroundColor = new Color(0.55f, 0.75f, 0.64f);
            track.Add(_fill);
            _activity = new VisualElement();
            _activity.style.position = Position.Absolute;
            _activity.style.width = 64;
            _activity.style.height = 6;
            _activity.style.backgroundColor = new Color(0.55f, 0.75f, 0.64f);
            track.Add(_activity);
            _detail = new Label();
            _detail.style.fontSize = 14;
            _detail.style.color = new Color(0.8f, 0.84f, 0.81f);
            _detail.style.marginTop = 10;
            root.Add(_detail);
            _elapsed = new Label();
            _elapsed.style.fontSize = 14;
            _elapsed.style.color = new Color(0.65f, 0.7f, 0.68f);
            _elapsed.style.marginTop = 14;
            root.Add(_elapsed);
            // The phases, a row each (William chose them, and a map beside them, 2026-09-25): made once the loading's first
            // step says which loading it is, and again should the second say it is the other.
            _phaseList = new VisualElement();
            _phaseList.style.marginTop = 26;
            _phaseList.style.width = TrackWidth;
            root.Add(_phaseList);
            _back = new Button(ClickBack) { text = "Back to menu" };
            _back.style.display = DisplayStyle.None;
            _back.style.width = 240;
            _back.style.height = 44;
            _back.style.marginTop = 24;
            root.Add(_back);
            Button quit = new Button(Application.Quit) { text = "Quit" };
            quit.style.width = 240;
            quit.style.height = 44;
            quit.style.marginTop = 12;
            root.Add(quit);
        }

        public void SetStage(string stage)
        {
            if (stage == null) return;
            _progress.Report(stage, Time.realtimeSinceStartupAsDouble);
            if (!_failed) _stage.text = StageWords(stage);
        }

        /// <summary>The button and recording scenario use the same return-to-menu action.</summary>
        public void ClickBack() { if (_failed && _canReturn) Back?.Invoke(); }

        public void ShowFailure(string reason, bool canReturn)
        {
            _failed = true;
            _canReturn = canReturn;
            _title.text = "Couldn't open this world";
            _stage.text = reason;
            _stage.style.display = DisplayStyle.Flex;
            _back.style.display = canReturn ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Update()
        {
            Updates++;
            if (_panel == null || _failed) return;
            double now = Time.realtimeSinceStartupAsDouble;
            double seconds = now - _started;
            string elapsed = "Elapsed " + TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");
            if (_progress.StepNumber == 0)
            {
                // Nothing to follow (a join, or before the first step): the mark says the screen is alive.
                _fill.style.width = 0;
                _activity.style.display = DisplayStyle.Flex;
                _activity.style.left = (float)((Math.Sin(seconds * 2) + 1) * (TrackWidth - 64f) / 2.0);
                _detail.text = "";
                _elapsed.text = elapsed;
                return;
            }
            double fraction = _progress.FractionAt(now);
            Fraction = fraction;
            _activity.style.display = DisplayStyle.None;
            _fill.style.width = (float)(fraction * TrackWidth);
            int count = _progress.StepCount;
            _detail.text = Mathf.FloorToInt((float)(fraction * 100.0)) + "%  ·  step " + _progress.StepNumber + (count > 0 ? " of " + count : "");
            _elapsed.text = elapsed + "  ·  " + TimeLeft(_progress.SecondsLeftAt(now));
            System.Collections.Generic.IReadOnlyList<LoadingPhase> phases = _progress.PhasesAt(now);
            // The phase in hand names its step on its own row, as the design William chose has it; the line above the bar is
            // for a loading that names no phases, and for a failure's reason.
            _stage.style.display = phases.Count > 0 ? DisplayStyle.None : DisplayStyle.Flex;
            ShowPhases(phases);
        }

        private void ShowPhases(System.Collections.Generic.IReadOnlyList<LoadingPhase> phases)
        {
            bool same = phases.Count == _rows.Count;
            for (int i = 0; same && i < phases.Count; i++) same = phases[i].Name == _rows[i].Name;
            if (!same)
            {
                _phaseList.Clear();
                _rows.Clear();
                foreach (LoadingPhase phase in phases) _rows.Add(AddRow(phase.Name));
            }
            for (int i = 0; i < phases.Count; i++)
            {
                LoadingPhase phase = phases[i];
                PhaseRow row = _rows[i];
                bool done = phase.State == LoadingPhaseState.Done, working = phase.State == LoadingPhaseState.Working;
                row.Dot.style.backgroundColor = done ? Green : working ? Bright : Track;
                row.Title.style.color = done ? Plain : working ? Bright : Dim;
                row.Fill.style.width = Length.Percent((float)(Math.Max(0.0, Math.Min(1.0, phase.Fraction)) * 100.0));
                row.Time.style.color = working ? Bright : Dim;
                row.Time.text = done || working ? Clock(phase.Seconds) : double.IsNaN(phase.Seconds) ? "" : "~" + Clock(Math.Max(1.0, phase.Seconds));
                row.Step.style.display = working ? DisplayStyle.Flex : DisplayStyle.None;
                if (working && phase.Step != null) row.Step.text = StageWords(phase.Step);
            }
        }

        private PhaseRow AddRow(string name)
        {
            var row = new PhaseRow { Name = name };
            VisualElement line = new VisualElement();
            line.style.flexDirection = FlexDirection.Row;
            line.style.alignItems = Align.Center;
            line.style.marginTop = 7;
            row.Dot = new VisualElement();
            row.Dot.style.width = 8;
            row.Dot.style.height = 8;
            row.Dot.style.marginRight = 10;
            row.Dot.style.borderTopLeftRadius = 4;
            row.Dot.style.borderTopRightRadius = 4;
            row.Dot.style.borderBottomLeftRadius = 4;
            row.Dot.style.borderBottomRightRadius = 4;
            line.Add(row.Dot);
            row.Title = new Label(name);
            row.Title.style.fontSize = 14;
            row.Title.style.width = 200;
            line.Add(row.Title);
            VisualElement track = new VisualElement();
            track.style.width = 130;
            track.style.height = 4;
            track.style.backgroundColor = Track;
            row.Fill = new VisualElement();
            row.Fill.style.height = 4;
            row.Fill.style.width = 0;
            row.Fill.style.backgroundColor = Green;
            track.Add(row.Fill);
            line.Add(track);
            row.Time = new Label();
            row.Time.style.fontSize = 13;
            row.Time.style.width = 62;
            row.Time.style.unityTextAlign = TextAnchor.MiddleRight;
            line.Add(row.Time);
            _phaseList.Add(line);
            row.Step = new Label();
            row.Step.style.fontSize = 12;
            row.Step.style.color = Dim;
            row.Step.style.marginLeft = 18;
            row.Step.style.display = DisplayStyle.None;
            _phaseList.Add(row.Step);
            return row;
        }

        /// <summary>Seconds as minutes and seconds, "1:05".</summary>
        private static string Clock(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0.0) return "";
            int s = (int)Math.Round(seconds);
            return (s / 60) + ":" + (s % 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>A report as the screen words it: the layers saved by their names, the file names' marks as spaces.</summary>
        private static string StageWords(string stage) =>
            stage.Replace("Saved capacity_", "Saved habitat for ").Replace('_', ' ').Replace('-', ' ');

        /// <summary>About how long is left, in words; the estimate is the pace so far, so it is given no finer than it is.</summary>
        public static string TimeLeft(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) return "working out the time left";
            if (seconds < 10.0) return "a few seconds left";
            if (seconds < 55.0) return "about " + (int)(Math.Round(seconds / 5.0) * 5.0) + " seconds left";
            if (seconds < 90.0) return "about a minute left";
            return "about " + (int)Math.Round(seconds / 60.0) + " minutes left";
        }

        public void Close()
        {
            if (_document != null) Destroy(_document);
            Destroy(this);
        }

        private void OnDestroy()
        {
            if (_panel != null) Destroy(_panel);
        }
    }
}
