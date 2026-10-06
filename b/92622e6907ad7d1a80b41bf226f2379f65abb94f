Shader "SunkenPrism/TorchFlame"
{
    Properties { _Color ("Regional fire colour", Color) = (1,0.35,0.08,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float seed : TEXCOORD1; UNITY_FOG_COORDS(2) };
            fixed4 _Color;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex=UnityObjectToClipPos(v.vertex);
                o.uv=v.uv;
                o.seed=unity_ObjectToWorld._m03*0.73+unity_ObjectToWorld._m23*0.31;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float y=saturate(i.uv.y), t=_Time.y*3.7+i.seed;
                float bend=(sin(y*8-t)*0.078+sin(y*17-t*1.27)*0.028)*y;
                float x=abs(i.uv.x-0.5-bend);
                float width=0.45*pow(max(0.001,1-y),0.72)*(0.83+0.16*sin(y*12-t*1.3));
                float outer=1-smoothstep(width*0.60,width,x);
                float vertical=smoothstep(0,0.06,y)*(1-smoothstep(0.89,1,y));
                float turbulence=0.8+0.2*sin(y*27-t*2+sin(i.uv.x*18+y*10));
                float core=(1-smoothstep(width*0.05,width*0.48,x))*pow(1-y,1.5);
                fixed4 result=fixed4(lerp(_Color.rgb*1.18,fixed3(1,0.93,0.76),core*0.42),outer*vertical*turbulence*_Color.a*0.68);
                UNITY_APPLY_FOG_COLOR(i.fogCoord,result,fixed4(0,0,0,0));
                return result;
            }
            ENDCG
        }
    }
}
