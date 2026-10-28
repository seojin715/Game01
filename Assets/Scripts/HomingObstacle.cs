using System;
using UnityEngine;

/// <summary>유도탄의 움직임과 모양 설정. LevelPlayer 인스펙터에서 조절한다.</summary>
[Serializable]
public class HomingSettings
{
    [Header("속도 (칸/초)")]
    [Tooltip("발사 순간 속도. 레벨 데이터에 speed가 있으면 그 값을 쓴다.")]
    [Min(0.1f)] public float startSpeed = 3f;

    [Tooltip("속도 증가량. 1초마다 이만큼 빨라진다. (칸/초²)")]
    [Min(0f)] public float acceleration = 6f;

    [Tooltip("최고 속도")]
    [Min(0.1f)] public float maxSpeed = 16f;

    [Header("방향 전환 (도/초)")]
    [Tooltip("발사 순간 1초에 꺾을 수 있는 최대 각도")]
    [Min(0f)] public float startTurnRate = 360f;

    [Tooltip("방향 전환 감소량. 1초마다 꺾을 수 있는 각도가 이만큼 줄어든다. (도/초²)")]
    [Min(0f)] public float turnRateDecay = 300f;

    [Tooltip("꺾을 수 있는 각도가 이 아래로는 줄지 않는다. 0이면 결국 직선으로만 간다.")]
    [Min(0f)] public float minTurnRate = 0f;

    [Header("유도탄 모양")]
    [Tooltip("유도탄 크기 (칸 대비)")]
    [Range(0.1f, 1f)] public float missileSize = 0.5f;

    [Tooltip("충돌 판정 크기 (보이는 크기 대비). 약간 작게 둬서 억울한 피격을 줄인다.")]
    [Range(0.3f, 1f)] public float hitScale = 0.7f;

    [Tooltip("유도탄이 나타날 때 커지면서 선명해지는 시간 (초)")]
    [Min(0f)] public float appearTime = 0.08f;

    [Tooltip("발사 뒤 이 시간이 지나면 화면 안이어도 터진다 (초). 안전장치.")]
    [Min(0.5f)] public float maxLifetime = 8f;

    [Header("터짐")]
    [Tooltip("터지는 모션 시간 (초)")]
    [Min(0.01f)] public float explosionTime = 0.3f;

    [Tooltip("터지면서 퍼지는 고리의 최종 크기 (유도탄 크기 대비)")]
    [Min(1f)] public float explosionScale = 2.5f;

    [Header("발사기")]
    [Tooltip("발사기 크기 (칸 대비)")]
    [Range(0.1f, 1.5f)] public float launcherSize = 0.6f;

    [Tooltip("발사기 테두리 두께 (크기 대비)")]
    [Range(0.02f, 0.3f)] public float launcherThickness = 0.09f;

    [Tooltip("격자 가장자리에서 발사기 중심까지 거리 (칸 대비)")]
    [Min(0f)] public float launcherDistance = 0.55f;

    [Tooltip("발사 몇 초 전에 발사기가 화면 밖에서 들어오기 시작하는지")]
    [Min(0.05f)] public float leadTime = 1f;

    [Tooltip("발사기가 화면 밖에서 제자리까지 오는 데 걸리는 시간")]
    [Min(0.01f)] public float enterTime = 0.35f;

    [Tooltip("발사 순간 발사기가 앞으로 튀어나가는 거리 (칸 대비)")]
    [Min(0f)] public float kickDistance = 0.25f;

    [Tooltip("앞으로 튀어나갔다가 돌아오는 시간")]
    [Min(0.01f)] public float kickTime = 0.18f;

    [Tooltip("발사 뒤 발사기가 남아 있는 시간")]
    [Min(0f)] public float stayTime = 0.3f;

    [Tooltip("발사기가 작아지며 사라지는 시간")]
    [Min(0.01f)] public float exitTime = 0.2f;

    [Header("계산")]
    [Tooltip("움직임을 이 시간(초)보다 잘게 나눠 계산한다. 프레임이 튀어도 경로가 같아진다.")]
    [Range(0.002f, 0.05f)] public float stepTime = 1f / 120f;

    [Header("그리는 순서")]
    public int launcherSortingOrder = 5;
    public int missileSortingOrder = 20;
    public int explosionSortingOrder = 21;
}

/// <summary>
/// 유도탄 (homing).
/// 발사기(속 빈 삼각형)가 화면 밖에서 지정한 줄 옆으로 와서 자리 잡고, time에 유도탄(채운 삼각형)을 쏜다.
/// 유도탄은 하트를 따라가는데, 속도는 점점 빨라지고 꺾을 수 있는 각도는 점점 줄어서 결국 피할 수 있다.
/// 하트에 닿거나 화면 밖으로 나가면 터지는 모션과 함께 사라진다.
///
/// 하트를 따라가므로 위치를 노래 시간만으로 정할 수 없다. 대신 노래 시간이 흐른 만큼만
/// 잘게 나눠 움직인다. 그래서 슬로우가 걸리면 같은 비율로 느려지고, 프레임이 튀어도 경로가 같다.
/// </summary>
public class HomingObstacle : LevelObstacle
{
    readonly HomingSettings settings;
    readonly float cell;

    readonly Vector2 inward;          // 발사 방향 (격자 안쪽)
    readonly Vector2 restPosition;
    readonly Vector2 offscreenPosition;
    readonly float fireTime;          // = time
    readonly float enterStart;
    readonly float exitStart;
    readonly float startSpeedWorld;
    readonly float launcherWorldSize;
    readonly float missileWorldSize;

    GameObject root;
    SpriteRenderer launcher;
    SpriteRenderer missile;
    SpriteRenderer explosionRing;
    SpriteRenderer explosionFlash;

    // 유도탄 상태 (발사 뒤 노래 시간이 흐른 만큼 계산해 둔다)
    bool launched;
    bool exploded;
    float simulatedTime;    // 여기까지 계산했다는 노래 시간
    float age;              // 발사 뒤 지난 노래 시간
    Vector2 position;
    Vector2 direction;
    float explodeTime;
    Vector2 explodePosition;

    public HomingObstacle(ObstacleData data, ObstacleContext context, HomingSettings settings, GridSide side)
        : base(data, context)
    {
        this.settings = settings;
        cell = context.CellSize;

        inward = GridSideUtil.InwardDirection(side);
        launcherWorldSize = settings.launcherSize * cell;
        missileWorldSize = settings.missileSize * cell;

        Vector2 edgePoint = EdgePoint(context.grid, side, data.line);
        restPosition = edgePoint - inward * (settings.launcherDistance * cell);
        offscreenPosition = OutsideView(context.view, restPosition, -inward, launcherWorldSize);

        float startSpeedCells = data.speed > 0f ? data.speed : settings.startSpeed;
        startSpeedWorld = startSpeedCells * cell;

        // 유도탄은 time에 발사된다. (하트에 닿는 시점은 플레이어 움직임에 따라 달라지므로)
        fireTime = data.time;
        enterStart = fireTime - Mathf.Max(settings.leadTime, settings.enterTime);
        exitStart = fireTime + settings.stayTime;

        AppearTime = enterStart;
        EndTime = float.MaxValue;   // 언제 터질지 모르므로, 터지면 그때 정한다.
    }

    protected override void OnSpawn()
    {
        root = new GameObject($"Homing {Data.side} {Data.line} @{Data.time:0.##}");
        root.transform.SetParent(Context.container, false);

        launcher = ShapeSprites.CreateRenderer("Launcher", root.transform,
            ShapeSprites.TriangleOutline(settings.launcherThickness), Context.Color, settings.launcherSortingOrder);
        missile = ShapeSprites.CreateRenderer("Missile", root.transform,
            ShapeSprites.Triangle, Context.Color, settings.missileSortingOrder);
        explosionRing = ShapeSprites.CreateRenderer("ExplosionRing", root.transform,
            ShapeSprites.Ring(0.08f), Context.Color, settings.explosionSortingOrder);
        explosionFlash = ShapeSprites.CreateRenderer("ExplosionFlash", root.transform,
            ShapeSprites.Circle, Context.Color, settings.explosionSortingOrder);

        // 발사기와 유도탄은 격자 안쪽을 가리킨다.
        launcher.transform.rotation = RotationOf(inward);
        missile.enabled = false;
        explosionRing.enabled = false;
        explosionFlash.enabled = false;
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
        Simulate(songTime);
        UpdateMissile(songTime);
        UpdateExplosion(songTime);
    }

    /// <summary>발사기: 화면 밖에서 들어옴 → 발사 순간 앞으로 튀어나갔다 돌아옴 → 작아지며 사라짐</summary>
    void UpdateLauncher(float t)
    {
        float enter = EaseOutCubic((t - enterStart) / settings.enterTime);
        Vector2 pos = Vector2.Lerp(offscreenPosition, restPosition, enter);
        float scale = Mathf.Lerp(0.6f, 1f, enter);
        float alpha = enter;

        // 발사 순간 앞으로 튀어나간다. (빠르게 나갔다가 천천히 돌아온다)
        if (t >= fireTime && t < fireTime + settings.kickTime)
        {
            float k = (t - fireTime) / settings.kickTime;
            float kick = k < 0.25f ? EaseOutCubic(k / 0.25f) : 1f - EaseInOut((k - 0.25f) / 0.75f);
            pos += inward * (settings.kickDistance * cell * kick);
        }

        if (t >= exitStart)
        {
            float exit = Mathf.Clamp01((t - exitStart) / settings.exitTime);
            alpha *= 1f - exit;
            scale *= Mathf.Lerp(1f, 0.5f, exit);
        }

        launcher.enabled = alpha > 0f;
        launcher.transform.position = pos;
        launcher.transform.localScale = Vector3.one * (launcherWorldSize * scale);
        launcher.color = WithAlpha(Context.Color, alpha);
    }

    /// <summary>지난번에 계산한 시간부터 지금 노래 시간까지 잘게 나눠 유도탄을 움직인다.</summary>
    void Simulate(float t)
    {
        if (!launched)
        {
            if (t < fireTime) return;

            launched = true;
            simulatedTime = fireTime;
            position = restPosition;
            direction = inward;
            age = 0f;
        }

        while (!exploded && simulatedTime < t)
        {
            float dt = Mathf.Min(settings.stepTime, t - simulatedTime);
            Step(dt);
            simulatedTime += dt;
        }
    }

    /// <summary>한 조각 시간만큼: 하트 쪽으로 꺾고, 빨라지고, 움직이고, 판정한다.</summary>
    void Step(float dt)
    {
        age += dt;

        float speed = Mathf.Min(settings.maxSpeed * cell, startSpeedWorld + settings.acceleration * cell * age);
        float turnRate = Mathf.Max(settings.minTurnRate, settings.startTurnRate - settings.turnRateDecay * age);

        // 하트 쪽으로 꺾되, 이번 조각에서 꺾을 수 있는 각도까지만 꺾는다.
        if (Context.heart != null)
        {
            Vector2 toHeart = (Vector2)Context.heart.transform.position - position;
            if (toHeart.sqrMagnitude > 0.0001f)
            {
                float wanted = Vector2.SignedAngle(direction, toHeart);
                float turn = Mathf.Clamp(wanted, -turnRate * dt, turnRate * dt);
                direction = (Quaternion.Euler(0f, 0f, turn) * direction).normalized;
            }
        }

        Vector2 from = position;
        position += direction * (speed * dt);

        // 하트에 닿으면 피격을 알리고 그 자리에서 터진다.
        if (Context.heart != null)
        {
            float hitDistance = Context.heart.HitRadius + missileWorldSize * 0.5f * settings.hitScale;
            if (DistanceToSegment(Context.heart.transform.position, from, position) <= hitDistance)
            {
                Context.onPlayerHit?.Invoke(this);
                Explode(simulatedTime + dt, position);
                return;
            }
        }

        // 화면 밖으로 나가면 화면 가장자리에서 터진다. (터지는 모션이 보이게)
        if (!Context.view.Contains(position))
        {
            Vector2 edge = new Vector2(
                Mathf.Clamp(position.x, Context.view.xMin, Context.view.xMax),
                Mathf.Clamp(position.y, Context.view.yMin, Context.view.yMax));
            Explode(simulatedTime + dt, edge);
            return;
        }

        if (age >= settings.maxLifetime) Explode(simulatedTime + dt, position);
    }

    void Explode(float time, Vector2 at)
    {
        exploded = true;
        explodeTime = time;
        explodePosition = at;

        // 터지는 모션과 발사기 퇴장이 모두 끝나면 지워도 된다.
        EndTime = Mathf.Max(time + settings.explosionTime, exitStart + settings.exitTime);
    }

    /// <summary>유도탄: 진행 방향으로 회전한 채 움직인다. 나타날 때 커지면서 선명해진다.</summary>
    void UpdateMissile(float t)
    {
        bool visible = launched && !exploded;
        missile.enabled = visible;
        if (!visible) return;

        float appear = settings.appearTime > 0f ? Mathf.Clamp01((t - fireTime) / settings.appearTime) : 1f;
        missile.transform.position = position;
        missile.transform.rotation = RotationOf(direction);
        missile.transform.localScale = Vector3.one * (missileWorldSize * Mathf.Lerp(0.5f, 1f, appear));
        missile.color = WithAlpha(Context.Color, appear);
    }

    /// <summary>터짐: 고리가 퍼지며 옅어지고, 가운데 번쩍임이 빠르게 줄어든다.</summary>
    void UpdateExplosion(float t)
    {
        bool visible = exploded && t < explodeTime + settings.explosionTime;
        explosionRing.enabled = visible;
        explosionFlash.enabled = visible;
        if (!visible) return;

        float k = Mathf.Clamp01((t - explodeTime) / settings.explosionTime);

        explosionRing.transform.position = explodePosition;
        explosionRing.transform.localScale = Vector3.one * (missileWorldSize * Mathf.Lerp(1f, settings.explosionScale, EaseOutCubic(k)));
        explosionRing.color = WithAlpha(Context.Color, 1f - k);

        explosionFlash.transform.position = explodePosition;
        explosionFlash.transform.localScale = Vector3.one * (missileWorldSize * Mathf.Lerp(1f, 0.2f, EaseOutCubic(k)));
        explosionFlash.color = WithAlpha(Context.Color, 1f - EaseOutCubic(k));
    }

    /// <summary>오른쪽을 가리키는 삼각형을 dir 방향으로 돌리는 회전</summary>
    static Quaternion RotationOf(Vector2 dir) => Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
}
