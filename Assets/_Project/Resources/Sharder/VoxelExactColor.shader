Shader "BlockCrusher/VoxelExactColor"
{
    Properties
    {
        _EdgeColor ("Edge Color", Color) = (0,0,0,1)
        _EdgeStrength ("Edge Strength", Range(0,1)) = 0.45
        _EdgeWidth ("Edge Width", Range(0.001,0.15)) = 0.035
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            fixed4 _EdgeColor;
            half _EdgeStrength;
            half _EdgeWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = i.color;
                color.a = 1;

                #ifndef UNITY_COLORSPACE_GAMMA
                    color.rgb = GammaToLinearSpace(color.rgb);
                #endif

                float2 edgeUv = min(i.uv, 1 - i.uv);
                float edgeDistance = min(edgeUv.x, edgeUv.y);
                float edgeFeather = max(fwidth(edgeDistance), 0.0001);
                float edgeMask = 1 - smoothstep(_EdgeWidth, _EdgeWidth + edgeFeather, edgeDistance);
                color.rgb = lerp(color.rgb, _EdgeColor.rgb, edgeMask * _EdgeStrength);

                return color;
            }
            ENDCG
        }
    }
}
