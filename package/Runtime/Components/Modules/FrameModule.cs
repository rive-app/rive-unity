namespace Rive.Components
{
    /// <summary>
    /// One piece of per-frame async work the Orchestrator drives. Each phase
    /// runs across every module in order, and a module only overrides the
    /// phases it cares about. Nothing here may wait except JoinForSync.
    /// </summary>
    internal abstract class FrameModule
    {
        /// Lands whatever has finished. Never waits.
        internal virtual void Poll() { }

        /// Before panels tick.
        internal virtual void BeforeTick() { }

        /// After panels have sent this frame's advances.
        internal virtual void Submit(bool advancedAny) { }

        /// Synchronous panels are about to read results, so land them now.
        internal virtual void JoinForSync() { }

        /// Late in the frame, before rendering.
        internal virtual void LateFrame() { }

        internal virtual void OnPanelRemoved(RivePanel panel) { }

        internal virtual void Dispose() { }
    }

    /// <summary>
    /// The view model values channel: captures after the tick pass, and
    /// delivering captures and reads in order.
    /// </summary>
    internal sealed class PropertyCaptureFrameModule : FrameModule
    {
        internal override void Poll()
        {
            PropertyCallbacksHub.Instance.PollProducerCapture();
        }

        internal override void Submit(bool advancedAny)
        {
            if (advancedAny)
            {
                PropertyCallbacksHub.Instance.SubmitProducerCapture();
            }
        }

        internal override void JoinForSync()
        {
            PropertyCallbacksHub.Instance.JoinProducerCapture();
        }
    }

#if RIVE_USING_EXPERIMENTAL
    /// <summary>
    /// Render texture images: bound before panels tick so they show on the
    /// first frame, and flushed again late so a build goes out with this
    /// frame's render.
    /// </summary>
    internal sealed class RenderImageFrameModule : FrameModule
    {
        internal override void BeforeTick()
        {
            RenderTextureImageManager.Instance.Tick();
        }

        internal override void LateFrame()
        {
            RenderImageCommandQueue.FlushIfActive();
        }
    }
#endif
}
