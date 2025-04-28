using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Rive
{
    /// <summary>
    /// A view model instance property that holds a color.
    /// </summary>
    public sealed class ViewModelInstanceColorProperty : ViewModelInstancePrimitiveProperty<UnityEngine.Color>
    {
        internal ViewModelInstanceColorProperty(IntPtr instanceValuePtr, ViewModelInstance rootInstance) : base(instanceValuePtr, rootInstance)
        {
        }

        private static int ColorToArgb(UnityEngine.Color color)
        {
            return (Mathf.RoundToInt(color.a * 255) << 24) |
                   (Mathf.RoundToInt(color.r * 255) << 16) |
                   (Mathf.RoundToInt(color.g * 255) << 8) |
                   Mathf.RoundToInt(color.b * 255);
        }

        private static int Color32ToArgb(Color32 color)
        {
            return (color.a << 24) |
                   (color.r << 16) |
                   (color.g << 8) |
                   color.b;
        }

        private static UnityEngine.Color ArgbToColor(int argb)
        {
            return new UnityEngine.Color(
                ((argb >> 16) & 0xFF) / 255f, // R (from ARGB)
                ((argb >> 8) & 0xFF) / 255f,  // G (from ARGB)
                (argb & 0xFF) / 255f,         // B (from ARGB)
                ((argb >> 24) & 0xFF) / 255f  // A (from ARGB)
            );
        }

        private static Color32 ArgbToColor32(int argb)
        {
            return new Color32(
                (byte)((argb >> 16) & 0xFF), // R (from ARGB)
                (byte)((argb >> 8) & 0xFF),  // G (from ARGB)
                (byte)(argb & 0xFF),         // B (from ARGB)
                (byte)((argb >> 24) & 0xFF)  // A (from ARGB)
            );
        }

        /// <summary>
        /// Gets or sets the color value as a Unity Color.
        /// </summary>
        public override UnityEngine.Color Value
        {
            get => ArgbToColor(getViewModelInstanceColorValue(InstancePropertyPtr));
            set => setViewModelInstanceColorValue(InstancePropertyPtr, ColorToArgb(value));
        }

        /// <summary>
        /// Gets or sets the color value as a Color32
        /// </summary>
        public Color32 Value32
        {
            get => ArgbToColor32(getViewModelInstanceColorValue(InstancePropertyPtr));
            set => setViewModelInstanceColorValue(InstancePropertyPtr, Color32ToArgb(value));
        }


        [DllImport(NativeLibrary.name)]
        private static extern int getViewModelInstanceColorValue(IntPtr instanceProperty);

        [DllImport(NativeLibrary.name)]
        private static extern void setViewModelInstanceColorValue(IntPtr instanceProperty, int value);
    }
}
