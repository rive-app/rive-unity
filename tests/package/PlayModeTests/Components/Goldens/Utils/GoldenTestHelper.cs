#if RIVE_USING_GRAPHICS_TEST_FRAMEWORK
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools.Graphics;
using Rive.Utils;
using System.IO;

namespace Rive.Tests.Utils
{
    internal class GoldenTestHelper
    {
        public enum SavedImageFormatType
        {
            PNG = 0,
            JPG = 1
        }

        private readonly bool m_isCapturingGolden;
        private readonly Dictionary<string, Texture2D> m_goldenImages;
        private readonly Dictionary<string, Texture2D> m_capturedImages;
        private readonly string m_goldenImagesPath;
        private readonly TestAssetLoadingManager m_testAssetLoadingManager;
        private readonly int m_maxResolution;


        private readonly SavedImageFormatType m_savedImageFormat = SavedImageFormatType.PNG;

        public bool InCaptureMode => m_isCapturingGolden;

        public SavedImageFormatType SavedImageFormat => m_savedImageFormat;

        public string SavedExtension => GetExtension();

        /// <summary>
        /// Initialize a GoldenTestHelper instance.
        /// </summary>
        /// <param name="assetLoadingManager"> The asset loading manager to use. </param>
        /// <param name="referenceImagesPath"> The directory for the reference images. </param>
        /// <param name="captureGolden"> Whether to capture golden images. If true, the golden images will be saved to disk. </param>
        public GoldenTestHelper(TestAssetLoadingManager assetLoadingManager, string referenceImagesPath, bool captureGolden = false, int maxResolution = -1, SavedImageFormatType savedImageFormat = SavedImageFormatType.PNG)
        {
            m_testAssetLoadingManager = assetLoadingManager;
            m_isCapturingGolden = Application.isEditor ? captureGolden : false;
            m_goldenImages = new Dictionary<string, Texture2D>();
            m_capturedImages = new Dictionary<string, Texture2D>();
            m_goldenImagesPath = referenceImagesPath;
            m_maxResolution = maxResolution;
            m_savedImageFormat = savedImageFormat;

        }

        private string GetExtension()
        {
            return m_savedImageFormat == SavedImageFormatType.PNG ? "png" : "jpg";
        }
        private string GetGoldenImagePath(string testId)
        {
            string m_savedImageExtension = GetExtension();
            // Replace any backslashes with forward slashes to maintain Unity path format on Windows and Mac
            return System.IO.Path.Combine(m_goldenImagesPath, $"{testId}.{m_savedImageExtension}")
                .Replace('\\', '/');
        }

        private IEnumerator LoadGoldenImageIfNeeded(string testId)
        {
            if (m_isCapturingGolden)
            {
                yield break;
            }

            if (m_goldenImages.ContainsKey(testId))
            {
                // Check that the value is actually loaded
                if (m_goldenImages[testId] != null)
                {
                    yield break;
                }
            }

            yield return m_testAssetLoadingManager.LoadAssetCoroutine<Texture2D>(
                GetGoldenImagePath(testId),
                (texture) => m_goldenImages[testId] = texture,
                () => Assert.Fail($"Failed to load golden image at {testId}")
            );


        }

        public void RegisterGoldenImage(string testId, Texture2D reference)
        {
            if (m_goldenImages.ContainsKey(testId))
            {
                return;
            }
            m_goldenImages[testId] = reference;
        }

        public Texture2D GetCapturedImage(string testId)
        {
            return m_capturedImages.TryGetValue(testId, out var texture) ? texture : null;
        }

        private void SaveImageToDisk(string path, Texture2D texture)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));

            if (m_savedImageFormat == SavedImageFormatType.JPG)
            {
                System.IO.File.WriteAllBytes(path, texture.EncodeToJPG());
                return;
            }
            else if (m_savedImageFormat == SavedImageFormatType.PNG)
            {
                System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            }

        }

        private Texture2D ResizeIfNeeded(Texture2D source)
        {
            if (m_maxResolution <= 0 || (source.width <= m_maxResolution && source.height <= m_maxResolution))
            {
                return source;
            }

            float aspect = (float)source.width / source.height;
            int newWidth, newHeight;

            if (source.width > source.height)
            {
                newWidth = m_maxResolution;
                newHeight = Mathf.RoundToInt(m_maxResolution / aspect);
            }
            else
            {
                newHeight = m_maxResolution;
                newWidth = Mathf.RoundToInt(m_maxResolution * aspect);
            }

            var tempRT = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, tempRT);

            var resized = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
            var prevRT = RenderTexture.active;
            RenderTexture.active = tempRT;
            resized.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            resized.Apply();
            RenderTexture.active = prevRT;

            RenderTexture.ReleaseTemporary(tempRT);

            if (source != null && source != resized)
            {
                Object.Destroy(source);
            }
            return resized;
        }

        public IEnumerator AssertWithTexture2D(string testId, Texture2D actual, ImageComparisonSettings imageComparisonSettings = null)
        {
            if (m_isCapturingGolden)
            {
                var processedTexture = ConvertToRGBA32(actual);
                processedTexture = ResizeIfNeeded(processedTexture);
                m_capturedImages[testId] = processedTexture;

                string m_savedImageExtension = GetExtension();


                string path = System.IO.Path.Combine(m_goldenImagesPath, $"{testId}.{m_savedImageExtension}");
                SaveImageToDisk(path, processedTexture);

                yield break;
            }

            yield return LoadGoldenImageIfNeeded(testId);

            if (!m_goldenImages.TryGetValue(testId, out var goldenImage))
            {
                Assert.Fail($"No golden image registered for test: {testId}");
                yield break;
            }

            // Convert both textures to RGBA32 for comparison, otherwise the comparison will fail
            var rgba32Golden = ConvertToRGBA32(goldenImage);
            var rgba32Actual = ConvertToRGBA32(actual);

            if (imageComparisonSettings == null)
            {
                imageComparisonSettings = new ImageComparisonSettings
                {
                    AverageCorrectnessThreshold = 0.0154115018f

                };
            }



            ImageAssert.AreEqual(rgba32Golden, rgba32Actual, saveFailedImage: true, settings: imageComparisonSettings);

            // Cleanup temporary textures
            if (rgba32Golden != goldenImage)
            {
                Object.Destroy(rgba32Golden);
            }
            if (rgba32Actual != actual)
            {
                Object.Destroy(rgba32Actual);
            }
        }

        public IEnumerator AssertWithRenderTexture(string testId, RenderTexture renderTexture, ImageComparisonSettings imageComparisonSettings = null)
        {
            yield return LoadGoldenImageIfNeeded(testId);

            yield return new WaitForEndOfFrame();

            if (m_isCapturingGolden)
            {
                // Only do this if we haven't already captured the image
                if (m_capturedImages.ContainsKey(testId))
                {
                    yield break;
                }

                var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                texture.Apply();

                texture = ResizeIfNeeded(texture);
                m_capturedImages[testId] = texture;

                var savedImageExtension = GetExtension();

                string path = System.IO.Path.Combine(m_goldenImagesPath, $"{testId}.{savedImageExtension}");
                SaveImageToDisk(path, texture);

                yield break;
            }

            if (!m_goldenImages.TryGetValue(testId, out var goldenImage))
            {
                Assert.Fail($"No golden image registered for test: {testId}");
                yield break;
            }

            // Create a temporary RenderTexture with same dimensions as golden
            var tempRT = new RenderTexture(goldenImage.width, goldenImage.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(renderTexture, tempRT);

            // Create Texture2D and read pixels
            var texture2D = new Texture2D(goldenImage.width, goldenImage.height, TextureFormat.RGBA32, false);
            var prevRT = RenderTexture.active;
            RenderTexture.active = tempRT;
            texture2D.ReadPixels(new Rect(0, 0, goldenImage.width, goldenImage.height), 0, 0);
            texture2D.Apply();
            RenderTexture.active = prevRT;

            yield return AssertWithTexture2D(testId, texture2D, imageComparisonSettings);

            Object.Destroy(texture2D);
            Object.Destroy(tempRT);
        }

        public IEnumerator CaptureAndCompare(string testId, System.Action<Camera> setupCamera = null)
        {
            // Wait till endofframe to make sure everything is rendered
            yield return new WaitForEndOfFrame();

            // Setup a clean camera for capturing
            var cameraObj = new GameObject("TestCamera");
            var camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UnityEngine.Color.gray;
            setupCamera?.Invoke(camera);

            var screenshot = ScreenCapture.CaptureScreenshotAsTexture();

            yield return AssertWithTexture2D(testId, screenshot);

            Object.Destroy(screenshot);
            Object.Destroy(cameraObj);
        }

        /// <summary>
        /// Converts the source Texture2D to RGBA32 format if it is not already in that format.
        /// </summary>
        /// <param name="source"> The source Texture2D to convert. </param>
        /// <returns> A new Texture2D in RGBA32 format. </returns>
        private Texture2D ConvertToRGBA32(Texture2D source)
        {
            // If already RGBA32, return the source
            if (source.format == TextureFormat.RGBA32)
            {
                return source;
            }

            var tempRT = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32
            );

            Graphics.Blit(source, tempRT);

            var rgba32Texture = new Texture2D(
                source.width,
                source.height,
                TextureFormat.RGBA32,
                false
            );

            var prevRT = RenderTexture.active;
            RenderTexture.active = tempRT;
            rgba32Texture.ReadPixels(
                new Rect(0, 0, tempRT.width, tempRT.height),
                0,
                0
            );
            rgba32Texture.Apply();
            RenderTexture.active = prevRT;

            RenderTexture.ReleaseTemporary(tempRT);

            return rgba32Texture;
        }

        public void Cleanup()
        {
            foreach (var texture in m_capturedImages.Values)
            {
                if (texture != null)
                {
                    Object.Destroy(texture);
                }
            }
            m_capturedImages.Clear();
        }
    }
}
#endif