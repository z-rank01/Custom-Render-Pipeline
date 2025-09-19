using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HierarchicalZPassResources : System.IDisposable
{
    // compute buffers
    private ComputeBuffer _aabbCenterBuffer;
    private ComputeBuffer _aabbExtentBuffer;
    
    // Hierarchical z depth mipmap

    // AABBs and transform matrices
    private Vector3[] _objectCenters;
    private Vector3[] _objectExtents;
    
    // Hi-Z Texture information
    private int _objectCount;

    public int ObjectCount => _objectCount;
    public ComputeBuffer AabbCenterBuffer => _aabbCenterBuffer;
    public ComputeBuffer AabbExtentBuffer => _aabbExtentBuffer;
    
    public HierarchicalZPassResources(Renderer[] renderers)
    {
        _objectCount = renderers.Length;

        // 1. 填充对象相关数组: center, extent, World-Screen Matrix
        _objectCenters = new Vector3[_objectCount];
        _objectExtents = new Vector3[_objectCount];
        for (int i = 0; i < _objectCount; i++)
        {
            _objectExtents[i] = renderers[i].bounds.extents;
            _objectCenters[i] = renderers[i].bounds.center;
        }
        
        // 2. 分配或重建 Buffers
        AllocateOrResizeBuffers(_objectCount);
        
        // 3. 重新上传 Buffers 数据
        UploadObjectData();
    }

    #region Interface

    // 更新（例如场景中对象移动/增减）
    public void UpdateObjects(Renderer[] renderers)
    {
        _objectCount = renderers.Length;
        _objectCenters = new Vector3[_objectCount];
        _objectExtents = new Vector3[_objectCount];

        for (int i = 0; i < _objectCount; i++)
        {
            var r = renderers[i];
            var bounds = r.bounds;
            _objectCenters[i] = bounds.center;
            _objectExtents[i] = bounds.extents;
        }

        AllocateOrResizeBuffers(_objectCount);
        UploadObjectData();
    }

    #endregion

    // 分配或重建所有 compute buffers
    private void AllocateOrResizeBuffers(int count)
    {
        ReleaseBuffers();
        if (count == 0) return;
        _aabbCenterBuffer = new ComputeBuffer(count, sizeof(float) * 3, ComputeBufferType.Structured);
        _aabbExtentBuffer = new ComputeBuffer(count, sizeof(float) * 3, ComputeBufferType.Structured);
    }

    // 上传 CPU 缓存到 GPU
    private void UploadObjectData()
    {
        if (_objectCount == 0) return;
        _aabbCenterBuffer.SetData(_objectCenters);
        _aabbExtentBuffer.SetData(_objectExtents);
    }

    private void ReleaseBuffers()
    {
        _aabbCenterBuffer?.Dispose(); _aabbCenterBuffer = null;
        _aabbExtentBuffer?.Dispose(); _aabbExtentBuffer = null;
    }

    private void Release()
    {
        // release compute buffers
        ReleaseBuffers();
    }

    public void Dispose() => Release();
    ~HierarchicalZPassResources() { Release(); }
}