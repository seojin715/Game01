using System;
using UnityEngine;

/// <summary>어둠 기믹 설정. LevelPlayer 인스펙터에서 조절한다.</summary>
[Serializable]
public class DarkSettings
{
    [Tooltip("어두워지거나 다시 밝아지는 데 걸리는 시간 (노래 시간, 초)")]
    [Min(0.01f)] public float transitionTime = 0.6f;

    [Tooltip("어두울 때 포탄과 발사기의 밝기. 1보다 크면 Bloom으로 빛난다.")]
    [Range(1f, 6f)] public float glowIntensity = 2.5f;

    [Tooltip("포탄과 발사기가 주변 격자 선을 비추는 거리 (칸 대비, 물체 가장자리에서부터)")]
    [Range(0f, 4f)] public float lightRadius = 0.8f;

    [Tooltip("하트가 주변 격자 선을 비추는 거리 (칸 대비, 하트 가운데에서부터)")]
    [Range(0f, 4f)] public float heartLightRadius = 1.2f;
}

/// <summary>
/// 어둠 기믹의 상태와 빛 목록. 격자 선 셰이더(ProjectZ/DarkGridLine)에 전역 값으로 넘긴다.
///
/// LevelPlayer가 매 프레임 Begin으로 어두운 정도를 정하고, 포탄·발사기·하트 자리에 빛을 더한 뒤 Apply로 넘긴다.
/// 어두우면 격자 선은 빛이 닿는 곳만 보인다.
/// </summary>
public static class Darkness
{
    /// <summary>한 번에 넘길 수 있는 빛 개수. 셰이더의 MAX_DARK_LIGHTS와 같아야 한다.</summary>
    public const int MaxLights = 64;

    /// <summary>지금 어두운 정도 (0 = 보통, 1 = 완전히 어두움)</summary>
    public static float Level { get; private set; }

    static readonly Vector4[] lightA = new Vector4[MaxLights];   // 시작 x, y, 반지름, 세기
    static readonly Vector4[] lightB = new Vector4[MaxLights];   // 끝 x, y
    static int count;

    static readonly int DarknessId = Shader.PropertyToID("_Darkness");
    static readonly int CountId = Shader.PropertyToID("_DarkLightCount");
    static readonly int LightAId = Shader.PropertyToID("_DarkLightA");
    static readonly int LightBId = Shader.PropertyToID("_DarkLightB");

    /// <summary>이번 프레임의 어두운 정도를 정하고 빛 목록을 비운다.</summary>
    public static void Begin(float level)
    {
        Level = Mathf.Clamp01(level);
        count = 0;
    }

    /// <summary>점 하나에서 radius(월드 단위)까지 비추는 빛</summary>
    public static void AddPoint(Vector2 position, float radius, float intensity) => AddSegment(position, position, radius, intensity);

    /// <summary>선분 전체에서 radius까지 비추는 빛 (레이저처럼 긴 것)</summary>
    public static void AddSegment(Vector2 from, Vector2 to, float radius, float intensity)
    {
        if (count >= MaxLights || intensity <= 0f || radius <= 0f) return;
        lightA[count] = new Vector4(from.x, from.y, radius, Mathf.Clamp01(intensity));
        lightB[count] = new Vector4(to.x, to.y, 0f, 0f);
        count++;
    }

    /// <summary>모은 빛과 어두운 정도를 셰이더에 넘긴다.</summary>
    public static void Apply()
    {
        Shader.SetGlobalFloat(DarknessId, Level);
        Shader.SetGlobalFloat(CountId, count);
        Shader.SetGlobalVectorArray(LightAId, lightA);
        Shader.SetGlobalVectorArray(LightBId, lightB);
    }

    // 지난 플레이에서 어두운 채로 멈췄어도 새 플레이는 밝게 시작한다. (전역 셰이더 값은 플레이가 끝나도 남는다)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        Begin(0f);
        Apply();
    }
}
