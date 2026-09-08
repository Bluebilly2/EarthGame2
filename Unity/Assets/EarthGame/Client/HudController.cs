using UnityEngine;
using UnityEngine.UIElements;

namespace EarthGame.Client
{
    /// <summary>
    /// The M1 HUD in UI Toolkit (ARCHITECTURE §8): a crosshair, the verb line (empty until verbs exist), the local
    /// clock, and a diagnostic line the frames carry so a picture says where and when it was taken. Built in code
    /// against the PanelSettings asset the project setup creates; readability floor 3:1 is met by white text with
    /// a dark shadow on any ground.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        public const string PanelResource = "EarthGame/HudPanel";

        private UIDocument _document;
        private Label _clock;
        private Label _verb;
        private Label _diagnostic;

        public PanelSettings Panel => _document != null ? _document.panelSettings : null;

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
    }
}
