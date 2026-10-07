using System;

namespace Rive
{
    /// <summary>
    /// Tests only. Stamps along the render texture image path, from a build
    /// being queued to the panel that shows it drawing. Null unless a test
    /// listens, so the default path pays a null check.
    /// </summary>
    internal static class ImagePipelineTrace
    {
        internal enum Step
        {
            // Value is true for the first build, a refresh or a rebind.
            BuildQueued = 0,
            SourceNotReady = 1,
            // Value is how many builds are waiting.
            FlushBlocked = 2,
            BatchSent = 3,
            BatchLanded = 4,
            // Value is the generation.
            FillIssued = 5,
            // Value is the widget.
            AdvanceSent = 6,
            AdvanceLanded = 7,
            // Value is the panel. Its draws were recorded.
            PanelDrawn = 8,
            // Value is the panel. A redraw was asked for.
            RedrawRequested = 9,
        }

        /// Step, image handle (0 when there isn't one), value.
        internal static Action<Step, uint, object> ForTests;
    }
}
