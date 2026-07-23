Shader "BlockCrusher/MetaballComposite"
{
    Properties
    {
        _MainTex ("Metaball Field", 2D) = "black" {}
        _Color ("Water Color", Color) = (0.05, 0.55, 1, 0.9)
        _Threshold ("Threshold", Range(0, 2)) = 0.35
        _Softness ("Edge Softness", Range(0.001, 0.25)) = 0.035
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest [unity_GUIZTestMode]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

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

            sampler2D _MainTex;
            fixed4 _Color;
            half _Threshold;
            half _Softness;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                half field = tex2D(_MainTex, input.uv).r;
                half alpha = smoothstep(_Threshold - _Softness, _Threshold + _Softness, field);
                fixed4 color = _Color * input.color;
                color.a *= alpha;
                return color;
            }
            ENDCG
        }
    }
}
