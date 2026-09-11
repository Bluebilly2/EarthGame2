using System.Collections.Generic;
using System.Globalization;
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
        private VisualElement _carrying;
        private readonly Label[] _places = new Label[Hands.Places];
        private bool _carryingOpen;

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
            _verb = MakeLabel(root, 18, 0, 0);
            _verb.style.left = new Length(50, LengthUnit.Percent);
            _verb.style.top = new Length(56, LengthUnit.Percent);
            _verb.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            _diagnostic = MakeLabel(root, 14, 18, 0);
            _diagnostic.style.top = StyleKeyword.Auto;
            _diagnostic.style.bottom = 14;

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

        public void SetClock(string text)
        {
            if (_clock != null) _clock.text = text;
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
                            what = t.Definition.DisplayName + "   " + t.Definition.MassKg.ToString("0.0", CultureInfo.InvariantCulture) + " kg";
                bool inHand = place == hand;
                row.text = place + "    " + what + (inHand ? "    in hand" : "");
                row.style.color = inHand ? HandColour : Color.white;
            }
        }
    }
}
