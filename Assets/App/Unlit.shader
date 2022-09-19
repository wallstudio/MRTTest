Shader "MyRender/Unlit"
{
    Properties
    {
        _MainTex ("Texture", any) = "" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma enable_d3d11_debug_symbols
            #pragma hlslcc_bytecode_disassembly

            half4 _Color;
            Texture2D<half4> _MainTex;
            SamplerState sampler_MainTex;
            float4x4 unity_ObjectToWorld, unity_MatrixVP;

            float4 vert(float4 vertex : POSITION, inout float2 uv : TEXCOORD0, inout half4 color : COLOR) : SV_Position
            {
                return mul(unity_MatrixVP, mul(unity_ObjectToWorld, vertex));
            }

            struct Target
            {
                half4 color0 : SV_Target0;
                half4 color1 : SV_Target1;
                half4 color2 : SV_Target2;
            };

            void frag(float4 vertex : SV_Position, float2 uv : TEXCOORD0, half4 color : COLOR, out Target target)
            {
                half3 tex = _MainTex.Sample(sampler_MainTex, uv).rgb * color.rgb * _Color.rgb;
                target.color0 = half4(tex, color.a * _Color.a);
                target.color1 = half4(1 - tex, color.a * _Color.a);
                target.color2 = half4((tex.r + tex.g + tex.b).xxx / 3, color.a * _Color.a);
            }
            ENDHLSL

        }
    }
}
