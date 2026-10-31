namespace Rive
{
    /// <summary>
    /// A state machine's focus as of its last advance or focus move.
    /// </summary>
    internal readonly struct FocusState
    {
        public FocusState(bool hasFocus, bool expectsKeyboardInput, bool hasFocusNodes)
        {
            HasFocus = hasFocus;
            ExpectsKeyboardInput = expectsKeyboardInput;
            HasFocusNodes = hasFocusNodes;
            HasBounds = false;
            Bounds = default;
        }

        private FocusState(FocusState state, AABB bounds)
        {
            this = state;
            HasBounds = true;
            Bounds = bounds;
        }

        /// <summary>
        /// True when a focus node currently holds primary focus.
        /// </summary>
        public bool HasFocus { get; }

        /// <summary>
        /// True when the focused node wants keyboard input. False when nothing is focused.
        /// </summary>
        public bool ExpectsKeyboardInput { get; }

        /// <summary>
        /// True when the state machine has anything that can take focus, even if nothing has it yet.
        /// </summary>
        public bool HasFocusNodes { get; }

        /// <summary>
        /// True when Bounds holds the focused node's bounds.
        /// </summary>
        public bool HasBounds { get; }

        /// <summary>
        /// The focused node's bounds in artboard space.
        /// </summary>
        public AABB Bounds { get; }

        internal FocusState WithBounds(AABB bounds) => new FocusState(this, bounds);

        /// <summary>
        /// The same, with nothing focused.
        /// </summary>
        internal FocusState WithoutFocus() => new FocusState(false, false, HasFocusNodes);
    }
}
