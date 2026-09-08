using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rive.Tests.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using RiveAssetImporter = Rive.AssetImporter;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace Rive.Tests.EditorTests
{
    /// <summary>
    /// Covers restoring out-of-band image references from importer userData
    /// on .riv import. Uses copies under Assets/ so we can control import
    /// order and importer overrides.
    /// </summary>
    [Timeout(60000)]
    public class AssetImporterOobReferenceTests
    {
        private const string TempRoot = "Assets/RiveOobImportTests";
        private const uint StormtrooperImageId = 266093;
        private const string StormtrooperImageFileName = "michael-myers-trooperbird-266093.png";
        private const float ImportTimeoutSeconds = 20f;

        private string m_testFolder;

        [SetUp]
        public void SetUp()
        {
            m_testFolder = $"{TempRoot}/{TestContext.CurrentContext.Test.Name}";
            EnsureFolder(m_testFolder);
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            // AfterUpdate on the .riv importer is deferred to EditorApplication.update.
            // Wait it out before deleting temp files or those callbacks spam missing-path errors.
            yield return WaitForImports();

            if (AssetDatabase.IsValidFolder(TempRoot))
            {
                AssetDatabase.DeleteAsset(TempRoot);
            }

            AssetDatabase.Refresh();
            yield return WaitForImports();
        }

        [UnityTest]
        public IEnumerator FirstImport_AutoDetectsReferencedPng_AndAppliesImageImporter()
        {
            var rivPath = CopyWithoutMeta(TestAssetReferences.riv_stormtrooper_bird, $"{m_testFolder}/stormtrooper_bird.riv");
            var pngPath = CopyWithoutMeta(TestAssetReferences.imageasset_stormtrooper_bird, $"{m_testFolder}/{StormtrooperImageFileName}");

            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(rivPath, ImportAssetOptions.ForceUpdate);

            yield return WaitUntil(
                () => HasAssignedStormtrooperImage(rivPath),
                "First import should auto-detect the matching PNG and assign an ImageOutOfBandAsset.");

            Assert.IsNotNull(AssetDatabase.GetImporterOverride(pngPath),
                "Matching PNG should use ImageAssetImporter after first import.");
        }

        [UnityTest]
        public IEnumerator StoredUserDataGuid_SurvivesReimport_WhenImageAlreadyImported()
        {
            var pngPath = CopyWithoutMeta(TestAssetReferences.imageasset_stormtrooper_bird, $"{m_testFolder}/{StormtrooperImageFileName}");
            yield return ImportAsRiveImage(pngPath);

            var rivPath = CopyWithoutMeta(TestAssetReferences.riv_stormtrooper_bird, $"{m_testFolder}/stormtrooper_bird.riv");
            AssetDatabase.ImportAsset(rivPath, ImportAssetOptions.ForceUpdate);
            yield return WaitForImports();

            var image = AssetDatabase.LoadAssetAtPath<ImageOutOfBandAsset>(pngPath);
            Assert.IsNotNull(image);

            var riveAsset = AssetDatabase.LoadAssetAtPath<Asset>(rivPath);
            RiveAssetImporter.SetOobAssetReference(riveAsset, StormtrooperImageId, image);
            yield return WaitForImports();

            AssetDatabase.ImportAsset(rivPath, ImportAssetOptions.ForceUpdate);
            yield return WaitForImports();

            var assigned = LoadReferencedImage(rivPath).OutOfBandAsset as ImageOutOfBandAsset;
            Assert.AreEqual(image, assigned);

            var importer = UnityEditor.AssetImporter.GetAtPath(rivPath);
            StringAssert.Contains(AssetDatabase.AssetPathToGUID(pngPath), importer.userData);
        }

        [UnityTest]
        public IEnumerator StoredUserDataGuid_ReimportsRiv_WhenMissingImageReappears()
        {
            var pngPath = CopyWithoutMeta(TestAssetReferences.imageasset_stormtrooper_bird, $"{m_testFolder}/{StormtrooperImageFileName}");
            yield return ImportAsRiveImage(pngPath);

            var rivPath = CopyWithoutMeta(TestAssetReferences.riv_stormtrooper_bird, $"{m_testFolder}/stormtrooper_bird.riv");
            AssetDatabase.ImportAsset(rivPath, ImportAssetOptions.ForceUpdate);
            yield return WaitForImports();

            var image = AssetDatabase.LoadAssetAtPath<ImageOutOfBandAsset>(pngPath);
            RiveAssetImporter.SetOobAssetReference(AssetDatabase.LoadAssetAtPath<Asset>(rivPath), StormtrooperImageId, image);
            yield return WaitForImports();

            AssetDatabase.SaveAssets();
            var pngBytes = IOFile.ReadAllBytes(pngPath);
            var pngMeta = IOFile.ReadAllText(pngPath + ".meta");

            AssetDatabase.DeleteAsset(pngPath);
            AssetDatabase.ImportAsset(rivPath, ImportAssetOptions.ForceUpdate);
            yield return WaitForImports();

            Assert.IsNull(LoadReferencedImage(rivPath).OutOfBandAsset,
                "Precondition: deleting the PNG should drop the object reference on the .riv.");

            // Restore the PNG only. Do not ImportAsset the .riv, the regression is
            // that Unity never retriggers it once the GUID artifact reappears.
            IOFile.WriteAllBytes(pngPath, pngBytes);
            IOFile.WriteAllText(pngPath + ".meta", pngMeta);
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

            yield return WaitUntil(
                () => HasAssignedStormtrooperImage(rivPath),
                "Restoring a PNG whose GUID is stored in .riv userData should reimport the .riv and reattach the OOB reference.");
        }

        [UnityTest]
        public IEnumerator ClearedUserDataGuid_DoesNotAutoDetectMatchingPng()
        {
            var pngPath = CopyWithoutMeta(TestAssetReferences.imageasset_stormtrooper_bird, $"{m_testFolder}/{StormtrooperImageFileName}");
            yield return ImportAsRiveImage(pngPath);

            var rivPath = CopyWithoutMeta(TestAssetReferences.riv_stormtrooper_bird, $"{m_testFolder}/stormtrooper_bird.riv");
            AssetDatabase.ImportAsset(rivPath, ImportAssetOptions.ForceUpdate);
            yield return WaitForImports();

            RiveAssetImporter.SetOobAssetReference(
                AssetDatabase.LoadAssetAtPath<Asset>(rivPath),
                StormtrooperImageId,
                null);
            yield return WaitForImports();

            Assert.IsNull(LoadReferencedImage(rivPath).OutOfBandAsset,
                "Clearing the inspector assignment should not fall back to auto-detecting a sibling PNG.");
        }

        private static bool HasAssignedStormtrooperImage(string rivPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Asset>(rivPath);
            var data = asset?.EmbeddedAssets?.FirstOrDefault(embedded => embedded.Id == StormtrooperImageId);
            return data?.OutOfBandAsset is ImageOutOfBandAsset;
        }

        private static EmbeddedAssetData LoadReferencedImage(string rivPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Asset>(rivPath);
            Assert.IsNotNull(asset, $"Failed to load Rive asset at {rivPath}");
            var data = asset.EmbeddedAssets.FirstOrDefault(embedded => embedded.Id == StormtrooperImageId);
            Assert.IsNotNull(data, $"Expected referenced image id {StormtrooperImageId} on {rivPath}");
            return data;
        }

        private static string CopyWithoutMeta(string sourceAssetPath, string destAssetPath)
        {
            var destDir = IOPath.GetDirectoryName(destAssetPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            if (!IOFile.Exists(sourceAssetPath))
            {
                Assert.Fail($"Source asset not found at '{sourceAssetPath}' (resolved '{IOPath.GetFullPath(sourceAssetPath)}').");
            }

            IOFile.WriteAllBytes(destAssetPath, IOFile.ReadAllBytes(sourceAssetPath));

            var destMeta = destAssetPath + ".meta";
            if (IOFile.Exists(destMeta))
            {
                IOFile.Delete(destMeta);
            }

            return destAssetPath.Replace('\\', '/');
        }

        private static IEnumerator ImportAsRiveImage(string pngPath)
        {
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            yield return WaitForImports();

            AssetDatabase.SetImporterOverride<ImageAssetImporter>(pngPath);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            yield return WaitForImports();
        }

        private static IEnumerator WaitForImports()
        {
            float start = Time.realtimeSinceStartup;
            while (true)
            {
                while (EditorApplication.isUpdating || EditorApplication.isCompiling)
                {
                    if (Time.realtimeSinceStartup - start > ImportTimeoutSeconds)
                    {
                        Assert.Fail("Timed out waiting for AssetDatabase to finish importing.");
                    }

                    yield return null;
                }

                // AfterUpdate callbacks run on the next editor tick once isUpdating is false,
                // and may kick off more imports.
                yield return null;
                yield return null;

                if (!EditorApplication.isUpdating && !EditorApplication.isCompiling)
                {
                    yield break;
                }
            }
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string failureMessage)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - start > ImportTimeoutSeconds)
                {
                    Assert.Fail(failureMessage);
                }

                yield return null;
            }
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
