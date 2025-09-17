using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

class HierarchicalZPassOutput
{
    public RTHandle HiZPyramid { get; private set; }
    public int MipCount
    {
        get
        {
            if (HiZPyramid == null) throw new System.InvalidOperationException("HiZ Pyramid not allocated.");
            return HiZPyramid.rt.mipmapCount;
        }
        private set { }
    }

    public HierarchicalZPassOutput()
    {
        ReAllocateIfNeeded(1, 1, () => { return null; }); // 初始分配一个 1x1 的贴图
    }

    public void ReAllocateIfNeeded(int width, int height, Func<RTHandle> createFunc)
    {
        if (createFunc == null) throw new ArgumentNullException("createFunc is null.");
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
}