using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 按 Shader 类型筛选材质的筛选器
/// </summary>
public class ShaderFilter : IMaterialFilter
{
    private string targetShaderName = "";
    private bool useContains = true;
    private bool isEnabled = false;

    public string FilterName => "Shader 筛选器";

    public string FilterDescription =>
        isEnabled ? $"筛选 Shader {(useContains ? "包含" : "等于")} '{targetShaderName}' 的材质" : "未启用";

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public List<MaterialUsageInfo> Filter(List<MaterialUsageInfo> materials)
    {
        if (!IsEnabled || string.IsNullOrEmpty(targetShaderName))
            return materials;

        return materials.Where(usage => MatchesShader(usage.Material)).ToList();
    }

    public void DrawFilterUI()
    {
        using (new VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Shader 筛选器", EditorStyles.boldLabel);

            isEnabled = EditorGUILayout.Toggle("启用筛选器", isEnabled);

            if (isEnabled)
            {
                targetShaderName = EditorGUILayout.TextField("目标 Shader", targetShaderName);
                useContains = EditorGUILayout.Toggle("包含匹配（否则为精确匹配）", useContains);

                if (!string.IsNullOrEmpty(targetShaderName))
                {
                    EditorGUILayout.HelpBox(
                        $"将筛选 Shader 名称{(useContains ? "包含" : "等于")} '{targetShaderName}' 的材质",
                        MessageType.Info);
                }
            }
        }
    }

    public void Reset()
    {
        targetShaderName = "";
        useContains = true;
        isEnabled = false;
    }

    private bool MatchesShader(Material material)
    {
        if (material?.shader == null)
            return false;

        string shaderName = material.shader.name;

        if (useContains)
        {
            return shaderName.ToLower().Contains(targetShaderName.ToLower());
        }
        else
        {
            return shaderName.Equals(targetShaderName, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}