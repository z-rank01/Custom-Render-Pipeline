using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomRenderPipeline : RenderPipeline
{
    public CustomRenderPipeline() : base() 
    {
        // use SRP batching
        GraphicsSettings.useScriptableRenderPipelineBatching = true;
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
            renderer.Render();
        }
    }
}
