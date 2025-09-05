using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 重构后的材质批量选择器 - 主显示窗口
/// 职责：UI显示和用户交互，通过调用存储管理器和筛选器来处理数据
/// </summary>
public class MaterialBatchSelectionWindow : EditorWindow
{
    #region Fields

    // 核心组件
    private MaterialStorage materialStorage;
    private ReferenceDetector referenceDetector;

    // 筛选器实例
    private ShaderFilter shaderFilter;
    private PropertyFilter propertyFilter;
    private NameFilter nameFilter;

    // UI状态
    private Vector2 scrollPosition;
    private Vector2 filterScrollPosition;
    private bool showAllMaterials = false;
    private bool showReferences = true;
    private bool showFilters = true;
    private string searchFilter = "";

    // UI样式
    private GUIStyle headerStyle;
    private GUIStyle boxStyle;

    #endregion

    #region Unity Lifecycle

    [MenuItem("Tools/Material Batch Selection (Refactored)")]
    public static void ShowWindow()
    {
        GetWindow<MaterialBatchSelectionWindow>("材质批选择 (重构版)");
    }

    private void OnEnable()
    {
        InitializeComponents();
        SetupStyles();
    }

    private void OnDisable()
    {
        CleanupComponents();
    }

    #endregion

    #region Initialization

    private void InitializeComponents()
    {
        // 初始化核心组件
        materialStorage = new MaterialStorage();
        referenceDetector = new ReferenceDetector();

        // 初始化筛选器
        shaderFilter = new ShaderFilter();
        propertyFilter = new PropertyFilter();
        nameFilter = new NameFilter();

        // 添加筛选器到存储管理器
        materialStorage.AddFilter(shaderFilter);
        materialStorage.AddFilter(propertyFilter);
        materialStorage.AddFilter(nameFilter);

        // 注册事件
        materialStorage.OnMaterialsChanged += OnMaterialsChanged;
        materialStorage.OnFiltersChanged += OnFiltersChanged;
    }

    private void CleanupComponents()
    {
        if (materialStorage != null)
        {
            materialStorage.OnMaterialsChanged -= OnMaterialsChanged;
            materialStorage.OnFiltersChanged -= OnFiltersChanged;
            materialStorage.Dispose();
        }

        referenceDetector?.Dispose();
    }

    private void SetupStyles()
    {
        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };

        boxStyle = new GUIStyle("box")
        {
            padding = new RectOffset(10, 10, 10, 10)
        };
    }

    private void OnMaterialsChanged()
    {
        Repaint();
    }

    private void OnFiltersChanged()
    {
        Repaint();
    }

    #endregion

    #region GUI Methods

    private void OnGUI()
    {
        try
        {
            DrawHeader();
            DrawInstructions();
            DrawToolbar();
            DrawFiltersSection();
            DrawSearchBar();
            DrawStatistics();
            DrawMaterialList();
        }
        catch (Exception e)
        {
            EditorGUILayout.HelpBox($"发生错误: {e.Message}", MessageType.Error);
            Debug.LogError($"MaterialBatchSelectionWindow Error: {e}");
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("材质批量选择器 (重构版)", headerStyle);
        EditorGUILayout.Space();
    }

    private void DrawInstructions()
    {
        using (new VerticalScope(boxStyle))
        {
            EditorGUILayout.LabelField("使用说明:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• 添加材质：选择材质资源后点击\"添加选中材质\"");
            EditorGUILayout.LabelField("• 运行时材质：运行时选择GameObject后点击\"添加运行时材质\"");
            EditorGUILayout.LabelField("• 筛选器：使用下方的筛选器来过滤材质列表");
            EditorGUILayout.LabelField("• 引用检测：点击\"检测引用\"来查找材质被哪些资源使用");
        }
    }

    private void DrawToolbar()
    {
        using (new VerticalScope(boxStyle))
        {
            EditorGUILayout.LabelField("操作工具栏", EditorStyles.boldLabel);

            using (new HorizontalScope())
            {
                if (GUILayout.Button("添加选中材质", GUILayout.Height(30)))
                {
                    OnAddMaterials();
                }

                if (GUILayout.Button("添加运行时材质", GUILayout.Height(30)))
                {
                    OnAddRuntimeMaterials();
                }

                if (GUILayout.Button("清空材质", GUILayout.Height(30)))
                {
                    OnClearMaterials();
                }

                if (GUILayout.Button("选择所有", GUILayout.Height(30)))
                {
                    OnSelectAllMaterials();
                }
            }

            using (new HorizontalScope())
            {
                if (GUILayout.Button("检测引用", GUILayout.Height(25)))
                {
                    OnCollectReferences();
                }

                if (GUILayout.Button("清除引用缓存", GUILayout.Height(25)))
                {
                    OnClearReferences();
                }

                showAllMaterials = GUILayout.Toggle(showAllMaterials, "显示所有材质", GUILayout.Height(25));
                showReferences = GUILayout.Toggle(showReferences, "显示引用信息", GUILayout.Height(25));
            }
        }
    }

    private void DrawFiltersSection()
    {
        using (new VerticalScope(boxStyle))
        {
            using (new HorizontalScope())
            {
                showFilters = EditorGUILayout.Foldout(showFilters, "筛选器配置", true, EditorStyles.foldoutHeader);

                if (GUILayout.Button("重置所有筛选器", GUILayout.Width(120)))
                {
                    materialStorage.ResetAllFilters();
                }

                if (GUILayout.Button("应用筛选器", GUILayout.Width(80)))
                {
                    materialStorage.ApplyFilters();
                }
            }

            if (showFilters)
            {
                using (new ScrollViewScope(ref filterScrollPosition, GUILayout.Height(200)))
                {
                    // 绘制所有筛选器的UI
                    foreach (var filter in materialStorage.GetFilters())
                    {
                        filter.DrawFilterUI();
                        EditorGUILayout.Space(5);
                    }
                }

                // 显示当前启用的筛选器状态
                DrawActiveFiltersStatus();
            }
        }
    }

    private void DrawActiveFiltersStatus()
    {
        var activeFilters = materialStorage.GetFilters().Where(f => f.IsEnabled).ToList();

        if (activeFilters.Any())
        {
            EditorGUILayout.LabelField("当前启用的筛选器:", EditorStyles.boldLabel);
            foreach (var filter in activeFilters)
            {
                EditorGUILayout.LabelField($"• {filter.FilterName}: {filter.FilterDescription}",
                    EditorStyles.miniLabel);
            }
        }
        else
        {
            EditorGUILayout.LabelField("当前无启用的筛选器", EditorStyles.centeredGreyMiniLabel);
        }
    }

    private void DrawSearchBar()
    {
        using (new HorizontalScope())
        {
            EditorGUILayout.LabelField("搜索:", GUILayout.Width(40));
            string newSearchFilter = EditorGUILayout.TextField(searchFilter);

            if (newSearchFilter != searchFilter)
            {
                searchFilter = newSearchFilter;
                Repaint();
            }

            if (GUILayout.Button("清除", GUILayout.Width(50)))
            {
                searchFilter = "";
                Repaint();
            }
        }
    }

    private void DrawStatistics()
    {
        using (new VerticalScope("box"))
        {
            EditorGUILayout.LabelField("统计信息", EditorStyles.boldLabel);

            var searchResults = string.IsNullOrEmpty(searchFilter)
                ? materialStorage.GetFilteredMaterials()
                : materialStorage.SearchMaterials(searchFilter);

            EditorGUILayout.LabelField($"总材质数: {materialStorage.TotalMaterialCount}");
            EditorGUILayout.LabelField($"筛选后: {materialStorage.FilteredMaterialCount}");
            EditorGUILayout.LabelField($"搜索结果: {searchResults.Count}");
            EditorGUILayout.LabelField($"Shader 分组数: {materialStorage.GetFilteredMaterialsByShader().Count}");
        }
    }

    private void DrawMaterialList()
    {
        EditorGUILayout.LabelField("材质列表", EditorStyles.boldLabel);

        using (new ScrollViewScope(ref scrollPosition))
        {
            try
            {
                var materialsToShow = string.IsNullOrEmpty(searchFilter)
                    ? materialStorage.GetFilteredMaterials()
                    : materialStorage.SearchMaterials(searchFilter);

                if (showAllMaterials)
                {
                    DrawMaterialListFlat(materialsToShow);
                }
                else
                {
                    DrawMaterialListGrouped(materialsToShow);
                }
            }
            catch (Exception e)
            {
                EditorGUILayout.HelpBox($"显示材质列表时出错: {e.Message}", MessageType.Error);
            }
        }
    }

    private void DrawMaterialListFlat(List<MaterialUsageInfo> materials)
    {
        foreach (var usage in materials)
        {
            DrawMaterialUsageInfo(usage);
        }
    }

    private void DrawMaterialListGrouped(List<MaterialUsageInfo> materials)
    {
        var groupedMaterials = materials.GroupBy(m => m.Material?.shader?.name ?? "无Shader")
            .OrderBy(g => g.Key);

        foreach (var group in groupedMaterials)
        {
            using (new VerticalScope("box"))
            {
                EditorGUILayout.LabelField($"Shader: {group.Key} ({group.Count()} 个材质)", EditorStyles.boldLabel);

                foreach (var usage in group)
                {
                    using (new HorizontalScope())
                    {
                        EditorGUILayout.Space(20, false);
                        using (new VerticalScope())
                        {
                            DrawMaterialUsageInfo(usage);
                        }
                    }
                }
            }

            EditorGUILayout.Space(5);
        }
    }

    private void DrawMaterialUsageInfo(MaterialUsageInfo usage)
    {
        if (usage?.Material == null) return;

        using (new HorizontalScope("box"))
        {
            // 材质预览 - 修复 null 预览问题
            // Rect materialRect = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64), GUILayout.Height(64));
            // var preview = AssetPreview.GetAssetPreview(usage.Material);
            // if (preview)
            // {
            //     EditorGUI.DrawPreviewTexture(materialRect, preview);
            // }
            // else
            // {
            //     // 如果预览为空，显示材质图标
            //     var miniThumbnail = AssetPreview.GetMiniThumbnail(usage.Material);
            //     if (miniThumbnail)
            //     {
            //         GUI.DrawTexture(materialRect, miniThumbnail, ScaleMode.ScaleToFit);
            //     }
            // }

            using (new VerticalScope())
            {
                // 材质信息 - 简化显示，与原版一致
                EditorGUILayout.LabelField($"材质: {usage.Material.name}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Shader: {usage.Material.shader?.name ?? "无"}");

                if (usage.User)
                {
                    EditorGUILayout.LabelField($"使用者: {usage.User.name}");
                    EditorGUILayout.LabelField($"路径: {usage.UserPath}");
                }

                if (usage.IsRuntimeMaterial)
                {
                    EditorGUILayout.LabelField("运行时材质", EditorStyles.miniLabel);
                }

                // 引用信息
                if (showReferences)
                {
                    int refCount = referenceDetector.GetReferenceCount(usage.Material);
                    string refTypes = referenceDetector.GetReferenceTypes(usage.Material);
                    EditorGUILayout.LabelField($"引用: {refCount} 个 ({refTypes})");
                }
            }

            // 操作按钮 - 简化为与原版一致
            using (new VerticalScope(GUILayout.Width(80)))
            {
                if (GUILayout.Button("选择", GUILayout.Height(25)))
                {
                    Selection.activeObject = usage.Material;
                }

                if (usage.User != null && GUILayout.Button("选择使用者", GUILayout.Height(25)))
                {
                    Selection.activeGameObject = usage.User;
                }
            }
        }

        EditorGUILayout.Space(2);
    }

    #endregion

    #region Event Handlers

    private void OnAddMaterials()
    {
        if (Selection.objects == null || Selection.objects.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先在Project面板中选择材质", "确定");
            return;
        }

        int addedCount = materialStorage.AddMaterials(Selection.objects);
        if (addedCount > 0)
        {
            Debug.Log($"成功添加 {addedCount} 个材质");
        }
    }

    private void OnClearMaterials()
    {
        if (EditorUtility.DisplayDialog("确认清空", "确定要清空所有材质吗？", "确定", "取消"))
        {
            materialStorage.ClearAllMaterials();
        }
    }

    private void OnSelectAllMaterials()
    {
        var materials = materialStorage.GetFilteredMaterials();
        if (materials.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有材质可选择", "确定");
            return;
        }

        Selection.objects = materials.Select(usage => usage.Material).Distinct().ToArray();
    }

    private void OnCollectReferences()
    {
        var materials = materialStorage.GetFilteredMaterials();
        if (materials.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有材质需要检测引用", "确定");
            return;
        }

        var uniqueMaterials = materials.Select(usage => usage.Material).Distinct().ToList();
        referenceDetector.CollectAllReferences(uniqueMaterials);
    }

    private void OnClearReferences()
    {
        if (EditorUtility.DisplayDialog("确认清除", "确定要清除引用缓存吗？", "确定", "取消"))
        {
            referenceDetector.ClearCache();
        }
    }

    private void OnAddRuntimeMaterials()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("提示", "只能在运行时获取运行时材质", "确定");
            return;
        }

        if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先在Hierarchy面板中选择GameObject", "确定");
            return;
        }

        int addedCount = materialStorage.AddRuntimeMaterials(Selection.gameObjects);
        if (addedCount > 0)
        {
            Debug.Log($"成功添加 {addedCount} 个运行时材质");
        }
        else
        {
            EditorUtility.DisplayDialog("提示", "未找到任何材质", "确定");
        }
    }

    #endregion
}