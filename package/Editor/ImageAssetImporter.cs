using UnityEngine;
using UnityEditor.AssetImporters;

namespace Rive
{
    internal static class ImageOobAssetExtensions
    {
        public const string PNG = "png";
        public const string JPG = "jpg";
        public const string JPEG = "jpeg";
        public const string WEBP = "webp";

        public static readonly string[] ImageExtensions = new[] { PNG, JPG, JPEG, WEBP };
    }

    [ScriptedImporter(2, null, new string[] { ImageOobAssetExtensions.PNG, ImageOobAssetExtensions.JPG, ImageOobAssetExtensions.JPEG, ImageOobAssetExtensions.WEBP })]
    public class ImageAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            byte[] bytesToAssign = System.IO.File.ReadAllBytes(ctx.assetPath);
            ImageOutOfBandAsset file = OutOfBandAsset.Create<ImageOutOfBandAsset>(bytesToAssign);

            ctx.AddObjectToAsset("rive-image", file);
            ctx.SetMainObject(file);
        }
    }
}
