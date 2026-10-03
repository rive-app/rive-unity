using Unity.Profiling;
using Unity.Profiling.Editor;

namespace Rive.EditorTools
{
    /// <summary>
    /// What core holds for Rive, in one Profiler module. The counters are
    /// filled in the editor and development players, and the names match the
    /// ones the native plugin makes.
    /// </summary>
    [System.Serializable]
    [ProfilerModuleMetadata("Rive Objects")]
    internal sealed class RiveObjectsProfilerModule : ProfilerModule
    {
        private static readonly ProfilerCounterDescriptor[] s_counters =
        {
            new ProfilerCounterDescriptor("Rive Files", ProfilerCategory.Scripts),
            new ProfilerCounterDescriptor("Rive Artboards", ProfilerCategory.Scripts),
            new ProfilerCounterDescriptor("Rive State Machines", ProfilerCategory.Scripts),
            new ProfilerCounterDescriptor("Rive View Model Instances", ProfilerCategory.Scripts),
            new ProfilerCounterDescriptor("Rive Images", ProfilerCategory.Scripts),
            new ProfilerCounterDescriptor("Rive Fonts", ProfilerCategory.Scripts),
            new ProfilerCounterDescriptor("Rive Audio Sources", ProfilerCategory.Scripts),
        };

        public RiveObjectsProfilerModule() : base(s_counters)
        {
        }
    }

    /// <summary>
    /// How busy Rive's command server and rendering are, in one Profiler
    /// module. Unity allows ten counters a module, so these sit apart from
    /// <see cref="RiveObjectsProfilerModule"/>.
    /// </summary>
    [System.Serializable]
    [ProfilerModuleMetadata("Rive Server")]
    internal sealed class RiveServerProfilerModule : ProfilerModule
    {
        private static readonly ProfilerCounterDescriptor[] s_counters =
        {
            new ProfilerCounterDescriptor("Rive Render Queues", ProfilerCategory.Scripts),
            new ProfilerCounterDescriptor("Rive Pending Canvas Frames", ProfilerCategory.Scripts),
            new ProfilerCounterDescriptor("Rive Largest Command Batch", ProfilerCategory.Scripts),
            new ProfilerCounterDescriptor("Rive Messages Drained", ProfilerCategory.Scripts),
        };

        public RiveServerProfilerModule() : base(s_counters)
        {
        }
    }
}
