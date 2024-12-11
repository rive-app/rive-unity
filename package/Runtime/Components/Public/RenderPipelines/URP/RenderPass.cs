#if RIVE_USING_URP
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_2023_1_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif
using UnityEngine.Rendering.Universal;
namespace Rive.Components.URP
{
    internal class RenderPass : ScriptableRenderPass
    {
        private IRenderer m_riveRenderer;

        private ProfilingSampler m_profilingSampler;

        private const string m_bufferName = "Rive Render Pass";


        internal RenderPass()
        {
            // Default constructor for object pooling
        }

        public RenderPass(Renderer riveRenderer, RenderPassEvent passEvent, string profilerMarkerName)
        {
            if (riveRenderer == null)
                throw new System.ArgumentNullException(nameof(riveRenderer));

            this.Init(riveRenderer, passEvent, profilerMarkerName);
        }

        internal void Init(IRenderer riveRenderer, RenderPassEvent passEvent, string profilerMarkerName)
        {
            m_riveRenderer = riveRenderer;
            renderPassEvent = passEvent;
            m_profilingSampler = new ProfilingSampler(profilerMarkerName);
        }



#if UNITY_2023_1_OR_NEWER
        private class PassData
        {
            internal Renderer riveRenderer;
        }


        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_riveRenderer == null) return;

            Renderer renderer = m_riveRenderer as Renderer;

            if (renderer == null) return;

            // Since we're not using most of the RenderGraph features, we can use AddUnsafePass instead of AddRasterRenderPass.
            // In this mode, we're required to handle/set up graphics state, which we already do in c++.
            // By using AddUnsafePass, we avoid the overhead of having to call SetRenderAttachment as required by AddRasterRenderPass.
            // More info: https://docs.unity.cn/Packages/com.unity.render-pipelines.core@17.0/api/UnityEngine.Rendering.RenderGraphModule.RenderGraph.html#UnityEngine_Rendering_RenderGraphModule_RenderGraph_AddUnsafePass__1_System_String___0__
            using (var builder = renderGraph.AddUnsafePass<PassData>("Rive Render Pass", out var passData))
            {
                passData.riveRenderer = renderer;

                // Prevent render graph from culling this pass
                builder.AllowPassCulling(false);


                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                {
                    ((Renderer)data.riveRenderer).AddToCommandBuffer(context.cmd);
                });
            }
        }
#endif
        // This method is obsolete in Unity 2023.1/Unity 6 and newer, but we keep it so that Rive rendering still works without RenderGraph when in Compatibility mode.
#if UNITY_2023_1_OR_NEWER
        [System.Obsolete]
#endif
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {

            if (m_riveRenderer == null)
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get(m_bufferName);

            using (new ProfilingScope(cmd, m_profilingSampler))
            {
                m_riveRenderer.AddToCommandBuffer(cmd);

            }


            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public IRenderer GetRenderer()
        {
            return m_riveRenderer;
        }

        public void UpdateRenderer(IRenderer renderer)
        {
            m_riveRenderer = renderer;
        }

        public void UpdateRenderPassEvent(RenderPassEvent passEvent)
        {
            renderPassEvent = passEvent;
        }

    }
}
#endif