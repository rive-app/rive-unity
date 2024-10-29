using UnityEngine;
using UnityEditor.AssetImporters;

namespace Rive
{

    internal static class AudioOobAssetExtensions
    {
        public const string WAV = "wav";
        public const string MP3 = "mp3";
        public const string FLAC = "flac";

        public static readonly string[] AudioExtensions = new[] { WAV, MP3, FLAC };
    }

    [ScriptedImporter(1, null, new string[] { AudioOobAssetExtensions.WAV, AudioOobAssetExtensions.MP3, AudioOobAssetExtensions.FLAC })]
    public class AudioAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            byte[] bytesToAssign = System.IO.File.ReadAllBytes(ctx.assetPath);
            AudioOutOfBandAsset file = OutOfBandAsset.Create<AudioOutOfBandAsset>(bytesToAssign);

            ctx.AddObjectToAsset("rive-audio", file);
            ctx.SetMainObject(file);
        }
    }
}
