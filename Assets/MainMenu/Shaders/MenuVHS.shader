Shader "CasaMenu/VHSOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Scanlines ("Scanline Strength", Range(0,1)) = 0.08
        _Grain ("Grain Strength", Range(0,1)) = 0.06
        _Vignette ("Vignette", Range(0,1)) = 0.45
        _Band ("Rolling Band", Range(0,1)) = 0.04
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 sp : TEXCOORD1; };

            fixed4 _Color;
            float _Scanlines, _Grain, _Vignette, _Band;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.sp = ComputeScreenPos(o.pos);
                o.color = v.color * _Color;
                return o;
            }

            float hash (float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 sp = i.sp.xy / max(i.sp.w, 0.0001);
                float t = _Time.y;
                float scan = sin(sp.y * _ScreenParams.y * 1.5708) * 0.5 + 0.5;
                float dark = scan * _Scanlines;
                float n = hash(floor(sp * _ScreenParams.xy * 0.5) + floor(t * 24.0));
                float band = smoothstep(0.0, 0.06, 0.06 - abs(frac(sp.y - t * 0.06) - 0.5) + 0.0);
                float2 c = i.uv - 0.5;
                float vig = saturate(dot(c, c) * 2.2) * _Vignette;
                float a = saturate(dark + vig + n * _Grain);
                float3 rgb = float3(0.0, 0.0, 0.02);
                a = saturate(a + band * _Band);
                return fixed4(rgb, a * i.color.a);
            }
            ENDCG
        }
    }
}
