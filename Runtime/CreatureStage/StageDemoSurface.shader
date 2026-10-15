Shader "AIBuilder/CreatureStage/DemoSurface"
{
    Properties
    {
        _MainTex ("颜色贴图", 2D) = "white" {}
        _Color ("颜色", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            struct Input { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct Output { float4 vertex : SV_POSITION; float3 normal : TEXCOORD0; float2 uv : TEXCOORD1; };
            Output vert(Input v)
            {
                Output o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            fixed4 frag(Output i) : SV_Target
            {
                fixed shade = 0.6 + 0.4 * saturate(dot(normalize(i.normal), normalize(float3(0.3, 1, -0.4))));
                return fixed4(tex2D(_MainTex, i.uv).rgb * _Color.rgb * shade, 1);
            }
            ENDCG
        }
    }
}
