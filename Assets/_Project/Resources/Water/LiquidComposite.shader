Shader "BlockCrusher/LiquidComposite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Liquid Mask", 2D) = "black" {}
        _LiquidColor ("Liquid Color", Color) = (0.05, 0.55, 0.9, 0.9)
        _Threshold ("Threshold", Range(0, 1)) = 0.35
        _Softness ("Edge Softness", Range(0.001, 0.25)) = 0.04
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _LiquidColor;
            half _Threshold;
            half _Softness;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                half mask = tex2D(_MainTex, input.uv).a;
                half alpha = smoothstep(_Threshold - _Softness, _Threshold + _Softness, mask);
                fixed4 color = _LiquidColor * input.color;
                color.a *= alpha;
                return color;
            }
            ENDCG
        }
    }
}
