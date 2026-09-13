using UnityEngine;
using UnityEngine.InputSystem;

namespace EarthGame.Client
{
    /// <summary>
    /// The fullscreen key fills the screen the game's window is on, borderless, and gives the window back as it was
    /// (William's ruling of 2026-09-13: "add the ability to enter fullscreen mode with f11"). <c>Bootstrap</c> polls it once
    /// a frame, in the menu and in play alike, and the controls asset binds it (<see cref="Controls.Fullscreen"/>). It keeps
    /// the window's own screen: a window sent to the primary screen lands on the one William watches television on, as the
    /// game started without <c>-monitor</c> once did.
    /// </summary>
    public static class WindowMode
    {
        /// <summary>
        /// How many times the key has asked, in any run: how the controls scenario sees a press reach the window, when a
        /// windowless run has no window to fill.
        /// </summary>
        public static int Requested { get; private set; }

        private static InputAction _action;
        private static bool _looked;
        private static bool _haveWindow;
        private static int _windowWidth, _windowHeight;
        private static Vector2Int _windowPosition;

        /// <summary>Once a frame: when the key was pressed, fills the window's screen or gives the window back.</summary>
        public static void Poll()
        {
            if (!_looked)
            {
                _looked = true;
                InputActionMap map = InputSystem.actions != null ? InputSystem.actions.FindActionMap(Controls.Map, false) : null;
                _action = map != null ? map.FindAction(Controls.Fullscreen, false) : null;
                // The menu comes before the founder's hands enable the map, so the key is enabled on its own.
                if (_action == null) Debug.LogError("[window] the controls asset lacks " + Controls.Fullscreen + "; the window keeps its mode");
                else _action.Enable();
            }
            if (_action == null || !_action.WasPressedThisFrame()) return;
            Requested++;
            if (!Application.isBatchMode) Toggle();
        }

        private static void Toggle()
        {
            DisplayInfo screen = Screen.mainWindowDisplayInfo;
            bool filling = Screen.fullScreenMode == FullScreenMode.Windowed;
            if (filling)
            {
                _haveWindow = true;
                _windowWidth = Screen.width;
                _windowHeight = Screen.height;
                _windowPosition = Screen.mainWindowPosition;
                Screen.SetResolution(screen.width, screen.height, FullScreenMode.FullScreenWindow);
                Screen.MoveMainWindowTo(screen, Vector2Int.zero);
            }
            else
            {
                // A game that started filling its screen has no window of its own yet: it is given one centred on that screen.
                int width = _haveWindow ? _windowWidth : Mathf.Min(1280, screen.width);
                int height = _haveWindow ? _windowHeight : Mathf.Min(720, screen.height);
                Vector2Int at = _haveWindow ? _windowPosition : new Vector2Int((screen.width - width) / 2, (screen.height - height) / 2);
                Screen.SetResolution(width, height, FullScreenMode.Windowed);
                Screen.MoveMainWindowTo(screen, at);
            }
            Debug.Log("[window] " + (filling ? "filling " : "leaving ") + screen.name);
        }
    }
}
