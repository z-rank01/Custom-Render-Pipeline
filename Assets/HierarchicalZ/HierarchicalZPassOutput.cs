using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

class HierarchicalZPassOutput
{
    public RTHandle HiZPyramid { get; private set; }
    public ComputeBuffer VisibleResultBuffer { get; private set; }
    public ComputeBuffer AppendBuffer { get; private set; }
    public int MipCount => HiZPyramid == null ? throw new System.InvalidOperationException("HiZ Pyramid not allocated.") : HiZPyramid.rt.mipmapCount;


    /// <summary>
    /// 根据需要重新分配 Hi-Z 金字塔贴图
    /// </summary>
    /// <param name="width">RT 长</param>
    /// <param name="height">RT 高</param>
    /// <param name="createFunc">RT 创建方法。此方法强制在 UPR 管线的正确时机创建，防止不断创建导致内存泄露</param>
    /// <exception cref="ArgumentNullException">创建方法为空</exception>
    public void ReAllocateIfNeeded(int width, int height, Func<RTHandle> createFunc)
    {
        if (createFunc == null) throw new ArgumentNullException($"{nameof(createFunc)} is null.");
        // 如果已经存在且尺寸匹配则无需重新创建
        bool needsReallocate = HiZPyramid == null || HiZPyramid.rt.width != width || HiZPyramid.rt.height != height;
        if (!needsReallocate)
        {
#if UNITY_EDITOR
            Debug.Log($"HiZ Pyramid already allocated with matching size: {width}x{height}");
#endif
            return;
        }

        // 释放旧资源
        if (HiZPyramid != null)
        {
            RTHandles.Release(HiZPyramid);
            HiZPyramid = null;
        }

        // 创建 Hi-Z 金字塔
        HiZPyramid = createFunc();  // RT 的分配必须保证在 OnCameraSetup 内，否则容易出现不断分配导致的内存泄漏
    }
    
    public void AllocateOrResizeBuffers(int objectCount)
    {
        // 1. 分配或重建 Buffers
        if (VisibleResultBuffer == null || VisibleResultBuffer.count < objectCount)
        {
            VisibleResultBuffer?.Release();
            VisibleResultBuffer = new ComputeBuffer(objectCount, sizeof(int), ComputeBufferType.IndirectArguments);
        }
        
        if (AppendBuffer == null || AppendBuffer.count < objectCount)
        {
            AppendBuffer?.Release();
            AppendBuffer = new ComputeBuffer(objectCount, sizeof(int), ComputeBufferType.Append);
            AppendBuffer.SetCounterValue(0);
        }
    }

    public void UploadVisibleBufferData(int objectCount)
    {
        if (VisibleResultBuffer == null || VisibleResultBuffer.count < objectCount)
            throw new InvalidOperationException("VisibleResultBuffer not allocated or too small.");
        var init = new int[objectCount];
        VisibleResultBuffer.SetData(init);
    }
    
    public void ResetAppendBufferData()
    {
        if (AppendBuffer == null)
            throw new InvalidOperationException("AppendBuffer not allocated.");
        AppendBuffer.SetCounterValue(0);
    }
    
    public void ReleaseBuffers()
    {
        VisibleResultBuffer?.Release();
        VisibleResultBuffer = null;
        
        AppendBuffer?.Release();
        AppendBuffer = null;
    }
}