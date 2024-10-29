using UnityEngine;
using UnityEditor.AssetImporters;

namespace Rive
{

    internal static class FontOobAssetExtensions
    {
        public const string TTF = "ttf";
        public const string OTF = "otf";

        public static readonly string[] FontExtensions = new[] { TTF, OTF };
    }


    [ScriptedImporter(2, null, new string[] { FontOobAssetExtensions.TTF, FontOobAssetExtensions.OTF })]
    public class FontAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            byte[] bytesToAssign = System.IO.File.ReadAllBytes(ctx.assetPath);
            FontOutOfBandAsset file = OutOfBandAsset.Create<FontOutOfBandAsset>(bytesToAssign);

            ctx.AddObjectToAsset("rive-font", file);
            ctx.SetMainObject(file);
        }
    }
}
