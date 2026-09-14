using System;
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
    /// game and frees the mouse, as the carrying window's Tab does, and the founder's hands rest while it is open. It holds
    /// the flight switch with the noclip switch under it; a slider for each setting a development server takes and a button
    /// for each deed (<see cref="DevSettings.All"/>, the one table the server reads too); the local hour, read and set; the
    /// sky now, as this client works it out from the seed, the region and the clock its Welcome carried; and where the
    /// founder stands. Built in code on the HUD's panel settings, like the HUD, on an object of its own, since an object holds
    /// one UIDocument and the HUD's is on the bootstrap's. A game not for development has no panel, and its key does nothing.
    /// </summary>
    public sealed class DevPanelController : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _panel;
        private Toggle _flight;
        private Toggle _noclip;
        private Slider _hour;
        private Label _time;
        private Label _sky;
        private Label _place;
        private GameClient _client;
        private PlayerController _player;
        private SolarClock _solar;
        private Climate _climate;
        private Synoptic _synoptic;
        private string _skyRefused;
        private bool _open;

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
            _panel.style.top = 60;
            _panel.style.width = 460;
            _panel.style.paddingLeft = 16;
            _panel.style.paddingRight = 16;
            _panel.style.paddingTop = 10;
            _panel.style.paddingBottom = 12;
            _panel.style.borderTopLeftRadius = 6;
            _panel.style.borderTopRightRadius = 6;
            _panel.style.borderBottomLeftRadius = 6;
            _panel.style.borderBottomRightRadius = 6;
            _panel.style.backgroundColor = new Color(0.05f, 0.05f, 0.04f, 0.82f);
            _panel.style.display = DisplayStyle.None;
            _panel.Add(Text("Developer", 18, 6));

            _flight = new Toggle("Flight");
            _flight.RegisterValueChangedCallback(e => _player.SetFlying(e.newValue));
            _panel.Add(_flight);
            _noclip = new Toggle("Noclip: through the ground and the trees");
            _noclip.style.marginLeft = 18;
            _noclip.RegisterValueChangedCallback(e => _player.SetNoclip(e.newValue));
            _panel.Add(_noclip);

            _panel.Add(Text("The server", 15, 8));
            foreach (DevSetting setting in DevSettings.All)
            {
                if (setting.IsDeed)
                {
                    string name = setting.Name;
                    Button deed = new Button(() => _client?.SendDevSetting(name, 0.0)) { text = setting.Says };
                    deed.style.marginTop = 6;
                    _panel.Add(deed);
                    continue;
                }
                Slider slider = MakeSlider(setting);
                if (setting.Name == DevSettings.ClockLocalHour) _hour = slider;
            }

            _panel.Add(Text("The world", 15, 8));
            _time = Text(string.Empty, 14, 2);
            _sky = Text(string.Empty, 14, 2);
            _place = Text(string.Empty, 14, 2);
            _panel.Add(_time);
            _panel.Add(_sky);
            _panel.Add(_place);
            root.Add(_panel);
        }

        /// <summary>A slider for a setting: its range the table's, its start the table's, and every change sent to the server.</summary>
        private Slider MakeSlider(DevSetting setting)
        {
            Slider slider = new Slider(setting.Says, (float)setting.Least, (float)setting.Most) { showInputField = true };
            if (!double.IsNaN(setting.Initial)) slider.SetValueWithoutNotify((float)setting.Initial);
            string name = setting.Name;
            slider.RegisterValueChangedCallback(e => _client?.SendDevSetting(name, e.newValue));
            _panel.Add(slider);
            return slider;
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

        /// <summary>Opens the panel and frees the mouse; the hour's slider starts at the hour it is.</summary>
        public void Show()
        {
            if (_panel == null || _open) return;
            _open = true;
            _panel.style.display = DisplayStyle.Flex;
            if (_hour != null && _solar != null) _hour.SetValueWithoutNotify((float)_solar.HourOfDay);
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
            if (_solar != null) _time.text = "Time   " + _solar.ClockText + "   day " + (_solar.DaysElapsed + 1);
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
