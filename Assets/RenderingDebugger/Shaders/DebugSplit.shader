Shader "RenderingDebugger/DepthSplit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    // 深度纹理由URP自动传递，通过DeclareDepthTexture.hlsl获取
    float _DebugDisplayHeightRatio; // 控制深度信息显示的高度比例
    float _DebugScreenWidth;
    float _DebugScreenHeight;

    struct Attributes
    {
        float4 positionOS : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    Varyings vert(Attributes input)
    {
        Varyings output;
        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        output.uv = input.uv;
        return output;
    }

    float4 frag(Varyings input) : SV_Target
    {
        float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

        float width = _DebugDisplayHeightRatio * _DebugScreenWidth;
        float height = _DebugDisplayHeightRatio * _DebugScreenHeight;
        float normalizedSizeX = width / _DebugScreenWidth;
        float normalizedSizeY = height / _DebugScreenHeight;

        // 只在屏幕底部的特定区域显示深度信息
        float2 uvOffset = half2(input.uv.x - (1 - normalizedSizeX), input.uv.y - (1 - normalizedSizeY));

        if ((uvOffset.x >= 0) && (uvOffset.x < normalizedSizeX) &&
            (uvOffset.y >= 0) && (uvOffset.y < normalizedSizeY))
        {
            // 从深度缓冲区采样
            float depth = SampleSceneDepth(input.uv);

            // 将深度值线性化 (1.0为远平面，0.0为近平面)
            float linearDepth = LinearEyeDepth(depth, _ZBufferParams);

            // 缩放深度值以增强可视化效果 (较远的物体为亮色，较近的物体为暗色)
            linearDepth = saturate(linearDepth / 20.0); // 可以调整这个除数来改变深度范围

            // 使用不同颜色通道表示不同深度范围，便于更好地可视化
            float3 depthColor;

            // 使用热度图着色深度 (深->近: 蓝->青->绿->黄->红)
            depthColor.r = smoothstep(0.5, 1.0, linearDepth);
            depthColor.g = smoothstep(0.0, 0.5, linearDepth) * (1.0 - smoothstep(0.5, 1.0, linearDepth));
            depthColor.b = 1.0 - smoothstep(0.0, 0.5, linearDepth);

            // 在深度区域添加网格线，便于更好地理解深度值
            float gridSize = 0.05;
            float gridX = step(frac(input.uv.x / gridSize), 0.01);
            float gridY = step(frac(input.uv.y / gridSize), 0.01);
            float grid = max(gridX, gridY) * 0.2;

            // 将深度信息叠加到原始颜色上
            color.rgb = lerp(depthColor, float3(1, 1, 1), grid);
            color.a = 1.0;
        }

        return color;
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"
        }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "Depth Debug"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}