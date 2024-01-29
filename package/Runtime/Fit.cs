namespace Rive
{
    /// <summary>
    /// Fit determines how the Rive content will be fitted to the view.
    /// </summary>
    public enum Fit : byte
    {
        /// <summary>
        /// Rive content will fill the available bounds.
        ///
        /// If the aspect ratios differ, then the Rive
        /// content will be stretched.
        /// </summary>
        fill = 0,

        /// <summary>
        /// (Default) Rive content will be contained within the bounds,
        /// preserving the aspect ratio.
        ///
        /// If the ratios differ, then a portion of the bounds will
        /// be unused.
        /// </summary>
        contain = 1,

        /// <summary>
        /// Rive will cover the bounds, preserving the aspect ratio.
        ///
        /// If the Rive content has a different ratio to the bounds,
        /// then the Rive content will be clipped.
        /// </summary>
        cover = 2,

        /// <summary>
        /// Rive content will fill to the width of the bounds.
        ///
        /// This may result in clipping or unfilled bound space.
        /// </summary>
        fitWidth = 3,

        /// <summary>
        /// Rive content will fill to the height of the bounds.
        ///
        /// This may result in clipping or unfilled bound space.
        /// </summary>
        fitHeight = 4,

        /// <summary>
        /// Rive content will render to the size of its artboard,
        /// which may result in clipping or unfilled bound space.
        /// </summary>
        none = 5,

        /// <summary>
        /// Rive content is scaled down to the size of the bounds,
        /// preserving the aspect ratio.
        ///
        /// This is equivalent to Contain when the content is larger
        /// than the bounds. If the bounds is larger, then ScaleDown
        /// will not scale up.
        /// </summary>
        scaleDown = 6
    }
}
