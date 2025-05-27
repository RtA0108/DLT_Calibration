Shader "Hidden/SobelEdgeMask"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            ZTest Always Cull Off ZWrite Off

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float3 c0 = tex2D(_MainTex, i.uv + float2(-_MainTex_TexelSize.x, 0)).rgb;
                float3 c1 = tex2D(_MainTex, i.uv + float2(_MainTex_TexelSize.x, 0)).rgb;
                float3 c2 = tex2D(_MainTex, i.uv + float2(0, -_MainTex_TexelSize.y)).rgb;
                float3 c3 = tex2D(_MainTex, i.uv + float2(0, _MainTex_TexelSize.y)).rgb;
                
                float dx = length(c1 - c0);
                float dy = length(c3 - c2);
                float edge = dx + dy;
                
                // 강도 조절
                edge *= 10.0;
                
                // threshold 낮게
                return edge > 0.005 ? fixed4(1,1,1,1) : fixed4(0,0,0,1);
            }
            ENDCG
        }
    }
    FallBack Off
}