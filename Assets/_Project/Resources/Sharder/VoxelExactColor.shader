Shader "BlockCrusher/VoxelExactColor"
{
    Properties
    {
        _EdgeColor ("Edge Color", Color) = (0,0,0,1)
        _Color ("Color", Color) = (1,1,1,1)
        _SpawnProgress ("Spawn Progress", Range(0,1)) = 1
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
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            fixed4 _EdgeColor;
            half _EdgeStrength;
            half _EdgeWidth;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(half, _SpawnProgress)
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
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 color = i.color * UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                color.a = 1;
                half spawnProgress = saturate(UNITY_ACCESS_INSTANCED_PROP(Props, _SpawnProgress));
                if (spawnProgress < 0.999)
                {
                    float dither = frac(52.9829189 * frac(dot(floor(i.vertex.xy), float2(0.06711056, 0.00583715))));
                    clip(spawnProgress - dither - 0.0001);
                }

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
