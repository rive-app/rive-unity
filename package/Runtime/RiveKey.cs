namespace Rive
{
    /// <summary>
    /// A key for <see cref="StateMachine.KeyInput"/> and <see cref="StateMachineHandle.KeyInputAsync"/>.
    /// </summary>
    public enum RiveKey : ushort
    {
        Space = 32,
        Apostrophe = 39,
        Comma = 44,
        Minus = 45,
        Period = 46,
        Slash = 47,
        Key0 = 48,
        Key1 = 49,
        Key2 = 50,
        Key3 = 51,
        Key4 = 52,
        Key5 = 53,
        Key6 = 54,
        Key7 = 55,
        Key8 = 56,
        Key9 = 57,
        Semicolon = 59,
        Equal = 61,
        A = 65,
        B = 66,
        C = 67,
        D = 68,
        E = 69,
        F = 70,
        G = 71,
        H = 72,
        I = 73,
        J = 74,
        K = 75,
        L = 76,
        M = 77,
        N = 78,
        O = 79,
        P = 80,
        Q = 81,
        R = 82,
        S = 83,
        T = 84,
        U = 85,
        V = 86,
        W = 87,
        X = 88,
        Y = 89,
        Z = 90,
        LeftBracket = 91,
        Backslash = 92,
        RightBracket = 93,
        GraveAccent = 96,
        /// <summary>
        /// A non-US key, like the extra key next to left Shift on ISO keyboards.
        /// </summary>
        World1 = 161,
        /// <summary>
        /// A second non-US key.
        /// </summary>
        World2 = 162,
        Escape = 256,
        Enter = 257,
        Tab = 258,
        Backspace = 259,
        Insert = 260,
        Delete = 261,
        Right = 262,
        Left = 263,
        Down = 264,
        Up = 265,
        PageUp = 266,
        PageDown = 267,
        Home = 268,
        End = 269,
        CapsLock = 280,
        ScrollLock = 281,
        NumLock = 282,
        PrintScreen = 283,
        Pause = 284,
        F1 = 290,
        F2 = 291,
        F3 = 292,
        F4 = 293,
        F5 = 294,
        F6 = 295,
        F7 = 296,
        F8 = 297,
        F9 = 298,
        F10 = 299,
        F11 = 300,
        F12 = 301,
        F13 = 302,
        F14 = 303,
        F15 = 304,
        F16 = 305,
        F17 = 306,
        F18 = 307,
        F19 = 308,
        F20 = 309,
        F21 = 310,
        F22 = 311,
        F23 = 312,
        F24 = 313,
        F25 = 314,
        /// <summary>
        /// Keypad 0. The Kp keys are the numeric keypad's.
        /// </summary>
        Kp0 = 320,
        Kp1 = 321,
        Kp2 = 322,
        Kp3 = 323,
        Kp4 = 324,
        Kp5 = 325,
        Kp6 = 326,
        Kp7 = 327,
        Kp8 = 328,
        Kp9 = 329,
        KpDecimal = 330,
        KpDivide = 331,
        KpMultiply = 332,
        KpSubtract = 333,
        KpAdd = 334,
        KpEnter = 335,
        KpEqual = 336,
        LeftShift = 340,
        LeftControl = 341,
        LeftAlt = 342,
        /// <summary>
        /// Command on macOS, the Windows key on Windows.
        /// </summary>
        LeftSuper = 343,
        RightShift = 344,
        RightControl = 345,
        RightAlt = 346,
        RightSuper = 347,
        Menu = 348,
    }

    /// <summary>
    /// The modifier keys held with a <see cref="RiveKey"/>.
    /// </summary>
    [System.Flags]
    public enum RiveKeyModifiers : byte
    {
        None = 0,
        Shift = 1 << 0,
        Ctrl = 1 << 1,
        Alt = 1 << 2,
        /// <summary>
        /// Command on macOS, the Windows key on Windows. Rive's text shortcuts use it everywhere but Windows, where they use Ctrl.
        /// </summary>
        Meta = 1 << 3,
    }
}
