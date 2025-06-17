// Assets/Shaders/Include/OverdrawDetection.hlsl
#ifndef OVERDRAW_DETECTION_INCLUDED
#define OVERDRAW_DETECTION_INCLUDED

#if defined(SHADER_API_D3D11) || defined(SHADER_API_D3D12) || defined(SHADER_API_VULKAN) || defined(SHADER_API_METAL) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3)
    #define OVERDRAW_DETECTION_SUPPORTED
#endif

#ifdef OVERDRAW_DETECTION_SUPPORTED
    #if defined(SHADER_API_D3D11) || defined(SHADER_API_D3D12)
        uniform RWStructuredBuffer<uint> _OverdrawCounters : register(u1);
    #elif defined(SHADER_API_VULKAN)
        layout(set = 0, binding = 1, std430) restrict buffer OverdrawCounters
        {
            uint _OverdrawCounters[];
        };
    #elif defined(SHADER_API_METAL)
        device atomic_uint* _OverdrawCounters [[buffer(1)]];
    #elif defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3)
        layout(binding = 1, std430) restrict buffer OverdrawCounters
        {
            uint _OverdrawCounters[];
        };
    #endif
#endif

int _EnableOverdrawDetection;
uint _OverdrawScreenWidth;
uint _OverdrawScreenHeight;

#define RECORD_OVERDRAW_SIMPLE(svPosition) \
    [branch] \
    if (_EnableOverdrawDetection > 0) \
    { \
        RecordOverdrawSimple(svPosition); \
    }

void RecordOverdrawSimple(float4 svPosition)
{
#ifdef OVERDRAW_DETECTION_SUPPORTED
    uint2 pixelCoord = uint2(svPosition.xy);
    uint x = pixelCoord.x;
    uint y = pixelCoord.y;

    #if UNITY_UV_STARTS_AT_TOP
        uint yCoord = y;
    #else
        uint yCoord = _OverdrawScreenHeight - 1 - y;
    #endif
    
    if (x < _OverdrawScreenWidth && yCoord < _OverdrawScreenHeight)
    {
        uint index = yCoord * _OverdrawScreenWidth + x;
        
        #if defined(SHADER_API_D3D11) || defined(SHADER_API_D3D12) || defined(SHADER_API_VULKAN) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3)
            InterlockedAdd(_OverdrawCounters[index], 1);
        #elif defined(SHADER_API_METAL)
            atomic_fetch_add_explicit(&_OverdrawCounters[index], 1, memory_order_relaxed);
        #endif
    }
#endif
}

#endif // OVERDRAW_DETECTION_INCLUDED