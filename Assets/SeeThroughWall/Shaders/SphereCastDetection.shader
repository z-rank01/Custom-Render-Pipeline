Shader "SeeThroughWall/SphereCastDetection"
{
    Properties
    {
        _DitherTex ("Dither Texture", 2D) = "white" {}
        _TargetWorldPosition ("Target World Position", Vector) = (0, 0, 0, 0)
        _DitherScale ("Dither Scale", Float) = 1.0
        _DistanceThreshold ("Distance Threshold", Float) = 10
        _DitherCircleMaxRadius ("Dither Circle Max Radius", Float) = 10
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

            struct appdata
            {
                float4 positionOS : POSITION;
            };

            struct v2f
            {
                half4 positionCS : SV_POSITION;
                half4 positionWS : TEXCOORD0;
                half4 positionVS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TargetWorldPosition;
                half _DitherScale;
                half _DistanceThreshold;
                half _DitherCircleMaxRadius;
            CBUFFER_END

            TEXTURE2D(_DitherTex);
            SAMPLER(sampler_DitherTex);

            half DistanceRelativeRadius(half4 targetWorldPosition, half4 objectWorldPosition, half maxRadius, half maxTargetDistance)
            {
                half target2CameraDepth = abs(TransformWorldToView(targetWorldPosition).z);
                half object2CenterLineDistance = length(TransformWorldToView(objectWorldPosition).xy);
                half radius = lerp(maxRadius, 0, target2CameraDepth / maxTargetDistance);
                return lerp(0, 1.1, object2CenterLineDistance / radius);
            }

            v2f vert(appdata v)
            {
                v2f o;
                VertexPositionInputs vertexInputs = GetVertexPositionInputs(v.positionOS);
                o.positionCS = vertexInputs.positionCS;
                o.positionWS.xyz = vertexInputs.positionWS;
                o.positionVS.xyz = vertexInputs.positionVS;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half ditherThreshold = DistanceRelativeRadius(_TargetWorldPosition, i.positionWS, _DitherCircleMaxRadius, _DistanceThreshold);
                half2 uv = i.positionCS.xy * _DitherScale;
                half d = SAMPLE_TEXTURE2D(_DitherTex, sampler_PointRepeat, uv).a;
                clip(ditherThreshold - d);
                return half4(1, 1, 1, 1);
            }
            ENDHLSL
        }
    }
}