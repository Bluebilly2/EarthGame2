using System.Collections.Generic;
using EarthGame.Client;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace EarthGame.Tests.Editor
{
    /// <summary>
    /// The controls asset is the project's actions and holds every action the code reads (M1.5a promise 1), each bound,
    /// and each with a binding for the keyboard or the mouse but the gamepad's shoulders, which step the hand as the
    /// wheel does: code names actions, the asset binds them, and nothing else does.
    /// </summary>
    public sealed class ControlsTests
    {
        [Test]
        public void TheProjectsActionsAreTheControlsAssetAndHoldEveryActionTheCodeReads()
        {
            InputActionAsset asset = InputSystem.actions;
            Assert.That(asset, Is.Not.Null, "no project-wide actions");
            Assert.That(AssetDatabase.GetAssetPath(asset), Is.EqualTo("Assets/InputSystem_Actions.inputactions"));
            List<string> missing = Controls.Missing(asset);
            Assert.That(missing, Is.Empty, "the asset lacks " + string.Join(", ", missing));
            InputActionMap map = asset.FindActionMap(Controls.Map, true);
            foreach (string name in Controls.Names)
            {
                InputAction action = map.FindAction(name, true);
                Assert.That(action.bindings.Count, Is.GreaterThan(0), name + " is bound to nothing");
                if (Controls.IsGamepadOnly(name)) continue;
                bool atTheDesk = false;
                foreach (InputBinding binding in action.bindings)
                {
                    string path = binding.effectivePath ?? string.Empty;
                    if (!binding.isComposite && (path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>") || path.StartsWith("<Pointer>"))) atTheDesk = true;
                }
                Assert.That(atTheDesk, Is.True, name + " has no keyboard or mouse binding");
            }
        }
    }
}
