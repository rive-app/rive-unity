using UnityEngine;
using System.Collections.Generic;
using UnityEditor.AssetImporters;
using UnityEditor;
using System;

namespace Rive
{
    [ScriptedImporter(version: 2, ext: "riv")]
    public class AssetImporter : ScriptedImporter
    {
        delegate void Callback();

        void AfterUpdate(Callback call)
        {
            void callback()
            {
                if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                {
                    call();
                    EditorApplication.update -= callback;
                }
            }

            EditorApplication.update += callback;
        }

        public static string[] fontExtensions = FontOobAssetExtensions.FontExtensions;
        public static string[] imageExtensions = ImageOobAssetExtensions.ImageExtensions;
        public static string[] audioExtensions = AudioOobAssetExtensions.AudioExtensions;

        private EmbeddedAssetDataLoader embeddedAssetDataLoader = new EmbeddedAssetDataLoader();


        public override void OnImportAsset(AssetImportContext ctx)
        {
            bool isFirstImport = importSettingsMissing;
            //Asset file = ScriptableObject.CreateInstance<Asset>();
            var assetPath = ctx.assetPath;
            byte[] bytes = System.IO.File.ReadAllBytes(assetPath);

            int oobAssetCount = 0;


            List<EmbeddedAssetData> assets = new List<EmbeddedAssetData>();

            foreach (var embeddedAsset in embeddedAssetDataLoader.LoadEmbeddedAssetDataFromRiveFileBytes(bytes))
            {
                var basePath = System.IO.Path.GetDirectoryName(assetPath);

                switch (embeddedAsset.AssetType)
                {
                    case EmbeddedAssetType.Font:
                        HandleAsset(embeddedAsset, basePath, fontExtensions, typeof(FontOutOfBandAsset), ctx, ref oobAssetCount);
                        break;
                    case EmbeddedAssetType.Image:
                        HandleAsset(embeddedAsset, basePath, imageExtensions, typeof(ImageOutOfBandAsset), ctx, ref oobAssetCount);
                        break;
                    case EmbeddedAssetType.Audio:
                        HandleAsset(embeddedAsset, basePath, audioExtensions, typeof(AudioOutOfBandAsset), ctx, ref oobAssetCount);
                        break;
                }

                assets.Add(embeddedAsset);
            }

            var file = Asset.Create(bytes, assets.ToArray());

            ctx.AddObjectToAsset("rive", file);

            if (oobAssetCount != 0 && isFirstImport)
            {
                // The file seems to have out of band assets, try to resolve
                // them. We only do this on first import so the user can go
                // manually change auto-detected OOB assets to use a different
                // importer if they want.
                AfterUpdate(() =>
                {
                    ImportOutOfBandAssets(assetPath);
                });
            }
            ctx.SetMainObject(file);

        }

        private void HandleAsset(EmbeddedAssetData embeddedAsset, string basePath, string[] extensions, Type assetType, AssetImportContext ctx, ref int oobAssetCount)
        {
            foreach (var path in AssetPaths(basePath, embeddedAsset.Name, embeddedAsset.Id, extensions))
            {
                if (System.IO.File.Exists(path))
                {
                    oobAssetCount++;
                    ctx.DependsOnArtifact(path);
                    if (!String.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                    {
                        var referencedAsset = AssetDatabase.LoadAssetAtPath(path, assetType);
                        if (referencedAsset != null)
                        {
                            embeddedAsset.OutOfBandAsset = referencedAsset as OutOfBandAsset;
                        }
                    }
                    break;
                }
            }
        }


        // Pre-import any out of band assets.

        private void ImportOutOfBandAssets(string assetPath)
        {
            var bytes = System.IO.File.ReadAllBytes(assetPath);
            var basePath = System.IO.Path.GetDirectoryName(assetPath);


            foreach (var embeddedAsset in embeddedAssetDataLoader.LoadEmbeddedAssetDataFromRiveFileBytes(bytes))
            {

                switch (embeddedAsset.AssetType)
                {
                    case EmbeddedAssetType.Font:
                        ImportAssetGeneric<FontAssetImporter>(embeddedAsset, basePath, fontExtensions);
                        break;
                    case EmbeddedAssetType.Image:
                        ImportAssetGeneric<ImageAssetImporter>(embeddedAsset, basePath, imageExtensions);
                        break;
                    case EmbeddedAssetType.Audio:
                        ImportAssetGeneric<AudioAssetImporter>(embeddedAsset, basePath, audioExtensions);
                        break;
                }
            }


        }

        private void ImportAssetGeneric<T>(EmbeddedAssetData embeddedAsset, string basePath, string[] extensions) where T : ScriptedImporter
        {
            foreach (var path in AssetPaths(basePath, embeddedAsset.Name, embeddedAsset.Id, extensions))
            {
                if (System.IO.File.Exists(path) && AssetDatabase.GetImporterOverride(path) == null)
                {
                    AssetDatabase.SetImporterOverride<T>(path);
                    AssetDatabase.ImportAsset(path);
                    break;
                }
            }
        }

        string[] AssetPaths(string basePath, string name, uint id, string[] extensions)
        {
            List<string> paths = new List<string>();
            foreach (var extension in extensions)
            {
                paths.Add(
                    basePath
                        + System.IO.Path.DirectorySeparatorChar
                        + System.IO.Path.GetFileNameWithoutExtension(name)
                        + "-"
                        + id
                        + "."
                        + extension
                );
                paths.Add(
                    basePath
                        + System.IO.Path.DirectorySeparatorChar
                        + System.IO.Path.GetFileNameWithoutExtension(name)
                        + "."
                        + extension
                );
            }
            return paths.ToArray();
        }
    }
}
