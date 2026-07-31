Shader "BlockCrusher/TwoTextureOverlay"
{
    Properties
    {
        _MainTex ("Texture 1 (Bottom)", 2D) = "white" {}
        _OverlayTex ("Texture 2 (Top)", 2D) = "white" {}
        _OverlayOpacity ("Texture 2 Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Cull Off
            ZWrite Off
            Blend One OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _OverlayTex;
            float4 _MainTex_ST;
            float4 _OverlayTex_ST;
            half _OverlayOpacity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 baseUv : TEXCOORD0;
                float2 overlayUv : TEXCOORD1;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.baseUv = TRANSFORM_TEX(input.uv, _MainTex);
                output.overlayUv = TRANSFORM_TEX(input.uv, _OverlayTex);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 baseColor = tex2D(_MainTex, input.baseUv);
                fixed4 overlayColor = tex2D(_OverlayTex, input.overlayUv);
                overlayColor.a *= _OverlayOpacity;

                fixed inverseOverlayAlpha = 1.0 - overlayColor.a;
                fixed4 result;
                result.rgb = overlayColor.rgb * overlayColor.a
                           + baseColor.rgb * baseColor.a * inverseOverlayAlpha;
                result.a = overlayColor.a + baseColor.a * inverseOverlayAlpha;
                return result;
            }
            ENDCG
        }
    }
}