using System;
using UnityEngine;

/// <summary>가시의 모양과 타이밍 설정. LevelPlayer 인스펙터에서 조절한다.</summary>
[Serializable]
public class SpikeSettings
{
    [Header("기본값 (레벨 데이터에 없을 때)")]
    [Tooltip("warnTime이 없거나 0일 때 쓰는 경고 시간 (초)")]
    [Min(0.05f)] public float defaultWarnTime = 0.6f;

    [Tooltip("holdTime이 없거나 0일 때 쓰는 튀어나와 있는 시간 (초)")]
    [Min(0.05f)] public float defaultHoldTime = 0.5f;

    [Header("모양")]
    [Tooltip("가시 밑변의 폭 (칸 대비)")]
    [Range(0.2f, 1f)] public float width = 0.9f;

    [Tooltip("가시가 벽에서 튀어나오는 길이 (칸 대비)")]
    [Range(0.2f, 1f)] public float depth = 0.85f;

    [Tooltip("충돌 판정 크기 (보이는 크기 대비). 약간 작게 둬서 억울한 피격을 줄인다.")]
    [Range(0.3f, 1f)] public float hitScale = 0.8f;

    [Tooltip("경고의 불투명도. 튀어나오기 직전에 이 값이 되고, 처음엔 그 절반에서 시작한다.")]
    [Range(0f, 1f)] public float warnAlpha = 0.3f;

    [Header("움직임 (노래 시간, 초)")]
    [Tooltip("경고가 나타나는 시간")]
    [Min(0.01f)] public float appearTime = 0.1f;

    [Tooltip("벽에서 튀어나오는 데 걸리는 시간")]
    [Min(0.01f)] public float extendTime = 0.08f;

    [Tooltip("튀어나올 때 제 길이보다 더 나왔다 돌아오는 정도. 1.15면 15% 더 나온다.")]
    [Range(1f, 1.5f)] public float overshoot = 1.15f;

    [Tooltip("벽 속으로 들어가며 사라지는 시간")]
    [Min(0.01f)] public float retractTime = 0.15f;

    [Header("그리는 순서")]
    [Tooltip("격자 선(-10)보다 높게")]
    public int warningSortingOrder = -5;

    [Tooltip("하트(10)보다 낮게 둬서 하트를 가리지 않는다.")]
    public int spikeSortingOrder = 7;
}

/// <summary>
/// 가시 (spike).
/// side 쪽 벽에서 line번 줄의 첫 칸을 큰 삼각형 하나로 찌른다.
/// warnTime 동안 같은 모양의 반투명 가시가 경고로 보이다가, time에 벽에서 튀어나와 holdTime 동안 유지되고,
/// 그다음 벽 속으로 들어가며 사라진다. 튀어나와 있는 동안 그 칸에 닿으면 피격이다.
/// </summary>
public class SpikeObstacle : LevelObstacle
{
    readonly SpikeSettings settings;

    readonly Vector2 edgePoint;      // 가시가 붙어 있는 벽 위의 점 (그 줄의 한가운데)
    readonly Vector2 inward;         // 벽에서 격자 안쪽 방향
    readonly Vector2 across;         // 벽을 따라가는 방향 (가시 밑변 방향)
    readonly float widthWorld;
    readonly float depthWorld;
    readonly Quaternion rotation;    // 오른쪽을 가리키는 삼각형을 안쪽으로 돌리는 회전

    readonly float warnStart;
    readonly float activeStart;      // = time
    readonly float activeEnd;        // = time + holdTime

    GameObject root;
    SpriteRenderer warning;
    SpriteRenderer spike;

    public SpikeObstacle(ObstacleData data, ObstacleContext context, SpikeSettings settings, GridSide side)
        : base(data, context)
    {
        this.settings = settings;
        float cell = context.CellSize;

        edgePoint = EdgePoint(context.grid, side, data.line);
        inward = GridSideUtil.InwardDirection(side);
        across = new Vector2(-inward.y, inward.x);
        widthWorld = settings.width * cell;
        depthWorld = settings.depth * cell;
        rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(inward.y, inward.x) * Mathf.Rad2Deg);

        float warnTime = data.warnTime > 0f ? data.warnTime : settings.defaultWarnTime;
        float holdTime = data.holdTime > 0f ? data.holdTime : settings.defaultHoldTime;

        warnStart = data.time - warnTime;
        activeStart = data.time;
        activeEnd = data.time + holdTime;

        AppearTime = warnStart;
        EndTime = activeEnd + settings.retractTime;
    }

    protected override void OnSpawn()
    {
        root = new GameObject($"Spike {Data.side} {Data.line} @{Data.time:0.##}");
        root.transform.SetParent(Context.container, false);

        warning = ShapeSprites.CreateRenderer("Warning", root.transform, ShapeSprites.Triangle, Context.Color, settings.warningSortingOrder);
        spike = ShapeSprites.CreateRenderer("Spike", root.transform, ShapeSprites.Triangle, Context.Color, settings.spikeSortingOrder);
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

        UpdateWarning(songTime);
        UpdateSpikes(songTime);
        CheckHit(songTime);
    }

    /// <summary>경고: 같은 모양의 반투명 가시가 나타나서 튀어나오기 직전까지 조금씩 진해진다.</summary>
    void UpdateWarning(float t)
    {
        bool visible = t < activeStart;
        float appear = EaseOutCubic((t - warnStart) / settings.appearTime);
        float progress = Mathf.Clamp01((t - warnStart) / (activeStart - warnStart));
        float alpha = settings.warnAlpha * Mathf.Lerp(0.5f, 1f, progress) * appear;

        warning.enabled = visible;
        if (visible) PlaceSpike(warning, Mathf.Lerp(0.6f, 1f, appear), alpha);
    }

    /// <summary>가시: 벽에서 날카롭게 튀어나왔다가(조금 더 나왔다 돌아옴), 끝나면 벽 속으로 들어가며 사라진다.</summary>
    void UpdateSpikes(float t)
    {
        bool visible = t >= activeStart && t < EndTime;
        float extend;   // 제 길이 대비 튀어나온 정도
        float alpha = 1f;

        if (t < activeEnd)
        {
            extend = ExtendCurve((t - activeStart) / settings.extendTime);
        }
        else
        {
            float retract = Mathf.Clamp01((t - activeEnd) / settings.retractTime);
            extend = 1f - EaseInOut(retract);
            alpha = 1f - retract;
        }

        spike.enabled = visible && extend > 0f;
        if (spike.enabled) PlaceSpike(spike, extend, alpha);
    }

    /// <summary>0~1 진행에 따라 0에서 overshoot까지 튀어나왔다가 1로 돌아오는 길이</summary>
    float ExtendCurve(float x)
    {
        if (x >= 1f) return 1f;

        const float peakAt = 0.6f;
        return x < peakAt
            ? Mathf.Lerp(0f, settings.overshoot, EaseOutCubic(x / peakAt))
            : Mathf.Lerp(settings.overshoot, 1f, EaseInOut((x - peakAt) / (1f - peakAt)));
    }

    /// <summary>가시를 벽에 밑변을 붙인 채 extend만큼 튀어나온 모습으로 놓는다.</summary>
    void PlaceSpike(SpriteRenderer renderer, float extend, float alpha)
    {
        // 삼각형 그림은 크기 1 상자에서 길이 0.9, 밑변 0.84를 차지한다.
        const float spriteLength = 0.9f;
        const float spriteBase = 0.84f;

        float length = depthWorld * extend;

        // 밑변이 벽에 붙도록, 튀어나온 길이의 절반만큼 안쪽에 중심을 둔다.
        renderer.transform.position = edgePoint + inward * (length * 0.5f);
        renderer.transform.localScale = new Vector3(length / spriteLength, widthWorld / spriteBase, 1f);
        renderer.color = WithAlpha(Context.Color, alpha);
    }

    /// <summary>튀어나와 있는 동안 하트가 그 칸의 가시에 닿으면 피격을 알린다.</summary>
    void CheckHit(float t)
    {
        if (t < activeStart || t >= activeEnd || Context.heart == null) return;

        // 판정용 삼각형 (보이는 가시보다 약간 작게): 밑변은 벽 위, 꼭짓점은 안쪽으로 hitDepth만큼
        float hitDepth = depthWorld * settings.hitScale;
        float hitHalfWidth = widthWorld * 0.5f * settings.hitScale;
        Vector2 baseA = edgePoint + across * hitHalfWidth;
        Vector2 baseB = edgePoint - across * hitHalfWidth;
        Vector2 tip = edgePoint + inward * hitDepth;

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
