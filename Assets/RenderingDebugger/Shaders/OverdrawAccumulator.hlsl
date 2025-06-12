// Assets/Shaders/Include/OverdrawDetection.hlsl
#ifndef OVERDRAW_DETECTION_INCLUDED
#define OVERDRAW_DETECTION_INCLUDED

// 检查是否支持原子操作
#if defined(SHADER_API_D3D11) // && (SHADER_TARGET >= 50)
    #define OVERDRAW_DETECTION_SUPPORTED
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
    if (pixelCoord.x < _OverdrawScreenWidth && pixelCoord.y < _OverdrawScreenHeight)
    {
        uint index = pixelCoord.y * _OverdrawScreenWidth + pixelCoord.x;
        InterlockedAdd(_OverdrawCounters[index], 1);
    }
#endif
}

#endif // OVERDRAW_DETECTION_INCLUDED