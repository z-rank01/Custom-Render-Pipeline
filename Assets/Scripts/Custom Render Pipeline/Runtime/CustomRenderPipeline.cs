using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomRenderPipeline : RenderPipeline
{
    private bool useDynamicBatching;
    private bool useGPUInstancing;

    public CustomRenderPipeline(bool useGPUInstancing, bool useSRPBatcher, bool useDynamicBatching) : base() 
    {
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        
        // use SRP batching
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
    }
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        // ignore this
    }

    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        CameraRenderer renderer = new CameraRenderer();

        for (int i = 0; i < cameras.Count; i++)
        {
            renderer.SetUp(context, cameras[i]);
            renderer.Render(useDynamicBatching, useGPUInstancing);
        }
    }
}
