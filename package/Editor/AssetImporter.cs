using UnityEngine;
using UnityEditor.AssetImporters;
using UnityEditor;

namespace Rive
{
    [ScriptedImporter(version: 1, ext: "riv")]
    public class AssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            Rive.Asset file = ScriptableObject.CreateInstance<Rive.Asset>();
            file.bytes = System.IO.File.ReadAllBytes(ctx.assetPath);

            ctx.AddObjectToAsset("RIVE", file);
            ctx.SetMainObject(file);
            Texture2D icon = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/app.rive.rive-unity/Editor/Icons/rive.png", typeof(Texture2D));
            EditorGUIUtility.SetIconForObject(file, icon);
        }
    }
}