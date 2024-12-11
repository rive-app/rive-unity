using Rive.Utils;
using UnityEditor;
using UnityEngine;

namespace Rive
{
    /// <summary>
    /// Custom editor for ImageOutOfBandAsset that displays a preview of the image.
    /// </summary>
    [CustomEditor(typeof(ImageOutOfBandAsset))]
    public class ImageOutOfBandAssetEditor : Editor
    {
        private enum PreviewMode
        {
            Contain = 0,
            Cover = 1
        }

        private PreviewMode previewMode = PreviewMode.Contain;
        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
        {
            var asset = (ImageOutOfBandAsset)target;

            if (asset == null || asset.Bytes == null || asset.Bytes.Length == 0)
            {
                return null;
            }

            Texture2D originalTexture = LoadOriginalTexture(asset.Bytes);
            if (originalTexture == null)
            {
                return null;
            }

            Vector2Int newSize = CalculateNewSize(originalTexture.width, originalTexture.height, width, height);
            Texture2D resizedTexture = ResizeTexture(originalTexture, newSize.x, newSize.y);
            Texture2D previewTexture = CreatePreviewTexture(resizedTexture, width, height);

            Object.DestroyImmediate(originalTexture);
            Object.DestroyImmediate(resizedTexture);

            return previewTexture;
        }

        private Texture2D LoadOriginalTexture(byte[] bytes)
        {
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(bytes))
            {
                return texture;
            }
            else
            {
                DebugLogger.Instance.LogWarning("Failed to load image preview for ImageOutOfBandAsset");
                return null;
            }
        }

        private Vector2Int CalculateNewSize(int originalWidth, int originalHeight, int targetWidth, int targetHeight)
        {
            float aspectRatio = (float)originalWidth / originalHeight;
            float targetAspectRatio = (float)targetWidth / targetHeight;

            switch (previewMode)
            {
                case PreviewMode.Contain:
                    if (targetAspectRatio > aspectRatio)
                    {
                        return new Vector2Int(
                            Mathf.RoundToInt(targetHeight * aspectRatio),
                            targetHeight
                        );
                    }
                    else
                    {
                        return new Vector2Int(
                            targetWidth,
                            Mathf.RoundToInt(targetWidth / aspectRatio)
                        );
                    }

                case PreviewMode.Cover:
                    if (targetAspectRatio > aspectRatio)
                    {
                        return new Vector2Int(
                            targetWidth,
                            Mathf.RoundToInt(targetWidth / aspectRatio)
                        );
                    }
                    else
                    {
                        return new Vector2Int(
                            Mathf.RoundToInt(targetHeight * aspectRatio),
                            targetHeight
                        );
                    }
                default:
                    DebugLogger.Instance.LogWarning($"Unsupported preview mode: {previewMode}. Falling back to Contain mode.");
                    goto case PreviewMode.Contain;
            }
        }

        private Texture2D ResizeTexture(Texture2D originalTexture, int newWidth, int newHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);

            // Set the filter mode to bilinear to match the previous scaling method
            rt.filterMode = FilterMode.Bilinear;

            RenderTexture.active = rt;
            Graphics.Blit(originalTexture, rt);
            Texture2D resizedTexture = new Texture2D(newWidth, newHeight);
            resizedTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            resizedTexture.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            return resizedTexture;
        }

        private Texture2D CreatePreviewTexture(Texture2D resizedTexture, int width, int height)
        {
            Texture2D previewTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            // Fill the background with transparency
            UnityEngine.Color[] fillPixels = new UnityEngine.Color[width * height];
            for (int i = 0; i < fillPixels.Length; i++)
                fillPixels[i] = UnityEngine.Color.clear;
            previewTexture.SetPixels(fillPixels);

            // Center the resized image
            int x = (width - resizedTexture.width) / 2;
            int y = (height - resizedTexture.height) / 2;

            // Copy the resized image to the center of the preview texture
            previewTexture.SetPixels32(x, y, resizedTexture.width, resizedTexture.height, resizedTexture.GetPixels32());
            previewTexture.Apply();

            return previewTexture;
        }

    }
}