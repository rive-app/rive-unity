namespace Rive.Components
{
    /// <summary>
    /// Determines how the visibility optimization should behave
    /// </summary>
    public enum VisibilityOptimizationMode
    {
        /// <summary>
        /// Only renders when the gameobject is rendering
        /// </summary>
        RenderWhenVisible = 0,

        /// <summary>
        /// Always renders regardless of gameobject's visibility
        /// </summary>
        AlwaysRender = 1
    }
}
