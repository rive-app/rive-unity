using UnityEngine;

namespace Rive.Tests
{
    /// <summary>
    /// The editor assembly turns residency tracking on in the editor, but it
    /// isn't in test players, so this does it there. Before any scene loads,
    /// so it lands before the first replay fixes the setting.
    /// </summary>
    internal static class GpuCanvasPlayerDiagnosticsEnabler
    {
#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Enable()
        {
            GpuCanvasDiagnostics.Enable();
        }
#endif
    }
}
