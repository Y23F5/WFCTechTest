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

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        struct Attributes
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
            return output;
        }

        float4 FragRaw(Varyings input) : SV_Target
        {
            float rawDepth = SampleSceneDepth(input.uv);
            return float4(rawDepth, rawDepth, rawDepth, 1.0);
        }

        float4 FragLinear(Varyings input) : SV_Target
        {
            float rawDepth = SampleSceneDepth(input.uv);
            float linearDepth = Linear01Depth(rawDepth, _ZBufferParams);
            return float4(linearDepth, linearDepth, linearDepth, 1.0);
        }
        ENDHLSL

        Pass
        {
            Name "RawDepth"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragRaw
            ENDHLSL
        }

        Pass
        {
            Name "LinearDepth"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragLinear
            ENDHLSL
        }
    }
}
