#if RIVE_USING_EXPERIMENTAL
namespace Rive.Components
{
    /// <summary>
    /// Experimental. How a panel antialiases what it draws.
    /// </summary>
    public enum AntialiasingMode
    {
        /// <summary>
        /// The renderer's usual antialiasing.
        /// </summary>
        Default = 0,

        /// <summary>
        /// The renderer's inkbleed antialiasing, which can be faster on some devices. Some content
        /// can look different than with <see cref="Default"/>, so check how yours looks. Only
        /// takes effect on Direct3D 11, Direct3D 12, Vulkan and OpenGL, not Metal, see
        /// <see cref="RivePanel.EffectiveAntialiasingMode"/>.
        /// </summary>
        Inkbleed = 1,
    }
}
#endif
