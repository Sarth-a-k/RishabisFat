Shader "CasaFX/UVGlow"
{
    Properties
    {
        _MainTex ("Glow mask", 2D) = "white" {}
        [HDR] _Color ("Glow colour", Color) = (0.8, 0.3, 1, 1)
        _Intensity ("Intensity", Float) = 1.6
        _Pulse ("Pulse amount", Range(0, 1)) = 0.25
        _Speed ("Pulse speed", Float) = 1.1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float seed : TEXCOORD1; };
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Intensity, _Pulse, _Speed;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.seed = unity_ObjectToWorld._m03 * 0.41 + unity_ObjectToWorld._m23 * 0.67;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                float p = 1 - _Pulse * (0.5 + 0.5 * sin(_Time.y * _Speed + i.seed));
                return fixed4(t.rgb * t.a * _Color.rgb * _Intensity * p, 1);
            }
            ENDCG
        }
    }
}
