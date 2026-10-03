using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using Rive.Host;

namespace Rive.Tests
{
    /// <summary>
    /// For tests that need Rive's own thread: they hold it, race it, or check
    /// that work is still pending. WebGL without Native C/C++ Multithreading
    /// runs Rive's work inline, so these are skipped there.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class NeedsRiveThreadAttribute : NUnitAttribute, IApplyToTest
    {
        public void ApplyToTest(Test test)
        {
            if (test.RunState == RunState.NotRunnable || CommandHost.IsThreaded)
            {
                return;
            }
            test.RunState = RunState.Skipped;
            test.Properties.Add(PropertyNames.SkipReason, "Rive's work runs inline here, with no thread of its own.");
        }
    }
}
