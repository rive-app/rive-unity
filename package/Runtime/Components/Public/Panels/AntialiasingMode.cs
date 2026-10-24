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
        /// Antialiases strokes by bleeding them out half a pixel, which can be faster on some
        /// devices. Fills aren't antialiased, and fills that don't use the clockwise fill rule
        /// are drawn as if they did. Layer masks are drawn without their mask. Only takes effect
        /// on Direct3D 11, Direct3D 12, Vulkan and OpenGL, not Metal, see
        /// <see cref="RivePanel.EffectiveAntialiasingMode"/>.
        /// </summary>
        Inkbleed = 1,
    }
}
#endif
