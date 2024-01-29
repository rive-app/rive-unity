using UnityEngine;
using UnityEditor.AssetImporters;

namespace Rive
{
    [ScriptedImporter(2, null, new string[] { "png" })]
    public class ImageAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            ImageAsset file = ScriptableObject.CreateInstance<ImageAsset>();
            file.bytes = System.IO.File.ReadAllBytes(ctx.assetPath);

            ctx.AddObjectToAsset("rive-image", file);
            ctx.SetMainObject(file);
        }
    }
}
