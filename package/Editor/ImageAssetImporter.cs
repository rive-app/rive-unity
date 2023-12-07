using UnityEngine;
using System.Collections.Generic;
using UnityEditor.AssetImporters;
using UnityEditor;
using System;
using System.Runtime.InteropServices;

namespace Rive
{
    [ScriptedImporter(2, null, new string[] { "png" })]
    public class ImageAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            Rive.ImageAsset file = ScriptableObject.CreateInstance<Rive.ImageAsset>();
            file.bytes = System.IO.File.ReadAllBytes(ctx.assetPath);

            ctx.AddObjectToAsset("rive-image", file);
            ctx.SetMainObject(file);
        }
    }
}
