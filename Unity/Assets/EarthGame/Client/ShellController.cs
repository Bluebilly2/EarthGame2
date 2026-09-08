using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace EarthGame.Client
{
    /// <summary>
    /// The minimal shell: new world, continue, quit (M1.A contract, promise 7). Three buttons in UI Toolkit; the
    /// bootstrap decides what each does. Never shown when the launch mode came from the command line.
    /// </summary>
    public sealed class ShellController : MonoBehaviour
    {
        private UIDocument _document;
        private Button _continue;
        private Label _status;

        public event Action NewWorld;
        public event Action Continue;
        public event Action Quit;

        public void Build(bool canContinue, string continueLabel)
        {
            PanelSettings panel = Resources.Load<PanelSettings>(HudController.PanelResource);
            if (panel == null)
            {
                Debug.LogError("[shell] PanelSettings missing at Resources/" + HudController.PanelResource);
                return;
            }
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panel;
            VisualElement root = _document.rootVisualElement;
            root.style.flexGrow = 1;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);

            Label title = new Label("EarthGame2");
            title.style.fontSize = 48;
            title.style.color = new Color(0.93f, 0.9f, 0.82f);
            title.style.marginBottom = 8;
            root.Add(title);
            Label sub = new Label("Bherwerre Peninsula, Jervis Bay. One person, a real Earth.");
            sub.style.fontSize = 16;
            sub.style.color = new Color(0.7f, 0.7f, 0.68f);
            sub.style.marginBottom = 36;
            root.Add(sub);

            root.Add(MakeButton("New world", () => NewWorld?.Invoke()));
            _continue = MakeButton(canContinue ? continueLabel : "Continue (no world yet)", () => Continue?.Invoke());
            _continue.SetEnabled(canContinue);
            root.Add(_continue);
            root.Add(MakeButton("Quit", () => Quit?.Invoke()));

            _status = new Label(string.Empty);
            _status.style.fontSize = 14;
            _status.style.color = new Color(0.6f, 0.62f, 0.6f);
            _status.style.marginTop = 24;
            root.Add(_status);
        }

        private static Button MakeButton(string text, Action onClick)
        {
            Button b = new Button(onClick) { text = text };
            b.style.width = 320;
            b.style.height = 48;
            b.style.fontSize = 20;
            b.style.marginBottom = 10;
            return b;
        }

        public void SetStatus(string text)
        {
            if (_status != null) _status.text = text;
        }

        /// <summary>What a scenario does instead of a mouse: the New world button's own action.</summary>
        public void ClickNewWorld() => NewWorld?.Invoke();

        public void Close()
        {
            if (_document != null) Destroy(_document);
            Destroy(this);
        }
    }
}
