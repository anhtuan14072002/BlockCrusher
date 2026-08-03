Shader "BlockCrusher/VoxelExactColor"
{
    Properties
    {
        _EdgeColor ("Edge Color", Color) = (0,0,0,1)
        _Color ("Color", Color) = (1,1,1,1)
        _UseVertexColor ("Use Vertex Color", Float) = 1
        _EdgeStrength ("Edge Strength", Range(0,1)) = 0.45
        _EdgeWidth ("Edge Width", Range(0.001,0.15)) = 0.035
        _SoilTex ("Soil Texture", 2D) = "gray" {}
        _SoilStrength ("Soil Strength", Range(0,0.35)) = 0.12
        _SoilTiling ("Soil Tiling", Float) = 0.65
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
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            fixed4 _EdgeColor;
            half _EdgeStrength;
            half _EdgeWidth;
            sampler2D _SoilTex;
            half _SoilStrength;
            half _SoilTiling;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(half, _UseVertexColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 soilUv : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                o.soilUv = mul(unity_ObjectToWorld, v.vertex).xy * _SoilTiling;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half useVertexColor = UNITY_ACCESS_INSTANCED_PROP(Props, _UseVertexColor);
                fixed4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                if (useVertexColor < 0.5)
                {
                    tint.a = 1;
                    return tint;
                }

                fixed4 color = i.color * tint;
                color.a = 1;

                #ifndef UNITY_COLORSPACE_GAMMA
                    color.rgb = GammaToLinearSpace(color.rgb);
                #endif

                float2 edgeUv = min(i.uv, 1 - i.uv);
                float edgeDistance = min(edgeUv.x, edgeUv.y);
                float edgeFeather = max(fwidth(edgeDistance), 0.0001);
                float edgeMask = 1 - smoothstep(_EdgeWidth, _EdgeWidth + edgeFeather, edgeDistance);
                color.rgb = lerp(color.rgb, _EdgeColor.rgb, edgeMask * _EdgeStrength);

                half soil = tex2D(_SoilTex, i.soilUv).r;
                color.rgb *= lerp(1 - _SoilStrength, 1 + _SoilStrength, soil);

                return color;
            }
            ENDCG
        }
    }
}
