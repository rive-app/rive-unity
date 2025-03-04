using UnityEngine;
using System.Collections.Generic;
using UnityEditor.AssetImporters;
using UnityEditor;
using System;
using System.Linq;
using Rive.Utils;

namespace Rive
{
    [ScriptedImporter(version: 2, ext: "riv")]
    public class AssetImporter : ScriptedImporter
    {

        [System.Serializable]
        private class OobReferenceData
        {
            /// <summary>
            /// ID of the referenced asset in Rive
            /// </summary>
            public uint assetIdInRiv;

            /// <summary>
            /// GUID of the referenced asset in Unity
            /// </summary>
            public string assetGuid;
        }

        [System.Serializable]
        private class UserDataContainer
        {
            /// <summary>
            /// List of stored OOB references to override OutOfBandAsset references on the generated Asset
            /// </summary>
            public List<OobReferenceData> oobReferences = new List<OobReferenceData>();


            /// <summary>
            /// Remove a stored OOB reference from the list
            /// </summary>
            /// <param name="assetId"> ID of the referenced asset in Rive </param>
            public void RemoveOobReference(uint assetId)
            {
                oobReferences.RemoveAll(r => r.assetIdInRiv == assetId);
            }

            /// <summary>
            /// Add a new OOB reference to the list
            /// </summary>
            /// <param name="assetId"> ID of the referenced asset in Rive </param>
            /// <param name="assetGuid"> GUID of the referenced asset in Unity </param>
            public void AddOobReference(uint assetId, string assetGuid)
            {

                // Remove any existing reference with the same ID
                RemoveOobReference(assetId);

                oobReferences.Add(new OobReferenceData
                {
                    assetIdInRiv = assetId,
                    assetGuid = assetGuid
                });
            }
        }

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
            var assetPath = ctx.assetPath;
            byte[] bytes = System.IO.File.ReadAllBytes(assetPath);

            // Deserialize stored references from userData
            // The Asset class is recreated on every import, so changes made to the Asset class will be lost on reimport. To get around this, we store reference information about oob asset overrides in the importer userData to keep them between imports. 
            // This lets us keep track of the custom assets provided by the user. The userData is stored in the meta file.
            var userDataContainer = string.IsNullOrEmpty(userData)
                ? new UserDataContainer()
                : JsonUtility.FromJson<UserDataContainer>(userData);

            int oobAssetCount = 0;

            List<EmbeddedAssetData> assets = new List<EmbeddedAssetData>();

            foreach (var embeddedAsset in embeddedAssetDataLoader.LoadEmbeddedAssetDataFromRiveFileBytes(bytes))
            {
                // Check if we have a stored reference
                var storedReference = userDataContainer.oobReferences
                    .FirstOrDefault(r => r.assetIdInRiv == embeddedAsset.Id);

                // If the asset at the index is not a referenced asset, then we clear the stored reference as we only want to store references to oob assets set to `Referenced`
                if (embeddedAsset.InBandBytesSize > 0)
                {
                    userDataContainer.RemoveOobReference(embeddedAsset.Id);

                    storedReference = null;
                }

                if (storedReference != null)
                {
                    var oobAssetPath = AssetDatabase.GUIDToAssetPath(storedReference.assetGuid);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        var referencedAsset = AssetDatabase.LoadAssetAtPath<OutOfBandAsset>(oobAssetPath);
                        if (referencedAsset != null)
                        {
                            embeddedAsset.OutOfBandAsset = referencedAsset;
                            ctx.DependsOnArtifact(oobAssetPath);
                        }
                    }
                }
                else
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

        internal static void SetOobAssetReference(Asset targetRiveAsset, uint assetId, OutOfBandAsset oobAsset)
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(targetRiveAsset)) as AssetImporter;

            if (importer == null)
            {
                DebugLogger.Instance.LogError("Could not get AssetImporter for target Rive asset");
                return;
            }

            string userDataToApply = importer.userData;


            var container = string.IsNullOrEmpty(userDataToApply)
                ? new UserDataContainer()
                : JsonUtility.FromJson<UserDataContainer>(userDataToApply);

            container.RemoveOobReference(assetId);

            // If the asset is null, we still want to store the reference as the user might have set it to null on purpose.
            // If we don't store it, the importer will try to auto-detect a matching asset in the directory on the next import.
            var path = oobAsset != null ? AssetDatabase.GetAssetPath(oobAsset) : null;
            var guid = path != null ? AssetDatabase.AssetPathToGUID(path) : null;

            container.AddOobReference(assetId, guid);

            // Clear out any items in the list that are no longer valid (e.g assetIdInRiv is no longer in the file)
            container.oobReferences.RemoveAll(r => targetRiveAsset.EmbeddedAssets.All(e => e.Id != r.assetIdInRiv));

            userDataToApply = JsonUtility.ToJson(container);

            importer.userData = userDataToApply;

            importer.SaveAndReimport();
        }


        private void HandleAsset(EmbeddedAssetData embeddedAsset, string basePath, string[] extensions, Type assetType, AssetImportContext ctx, ref int oobAssetCount)
        {
            // If this is a reimport and the asset already has a valid OutOfBandAsset, keep it
            if (embeddedAsset.OutOfBandAsset != null)
            {
                ctx.DependsOnArtifact(AssetDatabase.GetAssetPath(embeddedAsset.OutOfBandAsset));
                oobAssetCount++;
                return;
            }

            // Only try to auto-detect assets if there isn't one already assigned
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
