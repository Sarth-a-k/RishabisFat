Shader "SunkenPrism/LunarHalo"
{
 Properties { _Color ("Glow color", Color) = (1,1,1,0.16) }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" }
  Blend SrcAlpha One
  ZWrite Off
  Cull Off
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
   struct varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
   fixed4 _Color;
   varying vert(input v) { varying o; o.position=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o; }
   fixed4 frag(varying i):SV_Target {
    float radius=length(i.uv-.5);
    float ring=exp(-pow((radius-.343)/.067,2));
    return fixed4(_Color.rgb,_Color.a*ring);
   }
   ENDCG
  }
 }
}
