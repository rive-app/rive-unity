using UnityEngine;
using System;
using System.Runtime.InteropServices;

namespace Rive
{
    /// <summary>
    /// Represents a Component of a Rive Artboard.
    /// </summary>
    public class Component
    {
        private readonly IntPtr m_nativeComponent;

        internal IntPtr NativeComponent
        {
            get { return m_nativeComponent; }
        }

        internal Component(IntPtr nativeComponent)
        {
            m_nativeComponent = nativeComponent;
        }

        ~Component()
        {
            unrefArtboardComponent(m_nativeComponent);
        }

        public AABB ComputeBounds()
        {
            return componentBounds(m_nativeComponent);
        }

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        internal static extern void unrefArtboardComponent(IntPtr component);

        [DllImport(NativeLibrary.name)]
        internal static extern AABB componentBounds(IntPtr component);
        #endregion
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct AABB
{
    public float minX;
    public float minY;
    public float maxX;
    public float maxY;
}
