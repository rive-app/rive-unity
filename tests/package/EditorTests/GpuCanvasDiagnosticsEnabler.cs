using Rive;
using UnityEditor;

namespace Rive.Tests
{
    /// <summary>
    /// Residency tracking is off in the package, it costs the render
    /// thread a lock per replay. The tests need it, and this assembly only
    /// exists when they do.
    ///
    /// On editor domain load, the earliest hook there is. A replay before
    /// this latches it off for the session.
    /// </summary>
    internal static class GpuCanvasDiagnosticsEnabler
    {
        [InitializeOnLoadMethod]
        private static void Enable()
        {
            GpuCanvasDiagnostics.Enable();
        }
    }
}
