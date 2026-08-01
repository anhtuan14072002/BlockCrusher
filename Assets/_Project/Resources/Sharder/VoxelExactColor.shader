Shader "BlockCrusher/VoxelExactColor"
{
    Properties
    {
        _EdgeColor ("Edge Color", Color) = (0,0,0,1)
        _Color ("Color", Color) = (1,1,1,1)
        _EdgeStrength ("Edge Strength", Range(0,1)) = 0.45
        _EdgeWidth ("Edge Width", Range(0.001,0.15)) = 0.035
        _SoilTex ("Soil Texture", 2D) = "gray" {}
        _SoilStrength ("Soil Strength", Range(0,0.35)) = 0.12
        _SoilTiling ("Soil Tiling", Float) = 0.65
        _CutMask ("Cut Mask", 2D) = "white" {}
        _CutGrid ("Cut Grid", Vector) = (0,0,1,0)
        _CutMaskSize ("Cut Mask Size", Vector) = (1,1,1,1)
        _CutEdge ("Cut Edge", Range(0,1)) = 0.46
        _CutRoughness ("Cut Roughness", Range(0,0.5)) = 0.22
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
            sampler2D _CutMask;
            float4 _CutGrid;
            float4 _CutMaskSize;
            half _CutEdge;
            half _CutRoughness;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
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
                float2 localPosition : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            half Hash21(float2 value)
            {
                float3 p = frac(value.xyx * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            half ValueNoise(float2 value)
            {
                float2 cell = floor(value);
                float2 blend = frac(value);
                blend = blend * blend * (3 - 2 * blend);
                half bottom = lerp(Hash21(cell), Hash21(cell + float2(1, 0)), blend.x);
                half top = lerp(Hash21(cell + float2(0, 1)), Hash21(cell + 1), blend.x);
                return lerp(bottom, top, blend.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                o.soilUv = v.vertex.xy * _SoilTiling;
                o.localPosition = v.vertex.xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float2 gridCell = (i.localPosition - _CutGrid.xy) / _CutGrid.z;
                half2 contourWarp = half2(
                    ValueNoise(gridCell * 1.7 + float2(7.1, 2.3)),
                    ValueNoise(gridCell * 1.9 + float2(3.7, 9.2))) - 0.5;
                gridCell += contourWarp * 0.95;
                float2 cutUv = (gridCell + 0.5) * _CutMaskSize.zw;
                half cutMask = tex2D(_CutMask, cutUv).r;
                half cutNoise = (ValueNoise(gridCell * 4.2 + 17.4) - 0.5) * _CutRoughness;
                half cutValue = lerp(1, cutMask + cutNoise, saturate(_CutGrid.w));
                clip(cutValue - _CutEdge);

                fixed4 color = i.color * UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
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
