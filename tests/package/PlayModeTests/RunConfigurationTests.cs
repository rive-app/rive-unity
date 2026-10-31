using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rive.Tests
{
    /// <summary>
    /// CI passes what each row should run on as player arguments. A player
    /// quietly falls back when an API isn't in the build, so without this a
    /// row could pass on a different API than its name says. Skipped when
    /// the arguments aren't there, like in a local run.
    /// </summary>
    public class RunConfigurationTests
    {
        private const string ApiArgument = "-rive-expect-api";
        private const string GraphicsJobsArgument = "-rive-expect-graphics-jobs";

        [Test]
        public void Player_RunsOnTheExpectedGraphicsApi()
        {
            string expected = ArgumentValue(ApiArgument);
            if (expected == null)
            {
                Assert.Ignore($"No {ApiArgument} argument.");
            }
            Assert.AreEqual(ExpectedDeviceType(expected), SystemInfo.graphicsDeviceType,
                $"The row asked for {expected}.");
        }

        [Test]
        public void Player_RunsWithTheExpectedGraphicsJobsMode()
        {
            string expected = ArgumentValue(GraphicsJobsArgument);
            if (expected == null)
            {
                Assert.Ignore($"No {GraphicsJobsArgument} argument.");
            }
            // Every jobs mode has Jobs in its name, on 2022 and Unity 6 alike.
            RenderingThreadingMode mode = SystemInfo.renderingThreadingMode;
            bool jobs = mode.ToString().Contains("Jobs");
            Assert.AreEqual(expected == "1", jobs, $"Threading mode is {mode}.");
        }

        private static string ArgumentValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        private static GraphicsDeviceType ExpectedDeviceType(string api)
        {
            switch (api)
            {
                case "d3d11": return GraphicsDeviceType.Direct3D11;
                case "d3d12": return GraphicsDeviceType.Direct3D12;
                case "vulkan": return GraphicsDeviceType.Vulkan;
                case "metal": return GraphicsDeviceType.Metal;
                case "glcore": return GraphicsDeviceType.OpenGLCore;
                default:
                    Assert.Fail($"Unknown API {api}.");
                    return GraphicsDeviceType.Null;
            }
        }
    }
}
