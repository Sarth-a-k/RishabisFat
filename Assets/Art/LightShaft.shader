Shader "SunkenPrism/LightShaft" {
 Properties { _Color("Color",Color)=(1,.85,.5,.02) }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Lighting Off Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha One
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float4 _Color;
 struct v2f { float4 pos:SV_POSITION; };
 v2f vert(float4 vertex:POSITION) { v2f o;o.pos=UnityObjectToClipPos(vertex);return o; }
 fixed4 frag(v2f i):SV_Target { return _Color; }
 ENDCG }
 }
}
