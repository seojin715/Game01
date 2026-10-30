using System;
using UnityEngine;

/// <summary>
/// 장애물들이 함께 쓰는 정보. LevelPlayer가 만들어서 넘겨준다.
/// </summary>
public class ObstacleContext
{
    public GameGrid grid;
    public HeartView heart;

    /// <summary>레벨에 적힌 장애물 색 (obstacleColor)</summary>
    public Color color;

    /// <summary>지금 그려야 할 장애물 색. 무적 중 화면 반전이면 뒤집힌 색이다.</summary>
    public Color Color => ScreenInvert.Apply(color);

    /// <summary>장애물 오브젝트를 담아 둘 부모</summary>
    public Transform container;

    /// <summary>격자 밖은 그리지 않는 머티리얼. 벽 뒤에서 나오는 장애물(가시)이 벽 뒤 부분을 숨길 때 쓴다.</summary>
    public Material clippedMaterial;

    /// <summary>카메라가 비추는 영역 (월드 좌표). 화면 밖 위치를 정할 때 쓴다.</summary>
    public Rect view;

    /// <summary>장애물이 하트에 닿았을 때 부른다.</summary>
    public Action<LevelObstacle> onPlayerHit;

    public float CellSize => grid != null ? grid.cellSize : 1f;
}

/// <summary>
/// 레벨에 놓인 장애물 하나.
///
/// 모든 움직임은 "노래 시간"을 받아서 그 순간의 모습을 계산한다. (Tick)
/// 프레임마다 조금씩 쌓아 가는 방식이 아니라서 슬로우가 걸리거나 프레임이 튀어도
/// 노래와 어긋나지 않는다.
///
/// LevelPlayer가 AppearTime에 Spawn을 부르고, EndTime이 지나면 Despawn을 부른다.
/// </summary>
public abstract class LevelObstacle
{
    public ObstacleData Data { get; }
    protected ObstacleContext Context { get; }

    /// <summary>화면에 나타나기 시작하는 노래 시간 (발사기 등장, 경고 등)</summary>
    public float AppearTime { get; protected set; }

    /// <summary>완전히 사라지는 노래 시간</summary>
    public float EndTime { get; protected set; }

    public bool IsSpawned { get; private set; }

    protected LevelObstacle(ObstacleData data, ObstacleContext context)
    {
        Data = data;
        Context = context;
    }

    public void Spawn()
    {
        if (IsSpawned) return;
        IsSpawned = true;
        OnSpawn();
    }

    public void Despawn()
    {
        if (!IsSpawned) return;
        IsSpawned = false;
        OnDespawn();
    }

    /// <summary>노래 시간이 songTime일 때의 모습으로 맞추고 충돌을 판정한다.</summary>
    public abstract void Tick(float songTime, float previousSongTime);

    protected abstract void OnSpawn();
    protected abstract void OnDespawn();

    /// <summary>
    /// 노래 시간 time일 때, side 쪽 격자 가장자리(벽) 위에서 line번 줄의 한가운데 점.
    /// 발사체가 격자로 들어오는 곳이다. 벽이 움직이면 그 시간의 벽 위치를 쓴다.
    /// </summary>
    protected static Vector2 EdgePoint(GameGrid grid, GridSide side, int line, float time)
    {
        var bounds = grid.BoundsAt(time);

        if (GridSideUtil.IsHorizontal(side))
        {
            float laneY = grid.CellToWorld(0, line).y;
            float edgeX = grid.EdgeToWorldX(side == GridSide.Left ? bounds.minX : bounds.maxX);
            return new Vector2(edgeX, laneY);
        }

        float laneX = grid.CellToWorld(line, 0).x;
        float edgeY = grid.EdgeToWorldY(side == GridSide.Bottom ? bounds.minY : bounds.maxY);
        return new Vector2(laneX, edgeY);
    }

    /// <summary>start에서 dir(상하좌우) 쪽으로 가다가 화면 밖으로 size만큼 더 나간 점</summary>
    protected static Vector2 OutsideView(Rect view, Vector2 start, Vector2 dir, float size)
    {
        if (dir.x > 0f) return new Vector2(view.xMax + size, start.y);
        if (dir.x < 0f) return new Vector2(view.xMin - size, start.y);
        if (dir.y > 0f) return new Vector2(start.x, view.yMax + size);
        return new Vector2(start.x, view.yMin - size);
    }

    /// <summary>점에서 선분까지의 거리. 빠르게 지나가는 발사체가 하트를 건너뛰지 않게 판정할 때 쓴다.</summary>
    protected static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.sqrMagnitude;
        float t = lengthSquared <= 0f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
        return Vector2.Distance(point, a + ab * t);
    }

    /// <summary>색의 투명도에 alpha(0~1)를 곱한다. 등장과 소멸의 페이드에 쓴다.</summary>
    protected static Color WithAlpha(Color color, float alpha)
    {
        color.a *= Mathf.Clamp01(alpha);
        return color;
    }

    // 움직임을 부드럽게 하는 곡선들 (입력과 출력 모두 0~1)
    protected static float EaseOutCubic(float x) { x = 1f - Mathf.Clamp01(x); return 1f - x * x * x; }
    protected static float EaseInOut(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));
}
