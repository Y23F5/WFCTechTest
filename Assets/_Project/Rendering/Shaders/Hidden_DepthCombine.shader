Shader "Hidden/DepthCombine"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "DepthCombine"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Depth0); SAMPLER(sampler_Depth0);
            TEXTURE2D(_Depth1); SAMPLER(sampler_Depth1);
            TEXTURE2D(_Depth2); SAMPLER(sampler_Depth2);
            TEXTURE2D(_Depth3); SAMPLER(sampler_Depth3);

            struct Attributes { uint vertexID : SV_VertexID; };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.pos = GetFullScreenTriangleVertexPosition(i.vertexID);
                o.uv  = GetFullScreenTriangleTexCoord(i.vertexID);
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                return float4(
                    SAMPLE_TEXTURE2D(_Depth0, sampler_Depth0, i.uv).r,  // R = Camera1
                    SAMPLE_TEXTURE2D(_Depth1, sampler_Depth1, i.uv).r,  // G = Camera2
                    SAMPLE_TEXTURE2D(_Depth2, sampler_Depth2, i.uv).r,  // B = Camera3
                    SAMPLE_TEXTURE2D(_Depth3, sampler_Depth3, i.uv).r   // A = Camera4
                );
            }
            ENDHLSL
        }
    }
}
