Shader "Hidden/SceneDepthCapture"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #pragma target 3.0
        #pragma multi_compile _ DEBUG_SOLID

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        struct Attributes { uint vertexID : SV_VertexID; };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv         : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings o;
            o.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            o.uv         = GetFullScreenTriangleTexCoord(input.vertexID);
            return o;
        }

        float4 Frag(Varyings i) : SV_Target
        {
            #if defined(DEBUG_SOLID)
                // DEBUG：写 0.5 验证 pass 是否在运行
                // 结果全 0 → pass 未执行；看到 0.5 → pass 在跑但深度贴图有问题
                return float4(0.5, 0.5, 0.5, 0.5);
            #else
                float d = SampleSceneDepth(i.uv);
                return float4(d, d, d, d);  // R32F RT 只读 .r 通道
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "DepthCapture"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
