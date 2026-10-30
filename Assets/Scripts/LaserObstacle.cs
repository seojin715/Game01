using System;
using UnityEngine;

/// <summary>레이저가 발사기의 원에서 나가는 모습</summary>
public enum LaserFireStyle
{
    [Tooltip("원에서 반대쪽 벽까지 아주 빠르게 뻗어 나간다. 판정도 뻗은 만큼만 한다.")]
    Shoot,

    [Tooltip("원부터 반대쪽 벽까지 한꺼번에 켜지며 굵게 터졌다가 제 두께로 돌아온다.")]
    Instant,
}

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

    [Header("발사기")]
    [Tooltip("발사기 날개(삼각형)의 길이 (칸 대비)")]
    [Range(0.2f, 1.5f)] public float launcherLength = 0.7f;

    [Tooltip("날개 테두리 두께 (날개 길이 대비)")]
    [Range(0.02f, 0.15f)] public float launcherThickness = 0.06f;

    [Tooltip("격자 가장자리에서 발사기 가운데(원이 생기는 곳)까지 거리 (칸 대비). 날개 길이의 절반보다 크게 둬야 날개 끝이 격자를 침범하지 않는다.")]
    [Min(0f)] public float launcherDistance = 0.5f;

    [Tooltip("닫혀 있을 때 두 날개 사이 틈 (칸 대비)")]
    [Range(0f, 0.5f)] public float closedGap = 0.06f;

    [Header("발사기 타이밍 (노래 시간, 초)")]
    [Tooltip("경고가 시작될 때 발사기가 화면 밖에서 제자리까지 오는 데 걸리는 시간")]
    [Min(0.01f)] public float enterTime = 0.35f;

    [Tooltip("발사 몇 초 전부터 원이 커지며 날개가 벌어지는지. 레벨의 time에 원 지름이 레이저 두께와 같아지며 발사한다.")]
    [Min(0.01f)] public float openTime = 0.3f;

    [Tooltip("원이 커질 때 커졌다 작아졌다 흔들리는 정도 (다 커진 지름 대비). 0이면 쭉 커진다. 시작과 발사 순간에는 흔들림이 0이라 time에 정확히 레이저 두께가 된다.")]
    [Range(0f, 0.5f)] public float openWobbleAmount = 0.15f;

    [Tooltip("원이 흔들리는 빠르기 (초당 횟수)")]
    [Min(0.1f)] public float openWobbleRate = 12f;

    [Tooltip("레이저가 꺼지고 날개가 닫힌 뒤, 발사기가 화면 밖으로 나가는 시간")]
    [Min(0.01f)] public float exitTime = 0.25f;

    [Header("발사")]
    [Tooltip("Shoot: 원에서 반대쪽 벽까지 빠르게 뻗어 나감 / Instant: 한꺼번에 켜지며 굵게 터짐")]
    public LaserFireStyle fireStyle = LaserFireStyle.Shoot;

    [Tooltip("Shoot일 때, 원에서 반대쪽 벽까지 뻗어 나가는 시간")]
    [Min(0.01f)] public float shootTime = 0.06f;

    [Header("빛")]
    [Tooltip("보이는 레이저 두께 (위 두께 대비). 빛이 번지므로 칸보다 조금 가늘게 둔다. 발사기의 원도 이 두께까지 커진다.")]
    [Range(0.05f, 1f)] public float beamWidth = 0.6f;

    [Tooltip("레이저 밝기. 1보다 크면 Bloom이 번져서 빛난다. 발사기의 원도 커지면서 이 밝기까지 밝아진다.")]
    [Range(1f, 8f)] public float glowIntensity = 2.5f;

    [Header("발동 순간")]
    [Tooltip("Instant일 때, 가는 선에서 굵게 터졌다가 제 두께로 돌아오기까지 걸리는 시간 (초)")]
    [Min(0.01f)] public float spreadTime = 0.12f;

    [Tooltip("Instant일 때, 제 두께보다 굵어지는 정도. 1.3이면 30% 더 굵어졌다 돌아온다.")]
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
    [Tooltip("경고가 나타나는 시간")]
    [Min(0.01f)] public float appearTime = 0.1f;

    [Tooltip("발동이 끝난 뒤 레이저가 가늘어지며 사라지는 시간. 이 동안 원도 작아지고 날개가 닫힌다.")]
    [Min(0.01f)] public float fadeOutTime = 0.15f;

    [Header("그리는 순서")]
    [Tooltip("격자 선(-10)보다 높게, 하트(10)보다 낮게")]
    public int warningSortingOrder = -5;

    [Tooltip("하트(10)보다 낮게 둬서 하트를 가리지 않는다.")]
    public int beamSortingOrder = 8;

    [Tooltip("발사기 가운데의 원")]
    public int coreSortingOrder = 7;

    [Tooltip("발사기 날개")]
    public int launcherSortingOrder = 6;
}

/// <summary>
/// 레이저 (laser).
/// 가로 또는 세로 한 줄에 warnTime 동안 반투명 경고가 보이다가, time에 발동해서 holdTime 동안 유지된다.
/// 발동 중에 그 줄 위에 하트가 있으면 피격이다. 같은 time에 여러 개를 놓으면 동시에 발동한다.
///
/// 발사기는 줄의 한쪽(side)에만 있다. 위아래 두 날개(속 빈 삼각형)가 닫힌 채 화면 밖에서 들어와 자리 잡고,
/// 발사 직전 가운데에 원이 생겨 커지면서 날개를 벌린다. 원 지름이 레이저 두께와 같아지는 순간(time) 원에서 레이저가 나간다.
/// 레이저가 꺼지면 원이 작아지며 날개가 닫히고, 발사기는 화면 밖으로 나간다.
///
/// 레이저는 1보다 밝게 그려서 Bloom으로 빛난다. 발사 중에는 경고 없이 레이저만 보인다.
/// 모든 모양은 노래 시간으로 계산하므로 슬로우에서도 같은 모습이다.
/// </summary>
public class LaserObstacle : LevelObstacle
{
    static readonly int ColorProperty = Shader.PropertyToID("_Color");

    readonly LaserSettings settings;

    readonly bool horizontal;       // true면 가로줄(row), false면 세로줄(col)
    readonly Vector2 direction;     // 레이저가 나가는 방향 (발사기 쪽에서 격자 안쪽)
    readonly Vector2 across;        // 레이저와 수직인 방향 (날개가 벌어지는 방향)
    readonly Quaternion rotation;   // 오른쪽을 향하는 날개 그림을 레이저 방향으로 돌리는 회전

    readonly Vector2 origin;        // 발사기 가운데 = 원 = 레이저가 시작하는 곳
    readonly Vector2 offscreen;     // 발사기가 들어오기 전/나간 뒤의 자리
    readonly float beamLength;      // 원에서 반대쪽 벽까지
    readonly Vector2 gridCenter;    // 그 줄의 격자 부분 한가운데 (경고 자리)
    readonly float gridLength;      // 그 줄의 격자 부분 길이
    readonly float beamThickness;   // 월드 단위 두께 (경고 두께, 판정 기준)
    readonly float coreDiameter;    // 다 커진 원의 지름 = 보이는 레이저 두께
    readonly float wingLength;
    readonly float flickerSeed;     // 레이저마다 떨림이 조금씩 다르게

    readonly float warnStart;       // 경고 시작 = 발사기가 들어오기 시작
    readonly float openStart;       // 원이 커지며 날개가 벌어지기 시작
    readonly float activeStart;     // = time
    readonly float activeEnd;       // = time + holdTime
    readonly float closeEnd;        // 레이저가 사라지고 날개가 다 닫힌 순간
    readonly float exitEnd;         // 발사기가 화면 밖으로 다 나간 순간

    GameObject root;
    SpriteRenderer warning;
    SpriteRenderer beam;
    SpriteRenderer core;            // 발사기 가운데의 원
    SpriteRenderer wingTop;
    SpriteRenderer wingBottom;
    Material beamMaterial;          // 레이저를 1보다 밝게 그리는 머티리얼 (레이저마다 하나)
    Material coreMaterial;          // 원을 1보다 밝게 그리는 머티리얼

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

    /// <summary>side를 적지 않았을 때 발사기 쪽. 가로줄은 왼쪽, 세로줄은 아래쪽.</summary>
    public static GridSide DefaultSide(bool horizontal) => horizontal ? GridSide.Left : GridSide.Bottom;

    public LaserObstacle(ObstacleData data, ObstacleContext context, LaserSettings settings, bool horizontal, GridSide side)
        : base(data, context)
    {
        this.settings = settings;
        this.horizontal = horizontal;

        float cell = context.CellSize;
        var grid = context.grid;

        direction = GridSideUtil.InwardDirection(side);
        across = new Vector2(-direction.y, direction.x);
        rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        // 레이저가 켜지는 시간의 격자에 맞춘다. (벽이 움직여 격자가 줄거나 늘어도 그때 길이만큼)
        var bounds = grid.BoundsAt(data.time);
        Vector2 nearEdge = EdgePoint(grid, side, data.line, data.time);   // 발사기 쪽 벽
        gridLength = (horizontal ? bounds.Width : bounds.Height) * cell;
        gridCenter = nearEdge + direction * (gridLength * 0.5f);

        origin = nearEdge - direction * (settings.launcherDistance * cell);
        beamLength = gridLength + settings.launcherDistance * cell;
        wingLength = settings.launcherLength * cell;
        offscreen = OutsideView(context.view, origin, -direction, wingLength);

        beamThickness = settings.thickness * cell;
        coreDiameter = beamThickness * settings.beamWidth;
        flickerSeed = data.line * 1.7f + (horizontal ? 0f : 0.9f);

        float warnTime = data.warnTime > 0f ? data.warnTime : settings.defaultWarnTime;
        float holdTime = data.holdTime > 0f ? data.holdTime : settings.defaultHoldTime;

        warnStart = data.time - warnTime;
        openStart = Mathf.Max(warnStart, data.time - settings.openTime);
        activeStart = data.time;
        activeEnd = data.time + holdTime;
        closeEnd = activeEnd + settings.fadeOutTime;
        exitEnd = closeEnd + settings.exitTime;

        AppearTime = warnStart;
        EndTime = exitEnd;
    }

    protected override void OnSpawn()
    {
        root = new GameObject($"Laser {Data.axis} {Data.line} @{Data.time:0.##}");
        root.transform.SetParent(Context.container, false);

        var square = ShapeSprites.Square;
        var wing = ShapeSprites.LaserWingOutline(settings.launcherThickness);
        warning = ShapeSprites.CreateRenderer("Warning", root.transform, square, Context.Color, settings.warningSortingOrder);
        beam = ShapeSprites.CreateRenderer("Beam", root.transform, square, Context.Color, settings.beamSortingOrder);
        core = ShapeSprites.CreateRenderer("Core", root.transform, ShapeSprites.Circle, Context.Color, settings.coreSortingOrder);
        wingTop = ShapeSprites.CreateRenderer("WingTop", root.transform, wing, Context.Color, settings.launcherSortingOrder);
        wingBottom = ShapeSprites.CreateRenderer("WingBottom", root.transform, wing, Context.Color, settings.launcherSortingOrder);

        // 날개 그림은 안쪽 변이 아래에 붙어 있으므로, 아래 날개는 위아래로 뒤집는다.
        wingTop.transform.rotation = rotation;
        wingBottom.transform.rotation = rotation;
        wingBottom.flipY = true;

        // 렌더러 색은 1을 넘을 수 없으므로, 레이저와 원의 밝기는 머티리얼 색에 넣는다.
        beamMaterial = SpriteMaterial.Create("LaserBeam");
        if (beamMaterial != null) beam.sharedMaterial = beamMaterial;
        coreMaterial = SpriteMaterial.Create("LaserCore");
        if (coreMaterial != null) core.sharedMaterial = coreMaterial;

        warning.transform.position = gridCenter;
        warning.transform.localScale = BarScale(gridLength, 0f);
        beam.enabled = false;
        core.enabled = false;
    }

    protected override void OnDespawn()
    {
        if (beamMaterial != null) UnityEngine.Object.Destroy(beamMaterial);
        if (coreMaterial != null) UnityEngine.Object.Destroy(coreMaterial);
        beamMaterial = null;
        coreMaterial = null;

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
        UpdateLauncher(songTime);
        CheckHit(songTime);
    }

    /// <summary>경고: 그 줄의 격자 부분에 나타나서 발동 직전까지 조금씩 진해지고, 발동하면 사라진다.</summary>
    void UpdateWarning(float t)
    {
        bool visible = t < activeStart;
        warning.enabled = visible;
        if (!visible) return;

        float appear = EaseOutCubic((t - warnStart) / settings.appearTime);
        float progress = Mathf.Clamp01((t - warnStart) / (activeStart - warnStart));
        float alpha = settings.warnAlpha * Mathf.Lerp(0.5f, 1f, progress) * appear;

        // 나타날 때 두께가 살짝 커지면서 선명해진다.
        warning.transform.localScale = BarScale(gridLength, beamThickness * Mathf.Lerp(0.6f, 1f, appear));
        warning.color = WithAlpha(Context.Color, alpha);
    }

    /// <summary>
    /// 레이저: 원에서 나가서(Shoot는 뻗어 나가고, Instant는 굵게 터지며) 번쩍이고, 유지 중엔 떨리고,
    /// 끝나면 가늘어지며 사라진다.
    /// </summary>
    void UpdateBeam(float t)
    {
        bool visible = t >= activeStart && t < closeEnd;
        beam.enabled = visible;
        if (!visible) return;

        float widthScale;    // 제 두께 대비 굵기
        float alpha = 1f;

        if (t < activeEnd)
        {
            widthScale = settings.fireStyle == LaserFireStyle.Instant
                ? SpreadCurve((t - activeStart) / settings.spreadTime)
                : 1f;
            widthScale *= FlickerScale(t);
        }
        else
        {
            float fade = Mathf.Clamp01((t - activeEnd) / settings.fadeOutTime);
            widthScale = 1f - EaseInOut(fade);
            alpha = 1f - fade;
        }

        // 원에서 지금 닿은 곳까지 막대를 놓는다.
        float reach = ReachAt(t);
        beam.transform.position = origin + direction * (reach * 0.5f);
        beam.transform.localScale = BarScale(reach, coreDiameter * widthScale);
        beam.color = WithAlpha(Context.Color, alpha);

        // 1보다 밝게 그려서 Bloom이 번지게 한다. (화면 반전 중에는 검은색이라 빛나지 않는다)
        if (beamMaterial != null)
        {
            beamMaterial.SetColor(ColorProperty, SpriteMaterial.Glow(Color.white, settings.glowIntensity * BrightnessAt(t)));
        }
    }

    /// <summary>
    /// 발사기: 닫힌 채 화면 밖에서 들어옴 → 발사 직전 원이 커지며 날개가 벌어짐 → time에 원 지름 = 레이저 두께 →
    /// 레이저가 꺼지면 원이 작아지며 날개가 닫힘 → 화면 밖으로 나감
    /// </summary>
    void UpdateLauncher(float t)
    {
        // 들어오고 나가기
        float enter = EaseOutCubic((t - warnStart) / settings.enterTime);
        float exit = Mathf.Clamp01((t - closeEnd) / settings.exitTime);
        float exitEased = exit * exit * exit;   // 처음엔 천천히, 나중엔 빠르게 나간다.
        Vector2 position = t < closeEnd
            ? Vector2.Lerp(offscreen, origin, enter)
            : Vector2.Lerp(origin, offscreen, exitEased);
        float alpha = enter * (1f - exit);

        // 원 크기와 날개 사이 거리
        float open = OpenAt(t);
        float coreScale = t >= activeStart && t < activeEnd ? FlickerScale(t) : 1f;
        float coreSize = coreDiameter * open * coreScale;
        float gap = Mathf.Max(settings.closedGap * Context.CellSize * 0.5f, coreSize * 0.5f);

        // 날개 그림의 안쪽 변은 상자 가장자리에 있으므로, 상자 가운데를 (틈 + 길이 절반)만큼 바깥에 둔다.
        Vector2 wingOffset = across * (gap + wingLength * 0.5f);
        PlaceWing(wingTop, position + wingOffset, alpha);
        PlaceWing(wingBottom, position - wingOffset, alpha);

        core.enabled = coreSize > 0.001f && alpha > 0f;
        if (core.enabled)
        {
            core.transform.position = position;
            core.transform.localScale = Vector3.one * coreSize;
            core.color = WithAlpha(Context.Color, alpha);

            // 커지면서 레이저 밝기까지 밝아지고, 발동 중에는 레이저와 같이 번쩍이고 떨린다.
            if (coreMaterial != null)
            {
                float glow = Mathf.Lerp(1f, settings.glowIntensity, open) * BrightnessAt(t);
                coreMaterial.SetColor(ColorProperty, SpriteMaterial.Glow(Color.white, glow));
            }
        }
    }

    void PlaceWing(SpriteRenderer wing, Vector2 position, float alpha)
    {
        wing.enabled = alpha > 0f;
        wing.transform.position = position;
        wing.transform.localScale = Vector3.one * wingLength;
        wing.color = WithAlpha(Context.Color, alpha);
    }

    /// <summary>
    /// 노래 시간 t일 때 발사기가 벌어진 정도 (0 = 닫힘, 1 = 원 지름이 레이저 두께).
    /// 벌어지는 동안은 커졌다 작아졌다 불안정하게 흔들리며 커진다. 흔들림은 시작과 발사 순간에 0이다.
    /// </summary>
    float OpenAt(float t)
    {
        if (t < openStart) return 0f;
        if (t < activeStart)
        {
            float progress = Mathf.Clamp01((t - openStart) / Mathf.Max(0.001f, activeStart - openStart));
            float envelope = Mathf.Sin(progress * Mathf.PI);   // 가운데쯤에서 가장 크게 흔들린다.
            float phase = (t - openStart) * settings.openWobbleRate * Mathf.PI * 2f + flickerSeed;
            float wobble = 0.6f * Mathf.Sin(phase) + 0.4f * Mathf.Sin(phase * 1.61f + 2.1f);   // 단조롭지 않게 두 사인파를 섞는다.
            return Mathf.Max(0f, EaseInOut(progress) + settings.openWobbleAmount * envelope * wobble);
        }
        if (t < activeEnd) return 1f;
        return 1f - EaseInOut((t - activeEnd) / settings.fadeOutTime);
    }

    /// <summary>원에서 레이저 끝까지의 길이. Shoot면 time부터 빠르게 뻗어 나가고, Instant면 처음부터 끝까지 다 켜져 있다.</summary>
    float ReachAt(float t)
    {
        if (t < activeStart) return 0f;
        if (settings.fireStyle == LaserFireStyle.Instant) return beamLength;
        return beamLength * EaseOutCubic((t - activeStart) / settings.shootTime);
    }

    /// <summary>발동 중 밝기 배율: 발동 순간 번쩍였다가 원래 밝기로 돌아오고, 유지 중엔 떨린다.</summary>
    float BrightnessAt(float t)
    {
        if (t < activeStart || t >= activeEnd) return 1f;

        float flash = 1f - Mathf.Clamp01((t - activeStart) / settings.flashTime);
        return Mathf.Lerp(1f, settings.flashBoost, flash * flash) * FlickerScale(t);
    }

    /// <summary>0~1 진행에 따라 0.3에서 시작해 overshoot까지 굵어졌다가 1로 돌아오는 굵기 (Instant)</summary>
    float SpreadCurve(float x)
    {
        if (x >= 1f) return 1f;

        const float peakAt = 0.35f;   // 진행의 35% 지점에서 가장 굵다.
        return x < peakAt
            ? Mathf.Lerp(0.3f, settings.overshoot, EaseOutCubic(x / peakAt))
            : Mathf.Lerp(settings.overshoot, 1f, EaseInOut((x - peakAt) / (1f - peakAt)));
    }

    /// <summary>유지 중 떨림 배율 (1 ± flickerAmount). 노래 시간으로 정하므로 슬로우에서도 같은 모습이다.</summary>
    float FlickerScale(float t) => 1f + settings.flickerAmount * Flicker(t);

    /// <summary>-1 ~ 1 사이로 빠르게 불규칙하게 떨리는 값. 사인파 두 개를 섞어 단조롭지 않게 한다.</summary>
    float Flicker(float t)
    {
        float phase = t * settings.flickerRate * Mathf.PI * 2f + flickerSeed;
        return 0.6f * Mathf.Sin(phase) + 0.4f * Mathf.Sin(phase * 1.73f + 1.3f);
    }

    /// <summary>발동 중에 하트가 레이저(원에서 지금 닿은 곳까지) 위에 있으면 피격을 알린다.</summary>
    void CheckHit(float t)
    {
        bool active = t >= activeStart && t < activeEnd;
        bool onBeam = active && Context.heart != null && IsTouching(Context.heart.transform.position, Context.heart.HitRadius, ReachAt(t));

        // 레이저 위에 있는 동안 계속 알린다. 무적 중에는 PlayerHealth가 무시하므로,
        // 무적이 끝났는데도 아직 레이저 위에 있으면 다시 맞는다.
        if (onBeam) Context.onPlayerHit?.Invoke(this);
    }

    /// <summary>하트(원)가 레이저(직사각형)의 판정 영역에 닿는지. 판정 두께는 보이는 떨림과 상관없이 고정이다.</summary>
    bool IsTouching(Vector2 heartPosition, float heartRadius, float reach)
    {
        Vector2 offset = heartPosition - origin;
        float along = Vector2.Dot(offset, direction);   // 원에서 레이저 방향으로 간 거리
        float side = Vector2.Dot(offset, across);       // 레이저와 수직 방향 거리

        float halfHitThickness = beamThickness * 0.5f * settings.hitScale;
        return Mathf.Abs(side) <= halfHitThickness + heartRadius
            && along >= -heartRadius
            && along <= reach + heartRadius;
    }

    /// <summary>줄 방향 길이와 두께로 막대(정사각형 그림)의 크기를 정한다.</summary>
    Vector3 BarScale(float length, float thickness)
    {
        return horizontal
            ? new Vector3(length, thickness, 1f)
            : new Vector3(thickness, length, 1f);
    }
}
