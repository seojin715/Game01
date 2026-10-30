// 격자 밖을 그리지 않는 스프라이트 셰이더. 하트, 하트 잔상, 텔레포트 중 하트 복사본이 쓴다.
// 텔레포트(감싸기)로 하트가 벽 밖으로 빠져나가는 부분을 가리기 위한 것이다.
// 격자 범위는 GameGrid가 전역 값 _GridClipRect(월드 좌표: xMin, yMin, xMax, yMax)로 매 프레임 알려 준다.
// 색은 URP의 Sprite-Unlit-Default와 같은 방식으로 받는다. (스프라이트 색 x 렌더러 색 x 머티리얼 _Color)
Shader "ProjectZ/ClippedSprite"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                float2 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            // 격자 범위 (월드 좌표). 이 밖은 그리지 않는다.
            float4 _GridClipRect;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS.xy;
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 격자 밖이면 버린다. (범위가 아직 정해지지 않았으면 자르지 않는다)
                bool hasRect = _GridClipRect.z > _GridClipRect.x;
                if (hasRect && (input.positionWS.x < _GridClipRect.x || input.positionWS.x > _GridClipRect.z ||
                                input.positionWS.y < _GridClipRect.y || input.positionWS.y > _GridClipRect.w))
                {
                    discard;
                }

                return input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            }
            ENDHLSL
        }
    }
}
