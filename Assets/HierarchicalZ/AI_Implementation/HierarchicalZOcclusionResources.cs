using System;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace CustomRenderPipeline.HiZ
{
    /// <summary>
    /// Hi-Z / 遮挡剔除所需 GPU 资源的简单封装。
    /// 负责：
    /// 1. Hi-Z RenderTexture（RFloat / mip chain）。
    /// 2. Bounds Center / Extent StructuredBuffer。
    /// 3. 结果可见实例索引 AppendBuffer。
    /// 4. Indirect Args Buffer。
    /// </summary>
    public class HierarchicalZOcclusionResources : IDisposable
    {
        public RenderTexture hiZTexture { get; private set; }
        public ComputeBuffer boundsCenterBuffer { get; private set; }
        public ComputeBuffer boundsExtentBuffer { get; private set; }
        public ComputeBuffer visibleInstancesBuffer { get; private set; }
        public ComputeBuffer argsBuffer { get; private set; }

        private int _capacity;

        // CPU 端缓存，减少 GC (中心 + 半尺寸便于 AABB)
        public Vector3[] centersCache;
        public Vector3[] extentsCache;

        public HierarchicalZOcclusionResources(int capacity)
        {
            _capacity = Mathf.Max(1, capacity);
            AllocateCPUCache(_capacity);
        }

        private void AllocateCPUCache(int cap)
        {
            centersCache = new Vector3[cap];
            extentsCache = new Vector3[cap];
        }

        public void ReallocateInstanceBufferIfNeeded(int needed)
        {
            if (needed <= 0) needed = 1;
            if (needed > _capacity)
            {
                ReleaseInstanceBuffers();
                _capacity = Mathf.NextPowerOfTwo(needed);
                AllocateCPUCache(_capacity);
            }

            int stride = sizeof(float) * 3; // Vector3
            if (boundsCenterBuffer == null || boundsCenterBuffer.count < _capacity)
            {
                boundsCenterBuffer?.Release();
                boundsCenterBuffer = new ComputeBuffer(_capacity, stride, ComputeBufferType.Structured);
            }
            if (boundsExtentBuffer == null || boundsExtentBuffer.count < _capacity)
            {
                boundsExtentBuffer?.Release();
                boundsExtentBuffer = new ComputeBuffer(_capacity, stride, ComputeBufferType.Structured);
            }
            if (visibleInstancesBuffer == null || visibleInstancesBuffer.count < _capacity)
            {
                visibleInstancesBuffer?.Release();
                visibleInstancesBuffer = new ComputeBuffer(_capacity, sizeof(uint), ComputeBufferType.Append);
            }
            if (argsBuffer == null)
            {
                // args: [indexCountPerInst, instanceCount, startIndex, baseVertex, startInstance]
                argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
            }
        }

        public void ReallocateHiZTextureIfNeeded(CommandBuffer cmd, int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            bool needRecreate = false;
            if (hiZTexture == null)
            {
                needRecreate = true;
            }
            else if (hiZTexture.width != width || hiZTexture.height != height)
            {
                hiZTexture.Release();
                UnityEngine.Object.DestroyImmediate(hiZTexture);
                needRecreate = true;
            }

            if (needRecreate)
            {
                int maxDim = Mathf.Max(width, height);
                int mipCount = (int)Mathf.Floor(Mathf.Log(maxDim, 2)) + 1;
                var rt = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
                {
                    name = "HiZTexture",
                    enableRandomWrite = true,
                    useMipMap = true,
                    autoGenerateMips = false,
                    volumeDepth = 1,
                    dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
                    filterMode = FilterMode.Point,
                };
                Assert.IsTrue(rt.Create(), "Failed to create HiZ RenderTexture.");
                hiZTexture = rt;
            }
        }

        public void PrepareArgsBuffer(Mesh mesh)
        {
            if (mesh == null) return;
            uint indexCount = mesh != null ? (uint)mesh.GetIndexCount(0) : 0u;
            // 初始化：实例数=0（由后续 CopyCount 写入），其它按照简化假设
            var args = new uint[5];
            args[0] = indexCount;
            args[1] = 0; // 待写入可见实例数
            args[2] = mesh != null ? (uint)mesh.GetIndexStart(0) : 0u;
            args[3] = mesh != null ? (uint)mesh.GetBaseVertex(0) : 0u;
            args[4] = 0;
            argsBuffer.SetData(args);
        }

        private void ReleaseInstanceBuffers()
        {
            boundsCenterBuffer?.Release(); boundsCenterBuffer = null;
            boundsExtentBuffer?.Release(); boundsExtentBuffer = null;
            visibleInstancesBuffer?.Release(); visibleInstancesBuffer = null;
            argsBuffer?.Release(); argsBuffer = null;
        }

        public void Dispose()
        {
            ReleaseInstanceBuffers();
            if (hiZTexture)
            {
                hiZTexture.Release();
                UnityEngine.Object.DestroyImmediate(hiZTexture);
                hiZTexture = null;
            }
        }
    }
}
