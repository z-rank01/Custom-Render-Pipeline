#ifndef DISTANCE_DETECTION_INCLUDED
#define DISTANCE_DETECTION_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

half DistanceRelativeRadius(half4 targetWS, half4 occludeeWS, half maxRadius, half maxTargetDistance)
{
    half target2CameraDepth = abs(TransformWorldToView(targetWS).z);
    half object2CenterLineDistance = length(TransformWorldToView(occludeeWS).xy);
    half radius = lerp(maxRadius, 0.01, target2CameraDepth / maxTargetDistance);
    return lerp(0, 1.1, object2CenterLineDistance / radius);
}

half GetProjectionPerpendicularLengthFast(float3 a, float3 b)
{
    half dotAB = dot(a, b);
    half normASq = dot(a, a);
    half normBSq = dot(b, b);
    
    // utilize fma (fused multiply-add)
    half perpLengthSq = normASq - dotAB * dotAB * rcp(normBSq);
    
    return sqrt(max(0.0, perpLengthSq));
}

half CapsuleCast(half4 targetWS, half4 occludeeWS, half radius, half clipThreshold)
{
    // 原本的计算方式
    // // 直接计算到相机的向量
    // half3 occludeeToCamera = occludeeWS.xyz - _WorldSpaceCameraPos.xyz;
    // half3 targetToCamera = targetWS.xyz - _WorldSpaceCameraPos.xyz;
    //
    // // 合并投影计算和垂直距离计算
    // half targetSqrLength = dot(targetToCamera, targetToCamera);
    // half projectionRatio = saturate(dot(occludeeToCamera, targetToCamera) / targetSqrLength);
    //
    // // 直接计算垂直距离的平方，避免 length 计算
    // half3 perpendicular = occludeeToCamera - targetToCamera * projectionRatio;
    // half perpendicularSqrDistance = dot(perpendicular, perpendicular);

    // 优化后的计算方式
    half perpendicularSqrDistance = GetProjectionPerpendicularLengthFast(
        occludeeWS.xyz - _WorldSpaceCameraPos.xyz,
        targetWS.xyz - _WorldSpaceCameraPos.xyz
    );
    
    // 使用平方距离比较，避免开方运算
    half res = smoothstep(0, radius * radius, perpendicularSqrDistance);
    return lerp(res, 1, clipThreshold);
}


#endif
