#if RIVE_USING_HDRP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Rive.Components.HDRP
{
    /// <summary>
    /// This is responsible for enqueueing the rendering of Rive objects in the HDRP.
    /// </summary>
    internal class RiveCustomPass : CustomPass
    {

        private ProfilingSampler m_profilingSampler;

        private const string BUFFER_NAME = "Rive HDRP Render Pass";

        private HighDefinitionRenderPipelineHandler m_rpHandler;


        public RiveCustomPass(HighDefinitionRenderPipelineHandler rpHandler, string profilerMarkerName = null)
        {
            if (rpHandler == null)
                throw new System.ArgumentNullException(nameof(rpHandler));

            m_rpHandler = rpHandler;
            var profilerName = string.IsNullOrEmpty(profilerMarkerName) ? BUFFER_NAME : profilerMarkerName;
            m_profilingSampler = new ProfilingSampler(profilerName);
        }

        protected override void Execute(CustomPassContext ctx)
        {
            if (m_rpHandler == null)
                return;

            // Executed every frame for all the cameras inside the pass volume.
            // The context contains the command buffer to use to enqueue graphics commands.
            CommandBuffer cmd = ctx.cmd;

            using (new ProfilingScope(cmd, m_profilingSampler))
            {
                foreach (IRenderer renderer in m_rpHandler.ActiveRenderers)
                {
                    if (renderer == null)
                        continue;

                    renderer.AddToCommandBuffer(cmd);
                }


            }

        }


    }
}
#endif