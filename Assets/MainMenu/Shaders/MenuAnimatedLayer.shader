Shader "CasaMenu/AnimatedLayer"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SwayAmp ("Sway Amount", Float) = 0
        _SwayBase ("Sway Base (uv y)", Float) = 0.22
        _SwayTop ("Sway Top (uv y)", Float) = 0.86
        _SwayFreq ("Sway Frequency", Float) = 14
        _SwaySpeed ("Sway Speed", Float) = 1.1
        _WaterLine ("Water Line (uv y)", Float) = 0
        _WaterAmp ("Water Ripple", Float) = 0
        _BlinkTex ("Blink Ids", 2D) = "black" {}
        _BlinkAmount ("Blink Amount", Float) = 0
        _BlinkRate ("Blink Rate", Float) = 0.12
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            sampler2D _BlinkTex;
            fixed4 _Color;
            float _SwayAmp, _SwayBase, _SwayTop, _SwayFreq, _SwaySpeed;
            float _WaterLine, _WaterAmp;
            float _BlinkAmount, _BlinkRate;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _Time.y;

                float h = saturate((uv.y - _SwayBase) / max(_SwayTop - _SwayBase, 0.001));
                h = h * h * (3.0 - 2.0 * h);
                float wave = sin(uv.y * _SwayFreq + t * _SwaySpeed + uv.x * 9.0) * 0.55
                           + sin(uv.y * _SwayFreq * 2.3 - t * _SwaySpeed * 1.6 + uv.x * 21.0) * 0.25;
                float lean = sin(t * 0.33 + uv.x * 4.0) * 0.9;
                uv.x += _SwayAmp * h * (wave + lean);
                uv.y += _SwayAmp * 0.35 * h * sin(t * 0.47 + uv.x * 6.0);

                float below = saturate((_WaterLine - uv.y) / max(_WaterLine, 0.001));
                float wm = smoothstep(0.0, 0.08, below);
                uv.x += _WaterAmp * wm * (0.35 + below) * sin(uv.y * 900.0 * (1.15 - below) + t * 1.7)
                      + _WaterAmp * 0.6 * wm * sin(uv.y * 140.0 - t * 0.9);

                fixed4 col = tex2D(_MainTex, uv) * i.color;

                float id = tex2D(_BlinkTex, float2(uv.x, 0.5)).r;
                float cyc = frac(t * _BlinkRate * (0.8 + id * 0.5) + id * 7.31);
                float shut = 1.0 - smoothstep(0.0, 0.004, abs(cyc - 0.012) - 0.008);
                float cyc2 = frac(t * _BlinkRate * 0.37 + id * 3.17);
                shut = max(shut, 1.0 - smoothstep(0.0, 0.002, abs(cyc2 - 0.5) - 0.004));
                col.a *= 1.0 - _BlinkAmount * shut * step(0.01, id);
                return col;
            }
            ENDCG
        }
    }
}
