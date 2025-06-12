using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Custom_Render_Pipeline.Runtime
{
    public class CustomRenderPipeline : RenderPipeline
    {
        private bool m_useDynamicBatching;
        private bool m_useGPUInstancing;

        public CustomRenderPipeline(bool useGPUInstancing, bool useSRPBatcher, bool useDynamicBatching) : base()
        {
            this.m_useDynamicBatching = useDynamicBatching;
            this.m_useGPUInstancing = useGPUInstancing;

            // use SRP batching
            GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
            
            // use linear intensity for light
            GraphicsSettings.lightsUseLinearIntensity = true;
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            // ignore this
        }

        protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
        {
            Custom_Render_Pipeline.Runtime.CameraRenderer
                renderer = new Custom_Render_Pipeline.Runtime.CameraRenderer();

            for (int i = 0; i < cameras.Count; i++)
            {
                renderer.SetUp(context, cameras[i]);
                renderer.Render(m_useDynamicBatching, m_useGPUInstancing);
            }
        }
    }
}