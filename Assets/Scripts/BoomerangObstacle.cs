using System;
using UnityEngine;

/// <summary>부메랑의 움직임과 모양 설정. LevelPlayer 인스펙터에서 조절한다.</summary>
[Serializable]
public class BoomerangSettings
{
    [Header("움직임")]
    [Tooltip("발사 순간 속도 기본값 (칸/초). 레벨 데이터에 speed가 있으면 그 값을 쓴다. 되돌아올 때도 이 속도로 발사기에 들어온다.")]
    [Min(0.5f)] public float speed = 9f;

    [Tooltip("range가 없거나 0일 때 쓰는 값. 격자 안으로 몇 번째 칸 가운데까지 갔다가 돌아오는지")]
    [Min(0.5f)] public float defaultRange = 3f;

    [Tooltip("빙글빙글 도는 빠르기 (도/초)")]
    public float spinSpeed = 900f;

    [Header("부메랑 모양")]
    [Tooltip("부메랑 크기 (칸 대비)")]
    [Range(0.1f, 1f)] public float size = 0.65f;

    [Tooltip("충돌 판정 크기 (보이는 크기 대비). 약간 작게 둬서 억울한 피격을 줄인다.")]
    [Range(0.3f, 1f)] public float hitScale = 0.75f;

    [Tooltip("부메랑이 나타날 때 커지면서 선명해지는 시간 (초)")]
    [Min(0f)] public float appearTime = 0.06f;

    [Tooltip("발사기로 돌아올 때 작아지며 들어가는 시간 (초)")]
    [Min(0.01f)] public float catchTime = 0.08f;

    [Header("사거리 표시")]
    [Tooltip("켜면 발사기가 들어올 때부터 부메랑이 돌아올 때까지, 날아갈 길과 되돌아오는 지점을 반투명하게 보여 준다.")]
    public bool showRange = true;

    [Tooltip("격자 가장자리에서 되돌아오는 지점까지 깔리는 띠의 폭 (칸 대비)")]
    [Range(0.05f, 1f)] public float pathWidth = 0.35f;

    [Tooltip("띠의 불투명도")]
    [Range(0f, 1f)] public float pathAlpha = 0.15f;

    [Tooltip("되돌아오는 지점에 놓이는 반투명 부메랑 모양의 불투명도")]
    [Range(0f, 1f)] public float markerAlpha = 0.35f;

    [Tooltip("격자 선(-10)보다 높게, 하트(10)보다 낮게")]
    public int rangeSortingOrder = -5;

    [Header("발사기")]
    [Tooltip("발사기 크기 (칸 대비)")]
    [Range(0.1f, 1.5f)] public float launcherSize = 0.6f;

    [Tooltip("발사기 테두리 두께 (크기 대비)")]
    [Range(0.02f, 0.2f)] public float launcherThickness = 0.06f;

    [Tooltip("격자 가장자리에서 발사기 중심까지 거리 (칸 대비)")]
    [Min(0f)] public float launcherDistance = 0.55f;

    [Header("발사기 타이밍 (노래 시간, 초)")]
    [Tooltip("발사 몇 초 전에 발사기가 화면 밖에서 들어오기 시작하는지")]
    [Min(0.05f)] public float leadTime = 1f;

    [Tooltip("발사기가 화면 밖에서 제자리까지 오는 데 걸리는 시간")]
    [Min(0.01f)] public float enterTime = 0.35f;

    [Tooltip("발사 직전 수축하는 시간")]
    [Min(0f)] public float chargeTime = 0.15f;

    [Tooltip("가장 수축했을 때의 크기 비율")]
    [Range(0.3f, 1f)] public float chargeScale = 0.75f;

    [Tooltip("발사 뒤, 그리고 부메랑을 받은 뒤 원래 크기로 돌아오는 시간")]
    [Min(0.01f)] public float recoverTime = 0.1f;

    [Tooltip("부메랑을 받은 뒤 발사기가 남아 있는 시간")]
    [Min(0f)] public float stayTime = 0.15f;

    [Tooltip("발사기가 작아지며 사라지는 시간")]
    [Min(0.01f)] public float exitTime = 0.2f;

    [Header("그리는 순서")]
    public int launcherSortingOrder = 5;

    [Tooltip("하트(10)보다 높게")]
    public int boomerangSortingOrder = 20;
}

/// <summary>
/// 부메랑 (boomerang).
/// 발사기(속 빈 V자)가 화면 밖에서 지정한 줄 옆으로 와서 자리 잡고, V자 부메랑을 쏜다.
/// 부메랑은 빙글빙글 돌며 줄을 따라 날아가다 점점 느려져서 range번째 칸 가운데에서 멈추고,
/// 같은 줄로 점점 빨라지며 되돌아와 발사기로 들어간다. 가는 길과 오는 길 모두 판정한다.
/// time은 부메랑이 처음 격자 가장자리에 닿는 순간이다.
/// 발사기가 들어올 때부터 날아갈 길(반투명한 띠)과 되돌아오는 지점(반투명한 부메랑 모양)을 보여 준다.
///
/// 위치를 노래 시간만으로 계산한다. (일정하게 느려졌다가 같은 식으로 되돌아오는 운동)
/// </summary>
public class BoomerangObstacle : LevelObstacle
{
    readonly BoomerangSettings settings;

    readonly Vector2 inward;            // 날아가는 방향 (격자 안쪽)
    readonly Vector2 restPosition;      // 발사기 자리 (= 출발점이자 돌아오는 곳)
    readonly Vector2 offscreenPosition;
    readonly Vector2 edgePoint;         // 처음 격자에 닿는 곳
    readonly Vector2 turnPoint;         // 멈췄다가 되돌아오는 곳
    readonly float launchSpeed;         // 발사 속도 (초당 월드 거리)
    readonly float deceleration;        // 느려지는 정도 (초당 월드 거리²)
    readonly float flightTime;          // 나갔다 돌아오기까지 걸리는 시간
    readonly float fireTime;
    readonly float returnTime;          // 발사기로 돌아오는 시간
    readonly float enterStart;
    readonly float chargeStart;
    readonly float exitStart;
    readonly float launcherWorldSize;
    readonly float boomerangWorldSize;
    readonly Quaternion launcherRotation;

    GameObject root;
    SpriteRenderer launcher;
    SpriteRenderer boomerang;
    SpriteRenderer rangePath;           // 격자 가장자리에서 되돌아오는 지점까지의 반투명 띠
    SpriteRenderer rangeMarker;         // 되돌아오는 지점의 반투명 부메랑 모양

    public BoomerangObstacle(ObstacleData data, ObstacleContext context, BoomerangSettings settings, GridSide side, float range)
        : base(data, context)
    {
        this.settings = settings;
        float cell = context.CellSize;

        inward = GridSideUtil.InwardDirection(side);
        launcherWorldSize = settings.launcherSize * cell;
        boomerangWorldSize = settings.size * cell;
        launcherRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(inward.y, inward.x) * Mathf.Rad2Deg);

        edgePoint = EdgePoint(context.grid, side, data.line);
        float toEdge = settings.launcherDistance * cell;                 // 발사기에서 격자 가장자리까지
        restPosition = edgePoint - inward * toEdge;
        offscreenPosition = OutsideView(context.view, restPosition, -inward, launcherWorldSize);

        // 발사기에서 되돌아오는 지점까지의 거리. range번째 칸의 가운데에서 멈춘다.
        float turnDistance = toEdge + (range - 0.5f) * cell;
        turnPoint = restPosition + inward * turnDistance;

        // 일정하게 느려져서 turnDistance에서 딱 멈추도록 느려지는 정도를 정한다.
        launchSpeed = (data.speed > 0f ? data.speed : settings.speed) * cell;
        deceleration = launchSpeed * launchSpeed / (2f * turnDistance);
        flightTime = 2f * launchSpeed / deceleration;   // 나갔다 돌아오는 데 걸리는 시간

        // time에 처음 격자 가장자리에 닿도록 쏘는 시간을 거꾸로 계산한다.
        float timeToEdge = (launchSpeed - Mathf.Sqrt(launchSpeed * launchSpeed - 2f * deceleration * toEdge)) / deceleration;
        fireTime = data.time - timeToEdge;
        returnTime = fireTime + flightTime;

        float lead = Mathf.Max(settings.leadTime, settings.enterTime + settings.chargeTime);
        enterStart = fireTime - lead;
        chargeStart = fireTime - settings.chargeTime;
        exitStart = returnTime + settings.stayTime;

        AppearTime = enterStart;
        EndTime = exitStart + settings.exitTime;
    }

    protected override void OnSpawn()
    {
        root = new GameObject($"Boomerang {Data.side} {Data.line} @{Data.time:0.##}");
        root.transform.SetParent(Context.container, false);

        launcher = ShapeSprites.CreateRenderer("Launcher", root.transform,
            ShapeSprites.ChevronOutline(settings.launcherThickness), Context.Color, settings.launcherSortingOrder);
        boomerang = ShapeSprites.CreateRenderer("Boomerang", root.transform,
            ShapeSprites.Chevron, Context.Color, settings.boomerangSortingOrder);

        launcher.transform.rotation = launcherRotation;   // 뾰족한 끝이 격자 쪽을 가리킨다.
        boomerang.enabled = false;

        if (settings.showRange)
        {
            rangePath = ShapeSprites.CreateRenderer("RangePath", root.transform, ShapeSprites.Square, Context.Color, settings.rangeSortingOrder);
            rangeMarker = ShapeSprites.CreateRenderer("RangeMarker", root.transform, ShapeSprites.Chevron, Context.Color, settings.rangeSortingOrder);

            // 띠는 날아가는 방향으로 길게 눕히고, 표시는 되돌아오는 쪽(발사기 쪽)을 가리키게 둔다.
            float pathLength = Vector2.Distance(edgePoint, turnPoint);
            rangePath.transform.position = (edgePoint + turnPoint) * 0.5f;
            rangePath.transform.rotation = launcherRotation;
            rangePath.transform.localScale = new Vector3(pathLength, settings.pathWidth * Context.CellSize, 1f);
            rangePath.enabled = pathLength > 0.001f;

            rangeMarker.transform.position = turnPoint;
            rangeMarker.transform.rotation = launcherRotation * Quaternion.Euler(0f, 0f, 180f);
            rangeMarker.transform.localScale = Vector3.one * boomerangWorldSize;
        }
    }

    protected override void OnDespawn()
    {
        if (root != null) UnityEngine.Object.Destroy(root);
        root = null;
    }

    public override void Tick(float songTime, float previousSongTime)
    {
        if (root == null) return;

        UpdateLauncher(songTime);
        UpdateRange(songTime);
        UpdateBoomerang(songTime, previousSongTime);
    }

    /// <summary>발사기: 들어옴 → 수축 → 발사 → 부메랑을 받으며 한 번 더 움찔 → 작아지며 사라짐</summary>
    void UpdateLauncher(float t)
    {
        float enter = EaseOutCubic((t - enterStart) / settings.enterTime);
        Vector2 position = Vector2.Lerp(offscreenPosition, restPosition, enter);
        float scale = Mathf.Lerp(0.6f, 1f, enter);
        float alpha = enter;

        if (t >= chargeStart && t < fireTime && settings.chargeTime > 0f)
        {
            scale *= Mathf.Lerp(1f, settings.chargeScale, EaseInOut((t - chargeStart) / settings.chargeTime));
        }
        else if (t >= fireTime && t < fireTime + settings.recoverTime)
        {
            scale *= Mathf.Lerp(settings.chargeScale, 1f, EaseOutCubic((t - fireTime) / settings.recoverTime));
        }
        else if (t >= returnTime && t < returnTime + settings.recoverTime)
        {
            // 부메랑을 받는 순간 살짝 움찔한다.
            scale *= Mathf.Lerp(settings.chargeScale, 1f, EaseOutCubic((t - returnTime) / settings.recoverTime));
        }

        if (t >= exitStart)
        {
            float exit = Mathf.Clamp01((t - exitStart) / settings.exitTime);
            alpha *= 1f - exit;
            scale *= Mathf.Lerp(1f, 0.5f, exit);
        }

        launcher.enabled = alpha > 0f;
        launcher.transform.position = position;
        launcher.transform.localScale = Vector3.one * (launcherWorldSize * scale);
        launcher.color = WithAlpha(Context.Color, alpha);
    }

    /// <summary>사거리 표시: 발사기와 함께 나타나고, 부메랑이 발사기로 돌아오면 옅어지며 사라진다.</summary>
    void UpdateRange(float t)
    {
        if (rangeMarker == null) return;

        float alpha = EaseOutCubic((t - enterStart) / settings.enterTime);
        if (t >= returnTime) alpha *= 1f - Mathf.Clamp01((t - returnTime) / settings.exitTime);

        rangePath.color = WithAlpha(Context.Color, settings.pathAlpha * alpha);
        rangeMarker.color = WithAlpha(Context.Color, settings.markerAlpha * alpha);
    }

    /// <summary>부메랑: 돌면서 나갔다가 되돌아온다. 지나온 길 전체로 충돌을 판정한다.</summary>
    void UpdateBoomerang(float t, float previousT)
    {
        bool flying = t >= fireTime && t < returnTime;
        boomerang.enabled = flying;
        if (!flying) return;

        float elapsed = t - fireTime;

        // 나타날 때 커지면서 선명해지고, 돌아와서 발사기로 들어갈 때 작아지며 사라진다.
        float appear = settings.appearTime > 0f ? Mathf.Clamp01(elapsed / settings.appearTime) : 1f;
        float catching = Mathf.Clamp01((returnTime - t) / settings.catchTime);
        float scale = Mathf.Lerp(0.5f, 1f, appear) * Mathf.Lerp(0.4f, 1f, catching);
        float alpha = appear * catching;

        Vector2 position = PositionAt(elapsed);
        boomerang.transform.position = position;
        boomerang.transform.rotation = Quaternion.Euler(0f, 0f, settings.spinSpeed * elapsed);
        boomerang.transform.localScale = Vector3.one * (boomerangWorldSize * scale);
        boomerang.color = WithAlpha(Context.Color, alpha);

        // 지난 프레임 위치부터 지금 위치까지의 선분으로 판정해서, 빨라도 하트를 건너뛰지 않는다.
        if (Context.heart != null)
        {
            Vector2 from = PositionAt(Mathf.Clamp(previousT - fireTime, 0f, flightTime));
            float hitDistance = Context.heart.HitRadius + boomerangWorldSize * 0.5f * settings.hitScale;

            // 닿아 있는 동안 계속 알린다. 무적 중에는 PlayerHealth가 무시하므로,
            // 돌아오는 길에 무적이 끝났다면 다시 맞는다.
            if (DistanceToSegment(Context.heart.transform.position, from, position) <= hitDistance)
            {
                Context.onPlayerHit?.Invoke(this);
            }
        }
    }

    /// <summary>발사 뒤 elapsed초 지났을 때의 위치. 일정하게 느려지며 나갔다가 같은 식으로 되돌아온다.</summary>
    Vector2 PositionAt(float elapsed)
    {
        float distance = launchSpeed * elapsed - 0.5f * deceleration * elapsed * elapsed;
        return restPosition + inward * Mathf.Max(0f, distance);
    }
}
