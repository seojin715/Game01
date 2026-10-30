using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 격자 범위. 칸 단위이며 칸 x는 [x, x+1) 구간을 차지한다.
/// 처음 5x5 격자는 (0, 5, 0, 5)이고, 벽이 움직이면 소수점 값이 되기도 한다.
/// </summary>
public struct GridBounds
{
    const float Epsilon = 0.01f;

    public float minX, maxX, minY, maxY;

    public GridBounds(float minX, float maxX, float minY, float maxY)
    {
        this.minX = minX;
        this.maxX = maxX;
        this.minY = minY;
        this.maxY = maxY;
    }

    public float Width => maxX - minX;
    public float Height => maxY - minY;

    /// <summary>칸이 이 범위 안에 온전히 들어 있는지</summary>
    public bool ContainsCell(int x, int y) => ContainsColumn(x) && ContainsRow(y);

    /// <summary>세로줄(칸 x)이 온전히 들어 있는지</summary>
    public bool ContainsColumn(int x) => x >= minX - Epsilon && x + 1 <= maxX + Epsilon;

    /// <summary>가로줄(칸 y)이 온전히 들어 있는지</summary>
    public bool ContainsRow(int y) => y >= minY - Epsilon && y + 1 <= maxY + Epsilon;

    /// <summary>온전히 들어 있는 칸 번호의 범위 (처음, 끝 포함)</summary>
    public int FirstColumn => Mathf.CeilToInt(minX - Epsilon);
    public int LastColumn => Mathf.FloorToInt(maxX + Epsilon) - 1;
    public int FirstRow => Mathf.CeilToInt(minY - Epsilon);
    public int LastRow => Mathf.FloorToInt(maxY + Epsilon) - 1;

    /// <summary>side 쪽 벽의 위치 (칸 단위)</summary>
    public float Edge(GridSide side)
    {
        switch (side)
        {
            case GridSide.Left: return minX;
            case GridSide.Right: return maxX;
            case GridSide.Bottom: return minY;
            default: return maxY;
        }
    }

    public static GridBounds Union(GridBounds a, GridBounds b)
    {
        return new GridBounds(Mathf.Min(a.minX, b.minX), Mathf.Max(a.maxX, b.maxX), Mathf.Min(a.minY, b.minY), Mathf.Max(a.maxY, b.maxY));
    }
}

/// <summary>레벨의 벽 이벤트 하나. side 벽이 움직여서 그 방향의 칸 수가 size가 된다.</summary>
public struct WallEvent
{
    public float time;       // 벽이 도착하는 노래 시간
    public GridSide side;
    public int size;
    public float warnTime;   // 0이면 기본값
}

/// <summary>줄어들 칸의 경고 띠. GridView가 그린다.</summary>
public struct ShrinkWarning
{
    public Rect cells;       // 칸 단위 범위
    public float progress;   // 경고 시작(0)부터 벽 도착(1)까지 진행 정도
}

/// <summary>레벨의 텔레포트 이벤트 하나. 켜거나 끄고, 켤 때는 어느 벽끼리 이어질지 정한다.</summary>
public struct PortalEvent
{
    public float time;
    public bool enabled;
    public bool horizontal;  // 왼쪽 벽 ↔ 오른쪽 벽
    public bool vertical;    // 위 벽 ↔ 아래 벽
}

/// <summary>어느 순간의 텔레포트 상태</summary>
public struct PortalState
{
    public bool horizontal;  // 왼쪽 ↔ 오른쪽으로 넘어갈 수 있는지
    public bool vertical;    // 위 ↔ 아래로 넘어갈 수 있는지
    public float blend;      // 빛나는 벽이 퍼진 정도 (0~1, 1이면 벽 전체)
}

/// <summary>
/// 격자의 크기와 칸 좌표 변환을 담당한다.
/// 칸 좌표는 처음 격자의 왼쪽 아래가 (0, 0)이고, 벽이 움직여도 칸 좌표와 칸의 화면 위치는 바뀌지 않는다.
/// (늘어난 칸은 5, 6이나 -1처럼 이어지는 번호를 쓴다)
///
/// 벽은 레벨의 벽 이벤트에 따라 움직인다. 벽 위치는 노래 시간으로 계산하므로
/// 슬로우가 걸려도 박자에 맞고, 장애물이 "그 시간의 격자"를 미리 알 수 있다.
/// 격자가 처음보다 커지면 카메라가 물러나서 다 보이게 한다.
///
/// 다른 스크립트가 같은 프레임에 최신 벽 위치를 읽도록 먼저 실행한다. (노래 시계 다음)
/// </summary>
[DefaultExecutionOrder(-90)]
public class GameGrid : MonoBehaviour
{
    [Header("격자 크기 (처음)")]
    [Min(1)] public int columns = 5;
    [Min(1)] public int rows = 5;

    [Tooltip("칸 한 변의 길이 (월드 단위)")]
    [Min(0.1f)] public float cellSize = 1f;

    [Header("벽 움직임")]
    [Tooltip("벽이 움직여서 될 수 있는 최대 가로 칸 수")]
    [Min(1)] public int maxColumns = 7;

    [Tooltip("벽이 움직여서 될 수 있는 최대 세로 칸 수")]
    [Min(1)] public int maxRows = 7;

    [Tooltip("벽이 움직이는 데 걸리는 시간 (노래 시간, 초). 벽은 레벨의 time에 도착한다.")]
    [Min(0.05f)] public float wallMoveTime = 0.5f;

    [Tooltip("레벨에 warnTime이 없을 때, 벽이 움직이기 전에 줄어들 칸을 경고하는 시간 (초)")]
    [Min(0f)] public float defaultWallWarnTime = 1f;

    [Header("텔레포트 기믹")]
    [Tooltip("텔레포트 문(끝 칸 벽 선 빛)의 색. 하트와 겹치지 않게 빨강 계열은 피한다. 바깥 벽 자체는 GridView의 Portal Wall 설정을 따른다.")]
    public Color portalColor = new Color(0.4f, 0.85f, 1f, 1f);

    [Tooltip("텔레포트를 켜고 끌 때 빛나는 벽이 각 변의 가운데에서 양쪽 꼭짓점까지 퍼지는(끌 때는 가운데로 줄어드는) 시간 (노래 시간, 초)")]
    [Min(0.01f)] public float portalTransitionTime = 0.3f;

    [Header("카메라 맞춤")]
    [Tooltip("켜면 격자가 화면에 꽉 들어오도록 카메라를 자동으로 맞춘다. 격자가 커지면 따라 물러난다.")]
    public bool fitCamera = true;

    [Tooltip("격자와 화면 가장자리 사이 여백 (격자 크기 대비 비율)")]
    [Range(0f, 1f)] public float cameraMargin = 0.4f;

    [Tooltip("켜면 카메라 배경을 아래 색으로 바꾼다.")]
    public bool setCameraBackground = true;
    public Color backgroundColor = Color.black;

    [Tooltip("비워 두면 Main Camera를 쓴다.")]
    public Camera targetCamera;

    [Tooltip("비워 두면 씬에서 SongClock을 찾아 쓴다. 벽 위치를 노래 시간으로 계산한다.")]
    public SongClock clock;

    /// <summary>처음 격자 전체 가로 길이 (월드 단위)</summary>
    public float Width => columns * cellSize;

    /// <summary>처음 격자 전체 세로 길이 (월드 단위)</summary>
    public float Height => rows * cellSize;

    /// <summary>처음 격자 가운데 칸</summary>
    public Vector2Int CenterCell => new Vector2Int(columns / 2, rows / 2);

    /// <summary>처음 격자 범위</summary>
    public GridBounds BaseBounds => new GridBounds(0f, columns, 0f, rows);

    /// <summary>지금 벽 위치의 격자 범위 (움직이는 중이면 소수점)</summary>
    public GridBounds CurrentBounds { get; private set; }

    /// <summary>지금 움직이고 있는 벽이 도착할 곳까지 반영한 격자 범위</summary>
    public GridBounds DestinationBounds { get; private set; }

    /// <summary>레벨 전체에서 격자가 가장 넓을 때의 범위 (처음 격자 포함)</summary>
    public GridBounds MaxBounds { get; private set; }

    /// <summary>지금 텔레포트 상태</summary>
    public PortalState CurrentPortal { get; private set; }

    /// <summary>벽 계산에 쓰는 지금 노래 시간</summary>
    public float SongTime => clock != null && clock.IsLoaded ? clock.SongTime : 0f;

    /// <summary>벽이 한 번 움직이는 구간</summary>
    struct WallSegment
    {
        public float warnStart, moveStart, moveEnd;
        public float from, to;   // 벽 위치 (칸 단위)
    }

    // 네 벽마다 시간 순서대로 움직이는 구간들 (GridSide 순서)
    readonly List<WallSegment>[] segments =
    {
        new List<WallSegment>(), new List<WallSegment>(), new List<WallSegment>(), new List<WallSegment>()
    };

    readonly List<PortalEvent> portals = new List<PortalEvent>();   // 시간 순서

    static readonly int ClipRectId = Shader.PropertyToID("_GridClipRect");

    void Awake()
    {
        if (clock == null) clock = FindAnyObjectByType<SongClock>();

        CurrentBounds = BaseBounds;
        DestinationBounds = BaseBounds;
        MaxBounds = BaseBounds;
        Update();

        if (fitCamera || setCameraBackground) SetUpCamera();
    }

    void Update()
    {
        float t = SongTime;
        CurrentBounds = BoundsAt(t);
        DestinationBounds = new GridBounds(
            DestinationEdgeAt(GridSide.Left, t), DestinationEdgeAt(GridSide.Right, t),
            DestinationEdgeAt(GridSide.Bottom, t), DestinationEdgeAt(GridSide.Top, t));
        CurrentPortal = PortalAt(t);

        // 하트를 그리는 셰이더가 격자 밖을 잘라 내도록 지금 격자 범위(월드 좌표)를 알려 준다.
        var b = CurrentBounds;
        Shader.SetGlobalVector(ClipRectId, new Vector4(EdgeToWorldX(b.minX), EdgeToWorldY(b.minY), EdgeToWorldX(b.maxX), EdgeToWorldY(b.maxY)));
    }

    void LateUpdate()
    {
        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

        // 무적 중 화면 반전을 배경에도 적용한다.
        if (setCameraBackground) cam.backgroundColor = ScreenInvert.Apply(backgroundColor);

        // 격자가 처음보다 커지면 카메라를 물린다. (위치는 그대로 두고 크기만 바꾼다)
        if (fitCamera && cam.orthographic) cam.orthographicSize = OrthographicSizeFor(CurrentBounds, cam.aspect);
    }

    /// <summary>칸 좌표를 월드 좌표(칸 중심)로 바꾼다.</summary>
    public Vector3 CellToWorld(int x, int y)
    {
        float offsetX = (columns - 1) * 0.5f;
        float offsetY = (rows - 1) * 0.5f;
        return transform.position + new Vector3((x - offsetX) * cellSize, (y - offsetY) * cellSize, 0f);
    }

    /// <summary>칸 단위 가로 위치(벽 위치 등)를 월드 x로 바꾼다.</summary>
    public float EdgeToWorldX(float x) => transform.position.x + (x - columns * 0.5f) * cellSize;

    /// <summary>칸 단위 세로 위치(벽 위치 등)를 월드 y로 바꾼다.</summary>
    public float EdgeToWorldY(float y) => transform.position.y + (y - rows * 0.5f) * cellSize;

    /// <summary>칸이 지금 격자 안에 온전히 있는지. 벽이 들어오기 시작한 칸은 밖이다.</summary>
    public bool IsInside(int x, int y) => CurrentBounds.ContainsCell(x, y);

    /// <summary>하트가 이 칸으로 이동할 수 있는지. 줄어드는 중인 칸과 막힌 칸으로는 못 간다.</summary>
    public bool CanEnter(Vector2Int cell)
    {
        return CurrentBounds.ContainsCell(cell.x, cell.y)
            && DestinationBounds.ContainsCell(cell.x, cell.y)
            && !IsBlocked(cell);
    }

    /// <summary>지금 격자 밖의 칸 좌표를 가장 가까운 안쪽 칸으로 당겨 준다. (벽에 밀려날 때 쓴다)</summary>
    public Vector2Int ClampCell(Vector2Int cell)
    {
        var b = CurrentBounds;
        int x = Mathf.Clamp(cell.x, b.FirstColumn, Mathf.Max(b.FirstColumn, b.LastColumn));
        int y = Mathf.Clamp(cell.y, b.FirstRow, Mathf.Max(b.FirstRow, b.LastRow));
        return new Vector2Int(x, y);
    }

    // ---------------- 벽 움직임 ----------------

    /// <summary>
    /// 레벨의 벽 이벤트를 받아 네 벽이 언제 어디로 움직일지 정해 둔다.
    /// 잘못된 이벤트는 건너뛰고 problems에 이유를 담는다.
    /// </summary>
    public void SetWalls(IEnumerable<WallEvent> events, List<string> problems)
    {
        foreach (var list in segments) list.Clear();

        var sorted = new List<WallEvent>(events);
        sorted.Sort((a, b) => a.time.CompareTo(b.time));

        var settled = BaseBounds;   // 지금까지의 이벤트가 끝난 뒤의 범위
        var max = BaseBounds;

        foreach (var e in sorted)
        {
            bool horizontal = GridSideUtil.IsHorizontal(e.side);   // 왼쪽·오른쪽 벽이면 가로 칸 수가 바뀐다.
            int limit = horizontal ? maxColumns : maxRows;
            if (e.size < 1 || e.size > limit)
            {
                problems?.Add($"벽 time={e.time:0.###} side={e.side}: size는 1부터 {limit}까지입니다.");
                continue;
            }

            // 반대쪽 벽은 그대로 두고, 이 벽만 움직여서 칸 수를 size로 만든다.
            float from = settled.Edge(e.side);
            float to;
            switch (e.side)
            {
                case GridSide.Right: to = settled.minX + e.size; settled.maxX = to; break;
                case GridSide.Left: to = settled.maxX - e.size; settled.minX = to; break;
                case GridSide.Top: to = settled.minY + e.size; settled.maxY = to; break;
                default: to = settled.maxY - e.size; settled.minY = to; break;
            }

            float warn = e.warnTime > 0f ? e.warnTime : defaultWallWarnTime;
            segments[(int)e.side].Add(new WallSegment
            {
                moveStart = e.time - wallMoveTime,
                moveEnd = e.time,
                warnStart = e.time - wallMoveTime - warn,
                from = from,
                to = to
            });

            max = GridBounds.Union(max, settled);
        }

        MaxBounds = max;
        Update();   // 바로 지금 시간의 범위로 맞춰 둔다.
    }

    /// <summary>노래 시간 t일 때의 격자 범위</summary>
    public GridBounds BoundsAt(float t)
    {
        return new GridBounds(EdgeAt(GridSide.Left, t), EdgeAt(GridSide.Right, t), EdgeAt(GridSide.Bottom, t), EdgeAt(GridSide.Top, t));
    }

    /// <summary>노래 시간 t일 때 side 벽의 위치 (칸 단위). 움직이는 중이면 부드럽게 사이 값이다.</summary>
    public float EdgeAt(GridSide side, float t)
    {
        float edge = BaseBounds.Edge(side);
        foreach (var s in segments[(int)side])
        {
            if (t < s.moveStart) break;
            if (t < s.moveEnd) return Mathf.Lerp(s.from, s.to, Mathf.SmoothStep(0f, 1f, (t - s.moveStart) / (s.moveEnd - s.moveStart)));
            edge = s.to;
        }
        return edge;
    }

    /// <summary>노래 시간 t에 움직이고 있는 벽이라면 도착할 위치, 아니면 지금 위치</summary>
    float DestinationEdgeAt(GridSide side, float t)
    {
        float edge = BaseBounds.Edge(side);
        foreach (var s in segments[(int)side])
        {
            if (t < s.moveStart) break;
            edge = s.to;
        }
        return edge;
    }

    /// <summary>지금 줄어들 예정이거나 줄어드는 중인 칸들의 경고 띠를 output에 담는다.</summary>
    public void GetShrinkWarnings(List<ShrinkWarning> output)
    {
        output.Clear();
        float t = SongTime;
        var now = CurrentBounds;

        for (int i = 0; i < segments.Length; i++)
        {
            var side = (GridSide)i;
            foreach (var s in segments[i])
            {
                bool shrinking = side == GridSide.Right || side == GridSide.Top ? s.to < s.from : s.to > s.from;
                if (!shrinking || t < s.warnStart || t >= s.moveEnd) continue;

                // 지금 벽 위치부터 도착할 위치까지가 없어질 칸이다.
                float edge = EdgeAt(side, t);
                float a = Mathf.Min(edge, s.to);
                float b = Mathf.Max(edge, s.to);
                if (b - a <= 0.001f) continue;

                Rect cells = GridSideUtil.IsHorizontal(side)
                    ? Rect.MinMaxRect(a, now.minY, b, now.maxY)
                    : Rect.MinMaxRect(now.minX, a, now.maxX, b);

                output.Add(new ShrinkWarning
                {
                    cells = cells,
                    progress = Mathf.Clamp01((t - s.warnStart) / Mathf.Max(0.001f, s.moveEnd - s.warnStart))
                });
            }
        }
    }

    // ---------------- 텔레포트 ----------------

    /// <summary>레벨의 텔레포트 이벤트를 받아 둔다.</summary>
    public void SetPortals(IEnumerable<PortalEvent> events)
    {
        portals.Clear();
        portals.AddRange(events);
        portals.Sort((a, b) => a.time.CompareTo(b.time));
        Update();
    }

    /// <summary>
    /// 노래 시간 t일 때의 텔레포트 상태. 가장 최근 이벤트가 정하고, 다음 이벤트까지 유지된다.
    /// 격자 색은 이벤트 뒤 portalTransitionTime 동안 부드럽게 바뀐다.
    /// </summary>
    public PortalState PortalAt(float t)
    {
        int index = -1;
        for (int i = 0; i < portals.Count && portals[i].time <= t; i++) index = i;
        if (index < 0) return new PortalState();

        var current = portals[index];
        float before = index > 0 && portals[index - 1].enabled ? 1f : 0f;
        float after = current.enabled ? 1f : 0f;
        float u = Mathf.SmoothStep(0f, 1f, (t - current.time) / portalTransitionTime);

        return new PortalState
        {
            horizontal = current.enabled && current.horizontal,
            vertical = current.enabled && current.vertical,
            blend = Mathf.Lerp(before, after, u)
        };
    }

    /// <summary>
    /// 텔레포트로 넘어갈 수 있으면, from 칸에서 (dx, dy)로 벽을 넘었을 때 도착하는 반대쪽 끝 칸을 돌려준다.
    /// 그쪽 벽끼리 이어져 있지 않거나, 도착할 칸에 들어갈 수 없으면 false.
    /// </summary>
    public bool TryGetWrapTarget(Vector2Int from, int dx, int dy, out Vector2Int target)
    {
        var b = CurrentBounds;
        var portal = CurrentPortal;
        target = from;

        if (dx < 0 && portal.horizontal && from.x == b.FirstColumn) target = new Vector2Int(b.LastColumn, from.y);
        else if (dx > 0 && portal.horizontal && from.x == b.LastColumn) target = new Vector2Int(b.FirstColumn, from.y);
        else if (dy < 0 && portal.vertical && from.y == b.FirstRow) target = new Vector2Int(from.x, b.LastRow);
        else if (dy > 0 && portal.vertical && from.y == b.LastRow) target = new Vector2Int(from.x, b.FirstRow);
        else return false;

        return target != from && CanEnter(target);
    }

    // ---------------- 막힌 칸 ----------------

    // 막힌 칸마다 막고 있는 장애물 수. 같은 칸을 여러 바닥 포탄이 겹쳐 막아도
    // 마지막 하나가 풀릴 때까지 막혀 있게 하려고 개수로 센다.
    readonly Dictionary<Vector2Int, int> blockCounts = new Dictionary<Vector2Int, int>();

    /// <summary>칸이 막혀 있는지. 막힌 칸으로는 하트가 들어갈 수 없다.</summary>
    public bool IsBlocked(Vector2Int cell) => blockCounts.TryGetValue(cell, out int count) && count > 0;

    /// <summary>칸을 막는다. 같은 장애물이 나중에 Unblock을 꼭 한 번 불러야 한다.</summary>
    public void Block(Vector2Int cell)
    {
        blockCounts.TryGetValue(cell, out int count);
        blockCounts[cell] = count + 1;
    }

    /// <summary>Block으로 막았던 칸을 하나 푼다.</summary>
    public void Unblock(Vector2Int cell)
    {
        if (!blockCounts.TryGetValue(cell, out int count)) return;
        if (count <= 1) blockCounts.Remove(cell);
        else blockCounts[cell] = count - 1;
    }

    // ---------------- 카메라 ----------------

    /// <summary>격자 범위가 bounds일 때 카메라가 비추는 영역 (월드 좌표). 화면 밖 위치를 정할 때 쓴다.</summary>
    public Rect ViewRectFor(GridBounds bounds)
    {
        var cam = targetCamera != null ? targetCamera : Camera.main;
        float aspect = cam != null ? Mathf.Max(cam.aspect, 0.01f) : 16f / 9f;
        float halfHeight = OrthographicSizeFor(bounds, aspect);
        float halfWidth = halfHeight * aspect;
        Vector2 center = transform.position;
        return new Rect(center.x - halfWidth, center.y - halfHeight, halfWidth * 2f, halfHeight * 2f);
    }

    /// <summary>
    /// 처음 격자와 bounds를 모두 담는 카메라 크기. 카메라는 처음 격자 가운데에 고정하고,
    /// 가운데에서 가장 먼 벽까지 다 보이게 한다. 격자가 처음보다 작아질 때는 확대하지 않는다.
    /// </summary>
    float OrthographicSizeFor(GridBounds bounds, float aspect)
    {
        var b = GridBounds.Union(BaseBounds, bounds);
        float centerX = columns * 0.5f;
        float centerY = rows * 0.5f;
        float halfX = Mathf.Max(centerX - b.minX, b.maxX - centerX) * cellSize * (1f + cameraMargin);
        float halfY = Mathf.Max(centerY - b.minY, b.maxY - centerY) * cellSize * (1f + cameraMargin);
        return Mathf.Max(halfY, halfX / Mathf.Max(aspect, 0.01f));
    }

    /// <summary>카메라 위치와 크기, 배경색을 처음 격자에 맞춘다.</summary>
    void SetUpCamera()
    {
        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

        if (setCameraBackground)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
        }

        if (!fitCamera || !cam.orthographic) return;

        cam.orthographicSize = OrthographicSizeFor(BaseBounds, cam.aspect);
        Vector3 center = transform.position;
        cam.transform.position = new Vector3(center.x, center.y, cam.transform.position.z);
    }
}
