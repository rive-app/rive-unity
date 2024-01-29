using UnityEngine;
using UnityEditor.AssetImporters;

namespace Rive
{
    [ScriptedImporter(2, null, new string[] { "ttf", "otf" })]
    public class FontAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            FontAsset file = ScriptableObject.CreateInstance<FontAsset>();
            file.bytes = System.IO.File.ReadAllBytes(ctx.assetPath);

            ctx.AddObjectToAsset("rive-font", file);
            ctx.SetMainObject(file);
        }
    }
}
