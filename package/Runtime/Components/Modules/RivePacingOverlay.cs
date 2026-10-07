using System.Collections.Generic;
using System.Text;
using Rive.Host;
using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// Temporary. Shows how panels are pacing on screen, for panels with the
    /// pacing overlay turned on. Made when the first one turns it on and
    /// removed when the last one turns it off, so nothing runs otherwise.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class RivePacingOverlay : MonoBehaviour
    {
        // How often the numbers refresh.
        private const float Window = 0.5f;

        private static RivePacingOverlay s_instance;

        private readonly List<RivePanel> m_panels = new List<RivePanel>();
        private readonly StringBuilder m_text = new StringBuilder();
        private readonly FrameTiming[] m_timing = new FrameTiming[1];
        private string m_shown = "Rive pacing: measuring...";
        private float m_windowStart;
        private int m_frames;
        private double m_frameSum;
        private double m_frameMax;
        private double m_renderSum;
        private double m_renderMax;
        private int m_renderFrames;
        private uint m_pendingMax;
        // Indexed by RoutineTag. Pointer is the last one.
        private const int TagCount = (int)RoutineTag.Pointer + 1;
        private readonly ulong[] m_routineNanoseconds = new ulong[TagCount];
        private readonly uint[] m_routineCounts = new uint[TagCount];
        private readonly List<int> m_routineOrder = new List<int>(TagCount);
        private GUIStyle m_style;
        private Texture2D m_background;
        private readonly GUIContent m_content = new GUIContent();

        /// Tests only.
        internal static RivePacingOverlay InstanceForTests => s_instance;
        internal string TextForTests => m_shown;

        internal static void Show(RivePanel panel)
        {
            if (s_instance == null)
            {
                var go = new GameObject("Rive Pacing Overlay") { hideFlags = HideFlags.DontSave };
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<RivePacingOverlay>();
                SetServerTiming(true);
            }
            if (!s_instance.m_panels.Contains(panel))
            {
                s_instance.m_panels.Add(panel);
                panel.AdvanceForPacing.Pacing ??= new PanelAdvance.PacingStats();
            }
            PacingCounters.Enabled = true;
        }

        internal static void Hide(RivePanel panel)
        {
            if (s_instance == null)
            {
                return;
            }
            if (s_instance.m_panels.Remove(panel))
            {
                panel.AdvanceForPacing.Pacing = null;
            }
            if (s_instance.m_panels.Count == 0)
            {
                PacingCounters.Enabled = false;
                SetServerTiming(false);
                Destroy(s_instance.gameObject);
                s_instance = null;
            }
        }

        private static void SetServerTiming(bool on)
        {
            if (!NativeUsageGuard.IsNativeAvailable)
            {
                return;
            }
            try
            {
                HostNative.riveServerTimingEnable(on);
            }
            catch (System.EntryPointNotFoundException)
            {
                // An older plugin. The server lines say n/a.
            }
        }

        private void OnEnable()
        {
            m_windowStart = Time.realtimeSinceStartup;
            PacingCounters.ImageBuilds = 0;
        }

        private void Update()
        {
            double frameMs = Time.unscaledDeltaTime * 1000.0;
            m_frames++;
            m_frameSum += frameMs;
            m_frameMax = System.Math.Max(m_frameMax, frameMs);

            // Only filled when the project turns on Frame Timing Stats.
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, m_timing) > 0 && m_timing[0].cpuRenderThreadFrameTime > 0)
            {
                m_renderFrames++;
                m_renderSum += m_timing[0].cpuRenderThreadFrameTime;
                m_renderMax = System.Math.Max(m_renderMax, m_timing[0].cpuRenderThreadFrameTime);
            }

            float elapsed = Time.realtimeSinceStartup - m_windowStart;
            if (elapsed < Window)
            {
                return;
            }

            // Walks native tables, so only once a window.
            bool hasPending = GpuCanvasDiagnostics.TryRead(out GpuCanvasResidency residency);
            m_pendingMax = System.Math.Max(m_pendingMax, residency.PendingFrames);

            m_text.Clear();
            m_text.AppendLine("Rive pacing (temporary)");
            m_text.AppendLine($"frame ms: avg {m_frameSum / System.Math.Max(1, m_frames):F1}, worst {m_frameMax:F1}");
            m_text.AppendLine(m_renderFrames > 0
                ? $"render thread ms: avg {m_renderSum / m_renderFrames:F1}, worst {m_renderMax:F1}"
                : "render thread ms: n/a (turn on Frame Timing Stats in Player Settings)");
            m_text.AppendLine(hasPending
                ? $"canvas frames waiting: now {residency.PendingFrames}, worst {m_pendingMax}"
                : "canvas frames waiting: n/a in this build");
            m_text.AppendLine($"image builds/s: {PacingCounters.ImageBuilds / elapsed:F0}");
            AppendServer(System.Math.Max(1, m_frames));
            foreach (RivePanel panel in m_panels)
            {
                PanelAdvance.PacingStats stats = panel != null ? panel.AdvanceForPacing.Pacing : null;
                if (stats == null)
                {
                    continue;
                }
                string average = stats.Landed > 0 ? $"{stats.RoundTripSum / stats.Landed:F1}" : "-";
                m_text.AppendLine($"{panel.name} ({panel.ThreadingMode}): advance ms avg {average}, worst {stats.RoundTripMax:F1}; " +
                    $"sent {stats.Sent / elapsed:F0}/s, skipped {stats.Skipped / elapsed:F0}/s");
                stats.Clear();
            }
            m_shown = m_text.ToString();

            m_windowStart = Time.realtimeSinceStartup;
            m_frames = 0;
            m_frameSum = 0;
            m_frameMax = 0;
            m_renderFrames = 0;
            m_renderSum = 0;
            m_renderMax = 0;
            PacingCounters.ImageBuilds = 0;
        }

        // Server time per frame, then the routines taking most of it. What's
        // left over is core's own commands, like loads and binds.
        private void AppendServer(int frames)
        {
            ulong busy;
            ulong longest;
            uint batches;
            try
            {
                HostNative.riveServerTimingTake(out busy, out longest, out batches,
                    m_routineNanoseconds, m_routineCounts, (uint)TagCount);
            }
            catch (System.EntryPointNotFoundException)
            {
                m_text.AppendLine("server: n/a with this plugin");
                return;
            }
            m_text.AppendLine($"server busy ms/frame: {busy * 1e-6 / frames:F2}, longest batch {longest * 1e-6:F1} ms, batches/frame {(double)batches / frames:F1}");

            m_routineOrder.Clear();
            ulong routines = 0;
            for (int i = 0; i < TagCount; i++)
            {
                routines += m_routineNanoseconds[i];
                if (m_routineNanoseconds[i] > 0)
                {
                    m_routineOrder.Add(i);
                }
            }
            m_routineOrder.Sort((a, b) => m_routineNanoseconds[b].CompareTo(m_routineNanoseconds[a]));
            m_text.Append("  ms/frame:");
            for (int i = 0; i < m_routineOrder.Count && i < 5; i++)
            {
                int tag = m_routineOrder[i];
                m_text.Append($" {(RoutineTag)tag} {m_routineNanoseconds[tag] * 1e-6 / frames:F2} (x{(double)m_routineCounts[tag] / frames:F1})");
            }
            ulong other = busy > routines ? busy - routines : 0;
            m_text.AppendLine($" other {other * 1e-6 / frames:F2}");
        }

        private void OnGUI()
        {
            if (m_style == null)
            {
                // Near-black and mostly opaque, so it reads over bright scenes.
                m_background = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
                m_background.SetPixel(0, 0, new UnityEngine.Color(0f, 0f, 0f, 0.85f));
                m_background.Apply();
                m_style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 18,
                    richText = false,
                    wordWrap = false,
                    padding = new RectOffset(12, 12, 10, 10),
                };
                m_style.normal.background = m_background;
                m_style.normal.textColor = UnityEngine.Color.white;
            }
            m_content.text = m_shown;
            Vector2 size = m_style.CalcSize(m_content);
            GUI.Label(new Rect(10, 10, size.x, size.y), m_content, m_style);
        }

        private void OnDestroy()
        {
            if (m_background != null)
            {
                Destroy(m_background);
            }
        }
    }
}
