using System;
using UnityEngine;

/// <summary>일반 포탄의 모양과 타이밍 설정. LevelPlayer 인스펙터에서 조절한다.</summary>
[Serializable]
public class CannonSettings
{
    [Header("포탄")]
    [Tooltip("포탄 속도 기본값 (칸/초). 레벨 데이터에 speed가 있으면 그 값을 쓴다.")]
    [Min(0.1f)] public float speed = 8f;

    [Tooltip("포탄 지름 (칸 대비)")]
    [Range(0.1f, 1f)] public float ballSize = 0.5f;

    [Tooltip("충돌 판정 크기 (보이는 포탄 대비). 약간 작게 둬서 억울한 피격을 줄인다.")]
    [Range(0.3f, 1f)] public float ballHitScale = 0.8f;

    [Tooltip("포탄이 나타날 때 커지면서 선명해지는 시간 (초)")]
    [Min(0f)] public float ballAppearTime = 0.06f;

    [Header("발사기")]
    [Tooltip("발사기 지름 (칸 대비)")]
    [Range(0.1f, 1.5f)] public float launcherSize = 0.6f;

    [Tooltip("발사기 고리 두께 (지름 대비)")]
    [Range(0.02f, 0.5f)] public float launcherThickness = 0.14f;

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

    [Tooltip("발사 뒤 원래 크기로 돌아오는 시간")]
    [Min(0.01f)] public float recoverTime = 0.1f;

    [Tooltip("발사 뒤 발사기가 남아 있는 시간")]
    [Min(0f)] public float stayTime = 0.25f;

    [Tooltip("발사기가 작아지며 사라지는 시간")]
    [Min(0.01f)] public float exitTime = 0.2f;

    [Header("그리는 순서")]
    [Tooltip("격자 선(-10)보다 높게")]
    public int launcherSortingOrder = 5;

    [Tooltip("하트(10)보다 높게")]
    public int ballSortingOrder = 20;
}

/// <summary>
/// 일반 포탄 (cannon).
/// 발사기(속 빈 원)가 화면 밖에서 와서 격자 가장자리의 지정한 줄 옆에 자리 잡고,
/// 발사 직전 한 번 수축했다가 포탄(속이 찬 원)을 쏜다.
/// 포탄은 격자 줄을 따라 직선으로만 날아가며, 레벨의 time에 정확히 격자 가장자리에 닿는다.
/// </summary>
public class CannonObstacle : LevelObstacle
{
    readonly CannonSettings settings;

    // 계산해 둔 위치와 시간
    readonly Vector2 direction;      // 포탄이 날아가는 방향
    readonly Vector2 restPosition;   // 발사기가 자리 잡는 곳 (= 포탄 출발점)
    readonly Vector2 offscreenPosition;
    readonly float speedWorld;       // 초당 월드 거리
    readonly float fireTime;         // 포탄을 쏘는 노래 시간
    readonly float enterStart;
    readonly float chargeStart;
    readonly float exitStart;
    readonly float launcherWorldSize;
    readonly float ballWorldSize;

    GameObject root;
    SpriteRenderer launcher;
    SpriteRenderer ball;
    bool hasHit;

    public CannonObstacle(ObstacleData data, ObstacleContext context, CannonSettings settings, GridSide side)
        : base(data, context)
    {
        this.settings = settings;
        float cell = context.CellSize;

        direction = GridSideUtil.InwardDirection(side);
        launcherWorldSize = settings.launcherSize * cell;
        ballWorldSize = settings.ballSize * cell;

        // 포탄이 들어오는 격자 가장자리 위의 점 (지정한 줄의 한가운데)
        Vector2 edgePoint = EdgePoint(context.grid, side, data.line);

        restPosition = edgePoint - direction * (settings.launcherDistance * cell);
        offscreenPosition = OutsideView(context.view, restPosition, -direction, launcherWorldSize);

        // time에 포탄 중심이 격자 가장자리에 닿도록 쏘는 시간을 거꾸로 계산한다.
        float speedCells = data.speed > 0f ? data.speed : settings.speed;
        speedWorld = speedCells * cell;
        fireTime = data.time - Vector2.Distance(restPosition, edgePoint) / speedWorld;

        // 발사기 등장은 발사보다 앞서 자동으로 시작한다. 들어오고 수축할 시간은 꼭 확보한다.
        float lead = Mathf.Max(settings.leadTime, settings.enterTime + settings.chargeTime);
        enterStart = fireTime - lead;
        chargeStart = fireTime - settings.chargeTime;
        exitStart = fireTime + settings.stayTime;

        // 포탄이 반대편 화면 밖으로 완전히 나가는 시간
        Vector2 farPoint = OutsideView(context.view, restPosition, direction, ballWorldSize);
        float ballEndTime = fireTime + Vector2.Distance(restPosition, farPoint) / speedWorld;

        AppearTime = enterStart;
        EndTime = Mathf.Max(exitStart + settings.exitTime, ballEndTime);
    }

    protected override void OnSpawn()
    {
        root = new GameObject($"Cannon {Data.side} {Data.line} @{Data.time:0.##}");
        root.transform.SetParent(Context.container, false);

        launcher = ShapeSprites.CreateRenderer("Launcher", root.transform,
            ShapeSprites.Ring(settings.launcherThickness), Context.Color, settings.launcherSortingOrder);
        ball = ShapeSprites.CreateRenderer("Ball", root.transform,
            ShapeSprites.Circle, Context.Color, settings.ballSortingOrder);
        ball.enabled = false;
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
        UpdateBall(songTime, previousSongTime);
    }

    /// <summary>발사기: 화면 밖에서 들어옴 → 수축 → 발사 → 잠시 뒤 작아지며 사라짐</summary>
    void UpdateLauncher(float t)
    {
        // 들어오기 (작고 투명하게 시작해 제자리에서 원래 크기가 된다)
        float enter = EaseOutCubic((t - enterStart) / settings.enterTime);
        Vector2 position = Vector2.Lerp(offscreenPosition, restPosition, enter);
        float scale = Mathf.Lerp(0.6f, 1f, enter);
        float alpha = enter;

        // 발사 직전 수축했다가, 쏜 뒤 원래 크기로
        if (t >= chargeStart && t < fireTime && settings.chargeTime > 0f)
        {
            scale *= Mathf.Lerp(1f, settings.chargeScale, EaseInOut((t - chargeStart) / settings.chargeTime));
        }
        else if (t >= fireTime && t < fireTime + settings.recoverTime)
        {
            scale *= Mathf.Lerp(settings.chargeScale, 1f, EaseOutCubic((t - fireTime) / settings.recoverTime));
        }

        // 사라지기
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

    /// <summary>포탄: 쏜 순간부터 일정한 속도로 직선 이동. 지나온 길 전체로 충돌을 판정한다.</summary>
    void UpdateBall(float t, float previousT)
    {
        if (t < fireTime)
        {
            ball.enabled = false;
            return;
        }

        Vector2 position = PositionAt(t);
        float appear = settings.ballAppearTime > 0f ? Mathf.Clamp01((t - fireTime) / settings.ballAppearTime) : 1f;

        ball.enabled = true;
        ball.transform.position = position;
        ball.transform.localScale = Vector3.one * (ballWorldSize * Mathf.Lerp(0.5f, 1f, appear));
        ball.color = WithAlpha(Context.Color, appear);

        // 지난 프레임 위치부터 지금 위치까지의 선분으로 판정해서, 빨라도 하트를 건너뛰지 않는다.
        if (!hasHit && Context.heart != null)
        {
            Vector2 from = PositionAt(Mathf.Max(previousT, fireTime));
            Vector2 heartPosition = Context.heart.transform.position;
            float hitDistance = Context.heart.HitRadius + ballWorldSize * 0.5f * settings.ballHitScale;

            if (DistanceToSegment(heartPosition, from, position) <= hitDistance)
            {
                hasHit = true;   // 포탄 하나는 한 번만 맞힌다.
                Context.onPlayerHit?.Invoke(this);
            }
        }
    }

    Vector2 PositionAt(float t) => restPosition + direction * (speedWorld * (t - fireTime));
}
