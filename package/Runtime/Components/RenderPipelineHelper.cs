using Rive.Utils;
using UnityEngine;


namespace Rive.Components
{
    /// <summary>
    /// This class is responsible for handling the rendering of Rive objects depending on the current Unity render pipeline. 
    /// </summary>
    internal class RenderPipelineHelper
    {

        private static IRenderPipelineHandler s_currentHandler;

        public static IRenderPipelineHandler CurrentHandler => s_currentHandler;

        /// <summary>
        /// Checks if the handler reference is valid, accounting for Unity's object lifecycle (when using Unity as a Library)
        /// This is necessary because s_currentHandler is typed as an interface, which bypasses
        /// Unity's custom == operator that detects destroyed objects.
        /// </summary>
        private static bool IsHandlerValid => s_currentHandler is Object obj && obj != null;


        /// <summary>
        /// Gets or creates a render pipeline handler for the current render pipeline.
        /// </summary>
        /// <returns> The current render pipeline handler. </returns>
        public static IRenderPipelineHandler GetOrCreateHandler()
        {
            if (!IsHandlerValid)
            {

                s_currentHandler = null;

#if RIVE_USING_URP
                s_currentHandler = SpawnHandlerObject<URP.UniversalRenderPipelineHandler>("[Rive] URP Handler");
#elif RIVE_USING_HDRP
                    s_currentHandler = SpawnHandlerObject<HDRP.HighDefinitionRenderPipelineHandler>("[Rive] HDRP Handler");
#else
                s_currentHandler = SpawnHandlerObject<BuiltIn.BuiltInRenderPipelineHandler>("[Rive] BuiltInRP Handler");

#endif

            }

            if (!IsHandlerValid)
            {
                DebugLogger.Instance.LogError("No render pipeline handler found.");
            }

            return s_currentHandler;
        }

        /// <summary>
        /// Spawns a handler object of the specified type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        internal static T SpawnHandlerObject<T>(string name) where T : MonoBehaviour
        {
            GameObject handlerObject = new GameObject(name);
            Object.DontDestroyOnLoad(handlerObject);
            var handler = handlerObject.AddComponent<T>();
            return handler;
        }



#if UNITY_EDITOR
        // We need to account for Domain Reload in the editor being disabled, so we reset the current handler when the domain reloads.
        // If we don't do this, Rive Widgets won't render after domain reload and will show a white screen instead.
        // More info: https://docs.unity3d.com/6000.0/Documentation/Manual/domain-reloading.html
        // Note: This does NOT help with Unity as a Library reloads. IsHandlerValid handles that case.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            s_currentHandler = null;
        }
#endif

    }
}