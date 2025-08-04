using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


/// <summary>
/// 材质使用者信息
/// </summary>
public class MaterialUsageInfo
{
    public Material Material { get; set; }
    public GameObject User { get; set; }
    public string UserPath { get; set; }
    public bool IsRuntimeMaterial { get; set; }

    public MaterialUsageInfo(Material material, GameObject user, bool isRuntime)
    {
        Material = material;
        User = user;
        IsRuntimeMaterial = isRuntime;
        UserPath = GetGameObjectPath(user);
    }

    private string GetGameObjectPath(GameObject obj)
    {
        if (obj == null) return "";
        string path = obj.name;
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }
}

/// <summary>
/// 材质存储和管理器 - 负责存储材质列表和应用筛选器
/// </summary>
public class MaterialStorage : IDisposable
{
    private List<MaterialUsageInfo> allMaterials = new List<MaterialUsageInfo>();
    private List<MaterialUsageInfo> filteredMaterials = new List<MaterialUsageInfo>();
    private List<IMaterialFilter> filters = new List<IMaterialFilter>();

    public event Action OnMaterialsChanged;
    public event Action OnFiltersChanged;

    public int TotalMaterialCount => allMaterials.Count;
    public int FilteredMaterialCount => filteredMaterials.Count;
    public bool HasFilters => filters.Any(f => f.IsEnabled);

    /// <summary>
    /// 获取所有材质（原始列表）
    /// </summary>
    public List<MaterialUsageInfo> GetAllMaterials()
    {
        return new List<MaterialUsageInfo>(allMaterials);
    }

    /// <summary>
    /// 获取筛选后的材质列表
    /// </summary>
    public List<MaterialUsageInfo> GetFilteredMaterials()
    {
        return new List<MaterialUsageInfo>(filteredMaterials);
    }

    /// <summary>
    /// 获取按 Shader 分组的筛选结果
    /// </summary>
    public Dictionary<string, List<MaterialUsageInfo>> GetFilteredMaterialsByShader()
    {
        var groups = new Dictionary<string, List<MaterialUsageInfo>>();

        foreach (var usage in filteredMaterials)
        {
            string shaderName = usage.Material?.shader?.name ?? "无Shader";
            if (!groups.ContainsKey(shaderName))
            {
                groups[shaderName] = new List<MaterialUsageInfo>();
            }

            groups[shaderName].Add(usage);
        }

        return groups.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
    }

    /// <summary>
    /// 添加材质
    /// </summary>
    public int AddMaterials(UnityEngine.Object[] objects)
    {
        int addedCount = 0;

        foreach (UnityEngine.Object obj in objects)
        {
            if (obj is Material material)
            {
                var usageInfo = new MaterialUsageInfo(material, null, false);
                if (!allMaterials.Any(u => u.Material == material && u.User == null))
                {
                    allMaterials.Add(usageInfo);
                    addedCount++;
                }
            }
        }

        if (addedCount > 0)
        {
            ApplyFilters();
            OnMaterialsChanged?.Invoke();
        }

        return addedCount;
    }

    /// <summary>
    /// 添加运行时材质
    /// </summary>
    public int AddRuntimeMaterials(GameObject[] gameObjects)
    {
        int addedCount = 0;
        List<MaterialUsageInfo> newUsages = new List<MaterialUsageInfo>();

        foreach (GameObject go in gameObjects)
        {
            CollectMaterialUsagesFromGameObject(go, newUsages);
        }

        foreach (MaterialUsageInfo usage in newUsages)
        {
            if (!allMaterials.Any(u => u.Material == usage.Material && u.User == usage.User))
            {
                allMaterials.Add(usage);
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            ApplyFilters();
            OnMaterialsChanged?.Invoke();
        }

        return addedCount;
    }

    /// <summary>
    /// 清空所有材质
    /// </summary>
    public void ClearAllMaterials()
    {
        allMaterials.Clear();
        filteredMaterials.Clear();
        OnMaterialsChanged?.Invoke();
    }

    /// <summary>
    /// 添加筛选器
    /// </summary>
    public void AddFilter(IMaterialFilter filter)
    {
        if (filter != null && !filters.Contains(filter))
        {
            filters.Add(filter);
            ApplyFilters();
            OnFiltersChanged?.Invoke();
        }
    }

    /// <summary>
    /// 移除筛选器
    /// </summary>
    public void RemoveFilter(IMaterialFilter filter)
    {
        if (filters.Remove(filter))
        {
            ApplyFilters();
            OnFiltersChanged?.Invoke();
        }
    }

    /// <summary>
    /// 获取所有筛选器
    /// </summary>
    public List<IMaterialFilter> GetFilters()
    {
        return new List<IMaterialFilter>(filters);
    }

    /// <summary>
    /// 应用所有启用的筛选器
    /// </summary>
    public void ApplyFilters()
    {
        filteredMaterials = new List<MaterialUsageInfo>(allMaterials);

        // 依次应用所有启用的筛选器
        foreach (var filter in filters.Where(f => f.IsEnabled))
        {
            filteredMaterials = filter.Filter(filteredMaterials);
        }

        OnMaterialsChanged?.Invoke();
    }

    /// <summary>
    /// 重置所有筛选器
    /// </summary>
    public void ResetAllFilters()
    {
        foreach (var filter in filters)
        {
            filter.Reset();
        }

        ApplyFilters();
        OnFiltersChanged?.Invoke();
    }

    /// <summary>
    /// 根据搜索关键词筛选（额外的搜索功能）
    /// </summary>
    public List<MaterialUsageInfo> SearchMaterials(string searchFilter)
    {
        if (string.IsNullOrEmpty(searchFilter))
            return GetFilteredMaterials();

        return filteredMaterials.Where(usage => IsMaterialMatchSearchFilter(usage.Material, searchFilter)).ToList();
    }

    private bool IsMaterialMatchSearchFilter(Material material, string searchFilter)
    {
        if (material == null || string.IsNullOrEmpty(searchFilter))
            return false;

        string lowerFilter = searchFilter.ToLower();
        return material.name.ToLower().Contains(lowerFilter) ||
               (material.shader != null && material.shader.name.ToLower().Contains(lowerFilter));
    }

    private void CollectMaterialUsagesFromGameObject(GameObject go, List<MaterialUsageInfo> usages)
    {
        if (go == null) return;

        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;

            if (renderer.sharedMaterials != null)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null)
                    {
                        usages.Add(new MaterialUsageInfo(material, renderer.gameObject, true));
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        OnMaterialsChanged = null;
        OnFiltersChanged = null;
        allMaterials.Clear();
        filteredMaterials.Clear();
        filters.Clear();
    }
}