using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rive.Components;
using Rive.Producer;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;
using Rive.Host;

namespace Rive.Tests
{
    /// <summary>
    /// Out-of-band asset loading without waiting, the image, font and artboard
    /// property handles, and problems found after a handle call returned
    /// reaching the log and the widget's OnError.
    /// </summary>
    public class HandleAssetsAndErrorsTests
    {
        // 1x1 PNG.
        private static readonly byte[] s_imageBytes =
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE, 0x00, 0x00, 0x00,
            0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0x0F, 0x00, 0x00,
            0x01, 0x00, 0x01, 0x5C, 0xC2, 0x8A, 0x8E, 0x00, 0x00, 0x00, 0x00, 0x49,
            0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
        };

        private TestAssetLoadingManager m_assetLoader;
        private MockLogger m_mockLogger;
        private readonly List<IDisposable> m_toDispose = new List<IDisposable>();
        private readonly List<UnityEngine.Object> m_objects = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;
            m_assetLoader = new TestAssetLoadingManager();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < m_objects.Count; i++)
            {
                if (m_objects[i] != null)
                {
                    UnityEngine.Object.Destroy(m_objects[i]);
                }
            }
            m_objects.Clear();
            for (int i = m_toDispose.Count - 1; i >= 0; i--)
            {
                m_toDispose[i]?.Dispose();
            }
            m_toDispose.Clear();
            m_assetLoader.UnloadAllAssets();
        }

        private T Track<T>(T disposable) where T : IDisposable
        {
            m_toDispose.Add(disposable);
            return disposable;
        }

        private ImageOutOfBandAsset NewImage(byte[] bytes = null)
        {
            var image = OutOfBandAsset.Create<ImageOutOfBandAsset>(bytes ?? s_imageBytes);
            m_objects.Add(image);
            return image;
        }

        private IEnumerator LoadFile(string path, Action<FileHandle> onLoaded)
        {
            Asset asset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                path, a => asset = a, () => Assert.Fail($"Failed to load {path}"));
            Future<FileHandle> load = FileHandle.LoadAsync(asset);
            yield return WaitUntil(() => load.IsDone, "The file load");
            Assert.AreEqual(FutureStatus.Succeeded, load.Status);
            onLoaded(Track(load.Result));
        }

        // A state machine off the default artboard with its default instance bound.
        private IEnumerator LoadBound(string path, Action<FileHandle, StateMachineHandle, ViewModelInstanceHandle> onLoaded)
        {
            FileHandle file = null;
            yield return LoadFile(path, f => file = f);
            StateMachineHandle stateMachine = Track(Track(file.InstantiateArtboard()).InstantiateStateMachine());
            // Core binds the artboard's default instance when nothing is bound.
            stateMachine.BindViewModelInstanceAsync(null);
            ViewModelInstanceHandle instance = Track(stateMachine.GetViewModelInstance());
            onLoaded(file, stateMachine, instance);
        }

        private RiveWidget CreateBackgroundWidget()
        {
            RivePanel panel = RivePanelTestUtils.CreatePanel();
            m_objects.Add(panel.gameObject);
            panel.ThreadingMode = ThreadingMode.BackgroundThread;
            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);
            return widget;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string what)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!condition())
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, $"{what} didn't happen within 10 seconds.");
                yield return null;
            }
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator LoadAsync_DecodesWithoutWaiting()
        {
            ImageOutOfBandAsset image = NewImage();

            var waits = new List<string>();
            CommandTransport.MainThreadWaitsForTests = waits;
            Future load;
            try
            {
                load = image.LoadAsync();
            }
            finally
            {
                CommandTransport.MainThreadWaitsForTests = null;
            }
            CollectionAssert.IsEmpty(waits, "LoadAsync shouldn't wait.");
            yield return WaitUntil(() => load.IsDone, "The decode");

            Assert.AreEqual(FutureStatus.Succeeded, load.Status);
            Assert.IsTrue(image.NativeHandle.IsValid);
            image.Unload();
            Assert.IsFalse(image.NativeHandle.IsValid, "Unload pairs with LoadAsync like Load.");
        }

        // Images decode when first drawn, so only a font can fail here.
        [UnityTest]
        public IEnumerator LoadAsync_UndecodableFont_FailsWithLoadFailed()
        {
            var font = OutOfBandAsset.Create<FontOutOfBandAsset>(new byte[] { 1, 2, 3, 4 });
            m_objects.Add(font);

            Future load = font.LoadAsync();
            yield return WaitUntil(() => load.IsDone, "The decode");

            Assert.AreEqual(FutureStatus.Failed, load.Status);
            Assert.AreEqual(RiveErrorCode.LoadFailed, ((RiveException)load.Exception).Code);
            font.Unload();
        }

        [UnityTest]
        public IEnumerator LoadAndLoadAsync_ShareOneDecode()
        {
            ImageOutOfBandAsset image = NewImage();

            Future load = image.LoadAsync();
            image.Load();
            NativeAssetHandle afterLoad = image.NativeHandle;
            yield return WaitUntil(() => load.IsDone, "The decode");

            Assert.AreEqual(2, image.RefCount());
            Assert.AreEqual(afterLoad, image.NativeHandle, "The second load should find the first one's decode.");
            image.Unload();
            Assert.IsTrue(image.NativeHandle.IsValid);
            image.Unload();
            Assert.IsFalse(image.NativeHandle.IsValid);
        }

        [UnityTest]
        public IEnumerator ImageSetValue_WithoutLoading_SetsItAndLetsItGo()
        {
            StateMachineHandle stateMachine = null;
            ViewModelInstanceHandle instance = null;
            yield return LoadBound(TestAssetReferences.riv_image_db_test, (f, s, i) => { stateMachine = s; instance = i; });
            ImageOutOfBandAsset image = NewImage();

            int changes = 0;
            ImagePropertyHandle property = instance.GetImageProperty("image");
            property.Subscribe(() => changes++);
            property.SetValue(image);
            stateMachine.Advance(1f / 60f);
            yield return WaitUntil(() => changes > 0, "The image callback");

            Assert.AreEqual(0, image.RefCount(), "The write should let go of the asset once it has run.");
        }

        [UnityTest]
        public IEnumerator FontSetValue_SetsIt()
        {
            StateMachineHandle stateMachine = null;
            ViewModelInstanceHandle instance = null;
            yield return LoadBound(TestAssetReferences.riv_font_databinding_test, (f, s, i) => { stateMachine = s; instance = i; });
            FontOutOfBandAsset font = null;
            yield return m_assetLoader.LoadAssetCoroutine<FontOutOfBandAsset>(
                TestAssetReferences.fontasset_sekuya_regular, a => font = a, () => Assert.Fail("Failed to load the font"));

            Future preload = font.LoadAsync();
            int changes = 0;
            FontPropertyHandle property = instance.GetFontProperty("font");
            property.Subscribe(() => changes++);
            property.SetValue(font);
            stateMachine.Advance(1f / 60f);
            yield return WaitUntil(() => changes > 0 && preload.IsDone, "The font callback");

            Assert.AreEqual(1, font.RefCount(), "Only the LoadAsync should still hold the font.");
            font.Unload();
        }

        [UnityTest]
        public IEnumerator ArtboardSetValue_SetsIt()
        {
            FileHandle file = null;
            StateMachineHandle stateMachine = null;
            ViewModelInstanceHandle instance = null;
            yield return LoadBound(TestAssetReferences.riv_artboard_db_test, (f, s, i) => { file = f; stateMachine = s; instance = i; });

            Assert.IsNull(file.GetBindableArtboard("No such artboard"));
            Assert.IsTrue(m_mockLogger.LoggedWarningsContains("no artboard named 'No such artboard'"));

            BindableArtboardHandle blue = Track(file.GetBindableArtboard("ArtboardBlue"));
            int changes = 0;
            ArtboardPropertyHandle property = instance.GetArtboardProperty("artboard_1");
            property.Subscribe(() => changes++);
            property.SetValue(blue);
            stateMachine.Advance(1f / 60f);
            yield return WaitUntil(() => changes > 0, "The artboard callback");

            Assert.AreEqual("ArtboardBlue", blue.GetNameAsync().Result);
        }

#if RIVE_USING_EXPERIMENTAL
        [UnityTest]
        public IEnumerator RenderTextureImage_BindsThroughAHandle()
        {
            Asset asset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_image_db_test, a => asset = a, () => Assert.Fail("Failed to load the image asset"));
            RiveWidget widget = CreateBackgroundWidget();
            widget.Load(asset);
            yield return WaitUntil(() => widget.Status == WidgetStatus.Loaded, "The load");

            var texture = new RenderTexture(16, 16, 0);
            texture.Create();
            m_objects.Add(texture);
            var source = Track(new RenderTextureImageSource(texture));
            int changes = 0;
            ImagePropertyHandle image = widget.StateMachineHandle.GetViewModelInstance().GetImageProperty("image");
            image.Subscribe(() => changes++);

            image.SetFromRenderTextureImageSource(source);
            yield return WaitUntil(() => changes > 0, "The bound image reaching the property");

            Assert.AreEqual(1, RenderTextureImageManager.Instance.BindingCount);
            image.SetFromRenderTextureImageSource(null);
            Assert.AreEqual(0, RenderTextureImageManager.Instance.BindingCount, "Null unbinds it.");
        }
#endif

        [UnityTest]
        public IEnumerator MissingPath_ReachesOnErrorWithWhereItCameFrom()
        {
            Asset asset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_asset_databinding_test, a => asset = a, () => Assert.Fail("Failed to load the test asset"));
            RiveWidget widget = CreateBackgroundWidget();
            var errors = new List<RiveException>();
            widget.OnError += errors.Add;
            widget.Load(asset);
            yield return WaitUntil(() => widget.Status == WidgetStatus.Loaded, "The load");

            ViewModelInstanceHandle person = widget.StateMachineHandle.GetViewModelInstance();
            person.GetNumberProperty("missing").SetValue(1f);
            person.GetNumberProperty("name").SetValue(2f);
            yield return WaitUntil(() => errors.Count >= 2, "Both errors");

            Assert.AreEqual(RiveErrorCode.PropertyNotFound, errors[0].Code);
            Assert.AreEqual(RiveErrorCode.TypeMismatch, errors[1].Code);
            StringAssert.Contains("is a String property, not a Number property", errors[1].Message);
#if UNITY_EDITOR
            // The call site is only captured in the editor.
            Assert.IsTrue(m_mockLogger.LoggedWarningsContains(nameof(MissingPath_ReachesOnErrorWithWhereItCameFrom)),
                "In the editor the log should say where the property was asked for.");
#endif
        }

        [UnityTest]
        public IEnumerator ListIndexOutOfRange_ReachesOnError()
        {
            Asset asset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_db_list_test, a => asset = a, () => Assert.Fail("Failed to load the list asset"));
            RiveWidget widget = CreateBackgroundWidget();
            var errors = new List<RiveException>();
            widget.OnError += errors.Add;
            widget.Load(asset);
            yield return WaitUntil(() => widget.Status == WidgetStatus.Loaded, "The load");

            ListPropertyHandle items = widget.StateMachineHandle.GetViewModelInstance().GetListProperty("items");
            items.RemoveAt(999);
            ViewModelInstanceHandle nothing = Track(items.GetInstanceAt(999));
            Future<string> read = nothing.GetStringProperty("text").GetValueAsync();
            yield return WaitUntil(() => errors.Count >= 2 && read.IsDone, "The list errors");

            Assert.AreEqual(RiveErrorCode.IndexOutOfRange, errors[0].Code);
            StringAssert.Contains("RemoveAt: index 999", errors[0].Message);
            Assert.AreEqual(RiveErrorCode.IndexOutOfRange, errors[1].Code);
            Assert.AreSame(errors[1], nothing.Error);
            Assert.AreSame(errors[1], read.Exception, "A read on it fails with the lookup's own error.");
            yield return null;
            Assert.AreEqual(2, errors.Count, "The read doesn't report it again.");
        }

        [UnityTest]
        public IEnumerator LoadFailure_RaisesOnErrorThenError()
        {
            Asset asset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_asset_databinding_test, a => asset = a, () => Assert.Fail("Failed to load the test asset"));
            RiveWidget widget = CreateBackgroundWidget();
            RiveException error = null;
            WidgetStatus statusAtError = WidgetStatus.Uninitialized;
            widget.OnError += e =>
            {
                error = e;
                statusAtError = widget.Status;
            };

            widget.Load(asset, "No such artboard", null);
            yield return WaitUntil(() => widget.Status == WidgetStatus.Error, "The failure");

            Assert.IsNotNull(error);
            Assert.AreEqual(RiveErrorCode.ArtboardNotFound, error.Code);
            Assert.AreNotEqual(WidgetStatus.Error, statusAtError, "OnError comes before the status changes.");
        }
    }
}
