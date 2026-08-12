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
        _SoilTiling ("Soil Tiling", Float) = 0.65
        _SoilTint ("Soil Tint", Color) = (1,1,1,1)
        _CrackColor ("Crack Color", Color) = (0.08,0.08,0.08,1)
        _CrackAmount ("Crack Amount", Range(0,1)) = 0
        _CrackScale ("Crack Scale", Float) = 5
        _CrackWidth ("Crack Width", Range(0.001,0.1)) = 0.008
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
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            fixed4 _EdgeColor;
            half _EdgeStrength;
            half _EdgeWidth;
            sampler2D _SoilTex;
            half _SoilTiling;
            half4 _SoilTint;
            fixed4 _CrackColor;
            half _CrackScale;
            half _CrackWidth;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(half, _UseVertexColor)
                UNITY_DEFINE_INSTANCED_PROP(half, _CrackAmount)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 soilUv : TEXCOORD1;
                float2 crackUv : TEXCOORD2;
                float3 worldNormal : TEXCOORD3;
                float3 worldPosition : TEXCOORD5;
                SHADOW_COORDS(4)
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
                o.worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.soilUv = o.worldPosition.xy * _SoilTiling;
                o.crackUv = v.vertex.xy * _CrackScale;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                TRANSFER_SHADOW(o);
                return o;
            }

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float2 VoronoiEdge(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                float nearest = 10;
                float secondNearest = 10;
                float reveal = 0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y);
                        float2 feature = offset + Hash22(cell + offset) - local;
                        float distanceSquared = dot(feature, feature);
                        if (distanceSquared < nearest)
                        {
                            secondNearest = nearest;
                            nearest = distanceSquared;
                            reveal = Hash22(cell + offset + 17.17).x;
                        }
                        else if (distanceSquared < secondNearest)
                        {
                            secondNearest = distanceSquared;
                        }
                    }
                }

                return float2((sqrt(secondNearest) - sqrt(nearest)) * 0.5, reveal);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half useVertexColor = UNITY_ACCESS_INSTANCED_PROP(Props, _UseVertexColor);
                fixed4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                fixed4 color;
                if (useVertexColor < 0.5)
                    color = tint;
                else
                    color = i.color * tint;
                // Chunk dirt uses vertex alpha zero as a surface-class marker. Output stays
                // opaque; the marker lets us hide cell geometry without flattening rocks.
                half isSoil = step(0.5h, useVertexColor) * (1 - step(0.5h, i.color.a));
                color.a = 1;

                float2 edgeUv = min(i.uv, 1 - i.uv);
                float edgeDistance = min(edgeUv.x, edgeUv.y);
                float edgeFeather = max(fwidth(edgeDistance), 0.0001);
                float edgeMask = 1 - smoothstep(_EdgeWidth, _EdgeWidth + edgeFeather, edgeDistance);
                color.rgb = lerp(color.rgb, _EdgeColor.rgb,
                    edgeMask * _EdgeStrength * (1 - isSoil));

                half3 soilColor = tex2D(_SoilTex, i.soilUv).rgb * _SoilTint.rgb;
                color.rgb = lerp(color.rgb, soilColor, isSoil);

                half crackAmount = UNITY_ACCESS_INSTANCED_PROP(Props, _CrackAmount);
                float2 crack = VoronoiEdge(i.crackUv);
                float crackFeather = max(fwidth(crack.x), 0.001);
                float crackLine = 1 - smoothstep(_CrackWidth, _CrackWidth + crackFeather, crack.x);
                float crackReveal = step(crack.y, saturate(crackAmount * 1.15));
                color.rgb = lerp(color.rgb, _CrackColor.rgb, crackLine * crackReveal);

                float3 normal = normalize(i.worldNormal);
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                half diffuse = saturate(dot(normal, lightDirection));
                half3 ambient = ShadeSH9(float4(normal, 1.0)).rgb;
                // Vertex colors are authored as display colors. Do not apply a second
                // gamma-to-linear conversion here; it crushes dark voxel colors.
                half3 lighting = max(ambient, 0.65h) + _LightColor0.rgb *
                    (0.15h + 0.25h * diffuse * SHADOW_ATTENUATION(i));
                // The authored dirt fragments have intentionally faceted normals. Let the
                // physics/grid keep those meshes, but shade dirt as one flat surface so their
                // boundaries cannot appear. Dynamic shadows are still retained.
                half soilLighting = lerp(0.82h, 1.0h, SHADOW_ATTENUATION(i));
                color.rgb *= lerp(min(lighting, 1.15h), soilLighting, isSoil);

                half maxChannel = max(color.r, max(color.g, color.b));
                half minChannel = min(color.r, min(color.g, color.b));
                color.rgb *= lerp(1.0h, 1.35h, saturate((maxChannel - minChannel) * 2.0h));

                return color;
            }
            ENDCG
        }
    }

    Fallback "Diffuse"
}
