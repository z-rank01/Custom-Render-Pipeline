using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Custom_Render_Pipeline.Runtime
{
    public class Lighting
    {
        [SerializeField]
        private const int maxDirectionalLights = 4;
        private const string m_bufferName = "Lighting";
        private CommandBuffer m_commandBuffer;
        private CullingResults m_cullingResults;
        
        // shader property
        private static int directionalLightCountID = Shader.PropertyToID("_DirectionalLightCount");
        private static int directionalLightColorsID = Shader.PropertyToID("_DirectionalLightColors");
        private static int directionalLightDirectionsID = Shader.PropertyToID("_DirectionalLightDirections");
        
        // shader property appdata
        private static Vector4[] directionalLightColors = new Vector4[maxDirectionalLights];
        private static Vector4[] directionalLightDirections = new Vector4[maxDirectionalLights];
        
        // interface
        public void SetUp(ScriptableRenderContext context, ref CullingResults cullingResults)
        {
            m_commandBuffer = CommandBufferPool.Get(m_bufferName);
            m_cullingResults = cullingResults;
            
            // setup shader property
            CmdBufferBeginSample(m_bufferName);
            SetupDirectionalLights();
            CmdBufferEndSample(m_bufferName);
            CmdBufferExecute(context);
            CmdBufferClear();
        }
        
        // function

        private void SetupDirectionalLights()
        {
            NativeArray<VisibleLight> visibleLights = m_cullingResults.visibleLights;
            
            // setup lights' appdata
            int directionalLightCounter = 0;
            for (int i = 0; i < visibleLights.Length; ++i)
            {
                VisibleLight visibleLight = visibleLights[i];
                
                if (visibleLight.lightType != LightType.Directional) continue;

                directionalLightColors[directionalLightCounter] = visibleLight.finalColor;
                directionalLightDirections[directionalLightCounter] = - visibleLight.localToWorldMatrix.GetColumn(2);
                directionalLightCounter++;
                
                if (directionalLightCounter >= maxDirectionalLights)
                    break;
            }
            
            // transfer to shader property
            m_commandBuffer.SetGlobalInt(directionalLightCountID, visibleLights.Length);
            m_commandBuffer.SetGlobalVectorArray(directionalLightColorsID, directionalLightColors);
            m_commandBuffer.SetGlobalVectorArray(directionalLightDirectionsID, directionalLightDirections);
        }
        
        // command buffer executions
        private void CmdBufferExecute(ScriptableRenderContext context)
        {
            context.ExecuteCommandBuffer(m_commandBuffer);
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
    }
}