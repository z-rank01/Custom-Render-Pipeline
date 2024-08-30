using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Profiling;


// CameraRenderer: Editor part
public partial class CameraRenderer
{
    private partial void DrawUnsupportedShader();
    private partial void DrawGizmo();
    private partial void SetupSceneWindow();
    private partial void SetupBufferName(string targetBufferName);


    #region Unity Editor Part

#if UNITY_EDITOR

    // property
    static ShaderTagId[] legacyShaderTagIds = {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };

    private static Material m_errorMaterial;

    private string sampleName { get; set; }

    // method
    private partial void SetupSceneWindow()
    {
        if (m_camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(m_camera);
        }
    }

    private partial void SetupBufferName(string targetbufferName)
    {
        m_commandBuffer.name = sampleName = targetbufferName;
    }

    private partial void DrawGizmo()
    {
        if (Handles.ShouldRenderGizmos())
        {
            m_context.DrawGizmos(m_camera, GizmoSubset.PreImageEffects);
            m_context.DrawGizmos(m_camera, GizmoSubset.PostImageEffects);
        }
    }

    private partial void DrawUnsupportedShader()
    {
        if (m_errorMaterial == null)
        {
            m_errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }

        // how to sort
        SortingSettings sortingSettings = new SortingSettings(m_camera);

        // what to filter
        FilteringSettings filteringSettings = FilteringSettings.defaultValue;

        // what and how to draw
        DrawingSettings drawingSettings = new DrawingSettings(legacyShaderTagIds[0], sortingSettings);
        drawingSettings.overrideMaterial = m_errorMaterial;
        for (int i = 1; i < legacyShaderTagIds.Length; i++)
        {
            drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }

        // draw error material objects
        m_context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

#else

    const string sampleName = cmdBufferName;

#endif

    #endregion
}
