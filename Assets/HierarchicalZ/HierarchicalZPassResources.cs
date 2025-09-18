using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HierarchicalZPassResources : System.IDisposable
{
    // compute buffers
    private ComputeBuffer _aabbCenterBuffer;
    private ComputeBuffer _aabbExtentBuffer;
    private ComputeBuffer _objectTransformBuffer;
    private ComputeBuffer _appendBuffer;
    private ComputeBuffer _visibilityResultBuffer;   // 每对象一个 int (0/1)
    
    // Hierarchical z depth mipmap

    // AABBs and transform matrices
    private Vector3[] _objectCenters;
    private Vector3[] _objectExtents;
    private Matrix4x4[] _objectToWorldMatrices;
    private Matrix4x4 _worldToCameraMatrices;
    
    // Hi-Z Texture information
    private int _objectCount;

    public int ObjectCount => _objectCount;
    public ComputeBuffer AabbCenterBuffer => _aabbCenterBuffer;
    public ComputeBuffer AabbExtentBuffer => _aabbExtentBuffer;
    public ComputeBuffer ObjectTransformBuffer => _objectTransformBuffer;
    public ComputeBuffer VisibleResultBuffer => _visibilityResultBuffer;
    public ComputeBuffer AppendBuffer => _appendBuffer;
    public Matrix4x4[] ObjectToWorldMatrices => _objectToWorldMatrices;
    public Matrix4x4 WorldToCameraMatrix => _worldToCameraMatrices;
    
    public HierarchicalZPassResources(int width, int height, Renderer[] renderers, Camera camera)
    {
        _objectCount = renderers.Length;

        // 1. 填充对象相关数组: center, extent, World-Screen Matrix
        _objectCenters = new Vector3[_objectCount];
        _objectExtents = new Vector3[_objectCount];
        _objectToWorldMatrices = new Matrix4x4[_objectCount];
        _worldToCameraMatrices = camera.worldToCameraMatrix;
        for (int i = 0; i < _objectCount; i++)
        {
            _objectToWorldMatrices[i] = renderers[i].transform.localToWorldMatrix;
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
    public void UpdateObjects(Renderer[] renderers, Camera camera)
    {
        _objectCount = renderers.Length;
        _objectCenters = new Vector3[_objectCount];
        _objectExtents = new Vector3[_objectCount];
        _objectToWorldMatrices = new Matrix4x4[_objectCount];
        _worldToCameraMatrices = camera.worldToCameraMatrix;

        for (int i = 0; i < _objectCount; i++)
        {
            var r = renderers[i];
            _objectToWorldMatrices[i] = r.transform.localToWorldMatrix;
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
        _objectTransformBuffer = new ComputeBuffer(count, sizeof(float) * 16, ComputeBufferType.Structured);
        _visibilityResultBuffer = new ComputeBuffer(count, sizeof(int), ComputeBufferType.Structured);
        _appendBuffer = new ComputeBuffer(count, sizeof(uint), ComputeBufferType.Append);
        _appendBuffer.SetCounterValue(0);
    }

    // 上传 CPU 缓存到 GPU
    private void UploadObjectData()
    {
        if (_objectCount == 0) return;
        _aabbCenterBuffer.SetData(_objectCenters);
        _aabbExtentBuffer.SetData(_objectExtents);
        _objectTransformBuffer.SetData(_objectToWorldMatrices);

        // 结果缓冲初始化为可见(或 0 表示未判定，按需求)
        int[] init = new int[_objectCount];
        _visibilityResultBuffer.SetData(init);
    }

    private void ReleaseBuffers()
    {
        _aabbCenterBuffer?.Dispose(); _aabbCenterBuffer = null;
        _aabbExtentBuffer?.Dispose(); _aabbExtentBuffer = null;
        _objectTransformBuffer?.Dispose(); _objectTransformBuffer = null;
        _visibilityResultBuffer?.Dispose(); _visibilityResultBuffer = null;
        _appendBuffer?.Dispose(); _appendBuffer = null;
    }

    private void Release()
    {
        // release compute buffers
        ReleaseBuffers();
    }

    public void Dispose() => Release();
    ~HierarchicalZPassResources() { Release(); }
}