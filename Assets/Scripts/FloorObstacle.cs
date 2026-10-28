using System;
using UnityEngine;

/// <summary>바닥 포탄의 모양과 타이밍 설정. LevelPlayer 인스펙터에서 조절한다.</summary>
[Serializable]
public class FloorSettings
{
    [Header("기본값 (레벨 데이터에 없을 때)")]
    [Tooltip("warnTime이 없거나 0일 때 쓰는 경고 시간 (초)")]
    [Min(0.05f)] public float defaultWarnTime = 0.9f;

    [Tooltip("blockTime이 없거나 0일 때 쓰는 막는 시간 (초)")]
    [Min(0.05f)] public float defaultBlockTime = 2f;

    [Header("모양")]
    [Tooltip("솟아오른 정사각형 크기 (칸 대비). 1보다 조금 작게 둬서 격자 선이 보이게 한다.")]
    [Range(0.2f, 1f)] public float blockSize = 0.8f;

    [Tooltip("충돌 판정 크기 (보이는 크기 대비). 약간 작게 둬서 억울한 피격을 줄인다.")]
    [Range(0.3f, 1f)] public float hitScale = 0.8f;

    [Tooltip("경고의 불투명도")]
    [Range(0f, 1f)] public float warnAlpha = 0.3f;

    [Tooltip("경고가 수축해서 튀어나오기 직전에 남는 크기 (칸 대비)")]
    [Range(0f, 1f)] public float warnEndSize = 0.2f;

    [Header("움직임 (노래 시간, 초)")]
    [Tooltip("경고가 나타나는 시간")]
    [Min(0.01f)] public float appearTime = 0.1f;

    [Tooltip("튀어나와 제 크기가 되기까지 걸리는 시간")]
    [Min(0.01f)] public float riseTime = 0.12f;

    [Tooltip("튀어나올 때 제 크기보다 커졌다 돌아오는 정도. 0.1이면 10% 더 커진다.")]
    [Range(0f, 0.5f)] public float riseOvershoot = 0.1f;

    [Tooltip("해제 몇 초 전부터 깜빡이는지")]
    [Min(0f)] public float blinkTime = 0.45f;

    [Tooltip("깜빡이는 빠르기 (초당 횟수)")]
    [Min(0.5f)] public float blinkRate = 8f;

    [Tooltip("깜빡일 때 어두워지는 불투명도")]
    [Range(0f, 1f)] public float blinkAlpha = 0.3f;

    [Tooltip("해제된 뒤 작아지며 사라지는 시간")]
    [Min(0.01f)] public float fadeOutTime = 0.15f;

    [Header("그리는 순서")]
    [Tooltip("격자 선(-10)보다 높게")]
    public int warningSortingOrder = -5;

    [Tooltip("하트(10)보다 낮게 둬서 하트를 가리지 않는다.")]
    public int blockSortingOrder = 7;
}

/// <summary>
/// 바닥 포탄 (floor).
/// 지정한 칸에 warnTime 동안 반투명 경고가 안쪽으로 수축하다가, time에 그 칸에서 정사각형이 튀어나온다.
/// 튀어나오는 순간 하트가 그 칸에 걸쳐 있으면 피격이다.
/// 이후 blockTime 동안 그 칸은 막힌 칸이 되어 하트가 들어갈 수 없다. (들어가려 해도 피해는 없다)
/// </summary>
public class FloorObstacle : LevelObstacle
{
    readonly FloorSettings settings;
    readonly Vector2Int cell;
    readonly Vector2 center;
    readonly float cellWorld;
    readonly float blockWorldSize;

    readonly float warnStart;
    readonly float riseStart;    // = time
    readonly float blockEnd;     // = time + blockTime

    GameObject root;
    SpriteRenderer warning;
    SpriteRenderer block;
    bool isBlocking;

    public FloorObstacle(ObstacleData data, ObstacleContext context, FloorSettings settings, Vector2Int cell)
        : base(data, context)
    {
        this.settings = settings;
        this.cell = cell;

        cellWorld = context.CellSize;
        center = context.grid.CellToWorld(cell.x, cell.y);
        blockWorldSize = settings.blockSize * cellWorld;

        float warnTime = data.warnTime > 0f ? data.warnTime : settings.defaultWarnTime;
        float blockTime = data.blockTime > 0f ? data.blockTime : settings.defaultBlockTime;

        warnStart = data.time - warnTime;
        riseStart = data.time;
        blockEnd = data.time + blockTime;

        AppearTime = warnStart;
        EndTime = blockEnd + settings.fadeOutTime;
    }

    protected override void OnSpawn()
    {
        root = new GameObject($"Floor [{cell.x}, {cell.y}] @{Data.time:0.##}");
        root.transform.SetParent(Context.container, false);

        warning = ShapeSprites.CreateRenderer("Warning", root.transform, ShapeSprites.Square, Context.Color, settings.warningSortingOrder);
        block = ShapeSprites.CreateRenderer("Block", root.transform, ShapeSprites.Square, Context.Color, settings.blockSortingOrder);
        warning.transform.position = center;
        block.transform.position = center;
        block.enabled = false;
    }

    protected override void OnDespawn()
    {
        SetBlocking(false);   // 지워질 때 막아 둔 칸을 꼭 풀어 준다.
        if (root != null) UnityEngine.Object.Destroy(root);
        root = null;
    }

    public override void Tick(float songTime, float previousSongTime)
    {
        if (root == null) return;

        UpdateWarning(songTime);
        UpdateBlock(songTime);

        // 막힌 칸 상태는 노래 시간으로 정한다. (시간을 되돌려도 맞게 풀리고 막힌다)
        SetBlocking(songTime >= riseStart && songTime < blockEnd);

        // 튀어나오는 순간을 지나는 프레임에 한 번만 판정한다.
        if (previousSongTime < riseStart && songTime >= riseStart && songTime < blockEnd)
        {
            CheckHit();
        }
    }

    /// <summary>경고: 칸 크기로 나타나서 튀어나오는 순간까지 안쪽으로 수축한다.</summary>
    void UpdateWarning(float t)
    {
        bool visible = t < riseStart;
        warning.enabled = visible;
        if (!visible) return;

        float appear = EaseOutCubic((t - warnStart) / settings.appearTime);
        float progress = Mathf.Clamp01((t - warnStart) / (riseStart - warnStart));
        float size = cellWorld * Mathf.Lerp(1f, settings.warnEndSize, progress);

        warning.transform.localScale = Vector3.one * (size * Mathf.Lerp(0.8f, 1f, appear));
        warning.color = WithAlpha(Context.Color, settings.warnAlpha * appear);
    }

    /// <summary>솟아오른 정사각형: 튀어나옴 → 막힌 동안 유지 → 해제 직전 깜빡임 → 작아지며 사라짐</summary>
    void UpdateBlock(float t)
    {
        bool visible = t >= riseStart && t < EndTime;
        block.enabled = visible;
        if (!visible) return;

        float scale = 1f;
        float alpha = 1f;

        // 튀어나오기: 작게 시작해서 제 크기보다 살짝 커졌다가 돌아온다.
        float rise = Mathf.Clamp01((t - riseStart) / settings.riseTime);
        if (rise < 1f) scale = RiseCurve(rise);

        // 해제 직전 깜빡여서 곧 풀린다는 것을 알린다.
        if (t < blockEnd && t >= blockEnd - settings.blinkTime)
        {
            bool dim = Mathf.FloorToInt((t - (blockEnd - settings.blinkTime)) * settings.blinkRate * 2f) % 2 == 0;
            if (dim) alpha = settings.blinkAlpha;
        }

        // 해제되면 작아지며 사라진다.
        if (t >= blockEnd)
        {
            float fade = Mathf.Clamp01((t - blockEnd) / settings.fadeOutTime);
            scale *= Mathf.Lerp(1f, 0.5f, fade);
            alpha *= 1f - fade;
        }

        block.transform.localScale = Vector3.one * (blockWorldSize * scale);
        block.color = WithAlpha(Context.Color, alpha);
    }

    /// <summary>0~1 진행에 따라 0.3에서 시작해 1+overshoot까지 커졌다가 1로 돌아오는 크기</summary>
    float RiseCurve(float x)
    {
        const float peakAt = 0.6f;   // 진행의 60% 지점에서 가장 크다.
        float peak = 1f + settings.riseOvershoot;
        return x < peakAt
            ? Mathf.Lerp(0.3f, peak, EaseOutCubic(x / peakAt))
            : Mathf.Lerp(peak, 1f, EaseInOut((x - peakAt) / (1f - peakAt)));
    }

    /// <summary>하트(원)가 튀어나온 정사각형의 판정 영역에 걸쳐 있으면 피격</summary>
    void CheckHit()
    {
        if (Context.heart == null) return;

        Vector2 heartPosition = Context.heart.transform.position;
        float half = blockWorldSize * 0.5f * settings.hitScale;

        // 정사각형 안에서 하트 중심과 가장 가까운 점까지의 거리로 원과 사각형의 겹침을 본다.
        Vector2 offset = heartPosition - center;
        Vector2 closest = new Vector2(Mathf.Clamp(offset.x, -half, half), Mathf.Clamp(offset.y, -half, half));
        if ((offset - closest).magnitude <= Context.heart.HitRadius)
        {
            Context.onPlayerHit?.Invoke(this);
        }
    }

    void SetBlocking(bool value)
    {
        if (value == isBlocking) return;
        isBlocking = value;

        if (value) Context.grid.Block(cell);
        else Context.grid.Unblock(cell);
    }
}
