using System;
using UnityEngine;

/// <summary>레이저의 모양과 타이밍 설정. LevelPlayer 인스펙터에서 조절한다.</summary>
[Serializable]
public class LaserSettings
{
    [Header("기본값 (레벨 데이터에 없을 때)")]
    [Tooltip("warnTime이 없거나 0일 때 쓰는 경고 시간 (초)")]
    [Min(0.05f)] public float defaultWarnTime = 0.9f;

    [Tooltip("holdTime이 없거나 0일 때 쓰는 발동 시간 (초)")]
    [Min(0.05f)] public float defaultHoldTime = 0.4f;

    [Header("모양")]
    [Tooltip("레이저 칸의 두께 (칸 대비). 1이면 칸 너비. 경고와 충돌 판정이 이 두께를 기준으로 한다.")]
    [Range(0.1f, 1f)] public float thickness = 1f;

    [Tooltip("충돌 판정 두께 (보이는 두께 대비). 약간 작게 둬서 억울한 피격을 줄인다.")]
    [Range(0.3f, 1f)] public float hitScale = 0.8f;

    [Tooltip("경고의 불투명도. 발동 직전에 이 값이 되고, 처음엔 그 절반에서 시작한다.")]
    [Range(0f, 1f)] public float warnAlpha = 0.3f;

    [Tooltip("발사구(줄 양 끝의 작은 정사각형) 크기 (칸 대비)")]
    [Range(0.05f, 0.8f)] public float emitterSize = 0.25f;

    [Tooltip("격자 가장자리에서 발사구 중심까지 거리 (칸 대비)")]
    [Min(0f)] public float emitterDistance = 0.35f;

    [Header("빛")]
    [Tooltip("보이는 레이저 두께 (위 두께 대비). 빛이 번지므로 칸보다 조금 가늘게 둔다.")]
    [Range(0.05f, 1f)] public float beamWidth = 0.6f;

    [Tooltip("레이저 밝기. 1보다 크면 Bloom이 번져서 빛난다.")]
    [Range(1f, 8f)] public float glowIntensity = 2.5f;

    [Header("발동 순간")]
    [Tooltip("발동할 때 가는 선에서 굵게 터졌다가 제 두께로 돌아오기까지 걸리는 시간 (초)")]
    [Min(0.01f)] public float spreadTime = 0.12f;

    [Tooltip("발동할 때 제 두께보다 굵어지는 정도. 1.3이면 30% 더 굵어졌다 돌아온다.")]
    [Range(1f, 2f)] public float overshoot = 1.3f;

    [Tooltip("발동 순간 레이저가 더 밝아지는 배율")]
    [Range(1f, 4f)] public float flashBoost = 2f;

    [Tooltip("발동 순간 밝아진 레이저가 원래 밝기로 돌아오는 시간 (초)")]
    [Min(0.01f)] public float flashTime = 0.15f;

    [Header("유지 중 떨림")]
    [Tooltip("켜져 있는 동안 굵기와 밝기가 떨리는 정도. 0.05면 ±5%")]
    [Range(0f, 0.3f)] public float flickerAmount = 0.05f;

    [Tooltip("떨리는 빠르기 (초당 횟수)")]
    [Min(0.1f)] public float flickerRate = 25f;

    [Header("화면 흔들림")]
    [Tooltip("켜면 레이저가 켜지는 순간 화면이 살짝 흔들린다. (여러 줄이 동시에 켜져도 더해지지 않는다)")]
    public bool shakeOnActivate = true;

    [Tooltip("흔들리는 최대 거리 (칸 대비). 0.06이면 칸의 6%")]
    [Range(0f, 0.5f)] public float shakeAmount = 0.06f;

    [Tooltip("흔들림이 잦아들기까지 걸리는 시간 (실제 초)")]
    [Min(0.01f)] public float shakeDuration = 0.15f;

    [Header("움직임 (노래 시간, 초)")]
    [Tooltip("경고와 발사구가 나타나는 시간")]
    [Min(0.01f)] public float appearTime = 0.1f;

    [Tooltip("발동이 끝난 뒤 가늘어지며 사라지는 시간")]
    [Min(0.01f)] public float fadeOutTime = 0.15f;

    [Header("그리는 순서")]
    [Tooltip("격자 선(-10)보다 높게, 하트(10)보다 낮게")]
    public int warningSortingOrder = -5;

    [Tooltip("하트(10)보다 낮게 둬서 하트를 가리지 않는다.")]
    public int beamSortingOrder = 8;

    public int emitterSortingOrder = 6;
}

/// <summary>
/// 레이저 (laser).
/// 가로 또는 세로 한 줄에 warnTime 동안 반투명 경고가 보이다가, time에 발동해서 holdTime 동안 유지된다.
/// 발동 중에 그 줄 위에 하트가 있으면 피격이다. 같은 time에 여러 개를 놓으면 동시에 발동한다.
///
/// 레이저는 1보다 밝게 그려서 Bloom으로 빛난다. 발사 중에는 경고나 반투명한 띠 없이 레이저만 보인다.
/// 발동 순간 굵게 터졌다 돌아오며 번쩍이고, 유지 중에는 빠르게 조금씩 떨린다.
/// 모든 모양은 노래 시간으로 계산하므로 슬로우에서도 같은 모습이다.
/// </summary>
public class LaserObstacle : LevelObstacle
{
    static readonly int ColorProperty = Shader.PropertyToID("_Color");

    readonly LaserSettings settings;

    readonly bool horizontal;       // true면 가로줄(row), false면 세로줄(col)
    readonly Vector2 center;        // 레이저 한가운데
    readonly float length;          // 레이저 길이 (발사구에서 발사구까지)
    readonly float beamThickness;   // 월드 단위 두께 (경고 두께, 판정 기준)
    readonly float emitterWorldSize;
    readonly Vector2 emitterOffset; // 가운데에서 발사구까지
    readonly float flickerSeed;     // 레이저마다 떨림이 조금씩 다르게

    readonly float warnStart;
    readonly float activeStart;     // = time
    readonly float activeEnd;       // = time + holdTime

    GameObject root;
    SpriteRenderer warning;
    SpriteRenderer beam;
    SpriteRenderer emitterA;
    SpriteRenderer emitterB;
    Material beamMaterial;          // 레이저를 1보다 밝게 그리는 머티리얼 (레이저마다 하나)

    /// <summary>"row" 또는 "col"("column")을 읽는다. 가로줄이면 horizontal이 true.</summary>
    public static bool TryParseAxis(string text, out bool horizontal)
    {
        switch (text == null ? "" : text.Trim().ToLowerInvariant())
        {
            case "row": horizontal = true; return true;
            case "col":
            case "column": horizontal = false; return true;
            default: horizontal = true; return false;
        }
    }

    public LaserObstacle(ObstacleData data, ObstacleContext context, LaserSettings settings, bool horizontal)
        : base(data, context)
    {
        this.settings = settings;
        this.horizontal = horizontal;

        float cell = context.CellSize;
        var grid = context.grid;
        Vector2 gridCenter = grid.transform.position;

        // 줄의 가운데 좌표. 가로줄이면 y, 세로줄이면 x가 그 줄에 맞춰진다.
        center = horizontal
            ? new Vector2(gridCenter.x, grid.CellToWorld(0, data.line).y)
            : new Vector2(grid.CellToWorld(data.line, 0).x, gridCenter.y);

        float gridLength = horizontal ? grid.Width : grid.Height;
        float edgeToEmitter = settings.emitterDistance * cell;
        length = gridLength + edgeToEmitter * 2f;
        beamThickness = settings.thickness * cell;
        emitterWorldSize = settings.emitterSize * cell;
        emitterOffset = (horizontal ? Vector2.right : Vector2.up) * (length * 0.5f);
        flickerSeed = data.line * 1.7f + (horizontal ? 0f : 0.9f);

        float warnTime = data.warnTime > 0f ? data.warnTime : settings.defaultWarnTime;
        float holdTime = data.holdTime > 0f ? data.holdTime : settings.defaultHoldTime;

        warnStart = data.time - warnTime;
        activeStart = data.time;
        activeEnd = data.time + holdTime;

        AppearTime = warnStart;
        EndTime = activeEnd + settings.fadeOutTime;
    }

    protected override void OnSpawn()
    {
        root = new GameObject($"Laser {Data.axis} {Data.line} @{Data.time:0.##}");
        root.transform.SetParent(Context.container, false);

        var square = ShapeSprites.Square;
        warning = ShapeSprites.CreateRenderer("Warning", root.transform, square, Context.Color, settings.warningSortingOrder);
        beam = ShapeSprites.CreateRenderer("Beam", root.transform, square, Context.Color, settings.beamSortingOrder);
        emitterA = ShapeSprites.CreateRenderer("EmitterA", root.transform, square, Context.Color, settings.emitterSortingOrder);
        emitterB = ShapeSprites.CreateRenderer("EmitterB", root.transform, square, Context.Color, settings.emitterSortingOrder);

        // 렌더러 색은 1을 넘을 수 없으므로, 레이저 밝기는 머티리얼 색에 넣는다.
        beamMaterial = SpriteMaterial.Create("LaserBeam");
        if (beamMaterial != null) beam.sharedMaterial = beamMaterial;

        warning.transform.position = center;
        beam.transform.position = center;
        emitterA.transform.position = center - emitterOffset;
        emitterB.transform.position = center + emitterOffset;
        beam.enabled = false;
    }

    protected override void OnDespawn()
    {
        if (beamMaterial != null) UnityEngine.Object.Destroy(beamMaterial);
        beamMaterial = null;

        if (root != null) UnityEngine.Object.Destroy(root);
        root = null;
    }

    public override void Tick(float songTime, float previousSongTime)
    {
        if (root == null) return;

        // 켜지는 순간을 지나는 프레임에 화면을 살짝 흔든다.
        if (settings.shakeOnActivate && previousSongTime < activeStart && songTime >= activeStart && songTime < activeEnd)
        {
            CameraShake.Request(settings.shakeAmount * Context.CellSize, settings.shakeDuration);
        }

        UpdateWarning(songTime);
        UpdateBeam(songTime);
        UpdateEmitters(songTime);
        CheckHit(songTime);
    }

    /// <summary>경고: 나타나서 발동 직전까지 조금씩 진해지고, 발동하면 사라진다.</summary>
    void UpdateWarning(float t)
    {
        bool visible = t < activeStart;
        warning.enabled = visible;
        if (!visible) return;

        float appear = EaseOutCubic((t - warnStart) / settings.appearTime);
        float progress = Mathf.Clamp01((t - warnStart) / (activeStart - warnStart));
        float alpha = settings.warnAlpha * Mathf.Lerp(0.5f, 1f, progress) * appear;

        // 나타날 때 두께가 살짝 커지면서 선명해진다.
        SetBar(warning, beamThickness * Mathf.Lerp(0.6f, 1f, appear), alpha);
    }

    /// <summary>
    /// 레이저: 발동하면 굵게 터졌다가 제 두께로 돌아오며 번쩍이고, 유지 중엔 떨리고,
    /// 끝나면 가늘어지며 사라진다.
    /// </summary>
    void UpdateBeam(float t)
    {
        bool visible = t >= activeStart && t < EndTime;
        beam.enabled = visible;
        if (!visible) return;

        float widthScale;    // 제 두께 대비 굵기
        float alpha = 1f;
        float brightness = 1f;

        if (t < activeEnd)
        {
            widthScale = SpreadCurve((t - activeStart) / settings.spreadTime);

            // 발동 순간 번쩍였다가 원래 밝기로 돌아온다.
            float flash = 1f - Mathf.Clamp01((t - activeStart) / settings.flashTime);
            brightness = Mathf.Lerp(1f, settings.flashBoost, flash * flash);

            // 유지 중 떨림 (노래 시간으로 정하므로 슬로우에서도 같은 모습)
            float flicker = 1f + settings.flickerAmount * Flicker(t);
            widthScale *= flicker;
            brightness *= flicker;
        }
        else
        {
            float fade = Mathf.Clamp01((t - activeEnd) / settings.fadeOutTime);
            widthScale = 1f - EaseInOut(fade);
            alpha = 1f - fade;
        }

        SetBar(beam, beamThickness * settings.beamWidth * widthScale, alpha);

        // 1보다 밝게 그려서 Bloom이 번지게 한다. (화면 반전 중에는 검은색이라 빛나지 않는다)
        if (beamMaterial != null)
        {
            beamMaterial.SetColor(ColorProperty, SpriteMaterial.Glow(Color.white, settings.glowIntensity * brightness));
        }
    }

    /// <summary>0~1 진행에 따라 0.3에서 시작해 overshoot까지 굵어졌다가 1로 돌아오는 굵기</summary>
    float SpreadCurve(float x)
    {
        if (x >= 1f) return 1f;

        const float peakAt = 0.35f;   // 진행의 35% 지점에서 가장 굵다.
        return x < peakAt
            ? Mathf.Lerp(0.3f, settings.overshoot, EaseOutCubic(x / peakAt))
            : Mathf.Lerp(settings.overshoot, 1f, EaseInOut((x - peakAt) / (1f - peakAt)));
    }

    /// <summary>-1 ~ 1 사이로 빠르게 불규칙하게 떨리는 값. 사인파 두 개를 섞어 단조롭지 않게 한다.</summary>
    float Flicker(float t)
    {
        float phase = t * settings.flickerRate * Mathf.PI * 2f + flickerSeed;
        return 0.6f * Mathf.Sin(phase) + 0.4f * Mathf.Sin(phase * 1.73f + 1.3f);
    }

    /// <summary>발사구: 경고와 함께 나타나고, 레이저가 사라질 때 같이 작아지며 사라진다.</summary>
    void UpdateEmitters(float t)
    {
        float appear = EaseOutCubic((t - warnStart) / settings.appearTime);
        float scale = Mathf.Lerp(0.5f, 1f, appear);
        float alpha = appear;

        if (t >= activeEnd)
        {
            float fade = Mathf.Clamp01((t - activeEnd) / settings.fadeOutTime);
            scale *= Mathf.Lerp(1f, 0.5f, fade);
            alpha *= 1f - fade;
        }

        SetEmitter(emitterA, scale, alpha);
        SetEmitter(emitterB, scale, alpha);
    }

    void SetEmitter(SpriteRenderer emitter, float scale, float alpha)
    {
        emitter.transform.localScale = Vector3.one * (emitterWorldSize * scale);
        emitter.color = WithAlpha(Context.Color, alpha);
    }

    /// <summary>발동 중에 하트가 레이저 위에 있으면 피격을 알린다.</summary>
    void CheckHit(float t)
    {
        bool active = t >= activeStart && t < activeEnd;
        bool onBeam = active && Context.heart != null && IsTouching(Context.heart.transform.position, Context.heart.HitRadius);

        // 레이저 위에 있는 동안 계속 알린다. 무적 중에는 PlayerHealth가 무시하므로,
        // 무적이 끝났는데도 아직 레이저 위에 있으면 다시 맞는다.
        if (onBeam) Context.onPlayerHit?.Invoke(this);
    }

    /// <summary>하트(원)가 레이저(직사각형)의 판정 영역에 닿는지. 판정은 보이는 떨림과 상관없이 고정이다.</summary>
    bool IsTouching(Vector2 heartPosition, float heartRadius)
    {
        Vector2 offset = heartPosition - center;
        float across = horizontal ? offset.y : offset.x;   // 줄과 수직 방향 거리
        float along = horizontal ? offset.x : offset.y;    // 줄을 따라가는 방향 거리

        float halfHitThickness = beamThickness * 0.5f * settings.hitScale;
        return Mathf.Abs(across) <= halfHitThickness + heartRadius
            && Mathf.Abs(along) <= length * 0.5f + heartRadius;
    }

    /// <summary>가운데 기준으로 긴 막대 모양을 맞춘다.</summary>
    void SetBar(SpriteRenderer bar, float thickness, float alpha)
    {
        bar.transform.localScale = horizontal
            ? new Vector3(length, thickness, 1f)
            : new Vector3(thickness, length, 1f);
        bar.color = WithAlpha(Context.Color, alpha);
    }
}
