#if RIVE_USING_EXPERIMENTAL
using Rive.Host;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Rive.Components;
using Rive.Producer;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Rive.Tests
{
    /// <summary>
    /// Lifecycle/binding tests for <see cref="RenderTextureImageSource"/> and
    /// <see cref="RenderTextureImageManager"/>.
    /// </summary>
    public class RenderTextureImageSourceTests
    {
        private GameObject m_widgetObject;
        private RiveWidget m_widget;
        private RivePanel m_panel;
        private MockLogger m_mockLogger;
        private TestAssetLoadingManager m_testAssetLoadingManager;
        private List<File> m_loadedFiles;
        private readonly List<RenderTextureImageSource> m_createdImages = new List<RenderTextureImageSource>();
        private readonly List<RenderTexture> m_createdTextures = new List<RenderTexture>();

        private ViewModelInstance m_viewModelInstance;
        private ViewModelInstanceImageProperty m_imageProperty;
        // Only the pixel test needs one, so it's created there rather than in
        // Setup, and torn down here with everything else.
        private GameObject m_cameraObject;

        [SetUp]
        public void Setup()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;

            m_testAssetLoadingManager = new TestAssetLoadingManager();
            m_loadedFiles = new List<File>();

            m_widgetObject = new GameObject("RiveWidget");
            m_widget = m_widgetObject.AddComponent<RiveWidget>();

            var panelObject = new GameObject("RivePanel");
            m_panel = panelObject.AddComponent<RivePanel>();
            m_widget.transform.SetParent(m_panel.WidgetContainer, false);

            // Start each test from a clean manager so static state can't leak
            // between tests when editor domain reload is disabled.
            RenderTextureImageManager.Instance.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var image in m_createdImages)
            {
                image?.Dispose();
            }
            m_createdImages.Clear();

            RenderTextureImageManager.Instance.Clear();

            foreach (var texture in m_createdTextures)
            {
                if (texture != null)
                {
                    texture.Release();
                    Object.Destroy(texture);
                }
            }
            m_createdTextures.Clear();

            foreach (var file in m_loadedFiles)
            {
                file.Dispose();
            }
            m_loadedFiles.Clear();

            if (m_cameraObject != null)
            {
                Object.Destroy(m_cameraObject);
                m_cameraObject = null;
            }

            if (m_widgetObject != null)
            {
                Object.Destroy(m_widgetObject);
            }
            if (m_panel != null)
            {
                Object.Destroy(m_panel.gameObject);
            }

            m_testAssetLoadingManager.UnloadAllAssets();
        }

        private File LoadAndTrackFile(Asset asset)
        {
            var file = File.Load(asset);
            m_loadedFiles.Add(file);
            return file;
        }

        private RenderTexture CreateTrackUnityRenderTexture(int size = 64)
        {
            var rt = new RenderTexture(size, size, 0);
            rt.Create();
            m_createdTextures.Add(rt);
            return rt;
        }

        private RenderTexture CreateTrackUnityRenderTexture(RenderTextureDescriptor descriptor)
        {
            var rt = new RenderTexture(descriptor);
            rt.Create();
            m_createdTextures.Add(rt);
            return rt;
        }

        private static RenderTextureDescriptor BaseDescriptor(int size = 64)
        {
            return new RenderTextureDescriptor(size, size, RenderTextureFormat.ARGB32, 0);
        }


        private RenderTextureImageSource CreateTrackRenderTextureImage()
        {
            var image = new RenderTextureImageSource(
                CreateTrackUnityRenderTexture(),
                RenderTextureImageSource.TextureProcessingMode.None,// Using this because we only want to test that the manager works here, not the processing/blit logic.
                RenderTextureImageSource.RefreshMode.Manual); // Using this because we only want to test that the manager works here, not the refresh logic.
            m_createdImages.Add(image);
            return image;
        }

        // Loads the image data-binding test asset and resolves its "image" property.
        private IEnumerator LoadImagePropertyRoutine()
        {
            Asset riveAsset = null;
            yield return m_testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_image_db_test,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {TestAssetReferences.riv_image_db_test}"));

            File riveFile = LoadAndTrackFile(riveAsset);
            m_widget.Load(riveFile);
            yield return RivePanelTestUtils.WaitForLoaded(m_widget);

            m_viewModelInstance = m_widget.StateMachine.ViewModelInstance;
            Assert.IsNotNull(m_viewModelInstance, "ViewModelInstance should exist");

            m_imageProperty = m_viewModelInstance.GetProperty<ViewModelInstanceImageProperty>("image");
            Assert.IsNotNull(m_imageProperty, "Image property should exist");
        }


        private IEnumerator LoadImageAssetRoutine(System.Action<ImageOutOfBandAsset> onLoaded)
        {
            ImageOutOfBandAsset asset = null;
            yield return m_testAssetLoadingManager.LoadAssetCoroutine<ImageOutOfBandAsset>(
                TestAssetReferences.imageasset_desert,
                (loaded) => asset = loaded,
                () => Assert.Fail($"Failed to load asset at {TestAssetReferences.imageasset_desert}"));
            Assert.IsNotNull(asset, "Image asset should load");
            onLoaded(asset);
        }

        [Test]
        public void Construction_DoesNotRegisterWithManager()
        {
            CreateTrackRenderTextureImage();

            Assert.AreEqual(0, RenderTextureImageManager.Instance.BindingCount,
                "Constructing an image should not register it; registration happens on bind.");
            Assert.IsFalse(RenderTextureImageManager.HasAnyBindings);
        }

        [Test]
        public void IdleManagerTick_DoesNotQueueNativeWork()
        {
            long jobsBefore = CommandTransport.RequestsForTests;

            RenderTextureImageManager.Instance.Tick();

            Assert.AreEqual(jobsBefore, CommandTransport.RequestsForTests);
        }

        [Test]
        public void ImageCommandStorage_IsReusedAfterProducerCompletes()
        {
            var queue = RenderImageCommandQueue.Instance;
            uint handle = RenderImageCommandQueue.NextHandle();
            var properties = new ViewModelInstanceImageProperty[1];

            queue.EnqueueBuild(handle, IntPtr.Zero, 0, 0, false, properties);
            object command = queue.PendingCommandForTests(handle);
            Assert.IsNotNull(command);
            object instances = RenderImageCommandQueue.InstancesForTests(command);
            queue.EnqueueBuild(handle, IntPtr.Zero, 0, 0, false, properties);
            Assert.AreSame(command, queue.PendingCommandForTests(handle), "Pending updates should coalesce in place.");
            Assert.AreSame(instances, RenderImageCommandQueue.InstancesForTests(command));

            queue.Flush();
            object commands = queue.LastBatchCommandsForTests;
            CommandTransport.Barrier();
            queue.Flush();

            queue.EnqueueBuild(handle, IntPtr.Zero, 0, 0, false, properties);
            Assert.AreSame(command, queue.PendingCommandForTests(handle), "Completed commands should be pooled.");
            Assert.AreSame(instances, RenderImageCommandQueue.InstancesForTests(command));
            queue.Flush();
            Assert.AreSame(commands, queue.LastBatchCommandsForTests,
                "The batch command array should also be reused.");
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator ImageCommands_DoNotWaitForBusyProducer()
        {
            if (!CommandTransport.IsThreaded)
            {
                Assert.Ignore("Nothing is queued when work runs on the calling thread.");
            }

            var queue = RenderImageCommandQueue.Instance;
            var started = new ManualResetEventSlim(false);
            var gate = new ManualResetEventSlim(false);
            ServerGate.Hold(started, gate);
            Assert.IsTrue(started.Wait(2000), "The producer should have picked the job up.");

            try
            {
                long jobsBefore = CommandTransport.RequestsForTests;
                uint handle = RenderImageCommandQueue.NextHandle();
                queue.EnqueueBuild(handle, IntPtr.Zero, 0, 0, false, null);
                queue.Flush();
                queue.EnqueueDestroy(handle);
                queue.Flush();

                Assert.AreEqual(1, CommandTransport.RequestsForTests - jobsBefore,
                    "A busy producer should hold at most one image batch.");
            }
            finally
            {
                gate.Set();
                CommandTransport.Barrier();
            }

            queue.Flush();
            yield return null;
            queue.Flush();
        }

        [Test]
        public void Source_ReturnsProvidedTexture()
        {
            var rt = CreateTrackUnityRenderTexture();
            var image = new RenderTextureImageSource(rt, RenderTextureImageSource.TextureProcessingMode.None);
            m_createdImages.Add(image);

            Assert.AreSame(rt, image.Source);
        }

        [Test]
        public void IsValid_TracksBackendSupport()
        {
            var rt = CreateTrackUnityRenderTexture();
            var image = new RenderTextureImageSource(rt, RenderTextureImageSource.TextureProcessingMode.None);
            m_createdImages.Add(image);

            // A live source is only valid on a supported backend; on OpenGL/WebGL
            // it safe-fails to invalid rather than throwing.
            Assert.AreEqual(TextureHelper.SupportsRenderTextureImageSource(), image.IsValid);
        }

        [Test]
        public void IsValid_AfterDispose_IsFalse()
        {
            var image = CreateTrackRenderTextureImage();

            image.Dispose();

            Assert.IsFalse(image.IsValid);
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            var image = CreateTrackRenderTextureImage();

            image.Dispose();
            Assert.IsTrue(image.IsDisposed);

            // Second dispose should be a no-op and must not throw.
            Assert.DoesNotThrow(() => image.Dispose());
            Assert.IsTrue(image.IsDisposed);
        }

        [Test]
        public void Construction_WithNullSource_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RenderTextureImageSource(null));
        }

        [Test]
        public void Construction_WithMsaaSource_Throws()
        {
            var rt = new RenderTexture(64, 64, 0) { antiAliasing = 4 };
            rt.Create();
            m_createdTextures.Add(rt);

            Assert.Throws<ArgumentException>(() => new RenderTextureImageSource(rt));
        }

        [Test]
        public void Construction_WithArraySource_Throws()
        {
            var desc = BaseDescriptor();
            desc.dimension = TextureDimension.Tex2DArray;
            desc.volumeDepth = 2;
            var rt = CreateTrackUnityRenderTexture(desc);

            Assert.Throws<ArgumentException>(() => new RenderTextureImageSource(rt));
        }

        [Test]
        public void Construction_WithCubeSource_Throws()
        {
            var desc = BaseDescriptor();
            desc.dimension = TextureDimension.Cube;
            var rt = CreateTrackUnityRenderTexture(desc);

            Assert.Throws<ArgumentException>(() => new RenderTextureImageSource(rt));
        }

        [Test]
        public void Construction_With3DSource_Throws()
        {
            var desc = BaseDescriptor();
            desc.dimension = TextureDimension.Tex3D;
            desc.volumeDepth = 4;
            var rt = CreateTrackUnityRenderTexture(desc);

            Assert.Throws<ArgumentException>(() => new RenderTextureImageSource(rt));
        }

        [Test]
        public void Construction_WithPlain2DSource_DoesNotThrow()
        {
            var rt = CreateTrackUnityRenderTexture();

            RenderTextureImageSource image = null;
            Assert.DoesNotThrow(() => image = new RenderTextureImageSource(rt));
            m_createdImages.Add(image);
        }

        [Test]
        public void Image_WithTextureProcessingModeNone_UsesDirectSource()
        {

            var rt = CreateTrackUnityRenderTexture();
            var image = new RenderTextureImageSource(rt, RenderTextureImageSource.TextureProcessingMode.None);
            m_createdImages.Add(image);

            Assert.IsInstanceOf<DirectTextureFrameProvider>(image.FrameProvider);
        }

        [Test]
        public void Create_WithTextureProcessingModeNone_ReturnsDirectSource()
        {
            var rt = CreateTrackUnityRenderTexture();

            var source = TextureFrameProvider.Create(rt, RenderTextureImageSource.TextureProcessingMode.None);

            // None disables both transforms regardless of backend/color space, so
            // it must never allocate an intermediate.
            Assert.IsInstanceOf<DirectTextureFrameProvider>(source);
            Assert.AreSame(rt, source.Source);
            source.Dispose();
        }

        [Test]
        public void Create_WithAnyModeButNone_CopiesTheSource(
            [Values(
                RenderTextureImageSource.TextureProcessingMode.Auto,
                RenderTextureImageSource.TextureProcessingMode.Orientation,
                RenderTextureImageSource.TextureProcessingMode.Color)]
            RenderTextureImageSource.TextureProcessingMode mode)
        {
            var rt = CreateTrackUnityRenderTexture();

            var source = TextureFrameProvider.Create(rt, mode);

            Assert.IsInstanceOf<ProcessedTextureFrameProvider>(source);
            source.Dispose();
        }

        [Test]
        public void DirectTextureFrameProvider_Acquire_PreservesSourceMetadata()
        {
            var rt = CreateTrackUnityRenderTexture();
            var source = new DirectTextureFrameProvider(rt);

            NativeTextureFrame frame = source.Acquire();

            // Passthrough keeps the source's own sRGB-ness and size.
            if (frame.IsValid)
            {
                Assert.AreEqual(rt.sRGB, frame.IsSRGB);
                Assert.AreEqual(rt.width, frame.Width);
                Assert.AreEqual(rt.height, frame.Height);
            }
            Assert.AreSame(rt, source.Source);
            Assert.DoesNotThrow(() => source.Dispose());
        }

        [Test]
        public void ProcessedTextureFrameProvider_Acquire_ReturnsNonSRGBFrame()
        {
            // flip and gamma-encode exercises the real blit/intermediate path. It hands Rive gamma bytes in a non-sRGB texture, so the
            // frame must bind as non-sRGB (otherwise the image is double encoded).
            var rt = CreateTrackUnityRenderTexture();
            var source = new ProcessedTextureFrameProvider(rt, flip: true, gammaEncode: true);
            try
            {
                NativeTextureFrame frame = source.Acquire();

                if (frame.IsValid)
                {
                    Assert.IsFalse(frame.IsSRGB);
                    Assert.AreEqual(rt.width, frame.Width);
                    Assert.AreEqual(rt.height, frame.Height);
                }
                Assert.AreSame(rt, source.Source);
            }
            finally
            {
                Assert.DoesNotThrow(() => source.Dispose());
            }
        }

        [Test]
        public void ResolveTransforms_MatchesExpectedValues(
            [Values(
                RenderTextureImageSource.TextureProcessingMode.None,
                RenderTextureImageSource.TextureProcessingMode.Orientation,
                RenderTextureImageSource.TextureProcessingMode.Color,
                RenderTextureImageSource.TextureProcessingMode.Auto)]
            RenderTextureImageSource.TextureProcessingMode mode,
            [Values(false, true)] bool backendNeedsFlip,
            [Values(false, true)] bool projectNeedsColorFix)
        {
            bool needsIntermediate = TextureFrameProvider.ResolveTransforms(
                mode, backendNeedsFlip, projectNeedsColorFix,
                out bool flip, out bool color);

            // Each flag is allowed by the mode, then gated by whether it's actually needed.
            bool allowFlip =
                mode == RenderTextureImageSource.TextureProcessingMode.Auto ||
                mode == RenderTextureImageSource.TextureProcessingMode.Orientation;
            bool allowColor =
                mode == RenderTextureImageSource.TextureProcessingMode.Auto ||
                mode == RenderTextureImageSource.TextureProcessingMode.Color;
            bool expectedFlip = allowFlip && backendNeedsFlip;
            bool expectedColor = allowColor && projectNeedsColorFix;

            Assert.AreEqual(expectedFlip, flip, "flip mismatch");
            Assert.AreEqual(expectedColor, color, "color mismatch");
            Assert.AreEqual(expectedFlip || expectedColor, needsIntermediate, "needsIntermediate mismatch");
        }

        [Test]
        public void DirectTextureFrameProvider_Acquire_AfterSourceDestroyed_ReturnsInvalid()
        {
            var rt = CreateTrackUnityRenderTexture();
            var source = new DirectTextureFrameProvider(rt);

            Object.DestroyImmediate(rt);

            NativeTextureFrame frame = source.Acquire();
            Assert.IsFalse(frame.IsValid);
            Assert.DoesNotThrow(() => source.Dispose());
        }

        [Test]
        public void DirectTextureFrameProvider_Acquire_AfterSourceReleased_ReturnsInvalid()
        {
            var rt = CreateTrackUnityRenderTexture();
            var source = new DirectTextureFrameProvider(rt);

            rt.Release();

            NativeTextureFrame frame = source.Acquire();
            Assert.IsFalse(frame.IsValid);
            Assert.DoesNotThrow(() => source.Dispose());
        }

        [Test]
        public void ProcessedTextureFrameProvider_Acquire_AfterSourceDestroyed_ReturnsInvalid()
        {
            var rt = CreateTrackUnityRenderTexture();
            var source = new ProcessedTextureFrameProvider(rt, flip: true, gammaEncode: true);

            // Prime the intermediate texture so we can verify it gets released.
            source.Acquire();

            Object.DestroyImmediate(rt);

            NativeTextureFrame frame = source.Acquire();
            Assert.IsFalse(frame.IsValid);
            Assert.DoesNotThrow(() => source.Dispose());
        }

        [Test]
        public void ProcessedTextureFrameProvider_Acquire_AfterSourceReleased_ReturnsInvalid()
        {
            var rt = CreateTrackUnityRenderTexture();
            var source = new ProcessedTextureFrameProvider(rt, flip: true, gammaEncode: true);

            source.Acquire();

            rt.Release();

            NativeTextureFrame frame = source.Acquire();
            Assert.IsFalse(frame.IsValid);
            Assert.DoesNotThrow(() => source.Dispose());
        }

        [Test]
        public void Image_WithDestroyedSource_DoesNotThrowOnRefresh()
        {
            var rt = CreateTrackUnityRenderTexture();
            var image = new RenderTextureImageSource(rt, RenderTextureImageSource.TextureProcessingMode.None,
                RenderTextureImageSource.RefreshMode.Manual);
            m_createdImages.Add(image);

            Object.DestroyImmediate(rt);

            Assert.DoesNotThrow(() => image.Refresh());
        }

        [Test]
        public void Refresh_AfterDispose_DoesNotThrow()
        {
            var image = CreateTrackRenderTextureImage();
            image.Dispose();

            Assert.DoesNotThrow(() => image.Refresh());
        }

        [UnityTest]
        public IEnumerator Bind_RegistersImageWithManager()
        {
            yield return LoadImagePropertyRoutine();

            var image = CreateTrackRenderTextureImage();
            m_imageProperty.SetFromRenderTextureImageSource(image);

            Assert.IsTrue(RenderTextureImageManager.HasAnyBindings);
            Assert.AreEqual(1, RenderTextureImageManager.Instance.BindingCount);
        }

        [UnityTest]
        public IEnumerator SetFromRenderTextureImage_Null_Unbinds()
        {
            yield return LoadImagePropertyRoutine();

            var image = CreateTrackRenderTextureImage();
            m_imageProperty.SetFromRenderTextureImageSource(image);
            Assert.IsTrue(RenderTextureImageManager.HasAnyBindings);

            m_imageProperty.SetFromRenderTextureImageSource(null);

            Assert.IsFalse(RenderTextureImageManager.HasAnyBindings);
            Assert.AreEqual(0, RenderTextureImageManager.Instance.BindingCount);
        }

        [UnityTest]
        public IEnumerator AssigningRegularImageAsset_UnbindsRenderTexture()
        {
            yield return LoadImagePropertyRoutine();

            var image = CreateTrackRenderTextureImage();
            m_imageProperty.SetFromRenderTextureImageSource(image);
            Assert.IsTrue(RenderTextureImageManager.HasAnyBindings);

            ImageOutOfBandAsset asset = null;
            yield return LoadImageAssetRoutine((loaded) => asset = loaded);
            asset.Load();
            try
            {
                m_imageProperty.Value = asset;

                Assert.IsFalse(RenderTextureImageManager.HasAnyBindings,
                    "Assigning a regular image asset should detach the active render-texture binding.");
                Assert.AreEqual(0, RenderTextureImageManager.Instance.BindingCount);
            }
            finally
            {
                asset.Unload();
            }
        }

        [UnityTest]
        public IEnumerator OwningViewModelDisposed_DropsBindingOnTick()
        {
            yield return LoadImagePropertyRoutine();

            var image = CreateTrackRenderTextureImage();
            m_imageProperty.SetFromRenderTextureImageSource(image);
            Assert.IsTrue(RenderTextureImageManager.HasAnyBindings);

            m_viewModelInstance.Dispose();
            RenderTextureImageManager.Instance.Tick(); // The tick should drop any now-unbound image.

            Assert.IsFalse(RenderTextureImageManager.HasAnyBindings,
                "A binding whose owning view model instance was disposed should be dropped on the next tick.");
            Assert.AreEqual(0, RenderTextureImageManager.Instance.BindingCount);
        }

        [UnityTest]
        public IEnumerator RebindingPropertyToNewImage_KeepsSingleBinding()
        {
            yield return LoadImagePropertyRoutine();

            var imageA = CreateTrackRenderTextureImage();
            var imageB = CreateTrackRenderTextureImage();

            m_imageProperty.SetFromRenderTextureImageSource(imageA);
            Assert.AreEqual(1, RenderTextureImageManager.Instance.BindingCount);

            m_imageProperty.SetFromRenderTextureImageSource(imageB);

            Assert.IsTrue(RenderTextureImageManager.HasAnyBindings);
            Assert.AreEqual(1, RenderTextureImageManager.Instance.BindingCount,
                "Rebinding the same property to a new image should not leave the old image bound.");
        }

        [UnityTest]
        public IEnumerator DisposingBoundImage_Unregisters()
        {
            yield return LoadImagePropertyRoutine();

            var image = CreateTrackRenderTextureImage();
            m_imageProperty.SetFromRenderTextureImageSource(image);
            Assert.IsTrue(RenderTextureImageManager.HasAnyBindings);

            image.Dispose();

            Assert.IsFalse(RenderTextureImageManager.HasAnyBindings);
            Assert.AreEqual(0, RenderTextureImageManager.Instance.BindingCount);
            Assert.IsTrue(image.IsDisposed);
        }

        [UnityTest]
        public IEnumerator Clear_DisposesBoundImagesAndEmptiesBindings()
        {
            yield return LoadImagePropertyRoutine();

            var image = CreateTrackRenderTextureImage();
            m_imageProperty.SetFromRenderTextureImageSource(image);
            Assert.IsTrue(RenderTextureImageManager.HasAnyBindings);

            RenderTextureImageManager.Instance.Clear();

            Assert.IsFalse(RenderTextureImageManager.HasAnyBindings);
            Assert.AreEqual(0, RenderTextureImageManager.Instance.BindingCount);
            Assert.IsTrue(image.IsDisposed, "Clear should dispose images that were still bound.");
        }

        // Magenta color not used by image_db_test artboard. If it appears in output, it came from the test texture.
        private static readonly Color32 MarkerColor = new Color32(255, 0, 255, 255);

        // Tests how quickly a bound render texture is shown. DrawWhenChanged panels only redraw when the artboard changes, which should happen when a new image binds.
        [UnityTest]
        public IEnumerator BoundImage_ReachesADrawWhenChangedPanel()
        {
            if (!TextureHelper.SupportsRenderTextureImageSource())
            {
                Assert.Ignore(
                    "RenderTextureImageSource is unsupported on " +
                    $"{SystemInfo.graphicsDeviceType}.");
            }

            // Camera is needed for render pipeline drawing. Without it, nothing will draw to the texture.
            m_cameraObject = new GameObject("Camera");
            m_cameraObject.AddComponent<Camera>().tag = "MainCamera";

            // Set up panel size and layout so the widget and panel match.
            m_panel.SetDimensions(new Vector2(256, 256));
            m_panel.DrawOptimization = DrawOptimizationOptions.DrawWhenChanged;
            RivePanelTestUtils.MakeWidgetFillPanel(m_widget);
            m_widget.Fit = Fit.Contain;

            yield return LoadImagePropertyRoutine();

            // Let the output settle so this test sees only changes from the new image.
            yield return WaitForRenderedFrames(4);

            Assert.IsNotNull(m_panel.RenderTexture, "The panel never rendered.");
            Assert.IsTrue(
                HasVisiblePixels(m_panel.RenderTexture),
                "The panel drew nothing, so we can't test image arrival.");
            Assert.IsFalse(
                HasMarkerColor(m_panel.RenderTexture),
                "Panel already shows marker color; can't detect new image.");

            RenderTextureImageSource image = new RenderTextureImageSource(
                CreateMarkerSource(),
                RenderTextureImageSource.TextureProcessingMode.Auto,
                RenderTextureImageSource.RefreshMode.PerFrame);
            m_createdImages.Add(image);

            m_imageProperty.SetFromRenderTextureImageSource(image);

            const int budget = 6;
            int frames = 0;
            while (frames < budget && !HasMarkerColor(m_panel.RenderTexture))
            {
                frames++;
                yield return WaitForRenderedFrames(1);
            }

            Assert.IsTrue(
                HasMarkerColor(m_panel.RenderTexture),
                $"Bound texture did not show up in {budget} frames. " +
                "Maybe not bound, or panel did not redraw.");

            // Log the number of frames it took for the test texture to appear.
            UnityEngine.Debug.Log(
                $"Bound render texture reached the panel after {frames} frame(s).");
        }

        // A camera, a panel the widget fills, and the image asset loaded and
        // drawn, with no marker on it yet.
        private IEnumerator SetUpPanelForPixels()
        {
            if (!TextureHelper.SupportsRenderTextureImageSource())
            {
                Assert.Ignore(
                    "RenderTextureImageSource is unsupported on " +
                    $"{SystemInfo.graphicsDeviceType}.");
            }

            m_cameraObject = new GameObject("Camera");
            m_cameraObject.AddComponent<Camera>().tag = "MainCamera";
            m_panel.SetDimensions(new Vector2(256, 256));
            RivePanelTestUtils.MakeWidgetFillPanel(m_widget);
            m_widget.Fit = Fit.Contain;

            yield return LoadImagePropertyRoutine();
            yield return WaitForRenderedFrames(4);

            Assert.IsNotNull(m_panel.RenderTexture, "The panel never rendered.");
            Assert.IsFalse(HasMarkerColor(m_panel.RenderTexture), "Marker already showing.");
        }

        private static IEnumerator WaitUntilDone(Future operation, int frames = 30)
        {
            for (int i = 0; i < frames && !operation.IsDone; i++)
            {
                yield return WaitForRenderedFrames(1);
            }
        }

        // None, because an Auto source's new intermediate gets its first blit
        // the same frame, and on Metal a panel's texture pass reaches the GPU
        // before Unity's blit does.
        [UnityTest]
        public IEnumerator BoundImage_ShowsOnTheFirstRenderedFrame()
        {
            yield return SetUpPanelForPixels();

            RenderTextureImageSource image = new RenderTextureImageSource(
                CreateMarkerSource(),
                RenderTextureImageSource.TextureProcessingMode.None,
                RenderTextureImageSource.RefreshMode.Manual);
            m_createdImages.Add(image);
            m_imageProperty.SetFromRenderTextureImageSource(image);

            yield return WaitForRenderedFrames(1);

            Assert.IsTrue(HasMarkerColor(m_panel.RenderTexture));
        }

        [UnityTest]
        public IEnumerator DisposeAsync_CompletesAndEmptiesTheProperty()
        {
            yield return SetUpPanelForPixels();

            RenderTextureImageSource image = new RenderTextureImageSource(
                CreateMarkerSource(),
                RenderTextureImageSource.TextureProcessingMode.None,
                RenderTextureImageSource.RefreshMode.PerFrame);
            m_createdImages.Add(image);
            m_imageProperty.SetFromRenderTextureImageSource(image);
            yield return WaitForRenderedFrames(2);
            Assert.IsTrue(HasMarkerColor(m_panel.RenderTexture), "The image should show first.");

            Future disposing = image.DisposeAsync();
            yield return WaitUntilDone(disposing);

            Assert.AreEqual(FutureStatus.Succeeded, disposing.Status);
            Assert.IsTrue(image.IsDisposed);
            Assert.IsFalse(RenderTextureImageManager.HasAnyBindings);
            Assert.IsFalse(HasMarkerColor(m_panel.RenderTexture));
        }

        [UnityTest]
        public IEnumerator Dispose_EmptiesTheProperty()
        {
            yield return SetUpPanelForPixels();

            RenderTextureImageSource image = new RenderTextureImageSource(
                CreateMarkerSource(),
                RenderTextureImageSource.TextureProcessingMode.None,
                RenderTextureImageSource.RefreshMode.PerFrame);
            m_createdImages.Add(image);
            m_imageProperty.SetFromRenderTextureImageSource(image);
            yield return WaitForRenderedFrames(2);
            Assert.IsTrue(HasMarkerColor(m_panel.RenderTexture), "The image should show first.");

            image.Dispose();
            yield return WaitForRenderedFrames(2);

            Assert.IsFalse(HasMarkerColor(m_panel.RenderTexture));
        }

        [UnityTest]
        public IEnumerator ResizedSource_ReleasesTheOldIntermediateOnceUnused()
        {
            yield return SetUpPanelForPixels();

            RenderTextureImageSource image = new RenderTextureImageSource(
                CreateMarkerSource(),
                RenderTextureImageSource.TextureProcessingMode.Auto,
                RenderTextureImageSource.RefreshMode.PerFrame);
            m_createdImages.Add(image);
            m_imageProperty.SetFromRenderTextureImageSource(image);
            yield return WaitForRenderedFrames(2);
            var provider = (ProcessedTextureFrameProvider)image.FrameProvider;
            RenderTexture oldIntermediate = provider.IntermediateForTests;
            Assert.IsNotNull(oldIntermediate);

            RenderTexture source = image.Source;
            source.Release();
            source.width = 32;
            source.height = 32;
            source.Create();
            yield return null;
            Assert.AreNotSame(oldIntermediate, provider.IntermediateForTests);
            Assert.IsTrue(oldIntermediate != null, "Still in use, so not released yet.");

            for (int i = 0; i < 30 && oldIntermediate != null; i++)
            {
                yield return WaitForRenderedFrames(1);
            }

            Assert.IsTrue(oldIntermediate == null, "Released once nothing used it.");
        }

        [UnityTest]
        public IEnumerator DisposeAsync_NeverBound_Completes()
        {
            RenderTextureImageSource image = CreateTrackRenderTextureImage();

            Future disposing = image.DisposeAsync();
            yield return WaitUntilDone(disposing);

            Assert.AreEqual(FutureStatus.Succeeded, disposing.Status);
        }

        [UnityTest]
        public IEnumerator DisposeAsync_Twice_GivesTheSameOperation()
        {
            yield return LoadImagePropertyRoutine();
            RenderTextureImageSource image = CreateTrackRenderTextureImage();
            m_imageProperty.SetFromRenderTextureImageSource(image);
            yield return WaitForRenderedFrames(1);

            Future first = image.DisposeAsync();
            Future second = image.DisposeAsync();
            yield return WaitUntilDone(first);

            Assert.IsTrue(second.IsDone);
            Assert.AreEqual(first.Status, second.Status);
        }

        [UnityTest]
        public IEnumerator DirectSource_ResizedWhileBound_Warns()
        {
            yield return LoadImagePropertyRoutine();
            RenderTextureImageSource image = CreateTrackRenderTextureImage();
            Assert.IsTrue(image.DrawsSourceDirectly);
            m_imageProperty.SetFromRenderTextureImageSource(image);
            yield return WaitForRenderedFrames(2);

            RenderTexture source = image.Source;
            source.Release();
            source.width = 32;
            source.height = 32;
            source.Create();
            yield return WaitForRenderedFrames(2);

            Assert.IsTrue(m_mockLogger.LoggedWarningsContains("resized"));
        }

        [UnityTest]
        public IEnumerator DirectSource_ReleasedWhileBound_Warns()
        {
            yield return LoadImagePropertyRoutine();
            RenderTextureImageSource image = CreateTrackRenderTextureImage();
            m_imageProperty.SetFromRenderTextureImageSource(image);
            yield return WaitForRenderedFrames(2);

            image.Source.Release();
            yield return WaitForRenderedFrames(2);

            Assert.IsTrue(m_mockLogger.LoggedWarningsContains("released or destroyed"));
        }

        [UnityTest]
        public IEnumerator DirectSource_ReleasedAfterDisposeAsync_DoesNotWarn()
        {
            yield return LoadImagePropertyRoutine();
            RenderTextureImageSource image = CreateTrackRenderTextureImage();
            m_imageProperty.SetFromRenderTextureImageSource(image);
            yield return WaitForRenderedFrames(2);

            RenderTexture source = image.Source;
            Future disposing = image.DisposeAsync();
            yield return WaitUntilDone(disposing);
            source.Release();
            yield return WaitForRenderedFrames(2);

            Assert.AreEqual(FutureStatus.Succeeded, disposing.Status);
            Assert.AreEqual(0, m_mockLogger.LoggedWarnings.Count);
        }

        private RenderTexture CreateMarkerSource(int size = 64)
        {
            var content = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = MarkerColor;
            }
            content.SetPixels32(pixels);
            content.Apply(false, false);

            RenderTexture source = CreateTrackUnityRenderTexture(size);
            Graphics.Blit(content, source);
            Object.Destroy(content);
            return source;
        }

        private static IEnumerator WaitForRenderedFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
            }
        }

        private static bool HasVisiblePixels(RenderTexture renderTexture)
        {
            return AnyPixel(renderTexture, pixel => pixel.a != 0);
        }

        // Checks for "magenta" within a tolerance, since color conversions may change the exact value.
        private static bool HasMarkerColor(RenderTexture renderTexture)
        {
            return AnyPixel(
                renderTexture,
                pixel => pixel.a > 128 && pixel.r > 150 && pixel.b > 150 && pixel.g < 100);
        }

        private static bool AnyPixel(
            RenderTexture renderTexture, Func<Color32, bool> predicate)
        {
            RenderTexture previous = RenderTexture.active;
            var readback = new Texture2D(
                renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = renderTexture;
                readback.ReadPixels(
                    new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                readback.Apply();

                foreach (Color32 pixel in readback.GetPixels32())
                {
                    if (predicate(pixel))
                    {
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(readback);
            }
        }
    }
}
#endif // RIVE_USING_EXPERIMENTAL
