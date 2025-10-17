using System;
using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// Interface for providing input events to a RivePanel.
    /// </summary>
    public interface IPanelInputProvider
    {
        /// <summary>
        /// Event fired when a pointer is pressed. The Vector2 parameter represents the normalized local point in the panel,
        /// where coordinates are in the range [0,1] with (0,0) at bottom-left and (1,1) at top-right.
        /// </summary>
        event Action<PanelPointerEvent> PointerPressed;

        /// <summary>
        /// Event fired when a pointer is released. The Vector2 parameter represents the normalized local point in the panel,
        /// where coordinates are in the range [0,1] with (0,0) at bottom-left and (1,1) at top-right.
        /// </summary>
        event Action<PanelPointerEvent> PointerReleased;

        /// <summary>
        /// Event fired when a pointer is moved. The Vector2 parameter represents the normalized local point in the panel,
        /// where coordinates are in the range [0,1] with (0,0) at bottom-left and (1,1) at top-right.
        /// </summary>
        event Action<PanelPointerEvent> PointerMoved;


        /// <summary>
        /// Event fired when a pointer exits the panel. The Vector2 parameter represents the normalized local point in the panel,
        /// where coordinates are in the range [0,1] with (0,0) at bottom-left and (1,1) at top-right.
        /// </summary>
        event Action<PanelPointerEvent> PointerExited;

        /// <summary>
        /// Event fired when a pointer enters the panel. The Vector2 parameter represents the normalized local point in the panel,
        /// where coordinates are in the range [0,1] with (0,0) at bottom-left and (1,1) at top-right.
        /// </summary>
        event Action<PanelPointerEvent> PointerEntered;
    }
}
