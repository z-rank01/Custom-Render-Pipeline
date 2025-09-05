using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 自动化的 EditorGUILayout.BeginVertical/EndVertical 包装器
/// </summary>
public class VerticalScope : IDisposable
{
    public VerticalScope(params GUILayoutOption[] options)
    {
        EditorGUILayout.BeginVertical(options);
    }

    public VerticalScope(GUIStyle style, params GUILayoutOption[] options)
    {
        EditorGUILayout.BeginVertical(style, options);
    }

    public VerticalScope(string style, params GUILayoutOption[] options)
    {
        EditorGUILayout.BeginVertical(style, options);
    }

    public void Dispose()
    {
        EditorGUILayout.EndVertical();
    }
}

/// <summary>
/// 自动化的 EditorGUILayout.BeginHorizontal/EndHorizontal 包装器
/// </summary>
public class HorizontalScope : IDisposable
{
    public HorizontalScope(params GUILayoutOption[] options)
    {
        EditorGUILayout.BeginHorizontal(options);
    }

    public HorizontalScope(GUIStyle style, params GUILayoutOption[] options)
    {
        EditorGUILayout.BeginHorizontal(style, options);
    }

    public HorizontalScope(string style, params GUILayoutOption[] options)
    {
        EditorGUILayout.BeginHorizontal(style, options);
    }

    public void Dispose()
    {
        EditorGUILayout.EndHorizontal();
    }
}

/// <summary>
/// 自动化的 EditorGUILayout.BeginScrollView/EndScrollView 包装器
/// </summary>
public class ScrollViewScope : IDisposable
{
    public Vector2 ScrollPosition { get; private set; }

    public ScrollViewScope(ref Vector2 scrollPosition, params GUILayoutOption[] options)
    {
        ScrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, options);
        scrollPosition = ScrollPosition;
    }

    public ScrollViewScope(ref Vector2 scrollPosition, GUIStyle style, params GUILayoutOption[] options)
    {
        ScrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, style, options);
        scrollPosition = ScrollPosition;
    }

    public ScrollViewScope(ref Vector2 scrollPosition, bool alwaysShowHorizontal, bool alwaysShowVertical,
        params GUILayoutOption[] options)
    {
        ScrollPosition =
            EditorGUILayout.BeginScrollView(scrollPosition, alwaysShowHorizontal, alwaysShowVertical, options);
        scrollPosition = ScrollPosition;
    }

    public void Dispose()
    {
        EditorGUILayout.EndScrollView();
    }
}

/// <summary>
/// 自动化的 EditorGUI.BeginChangeCheck/EndChangeCheck 包装器
/// </summary>
public class ChangeCheckScope : IDisposable
{
    public bool Changed { get; private set; }

    public ChangeCheckScope()
    {
        EditorGUI.BeginChangeCheck();
    }

    public void Dispose()
    {
        Changed = EditorGUI.EndChangeCheck();
    }
}

/// <summary>
/// 自动化的 EditorGUI.BeginDisabledGroup/EndDisabledGroup 包装器
/// </summary>
public class DisabledScope : IDisposable
{
    public DisabledScope(bool disabled)
    {
        EditorGUI.BeginDisabledGroup(disabled);
    }

    public void Dispose()
    {
        EditorGUI.EndDisabledGroup();
    }
}

/// <summary>
/// 自动化的缩进级别管理
/// </summary>
public class IndentScope : IDisposable
{
    private readonly int originalIndentLevel;

    public IndentScope(int indentIncrease = 1)
    {
        originalIndentLevel = EditorGUI.indentLevel;
        EditorGUI.indentLevel += indentIncrease;
    }

    public void Dispose()
    {
        EditorGUI.indentLevel = originalIndentLevel;
    }
}

/// <summary>
/// 自动化的 GUI 颜色管理
/// </summary>
public class ColorScope : IDisposable
{
    private readonly Color originalColor;

    public ColorScope(Color color)
    {
        originalColor = GUI.color;
        GUI.color = color;
    }

    public void Dispose()
    {
        GUI.color = originalColor;
    }
}

/// <summary>
/// 自动化的 GUI 背景颜色管理
/// </summary>
public class BackgroundColorScope : IDisposable
{
    private readonly Color originalColor;

    public BackgroundColorScope(Color color)
    {
        originalColor = GUI.backgroundColor;
        GUI.backgroundColor = color;
    }

    public void Dispose()
    {
        GUI.backgroundColor = originalColor;
    }
}

/// <summary>
/// 组合多个 GUI 元素的复合 Scope
/// </summary>
public class CompoundScope : IDisposable
{
    private readonly IDisposable[] scopes;

    public CompoundScope(params IDisposable[] scopes)
    {
        this.scopes = scopes;
    }

    public void Dispose()
    {
        // 反向释放，确保正确的嵌套顺序
        for (int i = scopes.Length - 1; i >= 0; i--)
        {
            scopes[i]?.Dispose();
        }
    }
}