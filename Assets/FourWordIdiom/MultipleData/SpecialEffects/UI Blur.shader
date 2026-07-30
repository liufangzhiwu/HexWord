Shader "UI/Blur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurRadius ("Blur Radius", Range(0, 10)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            // 👇 关键：手动声明 _MainTex_TexelSize
            float4 _MainTex_TexelSize;
            float _BlurRadius;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 texelSize = _MainTex_TexelSize.xy * _BlurRadius;
                fixed4 col = fixed4(0,0,0,0);

                // 3x3 高斯核
                float kernel[9] = { 1,2,1, 2,4,2, 1,2,1 };
                float sum = 0;
                for (int k = 0; k < 9; k++) sum += kernel[k];

                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        float2 offset = float2(x, y) * texelSize;
                        float weight = kernel[(x+1) + (y+1)*3] / sum;
                        col += tex2D(_MainTex, i.uv + offset) * weight;
                    }
                }
                col *= i.color;
                return col;
            }
            ENDCG
        }
    }
}