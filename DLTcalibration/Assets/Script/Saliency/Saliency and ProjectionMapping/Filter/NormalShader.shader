Shader "Hidden/NormalsEdgeMask"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            TEXTURE2D(_CameraNormalsTexture);
            SAMPLER(sampler_CameraNormalsTexture);

            float4 frag(Varyings i) : SV_Target
            {
                // float2 texelSize = 1.0 / _ScreenParams.xy;

                // float3 nC = SAMPLE_TEXTURE2D(_CameraNormalsTexture, sampler_CameraNormalsTexture, i.uv).xyz;
                // float3 nR = SAMPLE_TEXTURE2D(_CameraNormalsTexture, sampler_CameraNormalsTexture, i.uv + float2(texelSize.x, 0)).xyz;
                // float3 nU = SAMPLE_TEXTURE2D(_CameraNormalsTexture, sampler_CameraNormalsTexture, i.uv + float2(0, texelSize.y)).xyz;

                // float dx = length(nC - nR);
                // float dy = length(nC - nU);

                // float edge = dx + dy;

                // return edge > 0.1 ? float4(1, 1, 1, 1) : float4(0, 0, 0, 1);
                float2 texelSize = 1.0 / _ScreenParams.xy;

                float3 nC = SAMPLE_TEXTURE2D(_CameraNormalsTexture, sampler_CameraNormalsTexture, i.uv).xyz;
                float3 nR = SAMPLE_TEXTURE2D(_CameraNormalsTexture, sampler_CameraNormalsTexture, i.uv + float2(texelSize.x, 0)).xyz;
                float3 nU = SAMPLE_TEXTURE2D(_CameraNormalsTexture, sampler_CameraNormalsTexture, i.uv + float2(0, texelSize.y)).xyz;

                float dx = length(nC - nR);
                float dy = length(nC - nU);

                float edge = dx + dy;

                edge *= 3.0; // edge 강도 강화
                return float4(edge, edge, edge, 1); // hard threshold 없음
            }
            ENDHLSL
        }
    }
    FallBack Off
}
