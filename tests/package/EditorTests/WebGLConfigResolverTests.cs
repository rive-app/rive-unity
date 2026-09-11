#if UNITY_EDITOR

using NUnit.Framework;
using Rive.EditorTools;

namespace Rive.Tests.EditorTests
{
    internal class TestWebGLEnvironment : IWebGLEnvironment
    {
        public string UnityVersion { get; set; } = "6000.0.26f1";
        public bool DisableWasmSimd { get; set; } = false;
        public bool TargetsWasm2023 { get; set; } = false;
        public bool UsesThreads { get; set; } = false;
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
            Assert.AreEqual("emscripten_3.1.38", System.IO.Path.GetFileName(config.SourcePath));
        }

        [Test]
        public void Resolve_Unity6_WithSimdDisabled_ResolvesNoSimdVariant()
        {
            env.UnityVersion = "6000.0.26f1";
            env.DisableWasmSimd = true;

            var config = WebGLConfigResolver.Resolve(env);

            Assert.AreEqual("3.1.38", config.EmscriptenVersion);
            Assert.IsTrue(config.UseNoSimd);
            Assert.AreEqual("emscripten_3.1.38_nosimd", System.IO.Path.GetFileName(config.SourcePath));
        }

        [Test]
        public void Resolve_Unity6_WithWasm2023_ResolvesWasm2023Variant()
        {
            env.UnityVersion = "6000.0.26f1";
            env.TargetsWasm2023 = true;

            var config = WebGLConfigResolver.Resolve(env);

            Assert.AreEqual("3.1.38", config.EmscriptenVersion);
            Assert.IsTrue(config.UseWasm2023);
            Assert.IsFalse(config.UseNoSimd);
            Assert.AreEqual("emscripten_3.1.38_wasm2023", System.IO.Path.GetFileName(config.SourcePath));
        }

        [Test]
        public void Resolve_Unity6_WithWasm2023AndSimdDisabled_PrefersWasm2023()
        {
            env.UnityVersion = "6000.0.26f1";
            env.TargetsWasm2023 = true;
            env.DisableWasmSimd = true;

            var config = WebGLConfigResolver.Resolve(env);

            Assert.IsTrue(config.UseWasm2023);
            Assert.IsFalse(config.UseNoSimd);
            Assert.AreEqual("emscripten_3.1.38_wasm2023", System.IO.Path.GetFileName(config.SourcePath));
        }

        [Test]
        public void Resolve_Unity6_WithoutWasm2023_ResolvesDefaultVariant()
        {
            env.UnityVersion = "6000.0.26f1";
            env.TargetsWasm2023 = false;

            var config = WebGLConfigResolver.Resolve(env);

            Assert.IsFalse(config.UseWasm2023);
            Assert.AreEqual("emscripten_3.1.38", System.IO.Path.GetFileName(config.SourcePath));
        }

        [Test]
        public void Resolve_Unity6_WithWasm2023AndThreads_ResolvesCombinedVariant()
        {
            env.UnityVersion = "6000.0.26f1";
            env.TargetsWasm2023 = true;
            env.UsesThreads = true;

            var config = WebGLConfigResolver.Resolve(env);

            Assert.IsTrue(config.UseWasm2023);
            Assert.IsTrue(config.UseThreads);
            Assert.AreEqual("emscripten_3.1.38_wasm2023_mt", System.IO.Path.GetFileName(config.SourcePath));
        }

        [Test]
        public void Resolve_Unity6_WithThreadsAndSimdDisabled_PrefersThreads()
        {
            env.UnityVersion = "6000.0.26f1";
            env.UsesThreads = true;
            env.TargetsWasm2023 = true; // Unity forces this on with threads
            env.DisableWasmSimd = true;

            var config = WebGLConfigResolver.Resolve(env);

            Assert.IsTrue(config.UseThreads);
            Assert.IsFalse(config.UseNoSimd);
            Assert.AreEqual("emscripten_3.1.38_wasm2023_mt", System.IO.Path.GetFileName(config.SourcePath));
        }

        [Test]
        public void Resolve_Unity2023_ThreadsWithoutWasm2023_DoesNotResolveMtVariant()
        {
            // 2023.2 passes IsUnity6OrNewer but has no wasm2023 setting, while threadsSupport
            // can still be on. That must not ask for a variant we don't ship.
            env.UnityVersion = "2023.2.0f1";
            env.UsesThreads = true;
            env.TargetsWasm2023 = false;

            var config = WebGLConfigResolver.Resolve(env);

            Assert.IsFalse(config.UseThreads);
            Assert.AreEqual("emscripten_3.1.38", System.IO.Path.GetFileName(config.SourcePath));
        }

        [Test]
        public void Validate_ThreadsWithoutWasm2023_ExplainsRequirement()
        {
            env.UnityVersion = "2023.2.0f1";
            env.UsesThreads = true;
            env.TargetsWasm2023 = false;
            env.DirectoryExistsOverride = true;

            var config = WebGLConfigResolver.Resolve(env);

            var ex = Assert.Throws<UnityEditor.Build.BuildFailedException>(
                () => WebGLConfigResolver.Validate(config, env));
            Assert.That(ex.Message, Does.Contain("Native C/C++ Multithreading"));
            Assert.That(ex.Message, Does.Contain("Target WebAssembly 2023"));
        }

        [Test]
        public void Validate_Unity6_ThreadsWithoutWasm2023_ExplainsRequirement()
        {
            // On a real Unity 6 editor this pair can't occur, because enabling multithreading
            // forces WebAssembly 2023 on and DefaultWebGLEnvironment reports that effective value.
            // Kept so Validate still refuses the combination rather than picking a variant that
            // was never built for it.
            env.UnityVersion = "6000.0.26f1";
            env.UsesThreads = true;
            env.TargetsWasm2023 = false;
            env.DirectoryExistsOverride = true;

            var config = WebGLConfigResolver.Resolve(env);

            Assert.IsFalse(config.UseThreads);
            var ex = Assert.Throws<UnityEditor.Build.BuildFailedException>(
                () => WebGLConfigResolver.Validate(config, env));
            Assert.That(ex.Message, Does.Contain("Target WebAssembly 2023"));
        }

        [Test]
        public void Resolve_ThreadsWithoutWasm2023_StillAppliesNoSimd()
        {
            // Threads don't suppress no-SIMD, since without wasm2023 they select no variant of
            // their own. The build then fails Validate rather than silently using these libs.
            env.UnityVersion = "6000.0.26f1";
            env.UsesThreads = true;
            env.TargetsWasm2023 = false;
            env.DisableWasmSimd = true;

            var config = WebGLConfigResolver.Resolve(env);

            Assert.IsTrue(config.UseNoSimd);
            Assert.IsFalse(config.UseThreads);
            Assert.AreEqual("emscripten_3.1.38_nosimd", System.IO.Path.GetFileName(config.SourcePath));

            Assert.Throws<UnityEditor.Build.BuildFailedException>(
                () => WebGLConfigResolver.Validate(config, env));
        }

        [Test]
        public void Validate_ThreadsOnOlderUnity_ExplainsRequirement()
        {
            env.UnityVersion = "2022.3.10f1";
            env.UsesThreads = true;
            env.DirectoryExistsOverride = true;

            var config = WebGLConfigResolver.Resolve(env);

            var ex = Assert.Throws<UnityEditor.Build.BuildFailedException>(
                () => WebGLConfigResolver.Validate(config, env));
            Assert.That(ex.Message, Does.Contain("Unity 6 or newer"));
        }

        [Test]
        public void Resolve_Unity2022_IgnoresThreadsSetting_ResolvesEmscripten318()
        {
            env.UnityVersion = "2022.3.10f1";
            env.UsesThreads = true;

            var config = WebGLConfigResolver.Resolve(env);

            Assert.AreEqual("3.1.8", config.EmscriptenVersion);
            Assert.IsFalse(config.UseThreads);
            Assert.AreEqual("emscripten_3.1.8", System.IO.Path.GetFileName(config.SourcePath));
        }

        [Test]
        public void Validate_DirectoryMissing_Threads_IncludesThreadsHint()
        {
            env.UnityVersion = "6000.0.26f1";
            env.UsesThreads = true;
            env.TargetsWasm2023 = true; // Unity forces this on with threads
            env.DirectoryExistsOverride = false;

            var config = WebGLConfigResolver.Resolve(env);

            var ex = Assert.Throws<UnityEditor.Build.BuildFailedException>(
                () => WebGLConfigResolver.Validate(config, env));
            Assert.That(ex.Message, Does.Contain("Native C/C++ Multithreading"));
        }

        [Test]
        public void Resolve_Unity2022_IgnoresWasm2023Setting_ResolvesEmscripten318()
        {
            env.UnityVersion = "2022.3.10f1";
            env.TargetsWasm2023 = true;

            var config = WebGLConfigResolver.Resolve(env);

            Assert.AreEqual("3.1.8", config.EmscriptenVersion);
            Assert.IsFalse(config.UseWasm2023);
            Assert.AreEqual("emscripten_3.1.8", System.IO.Path.GetFileName(config.SourcePath));
        }

        [Test]
        public void Validate_DirectoryMissing_Wasm2023_IncludesWasm2023Hint()
        {
            env.UnityVersion = "6000.0.26f1";
            env.TargetsWasm2023 = true;
            env.DirectoryExistsOverride = false;

            var config = WebGLConfigResolver.Resolve(env);

            var ex = Assert.Throws<UnityEditor.Build.BuildFailedException>(
                () => WebGLConfigResolver.Validate(config, env));
            Assert.That(ex.Message, Does.Contain("Target WebAssembly 2023"));
            Assert.That(ex.Message, Does.Not.Contain("Native C/C++ Multithreading"));
        }

        [Test]
        public void Resolve_Unity2022_IgnoresSimdSetting_ResolvesEmscripten318()
        {
            env.UnityVersion = "2022.3.10f1";
            env.DisableWasmSimd = true;

            var config = WebGLConfigResolver.Resolve(env);

            Assert.AreEqual("3.1.8", config.EmscriptenVersion);
            Assert.IsFalse(config.UseNoSimd);
            Assert.AreEqual("emscripten_3.1.8", System.IO.Path.GetFileName(config.SourcePath));
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
            Assert.AreEqual("emscripten_3.1.38_nosimd", System.IO.Path.GetFileName(config.SourcePath));
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
