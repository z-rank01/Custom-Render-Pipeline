using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 材质筛选器接口
/// 所有材质筛选器都应实现此接口
/// </summary>
public interface IMaterialFilter
{
    /// <summary>
    /// 筛选器名称
    /// </summary>
    string FilterName { get; }

    /// <summary>
    /// 筛选器描述
    /// </summary>
    string FilterDescription { get; }

    /// <summary>
    /// 是否启用筛选器
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// 对材质列表进行筛选
    /// </summary>
    /// <param name="materials">输入的材质列表</param>
    /// <returns>筛选后的材质列表</returns>
    List<MaterialUsageInfo> Filter(List<MaterialUsageInfo> materials);

    /// <summary>
    /// 绘制筛选器的UI配置界面
    /// </summary>
    void DrawFilterUI();

    /// <summary>
    /// 重置筛选器设置
    /// </summary>
    void Reset();
}