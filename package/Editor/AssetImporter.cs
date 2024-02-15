using UnityEngine;
using System.Collections.Generic;
using UnityEditor.AssetImporters;
using UnityEditor;
using System;
using System.Runtime.InteropServices;

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

        public static string[] fontExtensions = new string[] { "ttf", "otf" };
        public static string[] imageExtensions = new string[] { "png" };
        public static string[] audioExtensions = new string[] { "wav", "mp3", "flac" };

        public override void OnImportAsset(AssetImportContext ctx)
        {
            bool isFirstImport = importSettingsMissing;
            Asset file = ScriptableObject.CreateInstance<Asset>();
            var assetPath = ctx.assetPath;
            file.bytes = System.IO.File.ReadAllBytes(assetPath);

            var listPtr = loadEmbeddedAssetList(file.bytes, (nuint)file.bytes.Length);

            // Load the file and get the assets from it.
            var assets = new List<EmbeddedAsset>();
            int oobAssetCount = 0;

            var assetCount = getEmbeddedAssetCount(listPtr);
            for (nuint i = 0; i < assetCount; i++)
            {
                var embeddedAsset = new EmbeddedAsset();
                embeddedAsset.name = Marshal.PtrToStringAnsi(getEmbeddedAssetName(listPtr, i));
                var type = getEmbeddedAssetType(listPtr, i);
                embeddedAsset.type = Enum.IsDefined(typeof(EmbeddedAssetType), type)
                    ? (EmbeddedAssetType)type
                    : EmbeddedAssetType.unknown;
                embeddedAsset.id = getEmbeddedAssetId(listPtr, i);
                embeddedAsset.embeddedBytes = (uint)getEmbeddedAssetSize(listPtr, i);

                assets.Add(embeddedAsset);
                var basePath = System.IO.Path.GetDirectoryName(assetPath);

                switch (embeddedAsset.type)
                {
                    case EmbeddedAssetType.font:
                        foreach (
                            var path in AssetPaths(
                                basePath,
                                embeddedAsset.name,
                                embeddedAsset.id,
                                AssetImporter.fontExtensions
                            )
                        )
                        {
                            if (System.IO.File.Exists(path))
                            {
                                oobAssetCount++;
                                // We depend on the image asset so any time it
                                // re-importer/settings change this .riv asset
                                // re-imports too.
                                ctx.DependsOnArtifact(path);
                                if (!String.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                                {
                                    // If the FontAsset doesn't exit, make sure we import it before our Rive asset imports.
                                    FontAsset referencedAsset =
                                        AssetDatabase.LoadAssetAtPath<FontAsset>(path);
                                    if (referencedAsset != null)
                                    {
                                        embeddedAsset.asset = referencedAsset;
                                    }
                                }
                                break;
                            }
                        }
                        break;
                    case EmbeddedAssetType.image:
                        foreach (
                            var path in AssetPaths(
                                basePath,
                                embeddedAsset.name,
                                embeddedAsset.id,
                                AssetImporter.imageExtensions
                            )
                        )
                        {
                            if (System.IO.File.Exists(path))
                            {
                                oobAssetCount++;
                                // We depend on the image asset so any time it
                                // re-importer/settings change this .riv asset
                                // re-imports too.
                                ctx.DependsOnArtifact(path);
                                if (!String.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                                {
                                    // If the ImageAsset doesn't exit, make sure we import it before our Rive asset imports.
                                    ImageAsset referencedAsset =
                                        AssetDatabase.LoadAssetAtPath<ImageAsset>(path);
                                    if (referencedAsset != null)
                                    {
                                        embeddedAsset.asset = referencedAsset;
                                    }
                                }
                                break;
                            }
                        }
                        break;
                    case EmbeddedAssetType.audio:
                        foreach (
                            var path in AssetPaths(
                                basePath,
                                embeddedAsset.name,
                                embeddedAsset.id,
                                AssetImporter.audioExtensions
                            )
                        )
                        {
                            if (System.IO.File.Exists(path))
                            {
                                oobAssetCount++;
                                // We depend on the image asset so any time it
                                // re-importer/settings change this .riv asset
                                // re-imports too.
                                ctx.DependsOnArtifact(path);
                                if (!String.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                                {
                                    // If the AudioAsset doesn't exit, make sure we import it before our Rive asset imports.
                                    AudioAsset referencedAsset =
                                        AssetDatabase.LoadAssetAtPath<AudioAsset>(path);
                                    if (referencedAsset != null)
                                    {
                                        embeddedAsset.asset = referencedAsset;
                                    }
                                }
                                break;
                            }
                        }
                        break;
                }
            }
            file.assets = assets.ToArray();
            deleteEmbeddedAssetList(listPtr);

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

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr loadEmbeddedAssetList(byte[] bytes, nuint byteCount);

        [DllImport(NativeLibrary.name)]
        internal static extern void deleteEmbeddedAssetList(IntPtr list);

        [DllImport(NativeLibrary.name)]
        internal static extern nuint getEmbeddedAssetCount(IntPtr list);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getEmbeddedAssetName(IntPtr riveFile, nuint index);

        [DllImport(NativeLibrary.name)]
        internal static extern ushort getEmbeddedAssetType(IntPtr riveFile, nuint index);

        [DllImport(NativeLibrary.name)]
        internal static extern uint getEmbeddedAssetId(IntPtr riveFile, nuint index);

        [DllImport(NativeLibrary.name)]
        internal static extern nuint getEmbeddedAssetSize(IntPtr riveFile, nuint index);
        #endregion


        // Pre-import any out of band assets.
        private void ImportOutOfBandAssets(string assetPath)
        {
            var bytes = System.IO.File.ReadAllBytes(assetPath);

            var listPtr = AssetImporter.loadEmbeddedAssetList(bytes, (nuint)bytes.Length);
            var assetCount = AssetImporter.getEmbeddedAssetCount(listPtr);
            for (nuint i = 0; i < assetCount; i++)
            {
                var name = Marshal.PtrToStringAnsi(AssetImporter.getEmbeddedAssetName(listPtr, i));
                var type = AssetImporter.getEmbeddedAssetType(listPtr, i);
                var assetType = Enum.IsDefined(typeof(EmbeddedAssetType), type)
                    ? (EmbeddedAssetType)type
                    : EmbeddedAssetType.unknown;
                var id = AssetImporter.getEmbeddedAssetId(listPtr, i);
                var basePath = System.IO.Path.GetDirectoryName(assetPath);
                switch (assetType)
                {
                    case EmbeddedAssetType.font:
                        foreach (
                            var path in AssetPaths(basePath, name, id, AssetImporter.fontExtensions)
                        )
                        {
                            if (
                                System.IO.File.Exists(path)
                                && AssetDatabase.GetImporterOverride(path) == null
                            )
                            {
                                AssetDatabase.SetImporterOverride<FontAssetImporter>(path);
                                AssetDatabase.ImportAsset(path);
                                break;
                            }
                        }
                        break;
                    case EmbeddedAssetType.image:
                        foreach (
                            var path in AssetPaths(
                                basePath,
                                name,
                                id,
                                AssetImporter.imageExtensions
                            )
                        )
                        {
                            if (
                                System.IO.File.Exists(path)
                                && AssetDatabase.GetImporterOverride(path) == null
                            )
                            {
                                AssetDatabase.SetImporterOverride<ImageAssetImporter>(path);
                                AssetDatabase.ImportAsset(path);
                                break;
                            }
                        }
                        break;
                    case EmbeddedAssetType.audio:
                        foreach (
                            var path in AssetPaths(
                                basePath,
                                name,
                                id,
                                AssetImporter.audioExtensions
                            )
                        )
                        {
                            if (
                                System.IO.File.Exists(path)
                                && AssetDatabase.GetImporterOverride(path) == null
                            )
                            {
                                AssetDatabase.SetImporterOverride<AudioAssetImporter>(path);
                                AssetDatabase.ImportAsset(path);
                                break;
                            }
                        }
                        break;
                }
            }
            AssetImporter.deleteEmbeddedAssetList(listPtr);
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
