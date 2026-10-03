namespace Rive.Components
{
    /// <summary>
    /// The hit test behavior of a RiveWidget in relation to other elements.
    /// </summary>
    public enum HitTestBehavior
    {

        /// <summary>
        /// The bounds of the RiveWidget will consume all hits, even if there is no listener (hit area) at the target point. Content behind the RiveWidget will not receive hits. Recommended for non-blocking pointer input on asynchronous panels.
        /// </summary>
        Opaque = 0,

        /// <summary>
        /// The RiveWidget will only consume hits where there is a listener (hit area) at the target point. Content behind the RiveWidget will only receive hits if no listener was hit. This requires synchronous Rive hit testing and pointer handling, even on asynchronous panels, so Unity can resolve raycasts immediately.
        /// </summary>
        Translucent = 1,


        /// <summary>
        /// No hit testing will be performed on the RiveWidget.
        /// </summary>
        None = 3
    }
}
