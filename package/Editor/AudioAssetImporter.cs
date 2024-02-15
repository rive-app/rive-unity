using UnityEngine;
using UnityEditor.AssetImporters;

namespace Rive
{
    [ScriptedImporter(1, null, new string[] { "wav", "mp3", "flac" })]
    public class AudioAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            AudioAsset file = ScriptableObject.CreateInstance<AudioAsset>();
            file.bytes = System.IO.File.ReadAllBytes(ctx.assetPath);

            ctx.AddObjectToAsset("rive-audio", file);
            ctx.SetMainObject(file);
        }
    }
}
