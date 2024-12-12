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

        /// <summary>
        /// Gets or creates a render pipeline handler for the current render pipeline.
        /// </summary>
        /// <returns> The current render pipeline handler. </returns>
        public static IRenderPipelineHandler GetOrCreateHandler()
        {
            if (s_currentHandler == null)
            {
#if RIVE_USING_URP
                s_currentHandler = SpawnHandlerObject<URP.UniversalRenderPipelineHandler>("[Rive] URP Handler");
#elif RIVE_USING_HDRP
                    s_currentHandler = SpawnHandlerObject<HDRP.HighDefinitionRenderPipelineHandler>("[Rive] HDRP Handler");
#else
                    s_currentHandler = SpawnHandlerObject<BuiltIn.BuiltInRenderPipelineHandler>("[Rive] BuiltInRP Handler");

#endif

            }

            if (s_currentHandler == null)
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




    }
}