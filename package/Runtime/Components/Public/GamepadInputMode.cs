namespace Rive.Components
{
    /// <summary>
    /// When a panel sends Unity's gamepads to its widgets. Needs the Input System package and its input handling turned on.
    /// </summary>
    public enum GamepadInputMode
    {
        /// <summary>
        /// Every frame, whether or not the panel is selected, so in-game UI hears the controller without taking Unity's selection.
        /// </summary>
        Always = 0,

        /// <summary>
        /// Only while Unity has the panel selected. When it loses the selection, every button is released and every stick centered, so nothing stays held. For scenes where several panels would otherwise all react to one controller.
        /// </summary>
        WhenSelected = 1,

        /// <summary>
        /// Never. Scripts can still send gamepad values with <see cref="StateMachine.SetGamepadButton"/> and <see cref="StateMachine.SetGamepadAxis"/>.
        /// </summary>
        Disabled = 2,
    }
}
