using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Custom_Render_Pipeline.Runtime
{
    [CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
    public class CustomRenderPipelineAsset : RenderPipelineAsset
    {
        [SerializeField] private bool useDynamicBatching = true;

        [SerializeField] private bool useGPUInstancing = true;

        [SerializeField] private bool useSRPBatcher = true;

        protected override RenderPipeline CreatePipeline()
        {
            return new CustomRenderPipeline(useGPUInstancing, useSRPBatcher, useDynamicBatching);
        }
    }
}