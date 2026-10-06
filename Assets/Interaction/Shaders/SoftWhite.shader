Shader "CasaFX/SoftWhite"
{
    Properties
    {
        _Light ("Lit colour", Color) = (0.90, 0.92, 0.96, 1)
        _Shade ("Shade colour", Color) = (0.42, 0.46, 0.56, 1)
        _Dir ("Light direction", Vector) = (0.35, 0.85, -0.4, 0)
        _FarBright ("Far brighten", Range(0, 1)) = 0.35
        _FarDist ("Far distance", Float) = 40
        _FloorY ("Floor height", Float) = 0
        _Corner ("Corner darkening", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 vertex : SV_POSITION; float3 n : TEXCOORD0; float3 wpos : TEXCOORD1; };
            fixed4 _Light, _Shade;
            float4 _Dir;
            float _FarBright, _FarDist, _FloorY, _Corner;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.n);
                float k = saturate(dot(n, normalize(_Dir.xyz)) * 0.5 + 0.5);
                k = k * k * (3 - 2 * k);
                fixed3 c = lerp(_Shade.rgb, _Light.rgb, k);
                float h = i.wpos.y - _FloorY;
                float ao = 1 - _Corner * (1 - smoothstep(0.0, 0.9, h)) - _Corner * 0.6 * smoothstep(4.6, 6.2, h);
                c *= ao;
                float d = distance(_WorldSpaceCameraPos, i.wpos);
                c = lerp(c, fixed3(0.97, 0.98, 1.0), saturate(d / _FarDist) * _FarBright);
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
