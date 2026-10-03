using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Rive
{
    internal enum DrawOpCode : uint
    {
        Save = 0,
        Restore = 1,
        Transform = 2,
        DrawArtboard = 3,
        ClipRect = 4,
        AlignArtboard = 5,
        AlignArtboardWithFrame = 6,
    }

    /// <summary>
    /// One entry in a frame's draw list. Mirrors rive::unity::DrawOp, so only
    /// add to the end.
    ///
    /// The float slots mean different things per op, so use the helpers below
    /// instead of setting fields directly.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct DrawOp
    {
        public NativeArtboardHandle Artboard;
        public uint Code;
        public uint Fit;
        public float AlignX;
        public float AlignY;
        public float F0;
        public float F1;
        public float F2;
        public float F3;
        public float F4;
        public float F5;

        public static DrawOp Save() =>
            new DrawOp { Code = (uint)DrawOpCode.Save };

        public static DrawOp Restore() =>
            new DrawOp { Code = (uint)DrawOpCode.Restore };

        public static DrawOp Transform(Matrix3x2 m) => new DrawOp
        {
            Code = (uint)DrawOpCode.Transform,
            F0 = m.M11,
            F1 = m.M12,
            F2 = m.M21,
            F3 = m.M22,
            F4 = m.M31,
            F5 = m.M32,
        };

        public static DrawOp Translate(float x, float y) =>
            Transform(new Matrix3x2(1f, 0f, 0f, 1f, x, y));

        public static DrawOp DrawArtboard(NativeArtboardHandle artboard) => new DrawOp
        {
            Code = (uint)DrawOpCode.DrawArtboard,
            Artboard = artboard,
        };

        /// Axis aligned rect from the origin. Swap width and height where the
        /// platform needs the other orientation.
        public static DrawOp ClipRect(float width, float height) => new DrawOp
        {
            Code = (uint)DrawOpCode.ClipRect,
            F0 = width,
            F1 = height,
        };

        public static DrawOp Align(
            Fit fit, Alignment alignment, NativeArtboardHandle artboard, float scaleFactor)
            => new DrawOp
            {
                Code = (uint)DrawOpCode.AlignArtboard,
                Artboard = artboard,
                Fit = (uint)fit,
                AlignX = alignment.X,
                AlignY = alignment.Y,
                F0 = scaleFactor,
            };

        public static DrawOp AlignWithFrame(
            Fit fit,
            Alignment alignment,
            NativeArtboardHandle artboard,
            AABB frame,
            float scaleFactor)
            => new DrawOp
            {
                Code = (uint)DrawOpCode.AlignArtboardWithFrame,
                Artboard = artboard,
                Fit = (uint)fit,
                AlignX = alignment.X,
                AlignY = alignment.Y,
                F0 = frame.minX,
                F1 = frame.minY,
                F2 = frame.maxX,
                F3 = frame.maxY,
                F4 = scaleFactor,
            };
    }
}
