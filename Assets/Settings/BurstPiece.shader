// 분열탄 조각 셰이더. 연 모양을 그림 없이 계산으로 그린다.
// 안쪽은 끝까지 불투명하고, 경계만 화면 1픽셀 폭으로 부드럽게 처리한다.
// 그래서 크기와 상관없이 모서리가 반투명하게 번지거나 계단처럼 깨지지 않는다.
// 꼭짓점은 ShapeSprites의 BurstInner, BurstShoulder, BurstTip과 같아야 한다. (크기 1 상자 기준, 뾰족한 끝이 +x)
// 색은 스프라이트 색 x 렌더러 색 x 머티리얼 _Color로 받는다. (ClippedSprite와 같다)
Shader "ProjectZ/BurstPiece"
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
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            // 변 a→b의 바깥쪽으로 떨어진 거리 (꼭짓점은 반시계 방향 순서)
            float EdgeDistance(float2 p, float2 a, float2 b)
            {
                float2 e = b - a;
                return dot(p - a, normalize(float2(e.y, -e.x)));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                const float2 inner = float2(-0.45, 0.0);
                const float2 bottom = float2(-0.075, -0.347);
                const float2 tip = float2(0.45, 0.0);
                const float2 top = float2(-0.075, 0.347);

                float2 p = input.uv - 0.5;

                // 네 변 중 가장 바깥쪽 거리. 0보다 작으면 도형 안이다.
                float d = EdgeDistance(p, inner, bottom);
                d = max(d, EdgeDistance(p, bottom, tip));
                d = max(d, EdgeDistance(p, tip, top));
                d = max(d, EdgeDistance(p, top, inner));

                // 화면 1픽셀에 해당하는 폭만큼만 부드럽게 만든다.
                float coverage = saturate(0.5 - d / max(fwidth(d), 0.00001));

                half4 color = input.color;
                color.a *= coverage;
                return color;
            }
            ENDHLSL
        }
    }
}
