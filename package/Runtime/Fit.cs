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
        Fill = 0,

        /// <summary>
        /// (Default) Rive content will be contained within the bounds,
        /// preserving the aspect ratio.
        ///
        /// If the ratios differ, then a portion of the bounds will
        /// be unused.
        /// </summary>
        Contain = 1,

        /// <summary>
        /// Rive will cover the bounds, preserving the aspect ratio.
        ///
        /// If the Rive content has a different ratio to the bounds,
        /// then the Rive content will be clipped.
        /// </summary>
        Cover = 2,

        /// <summary>
        /// Rive content will fill to the width of the bounds.
        ///
        /// This may result in clipping or unfilled bound space.
        /// </summary>
        FitWidth = 3,

        /// <summary>
        /// Rive content will fill to the height of the bounds.
        ///
        /// This may result in clipping or unfilled bound space.
        /// </summary>
        FitHeight = 4,

        /// <summary>
        /// Rive content will render to the size of its artboard,
        /// which may result in clipping or unfilled bound space.
        /// </summary>
        None = 5,

        /// <summary>
        /// Rive content is scaled down to the size of the bounds,
        /// preserving the aspect ratio.
        ///
        /// This is equivalent to Contain when the content is larger
        /// than the bounds. If the bounds is larger, then ScaleDown
        /// will not scale up.
        /// </summary>
        ScaleDown = 6,

        /// <summary>
        /// Rive content will be resized automatically based on layout constraints of the artboard.
        /// </summary>
        Layout = 7

    }
}
