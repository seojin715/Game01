using System;
using UnityEngine;

/// <summary>분열탄이 오기 전에 보여 주는 반투명 경고</summary>
public enum BurstWarningStyle
{
    PathAndCross,   // 포탄이 칸까지 올 이동 경로 + 돌기 시작한 뒤 조각이 지나갈 십자 줄
    PathOnly,       // 이동 경로만
    None,           // 경고 없음
}

/// <summary>분열탄의 모양과 타이밍 설정. LevelPlayer 인스펙터에서 조절한다.</summary>
[Serializable]
public class BurstSettings
{
    [Header("회전과 경고")]
    [Tooltip("PathAndCross: 이동 경로 + 돌기 시작한 뒤 십자 줄 / PathOnly: 이동 경로만 / None: 경고 없음")]
    public BurstWarningStyle warningStyle = BurstWarningStyle.PathAndCross;

    [Tooltip("시계 방향으로 도는 각도. 315도면 대각선을 향하던 뾰족한 끝이 상하좌우를 향하게 된다.")]
    public float rotateDegrees = 315f;

    [Tooltip("회전의 가속 정도. 천천히 시작해 빨라졌다가 천천히 멈춘다. 클수록 처음과 끝이 더 느리고 가운데가 더 빠르다. 1이면 일정한 속도.")]
    [Range(1f, 6f)] public float rotateAcceleration = 3f;

    [Header("움직임 (노래 시간, 초)")]
    [Tooltip("화면 밖에서 격자 앞까지 날아오는 시간 (일반 발사기처럼)")]
    [Min(0.01f)] public float enterTime = 0.35f;

    [Tooltip("격자 앞에 도착해서 칸으로 움직이기 전까지 기다리는 시간")]
    [Min(0f)] public float waitTime = 0.3f;

    [Tooltip("칸으로 움직일 때 한 칸에 걸리는 박 (레벨의 BPM 기준). 벽에서 3번째 칸이면 이 값의 3배가 걸린다. 벽에서 먼 칸일수록 일찍 움직이기 시작한다.")]
    [Min(0.05f)] public float moveBeatsPerCell = 0.5f;

    [Tooltip("칸에 도착한 뒤 갈라지기까지의 길이 (박). 맨 끝에 움츠림, 그 바로 앞에 회전이 들어가고, 남는 시간은 멈춰 있는다. "
           + "갈라지는 시간(time)을 박에 놓으면 도착과 움직이기 시작하는 때도 박에 맞는다.")]
    [Min(0f)] public float holdBeats = 1f;

    [Tooltip("칸에 도착할 때 느려지는 정도. 0이면 끝까지 같은 속도, 1이면 처음에 빠르고 도착할 때 많이 느려진다.")]
    [Range(0f, 1f)] public float flightEasing = 0.4f;

    [Tooltip("갈라지기 직전 제자리에서 도는 시간. 움츠리는 시간과 합쳐 Hold Beats보다 길면 함께 줄어든다.")]
    [Min(0.01f)] public float rotateTime = 0.4f;

    [Tooltip("발사 직전 움츠러드는 시간. 레벨의 time에 다 움츠러들고 네 조각이 나간다.")]
    [Min(0.01f)] public float shrinkTime = 0.15f;

    /// <summary>벽에서 cells번째 칸을 노릴 때, 화면 밖에서 출발해 갈라지기까지 걸리는 시간 (초)</summary>
    public float LeadSeconds(int cells, float beatLength)
    {
        return enterTime + waitTime + (moveBeatsPerCell * cells + holdBeats) * beatLength;
    }

    [Tooltip("네 조각이 날아가는 속도 기본값 (칸/초). 레벨 데이터에 speed가 있으면 그 값을 쓴다.")]
    [Min(0.5f)] public float shardSpeed = 9f;

    [Header("모양")]
    [Tooltip("격자 가장자리에서 기다리는 자리까지 거리 (칸 대비). 일반 발사기와 같은 자리다.")]
    [Min(0f)] public float waitDistance = 0.55f;

    [Tooltip("합쳐진 포탄 크기 (칸 대비). 위 끝에서 아래 끝까지의 길이")]
    [Range(0.2f, 1.5f)] public float size = 0.8f;

    [Tooltip("평소 네 조각 사이의 틈 (칸 대비)")]
    [Range(0f, 0.2f)] public float gap = 0.035f;

    [Tooltip("움츠러들었을 때 네 조각 사이의 틈 (칸 대비)")]
    [Range(0f, 0.2f)] public float shrunkGap = 0.008f;

    [Tooltip("가장 움츠러들었을 때의 크기 비율")]
    [Range(0.4f, 1f)] public float shrinkScale = 0.88f;

    [Tooltip("갈라진 뒤 조각의 크기 비율 (합쳐져 있을 때 대비). 1이면 그대로, 클수록 날아가는 조각이 커진다. 판정도 같이 커진다.")]
    [Range(0.5f, 3f)] public float shardScale = 1.4f;

    [Tooltip("갈라진 뒤 조각이 위 크기까지 커지는 시간 (노래 시간, 초)")]
    [Min(0.01f)] public float shardGrowTime = 0.12f;

    [Tooltip("충돌 판정 크기 (보이는 크기 대비). 약간 작게 둬서 억울한 피격을 줄인다.")]
    [Range(0.3f, 1f)] public float hitScale = 0.75f;

    [Header("경고")]
    [Tooltip("이동 경로 경고의 불투명도. 포탄이 화면에 들어올 때 나타나고 칸에 도착하면 사라진다.")]
    [Range(0f, 1f)] public float pathWarnAlpha = 0.18f;

    [Tooltip("십자 줄 경고의 불투명도. 돌기 시작할 때 나타나고 갈라지면 사라진다.")]
    [Range(0f, 1f)] public float lineWarnAlpha = 0.14f;

    [Tooltip("이동 경로와 십자 줄 경고의 폭 (칸 대비)")]
    [Range(0.05f, 1f)] public float lineWarnWidth = 0.5f;

    [Tooltip("돌기 시작하고 나서 십자 줄 경고가 나타나기까지 늦추는 시간 (노래 시간, 초). 크게 잡아도 갈라지기 전에는 나타난다.")]
    [Min(0f)] public float crossWarnDelay = 0.08f;

    [Tooltip("경고가 나타나는 시간")]
    [Min(0.01f)] public float warnAppearTime = 0.1f;

    [Header("소리")]
    [Tooltip("네 조각으로 갈라지는 순간(time) 나는 소리")]
    public ObstacleSound sound = new ObstacleSound();

    [Header("그리는 순서")]
    [Tooltip("격자 선(-10)보다 높게")]
    public int warningSortingOrder = -5;

    [Tooltip("하트(10)보다 높게")]
    public int shardSortingOrder = 20;
}

/// <summary>
/// 분열탄 (burst).
/// 네 조각이 합쳐진 포탄이 일반 발사기처럼 화면 밖 side 쪽에서 격자 앞까지 날아와 잠깐 기다린 뒤, cell 칸까지 움직인다. 대각선을 향하던 뾰족한 끝이 315도 돌아
/// 상하좌우를 향하게 되고, 잠깐 움츠러들었다가 time에 네 조각으로 갈라져 상하좌우로 날아간다.
/// 날아오는 동안, 칸에 있는 동안, 조각이 날아가는 동안 모두 판정한다.
///
/// 모든 모습은 노래 시간만으로 계산한다. 그래서 슬로우에서도 어긋나지 않고 미리보기에서 시간을 옮겨도 그대로다.
/// </summary>
public class BurstObstacle : LevelObstacle
{
    const int PieceCount = 4;

    readonly BurstSettings settings;
    readonly Vector2 center;       // 도착할 칸 가운데
    readonly Vector2 offscreen;    // 날아오기 시작하는 곳
    readonly Vector2 waitPosition; // 격자 앞에서 기다리는 곳
    readonly float radius;         // 가운데에서 뾰족한 끝까지 (= 조각 길이)
    readonly float cell;
    readonly float shardSpeedWorld;

    readonly float warnStart;
    readonly float flyStart;       // 화면 밖에서 출발
    readonly float waitStart;      // 격자 앞에 도착
    readonly float moveStart;      // 칸으로 움직이기 시작
    readonly float arriveTime;
    readonly float rotateStart;
    readonly float rotateEnd;
    readonly float shrinkStart;
    readonly float splitTime;      // = time
    readonly float flightDuration; // 격자 앞에서 칸까지 움직이는 시간 (한 칸당 박 x 칸 수)
    readonly float shrinkDuration; // 움츠러드는 시간

    readonly Rect armLeft, armRight, armUp, armDown;   // 벽에서 가운데까지 네 팔 (겹쳐 진해지지 않게 가운데는 따로 그린다)
    readonly Rect centerRect;
    readonly int pathArm;          // 포탄이 오는 쪽 팔 번호 (이동 경로)

    GameObject root;
    readonly SpriteRenderer[] pieces = new SpriteRenderer[PieceCount];
    SpriteRenderer centerWarning;
    readonly SpriteRenderer[] armWarnings = new SpriteRenderer[PieceCount];

    public BurstObstacle(ObstacleData data, ObstacleContext context, BurstSettings settings, Vector2Int targetCell, GridSide side)
        : base(data, context)
    {
        this.settings = settings;
        cell = context.CellSize;
        var grid = context.grid;

        center = grid.CellToWorld(targetCell.x, targetCell.y);
        radius = settings.size * cell * 0.5f;
        shardSpeedWorld = (data.speed > 0f ? data.speed : settings.shardSpeed) * cell;

        // 줄을 따라 side 쪽 화면 밖에서 와서, 그 줄의 벽 바로 바깥에서 기다린다.
        Vector2 inward = GridSideUtil.InwardDirection(side);
        int line = GridSideUtil.IsHorizontal(side) ? targetCell.y : targetCell.x;
        waitPosition = EdgePoint(grid, side, line, data.time) - inward * (settings.waitDistance * cell);
        offscreen = OutsideView(context.view, center, -inward, radius * 2f);

        var bounds = grid.BoundsAt(data.time);

        // time(갈라지는 순간)에서 박 단위로 거꾸로 정한다. 그래서 움직이기 시작, 도착, 갈라짐이 모두 박에 맞는다.
        // 도착 → 갈라짐(holdBeats) 안에 회전과 움츠림을 넣고, 박이 짧아 안 들어가면 함께 줄인다.
        splitTime = data.time;
        arriveTime = splitTime - settings.holdBeats * context.beatLength;

        // 벽에서 몇 번째 칸인지 세어 한 칸당 박만큼 움직인다.
        int cellsFromWall = side == GridSide.Left ? targetCell.x - bounds.FirstColumn + 1
            : side == GridSide.Right ? bounds.LastColumn - targetCell.x + 1
            : side == GridSide.Bottom ? targetCell.y - bounds.FirstRow + 1
            : bounds.LastRow - targetCell.y + 1;
        flightDuration = settings.moveBeatsPerCell * Mathf.Max(1, cellsFromWall) * context.beatLength;
        moveStart = arriveTime - flightDuration;

        // 갈라지기 직전에 돌고, 바로 이어서 움츠린다.
        float hold = splitTime - arriveTime;
        float needed = settings.shrinkTime + settings.rotateTime;
        float fit = needed > hold ? hold / needed : 1f;

        shrinkDuration = Mathf.Max(0.001f, settings.shrinkTime * fit);
        shrinkStart = splitTime - settings.shrinkTime * fit;
        rotateEnd = shrinkStart;
        rotateStart = rotateEnd - settings.rotateTime * fit;
        waitStart = moveStart - settings.waitTime;
        flyStart = waitStart - settings.enterTime;

        // 돌기 시작하고 살짝 뒤에 십자 줄을 보여 준다. 갈라지기 전에는 잠깐이라도 보이게 한다.
        warnStart = Mathf.Min(rotateStart + settings.crossWarnDelay, splitTime - settings.warnAppearTime);

        // 경고: 그 시간 격자의 벽에서 칸 가운데까지 오는 네 팔과 가운데. 포탄이 오는 쪽 팔이 이동 경로다.
        float left = grid.EdgeToWorldX(bounds.minX), right = grid.EdgeToWorldX(bounds.maxX);
        float bottom = grid.EdgeToWorldY(bounds.minY), top = grid.EdgeToWorldY(bounds.maxY);
        float width = settings.lineWarnWidth * cell;
        float half = width * 0.5f;
        centerRect = new Rect(center.x - half, center.y - half, width, width);
        pathArm = side == GridSide.Left ? 0 : side == GridSide.Right ? 1 : side == GridSide.Bottom ? 2 : 3;
        armLeft = new Rect(left, center.y - width * 0.5f, Mathf.Max(0f, centerRect.xMin - left), width);
        armRight = new Rect(centerRect.xMax, center.y - width * 0.5f, Mathf.Max(0f, right - centerRect.xMax), width);
        armDown = new Rect(center.x - width * 0.5f, bottom, width, Mathf.Max(0f, centerRect.yMin - bottom));
        armUp = new Rect(center.x - width * 0.5f, centerRect.yMax, width, Mathf.Max(0f, top - centerRect.yMax));

        // 네 조각이 모두 화면 밖으로 나가면 끝난다. (가장 먼 쪽 기준)
        Rect view = context.view;
        float farthest = Mathf.Max(Mathf.Max(view.xMax - center.x, center.x - view.xMin), Mathf.Max(view.yMax - center.y, center.y - view.yMin));
        AppearTime = flyStart;
        EndTime = splitTime + (farthest + radius * 2f * Mathf.Max(1f, settings.shardScale)) / shardSpeedWorld;
    }

    protected override void OnSpawn()
    {
        root = new GameObject($"Burst {Data.side} @{Data.time:0.##}");
        root.transform.SetParent(Context.container, false);

        // 셰이더가 사각형 안에 연 모양을 계산해서 그린다. 쓸 수 없으면 꼭짓점 도형으로 대신한다.
        var shape = Context.burstMaterial != null ? ShapeSprites.Square : ShapeSprites.BurstPiece;
        for (int i = 0; i < PieceCount; i++)
        {
            pieces[i] = ShapeSprites.CreateRenderer("Piece", root.transform, shape, Context.Color, settings.shardSortingOrder);
            if (Context.burstMaterial != null) pieces[i].sharedMaterial = Context.burstMaterial;
            pieces[i].enabled = false;
        }

        // 이름이 Warning인 그림은 어둠 기믹에서 빛나거나 비추지 않는다.
        if (settings.warningStyle != BurstWarningStyle.None)
        {
            centerWarning = CreateWarning(centerRect);
            Rect[] arms = { armLeft, armRight, armDown, armUp };
            for (int i = 0; i < PieceCount; i++)
            {
                if (i == pathArm || settings.warningStyle == BurstWarningStyle.PathAndCross) armWarnings[i] = CreateWarning(arms[i]);
            }
        }
    }

    SpriteRenderer CreateWarning(Rect area)
    {
        var warning = ShapeSprites.CreateSquare("Warning", root.transform, Context.Color, settings.warningSortingOrder);
        warning.transform.position = area.center;
        warning.transform.localScale = new Vector3(area.width, area.height, 1f);
        warning.enabled = false;
        return warning;
    }

    protected override void OnDespawn()
    {
        if (root != null) UnityEngine.Object.Destroy(root);
        root = null;
        centerWarning = null;
        Array.Clear(armWarnings, 0, armWarnings.Length);
    }

    public override void Tick(float songTime, float previousSongTime)
    {
        if (root == null) return;

        settings.sound.PlayAt(splitTime, songTime, previousSongTime);
        UpdateWarnings(songTime);
        if (songTime < splitTime) UpdateWhole(songTime);
        else UpdateShards(songTime);

        CheckHit(songTime, previousSongTime);
    }

    /// <summary>
    /// 경고. 이동 경로는 포탄이 들어올 때 나타나서 칸에 도착하면 사라진다.
    /// 십자 줄은 돌기 시작할 때 나타나 조금씩 진해지고, 갈라지면 사라진다.
    /// 같은 자리의 경고는 더 진한 쪽 하나로 그린다. (겹쳐서 진해지지 않게)
    /// </summary>
    void UpdateWarnings(float t)
    {
        float path = 0f;
        if (t < arriveTime + settings.warnAppearTime)
        {
            float pathIn = EaseOutCubic((t - flyStart) / settings.warnAppearTime);
            float pathOut = 1f - Mathf.Clamp01((t - arriveTime) / settings.warnAppearTime);
            path = settings.pathWarnAlpha * pathIn * pathOut;
        }

        float cross = 0f;
        if (settings.warningStyle == BurstWarningStyle.PathAndCross && t >= warnStart && t < splitTime)
        {
            float appear = EaseOutCubic((t - warnStart) / settings.warnAppearTime);
            float progress = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01((t - warnStart) / Mathf.Max(0.001f, splitTime - warnStart)));
            cross = settings.lineWarnAlpha * progress * appear;
        }

        if (t >= splitTime)
        {
            path = 0f;
            cross = 0f;
        }

        PaintWarning(centerWarning, Mathf.Max(path, cross));
        for (int i = 0; i < PieceCount; i++)
        {
            PaintWarning(armWarnings[i], i == pathArm ? Mathf.Max(path, cross) : cross);
        }
    }

    void PaintWarning(SpriteRenderer warning, float alpha)
    {
        if (warning == null) return;
        warning.enabled = alpha > 0f && warning.transform.localScale.x > 0f && warning.transform.localScale.y > 0f;
        warning.color = WithAlpha(Context.Color, alpha);
    }

    /// <summary>갈라지기 전: 날아오고, 돌고, 움츠러든다.</summary>
    void UpdateWhole(float t)
    {
        bool visible = t >= flyStart;
        float shrink = EaseInOut((t - shrinkStart) / shrinkDuration);
        float scale = Mathf.Lerp(1f, settings.shrinkScale, shrink);
        float gap = Mathf.Lerp(settings.gap, settings.shrunkGap, shrink) * cell;

        // 시계 방향으로 돌아서 rotateEnd에 뾰족한 끝이 상하좌우를 향한다.
        float turn = settings.rotateDegrees * (1f - RotateCurve((t - rotateStart) / Mathf.Max(0.001f, rotateEnd - rotateStart)));

        Vector2 position = WholePosition(t);
        for (int i = 0; i < PieceCount; i++)
        {
            pieces[i].enabled = visible;
            if (visible) PlacePiece(pieces[i], position, turn + i * 90f, gap, scale);
        }
    }

    /// <summary>회전 진행(0~1). 천천히 시작해 점점 빨라졌다가 다시 천천히 멈춘다. (시작과 끝의 속도가 0이라 부드럽다)</summary>
    float RotateCurve(float x)
    {
        x = Mathf.Clamp01(x);
        float a = Mathf.Pow(x, settings.rotateAcceleration);
        float b = Mathf.Pow(1f - x, settings.rotateAcceleration);
        return a / (a + b);
    }

    /// <summary>갈라진 뒤: 네 조각이 상하좌우로 일정한 속도로 날아간다.</summary>
    void UpdateShards(float t)
    {
        float travelled = shardSpeedWorld * (t - splitTime);
        float gap = settings.shrunkGap * cell + travelled;

        for (int i = 0; i < PieceCount; i++)
        {
            pieces[i].enabled = true;
            PlacePiece(pieces[i], center, i * 90f, gap, ShardScaleAt(t));
        }
    }

    /// <summary>갈라진 뒤 조각 크기 비율. 갈라지는 순간 1에서 시작해 Shard Grow Time 동안 Shard Scale까지 커진다.</summary>
    float ShardScaleAt(float t)
    {
        return Mathf.Lerp(1f, settings.shardScale, EaseOutCubic((t - splitTime) / settings.shardGrowTime));
    }

    /// <summary>조각 하나를 놓는다. 그림은 안쪽 끝이 상자 왼쪽, 뾰족한 끝이 오른쪽(+x)이다.</summary>
    void PlacePiece(SpriteRenderer piece, Vector2 from, float angle, float gap, float scale)
    {
        Vector2 direction = DirectionOf(angle);
        float length = radius * scale;
        piece.transform.position = from + direction * (gap + length * 0.5f);
        piece.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        piece.transform.localScale = Vector3.one * (length / ShapeSprites.BurstPieceLength);   // 그림의 여백을 뺀 길이가 length가 되게
        piece.color = Context.Color;
    }

    static Vector2 DirectionOf(float angle)
    {
        float rad = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    /// <summary>합쳐진 포탄의 위치. 화면 밖 → 격자 앞(잠깐 기다림) → 도착할 칸</summary>
    Vector2 WholePosition(float t)
    {
        if (t < waitStart) return Vector2.Lerp(offscreen, waitPosition, EaseOutCubic((t - flyStart) / settings.enterTime));
        if (t < moveStart) return waitPosition;

        float x = Mathf.Clamp01((t - moveStart) / flightDuration);
        float eased = 1f - (1f - x) * (1f - x);
        return Vector2.Lerp(waitPosition, center, Mathf.Lerp(x, eased, settings.flightEasing));
    }

    /// <summary>지난 프레임부터 지금까지 지나온 길로 판정해서, 빨라도 하트를 건너뛰지 않는다.</summary>
    void CheckHit(float t, float previousT)
    {
        if (Context.heart == null || t < flyStart) return;

        Vector2 heart = Context.heart.transform.position;
        bool hit;
        if (t < splitTime)
        {
            float distance = Context.heart.HitRadius + radius * settings.hitScale;
            hit = DistanceToSegment(heart, WholePosition(Mathf.Max(previousT, flyStart)), WholePosition(t)) <= distance;
        }
        else
        {
            // 조각의 판정은 조각 가운데쯤의 원으로 본다. 커진 조각은 판정도 같이 커진다.
            float before = Mathf.Max(previousT, splitTime);
            float scaleNow = ShardScaleAt(t);
            float distance = Context.heart.HitRadius + radius * scaleNow * 0.4f * settings.hitScale;
            float gapNow = shardSpeedWorld * (t - splitTime) + radius * scaleNow * 0.5f;
            float gapBefore = shardSpeedWorld * (before - splitTime) + radius * ShardScaleAt(before) * 0.5f;
            hit = false;
            for (int i = 0; i < PieceCount && !hit; i++)
            {
                Vector2 direction = DirectionOf(i * 90f);
                hit = DistanceToSegment(heart, center + direction * gapBefore, center + direction * gapNow) <= distance;
            }
        }

        // 닿아 있는 동안 계속 알린다. 무적 중에는 PlayerHealth가 무시한다.
        if (hit) Context.onPlayerHit?.Invoke(this);
    }
}
