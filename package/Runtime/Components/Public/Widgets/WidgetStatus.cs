using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// Represents the current status of a Rive widget.
    /// </summary>
    public enum WidgetStatus
    {
        /// <summary>
        /// The widget has not yet been initialized. This is the default state.
        /// </summary>
        Uninitialized = 0,

        /// <summary>
        /// The widget is currently setting up the Rive graphic.
        /// </summary>
        Loading = 1,

        /// <summary>
        /// The widget is ready to display the Rive graphic.
        /// </summary>
        Loaded = 2,

        /// <summary>
        /// An error occurred while loading the Rive graphic.
        /// </summary>
        Error = 3
    }
}
