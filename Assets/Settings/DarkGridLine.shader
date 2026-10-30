// 어둠 기믹에서 격자 선이 쓰는 스프라이트 셰이더. GridView의 선이 쓴다.
// 어둡지 않을 때(_Darkness = 0)는 보통 스프라이트와 똑같이 그린다.
// 어두워지면 선이 사라지고, 빛(포탄, 발사기, 하트 주변) 가까이에 있는 부분만 보인다. 불을 켠 곳만 선이 드러나는 것처럼.
// 빛은 LevelPlayer가 매 프레임 전역 값으로 알려 준다. (Darkness.cs)
//   _DarkLightA[i] = (선분 시작 x, y, 비추는 반지름, 세기), _DarkLightB[i] = (선분 끝 x, y)
//   점 빛은 시작과 끝이 같은 선분이다. 레이저처럼 긴 것은 선분 전체가 빛난다.
Shader "ProjectZ/DarkGridLine"
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

            #define MAX_DARK_LIGHTS 64

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

            // 어두운 정도 (0 = 보통, 1 = 빛이 닿는 곳만 보임)
            float _Darkness;
            float _DarkLightCount;
            float4 _DarkLightA[MAX_DARK_LIGHTS];
            float4 _DarkLightB[MAX_DARK_LIGHTS];

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
                half4 color = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                if (_Darkness > 0.001)
                {
                    // 가장 가까이에서 비추는 빛의 밝기를 고른다. 빛에서 멀어질수록 부드럽게 어두워진다.
                    float light = 0.0;
                    int count = (int)_DarkLightCount;
                    [loop]
                    for (int i = 0; i < MAX_DARK_LIGHTS; i++)
                    {
                        if (i >= count) break;

                        float2 a = _DarkLightA[i].xy;
                        float2 b = _DarkLightB[i].xy;
                        float2 ab = b - a;
                        float t = saturate(dot(input.positionWS - a, ab) / max(dot(ab, ab), 1e-5));
                        float distance = length(input.positionWS - (a + ab * t));

                        float f = saturate(1.0 - distance / max(_DarkLightA[i].z, 1e-4));
                        f = f * f * (3.0 - 2.0 * f);
                        light = max(light, f * _DarkLightA[i].w);
                    }

                    color.a *= lerp(1.0, light, _Darkness);
                }

                return color;
            }
            ENDHLSL
        }
    }
}
