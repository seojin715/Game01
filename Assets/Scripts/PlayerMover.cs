using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 하트의 입력과 이동을 담당한다.
/// 키를 한 번 누르면 목표 칸이 한 칸 옮겨지고, 하트는 목표 칸까지
/// 남은 거리의 일정 비율씩 다가가며 부드럽게 움직인다.
/// 모양과 크기는 HeartView가 담당한다.
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

        // 격자 밖이나 막힌 칸이면 그대로 멈춘다. (막힌 칸으로 가려고 해도 피해는 없다)
        if (grid.IsInside(next.x, next.y) && !grid.IsBlocked(next)) Cell = next;
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
            Velocity = Vector2.zero;
            return;
        }

        // 기준 프레임당 approachRatio씩 좁히는 것을 실제 deltaTime으로 환산한다.
        float t = 1f - Mathf.Pow(1f - approachRatio, Time.deltaTime * referenceFps);
        Vector3 next = Vector3.Lerp(current, target, t);
        transform.position = next;
        IsMoving = true;

        // 시간이 멈춰 있으면(deltaTime 0) 속도를 0으로 둔다.
        Velocity = Time.deltaTime > 0f ? (Vector2)(next - current) / Time.deltaTime : Vector2.zero;
    }
}
