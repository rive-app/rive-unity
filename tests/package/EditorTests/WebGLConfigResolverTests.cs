#if UNITY_EDITOR

using NUnit.Framework;
using Rive.EditorTools;

namespace Rive.Tests.EditorTests
{
    internal class TestWebGLEnvironment : IWebGLEnvironment
    {
        public string UnityVersion { get; set; } = "6000.0.26f1";
        public bool DisableWasmSimd { get; set; } = false;
        public string PackageName { get; set; } = "app.rive.rive-unity";
        public bool DirectoryExists(string path) => DirectoryExistsOverride;
        public bool DirectoryExistsOverride { get; set; } = true;
    }

    [TestFixture]
    public class WebGLConfigResolverTests
    {
        private TestWebGLEnvironment env;

        [SetUp]
        public void SetUp()
        {
            env = new TestWebGLEnvironment();
        }

        [Test]
        public void Resolve_Unity6_WithSimdEnabled_ResolvesEmscripten3138()
        {
            env.UnityVersion = "6000.0.26f1";
            env.DisableWasmSimd = false;

            var config = WebGLConfigResolver.Resolve(env);

            Assert.AreEqual("3.1.38", config.EmscriptenVersion);
            Assert.IsFalse(config.UseNoSimd);
            Assert.That(config.SourcePath, Does.Contain("emscripten_3.1.38"));
            Assert.That(config.SourcePath, Does.Not.Contain("_nosimd"));
        }

        [Test]
        public void Resolve_Unity6_WithSimdDisabled_ResolvesNoSimdVariant()
        {
            env.UnityVersion = "6000.0.26f1";
            env.DisableWasmSimd = true;

            var config = WebGLConfigResolver.Resolve(env);

            Assert.AreEqual("3.1.38", config.EmscriptenVersion);
            Assert.IsTrue(config.UseNoSimd);
            Assert.That(config.SourcePath, Does.Contain("emscripten_3.1.38_nosimd"));
        }

        [Test]
        public void Resolve_Unity2022_IgnoresSimdSetting_ResolvesEmscripten318()
        {
            env.UnityVersion = "2022.3.10f1";
            env.DisableWasmSimd = true;

            var config = WebGLConfigResolver.Resolve(env);

            Assert.AreEqual("3.1.8", config.EmscriptenVersion);
            Assert.IsFalse(config.UseNoSimd);
            Assert.That(config.SourcePath, Does.Contain("emscripten_3.1.8"));
            Assert.That(config.SourcePath, Does.Not.Contain("_nosimd"));
        }

        [Test]
        public void Resolve_Unity2023_TreatedAsUnity6()
        {
            env.UnityVersion = "2023.2.0f1";
            env.DisableWasmSimd = false;

            var config = WebGLConfigResolver.Resolve(env);

            Assert.AreEqual("3.1.38", config.EmscriptenVersion);
            Assert.IsFalse(config.UseNoSimd);
        }

        [Test]
        public void Resolve_Unity2023_WithSimdDisabled_ResolvesNoSimdVariant()
        {
            env.UnityVersion = "2023.2.0f1";
            env.DisableWasmSimd = true;

            var config = WebGLConfigResolver.Resolve(env);

            Assert.IsTrue(config.UseNoSimd);
            Assert.That(config.SourcePath, Does.Contain("emscripten_3.1.38_nosimd"));
        }

        [Test]
        public void Resolve_Unity2021_ResolvesEmscripten318()
        {
            env.UnityVersion = "2021.3.25f1";
            env.DisableWasmSimd = false;

            var config = WebGLConfigResolver.Resolve(env);

            Assert.AreEqual("3.1.8", config.EmscriptenVersion);
            Assert.IsFalse(config.UseNoSimd);
        }

        [Test]
        public void Resolve_SourcePathIncludesPackageName()
        {
            env.PackageName = "com.example.test-package";

            var config = WebGLConfigResolver.Resolve(env);

            Assert.That(config.SourcePath, Does.Contain("com.example.test-package"));
        }

        [Test]
        public void Validate_DirectoryExists_DoesNotThrow()
        {
            env.DirectoryExistsOverride = true;
            var config = WebGLConfigResolver.Resolve(env);

            Assert.DoesNotThrow(() => WebGLConfigResolver.Validate(config, env));
        }

        [Test]
        public void Validate_DirectoryMissing_ThrowsBuildFailedException()
        {
            env.DirectoryExistsOverride = false;
            var config = WebGLConfigResolver.Resolve(env);

            var ex = Assert.Throws<UnityEditor.Build.BuildFailedException>(
                () => WebGLConfigResolver.Validate(config, env));
            Assert.That(ex.Message, Does.Contain("Could not find WebGL libraries"));
        }

        [Test]
        public void Validate_DirectoryMissing_NoSimd_IncludesSimdHint()
        {
            env.UnityVersion = "6000.0.26f1";
            env.DisableWasmSimd = true;
            env.DirectoryExistsOverride = false;

            var config = WebGLConfigResolver.Resolve(env);

            var ex = Assert.Throws<UnityEditor.Build.BuildFailedException>(
                () => WebGLConfigResolver.Validate(config, env));
            Assert.That(ex.Message, Does.Contain("no-SIMD library variant"));
        }

        [Test]
        public void Validate_DirectoryMissing_WithSimd_DoesNotIncludeSimdHint()
        {
            env.UnityVersion = "6000.0.26f1";
            env.DisableWasmSimd = false;
            env.DirectoryExistsOverride = false;

            var config = WebGLConfigResolver.Resolve(env);

            var ex = Assert.Throws<UnityEditor.Build.BuildFailedException>(
                () => WebGLConfigResolver.Validate(config, env));
            Assert.That(ex.Message, Does.Not.Contain("no-SIMD library variant"));
        }

        [TestCase("6000.0.26f1", true)]
        [TestCase("6001.0.0f1", true)]
        [TestCase("6100.2.5f1", true)]
        [TestCase("7000.0.0f1", true)]
        [TestCase("2023.2.0f1", true)]
        [TestCase("2022.3.10f1", false)]
        [TestCase("2021.3.25f1", false)]
        [TestCase("2019.4.0f1", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase("not-a-version", false)]
        public void IsUnity6OrNewer_HandlesVersionStrings(string unityVersion, bool expected)
        {
            Assert.AreEqual(expected, WebGLConfigResolver.IsUnity6OrNewer(unityVersion));
        }
    }
}

#endif
