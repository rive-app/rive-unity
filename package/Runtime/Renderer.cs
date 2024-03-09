using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Numerics;
using System.Diagnostics;

namespace Rive
{
    public abstract class Gradient
    {
        private Vector2 m_start;
        private readonly List<Color> m_colors;
        private readonly List<float> m_stops;

        /// <summary>
        ///  The start point of the gradient.
        /// </summary>
        public Vector2 Start => m_start;

        /// <summary>
        /// The list of colors in the gradient.
        /// </summary>
        public List<Color> Colors => m_colors;

        /// <summary>
        /// The list of stops in the gradient.
        /// </summary>
        public List<float> Stops => m_stops;

        public Gradient(Vector2 start, List<Color> colors, List<float> stops)
        {
            m_start = start;
            m_colors = colors;
            m_stops = stops;
        }
    }

    /// <summary>
    /// A radial gradient. The gradient will be a circle with the given radius
    /// centered at the start point.
    ///
    /// Provide a list of colors and stops to define the gradient.
    /// </summary>
    public class RadialGradient : Gradient
    {
        private readonly float m_radius;

        public RadialGradient(Vector2 start, float radius, List<Color> colors, List<float> stops)
            : base(start, colors, stops)
        {
            m_radius = radius;
        }

        public float Radius
        {
            get { return m_radius; }
        }
    }

    /// <summary>
    /// A linear gradient. The gradient will be a line from the start point to the
    /// end point.
    ///
    /// Provide a list of colors and stops to define the gradient.
    /// </summary>
    public class LinearGradient : Gradient
    {
        private Vector2 m_end;

        public LinearGradient(Vector2 start, Vector2 end, List<Color> colors, List<float> stops)
            : base(start, colors, stops)
        {
            m_end = end;
        }

        public Vector2 End => m_end;
    }

    enum PathVerb : byte
    {
        move = 0,
        line = 1,
        quad = 2,
        cubic = 4,
        close = 5
    }

    /// <summary>
    /// A path is a series of drawing commands. The path is used to define the
    /// outline of a shape or to define a clipping mask.
    ///
    /// Paths are built by calling moveTo, lineTo, quadTo, cubicTo, close, etc.
    /// </summary>
    public class Path
    {
        IntPtr m_nativePath = IntPtr.Zero;
        private readonly List<byte> m_verbs = new();
        private readonly List<float> m_points = new();

        internal const int scratchSize = 1024;
        internal static byte[] scratchBuffer = new byte[scratchSize];

        ~Path()
        {
            unrefRenderPath(m_nativePath);
        }

        internal IntPtr NativePath
        {
            get
            {
                Flush();
                return m_nativePath;
            }
        }

        private int VerbPointCount(PathVerb verb)
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

        /// <summary>
        /// Resets the path to an empty state.
        ///
        /// This is called automatically when the path is flushed (see flush).
        /// </summary>
        public void Reset()
        {
            m_verbs.Clear();
            m_points.Clear();
        }

        /// <summary>
        /// Closes the path. This will draw a line from the current point to the
        /// first point in the path.
        /// </summary>
        public void Close()
        {
            m_verbs.Add((byte)PathVerb.close);
        }

        /// <summary>
        /// Adds a cubic bezier curve to the path.
        ///
        /// The curve starts at the current point and ends at the given point (x, y).
        /// The control points (ox, oy) and (ix, iy) are used to define the curve.
        /// </summary>
        public void CubicTo(float ox, float oy, float ix, float iy, float x, float y)
        {
            m_points.Add(ox);
            m_points.Add(oy);
            m_points.Add(ix);
            m_points.Add(iy);
            m_points.Add(x);
            m_points.Add(y);
            m_verbs.Add((byte)PathVerb.cubic);
        }

        /// <summary>
        /// Adds a circle to the path.
        ///
        /// The circle is centered at (centerX, centerY) and has the given radius.
        /// </summary>
        public void Circle(float centerX, float centerY, float radius)
        {
            const float circleConstant = 0.552284749831f;

            float ox = centerX;
            float oy = centerY;
            float radiusY = radius;
            float radiusX = radius;
            MoveTo(ox, oy - radiusY);

            CubicTo(
                ox + radiusX * circleConstant,
                oy - radiusY,
                ox + radiusX,
                oy + circleConstant * -radiusY,
                ox + radiusX,
                oy
            );

            CubicTo(
                ox + radiusX,
                oy + circleConstant * radiusY,
                ox + radiusX * circleConstant,
                oy + radiusY,
                ox,
                oy + radiusY
            );

            CubicTo(
                ox - radiusX * circleConstant,
                oy + radiusY,
                ox - radiusX,
                oy + radiusY * circleConstant,
                ox - radiusX,
                oy
            );

            CubicTo(
                ox - radiusX,
                oy - radiusY * circleConstant,
                ox - radiusX * circleConstant,
                oy - radiusY,
                ox,
                oy - radiusY
            );
        }

        /// <summary>
        /// Adds a quadratic bezier segment that curves from the current point
        /// to the given point (x,y), using the control point (cx,cy).
        /// </summary>
        public void QuadTo(float cx, float cy, float x, float y)
        {
            m_points.Add(cx);
            m_points.Add(cy);
            m_points.Add(x);
            m_points.Add(y);
            m_verbs.Add((byte)PathVerb.quad);
        }

        /// <summary>
        /// Adds a straight line from the current point to the given point (x,y).
        /// </summary>
        public void LineTo(float x, float y)
        {
            m_points.Add(x);
            m_points.Add(y);
            m_verbs.Add((byte)PathVerb.line);
        }

        /// <summary>
        /// Moves the current point to the given point (x,y).
        /// </summary>
        public void MoveTo(float x, float y)
        {
            m_points.Add(x);
            m_points.Add(y);
            m_verbs.Add((byte)PathVerb.move);
        }

        /// <summary>
        /// Adds the sub-paths of path to this path, transformed by the provided matrix (Mat2D).
        /// </summary>
        public void AddPath(Path path, Matrix3x2 transform)
        {
            if (m_nativePath == null)
            {
                m_nativePath = makeEmptyRenderPath();
            }
            addPathToPath(
                m_nativePath,
                path.m_nativePath,
                transform.M11,
                transform.M12,
                transform.M21,
                transform.M22,
                transform.M31,
                transform.M32
            );
        }

        /// <summary>
        /// Flushes the path to native memory.
        /// </summary>
        public void Flush()
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
                int elementCount = VerbPointCount((PathVerb)verb) * 2;
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
                unrefRenderPath(m_nativePath);
            }
            m_nativePath = makeRenderPath();

            Reset();
        }

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        private static extern void appendCommands(byte[] bytes, uint commandCount);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr makeRenderPath();

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr makeEmptyRenderPath();

        [DllImport(NativeLibrary.name)]
        private static extern void unrefRenderPath(IntPtr nativePath);

        [DllImport(NativeLibrary.name)]
        private static extern void addPathToPath(
            IntPtr to,
            IntPtr from,
            float x1,
            float y1,
            float x2,
            float y2,
            float tx,
            float ty
        );
        #endregion
    }

    /// <summary>
    /// Algorithms to use when painting on the canvas.
    ///
    /// When painting the algorithm is used to blend the source
    /// pixels with the destination pixels.
    /// </summary>
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

    /// <summary>
    /// The style to use when painting on the canvas.
    ///
    /// When painting the style is used to determine if the
    /// shape is filled or stroked.
    /// </summary>
    public enum PaintingStyle : byte
    {
        /// <summary>
        /// Fill the shape.
        /// </summary>
        fill = 0,

        /// <summary>
        /// Stroke the shape.
        /// </summary>
        stroke = 1
    }

    /// <summary>
    /// The cap to use when stroking a path.
    ///
    /// When stroking a path the cap is used to determine how the
    /// end points of the path are drawn.
    /// </summary>
    public enum StrokeCap : byte
    {
        /// <summary>
        /// The end of the path is squared off.
        /// </summary>
        butt = 0,

        /// <summary>
        /// The end of the path is rounded.
        /// </summary>
        round = 1,

        /// <summary>
        /// The end of the path is squared off and extends past the end of the path.
        /// </summary>
        square = 2
    }

    /// <summary>
    /// The join to use when stroking a path.
    ///
    /// The kind of finish to place on the joins between segments.
    /// </summary>
    public enum StrokeJoin : byte
    {
        /// <summary>
        /// Joins between segments are sharp.
        /// </summary>
        miter = 0,

        /// <summary>
        /// Joins between segments are rounded.
        /// </summary>
        round = 1,

        /// <summary>
        /// Joins between segments are beveled.
        /// </summary>
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

    /// <summary>
    /// A paint is used to describe how to draw a shape.
    ///
    /// The paint describes the color, gradient, style, thickness, etc.
    /// </summary>
    public class Paint
    {
        readonly IntPtr m_nativePaint;
        BlendMode m_blendMode;
        PaintDirt m_dirty = PaintDirt.none;
        Color m_color = new(0x000000FF);
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

        internal IntPtr NativePaint
        {
            get
            {
                Flush();
                return m_nativePaint;
            }
        }

        /// <summary>
        /// The blend mode to use when painting.
        ///
        /// When painting the blend mode is used to determine how the
        /// source pixels are blended with the destination pixels.
        /// </summary>
        public BlendMode BlendMode
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

        /// <summary>
        /// The color to use when painting.
        ///
        /// When painting the color is used to determine the color of the
        /// shape.
        /// </summary>
        public Color Color
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

        /// <summary>
        /// The style to use when painting.
        ///
        /// When painting the style is used to determine if the
        /// shape is filled or stroked.
        /// </summary>
        public PaintingStyle Style
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

        /// <summary>
        /// The thickness to use when stroking.
        /// </summary>
        public float Thickness
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

        /// <summary>
        /// The join to use when stroking.
        ///
        /// The kind of finish to place on the joins between segments.
        /// </summary>
        public StrokeJoin Join
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

        /// <summary>
        /// The cap to use when stroking.
        ///
        /// When stroking a path the cap is used to determine how the
        /// end points of the path are drawn.
        /// </summary>
        public StrokeCap Cap
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

        /// <summary>
        /// The gradient to use when painting.
        ///
        /// When painting the gradient is used to determine the color of the
        /// shape.
        /// </summary>
        public Gradient Gradient
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

        /// <summary>
        /// Flushes the paint to native memory.
        /// </summary>
        public void Flush()
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
                    var stopsToWrite = Math.Min(
                        stopsAvailable,
                        (Gradient.Stops.Count - writeStopIndex)
                    );
                    for (int i = 0; i < stopsToWrite; i++)
                    {
                        wroteStops++;

                        var bytes = BitConverter.GetBytes(Gradient.Stops[writeStopIndex]);
                        for (int j = 0; j < 4; j++)
                        {
                            buffer[offset++] = bytes[j];
                        }

                        bytes = BitConverter.GetBytes(Gradient.Colors[writeStopIndex].value);
                        for (int j = 0; j < 4; j++)
                        {
                            buffer[offset++] = bytes[j];
                        }

                        writeStopIndex++;
                    }

                    if (Gradient.Stops.Count - writeStopIndex == 0)
                    {
                        var bytes = BitConverter.GetBytes(Gradient.Start.X);
                        for (int j = 0; j < 4; j++)
                        {
                            buffer[offset++] = bytes[j];
                        }

                        bytes = BitConverter.GetBytes(Gradient.Start.Y);
                        for (int j = 0; j < 4; j++)
                        {
                            buffer[offset++] = bytes[j];
                        }
                        if (isRadial)
                        {
                            var radial = Gradient as RadialGradient;
                            bytes = BitConverter.GetBytes(radial.Radius);
                            for (int j = 0; j < 4; j++)
                            {
                                buffer[offset++] = bytes[j];
                            }
                        }
                        else
                        {
                            var linear = Gradient as LinearGradient;
                            bytes = BitConverter.GetBytes(linear.End.Y);
                            for (int j = 0; j < 4; j++)
                            {
                                buffer[offset++] = bytes[j];
                            }
                            bytes = BitConverter.GetBytes(linear.End.Y);
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
        private static extern void updatePaint(
            IntPtr nativePaint,
            ushort dirt,
            byte[] bytes,
            uint stops
        );
        #endregion
    }
}
