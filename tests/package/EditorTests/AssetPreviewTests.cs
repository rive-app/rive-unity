using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rive.Tests.EditorTests
{
    /// <summary>
    /// The Project window thumbnail for a .riv comes from one static preview
    /// render, and Unity caches whatever it returns.
    /// </summary>
    public class AssetPreviewTests
    {
        private const string AssetPath = "Packages/app.rive.rive-unity.tests/Shared/Assets/robo_dude.riv";
        private const int Size = 128;

        [Test]
        public void StaticPreview_DrawsTheArtboard()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Asset>(AssetPath);
            Assert.IsNotNull(asset, $"Couldn't load {AssetPath}.");

            Editor editor = Editor.CreateEditor(asset);
            Texture2D preview = null;
            try
            {
                preview = editor.RenderStaticPreview(AssetPath, null, Size, Size);
                Assert.IsNotNull(preview, "The static preview should render.");

                int drawn = 0;
                foreach (Color32 pixel in preview.GetPixels32())
                {
                    if (pixel.a > 0 && (pixel.r | pixel.g | pixel.b) != 0)
                    {
                        drawn++;
                    }
                }
                Assert.Greater(drawn, Size * Size / 100, "The thumbnail should show the artboard, not a blank frame.");
            }
            finally
            {
                Object.DestroyImmediate(editor);
                if (preview != null)
                {
                    Object.DestroyImmediate(preview);
                }
            }
        }
    }
}
