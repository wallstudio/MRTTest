Shader "MyRender/BlitCopy"
{
    Properties
    {
        _MainTex ("Texture", any) = "" {}
    }
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma enable_d3d11_debug_symbols
            #pragma hlslcc_bytecode_disassembly

            Texture2D<half4> _MainTex;
            SamplerState sampler_MainTex;
            float4x4 unity_ObjectToWorld;

            float4 vert(float4 vertex : POSITION, inout float2 texcoord : TEXCOORD0) : SV_Position
            {
                return mul(unity_ObjectToWorld, vertex);
            }

            half4 frag(float4 vertex : SV_Position, float2 uv : TEXCOORD0) : SV_Target
            {
                return _MainTex.Sample(sampler_MainTex, uv);
            }
            ENDHLSL
        }
    }
}
