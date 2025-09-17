using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

class HierarchicalZPassOutput
{
    public RTHandle HiZPyramid { get; private set; }

    public HierarchicalZPassOutput()
    {
        ReAllocateIfNeeded(1, 1); // 初始分配一个 1x1 的贴图
    }

    public void ReAllocateIfNeeded(int width, int height)
    {
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
        var mipCount = Mathf.FloorToInt(Mathf.Log(Mathf.Max(width, height), 2f)) + 1;
        var desc = new RenderTextureDescriptor(width, height)
        {
            enableRandomWrite = true,
            dimension = TextureDimension.Tex2D,
            useMipMap = true,
            autoGenerateMips = false,
            graphicsFormat = GraphicsFormat.R32_SFloat,
            mipCount = mipCount,
            msaaSamples = 1
        };
        HiZPyramid = RTHandles.Alloc(desc, name: "HiZ_Pyramid");
    }
}