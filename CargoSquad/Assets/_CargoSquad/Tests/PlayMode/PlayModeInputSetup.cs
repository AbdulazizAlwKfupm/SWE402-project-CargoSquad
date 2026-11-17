#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace CargoSquad.Tests
{
    // The editor has no focused Game view in batch mode. Send simulated input to gameplay consistently.
    [SetUpFixture]
    public sealed class PlayModeInputSetup
    {
        private InputSettings.EditorInputBehaviorInPlayMode input;
        private InputSettings.BackgroundBehavior background;
        [OneTimeSetUp]
        public void SetUp()
        {
            input = InputSystem.settings.editorInputBehaviorInPlayMode;
            background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        }
        [OneTimeTearDown]
        public void TearDown()
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = input;
            InputSystem.settings.backgroundBehavior = background;
        }
    }
}
#endif
