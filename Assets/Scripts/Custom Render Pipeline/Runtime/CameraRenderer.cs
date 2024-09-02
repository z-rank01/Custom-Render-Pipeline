using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

// CameraRenderer: Runtime(game) part
namespace Custom_Render_Pipeline.Runtime
{
    public partial class CameraRenderer
    {
        private ScriptableRenderContext m_context;
        private Camera m_camera;
        private CommandBuffer m_commandBuffer;
        private CullingResults m_cullingResults;
        private Lighting m_lighting;

        private static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
        private static ShaderTagId litLightShaderTagId = new ShaderTagId("CustomLit");

        // interface
        public void SetUp(ScriptableRenderContext context, Camera camera)
        {
            m_context = context;
            m_camera = camera;
            m_commandBuffer = CommandBufferPool.Get();
            m_lighting = new Lighting();
            
            // editor setup
            SetupSceneWindow();

            ProfilerBeginSample("Editor Only");
            SetupBufferName(m_camera.name);
            ProfilerEndSample();

            // culling
            if (!Cull())
                return;

            // game setup
            SetUpCameraContext();
            SetUpBuffer();
            m_lighting.SetUp(m_context, ref m_cullingResults);
        }

        public void Render(bool useDynamicBatching, bool useGPUInstancing)
        {
            DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);
            DrawUnsupportedShader();
            DrawGizmo();
            Submit();
        }


        // function

        // setups
        private void SetUpCameraContext()
        {
            m_context.SetupCameraProperties(m_camera);
        }

        private void SetUpBuffer()
        {
            // this is for sampling in frame Debugger and profiler.
            CmdBufferBeginSample(m_sampleName);

            ClearRenderTarget();
            CmdBufferExecute();
            CmdBufferClear();
        }

        private bool Cull()
        {
            ScriptableCullingParameters parameters;
            if (m_camera.TryGetCullingParameters(out parameters))
            {
                m_cullingResults = m_context.Cull(ref parameters);
                return true;
            }

            return false;
        }

        // render 
        private void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing)
        {
            // how to sort
            SortingSettings sortingSettings = new SortingSettings(m_camera);
            sortingSettings.criteria = SortingCriteria.CommonOpaque;

            // what to filter
            FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

            // what and how to draw
            DrawingSettings drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings);
            drawingSettings.enableInstancing = useGPUInstancing;
            drawingSettings.enableDynamicBatching = useDynamicBatching;
            drawingSettings.SetShaderPassName(1, litLightShaderTagId);
            
            // draw opaque
            m_context.DrawRenderers(m_cullingResults, ref drawingSettings, ref filteringSettings);
            m_context.DrawSkybox(m_camera);

            // draw transparent
            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            drawingSettings.sortingSettings = sortingSettings;
            filteringSettings.renderQueueRange = RenderQueueRange.transparent;

            m_context.DrawRenderers(m_cullingResults, ref drawingSettings, ref filteringSettings);
        }

        private void Submit()
        {
            CmdBufferEndSample(m_sampleName);
            CmdBufferExecute();
            CmdBufferClear();
            m_context.Submit();
            CommandBufferPool.Release(m_commandBuffer);
        }

        private void ClearRenderTarget(bool clearDepth, bool clearColor, Color color)
        {
            m_commandBuffer.ClearRenderTarget(clearDepth, clearColor, color);
        }

        private void ClearRenderTarget()
        {
            CameraClearFlags flags = m_camera.clearFlags;
            m_commandBuffer.ClearRenderTarget(flags <= CameraClearFlags.Depth,
                flags == CameraClearFlags.Color,
                Color.clear);
        }

        // command buffer executions
        private void CmdBufferExecute()
        {
            m_context.ExecuteCommandBuffer(m_commandBuffer);
        }

        private void CmdBufferClear()
        {
            m_commandBuffer.Clear();
        }

        private void CmdBufferBeginSample(string bufferName)
        {
            m_commandBuffer.BeginSample(bufferName);
        }

        private void CmdBufferEndSample(string bufferName)
        {
            m_commandBuffer.EndSample(bufferName);
        }

        // profiler
        private void ProfilerBeginSample(string showName)
        {
            Profiler.BeginSample(showName);
        }

        private void ProfilerEndSample()
        {
            Profiler.EndSample();
        }
    }
}