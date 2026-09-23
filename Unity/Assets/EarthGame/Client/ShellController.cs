using System;
using System.Collections.Generic;
using System.IO;
using EarthGame.Engine;
using EarthGame.Shared;
using UnityEngine;
using UnityEngine.UIElements;

namespace EarthGame.Client
{
    /// <summary>
    /// The minimal shell: a new world in a chosen place, continue, quit (M1.A contract, promise 7; the places since CANON
    /// ruling 44, 2026-09-22). Buttons in UI Toolkit; the bootstrap decides what each does. Never shown when the launch mode
    /// came from the command line.
    /// </summary>
    public sealed class ShellController : MonoBehaviour
    {
        /// <summary>A place the shell offers a new world in (CANON ruling 44): a region with its ground on disk, or a name greyed as not yet.</summary>
        public sealed class Place
        {
            public Region Region;
            public string Name;
            public bool Ready;
            public string Note;
        }

        private UIDocument _document;
        private Button _continue;
        private Label _status;
        private Region _firstReady;

        /// <summary>The player chose a place for a new world.</summary>
        public event Action<Region> NewWorldIn;
        public event Action Continue;
        public event Action Quit;

        /// <summary>
        /// The places a new world can be made in (CANON ruling 44): the regions this build knows whose bake is on disk are
        /// offered; the places named for later are shown greyed as not yet, so the list says where the game is going without
        /// pretending it is there. A region whose ground is not fetched is greyed the same way, with the reason.
        /// </summary>
        public static List<Place> DefaultPlaces()
        {
            List<Place> places = new List<Place>();
            // The whole valley (ruling 45, WG.2b) is offered since its memory work and the far forest's bound landed (2026-09-23):
            // a new one is made inside the game in some four minutes at a peak near 10 GB, where it would have been ten minutes and
            // 17 GB. Bherwerre stays first, the place the scenarios' new world is made in; the 8 km valley stays with its worlds.
            foreach (Region region in new[] { Region.Bherwerre, Region.KangarooValleyWhole, Region.KangarooValley })
            {
                bool ready = File.Exists(Path.Combine(RegionDataLocator.DataDir(region), "heights.json"));
                places.Add(new Place { Region = region, Name = region.DisplayName, Ready = ready, Note = ready ? "" : "no ground fetched" });
            }
            foreach (string name in new[] { "Wilsons Promontory", "Blue Mountains, the Grose Valley", "Alice Springs, the MacDonnell Ranges" })
                places.Add(new Place { Region = null, Name = name, Ready = false, Note = "not yet" });
            return places;
        }

        public void Build(bool canContinue, string continueLabel) => Build(canContinue, continueLabel, null);

        public void Build(bool canContinue, string continueLabel, IReadOnlyList<Place> places)
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
            Label sub = new Label("One person, a real Earth.");
            sub.style.fontSize = 16;
            sub.style.color = new Color(0.7f, 0.7f, 0.68f);
            sub.style.marginBottom = 28;
            root.Add(sub);

            Label where = new Label("New world in");
            where.style.fontSize = 16;
            where.style.color = new Color(0.7f, 0.7f, 0.68f);
            where.style.marginBottom = 6;
            root.Add(where);
            _firstReady = null;
            foreach (Place place in places ?? DefaultPlaces())
            {
                Region region = place.Region;
                bool offered = place.Ready && region != null;
                Button b = MakeButton(offered ? place.Name : place.Name + " (" + place.Note + ")", () => { if (region != null) NewWorldIn?.Invoke(region); });
                b.SetEnabled(offered);
                if (offered && _firstReady == null) _firstReady = region;
                root.Add(b);
            }
            Label gap = new Label(string.Empty);
            gap.style.height = 14;
            root.Add(gap);
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

        /// <summary>What a scenario does instead of a mouse: a new world in the first place that is offered.</summary>
        public void ClickNewWorld()
        {
            if (_firstReady != null) NewWorldIn?.Invoke(_firstReady);
        }

        public void Close()
        {
            if (_document != null) Destroy(_document);
            Destroy(this);
        }
    }
}
