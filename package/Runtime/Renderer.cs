using System;
using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Rive
{
    public abstract class Gradient
    {
        private Vec2D m_start;
        private List<Rive.Color> m_colors;
        private List<float> m_stops;

        public Vec2D start { get { return m_start; } }
        public List<Rive.Color> colors { get { return m_colors; } }
        public List<float> stops { get { return m_stops; } }

        public Gradient(Vec2D start, List<Rive.Color> colors, List<float> stops)
        {
            m_start = start;
            m_colors = colors;
            m_stops = stops;
        }
    }

    public class RadialGradient : Gradient
    {
        private float m_radius;
        public RadialGradient(Vec2D start, float radius, List<Rive.Color> colors, List<float> stops) : base(start, colors, stops)
        {
            m_radius = radius;
        }

        public float radius { get { return m_radius; } }
    }

    public class LinearGradient : Gradient
    {
        private Vec2D m_end;
        public LinearGradient(Vec2D start, Vec2D end, List<Rive.Color> colors, List<float> stops) : base(start, colors, stops)
        {
            m_end = end;
        }

        public Vec2D end { get { return m_end; } }
    }

    enum PathVerb : byte
    {
        move = 0,
        line = 1,
        quad = 2,
        cubic = 4,
        close = 5
    }


    public class Path
    {
        IntPtr m_nativePath = IntPtr.Zero;
        private List<byte> m_verbs = new List<byte>();
        private List<float> m_points = new List<float>();

        internal const int scratchSize = 1024;
        internal static byte[] scratchBuffer = new byte[scratchSize];

        ~Path()
        {
            unrefRenderPath(m_nativePath);
        }

        internal IntPtr nativePath
        {
            get { flush(); return m_nativePath; }
        }

        private int verbPointCount(PathVerb verb)
        {
            switch (verb)
            {
                case PathVerb.close:
                    return 0;
                case PathVerb.line:
                case PathVerb.move:
                    return 1;
                case PathVerb.quad:
                    return 2;
                case PathVerb.cubic:
                    return 3;
                default:
                    return 0;
            }
        }


        public void reset()
        {
            m_verbs.Clear();
            m_points.Clear();
        }

        public void close() { m_verbs.Add((byte)PathVerb.close); }

        public void cubicTo(float ox, float oy, float ix, float iy, float x, float y)
        {
            m_points.Add(ox);
            m_points.Add(oy);
            m_points.Add(ix);
            m_points.Add(iy);
            m_points.Add(x);
            m_points.Add(y);
            m_verbs.Add((byte)PathVerb.cubic);
        }

        public void circle(float centerX, float centerY, float radius)
        {
            const float circleConstant = 0.552284749831f;
            const float icircleConstant = 1.0f - circleConstant;

            float ox = centerX;
            float oy = centerY;
            float radiusY = radius;
            float radiusX = radius;
            moveTo(ox, oy - radiusY);

            cubicTo(ox + radiusX * circleConstant, oy - radiusY,
                    ox + radiusX, oy + circleConstant * -radiusY,
                    ox + radiusX, oy);

            cubicTo(ox + radiusX, oy + circleConstant * radiusY,
                    ox + radiusX * circleConstant, oy + radiusY,
                    ox, oy + radiusY);

            cubicTo(ox - radiusX * circleConstant, oy + radiusY,
                    ox - radiusX, oy + radiusY * circleConstant,
                    ox - radiusX, oy);

            cubicTo(ox - radiusX, oy - radiusY * circleConstant,
                    ox - radiusX * circleConstant, oy - radiusY,
                    ox, oy - radiusY);
        }

        public void quadTo(float cx, float cy, float x, float y)
        {
            m_points.Add(cx);
            m_points.Add(cy);
            m_points.Add(x);
            m_points.Add(y);
            m_verbs.Add((byte)PathVerb.quad);
        }


        public void lineTo(float x, float y)
        {
            m_points.Add(x);
            m_points.Add(y);
            m_verbs.Add((byte)PathVerb.line);
        }

        public void moveTo(float x, float y)
        {
            m_points.Add(x);
            m_points.Add(y);
            m_verbs.Add((byte)PathVerb.move);
        }

        void addPath(Path path, Mat2D transform)
        {
            if (m_nativePath == null)
            {
                m_nativePath = makeEmptyRenderPath();
            }
            addPathToPath(
              m_nativePath,
              path.m_nativePath,
              transform.xx,
              transform.xy,
              transform.yx,
              transform.yy,
              transform.tx,
              transform.ty
            );
        }

        public void flush()
        {
            if (m_verbs.Count == 0)
            {
                if (m_nativePath == IntPtr.Zero)
                {
                    m_nativePath = makeEmptyRenderPath();
                }
                return;
            }

            uint offset = 0;
            int pointIndex = 0;
            uint commandCount = 0;

            foreach (var verb in m_verbs)
            {
                int elementCount = verbPointCount((PathVerb)verb) * 2;
                int requiredSize = elementCount * 4 + 1;
                if (requiredSize + offset >= scratchSize)
                {
                    // Flush
                    appendCommands(scratchBuffer, commandCount);
                    offset = 0;
                    commandCount = 0;
                }
                scratchBuffer[offset++] = verb;
                for (int i = 0; i < elementCount; i++)
                {
                    var bytes = BitConverter.GetBytes(m_points[pointIndex++]);
                    for (int j = 0; j < 4; j++)
                    {
                        scratchBuffer[offset++] = bytes[j];
                    }
                }
                commandCount++;
            }
            if (commandCount > 0)
            {
                appendCommands(scratchBuffer, commandCount);
            }
            if (m_nativePath != IntPtr.Zero)
            {
                updateRenderPath(m_nativePath);
            }
            else
            {
                m_nativePath = makeRenderPath();
            }

            reset();
        }

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        private static extern IntPtr appendCommands(byte[] bytes, uint commandCount);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr makeRenderPath();

        [DllImport(NativeLibrary.name)]
        private static extern void updateRenderPath(IntPtr nativePath);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr makeEmptyRenderPath();

        [DllImport(NativeLibrary.name)]
        private static extern void unrefRenderPath(IntPtr nativePath);

        [DllImport(NativeLibrary.name)]
        private static extern void addPathToPath(IntPtr to, IntPtr from, float x1, float y1, float x2, float y2, float tx, float ty);
        #endregion
    }

    public enum BlendMode : byte
    {
        srcOver = 3,
        screen = 14,
        overlay = 15,
        darken = 16,
        lighten = 17,
        colorDodge = 18,
        colorBurn = 19,
        hardLight = 20,
        softLight = 21,
        difference = 22,
        exclusion = 23,
        multiply = 24,
        hue = 25,
        saturation = 26,
        color = 27,
        luminosity = 28
    }

    public enum PaintingStyle : byte
    {
        fill = 0,
        stroke = 1
    }

    public enum StrokeCap : byte
    {
        butt = 0,
        round = 1,
        square = 2
    }

    public enum StrokeJoin : byte
    {
        miter = 0,
        round = 1,
        bevel = 2
    }

    [Flags]
    internal enum PaintDirt : ushort
    {
        none = 0,
        style = 1 << 0,
        color = 1 << 1,
        thickness = 1 << 2,
        join = 1 << 3,
        cap = 1 << 4,
        blendMode = 1 << 5,
        radial = 1 << 6, // 0 == linear, 1 == radial only valid if stops != 0
        done = 1 << 7, // 1 when no more gradien stops will follow,

        // Anything higher than 8 bits will not be written to native, but can be used
        // as flags.
        gradient = 1 << 8,
    }


    public class Paint
    {
        IntPtr m_nativePaint;
        BlendMode m_blendMode;
        PaintDirt m_dirty = PaintDirt.none;
        Color m_color = new Color(0x000000FF);
        PaintingStyle m_style = PaintingStyle.fill;
        float m_thickness = 1.0f;
        StrokeCap m_cap = StrokeCap.butt;
        StrokeJoin m_join = StrokeJoin.bevel;
        Gradient m_gradient;

        public Paint()
        {
            m_nativePaint = makeRenderPaint();
        }

        ~Paint()
        {
            unrefRenderPaint(m_nativePaint);
        }

        internal IntPtr nativePaint
        {
            get { flush(); return m_nativePaint; }
        }

        public BlendMode blendMode
        {
            get { return m_blendMode; }
            set
            {
                if (m_blendMode == value)
                {
                    return;
                }
                m_blendMode = value;
                m_dirty |= PaintDirt.blendMode;
            }
        }

        public Color color
        {
            get { return m_color; }
            set
            {
                if (m_color == value)
                {
                    return;
                }
                m_color = value;
                m_dirty |= PaintDirt.color;
            }
        }

        public PaintingStyle style
        {
            get { return m_style; }
            set
            {
                if (m_style == value)
                {
                    return;
                }
                m_style = value;
                m_dirty |= PaintDirt.style;
            }
        }

        public float thickness
        {
            get { return m_thickness; }
            set
            {
                if (m_thickness == value)
                {
                    return;
                }
                m_thickness = value;
                m_dirty |= PaintDirt.thickness;
            }
        }

        public StrokeJoin join
        {
            get { return m_join; }
            set
            {
                if (m_join == value)
                {
                    return;
                }
                m_join = value;
                m_dirty |= PaintDirt.join;
            }
        }

        public StrokeCap cap
        {
            get { return m_cap; }
            set
            {
                if (m_cap == value)
                {
                    return;
                }
                m_cap = value;
                m_dirty |= PaintDirt.cap;
            }
        }

        public Gradient gradient
        {
            get { return m_gradient; }
            set
            {
                if (m_gradient == value)
                {
                    return;
                }
                m_gradient = value;
                m_dirty |= PaintDirt.gradient;
            }
        }

        public void flush()
        {
            if (m_dirty == PaintDirt.none)
            {
                return;
            }

            int offset = 0;
            var buffer = Path.scratchBuffer;

            if ((m_dirty & PaintDirt.style) != 0)
            {
                buffer[offset++] = (byte)(m_style == PaintingStyle.stroke ? 0 : 1);
            }
            if ((m_dirty & PaintDirt.color) != 0)
            {
                var bytes = BitConverter.GetBytes(m_color.value);
                for (int j = 0; j < 4; j++)
                {
                    buffer[offset++] = bytes[j];
                }
            }
            if ((m_dirty & PaintDirt.thickness) != 0)
            {
                var bytes = BitConverter.GetBytes(m_thickness);
                for (int j = 0; j < 4; j++)
                {
                    buffer[offset++] = bytes[j];
                }
            }
            if ((m_dirty & PaintDirt.join) != 0)
            {
                buffer[offset++] = (byte)m_join;
            }
            if ((m_dirty & PaintDirt.cap) != 0)
            {
                buffer[offset++] = (byte)m_cap;
            }
            if ((m_dirty & PaintDirt.blendMode) != 0)
            {
                buffer[offset++] = (byte)m_blendMode;
            }

            uint wroteStops = 0;
            if (m_gradient != null)
            {
                var isRadial = m_gradient is RadialGradient;
                int writeStopIndex = 0;
                while (true)
                {
                    if (isRadial)
                    {
                        m_dirty |= PaintDirt.radial;
                    }
                    var remaining = Path.scratchSize - offset - 16;
                    int stopsAvailable = remaining / 8;
                    var stopsToWrite =
                        Math.Min(stopsAvailable, (gradient.stops.Count - writeStopIndex));
                    for (int i = 0; i < stopsToWrite; i++)
                    {
                        wroteStops++;

                        var bytes = BitConverter.GetBytes(gradient.stops[writeStopIndex]);
                        for (int j = 0; j < 4; j++)
                        {
                            buffer[offset++] = bytes[j];
                        }

                        bytes = BitConverter.GetBytes(gradient.colors[writeStopIndex].value);
                        for (int j = 0; j < 4; j++)
                        {
                            buffer[offset++] = bytes[j];
                        }

                        writeStopIndex++;
                    }

                    if (gradient.stops.Count - writeStopIndex == 0)
                    {
                        var bytes = BitConverter.GetBytes(gradient.start.x);
                        for (int j = 0; j < 4; j++)
                        {
                            buffer[offset++] = bytes[j];
                        }

                        bytes = BitConverter.GetBytes(gradient.start.y);
                        for (int j = 0; j < 4; j++)
                        {
                            buffer[offset++] = bytes[j];
                        }
                        if (isRadial)
                        {
                            var radial = gradient as RadialGradient;
                            bytes = BitConverter.GetBytes(radial.radius);
                            for (int j = 0; j < 4; j++)
                            {
                                buffer[offset++] = bytes[j];
                            }
                        }
                        else
                        {
                            var linear = gradient as LinearGradient;
                            bytes = BitConverter.GetBytes(linear.end.x);
                            for (int j = 0; j < 4; j++)
                            {
                                buffer[offset++] = bytes[j];
                            }
                            bytes = BitConverter.GetBytes(linear.end.y);
                            for (int j = 0; j < 4; j++)
                            {
                                buffer[offset++] = bytes[j];
                            }
                        }

                        // Stop looping, we've built the gradient.
                        m_dirty |= PaintDirt.done;
                        break;
                    }
                    else
                    {
                        // Gotta flush, we're out of space.
                        updatePaint(m_nativePaint, (ushort)m_dirty, buffer, wroteStops);
                        wroteStops = 0;
                        m_dirty = PaintDirt.none;
                        offset = 0;
                    }
                }
            }
            updatePaint(m_nativePaint, (ushort)m_dirty, buffer, wroteStops);
            m_dirty = PaintDirt.none;
        }

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        private static extern IntPtr makeRenderPaint();

        [DllImport(NativeLibrary.name)]
        private static extern void unrefRenderPaint(IntPtr nativePaint);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr updatePaint(IntPtr nativePaint, ushort dirt, byte[] bytes, uint stops);
        #endregion
    }
}