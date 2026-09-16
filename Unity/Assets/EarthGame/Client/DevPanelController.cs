using System;
using System.Collections.Generic;
using System.Globalization;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using UnityEngine;
using UnityEngine.UIElements;

namespace EarthGame.Client
{
    /// <summary>
    /// The developer's panel (M1.D, CANON ruling 30). In a development game (<c>-eg-dev</c>) its key opens a panel over the
    /// game and frees the mouse, as the carrying window's Tab does, and the founder's hands rest while it is open. Its
    /// sections: the founder (the flight switch with the noclip switch under it, and standing at the wake); time (the local
    /// hour and the day of the year, read and set, and how fast the clock runs); the animals (how near they are stood up and
    /// taken away, how wary they are and how far and fast they run, and what pose an animal set down by hand is shown in);
    /// spawning (a stick, a cobble, a kangaroo or an oystercatcher two metres ahead, the animals since M1.7b gave them
    /// looks to judge); the environment (the sky is worked out from the seed and
    /// the clock, M1.8a, so a hand on it waits for the weather drawn, M1.8b, and its controls stand greyed); and the world
    /// as this client reads it. Every setting the server takes is a row of <see cref="DevSettings.All"/>, the one table the
    /// server reads too, drawn by its name's prefix into its section; a row that has a start has a reset of its own, and one
    /// button resets them all with the flight. Built in code on the HUD's panel settings, like the HUD, on an object of its
    /// own, since an object holds one UIDocument and the HUD's is on the bootstrap's; every label is white, since the
    /// theme's grey could not be read on the panel (William, 2026-09-14). A game not for development has no panel, and its
    /// key does nothing.
    /// </summary>
    public sealed class DevPanelController : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _panel;
        private ScrollView _scroll;
        private Toggle _flight;
        private Toggle _noclip;
        private Slider _hour;
        private Slider _day;
        private Label _time;
        private Label _sky;
        private Label _place;
        private readonly Dictionary<string, Slider> _sliders = new Dictionary<string, Slider>(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> _sections = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
        private GameClient _client;
        private PlayerController _player;
        private SolarClock _solar;
        private Climate _climate;
        private Synoptic _synoptic;
        private string _skyRefused;
        private bool _open;

        private static readonly Color Panel = new Color(0.05f, 0.05f, 0.04f, 0.86f);
        private static readonly Color ButtonFace = new Color(0.22f, 0.22f, 0.20f, 1f);

        /// <summary>Whether the panel is open: the mouse is its and the founder's hands rest.</summary>
        public bool Open => _open;

        public void Build(GameClient client, PlayerController player, Region region, ulong seed, SolarClock solar)
        {
            _client = client;
            _player = player;
            _solar = solar;
            try
            {
                _climate = Climate.ForRegion(region);
                _synoptic = new Synoptic(seed);
            }
            catch (ArgumentException ex)
            {
                // A region this build holds no station record for (a test's fixture): the panel says so where the sky would be.
                _skyRefused = ex.Message;
            }
            PanelSettings panel = Resources.Load<PanelSettings>(HudController.PanelResource);
            if (panel == null)
            {
                Debug.LogError("[dev] PanelSettings missing at Resources/" + HudController.PanelResource + "; there is no panel");
                return;
            }
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panel;
            // Over the HUD's document, which shares the settings.
            _document.sortingOrder = 10;
            VisualElement root = _document.rootVisualElement;
            root.style.flexGrow = 1;
            root.pickingMode = PickingMode.Ignore;

            _panel = new VisualElement();
            _panel.style.position = Position.Absolute;
            _panel.style.left = 28;
            _panel.style.top = 48;
            _panel.style.width = 500;
            _panel.style.maxHeight = new Length(88, LengthUnit.Percent);
            _panel.style.paddingLeft = 16;
            _panel.style.paddingRight = 16;
            _panel.style.paddingTop = 10;
            _panel.style.paddingBottom = 12;
            _panel.style.borderTopLeftRadius = 6;
            _panel.style.borderTopRightRadius = 6;
            _panel.style.borderBottomLeftRadius = 6;
            _panel.style.borderBottomRightRadius = 6;
            _panel.style.backgroundColor = Panel;
            _panel.style.display = DisplayStyle.None;

            VisualElement head = Row();
            Label title = Text("Developer", 18, 0);
            title.style.flexGrow = 1;
            head.Add(title);
            head.Add(MakeButton("Reset all", ResetAll));
            _panel.Add(head);

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.flexGrow = 1;
            _panel.Add(_scroll);

            VisualElement founder = Section("founder", "The founder");
            _flight = MakeToggle("Flight", on => _player.SetFlying(on));
            founder.Add(_flight);
            _noclip = MakeToggle("Noclip: through the ground and the trees", on => _player.SetNoclip(on));
            _noclip.style.marginLeft = 18;
            founder.Add(_noclip);

            Section("clock", "Time");
            Section("animals", "The animals");
            VisualElement spawning = Section("spawn", "Spawning");
            foreach (DevSetting setting in DevSettings.All) AddRow(setting);
            spawning.Add(Note("Kangaroos and oystercatchers are stood up by the country itself (M1.7a). One set down here is yours to look at: two metres ahead and broadside, in the pose chosen under The animals, and it stays where it is put until the game closes (M1.7b)."));

            VisualElement environment = Section("environment", "The environment");
            environment.Add(Note("The sky is worked out from the world's seed and its clock (M1.8a); a hand on it waits for the weather drawn (M1.8b). Until then these do nothing."));
            environment.Add(Stub(MakeSlider("Rain, mm/h", 0f, 50f, 0f, null)));
            environment.Add(Stub(MakeSlider("Wind, m/s", 0f, 30f, 0f, null)));
            environment.Add(Stub(MakeSlider("Cloud, %", 0f, 100f, 0f, null)));

            VisualElement world = Section("world", "The world");
            _time = Text(string.Empty, 14, 2);
            _sky = Text(string.Empty, 14, 2);
            _place = Text(string.Empty, 14, 2);
            world.Add(_time);
            world.Add(_sky);
            world.Add(_place);
            root.Add(_panel);
        }

        /// <summary>A row of the table, drawn into the section its name's prefix names: a deed as a button, a number as a slider with a reset when it has a start.</summary>
        private void AddRow(DevSetting setting)
        {
            string prefix = setting.Name.Substring(0, setting.Name.IndexOf('.'));
            VisualElement section = _sections.TryGetValue(prefix, out VisualElement s) ? s : _sections["world"];
            string name = setting.Name;
            if (setting.IsDeed)
            {
                Button deed = MakeButton(setting.Says, () => _client?.SendDevSetting(name, 0.0));
                deed.style.marginTop = 6;
                section.Add(deed);
                return;
            }
            VisualElement row = Row();
            Slider slider = MakeSlider(setting.Says, (float)setting.Least, (float)setting.Most,
                double.IsNaN(setting.Initial) ? (float)setting.Least : (float)setting.Initial, v => _client?.SendDevSetting(name, v));
            slider.style.flexGrow = 1;
            row.Add(slider);
            if (!double.IsNaN(setting.Initial))
            {
                float start = (float)setting.Initial;
                Button reset = MakeButton("reset", () => slider.value = start);
                reset.style.marginLeft = 6;
                row.Add(reset);
            }
            section.Add(row);
            _sliders[name] = slider;
            if (name == DevSettings.ClockLocalHour) _hour = slider;
            if (name == DevSettings.ClockDayOfYear) _day = slider;
        }

        /// <summary>Every row with a start back to it, the flight off and the noclip on: what a developer's session starts with.</summary>
        private void ResetAll()
        {
            foreach (DevSetting setting in DevSettings.All)
                if (!setting.IsDeed && !double.IsNaN(setting.Initial) && _sliders.TryGetValue(setting.Name, out Slider slider))
                    slider.value = (float)setting.Initial;
            _player.SetFlying(false);
            _player.SetNoclip(true);
        }

        private VisualElement Section(string key, string title)
        {
            VisualElement section = new VisualElement();
            section.Add(Text(title, 15, 10));
            _scroll.Add(section);
            _sections[key] = section;
            return section;
        }

        private static VisualElement Row()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            return row;
        }

        private static Toggle MakeToggle(string label, Action<bool> changed)
        {
            Toggle toggle = new Toggle(label);
            toggle.labelElement.style.color = Color.white;
            toggle.labelElement.style.minWidth = 0;
            if (changed != null) toggle.RegisterValueChangedCallback(e => changed(e.newValue));
            return toggle;
        }

        /// <summary>A slider, its label white and its number typed beside it; every change goes to <paramref name="changed"/>.</summary>
        private static Slider MakeSlider(string label, float least, float most, float start, Action<float> changed)
        {
            Slider slider = new Slider(label, least, most) { showInputField = true };
            slider.labelElement.style.color = Color.white;
            slider.labelElement.style.minWidth = 200;
            slider.SetValueWithoutNotify(start);
            if (changed != null) slider.RegisterValueChangedCallback(e => changed(e.newValue));
            return slider;
        }

        private static Button MakeButton(string text, Action clicked)
        {
            Button button = clicked != null ? new Button(clicked) : new Button();
            button.text = text;
            button.style.color = Color.white;
            button.style.backgroundColor = ButtonFace;
            return button;
        }

        /// <summary>A control that does nothing yet, shown greyed so that what is not built is seen and not mistaken for what is.</summary>
        private static T Stub<T>(T control) where T : VisualElement
        {
            control.SetEnabled(false);
            return control;
        }

        private static Label Note(string text)
        {
            Label note = Text(text, 12, 2);
            note.style.unityFontStyleAndWeight = FontStyle.Normal;
            note.style.color = new Color(0.85f, 0.85f, 0.8f);
            return note;
        }

        private static Label Text(string text, int fontSize, int marginTop)
        {
            Label label = new Label(text);
            label.style.fontSize = fontSize;
            label.style.color = Color.white;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = marginTop;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        /// <summary>A new connection (a rejoin): the settings go to the new server.</summary>
        public void Rebind(GameClient client) => _client = client;

        /// <summary>Opens the panel and frees the mouse; the hour's and the day's sliders start at the hour and the day it is.</summary>
        public void Show()
        {
            if (_panel == null || _open) return;
            _open = true;
            _panel.style.display = DisplayStyle.Flex;
            if (_solar != null)
            {
                _hour?.SetValueWithoutNotify((float)_solar.HourOfDay);
                _day?.SetValueWithoutNotify(_solar.DayOfYear);
            }
            if (!Application.isBatchMode)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            Tick();
        }

        /// <summary>Closes the panel and takes the mouse back.</summary>
        public void Hide()
        {
            if (!_open) return;
            _open = false;
            _panel.style.display = DisplayStyle.None;
            if (!Application.isBatchMode)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }
        }

        /// <summary>Once a frame while open: the switches shown as the body has them, and the readouts.</summary>
        public void Tick()
        {
            if (!_open || _player == null) return;
            _flight.SetValueWithoutNotify(_player.Flying);
            _noclip.SetValueWithoutNotify(_player.Noclip);
            _noclip.SetEnabled(_player.Flying);
            MoverState s = _player.State;
            if (_solar != null)
                _time.text = "Time   " + _solar.ClockText + "   day " + (_solar.DaysElapsed + 1) + " of the world, day " + _solar.DayOfYear + " of the year"
                             + (_client != null && _client.LastClockScale != 1.0 ? "   the clock at " + F(_client.LastClockScale, "0.#") + " times" : "");
            if (_climate != null && _solar != null)
            {
                Weather sky = Weather.At(_climate, _synoptic, _solar, Math.Max(0.0, s.Up), 1.0);
                _sky.text = "Sky   " + F(sky.AirC, "0.0") + " °C   wind " + F(sky.WindMs, "0.0") + " m/s from " + F(sky.WindFromDeg, "0") + "°"
                            + "   cloud " + F(sky.CloudCover01 * 100.0, "0") + " %   " + (sky.IsRaining ? "rain " + F(sky.RainRateMmPerHour, "0.0") + " mm/h" : "dry");
            }
            else _sky.text = "Sky   " + _skyRefused;
            double ground = _player.Ground != null ? _player.Ground.HeightAt(s.East, s.North) : double.NaN;
            _place.text = "Place   E " + F(s.East, "0.0") + "   N " + F(s.North, "0.0") + "   up " + F(s.Up, "0.0")
                          + (double.IsNaN(ground) ? "" : "   the ground at " + F(ground, "0.0"))
                          + (s.Grounded ? "   standing" : _player.Flying ? "   flying" : "   in the air");
        }

        private static string F(double v, string format) => v.ToString(format, CultureInfo.InvariantCulture);
    }
}
