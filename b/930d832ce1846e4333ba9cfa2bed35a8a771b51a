Shader "CasaFX/BlackAura"
{
    Properties
    {
        _Radius ("Radius", Float) = 2.5
        _Strength ("Strength", Range(0, 1)) = 0.92
        _Pulse ("Pulse", Range(0, 1)) = 0.12
        _Lift ("Toward camera", Float) = 0.6
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend DstColor Zero
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
            float _Radius, _Strength, _Pulse, _Lift;
            v2f vert(appdata v)
            {
                v2f o;
                float3 centre = mul(UNITY_MATRIX_V, float4(unity_ObjectToWorld._m03_m13_m23, 1)).xyz;
                float3 toCam = normalize(-centre);
                centre += toCam * _Lift;
                float2 corner = (v.uv - 0.5) * 2 * _Radius;
                o.vertex = mul(UNITY_MATRIX_P, float4(centre + float3(corner, 0), 1));
                o.uv = v.uv;
                o.seed = unity_ObjectToWorld._m03 * 0.37 + unity_ObjectToWorld._m23 * 0.53;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2;
                float t = _Time.y * 2.3 + i.seed;
                float flicker = 1 - _Pulse * (0.5 + 0.5 * sin(t) * sin(t * 1.7 + 1.3));
                float dark = pow(saturate(1 - d), 1.6) * _Strength * flicker;
                return fixed4(1 - dark, 1 - dark, 1 - dark, 1);
            }
            ENDCG
        }
    }
}
