using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 按材质属性筛选的筛选器
/// </summary>
public class PropertyFilter : IMaterialFilter
{
    public enum PropertyType
    {
        All, // 所有类型
        Texture, // 纹理属性
        Color, // 颜色属性
        Float, // 浮点数属性
        Vector, // 向量属性
        Integer, // 整数属性
        Keyword // 关键字
    }

    public enum CompareMode
    {
        HasProperty, // 仅检查是否有该属性
        Equals, // 等于指定值
        NotEquals, // 不等于指定值
        GreaterThan, // 大于指定值（仅数值）
        LessThan // 小于指定值（仅数值）
    }

    private PropertyType selectedPropertyType = PropertyType.All;
    private string propertyName = "";
    private bool isEnabled = false;
    private CompareMode compareMode = CompareMode.HasProperty;

    // 比较值
    private float floatValue = 0f;
    private int intValue = 0;
    private Color colorValue = Color.white;
    private string textureNameValue = "";
    private Vector4 vectorValue = Vector4.zero;
    private bool keywordValue = false;

    public string FilterName => "属性筛选器";

    public string FilterDescription =>
        isEnabled ? $"筛选具有属性 '{propertyName}' 的材质 ({selectedPropertyType}, {compareMode})" : "未启用";

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public List<MaterialUsageInfo> Filter(List<MaterialUsageInfo> materials)
    {
        if (!IsEnabled || string.IsNullOrEmpty(propertyName))
            return materials;

        return materials.Where(usage => HasProperty(usage.Material)).ToList();
    }

    public void DrawFilterUI()
    {
        using (new VerticalScope("box"))
        {
            EditorGUILayout.LabelField("属性筛选器", EditorStyles.boldLabel);

            isEnabled = EditorGUILayout.Toggle("启用筛选器", isEnabled);

            if (isEnabled)
            {
                propertyName = EditorGUILayout.TextField("属性名称", propertyName);
                selectedPropertyType = (PropertyType)EditorGUILayout.EnumPopup("属性类型", selectedPropertyType);
                compareMode = (CompareMode)EditorGUILayout.EnumPopup("比较模式", compareMode);

                // 根据属性类型和比较模式显示相应的输入控件
                if (compareMode != CompareMode.HasProperty)
                {
                    DrawValueInputs();
                }

                if (!string.IsNullOrEmpty(propertyName))
                {
                    EditorGUILayout.HelpBox($"将筛选具有属性 '{propertyName}' 的材质", MessageType.Info);
                }
            }
        }
    }

    private void DrawValueInputs()
    {
        using (new IndentScope())
        {
            switch (selectedPropertyType)
            {
                case PropertyType.Float:
                    if (compareMode == CompareMode.Equals || compareMode == CompareMode.NotEquals ||
                        compareMode == CompareMode.GreaterThan || compareMode == CompareMode.LessThan)
                    {
                        floatValue = EditorGUILayout.FloatField("浮点值", floatValue);
                    }

                    break;

                case PropertyType.Integer:
                    if (compareMode == CompareMode.Equals || compareMode == CompareMode.NotEquals ||
                        compareMode == CompareMode.GreaterThan || compareMode == CompareMode.LessThan)
                    {
                        intValue = EditorGUILayout.IntField("整数值", intValue);
                    }

                    break;

                case PropertyType.Color:
                    if (compareMode == CompareMode.Equals || compareMode == CompareMode.NotEquals)
                    {
                        colorValue = EditorGUILayout.ColorField("颜色值", colorValue);
                    }

                    break;

                case PropertyType.Vector:
                    if (compareMode == CompareMode.Equals || compareMode == CompareMode.NotEquals)
                    {
                        vectorValue = EditorGUILayout.Vector4Field("向量值", vectorValue);
                    }

                    break;

                case PropertyType.Texture:
                    if (compareMode == CompareMode.Equals || compareMode == CompareMode.NotEquals)
                    {
                        textureNameValue = EditorGUILayout.TextField("纹理名称", textureNameValue);
                        EditorGUILayout.HelpBox("留空表示检查是否为 null", MessageType.Info);
                    }

                    break;

                case PropertyType.Keyword:
                    if (compareMode == CompareMode.Equals || compareMode == CompareMode.NotEquals)
                    {
                        keywordValue = EditorGUILayout.Toggle("关键字状态", keywordValue);
                    }

                    break;
            }
        }
    }

    public void Reset()
    {
        selectedPropertyType = PropertyType.All;
        propertyName = "";
        isEnabled = false;
        compareMode = CompareMode.HasProperty;
        floatValue = 0f;
        intValue = 0;
        colorValue = Color.white;
        textureNameValue = "";
        vectorValue = Vector4.zero;
        keywordValue = false;
    }

    private bool HasProperty(Material material)
    {
        if (material == null || material.shader == null)
            return false;

        switch (selectedPropertyType)
        {
            case PropertyType.All:
                return HasAnyProperty(material);
            case PropertyType.Texture:
                return CheckTextureProperty(material);
            case PropertyType.Color:
                return CheckColorProperty(material);
            case PropertyType.Float:
                return CheckFloatProperty(material);
            case PropertyType.Vector:
                return CheckVectorProperty(material);
            case PropertyType.Integer:
                return CheckIntegerProperty(material);
            case PropertyType.Keyword:
                return CheckKeywordProperty(material);
            default:
                return false;
        }
    }

    private bool HasAnyProperty(Material material)
    {
        if (compareMode == CompareMode.HasProperty)
        {
            return material.HasProperty(propertyName) || material.IsKeywordEnabled(propertyName);
        }

        return false;
    }

    private bool CheckTextureProperty(Material material)
    {
        if (!material.HasProperty(propertyName))
            return false;

        if (compareMode == CompareMode.HasProperty)
        {
            try
            {
                material.GetTexture(propertyName);
                return true;
            }
            catch
            {
                return false;
            }
        }
        else if (compareMode == CompareMode.Equals || compareMode == CompareMode.NotEquals)
        {
            try
            {
                Texture texture = material.GetTexture(propertyName);
                string textureName = texture != null ? texture.name : "";
                bool isEqual = string.IsNullOrEmpty(textureNameValue)
                    ? texture == null
                    : textureName.Equals(textureNameValue, System.StringComparison.OrdinalIgnoreCase);

                return compareMode == CompareMode.Equals ? isEqual : !isEqual;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private bool CheckColorProperty(Material material)
    {
        if (!material.HasProperty(propertyName))
            return false;

        if (compareMode == CompareMode.HasProperty)
        {
            try
            {
                material.GetColor(propertyName);
                return true;
            }
            catch
            {
                return false;
            }
        }
        else if (compareMode == CompareMode.Equals || compareMode == CompareMode.NotEquals)
        {
            try
            {
                Color materialColor = material.GetColor(propertyName);
                bool isEqual = Mathf.Approximately(materialColor.r, colorValue.r) &&
                               Mathf.Approximately(materialColor.g, colorValue.g) &&
                               Mathf.Approximately(materialColor.b, colorValue.b) &&
                               Mathf.Approximately(materialColor.a, colorValue.a);

                return compareMode == CompareMode.Equals ? isEqual : !isEqual;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private bool CheckFloatProperty(Material material)
    {
        if (!material.HasProperty(propertyName))
            return false;

        if (compareMode == CompareMode.HasProperty)
        {
            try
            {
                material.GetFloat(propertyName);
                return true;
            }
            catch
            {
                return false;
            }
        }
        else
        {
            try
            {
                float materialValue = material.GetFloat(propertyName);

                switch (compareMode)
                {
                    case CompareMode.Equals:
                        return Mathf.Approximately(materialValue, floatValue);
                    case CompareMode.NotEquals:
                        return !Mathf.Approximately(materialValue, floatValue);
                    case CompareMode.GreaterThan:
                        return materialValue > floatValue;
                    case CompareMode.LessThan:
                        return materialValue < floatValue;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }
    }

    private bool CheckVectorProperty(Material material)
    {
        if (!material.HasProperty(propertyName))
            return false;

        if (compareMode == CompareMode.HasProperty)
        {
            try
            {
                material.GetVector(propertyName);
                return true;
            }
            catch
            {
                return false;
            }
        }
        else if (compareMode == CompareMode.Equals || compareMode == CompareMode.NotEquals)
        {
            try
            {
                Vector4 materialVector = material.GetVector(propertyName);
                bool isEqual = Mathf.Approximately(materialVector.x, vectorValue.x) &&
                               Mathf.Approximately(materialVector.y, vectorValue.y) &&
                               Mathf.Approximately(materialVector.z, vectorValue.z) &&
                               Mathf.Approximately(materialVector.w, vectorValue.w);

                return compareMode == CompareMode.Equals ? isEqual : !isEqual;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private bool CheckIntegerProperty(Material material)
    {
        if (!material.HasProperty(propertyName))
            return false;

        if (compareMode == CompareMode.HasProperty)
        {
            try
            {
                material.GetInt(propertyName);
                return true;
            }
            catch
            {
                return false;
            }
        }
        else
        {
            try
            {
                int materialValue = material.GetInt(propertyName);

                switch (compareMode)
                {
                    case CompareMode.Equals:
                        return materialValue == intValue;
                    case CompareMode.NotEquals:
                        return materialValue != intValue;
                    case CompareMode.GreaterThan:
                        return materialValue > intValue;
                    case CompareMode.LessThan:
                        return materialValue < intValue;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }
    }

    private bool CheckKeywordProperty(Material material)
    {
        if (compareMode == CompareMode.HasProperty)
        {
            return material.IsKeywordEnabled(propertyName);
        }
        else if (compareMode == CompareMode.Equals || compareMode == CompareMode.NotEquals)
        {
            bool isEnabled = material.IsKeywordEnabled(propertyName);
            return compareMode == CompareMode.Equals ? isEnabled == keywordValue : isEnabled != keywordValue;
        }

        return false;
    }
}