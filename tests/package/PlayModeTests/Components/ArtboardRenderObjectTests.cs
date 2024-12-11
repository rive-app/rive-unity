using NUnit.Framework;
using UnityEngine;
using Rive.Components;
using Rive.Tests.Utils;
using System.Collections;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    public class ArtboardRenderObjectTests
    {
        private Artboard m_artboard;
        private File m_file;
        private MockRenderer m_renderer;
        private ArtboardRenderObject m_renderObject;
        protected TestAssetLoadingManager m_testAssetLoadingManager;

        [SetUp]
        public void Setup()
        {
            m_testAssetLoadingManager = new TestAssetLoadingManager();

            m_renderer = new MockRenderer();
        }

        [TearDown]
        public void TearDown()
        {
            m_file?.Dispose();
            m_testAssetLoadingManager.UnloadAllAssets();
        }

        private IEnumerator CreateArtboard(float width, float height)
        {
            string assetAddressablePath = TestAssetReferences.riv_sophiaHud;
            Asset riveAsset = null;

            yield return m_testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                assetAddressablePath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {assetAddressablePath}")
            );

            // Load the file directly instead of through the asset
            m_file = File.Load(riveAsset);

            // Create the artboard
            m_artboard = m_file.Artboard(0);

            // Set the artboard size
            m_artboard.Width = width;
            m_artboard.Height = height;

        }

        [Test]
        public void NeedsClipping_Fill_NeverClips()
        {
            // Test various size combinations
            Vector2 artboardSize = new Vector2(100, 100);
            Vector2[] frameSizes = new Vector2[]
            {
                new Vector2(50, 50),    // Smaller frame
                new Vector2(100, 100),  // Same size
                new Vector2(200, 200),  // Larger frame
                new Vector2(100, 200),  // Different aspect ratio
                new Vector2(200, 100)   // Different aspect ratio
            };

            foreach (var frameSize in frameSizes)
            {
                Assert.IsFalse(
                    ArtboardRenderObject.NeedsClipping(Fit.Fill, artboardSize, frameSize),
                    $"Fill should never need clipping (frame: {frameSize})"
                );
            }
        }

        [Test]
        public void NeedsClipping_Contain_NeverClips()
        {
            Vector2 artboardSize = new Vector2(100, 100);
            Vector2[] frameSizes = new Vector2[]
            {
                new Vector2(50, 50),
                new Vector2(100, 100),
                new Vector2(200, 200),
                new Vector2(100, 200),
                new Vector2(200, 100)
            };

            foreach (var frameSize in frameSizes)
            {
                Assert.IsFalse(
                    ArtboardRenderObject.NeedsClipping(Fit.Contain, artboardSize, frameSize),
                    $"Contain should never need clipping (frame: {frameSize})"
                );
            }
        }

        [Test]
        public void NeedsClipping_Cover_ClipsWhenAspectRatiosDiffer()
        {
            Vector2 artboardSize = new Vector2(100, 100);

            // Same aspect ratio - no clipping
            Assert.IsFalse(
                ArtboardRenderObject.NeedsClipping(
                    Fit.Cover,
                    artboardSize,
                    new Vector2(200, 200)
                ),
                "Cover should not clip when aspect ratios match"
            );

            // Different aspect ratios - needs clipping
            Assert.IsTrue(
                ArtboardRenderObject.NeedsClipping(
                    Fit.Cover,
                    artboardSize,
                    new Vector2(200, 100)
                ),
                "Cover should clip when aspect ratios differ"
            );
        }

        [Test]
        public void NeedsClipping_FitWidth_ClipsWhenHeightOverflows()
        {
            Vector2 artboardSize = new Vector2(100, 100);

            // Height fits - no clipping
            Assert.IsFalse(
                ArtboardRenderObject.NeedsClipping(
                    Fit.FitWidth,
                    artboardSize,
                    new Vector2(200, 200)
                ),
                "FitWidth should not clip when height fits"
            );

            // Height overflows - needs clipping
            Assert.IsTrue(
                ArtboardRenderObject.NeedsClipping(
                    Fit.FitWidth,
                    artboardSize,
                    new Vector2(200, 150)
                ),
                "FitWidth should clip when height overflows"
            );
        }

        [Test]
        public void NeedsClipping_FitHeight_ClipsWhenWidthOverflows()
        {
            Vector2 artboardSize = new Vector2(100, 100);

            // Width fits - no clipping
            Assert.IsFalse(
                ArtboardRenderObject.NeedsClipping(
                    Fit.FitHeight,
                    artboardSize,
                    new Vector2(200, 200)
                ),
                "FitHeight should not clip when width fits"
            );

            // Width overflows - needs clipping
            Assert.IsTrue(
                ArtboardRenderObject.NeedsClipping(
                    Fit.FitHeight,
                    artboardSize,
                    new Vector2(150, 200)
                ),
                "FitHeight should clip when width overflows"
            );
        }

        [Test]
        public void NeedsClipping_None_ClipsWhenOverflows()
        {
            Vector2 artboardSize = new Vector2(100, 100);

            // No overflow - no clipping
            Assert.IsFalse(
                ArtboardRenderObject.NeedsClipping(
                    Fit.None,
                    artboardSize,
                    new Vector2(200, 200)
                ),
                "None should not clip when content fits"
            );

            // Width overflow - needs clipping
            Assert.IsTrue(
                ArtboardRenderObject.NeedsClipping(
                    Fit.None,
                    artboardSize,
                    new Vector2(50, 200)
                ),
                "None should clip when width overflows"
            );

            // Height overflow - needs clipping
            Assert.IsTrue(
                ArtboardRenderObject.NeedsClipping(
                    Fit.None,
                    artboardSize,
                    new Vector2(200, 50)
                ),
                "None should clip when height overflows"
            );
        }

        [Test]
        public void NeedsClipping_ScaleDown_ClipsWhenOverflows()
        {
            Vector2 artboardSize = new Vector2(100, 100);

            // No overflow - no clipping
            Assert.IsFalse(
                ArtboardRenderObject.NeedsClipping(
                    Fit.ScaleDown,
                    artboardSize,
                    new Vector2(200, 200)
                ),
                "ScaleDown should not clip when content fits"
            );

            // Overflow - needs clipping
            Assert.IsTrue(
                ArtboardRenderObject.NeedsClipping(
                    Fit.ScaleDown,
                    artboardSize,
                    new Vector2(50, 50)
                ),
                "ScaleDown should clip when content overflows"
            );
        }

        [Test]
        public void NeedsClipping_Layout_NeverClips()
        {
            Vector2 artboardSize = new Vector2(100, 100);
            Vector2[] frameSizes = new Vector2[]
            {
                new Vector2(50, 50),
                new Vector2(100, 100),
                new Vector2(200, 200),
                new Vector2(100, 200),
                new Vector2(200, 100)
            };

            foreach (var frameSize in frameSizes)
            {
                Assert.IsFalse(
                    ArtboardRenderObject.NeedsClipping(Fit.Layout, artboardSize, frameSize),
                    $"Layout should never need clipping (frame: {frameSize})"
                );
            }
        }

        [UnityTest]
        public IEnumerator DrawContent_AppliesClippingWhenNeeded()
        {
            yield return CreateArtboard(100, 100);

            m_renderObject = new ArtboardRenderObject(m_artboard, Alignment.Center, Fit.None, 1.0f);

            // Create frame smaller than artboard to force clipping
            var frame = new AABB(0, 0, 50, 50);
            var renderContext = new RenderContext(RenderContext.ClippingModeSetting.CheckClipping);

            // Draw content
            m_renderObject.DrawContent(m_renderer, frame, renderContext);

            // Verify clipping was applied
            Assert.IsTrue(m_renderer.ClipWasCalled, "Clip should be called when needed");
            Assert.IsNotNull(m_renderer.LastClipPath, "Clip path should be created");
        }

        [UnityTest]
        public IEnumerator DrawContent_SkipsClippingWhenNotNeeded()
        {
            yield return CreateArtboard(100, 100);

            // Create render object that won't need clipping
            m_renderObject = new ArtboardRenderObject(m_artboard, Alignment.Center, Fit.Contain, 1.0f);

            // Create frame larger than artboard
            var frame = new AABB(0, 0, 200, 200);
            var renderContext = new RenderContext(RenderContext.ClippingModeSetting.CheckClipping);

            m_renderObject.DrawContent(m_renderer, frame, renderContext);

            // Verify clipping was not applied
            Assert.IsFalse(m_renderer.ClipWasCalled, "Clip should not be called when not needed");
            Assert.IsNull(m_renderer.LastClipPath, "Clip path should not be created");
        }

        public class MockRenderer : IRenderer
        {



            public bool ClipWasCalled { get; private set; }
            public Path LastClipPath { get; private set; }

            public void Clip(Path path)
            {
                ClipWasCalled = true;
                LastClipPath = path;
            }

            public void Draw(Artboard artboard) { }
            public void Align(Fit fit, Alignment alignment, Artboard artboard, AABB frame, float scale = 1) { }
            public void Clear() { }

            public void Draw(Path path, Paint paint)
            {
            }

            public void Save()
            {
            }

            public void Restore()
            {
            }

            public void Translate(System.Numerics.Vector2 translation)
            {
            }

            public void Translate(float x, float y)
            {
            }

            public void Transform(System.Numerics.Matrix3x2 matrix)
            {
            }

            public void Align(Fit fit, Alignment alignment, Artboard artboard, float scaleFactor = 1)
            {
            }

            public void Submit()
            {
            }

            public void SubmitAndRelease()
            {
            }

            public void AddToCommandBuffer(UnityEngine.Rendering.CommandBuffer commandBuffer, bool release = false)
            {
            }
        }
    }



}