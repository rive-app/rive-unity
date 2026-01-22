namespace Rive.Components
{
    /// <summary>
    /// Controls how often a widget requests a redraw from its panel.
    /// </summary>
    public enum DrawOptimizationOptions
    {
        /// <summary>
        /// The widget will request a redraw every frame.
        /// </summary>
        AlwaysDraw = 0,

        /// <summary>
        /// The widget will only request redraws when its underlying content changes.
        /// </summary>
        DrawWhenChanged = 1
    }
}


