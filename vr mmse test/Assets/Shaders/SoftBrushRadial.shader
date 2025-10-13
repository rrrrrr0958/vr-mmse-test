Shader "Hidden/SoftBrushRadial"
{
    Properties {
        _Color     ("Color", Color) = (0,0,0,1)
        _EdgeWidth ("Edge Width (0~0.5)", Range(0.01,0.5)) = 0.12
        _Hardness  ("Hardness (0~1)", Range(0,1)) = 0.8
    }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float  _EdgeWidth;   // 邊緣過渡寬度（半徑比例）
            float  _Hardness;    // 0=軟, 1=硬（影響曲線）

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f     { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            v2f vert (appdata v){ v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            // 矩形內部用 UV 畫一個圓；中心實心，外圈 edge 內做窄過渡
            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;   // [-1,1]
                float r  = length(p);          // 0(中心)~1(邊)

                // 實心半徑 = 1 - edgeWidth
                float inner = saturate(1.0 - _EdgeWidth);
                // 在 [inner, 1] 做過渡：r<=inner 完全不透明，r>=1 完全透明
                float t = saturate((1.0 - r) / max(1e-5, _EdgeWidth)); // 1->0
                // 硬度：越硬 -> 指數越高，邊更銳利
                float expK = lerp(2.0, 10.0, saturate(_Hardness));
                float aEdge = pow(t, expK);

                // 內部實心（r<=inner 時為 1）
                float aCore = step(r, inner);

                float a = max(aCore, aEdge); // 內部 1，外圈窄過渡
                return fixed4(_Color.rgb, _Color.a * a);
            }
            ENDCG
        }
    }
}
