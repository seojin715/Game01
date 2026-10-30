using System;
using UnityEngine;

/// <summary>가시의 모양과 타이밍 설정. LevelPlayer 인스펙터에서 조절한다.</summary>
[Serializable]
public class SpikeSettings
{
    [Header("기본값 (레벨 데이터에 없을 때)")]
    [Tooltip("warnTime이 없거나 0일 때 쓰는 경고 시간 (초). 이 동안 가시가 벽에서 아주 조금씩 삐져나온다.")]
    [Min(0.05f)] public float defaultWarnTime = 0.6f;

    [Tooltip("holdTime이 없거나 0일 때 쓰는, 다 나와 있는 시간 (초)")]
    [Min(0.05f)] public float defaultHoldTime = 0.5f;

    [Header("모양")]
    [Tooltip("가시 밑변의 폭 (칸 대비)")]
    [Range(0.2f, 1f)] public float width = 0.9f;

    [Tooltip("가시가 벽에서 나오는 길이 (칸 대비)")]
    [Range(0.2f, 1f)] public float depth = 0.85f;

    [Tooltip("충돌 판정 크기 (보이는 크기 대비). 약간 작게 둬서 억울한 피격을 줄인다.")]
    [Range(0.3f, 1f)] public float hitScale = 0.8f;

    [Header("경고")]
    [Tooltip("경고 시간 동안 벽에서 삐져나오는 최대 길이 (칸 대비). 끝 칸의 하트에 닿지 않을 만큼 작게 둔다.")]
    [Range(0f, 0.4f)] public float peekDistance = 0.12f;

    [Tooltip("다 나올 자리에 보이는 반투명 경고의 불투명도. 튀어나오기 직전에 이 값이 되고, 처음엔 그 절반에서 시작한다.")]
    [Range(0f, 1f)] public float warnAlpha = 0.3f;

    [Tooltip("반투명 경고가 나타나는 시간 (노래 시간, 초)")]
    [Min(0.01f)] public float warnAppearTime = 0.1f;

    [Header("움직임 (노래 시간, 초)")]
    [Tooltip("삐져나온 상태에서 확 튀어나오는 시간. 레벨의 time에 다 나오도록 그보다 이만큼 먼저 튀어나오기 시작한다.")]
    [Min(0.01f)] public float popTime = 0.06f;

    [Tooltip("다 나와 있다가 벽 속으로 다시 들어가는 시간")]
    [Min(0.01f)] public float hideTime = 0.25f;

    [Header("그리는 순서")]
    [Tooltip("반투명 경고. 격자 선(-10)보다 높게")]
    public int warningSortingOrder = -5;

    [Tooltip("하트(10)보다 낮게 둬서 하트를 가리지 않는다.")]
    public int spikeSortingOrder = 7;
}

/// <summary>
/// 가시 (spike).
/// side 쪽 벽에서 line번 줄의 첫 칸을 큰 삼각형 하나로 찌른다.
///
/// 가시는 모양 그대로 벽 뒤에 숨어 있다. 경고 시간(warnTime) 동안 다 나올 자리에 같은 모양의 반투명 경고가 보이고,
/// 진짜 가시는 벽에서 아주 조금씩 천천히 삐져나온다(이때는 판정이 없다). 레벨의 time에 맞춰 확 튀어나온다. holdTime 동안 그대로 있다가 벽 속으로 들어간다.
/// 벽 뒤에 있는 부분은 격자 밖을 그리지 않는 머티리얼로 잘라서 보이지 않는다.
/// 판정은 튀어나오기 시작한 뒤부터, 보이는 만큼만 한다.
/// </summary>
public class SpikeObstacle : LevelObstacle
{
    readonly SpikeSettings settings;

    readonly Vector2 edgePoint;      // 가시가 나오는 벽 위의 점 (그 줄의 한가운데)
    readonly Vector2 inward;         // 벽에서 격자 안쪽 방향
    readonly Vector2 across;         // 벽을 따라가는 방향 (가시 밑변 방향)
    readonly float widthWorld;
    readonly float depthWorld;
    readonly float peekExtend;       // 경고 동안 나오는 최대 정도 (가시 길이 대비)
    readonly Quaternion rotation;    // 오른쪽을 가리키는 삼각형을 안쪽으로 돌리는 회전

    readonly float warnStart;        // 삐져나오기 시작
    readonly float popStart;         // 확 튀어나오기 시작
    readonly float activeStart;      // = time (다 나온 순간)
    readonly float activeEnd;        // = time + holdTime (들어가기 시작하는 순간)

    GameObject root;
    SpriteRenderer warning;   // 다 나올 자리의 반투명 경고
    SpriteRenderer spike;

    public SpikeObstacle(ObstacleData data, ObstacleContext context, SpikeSettings settings, GridSide side)
        : base(data, context)
    {
        this.settings = settings;
        float cell = context.CellSize;

        edgePoint = EdgePoint(context.grid, side, data.line, data.time);
        inward = GridSideUtil.InwardDirection(side);
        across = new Vector2(-inward.y, inward.x);
        widthWorld = settings.width * cell;
        depthWorld = settings.depth * cell;
        peekExtend = Mathf.Clamp01(settings.peekDistance * cell / depthWorld);
        rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(inward.y, inward.x) * Mathf.Rad2Deg);

        float warnTime = data.warnTime > 0f ? data.warnTime : settings.defaultWarnTime;
        float holdTime = data.holdTime > 0f ? data.holdTime : settings.defaultHoldTime;

        activeStart = data.time;
        popStart = data.time - settings.popTime;
        warnStart = Mathf.Min(popStart, data.time - warnTime);
        activeEnd = data.time + holdTime;

        AppearTime = warnStart;
        EndTime = activeEnd + settings.hideTime;
    }

    protected override void OnSpawn()
    {
        root = new GameObject($"Spike {Data.side} {Data.line} @{Data.time:0.##}");
        root.transform.SetParent(Context.container, false);

        warning = ShapeSprites.CreateRenderer("Warning", root.transform, ShapeSprites.Triangle, Context.Color, settings.warningSortingOrder);
        spike = ShapeSprites.CreateRenderer("Spike", root.transform, ShapeSprites.Triangle, Context.Color, settings.spikeSortingOrder);

        // 벽 뒤(격자 밖)에 있는 부분은 그리지 않는다.
        if (Context.clippedMaterial != null) spike.sharedMaterial = Context.clippedMaterial;

        warning.transform.rotation = rotation;
        spike.transform.rotation = rotation;
        spike.enabled = false;
    }

    protected override void OnDespawn()
    {
        if (root != null) UnityEngine.Object.Destroy(root);
        root = null;
    }

    public override void Tick(float songTime, float previousSongTime)
    {
        if (root == null) return;

        float extend = ExtendAt(songTime);
        UpdateWarning(songTime);
        UpdateSpike(extend);

        // 경고(삐져나오기) 동안은 판정하지 않는다.
        if (songTime >= popStart) CheckHit(extend);
    }

    /// <summary>
    /// 노래 시간 t일 때 벽 밖으로 나와 있는 정도 (0 = 벽 속에 다 숨음, 1 = 다 나옴).
    /// 경고 동안 아주 조금씩 삐져나오고, time 직전에 확 튀어나오고, holdTime 뒤 서서히 들어간다.
    /// </summary>
    float ExtendAt(float t)
    {
        if (t < warnStart) return 0f;
        if (t < popStart) return peekExtend * EaseInOut((t - warnStart) / Mathf.Max(0.001f, popStart - warnStart));
        if (t < activeStart) return Mathf.Lerp(peekExtend, 1f, EaseOutCubic((t - popStart) / settings.popTime));
        if (t < activeEnd) return 1f;
        return 1f - EaseInOut((t - activeEnd) / settings.hideTime);
    }

    /// <summary>
    /// 반투명 경고: 다 나올 자리에 나타나서(커지면서 선명해짐) 튀어나오기 직전까지 조금씩 진해지고,
    /// 다 나오는 순간 사라진다.
    /// </summary>
    void UpdateWarning(float t)
    {
        bool visible = t >= warnStart && t < activeStart;
        warning.enabled = visible;
        if (!visible) return;

        float appear = EaseOutCubic((t - warnStart) / settings.warnAppearTime);
        float progress = Mathf.Clamp01((t - warnStart) / Mathf.Max(0.001f, activeStart - warnStart));
        float alpha = settings.warnAlpha * Mathf.Lerp(0.5f, 1f, progress) * appear;

        PlaceTriangle(warning, 1f, Mathf.Lerp(0.8f, 1f, appear));
        warning.color = WithAlpha(Context.Color, alpha);
    }

    /// <summary>가시: 모양은 그대로 두고 벽 밖으로 나온 만큼만 위치를 옮긴다. 벽 뒤 부분은 잘려서 안 보인다.</summary>
    void UpdateSpike(float extend)
    {
        spike.enabled = extend > 0f;
        if (!spike.enabled) return;

        PlaceTriangle(spike, extend, 1f);
        spike.color = Context.Color;
    }

    /// <summary>
    /// 삼각형을 벽에서 extend만큼 나온 자리에 놓는다. extend가 1이면 밑변이 벽에 딱 붙고,
    /// 그보다 작으면 밑변 쪽이 벽 뒤로 들어가 있다. size는 모양 크기 배율 (경고가 나타날 때 쓴다).
    /// </summary>
    void PlaceTriangle(SpriteRenderer renderer, float extend, float size)
    {
        // 삼각형 그림은 크기 1 상자에서 길이 0.9, 밑변 0.84를 차지한다.
        const float spriteLength = 0.9f;
        const float spriteBase = 0.84f;

        // 중심 = 꼭짓점이 벽에서 (길이 x extend)만큼 나온 자리에서 길이의 절반만큼 뒤
        renderer.transform.position = edgePoint + inward * (depthWorld * extend - depthWorld * 0.5f);
        renderer.transform.localScale = new Vector3(depthWorld / spriteLength * size, widthWorld / spriteBase * size, 1f);
    }

    /// <summary>하트가 보이는 가시에 닿으면 피격을 알린다.</summary>
    void CheckHit(float extend)
    {
        if (extend <= 0f || Context.heart == null) return;

        // 판정용 삼각형 (보이는 가시보다 약간 작게). 가시와 같이 움직이므로 벽 뒤 부분은 격자 밖이라 닿을 일이 없다.
        float hitHalfWidth = widthWorld * 0.5f * settings.hitScale;
        Vector2 baseCenter = edgePoint + inward * (depthWorld * extend - depthWorld);
        Vector2 baseA = baseCenter + across * hitHalfWidth;
        Vector2 baseB = baseCenter - across * hitHalfWidth;
        Vector2 tip = edgePoint + inward * (depthWorld * extend - depthWorld * (1f - settings.hitScale));

        // 하트(원)가 삼각형 안에 있거나, 세 변 중 하나에 반지름보다 가까우면 닿은 것이다.
        Vector2 heart = Context.heart.transform.position;
        float radius = Context.heart.HitRadius;
        bool touching = InsideTriangle(heart, baseA, tip, baseB)
            || DistanceToSegment(heart, baseA, tip) <= radius
            || DistanceToSegment(heart, tip, baseB) <= radius
            || DistanceToSegment(heart, baseB, baseA) <= radius;

        // 닿아 있는 동안 계속 알린다. 무적 중에는 PlayerHealth가 무시한다.
        if (touching) Context.onPlayerHit?.Invoke(this);
    }

    static bool InsideTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        float d2 = (c.x - b.x) * (p.y - b.y) - (c.y - b.y) * (p.x - b.x);
        float d3 = (a.x - c.x) * (p.y - c.y) - (a.y - c.y) * (p.x - c.x);
        return (d1 >= 0f && d2 >= 0f && d3 >= 0f) || (d1 <= 0f && d2 <= 0f && d3 <= 0f);
    }
}
