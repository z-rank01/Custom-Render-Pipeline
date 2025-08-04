using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 按材质名称筛选的筛选器
/// </summary>
public class NameFilter : IMaterialFilter
{
    private string targetName = "";
    private bool useContains = true;
    private bool caseSensitive = false;
    private bool isEnabled = false;

    public string FilterName => "名称筛选器";

    public string FilterDescription => isEnabled
        ? $"筛选名称{(useContains ? "包含" : "等于")} '{targetName}' 的材质{(caseSensitive ? " (区分大小写)" : " (不区分大小写)")}"
        : "未启用";

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public List<MaterialUsageInfo> Filter(List<MaterialUsageInfo> materials)
    {
        if (!IsEnabled || string.IsNullOrEmpty(targetName))
            return materials;

        return materials.Where(usage => MatchesName(usage.Material)).ToList();
    }

    public void DrawFilterUI()
    {
        using (new VerticalScope("box"))
        {
            EditorGUILayout.LabelField("名称筛选器", EditorStyles.boldLabel);

            isEnabled = EditorGUILayout.Toggle("启用筛选器", isEnabled);

            if (isEnabled)
            {
                targetName = EditorGUILayout.TextField("目标名称", targetName);
                useContains = EditorGUILayout.Toggle("包含匹配（否则为精确匹配）", useContains);
                caseSensitive = EditorGUILayout.Toggle("区分大小写", caseSensitive);

                if (!string.IsNullOrEmpty(targetName))
                {
                    EditorGUILayout.HelpBox($"将筛选名称{(useContains ? "包含" : "等于")} '{targetName}' 的材质", MessageType.Info);
                }
            }
        }
    }

    public void Reset()
    {
        targetName = "";
        useContains = true;
        caseSensitive = false;
        isEnabled = false;
    }

    private bool MatchesName(Material material)
    {
        if (material == null)
            return false;

        string materialName = material.name;
        string searchName = targetName;

        if (!caseSensitive)
        {
            materialName = materialName.ToLower();
            searchName = searchName.ToLower();
        }

        if (useContains)
        {
            return materialName.Contains(searchName);
        }
        else
        {
            return materialName.Equals(searchName);
        }
    }
}