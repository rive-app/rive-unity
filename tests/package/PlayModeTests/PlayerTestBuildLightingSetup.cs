#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: TestPlayerBuildModifier(typeof(Rive.Tests.PlayerTestBuildLightingSetup))]

namespace Rive.Tests
{
    public sealed class PlayerTestBuildLightingSetup : ITestPlayerBuildModifier
    {
        public BuildPlayerOptions ModifyOptions(BuildPlayerOptions playerOptions)
        {
            foreach (var scenePath in playerOptions.scenes)
            {
                // Only the scene the test framework generates. Project scenes
                // carry their own lighting settings, and saving them here
                // would change them in the repo.
                if (string.IsNullOrEmpty(scenePath) ||
                    !System.IO.Path.GetFileName(scenePath).StartsWith("InitTestScene"))
                {
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                DisableBakedLighting(scene);
                EditorSceneManager.SaveScene(scene);
            }

            return playerOptions;
        }

        private static void DisableBakedLighting(Scene scene)
        {
            // Player tests build a temporary InitTestScene. On CI, that scene can try to
            // bake GI and hit the unavailable lightmapper, so make the generated scene
            // explicit about not using baked or realtime GI before BuildPipeline runs.
            var lightingSettings = new LightingSettings
            {
                name = "Rive Player Test Lighting Settings",
                autoGenerate = false,
                bakedGI = false,
                realtimeGI = false,
                lightmapper = LightingSettings.Lightmapper.ProgressiveCPU
            };

            Lightmapping.SetLightingSettingsForScene(scene, lightingSettings);
            Lightmapping.Clear();
            Lightmapping.ClearLightingDataAsset();
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[Rive Tests] Disabled baked lighting for player test scene: {scene.path}");
        }
    }
}
#endif
