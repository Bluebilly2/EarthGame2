using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace EarthGame.Client
{
    /// <summary>
    /// Startup stays visible until the terrain and snapshot are ready. Actual work stages are named,
    /// while the moving mark says the UI is alive; it does not pretend to know a percentage or an ETA.
    /// A separate panel prevents the loading overlay from sharing render targets with the gameplay HUD.
    /// </summary>
    public sealed class LoadingController : MonoBehaviour
    {
        private UIDocument _document;
        private PanelSettings _panel;
        private Label _stage, _elapsed, _title, _place;
        private VisualElement _activity;
        private Button _back;
        private double _started;
        private bool _failed, _canReturn;
        public event Action Back;
        public PanelSettings Panel => _panel;
        public string Stage => _stage != null ? _stage.text : "";
        public int Updates { get; private set; }
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
            track.style.width = 320;
            track.style.height = 3;
            track.style.marginTop = 28;
            track.style.backgroundColor = new Color(0.18f, 0.23f, 0.22f);
            root.Add(track);
            _activity = new VisualElement();
            _activity.style.position = Position.Absolute;
            _activity.style.width = 64;
            _activity.style.height = 3;
            _activity.style.backgroundColor = new Color(0.55f, 0.75f, 0.64f);
            track.Add(_activity);
            _elapsed = new Label();
            _elapsed.style.fontSize = 14;
            _elapsed.style.color = new Color(0.65f, 0.7f, 0.68f);
            _elapsed.style.marginTop = 14;
            root.Add(_elapsed);
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
            if (!_failed) _stage.text = stage.Replace("Saved capacity_", "Saved habitat for ").Replace('_', ' ').Replace('-', ' ');
        }

        /// <summary>The button and recording scenario use the same return-to-menu action.</summary>
        public void ClickBack() { if (_failed && _canReturn) Back?.Invoke(); }

        public void ShowFailure(string reason, bool canReturn)
        {
            _failed = true;
            _canReturn = canReturn;
            _title.text = "Couldn't open this world";
            _stage.text = reason;
            _back.style.display = canReturn ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Update()
        {
            Updates++;
            if (_panel == null || _failed) return;
            double seconds = Time.realtimeSinceStartupAsDouble - _started;
            _elapsed.text = "Elapsed " + TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");
            _activity.style.left = (float)((Math.Sin(seconds * 2) + 1) * 128);
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
