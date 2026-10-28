// 잔상형 모션 블러 셰이더. AfterimageRendererFeature가 쓴다.
// 지금 화면(_BlitTexture)과 지난 화면(_AfterimageHistory)을 섞는다.
// 움직이는 것은 지난 자리가 남아 꼬리처럼 번지고, 멈춰 있는 것은 두 화면이 같아서 그대로 보인다.
Shader "ProjectZ/Afterimage"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "AfterimageBlend"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_AfterimageHistory);

            // 지난 화면을 얼마나 남길지 (0이면 지금 화면만, 1에 가까울수록 꼬리가 길다)
            float _AfterimageAmount;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half4 current = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half4 history = SAMPLE_TEXTURE2D_X(_AfterimageHistory, sampler_LinearClamp, uv);
                return lerp(current, history, _AfterimageAmount);
            }
            ENDHLSL
        }
    }
}
