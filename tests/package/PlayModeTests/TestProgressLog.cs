using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(Rive.Tests.TestProgressLog))]

namespace Rive.Tests
{
    /// <summary>
    /// Logs each test as it starts and ends, so when a player crashes, the
    /// last line in its log names the test that was running.
    /// </summary>
    public class TestProgressLog : ITestRunCallback
    {
        public void RunStarted(ITest testsToRun)
        {
        }

        public void RunFinished(ITestResult testResults)
        {
        }

        public void TestStarted(ITest test)
        {
            if (!test.IsSuite)
            {
                Log($"[Rive Tests] Start {test.FullName}");
            }
        }

        public void TestFinished(ITestResult result)
        {
            if (!result.Test.IsSuite)
            {
                Log($"[Rive Tests] End {result.Test.FullName}: {result.ResultState} in {result.Duration:0.00}s");
                // A crash later on loses the results file, so keep why here.
                if (result.ResultState.Status == TestStatus.Failed && !string.IsNullOrEmpty(result.Message))
                {
                    Log($"[Rive Tests] Failure {result.Test.FullName}: {result.Message.Trim().Replace('\n', ' ')}");
                }
            }
        }

        private static void Log(string message)
        {
            // One line each, without a stack trace.
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", message);
        }
    }
}
