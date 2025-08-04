using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 智能引用检测器 - 使用策略模式和智能缓存
/// </summary>
public class ReferenceDetector : IDisposable
{
    private Dictionary<Material, List<UnityEngine.Object>> materialReferences =
        new Dictionary<Material, List<UnityEngine.Object>>();

    private Dictionary<Material, string> referenceTypeCache = new Dictionary<Material, string>();
    private SmartDependencyCache dependencyCache;

    public ReferenceDetector()
    {
        dependencyCache = new SmartDependencyCache();
    }

    public void CollectAllReferences(List<Material> materials)
    {
        if (materials.Count == 0) return;

        EditorUtility.DisplayProgressBar("检测引用", "正在检测材质引用...", 0f);

        try
        {
            for (int i = 0; i < materials.Count; i++)
            {
                Material material = materials[i];
                float progress = (float)i / materials.Count;
                EditorUtility.DisplayProgressBar("检测引用", $"检测材质: {material.name}", progress);

                CollectMaterialReferences(material);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"引用检测失败: {e.Message}");
            EditorUtility.DisplayDialog("错误", $"引用检测失败: {e.Message}", "确定");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private void CollectMaterialReferences(Material material)
    {
        if (material == null || materialReferences.ContainsKey(material))
            return;

        List<UnityEngine.Object> references = new List<UnityEngine.Object>();
        string materialPath = AssetDatabase.GetAssetPath(material);

        if (string.IsNullOrEmpty(materialPath))
            return;

        // 使用策略模式检测不同类型的引用
        var detectionStrategies = new IReferenceDetectionStrategy[]
        {
            new PrefabReferenceStrategy(dependencyCache),
            new SceneReferenceStrategy(dependencyCache)
        };

        foreach (var strategy in detectionStrategies)
        {
            strategy.DetectReferences(material, materialPath, references);
        }

        materialReferences[material] = references;
        referenceTypeCache.Remove(material);
    }

    public int GetReferenceCount(Material material)
    {
        return materialReferences.ContainsKey(material) ? materialReferences[material].Count : 0;
    }

    public string GetReferenceTypes(Material material)
    {
        if (material == null) return "";

        if (referenceTypeCache.ContainsKey(material))
            return referenceTypeCache[material];

        if (!materialReferences.ContainsKey(material))
            return "";

        var references = materialReferences[material];
        var types = new HashSet<string>();

        foreach (var reference in references)
        {
            if (reference is GameObject go)
            {
                string path = AssetDatabase.GetAssetPath(reference);
                if (path.EndsWith(".prefab"))
                {
                    types.Add("Prefab");
                }
                else if (path.EndsWith(".unity"))
                {
                    types.Add("Scene");
                }
                else
                {
                    types.Add("GameObject");
                }
            }
        }

        string result = string.Join(", ", types);
        referenceTypeCache[material] = result;
        return result;
    }

    public List<UnityEngine.Object> GetReferences(Material material)
    {
        return materialReferences.ContainsKey(material)
            ? new List<UnityEngine.Object>(materialReferences[material])
            : new List<UnityEngine.Object>();
    }

    public void ClearCache()
    {
        materialReferences.Clear();
        referenceTypeCache.Clear();
        dependencyCache.Clear();
    }

    public void Dispose()
    {
        dependencyCache?.Dispose();
    }
}

/// <summary>
/// 智能依赖缓存 - 基于文件修改时间的缓存策略
/// </summary>
public class SmartDependencyCache : IDisposable
{
    private Dictionary<string, CachedDependency> prefabDependencies = new Dictionary<string, CachedDependency>();
    private Dictionary<string, CachedDependency> sceneDependencies = new Dictionary<string, CachedDependency>();

    public string[] GetDependencies(string path, bool isPrefab)
    {
        var cache = isPrefab ? prefabDependencies : sceneDependencies;

        if (cache.TryGetValue(path, out var cached) && !cached.IsExpired())
        {
            return cached.Dependencies;
        }

        var dependencies = AssetDatabase.GetDependencies(path, false);
        cache[path] = new CachedDependency(dependencies, path);

        return dependencies;
    }

    public void Clear()
    {
        prefabDependencies.Clear();
        sceneDependencies.Clear();
    }

    public void Dispose()
    {
        Clear();
    }
}

/// <summary>
/// 缓存的依赖信息
/// </summary>
public class CachedDependency
{
    public string[] Dependencies { get; }
    public DateTime LastModified { get; }
    private string assetPath;

    public CachedDependency(string[] dependencies, string path)
    {
        Dependencies = dependencies;
        assetPath = path;
        LastModified = System.IO.File.GetLastWriteTime(path);
    }

    public bool IsExpired()
    {
        try
        {
            var currentModified = System.IO.File.GetLastWriteTime(assetPath);
            return currentModified > LastModified;
        }
        catch
        {
            return true; // 文件不存在或无法访问，认为已过期
        }
    }
}

/// <summary>
/// 引用检测策略接口
/// </summary>
public interface IReferenceDetectionStrategy
{
    void DetectReferences(Material material, string materialPath, List<UnityEngine.Object> references);
}

/// <summary>
/// 基础引用检测策略 - 提取公共逻辑
/// </summary>
public abstract class BaseReferenceDetectionStrategy : IReferenceDetectionStrategy
{
    protected SmartDependencyCache dependencyCache;

    protected BaseReferenceDetectionStrategy(SmartDependencyCache cache)
    {
        dependencyCache = cache;
    }

    public abstract void DetectReferences(Material material, string materialPath, List<UnityEngine.Object> references);

    protected void ProcessAssetsByType(string assetType, bool isPrefab, Material material, string materialPath,
        List<UnityEngine.Object> references, Action<string, Material, List<UnityEngine.Object>> processAsset)
    {
        string[] guids = AssetDatabase.FindAssets($"t:{assetType}");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string[] dependencies = dependencyCache.GetDependencies(path, isPrefab);

            if (dependencies.Contains(materialPath))
            {
                processAsset(path, material, references);
            }
        }
    }
}

/// <summary>
/// Prefab引用检测策略
/// </summary>
public class PrefabReferenceStrategy : BaseReferenceDetectionStrategy
{
    public PrefabReferenceStrategy(SmartDependencyCache cache) : base(cache)
    {
    }

    public override void DetectReferences(Material material, string materialPath, List<UnityEngine.Object> references)
    {
        ProcessAssetsByType("Prefab", true, material, materialPath, references, ProcessPrefabAsset);
    }

    private void ProcessPrefabAsset(string path, Material material, List<UnityEngine.Object> references)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
        {
            CheckGameObjectForMaterial(prefab, material, references);
        }
    }

    private void CheckGameObjectForMaterial(GameObject go, Material targetMaterial, List<UnityEngine.Object> references)
    {
        if (go == null || targetMaterial == null) return;

        // 检查所有Renderer组件
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0)
            {
                if (renderer.sharedMaterials.Contains(targetMaterial))
                {
                    references.Add(go);
                    break;
                }
            }
        }

        // 检查特殊Renderer组件
        CheckSpecialRenderers(go, targetMaterial, references);
    }

    private void CheckSpecialRenderers(GameObject go, Material targetMaterial, List<UnityEngine.Object> references)
    {
        var specialRenderers = new Component[]
        {
            go.GetComponentInChildren<ParticleSystemRenderer>(true),
            go.GetComponentInChildren<LineRenderer>(true),
            go.GetComponentInChildren<TrailRenderer>(true)
        };

        foreach (var renderer in specialRenderers)
        {
            if (renderer != null)
            {
                var materialProperty = renderer.GetType().GetProperty("sharedMaterial");
                if (materialProperty != null)
                {
                    var material = materialProperty.GetValue(renderer) as Material;
                    if (material == targetMaterial)
                    {
                        references.Add(go);
                        break;
                    }
                }
            }
        }
    }
}

/// <summary>
/// Scene引用检测策略
/// </summary>
public class SceneReferenceStrategy : BaseReferenceDetectionStrategy
{
    public SceneReferenceStrategy(SmartDependencyCache cache) : base(cache)
    {
    }

    public override void DetectReferences(Material material, string materialPath, List<UnityEngine.Object> references)
    {
        ProcessAssetsByType("Scene", false, material, materialPath, references, ProcessSceneAsset);
    }

    private void ProcessSceneAsset(string path, Material material, List<UnityEngine.Object> references)
    {
        references.Add(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path));
    }
}