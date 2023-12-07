using UnityEngine;
using System.Collections.Generic;
using UnityEditor.AssetImporters;
using UnityEditor;
using System;
using System.Runtime.InteropServices;

namespace Rive
{
    [ScriptedImporter(2, null, new string[] { "ttf", "otf" })]
    public class FontAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            Rive.FontAsset file = ScriptableObject.CreateInstance<Rive.FontAsset>();
            file.bytes = System.IO.File.ReadAllBytes(ctx.assetPath);

            ctx.AddObjectToAsset("rive-font", file);
            ctx.SetMainObject(file);
        }
    }
}
