using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 격자의 크기와 칸 좌표 변환을 담당한다.
/// 칸 좌표는 왼쪽 아래가 (0, 0), 오른쪽 위가 (columns - 1, rows - 1).
/// 앞으로 장애물도 이 좌표계를 기준으로 배치한다.
/// </summary>
public class GameGrid : MonoBehaviour
{
    [Header("격자 크기")]
    [Min(1)] public int columns = 5;
    [Min(1)] public int rows = 5;

    [Tooltip("칸 한 변의 길이 (월드 단위)")]
    [Min(0.1f)] public float cellSize = 1f;

    [Header("카메라 맞춤")]
    [Tooltip("켜면 격자가 화면에 꽉 들어오도록 카메라를 자동으로 맞춘다.")]
    public bool fitCamera = true;

    [Tooltip("격자와 화면 가장자리 사이 여백 (격자 크기 대비 비율)")]
    [Range(0f, 1f)] public float cameraMargin = 0.4f;

    [Tooltip("켜면 카메라 배경을 아래 색으로 바꾼다.")]
    public bool setCameraBackground = true;
    public Color backgroundColor = Color.black;

    [Tooltip("비워 두면 Main Camera를 쓴다.")]
    public Camera targetCamera;

    /// <summary>격자 전체 가로 길이</summary>
    public float Width => columns * cellSize;

    /// <summary>격자 전체 세로 길이</summary>
    public float Height => rows * cellSize;

    /// <summary>격자 가운데 칸</summary>
    public Vector2Int CenterCell => new Vector2Int(columns / 2, rows / 2);

    void Awake()
    {
        if (fitCamera || setCameraBackground) SetUpCamera();
    }

    void LateUpdate()
    {
        // 무적 중 화면 반전을 배경에도 적용한다.
        if (!setCameraBackground) return;

        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam != null) cam.backgroundColor = ScreenInvert.Apply(backgroundColor);
    }

    /// <summary>칸 좌표를 월드 좌표(칸 중심)로 바꾼다.</summary>
    public Vector3 CellToWorld(int x, int y)
    {
        float offsetX = (columns - 1) * 0.5f;
        float offsetY = (rows - 1) * 0.5f;
        return transform.position + new Vector3((x - offsetX) * cellSize, (y - offsetY) * cellSize, 0f);
    }

    /// <summary>칸 좌표가 격자 안인지 검사한다.</summary>
    public bool IsInside(int x, int y) => x >= 0 && x < columns && y >= 0 && y < rows;

    /// <summary>격자 밖의 칸 좌표를 격자 안으로 당겨 준다.</summary>
    public Vector2Int ClampCell(Vector2Int cell)
    {
        return new Vector2Int(Mathf.Clamp(cell.x, 0, columns - 1), Mathf.Clamp(cell.y, 0, rows - 1));
    }

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

    /// <summary>격자가 화면에 다 보이도록 카메라 위치와 크기, 배경색을 맞춘다.</summary>
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

        float halfHeight = Height * 0.5f * (1f + cameraMargin);
        float halfWidth = Width * 0.5f * (1f + cameraMargin);

        // orthographicSize는 화면 세로 절반 크기라서, 가로가 넘치면 화면 비율로 나눠 맞춘다.
        float aspect = Mathf.Max(cam.aspect, 0.01f);
        cam.orthographicSize = Mathf.Max(halfHeight, halfWidth / aspect);

        Vector3 center = transform.position;
        cam.transform.position = new Vector3(center.x, center.y, cam.transform.position.z);
    }
}
