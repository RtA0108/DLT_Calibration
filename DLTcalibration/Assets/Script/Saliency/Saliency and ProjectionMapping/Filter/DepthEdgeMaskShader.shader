Shader "Hidden/DepthEdgeMask"
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

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            float4 frag(Varyings i) : SV_Target
{
    float2 texelSize = 1.0 / _ScreenParams.xy;

    float depthC = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.uv);
    float depthR = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.uv + float2(texelSize.x, 0));
    float depthU = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.uv + float2(0, texelSize.y));

    float dx = abs(depthC - depthR);
    float dy = abs(depthC - depthU);

    float edge = dx + dy;   

    // 강도 100배
    edge *= 100.0;

    // 매우 낮은 threshold
    return edge > 0.001 ? float4(1,1,1,1) : float4(0,0,0,1);
}
            ENDHLSL
        }
    }
    FallBack Off
}