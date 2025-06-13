// Assets/Shaders/Include/OverdrawDetection.hlsl
#ifndef OVERDRAW_DETECTION_INCLUDED
#define OVERDRAW_DETECTION_INCLUDED

// 检查是否支持原子操作
#ifdef SHADER_API_D3D11
#define OVERDRAW_DETECTION_SUPPORTED
uniform RWStructuredBuffer<uint> _OverdrawCounters : register(u1);
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
    uint x = svPosition.x;
    uint y = svPosition.y;

    #if UNITY_UV_STARTS_AT_TOP
        uint yCoord = pixelCoord.y;
    #else
        uint yCoord = _OverdrawScreenHeight - 1 - pixelCoord.y;
    #endif
    
    if (pixelCoord.x < _OverdrawScreenWidth && pixelCoord.y < _OverdrawScreenHeight)
    {
        // uint index = pixelCoord.y * _OverdrawScreenWidth + pixelCoord.x;
        uint index = y *  _OverdrawScreenWidth + x;
        InterlockedAdd(_OverdrawCounters[index], 1);
    }
#endif
}

#endif // OVERDRAW_DETECTION_INCLUDED