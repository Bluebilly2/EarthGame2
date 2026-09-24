using System.Collections.Generic;
using System.Globalization;
using EarthGame.ClientCore;
using EarthGame.Engine;
using UnityEngine;
using UnityEngine.UIElements;

namespace EarthGame.Client
{
    /// <summary>
    /// The M1 HUD in UI Toolkit (ARCHITECTURE §8): a crosshair, the verb line under it (what the right mouse would do,
    /// since M1.5a), the local clock, the carrying window, and a diagnostic line the frames carry so a picture says
    /// where and when it was taken. Built in code against the PanelSettings asset the project setup creates;
    /// readability floor 3:1 is met by white text with a dark shadow on any ground.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        public const string PanelResource = "EarthGame/HudPanel";

        private static readonly Color HandColour = new Color(1f, 0.9f, 0.55f);

        private UIDocument _document;
        private Label _clock;
        private Label _verb;
        private Label _diagnostic;
        private Label _condition;
        private Label _notice;
        private Label _paused;
        private float _noticeUntil;
        private VisualElement _carrying;
        private readonly Label[] _places = new Label[Hands.Places];
        private bool _carryingOpen;

        // The body's bars (M1.F, CANON ruling 48): each its own colour, and its per cent white, amber at the body's first words
        // and red at the words that kill, so the bar agrees with the word under the clock.
        private static readonly Color[] BarColours = { new Color(0.36f, 0.66f, 1f), new Color(1f, 0.56f, 0.24f), new Color(0.5f, 0.85f, 0.38f) };
        private static readonly Color WordColour = new Color(1f, 0.8f, 0.3f), DangerColour = new Color(1f, 0.36f, 0.3f);
        private VisualElement _bars;
        private readonly Label[] _barNames = new Label[3];
        private readonly VisualElement[] _barFills = new VisualElement[3];
        private readonly Label[] _barPercents = new Label[3];
        private bool _barsWanted = true;
        private bool _barsHeard;

        /// <summary>Whether the bars are wanted: H hides and shows them, and they are wanted at every start (M1.F).</summary>
        public bool BarsWanted => _barsWanted;

        /// <summary>Whether the bars are on the screen: wanted, and a body has been heard of.</summary>
        public bool BarsShown => _barsWanted && _barsHeard;

        /// <summary>The per cents as last drawn, water, warmth and strength, for the recorder's records.</summary>
        public int[] BarPercents { get; } = new int[3];

        public PanelSettings Panel => _document != null ? _document.panelSettings : null;

        /// <summary>Whether the carrying window is open.</summary>
        public bool CarryingOpen => _carryingOpen;

        public void Build()
        {
            PanelSettings panel = Resources.Load<PanelSettings>(PanelResource);
            if (panel == null)
            {
                Debug.LogError("[hud] PanelSettings missing at Resources/" + PanelResource + "; run EarthGame > Apply project checklist");
                return;
            }
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panel;
            VisualElement root = _document.rootVisualElement;
            root.style.flexGrow = 1;
            root.pickingMode = PickingMode.Ignore;

            VisualElement crosshair = new VisualElement();
            crosshair.style.position = Position.Absolute;
            crosshair.style.left = new Length(50, LengthUnit.Percent);
            crosshair.style.top = new Length(50, LengthUnit.Percent);
            crosshair.style.width = 6;
            crosshair.style.height = 6;
            crosshair.style.marginLeft = -3;
            crosshair.style.marginTop = -3;
            crosshair.style.borderTopLeftRadius = 3;
            crosshair.style.borderTopRightRadius = 3;
            crosshair.style.borderBottomLeftRadius = 3;
            crosshair.style.borderBottomRightRadius = 3;
            crosshair.style.backgroundColor = new Color(1f, 1f, 1f, 0.85f);
            crosshair.pickingMode = PickingMode.Ignore;
            root.Add(crosshair);

            _clock = MakeLabel(root, 22, 18, 16);
            // The word for the founder's state, under the clock (FP.1): nothing while there is nothing to say.
            _condition = MakeLabel(root, 18, 18, 46);
            // A notice under it (FP.2): what killed the founder, in the one sentence, for a while.
            _notice = MakeLabel(root, 16, 18, 76);
            _notice.style.maxWidth = new Length(60, LengthUnit.Percent);
            _notice.style.whiteSpace = WhiteSpace.Normal;
            _verb = MakeLabel(root, 18, 0, 0);
            _verb.style.left = new Length(50, LengthUnit.Percent);
            _verb.style.top = new Length(56, LengthUnit.Percent);
            _verb.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            _diagnostic = MakeLabel(root, 14, 18, 0);
            _diagnostic.style.top = StyleKeyword.Auto;
            _diagnostic.style.bottom = 14;
            // The idle pause (M1.E, CANON ruling 38): one line across the middle while a game left alone waits.
            _paused = MakeLabel(root, 22, 0, 0);
            _paused.style.left = new Length(50, LengthUnit.Percent);
            _paused.style.top = new Length(42, LengthUnit.Percent);
            _paused.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            _paused.style.unityTextAlign = TextAnchor.MiddleCenter;

            // The carrying window (M1.5a): the nine places, what is in each and its mass, and which is the hand. It only
            // shows; the world runs on while it is open (v1's I1), and Tab closes it again.
            _carrying = new VisualElement();
            _carrying.style.position = Position.Absolute;
            _carrying.style.right = 28;
            _carrying.style.top = new Length(26, LengthUnit.Percent);
            _carrying.style.paddingLeft = 16;
            _carrying.style.paddingRight = 16;
            _carrying.style.paddingTop = 10;
            _carrying.style.paddingBottom = 12;
            _carrying.style.borderTopLeftRadius = 6;
            _carrying.style.borderTopRightRadius = 6;
            _carrying.style.borderBottomLeftRadius = 6;
            _carrying.style.borderBottomRightRadius = 6;
            _carrying.style.backgroundColor = new Color(0.05f, 0.05f, 0.04f, 0.62f);
            _carrying.style.display = DisplayStyle.None;
            _carrying.pickingMode = PickingMode.Ignore;
            Label title = RowLabel(18);
            title.text = "Carrying";
            title.style.marginBottom = 6;
            _carrying.Add(title);
            for (int i = 0; i < _places.Length; i++)
            {
                _places[i] = RowLabel(16);
                _carrying.Add(_places[i]);
            }
            root.Add(_carrying);
            SetCarrying(0, null);

            // The body's bars (M1.F): at the bottom left over the diagnostic line, a name, a track with its fill, the per cent.
            _bars = new VisualElement();
            _bars.style.position = Position.Absolute;
            _bars.style.left = 18;
            _bars.style.bottom = 44;
            _bars.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < _barFills.Length; i++)
            {
                VisualElement row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginTop = 4;
                row.pickingMode = PickingMode.Ignore;
                _barNames[i] = RowLabel(15);
                _barNames[i].style.width = 78;
                row.Add(_barNames[i]);
                VisualElement track = new VisualElement();
                track.style.width = 180;
                track.style.height = 11;
                track.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
                track.style.borderTopWidth = 1;
                track.style.borderBottomWidth = 1;
                track.style.borderLeftWidth = 1;
                track.style.borderRightWidth = 1;
                Color edge = new Color(1f, 1f, 1f, 0.35f);
                track.style.borderTopColor = edge;
                track.style.borderBottomColor = edge;
                track.style.borderLeftColor = edge;
                track.style.borderRightColor = edge;
                track.style.borderTopLeftRadius = 3;
                track.style.borderTopRightRadius = 3;
                track.style.borderBottomLeftRadius = 3;
                track.style.borderBottomRightRadius = 3;
                track.pickingMode = PickingMode.Ignore;
                _barFills[i] = new VisualElement();
                _barFills[i].style.height = new Length(100, LengthUnit.Percent);
                _barFills[i].style.width = new Length(100, LengthUnit.Percent);
                _barFills[i].style.backgroundColor = BarColours[i];
                _barFills[i].style.borderTopLeftRadius = 2;
                _barFills[i].style.borderTopRightRadius = 2;
                _barFills[i].style.borderBottomLeftRadius = 2;
                _barFills[i].style.borderBottomRightRadius = 2;
                _barFills[i].pickingMode = PickingMode.Ignore;
                track.Add(_barFills[i]);
                row.Add(track);
                _barPercents[i] = RowLabel(15);
                _barPercents[i].style.marginLeft = 8;
                row.Add(_barPercents[i]);
                _bars.Add(row);
            }
            _bars.style.display = DisplayStyle.None;
            root.Add(_bars);
        }

        /// <summary>The founder's body as the server last told it, as bars (M1.F); the first call lets them show.</summary>
        public void SetBody(BodyBars.Bar[] bars)
        {
            if (_bars == null || bars == null) return;
            for (int i = 0; i < _barFills.Length && i < bars.Length; i++)
            {
                _barNames[i].text = bars[i].Name;
                _barFills[i].style.width = new Length((float)(bars[i].Share * 100.0), LengthUnit.Percent);
                _barPercents[i].text = bars[i].Percent.ToString(CultureInfo.InvariantCulture) + "%";
                _barPercents[i].style.color = bars[i].Stage == BodyBars.Stage.Danger ? DangerColour
                                            : bars[i].Stage == BodyBars.Stage.Word ? WordColour : Color.white;
                BarPercents[i] = bars[i].Percent;
            }
            _barsHeard = true;
            ShowBars();
        }

        /// <summary>H: the bars hidden, or shown again (M1.F).</summary>
        public void ToggleBars()
        {
            _barsWanted = !_barsWanted;
            ShowBars();
        }

        private void ShowBars()
        {
            if (_bars != null) _bars.style.display = BarsShown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static Label MakeLabel(VisualElement root, int fontSize, int left, int top)
        {
            Label label = new Label(string.Empty);
            label.style.position = Position.Absolute;
            label.style.left = left;
            label.style.top = top;
            label.style.fontSize = fontSize;
            label.style.color = Color.white;
            label.style.textShadow = new TextShadow { offset = new Vector2(1f, 1f), blurRadius = 2f, color = new Color(0f, 0f, 0f, 0.9f) };
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.pickingMode = PickingMode.Ignore;
            root.Add(label);
            return label;
        }

        private static Label RowLabel(int fontSize)
        {
            Label label = new Label(string.Empty);
            label.style.fontSize = fontSize;
            label.style.color = Color.white;
            label.style.textShadow = new TextShadow { offset = new Vector2(1f, 1f), blurRadius = 2f, color = new Color(0f, 0f, 0f, 0.9f) };
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 2;
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        /// <summary>The line a game left alone shows (M1.E, ruling 38), or nothing once it is played again.</summary>
        public void SetPaused(bool paused)
        {
            if (_paused != null) _paused.text = paused ? PausedWords : string.Empty;
        }

        /// <summary>What the paused line says: that the world and its clock have stopped, and what starts them again.</summary>
        public const string PausedWords = "Paused — the world and its time wait while you are away. Press any key to go on.";

        public void SetClock(string text)
        {
            if (_clock != null) _clock.text = text;
        }

        /// <summary>The word for the founder's state under the clock (FP.1), or nothing.</summary>
        public void SetCondition(string text)
        {
            if (_condition != null) _condition.text = text ?? string.Empty;
        }

        /// <summary>A notice under the words for a while (FP.2): a death's explanation.</summary>
        public void SetNotice(string text, float seconds)
        {
            if (_notice == null) return;
            _notice.text = text ?? string.Empty;
            _noticeUntil = Time.time + Mathf.Max(0f, seconds);
        }

        private void Update()
        {
            if (_notice != null && _noticeUntil > 0f && Time.time > _noticeUntil)
            {
                _notice.text = string.Empty;
                _noticeUntil = 0f;
            }
        }

        public void SetVerb(string text)
        {
            if (_verb != null) _verb.text = text;
        }

        public void SetDiagnostic(string text)
        {
            if (_diagnostic != null) _diagnostic.text = text;
        }

        public void ToggleCarrying()
        {
            if (_carrying == null) return;
            _carryingOpen = !_carryingOpen;
            _carrying.style.display = _carryingOpen ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>What each place holds and its mass, and which place is the hand.</summary>
        public void SetCarrying(byte hand, IReadOnlyList<CarriedThing> things)
        {
            for (int place = 1; place <= _places.Length; place++)
            {
                Label row = _places[place - 1];
                if (row == null) continue;
                string what = "·";
                if (things != null)
                    foreach (CarriedThing t in things)
                        if (t.Place == place)
                            what = ThingWords.Describe(t.Definition, t.Item.State) + "   " + ThingWords.MassOf(t.Definition, t.Item.State).ToString("0.00", CultureInfo.InvariantCulture) + " kg";
                bool inHand = place == hand;
                row.text = place + "    " + what + (inHand ? "    in hand" : "");
                row.style.color = inHand ? HandColour : Color.white;
            }
        }
    }
}
