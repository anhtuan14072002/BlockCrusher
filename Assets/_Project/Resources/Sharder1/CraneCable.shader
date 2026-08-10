Shader "Crusher/Crane Cable"
{
    Properties
    {
        _Color ("Cable Color", Color) = (1,1,1,1)
        _HighlightColor ("Highlight Color", Color) = (1,1,1,1)
        _HighlightPosition ("Highlight Position", Range(0,1)) = 0.35
        _HighlightWidth ("Highlight Width", Range(0.01,0.5)) = 0.32
        _EdgeDarkness ("Edge Darkness", Range(0,1)) = 0.28
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _HighlightColor;
            float _HighlightPosition;
            float _HighlightWidth;
            float _EdgeDarkness;

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
                float across = saturate(i.uv.y);
                float edge = abs(across * 2.0 - 1.0);
                float roundedProfile = pow(saturate(1.0 - edge), 1.35);
                float highlight = 1.0 - smoothstep(0.0, _HighlightWidth,
                    abs(across - _HighlightPosition));
                fixed3 cable = i.color.rgb * _Color.rgb;
                cable *= lerp(1.0 - _EdgeDarkness, 1.0, roundedProfile);
                cable = saturate(cable + _HighlightColor.rgb * highlight * 0.12);
                return fixed4(cable, i.color.a * _Color.a);
            }
            ENDCG
        }
    }
}
