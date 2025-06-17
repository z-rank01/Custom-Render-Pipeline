// Assets/Shaders/Include/OverdrawDetection.hlsl
#ifndef OVERDRAW_DETECTION_INCLUDED
#define OVERDRAW_DETECTION_INCLUDED

#if defined(SHADER_API_D3D11) || defined(SHADER_API_D3D12) || defined(SHADER_API_VULKAN) || defined(SHADER_API_METAL) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3)
    #define OVERDRAW_DETECTION_SUPPORTED
#endif

#ifdef OVERDRAW_DETECTION_SUPPORTED
    RWStructuredBuffer<uint> _OverdrawCounters : register(u1);
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
    
    if (x < _OverdrawScreenWidth && y < _OverdrawScreenHeight)
    {
        uint index = y * _OverdrawScreenWidth + x;
        InterlockedAdd(_OverdrawCounters[index], 1);
    }
#endif
}

#endif // OVERDRAW_DETECTION_INCLUDED