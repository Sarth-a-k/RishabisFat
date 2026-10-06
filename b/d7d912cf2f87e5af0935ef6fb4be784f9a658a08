Shader "CasaFX/PickupGlow"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.78, 0.4, 1)
        _Intensity ("Intensity", Float) = 0.6
        _RimPower ("Rim Power", Float) = 2.2
        _Inflate ("Inflate", Float) = 0.006
        _SweepY ("Sweep Height", Float) = -10000
        _SweepWidth ("Sweep Width", Float) = 0.12
        _SweepIntensity ("Sweep Intensity", Float) = 1.4
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 wpos : TEXCOORD0; float3 wnrm : TEXCOORD1; };

            float4 _Color;
            float _Intensity, _RimPower, _Inflate, _SweepY, _SweepWidth, _SweepIntensity;

            v2f vert (appdata v)
            {
                v2f o;
                float3 n = UnityObjectToWorldNormal(v.normal);
                float3 w = mul(unity_ObjectToWorld, v.vertex).xyz + n * _Inflate;
                o.pos = mul(UNITY_MATRIX_VP, float4(w, 1.0));
                o.wpos = w;
                o.wnrm = n;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.wnrm);
                float3 v = normalize(_WorldSpaceCameraPos - i.wpos);
                float rim = pow(1.0 - saturate(abs(dot(n, v))), _RimPower);
                float d = (i.wpos.y - _SweepY) / max(_SweepWidth, 0.001);
                float band = exp(-d * d);
                float3 c = _Color.rgb * (rim * _Intensity + band * _SweepIntensity * (0.25 + rim));
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }
}
