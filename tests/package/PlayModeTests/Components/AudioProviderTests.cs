using System.Collections;
using NUnit.Framework;
using Rive.Components;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    public class AudioProviderTests
    {
        private RivePanel m_panel;
        private RiveWidget m_widget;
        private GameObject m_widgetObject;
        private TestAssetLoadingManager testAssetLoadingManager;
        private MockLogger mockLogger;

        [SetUp]
        public void Setup()
        {
            mockLogger = new MockLogger();
            DebugLogger.Instance = mockLogger;

            CleanupAudioProviders();

            m_panel = RivePanelTestUtils.CreatePanel("AudioTestPanel");
            m_widget = RivePanelTestUtils.CreateWidget<RiveWidget>("AudioTestWidget");
            m_widgetObject = m_widget.gameObject;
            m_widget.transform.SetParent(m_panel.WidgetContainer, false);

            testAssetLoadingManager = new TestAssetLoadingManager();
        }

        [TearDown]
        public void TearDown()
        {
            if (m_widgetObject != null)
            {
                Object.Destroy(m_widgetObject);
            }
            if (m_panel != null)
            {
                Object.Destroy(m_panel.gameObject);
            }

            CleanupAudioProviders();

            testAssetLoadingManager.UnloadAllAssets();
        }

        private void CleanupAudioProviders()
        {
            var providers = Resources.FindObjectsOfTypeAll<AudioProvider>();
            foreach (var provider in providers)
            {
                if (provider != null)
                {
                    Object.Destroy(provider);
                }
            }
        }
#if !UNITY_WEBGL || UNITY_EDITOR

        [UnityTest]
        public IEnumerator RiveWidget_WithNoAudioArtboard_DoesNotCreateProvider()
        {
            // Load a Rive asset known to have no audio
            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_cleanTheCar,
                asset => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {TestAssetReferences.riv_cleanTheCar}"));

            Assert.IsNotNull(riveAsset, "Expected a valid Rive Asset");

            Assert.IsNull(Object.FindObjectOfType<AudioProvider>(), "AudioProvider should not exist before loading");

            m_widget.Load(riveAsset);

            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

            Assert.IsFalse(m_widget.Artboard.HasAudio, "Artboard should not have audio");

            yield return null;

            Assert.IsNull(Object.FindObjectOfType<AudioProvider>(), "AudioProvider should not be created for artboards without audio");
        }

        [UnityTest]
        public IEnumerator RiveWidget_WithAudioArtboard_CreatesProvider()
        {
            // Load a Rive asset known to have audio
            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_ping_pong_demo,
                asset => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {TestAssetReferences.riv_ping_pong_demo}"));

            Assert.IsNotNull(riveAsset, "Expected a valid Rive Asset");

            Assert.IsNull(Object.FindObjectOfType<AudioProvider>(), "AudioProvider should not exist before loading");

            m_widget.Load(riveAsset);

            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

            Assert.IsTrue(m_widget.Artboard.HasAudio, "Artboard should have audio");

            yield return null;

            AudioProvider audioProvider = Object.FindObjectOfType<AudioProvider>();

            Assert.IsNotNull(audioProvider, "AudioProvider should be created for artboards with audio");

            Assert.AreSame(audioProvider, m_widget.AudioProvider, "AudioProvider should be the same as the one used by the widget");
        }

        [UnityTest]
        public IEnumerator RiveWidget_SwitchingFromNoAudioToAudio_CreatesProvider()
        {
            // First, load a file without audio
            Asset noAudioAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_cleanTheCar,
                asset => noAudioAsset = asset,
                () => Assert.Fail($"Failed to load asset at {TestAssetReferences.riv_cleanTheCar}"));

            m_widget.Load(noAudioAsset);
            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);
            Assert.IsFalse(m_widget.Artboard.HasAudio, "Artboard should not have audio");
            yield return null;
            Assert.IsNull(Object.FindObjectOfType<AudioProvider>(), "AudioProvider should not exist after loading no-audio file");

            // Then, load a file with audio
            Asset audioAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_ping_pong_demo,
                asset => audioAsset = asset,
                () => Assert.Fail($"Failed to load asset at {TestAssetReferences.riv_ping_pong_demo}"));

            m_widget.Load(audioAsset);
            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

            Assert.IsTrue(m_widget.Artboard.HasAudio, "Artboard should have audio");
            yield return null;

            Assert.IsNotNull(Object.FindObjectOfType<AudioProvider>(), "AudioProvider should be created after switching to an audio-enabled file");
        }

        [UnityTest]
        public IEnumerator RiveWidget_UsesExplicitCustomAudioProvider_WhenAssignedBeforeLoad()
        {
            var customProviderGO = new GameObject("CustomAudioProvider");
            var customProvider = customProviderGO.AddComponent<AudioProvider>();

            // Explicitly assign it to the widget before loading so that it is used instead of the global provider
            m_widget.CustomAudioProvider = customProvider;

            // Load a Rive asset known to have audio
            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_ping_pong_demo,
                asset => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {TestAssetReferences.riv_ping_pong_demo}"));

            Assert.IsNotNull(riveAsset, "Expected a valid Rive Asset");

            Assert.AreEqual(1, Resources.FindObjectsOfTypeAll<AudioProvider>().Length,
                "Only the custom AudioProvider created for this test should exist before loading");

            m_widget.Load(riveAsset);

            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);
            Assert.IsTrue(m_widget.Artboard.HasAudio, "Artboard should have audio");

            yield return null;

            Assert.AreSame(customProvider, m_widget.AudioProvider,
                "Widget should use the explicitly assigned AudioProvider instance");

            Assert.IsNotNull(customProvider.AudioSource, "Custom AudioProvider should have an AudioSource");
            Assert.IsTrue(customProvider.AudioSource.isPlaying,
                "Custom AudioProvider's AudioSource should be playing so audio can be generated");

            Object.Destroy(customProviderGO);
        }

        [UnityTest]
        public IEnumerator RiveWidget_SwitchingToCustomAudioProvider_ReconfiguresArtboard()
        {
            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_ping_pong_demo,
                asset => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {TestAssetReferences.riv_ping_pong_demo}"));

            Assert.IsNotNull(riveAsset, "Expected a valid Rive Asset");

            m_widget.Load(riveAsset);

            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);
            Assert.IsTrue(m_widget.Artboard.HasAudio, "Artboard should have audio");

            yield return null;

            var initialProvider = m_widget.AudioProvider;
            Assert.IsNotNull(initialProvider, "Widget should have an AudioProvider after loading an audio artboard");

            var customProviderGO = new GameObject("RuntimeCustomAudioProvider");
            var customProvider = customProviderGO.AddComponent<AudioProvider>();

            m_widget.CustomAudioProvider = customProvider;

            yield return null;

            Assert.AreSame(customProvider, m_widget.AudioProvider,
                "Widget should use the custom AudioProvider after it is assigned at runtime");

            Assert.AreNotSame(initialProvider, m_widget.AudioProvider,
                "Widget AudioProvider should be updated to the new custom provider");

            Object.Destroy(customProviderGO);
        }

        [UnityTest]
        public IEnumerator RiveWidget_SettingAudioProviderToNull_RevertsToGlobalProvider()
        {
            // We load an audio-enabled asset so that the default/global provider is created
            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_ping_pong_demo,
                asset => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {TestAssetReferences.riv_ping_pong_demo}"));

            Assert.IsNotNull(riveAsset, "Expected a valid Rive Asset");

            m_widget.Load(riveAsset);

            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);
            Assert.IsTrue(m_widget.Artboard.HasAudio, "Artboard should have audio");

            yield return null;

            var globalProvider = m_widget.AudioProvider;
            Assert.IsNotNull(globalProvider, "Widget should have a global AudioProvider after loading an audio artboard");

            var customProviderGO = new GameObject("CustomAudioProviderForNullTest");
            var customProvider = customProviderGO.AddComponent<AudioProvider>();

            m_widget.CustomAudioProvider = customProvider;

            yield return null;

            Assert.AreSame(customProvider, m_widget.AudioProvider,
                "Widget should use the custom AudioProvider after it is assigned");
            Assert.AreNotSame(globalProvider, m_widget.AudioProvider,
                "Widget AudioProvider should differ from the original global provider while a custom provider is set");

            m_widget.CustomAudioProvider = null;

            yield return null;

            Assert.IsNotNull(m_widget.AudioProvider,
                "Widget should still have an AudioProvider after clearing the custom provider");
            Assert.AreSame(globalProvider, m_widget.AudioProvider,
                "Clearing the custom AudioProvider should revert the widget back to the shared global provider");

            Object.Destroy(customProviderGO);
        }
#endif


#if UNITY_WEBGL && !UNITY_EDITOR
        [UnityTest]
        public IEnumerator WebGL_RiveWidget_WithAudioArtboard_DoesNotCreateProvider()
        {
            // On WebGL, we use system audio (bypassing AudioSource); ensure no provider is spawned even for audio-enabled files
            Asset audioAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_ping_pong_demo,
                asset => audioAsset = asset,
                () => Assert.Fail($"Failed to load asset at {TestAssetReferences.riv_ping_pong_demo}"));

            m_widget.Load(audioAsset);
            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

            Assert.IsTrue(m_widget.Artboard.HasAudio, "Artboard should have audio");
            yield return null;

            Assert.IsNull(Object.FindObjectOfType<AudioProvider>(), "AudioProvider should not be created on WebGL");
        }
#endif
    }
}


