// Unity based gaussian blue shader

Shader "VueCode/BlurEffect" {
	Properties { _MainTex ("", any) = "" {} }
	CGINCLUDE
	#include "UnityCG.cginc"
	struct v2f {
		float4 pos : SV_POSITION;
		half2 uv : TEXCOORD0;
		half2 taps[4] : TEXCOORD1; 
	};
	sampler2D _MainTex;
	half4 _MainTex_TexelSize;
	half _BlurSize;
	v2f vert( appdata_img v ) {
		v2f o; 
		o.pos = UnityObjectToClipPos(v.vertex);
		o.uv = v.texcoord;
		half2 offset = _MainTex_TexelSize.xy * _BlurSize;
		o.taps[0] = o.uv + offset;
		o.taps[1] = o.uv - offset;
		o.taps[2] = o.uv + offset * half2(1,-1);
		o.taps[3] = o.uv - offset * half2(1,-1);
		return o;
	}
	half4 frag(v2f i) : SV_Target {
		half4 center = tex2D(_MainTex, i.uv);
		half4 tap0 = tex2D(_MainTex, i.taps[0]);
		half4 tap1 = tex2D(_MainTex, i.taps[1]);
		half4 tap2 = tex2D(_MainTex, i.taps[2]);
		half4 tap3 = tex2D(_MainTex, i.taps[3]);
		half4 color = (center + tap0 + tap1 + tap2 + tap3) * 0.2;
		color.a = max(center.a * 0.98, color.a);
		return color;
	}
	ENDCG
	SubShader {
		 Pass {
			  ZTest Always Cull Off ZWrite Off

			  CGPROGRAM
			  #pragma vertex vert
			  #pragma fragment frag
			  ENDCG
		  }
	}
	Fallback off
}
