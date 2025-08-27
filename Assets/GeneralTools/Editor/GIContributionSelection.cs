using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System;

/// <summary>
/// GI贡献状态选择器 - 按GI贡献状态分组材质管理
/// </summary>
public class GIContributionSelection : EditorWindow
{
    #region Fields

    // 核心管理器
    private GIContributionManager contributionManager;
    private GIContributionUIManager uiManager;

    // UI状态
    private Vector2 scrollPosition;
    private bool showContributeGI = true;
    private bool showNonContributeGI = true;
    private string searchFilter = "";

    #endregion

    #region Unity Lifecycle

    [MenuItem("Tools/GI Contribution Selection")]
    public static void ShowWindow()
    {
        GetWindow<GIContributionSelection>("GI贡献状态选择器");
    }

    private void OnEnable()
    {
        InitializeManagers();
    }

    private void OnDisable()
    {
        CleanupManagers();
    }

    #endregion

    #region Initialization

    private void InitializeManagers()
    {
        contributionManager = new GIContributionManager();
        uiManager = new GIContributionUIManager(contributionManager);

        // 注册事件
        contributionManager.OnDataChanged += OnDataChanged;
    }

    private void CleanupManagers()
    {
        if (contributionManager != null)
        {
            contributionManager.OnDataChanged -= OnDataChanged;
            contributionManager.Dispose();
        }
    }

    private void OnDataChanged()
    {
        Repaint();
    }

    #endregion

    #region GUI Methods

    private void OnGUI()
    {
        try
        {
            uiManager.DrawHeader();
            uiManager.DrawInstructions(contributionManager.TotalObjectCount);
            uiManager.DrawToolbar(OnAnalyzeSelection, OnClearData, OnSelectAllObjects, ref showContributeGI, ref showNonContributeGI);
            uiManager.DrawSearchBar(ref searchFilter, OnSearchChanged);
            uiManager.DrawStatistics(contributionManager.GetStatistics());
            uiManager.DrawContent(showContributeGI, showNonContributeGI, searchFilter, ref scrollPosition);
        }
        catch (Exception e)
        {
            EditorGUILayout.HelpBox($"发生错误: {e.Message}", MessageType.Error);
            Debug.LogError($"GIContributionSelection Error: {e}");
        }
    }

    #endregion

    #region Event Handlers

    private void OnAnalyzeSelection()
    {
        if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先在Hierarchy面板中选择GameObject", "确定");
            return;
        }

        int processedCount = contributionManager.AnalyzeGameObjects(Selection.gameObjects);
        if (processedCount > 0)
        {
            Debug.Log($"成功分析 {processedCount} 个包含MeshRenderer的GameObject");
        }
        else
        {
            EditorUtility.DisplayDialog("提示", "选中的GameObject中没有找到MeshRenderer组件", "确定");
        }
    }

    private void OnClearData()
    {
        if (EditorUtility.DisplayDialog("确认清空", "确定要清空所有数据吗？", "确定", "取消"))
        {
            contributionManager.ClearAllData();
        }
    }

    private void OnSelectAllObjects()
    {
        var allObjects = contributionManager.GetAllGameObjects();
        if (allObjects.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有GameObject可选择", "确定");
            return;
        }

        Selection.objects = allObjects.ToArray();
    }

    private void OnSearchChanged()
    {
        // 搜索逻辑由UIManager内部处理
    }

    #endregion
}

#region Core Managers

/// <summary>
/// GI贡献信息
/// </summary>
public class GIContributionInfo
{
    public GameObject GameObject { get; set; }
    public MeshRenderer Renderer { get; set; }
    public Material[] Materials { get; set; }
    public bool ContributeGI { get; set; }
    public string ObjectPath { get; set; }

    public GIContributionInfo(GameObject gameObject, MeshRenderer renderer)
    {
        GameObject = gameObject;
        Renderer = renderer;
        Materials = renderer.sharedMaterials;
        ContributeGI = GameObjectUtility.GetStaticEditorFlags(gameObject).HasFlag(StaticEditorFlags.ContributeGI);
        ObjectPath = GetGameObjectPath(gameObject);
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
/// 材质分组信息
/// </summary>
public class MaterialGroupInfo
{
    public Material Material { get; set; }
    public string ShaderName { get; set; }
    public List<GIContributionInfo> Users { get; set; }

    public MaterialGroupInfo(Material material)
    {
        Material = material;
        ShaderName = material?.shader?.name ?? "无Shader";
        Users = new List<GIContributionInfo>();
    }
}

/// <summary>
/// GI贡献状态管理器
/// </summary>
public class GIContributionManager : IDisposable
{
    private List<GIContributionInfo> allContributionInfos = new List<GIContributionInfo>();
    private Dictionary<bool, Dictionary<string, List<MaterialGroupInfo>>> groupedData = 
        new Dictionary<bool, Dictionary<string, List<MaterialGroupInfo>>>();

    public event Action OnDataChanged;

    public int TotalObjectCount => allContributionInfos.Count;

    public int AnalyzeGameObjects(GameObject[] gameObjects)
    {
        int processedCount = 0;
        var newInfos = new List<GIContributionInfo>();

        foreach (GameObject go in gameObjects)
        {
            CollectGIContributionInfoFromGameObject(go, newInfos);
        }

        foreach (GIContributionInfo info in newInfos)
        {
            // 检查是否已经存在相同的GameObject
            if (!allContributionInfos.Any(existing => existing.GameObject == info.GameObject))
            {
                allContributionInfos.Add(info);
                processedCount++;
            }
        }

        if (processedCount > 0)
        {
            RebuildGroupedData();
            OnDataChanged?.Invoke();
        }

        return processedCount;
    }

    private void CollectGIContributionInfoFromGameObject(GameObject go, List<GIContributionInfo> infos)
    {
        if (go == null) return;

        // 检查当前GameObject
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            infos.Add(new GIContributionInfo(go, renderer));
        }

        // 递归检查子对象
        foreach (Transform child in go.transform)
        {
            CollectGIContributionInfoFromGameObject(child.gameObject, infos);
        }
    }

    private void RebuildGroupedData()
    {
        groupedData.Clear();
        groupedData[true] = new Dictionary<string, List<MaterialGroupInfo>>();   // Contribute GI
        groupedData[false] = new Dictionary<string, List<MaterialGroupInfo>>();  // Non-Contribute GI

        foreach (GIContributionInfo info in allContributionInfos)
        {
            var targetGroup = groupedData[info.ContributeGI];

            foreach (Material material in info.Materials)
            {
                if (material == null) continue;

                string shaderName = material.shader?.name ?? "无Shader";
                
                if (!targetGroup.ContainsKey(shaderName))
                {
                    targetGroup[shaderName] = new List<MaterialGroupInfo>();
                }

                // 查找或创建材质组
                var materialGroup = targetGroup[shaderName].FirstOrDefault(mg => mg.Material == material);
                if (materialGroup == null)
                {
                    materialGroup = new MaterialGroupInfo(material);
                    targetGroup[shaderName].Add(materialGroup);
                }

                materialGroup.Users.Add(info);
            }
        }

        // 排序
        foreach (var giGroup in groupedData.Values)
        {
            foreach (var shaderGroup in giGroup.Values)
            {
                shaderGroup.Sort((a, b) => string.Compare(a.Material.name, b.Material.name));
            }
        }
    }

    public void ClearAllData()
    {
        allContributionInfos.Clear();
        groupedData.Clear();
        OnDataChanged?.Invoke();
    }

    public List<GameObject> GetAllGameObjects()
    {
        return allContributionInfos.Select(info => info.GameObject).Distinct().ToList();
    }

    public Dictionary<string, List<MaterialGroupInfo>> GetShaderGroups(bool contributeGI)
    {
        return groupedData.ContainsKey(contributeGI) ? 
            new Dictionary<string, List<MaterialGroupInfo>>(groupedData[contributeGI]) : 
            new Dictionary<string, List<MaterialGroupInfo>>();
    }

    public List<GIContributionInfo> GetFilteredContributionInfos(string searchFilter, bool contributeGI)
    {
        var filtered = allContributionInfos.Where(info => info.ContributeGI == contributeGI);

        if (!string.IsNullOrEmpty(searchFilter))
        {
            string lowerFilter = searchFilter.ToLower();
            filtered = filtered.Where(info => 
                info.GameObject.name.ToLower().Contains(lowerFilter) ||
                info.ObjectPath.ToLower().Contains(lowerFilter) ||
                info.Materials.Any(mat => mat != null && mat.name.ToLower().Contains(lowerFilter))
            );
        }

        return filtered.ToList();
    }

    public (int contributeCount, int nonContributeCount, int totalMaterials) GetStatistics()
    {
        int contributeCount = allContributionInfos.Count(info => info.ContributeGI);
        int nonContributeCount = allContributionInfos.Count(info => !info.ContributeGI);
        
        var allMaterials = new HashSet<Material>();
        foreach (var info in allContributionInfos)
        {
            foreach (var mat in info.Materials)
            {
                if (mat != null) allMaterials.Add(mat);
            }
        }

        return (contributeCount, nonContributeCount, allMaterials.Count);
    }

    /// <summary>
    /// 请求重新构建数据（用于响应外部更改）
    /// </summary>
    public void RequestDataRebuild()
    {
        RebuildGroupedData();
        OnDataChanged?.Invoke();
    }

    /// <summary>
    /// 替换指定材质为新材质
    /// </summary>
    public int ReplaceMaterial(Material oldMaterial, Material newMaterial)
    {
        if (oldMaterial == null || newMaterial == null)
        {
            Debug.LogWarning("替换材质失败：旧材质或新材质为空");
            return 0;
        }

        int replacedCount = 0;
        var affectedInfos = new List<GIContributionInfo>();

        foreach (var info in allContributionInfos)
        {
            if (info.Renderer == null) continue;

            var materials = info.Renderer.sharedMaterials.ToArray();
            bool hasChanged = false;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == oldMaterial)
                {
                    materials[i] = newMaterial;
                    hasChanged = true;
                    replacedCount++;
                }
            }

            if (hasChanged)
            {
                // 记录撤销操作
                Undo.RecordObject(info.Renderer, $"Replace Material {oldMaterial.name} with {newMaterial.name}");
                info.Renderer.sharedMaterials = materials;
                info.Materials = materials; // 更新缓存
                affectedInfos.Add(info);
            }
        }

        if (replacedCount > 0)
        {
            RebuildGroupedData();
            OnDataChanged?.Invoke();
            Debug.Log($"成功替换 {replacedCount} 个材质引用，影响 {affectedInfos.Count} 个对象");
        }

        return replacedCount;
    }

    public void Dispose()
    {
        OnDataChanged = null;
    }
}

/// <summary>
/// GI贡献状态UI管理器
/// </summary>
public class GIContributionUIManager
{
    private GIContributionManager contributionManager;
    private Dictionary<string, bool> expandedContributeGroups = new Dictionary<string, bool>();
    private Dictionary<string, bool> expandedNonContributeGroups = new Dictionary<string, bool>();
    private Dictionary<string, bool> expandedMaterials = new Dictionary<string, bool>(); // 新增材质展开状态

    public GIContributionUIManager(GIContributionManager contributionManager)
    {
        this.contributionManager = contributionManager;
    }

    public void DrawHeader()
    {
        GUILayout.Label("GI贡献状态选择器", EditorStyles.boldLabel);
    }

    public void DrawInstructions(int objectCount)
    {
        if (objectCount == 0)
        {
            EditorGUILayout.HelpBox(
                "使用说明：\n" +
                "1. 在Hierarchy面板中选择要分析的GameObject\n" +
                "2. 点击'分析选中对象'按钮\n" +
                "3. 系统会检测所有MeshRenderer组件\n" +
                "4. 根据GameObject的GI贡献状态进行分组\n" +
                "5. 每组内的材质会按Shader进一步分类\n" +
                "6. 可以快速选择和管理不同GI状态的对象",
                MessageType.Info);
            EditorGUILayout.Space();
        }
    }

    public void DrawToolbar(Action onAnalyzeSelection, Action onClearData, Action onSelectAllObjects,
        ref bool showContributeGI, ref bool showNonContributeGI)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("分析选中对象", GUILayout.Width(120)))
            {
                onAnalyzeSelection?.Invoke();
            }

            if (GUILayout.Button("清空数据", GUILayout.Width(100)))
            {
                onClearData?.Invoke();
            }

            if (GUILayout.Button("选择所有对象", GUILayout.Width(120)))
            {
                onSelectAllObjects?.Invoke();
            }

            GUILayout.FlexibleSpace();

            Color originalColor = GUI.color;
            
            GUI.color = showContributeGI ? Color.green : Color.white;
            showContributeGI = GUILayout.Toggle(showContributeGI, "显示贡献GI", "Button", GUILayout.Width(100));
            
            GUI.color = showNonContributeGI ? Color.red : Color.white;
            showNonContributeGI = GUILayout.Toggle(showNonContributeGI, "显示非贡献GI", "Button", GUILayout.Width(100));
            
            GUI.color = originalColor;
        }
    }

    public void DrawSearchBar(ref string searchFilter, Action onSearchChanged)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("搜索:", GUILayout.Width(50));

            string newSearchFilter = EditorGUILayout.TextField(searchFilter);
            if (newSearchFilter != searchFilter)
            {
                searchFilter = newSearchFilter;
                onSearchChanged?.Invoke();
            }

            if (GUILayout.Button("清除", GUILayout.Width(50)))
            {
                searchFilter = "";
                onSearchChanged?.Invoke();
            }
        }
    }

    public void DrawStatistics((int contributeCount, int nonContributeCount, int totalMaterials) stats)
    {
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            Color originalColor = GUI.color;
            
            GUI.color = Color.green;
            GUILayout.Label($"贡献GI: {stats.contributeCount}", EditorStyles.miniLabel, GUILayout.Width(80));
            
            GUI.color = Color.red;
            GUILayout.Label($"非贡献GI: {stats.nonContributeCount}", EditorStyles.miniLabel, GUILayout.Width(80));
            
            GUI.color = originalColor;
            GUILayout.Label($"总材质数: {stats.totalMaterials}", EditorStyles.miniLabel, GUILayout.Width(80));
        }
        EditorGUILayout.Space();
    }

    public void DrawContent(bool showContributeGI, bool showNonContributeGI, string searchFilter, ref Vector2 scrollPosition)
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (showContributeGI)
        {
            DrawGIContributionSection(true, searchFilter, "贡献GI的对象", Color.green);
        }

        if (showNonContributeGI)
        {
            if (showContributeGI) EditorGUILayout.Space(10);
            DrawGIContributionSection(false, searchFilter, "非贡献GI的对象", Color.red);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawGIContributionSection(bool contributeGI, string searchFilter, string sectionTitle, Color sectionColor)
    {
        var shaderGroups = contributionManager.GetShaderGroups(contributeGI);
        var expandedGroups = contributeGI ? expandedContributeGroups : expandedNonContributeGroups;

        if (shaderGroups.Count == 0) return;

        // 绘制分区标题
        using (new EditorGUILayout.VerticalScope("box"))
        {
            Color originalColor = GUI.color;
            GUI.color = sectionColor;
            GUILayout.Label(sectionTitle, EditorStyles.boldLabel);
            GUI.color = originalColor;

            foreach (var shaderGroup in shaderGroups)
            {
                string shaderName = shaderGroup.Key;
                List<MaterialGroupInfo> materialGroups = shaderGroup.Value;

                // 应用搜索过滤
                var filteredMaterialGroups = FilterMaterialGroups(materialGroups, searchFilter);
                if (filteredMaterialGroups.Count == 0) continue;

                DrawShaderGroupHeader(shaderName, filteredMaterialGroups, expandedGroups, contributeGI);

                // 修复：使用正确的groupKey格式
                string groupKey = $"{contributeGI}_{shaderName}";
                if (expandedGroups.ContainsKey(groupKey) && expandedGroups[groupKey])
                {
                    DrawMaterialGroups(filteredMaterialGroups);
                }

                EditorGUILayout.Space();
            }
        }
    }

    private List<MaterialGroupInfo> FilterMaterialGroups(List<MaterialGroupInfo> materialGroups, string searchFilter)
    {
        if (string.IsNullOrEmpty(searchFilter))
            return materialGroups;

        string lowerFilter = searchFilter.ToLower();
        return materialGroups.Where(mg => 
            mg.Material.name.ToLower().Contains(lowerFilter) ||
            mg.ShaderName.ToLower().Contains(lowerFilter) ||
            mg.Users.Any(user => user.GameObject.name.ToLower().Contains(lowerFilter))
        ).ToList();
    }

    private void DrawShaderGroupHeader(string shaderName, List<MaterialGroupInfo> materialGroups, Dictionary<string, bool> expandedGroups, bool contributeGI)
    {
        // 修复：使用包含GI状态的唯一键
        string groupKey = $"{contributeGI}_{shaderName}";
        if (!expandedGroups.ContainsKey(groupKey))
        {
            expandedGroups[groupKey] = false;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            string expandIcon = expandedGroups[groupKey] ? "▼" : "▶";
            if (GUILayout.Button(expandIcon, GUILayout.Width(20)))
            {
                expandedGroups[groupKey] = !expandedGroups[groupKey];
                // 强制重绘界面以反映状态变化
                GUI.changed = true;
            }

            int materialCount = materialGroups.Count;
            int objectCount = materialGroups.Sum(mg => mg.Users.Count);
            GUILayout.Label($"{shaderName} ({materialCount} 材质, {objectCount} 对象)", EditorStyles.boldLabel);

            // 全选材质按钮
            if (GUILayout.Button("选择材质", GUILayout.Width(80)))
            {
                Selection.objects = materialGroups.Select(mg => mg.Material).ToArray();
            }

            // 全选对象按钮
            if (GUILayout.Button("选择对象", GUILayout.Width(80)))
            {
                var allObjects = materialGroups.SelectMany(mg => mg.Users.Select(u => u.GameObject)).Distinct().ToArray();
                Selection.objects = allObjects;
            }
        }
    }

    private void DrawMaterialGroups(List<MaterialGroupInfo> materialGroups)
    {
        foreach (MaterialGroupInfo materialGroup in materialGroups)
        {
            DrawMaterialGroupRow(materialGroup);
        }
    }

    private void DrawMaterialGroupRow(MaterialGroupInfo materialGroup)
    {
        string materialKey = GetMaterialKey(materialGroup.Material);
        if (!expandedMaterials.ContainsKey(materialKey))
        {
            expandedMaterials[materialKey] = false;
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            // 材质标题行
            using (new EditorGUILayout.HorizontalScope())
            {
                // 展开/折叠按钮
                string expandIcon = expandedMaterials[materialKey] ? "▼" : "▶";
                if (GUILayout.Button(expandIcon, GUILayout.Width(20)))
                {
                    expandedMaterials[materialKey] = !expandedMaterials[materialKey];
                    GUI.changed = true;
                }

                GUILayout.Label(AssetPreview.GetMiniThumbnail(materialGroup.Material), GUILayout.Width(16), GUILayout.Height(16));
                GUILayout.Label(materialGroup.Material.name, EditorStyles.boldLabel, GUILayout.Width(200));
                GUILayout.Label($"({materialGroup.Users.Count} 个使用者)", GUILayout.Width(100));

                // 显示GI状态统计
                int contributeCount = materialGroup.Users.Count(u => u.ContributeGI);
                int nonContributeCount = materialGroup.Users.Count - contributeCount;
                
                Color originalColor = GUI.color;
                if (contributeCount > 0)
                {
                    GUI.color = Color.green;
                    GUILayout.Label($"[GI:{contributeCount}]", GUILayout.Width(60));
                }
                if (nonContributeCount > 0)
                {
                    GUI.color = Color.red;
                    GUILayout.Label($"[非GI:{nonContributeCount}]", GUILayout.Width(70));
                }
                GUI.color = originalColor;

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("选择材质", GUILayout.Width(80)))
                {
                    Selection.activeObject = materialGroup.Material;
                    EditorGUIUtility.PingObject(materialGroup.Material);
                }

                if (GUILayout.Button("选择使用者", GUILayout.Width(80)))
                {
                    Selection.objects = materialGroup.Users.Select(u => u.GameObject).ToArray();
                }

                // 新增：替换材质按钮
                GUI.color = Color.yellow;
                if (GUILayout.Button("替换材质", GUILayout.Width(80)))
                {
                    MaterialSelectionDialog.Show((newMaterial) => 
                    {
                        if (EditorUtility.DisplayDialog("确认替换", 
                            $"确定要将材质 '{materialGroup.Material.name}' 替换为 '{newMaterial.name}' 吗？\n" +
                            $"这将影响 {materialGroup.Users.Count} 个对象。", 
                            "确定", "取消"))
                        {
                            int replacedCount = contributionManager.ReplaceMaterial(materialGroup.Material, newMaterial);
                            if (replacedCount > 0)
                            {
                                EditorUtility.DisplayDialog("替换完成", 
                                    $"成功替换了 {replacedCount} 个材质引用", "确定");
                            }
                        }
                    });
                }
                GUI.color = originalColor;
            }

            // 使用者列表（只在展开时显示）
            if (expandedMaterials[materialKey])
            {
                DrawMaterialUsersList(materialGroup.Users);
            }
        }
    }

    private void DrawMaterialUsersList(List<GIContributionInfo> users)
    {
        using (new EditorGUILayout.VerticalScope())
        {
            foreach (GIContributionInfo user in users)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(40); // 缩进显示
                    
                    // GameObject图标
                    GUILayout.Label(EditorGUIUtility.ObjectContent(user.GameObject, typeof(GameObject)).image, GUILayout.Width(16), GUILayout.Height(16));
                    
                    // GI状态指示器
                    Color originalColor = GUI.color;
                    GUI.color = user.ContributeGI ? Color.green : Color.red;
                    GUILayout.Label(user.ContributeGI ? "[GI]" : "[非GI]", GUILayout.Width(40));
                    GUI.color = originalColor;

                    GUILayout.Label(user.GameObject.name, GUILayout.Width(150));
                    GUILayout.Label(user.ObjectPath, EditorStyles.miniLabel, GUILayout.Width(300));

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("选择", GUILayout.Width(50)))
                    {
                        Selection.activeGameObject = user.GameObject;
                        EditorGUIUtility.PingObject(user.GameObject);
                    }

                    // 切换GI状态按钮
                    string toggleText = user.ContributeGI ? "禁用GI" : "启用GI";
                    if (GUILayout.Button(toggleText, GUILayout.Width(60)))
                    {
                        ToggleGIContribution(user);
                    }
                }
            }
        }
    }

    private void ToggleGIContribution(GIContributionInfo info)
    {
        if (info.GameObject == null) return;

        var flags = GameObjectUtility.GetStaticEditorFlags(info.GameObject);
        if (info.ContributeGI)
        {
            flags &= ~StaticEditorFlags.ContributeGI;
        }
        else
        {
            flags |= StaticEditorFlags.ContributeGI;
        }
        
        GameObjectUtility.SetStaticEditorFlags(info.GameObject, flags);
        
        // 更新缓存的状态
        info.ContributeGI = !info.ContributeGI;
        
        // 请求重新构建数据以反映更改
        contributionManager.RequestDataRebuild();
    }

    private string GetMaterialKey(Material material)
    {
        if (material == null) return "null";
        return $"{material.GetInstanceID()}_{material.name}";
    }
}

#endregion

/// <summary>
/// 材质选择对话框
/// </summary>
public class MaterialSelectionDialog : EditorWindow
{
    private Material selectedMaterial;
    private System.Action<Material> onMaterialSelected;
    private string searchFilter = "";
    private Vector2 scrollPosition;
    private Material[] allMaterials;

    public static void Show(System.Action<Material> onMaterialSelected)
    {
        var window = GetWindow<MaterialSelectionDialog>("选择替换材质");
        window.onMaterialSelected = onMaterialSelected;
        window.Initialize();
        window.ShowModalUtility();
    }

    private void Initialize()
    {
        // 获取项目中所有材质
        string[] guids = AssetDatabase.FindAssets("t:Material");
        allMaterials = guids.Select(guid => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)))
                           .Where(mat => mat != null)
                           .OrderBy(mat => mat.name)
                           .ToArray();
    }

    private void OnGUI()
    {
        GUILayout.Label("选择要替换的材质", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 搜索栏
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("搜索:", GUILayout.Width(50));
            searchFilter = EditorGUILayout.TextField(searchFilter);
            if (GUILayout.Button("清除", GUILayout.Width(50)))
            {
                searchFilter = "";
            }
        }

        EditorGUILayout.Space();

        // 当前选中材质显示
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("选中材质:", GUILayout.Width(80));
            selectedMaterial = EditorGUILayout.ObjectField(selectedMaterial, typeof(Material), false) as Material;
        }

        EditorGUILayout.Space();

        // 材质列表
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        var filteredMaterials = string.IsNullOrEmpty(searchFilter) ? allMaterials : 
            allMaterials.Where(mat => mat.name.ToLower().Contains(searchFilter.ToLower())).ToArray();

        foreach (var material in filteredMaterials)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(AssetPreview.GetMiniThumbnail(material), GUILayout.Width(20), GUILayout.Height(20)))
                {
                    selectedMaterial = material;
                }
                
                if (GUILayout.Button(material.name, "Label"))
                {
                    selectedMaterial = material;
                }
                
                if (selectedMaterial == material)
                {
                    GUILayout.Label("✓", GUILayout.Width(20));
                }
            }
        }
        
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        // 按钮
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("确定", GUILayout.Height(30)))
            {
                if (selectedMaterial != null)
                {
                    onMaterialSelected?.Invoke(selectedMaterial);
                    Close();
                }
                else
                {
                    EditorUtility.DisplayDialog("错误", "请选择一个材质", "确定");
                }
            }

            if (GUILayout.Button("取消", GUILayout.Height(30)))
            {
                Close();
            }
        }
    }
}

