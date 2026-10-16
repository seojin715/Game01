using UnityEngine;

/// <summary>
/// 격자를 흰 선으로만 그린다. 칸은 채우지 않는다.
/// 선은 플레이할 때 자식 오브젝트로 만들어진다.
/// </summary>
[RequireComponent(typeof(GameGrid))]
public class GridView : MonoBehaviour
{
    [Header("선")]
    public Color lineColor = Color.white;

    [Tooltip("선 두께 (월드 단위)")]
    [Min(0.001f)] public float lineWidth = 0.04f;

    [Tooltip("그리는 순서. 하트보다 낮게 둬서 하트가 선 위에 보이게 한다.")]
    public int sortingOrder = -10;

    GameGrid grid;

    void Awake()
    {
        grid = GetComponent<GameGrid>();
        BuildLines();
    }

    /// <summary>세로 선 columns + 1개, 가로 선 rows + 1개를 만든다.</summary>
    void BuildLines()
    {
        Vector3 center = transform.position;
        float width = grid.Width;
        float height = grid.Height;

        for (int i = 0; i <= grid.columns; i++)
        {
            float x = center.x - width * 0.5f + i * grid.cellSize;
            // 선 끝이 서로 맞물리도록 길이에 두께만큼을 더한다.
            MakeLine($"VerticalLine_{i}", new Vector3(x, center.y, 0f), new Vector2(lineWidth, height + lineWidth));
        }

        for (int i = 0; i <= grid.rows; i++)
        {
            float y = center.y - height * 0.5f + i * grid.cellSize;
            MakeLine($"HorizontalLine_{i}", new Vector3(center.x, y, 0f), new Vector2(width + lineWidth, lineWidth));
        }
    }

    void MakeLine(string name, Vector3 position, Vector2 size)
    {
        var renderer = ShapeSprites.CreateSquare(name, transform, lineColor, sortingOrder);
        renderer.transform.position = position;
        renderer.transform.localScale = new Vector3(size.x, size.y, 1f);
    }
}
