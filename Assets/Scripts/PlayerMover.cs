using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 하트의 입력과 이동을 담당한다.
/// 키를 한 번 누르면 목표 칸이 한 칸 옮겨지고, 하트는 목표 칸까지
/// 남은 거리의 일정 비율씩 다가가며 부드럽게 움직인다.
/// 모양과 크기는 HeartView가 담당한다.
///
/// 텔레포트 기믹이 켜져 있으면, 끝 칸에서 벽 쪽으로 움직일 때 반대편 끝 칸으로 감싸며 넘어간다.
/// 하트를 "벽 너머 한 칸"과 "반대편 도착 칸"의 차이만큼 옮겨서, 반대편 벽 밖에서 일반 이동처럼 미끄러져 들어오게 한다.
/// 원래 쪽에서 빠져나가는 모습은 HeartView가 복사본으로 그리고, 격자 밖은 셰이더가 잘라 낸다.
/// </summary>
public class PlayerMover : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("비워 두면 씬에서 GameGrid를 찾아 쓴다.")]
    public GameGrid grid;

    [Header("시작 위치")]
    [Tooltip("-1로 두면 격자 가운데 칸에서 시작한다.")]
    public Vector2Int startCell = new Vector2Int(-1, -1);

    [Header("이동")]
    [Tooltip("기준 프레임 한 번에 목표까지 남은 거리의 몇 비율을 좁히는지. 크면 빨라진다.")]
    [Range(0.01f, 0.9f)] public float approachRatio = 0.4f;

    [Tooltip("위 비율의 기준 프레임 수. 실제 프레임 속도와 무관하게 같은 속도가 된다.")]
    [Min(1f)] public float referenceFps = 60f;

    [Tooltip("목표까지 이 거리보다 가까우면 칸에 딱 붙인다.")]
    [Min(0.0001f)] public float snapDistance = 0.01f;

    /// <summary>하트가 향하고 있는 목표 칸</summary>
    public Vector2Int Cell { get; private set; }

    /// <summary>칸 사이를 움직이고 있는지</summary>
    public bool IsMoving { get; private set; }

    /// <summary>이번 프레임의 이동 속도 (초당 월드 거리, 방향 포함). 하트 늘어나기에 쓴다.</summary>
    public Vector2 Velocity { get; private set; }

    /// <summary>벽을 넘어가는 중인지 (하트가 아직 반대편 벽 근처에 걸쳐 있는지)</summary>
    public bool IsWrapping { get; private set; }

    /// <summary>마지막 텔레포트에서 옮긴 거리. 원래 쪽 복사본은 하트 위치에서 이만큼 뺀 곳에 있다.</summary>
    public Vector3 WrapOffset { get; private set; }

    /// <summary>텔레포트한 순간 옮긴 거리를 알린다. (잔상이 화면을 가로질러 남지 않게)</summary>
    public event System.Action<Vector3> Teleported;

    void Awake()
    {
        if (grid == null) grid = FindAnyObjectByType<GameGrid>();
    }

    void Start()
    {
        if (grid == null)
        {
            Debug.LogError("PlayerMover: 씬에 GameGrid가 없습니다.");
            enabled = false;
            return;
        }

        Cell = startCell.x < 0 || startCell.y < 0 ? grid.CenterCell : grid.ClampCell(startCell);
        transform.position = grid.CellToWorld(Cell.x, Cell.y);
    }

    void Update()
    {
        ReadInput();
        PushByWalls();
        MoveTowardCell();
    }

    /// <summary>WASD와 방향키를 읽어 목표 칸을 한 칸 옮긴다.</summary>
    void ReadInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        int dx = 0;
        int dy = 0;

        if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) dy += 1;
        if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) dy -= 1;
        if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) dx -= 1;
        if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) dx += 1;

        // 같은 프레임에 가로와 세로가 함께 눌리면 대각선이 되지 않도록 가로만 쓴다.
        if (dx != 0) dy = 0;
        if (dx == 0 && dy == 0) return;

        var next = new Vector2Int(Cell.x + dx, Cell.y + dy);

        // 격자 밖, 줄어드는 중인 칸, 막힌 칸이면 그대로 멈춘다. (가려고 해도 피해는 없다)
        if (grid.CanEnter(next))
        {
            Cell = next;
            return;
        }

        // 텔레포트가 켜져 있고 끝 칸이면 반대편 끝 칸으로 넘어간다.
        if (grid.TryGetWrapTarget(Cell, dx, dy, out var target)) WrapTo(next, target);
    }

    /// <summary>
    /// 벽 너머의 칸(beyondWall)으로 가는 대신 반대편 끝 칸(target)으로 간다.
    /// 두 칸의 차이만큼 하트를 옮기면, 하트는 반대편 벽 밖에서 같은 방향으로 미끄러져 들어온다.
    /// </summary>
    void WrapTo(Vector2Int beyondWall, Vector2Int target)
    {
        Vector3 offset = grid.CellToWorld(target.x, target.y) - grid.CellToWorld(beyondWall.x, beyondWall.y);
        transform.position += offset;
        Cell = target;
        WrapOffset = offset;
        IsWrapping = true;
        Teleported?.Invoke(offset);
    }

    /// <summary>벽이 지금 칸으로 들어오기 시작하면 피해 없이 가장 가까운 안쪽 칸으로 밀려난다.</summary>
    void PushByWalls()
    {
        if (!grid.IsInside(Cell.x, Cell.y)) Cell = grid.ClampCell(Cell);
    }

    /// <summary>목표 칸까지 남은 거리의 일정 비율씩 다가간다. 프레임 속도와 무관하다.</summary>
    void MoveTowardCell()
    {
        Vector3 target = grid.CellToWorld(Cell.x, Cell.y);
        Vector3 current = transform.position;

        if (Vector3.Distance(current, target) <= snapDistance)
        {
            transform.position = target;
            IsMoving = false;
            IsWrapping = false;
            Velocity = Vector2.zero;
            return;
        }

        // 기준 프레임당 approachRatio씩 좁히는 것을 실제 deltaTime으로 환산한다.
        float t = 1f - Mathf.Pow(1f - approachRatio, Time.deltaTime * referenceFps);
        Vector3 next = Vector3.Lerp(current, target, t);

        // 벽을 넘어가는 중에는 벽 밖에서 들어와야 하므로 벽 안쪽으로 붙잡지 않는다.
        if (IsWrapping)
        {
            if (IsWellInside(next)) IsWrapping = false;
        }
        else
        {
            next = KeepInsideWalls(next);
        }
        transform.position = next;
        IsMoving = true;

        // 시간이 멈춰 있으면(deltaTime 0) 속도를 0으로 둔다.
        Velocity = Time.deltaTime > 0f ? (Vector2)(next - current) / Time.deltaTime : Vector2.zero;
    }

    /// <summary>하트 중심이 모든 벽에서 반 칸 이상 안쪽에 있는지 (벽 밖으로 삐져나온 부분이 없는지)</summary>
    bool IsWellInside(Vector3 position)
    {
        return Vector3.Distance(position, KeepInsideWalls(position)) < 0.0001f;
    }

    /// <summary>하트 중심이 벽에서 반 칸보다 가까워지지 않게 한다. 벽이 들어오면 하트를 밀어내는 효과다.</summary>
    Vector3 KeepInsideWalls(Vector3 position)
    {
        var b = grid.CurrentBounds;
        float half = grid.cellSize * 0.5f;
        float minX = grid.EdgeToWorldX(b.minX) + half;
        float maxX = grid.EdgeToWorldX(b.maxX) - half;
        float minY = grid.EdgeToWorldY(b.minY) + half;
        float maxY = grid.EdgeToWorldY(b.maxY) - half;

        if (minX <= maxX) position.x = Mathf.Clamp(position.x, minX, maxX);
        if (minY <= maxY) position.y = Mathf.Clamp(position.y, minY, maxY);
        return position;
    }
}
