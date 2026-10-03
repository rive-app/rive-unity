using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    /// <summary>
    /// For tests that start C# threads or tasks. WebGL has none, even with
    /// Native C/C++ Multithreading, which only gives native code threads.
    /// </summary>
    public class NeedsManagedThreadsAttribute : UnityPlatformAttribute
    {
        public NeedsManagedThreadsAttribute()
        {
            exclude = new[] { RuntimePlatform.WebGLPlayer };
        }
    }
}
