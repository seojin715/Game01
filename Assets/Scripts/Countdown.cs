using UnityEngine;

/// <summary>
/// 게임을 시작하면 노래가 나오기 전에 3, 2, 1을 격자 칸으로 세어 주고, 끝나면 노래를 튼다.
///
/// 세는 동안에는 원래 격자 선이 사라지고, 숫자 모양(#)인 칸의 테두리만 격자 선처럼 그려져서 빛난다.
/// 나머지 칸(0)은 없는 칸처럼 아무것도 그리지 않는다. (칸을 채우지 않고 원래 격자 모양 그대로 드러난다)
///
/// 숫자가 바뀔 때는 크기를 바꾸지 않고 칸마다 서서히 나타나고 사라진다.
/// 다음 숫자에도 있는 칸은 그대로 두고, 필요 없어진 칸만 사라지고(fade out) 새로 필요한 칸만 나타난다(fade in).
/// 처음 숫자는 모든 칸이 나타난다. 마지막 숫자 뒤에는 빈 칸까지 모두 나타나면서 빛이 원래 격자 밝기로 내려가
/// 원래 격자와 똑같은 모습이 되고, 그 순간 진짜 격자(GridView)로 넘겨준 뒤 준비 시간이 시작된다. (끊기지 않게)
///
/// 하트는 움직이지 못하고, 장애물도 나오지 않는다. (PlayerMover, GridView, LevelPlayer가 IsActive를 본다)
/// Music 오브젝트(SongClock이 있는 곳)에 붙인다. 끄면(체크 해제) 예전처럼 바로 노래가 시작된다.
/// </summary>
[DefaultExecutionOrder(-95)]
public class Countdown : MonoBehaviour
{
    [Header("숫자 모양")]
    [Tooltip("차례로 보여 줄 숫자. 한 줄이 격자 한 줄(맨 윗줄부터)이고, #는 테두리가 드러나는 칸, 0은 없는 칸이다.")]
    [TextArea(5, 7)]
    public string[] digits =
    {
        "0###0\n000#0\n0###0\n000#0\n0###0",   // 3
        "0###0\n000#0\n0###0\n0#000\n0###0",   // 2
        "00#00\n0##00\n00#00\n00#00\n0###0",   // 1
    };

    [Header("타이밍 (실제 초)")]
    [Tooltip("노래를 다 불러온 뒤 첫 숫자가 나타나기 시작할 때까지 기다리는 시간")]
    [Min(0f)] public float startDelay = 0.3f;

    [Tooltip("숫자 하나를 보여 주는 시간 (다음 숫자로 바뀌기 시작할 때까지)")]
    [Min(0.1f)] public float stepTime = 1f;

    [Tooltip("칸이 서서히 나타나거나 사라지는 시간. 마지막 숫자 뒤에 원래 격자로 바뀌는 시간도 이것이다.")]
    [Min(0.01f)] public float fadeTime = 0.25f;

    [Tooltip("한 프레임에 흐르는 시간의 최대값. 컴퓨터가 잠깐 멈칫해도 연출이 건너뛰지 않고 이어지게 한다.")]
    [Range(0.01f, 0.1f)] public float maxFrameStep = 1f / 30f;

    [Header("빈 칸 비침")]
    [Tooltip("숫자가 처음 나타날 때와 바뀔 때, 비어 있는 칸까지 원래 격자 전체가 비치는 진하기. 0이면 비치지 않는다.")]
    [Range(0f, 1f)] public float ghostAlpha = 0.12f;

    [Tooltip("격자 전체가 옅게 비쳤다가 사라지기까지 걸리는 시간")]
    [Min(0.01f)] public float ghostTime = 0.5f;

    [Header("모양")]
    [Tooltip("테두리 선 두께 (월드 단위). 0이면 격자(GridView)의 선 두께와 같게 한다.")]
    [Min(0f)] public float lineWidth = 0f;

    public Color color = Color.white;

    [Tooltip("밝기. 1보다 크면 Bloom이 번져서 빛난다.")]
    [Range(1f, 6f)] public float glowIntensity = 2f;

    [Tooltip("격자 선(-10)보다 높게, 하트(10)보다 낮게")]
    public int sortingOrder = -8;

    [Header("참조 (비워 두면 씬에서 찾는다)")]
    public SongClock clock;
    public GameGrid grid;

    /// <summary>지금 세는 중인지 (노래를 불러오는 동안 포함). 이 동안 하트와 장애물이 멈춘다.</summary>
    public static bool IsActive { get; private set; }

    static readonly int ColorProperty = Shader.PropertyToID("_Color");

    // 선 조각. 겹치지 않게 꼭짓점(작은 정사각형), 가로 변, 세로 변으로 나눠 둔다.
    // 그래야 칸마다 투명도가 달라도 겹친 곳만 진해지지 않는다.
    SpriteRenderer[,] corners;     // [columns + 1, rows + 1]
    SpriteRenderer[,] horizontals; // [columns, rows + 1]  k번 가로선 = y가 k - 1인 칸과 k인 칸 사이
    SpriteRenderer[,] verticals;   // [columns + 1, rows]  k번 세로선 = x가 k - 1인 칸과 k인 칸 사이
    bool[][,] shapes;              // 숫자마다 칸이 켜져 있는지
    float[,] alphas;               // 이번 프레임 칸마다 보이는 정도 (0~1)
    Material glowMaterial;
    bool[,] fullGrid;              // 모든 칸이 켜진 모양 (마지막에 원래 격자로 돌아갈 때)
    float glow = 1f;               // 지금 밝기
    float elapsed;
    bool counting;

    // 지난 플레이에서 세던 중에 멈췄어도 새 플레이는 꺼진 상태로 시작한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => IsActive = false;

    void Awake()
    {
        if (clock == null) clock = FindAnyObjectByType<SongClock>();
        if (grid == null) grid = FindAnyObjectByType<GameGrid>();
        if (!enabled || clock == null || grid == null || digits == null || digits.Length == 0) return;

        // 노래는 다 센 뒤에 직접 튼다.
        clock.playOnStart = false;
        IsActive = true;
    }

    void Start()
    {
        if (!IsActive) return;

        int columns = grid.columns;
        int rows = grid.rows;

        shapes = new bool[digits.Length][,];
        for (int i = 0; i < digits.Length; i++) shapes[i] = ParseDigit(digits[i], columns, rows);
        fullGrid = new bool[columns, rows];
        for (int x = 0; x < columns; x++) for (int y = 0; y < rows; y++) fullGrid[x, y] = true;
        alphas = new float[columns, rows];
        glow = glowIntensity;

        BuildLines(columns, rows);
        SetAlphas();
    }

    void Update()
    {
        if (!IsActive) return;

        // 노래 파일을 못 찾았으면 세지 않고 그만둔다. (하트가 멈춘 채로 남지 않게)
        if (clock.LoadFailed)
        {
            Finish(false);
            return;
        }

        // 노래를 다 불러온 뒤부터 센다. 불러온 프레임은 오래 걸려서 건너뛰고 다음 프레임부터 잰다.
        if (!counting)
        {
            if (!clock.IsLoaded) return;
            counting = true;
            elapsed = 0f;
            return;
        }

        // 한 프레임이 길게 멈칫해도(불러오기, 처음 그리기 등) 연출이 건너뛰지 않도록 한 번에 흐르는 시간을 제한한다.
        elapsed += Mathf.Min(Time.unscaledDeltaTime, maxFrameStep);
        float t = elapsed - startDelay;
        if (t < 0f) return;

        // step번째 숫자로 바뀌는 중이거나 바뀐 뒤다. (digits.Length번째는 "격자 전체" = 원래 격자로 돌아감)
        int step = Mathf.FloorToInt(t / stepTime);
        float local = t - step * stepTime;
        if (step >= digits.Length && local >= fadeTime)
        {
            Finish(true);
            return;
        }

        // 이전 모양에서 이번 모양으로: 둘 다 있는 칸은 그대로, 없어지는 칸은 사라지고, 새 칸은 나타난다.
        float p = Mathf.SmoothStep(0f, 1f, local / fadeTime);
        bool[,] previous = step > 0 ? ShapeAt(step - 1) : null;
        bool[,] current = ShapeAt(step);
        bool toGrid = step >= digits.Length;

        // 숫자가 나타나거나 바뀔 때, 빈 칸까지 원래 격자 전체가 아주 옅게 비쳤다가 사라진다. (원래 격자로 돌아갈 때는 없음)
        float ghost = 0f;
        if (!toGrid && local < ghostTime) ghost = ghostAlpha * Mathf.Sin(local / ghostTime * Mathf.PI);

        // 원래 격자로 돌아가는 동안 빛을 원래 격자 밝기(1)로 내려서, 넘겨줄 때 밝기가 튀지 않게 한다.
        glow = toGrid ? Mathf.Lerp(glowIntensity, 1f, p) : glowIntensity;

        for (int x = 0; x < alphas.GetLength(0); x++)
        {
            for (int y = 0; y < alphas.GetLength(1); y++)
            {
                bool was = previous != null && previous[x, y];
                bool now = current != null && current[x, y];
                float alpha = was && now ? 1f : now ? p : was ? 1f - p : 0f;

                // 비침을 겹쳐 더한다. (큰 쪽만 고르면 사라지는 칸이 비침 높이에서 잠깐 멈췄다 사라져서 끊겨 보인다)
                alphas[x, y] = alpha + ghost * (1f - alpha);
            }
        }

        SetAlphas();
    }

    /// <summary>칸마다 보이는 정도에 맞춰 선 조각을 칠한다. 선은 맞닿은 칸 중 가장 잘 보이는 칸을 따른다.</summary>
    void SetAlphas()
    {
        if (alphas == null) return;
        if (glowMaterial != null) glowMaterial.SetColor(ColorProperty, SpriteMaterial.Glow(Color.white, glow));

        int columns = alphas.GetLength(0);
        int rows = alphas.GetLength(1);

        for (int x = 0; x <= columns; x++)
        {
            for (int y = 0; y <= rows; y++)
            {
                float corner = Mathf.Max(Mathf.Max(AlphaAt(x - 1, y - 1), AlphaAt(x, y - 1)), Mathf.Max(AlphaAt(x - 1, y), AlphaAt(x, y)));
                Paint(corners[x, y], corner);

                if (x < columns) Paint(horizontals[x, y], Mathf.Max(AlphaAt(x, y - 1), AlphaAt(x, y)));
                if (y < rows) Paint(verticals[x, y], Mathf.Max(AlphaAt(x - 1, y), AlphaAt(x, y)));
            }
        }
    }

    /// <summary>step번째 모양. 숫자 다음은 격자 전체다.</summary>
    bool[,] ShapeAt(int step) => step < shapes.Length ? shapes[step] : fullGrid;

    float AlphaAt(int x, int y)
    {
        return x >= 0 && y >= 0 && x < alphas.GetLength(0) && y < alphas.GetLength(1) ? alphas[x, y] : 0f;
    }

    void Paint(SpriteRenderer line, float alpha)
    {
        line.enabled = alpha > 0f;
        var c = color;
        c.a = alpha;
        line.color = c;
        line.sortingOrder = sortingOrder;
    }

    /// <summary>처음 격자 크기만큼 선 조각을 만들어 둔다. (벽 이동은 노래가 시작된 뒤의 일이다)</summary>
    void BuildLines(int columns, int rows)
    {
        glowMaterial = SpriteMaterial.Create("CountdownGlow");
        var root = new GameObject("CountdownOutline").transform;
        root.SetParent(transform, false);

        float width = lineWidth > 0f ? lineWidth : GridLineWidth();
        float cell = grid.cellSize;

        corners = new SpriteRenderer[columns + 1, rows + 1];
        horizontals = new SpriteRenderer[columns, rows + 1];
        verticals = new SpriteRenderer[columns + 1, rows];

        for (int x = 0; x <= columns; x++)
        {
            for (int y = 0; y <= rows; y++)
            {
                float px = grid.EdgeToWorldX(x);
                float py = grid.EdgeToWorldY(y);
                corners[x, y] = CreateLine(root, new Vector2(px, py), new Vector2(width, width));

                // 변은 양 끝 꼭짓점 사이만 채운다.
                if (x < columns) horizontals[x, y] = CreateLine(root, new Vector2(px + cell * 0.5f, py), new Vector2(cell - width, width));
                if (y < rows) verticals[x, y] = CreateLine(root, new Vector2(px, py + cell * 0.5f), new Vector2(width, cell - width));
            }
        }
    }

    SpriteRenderer CreateLine(Transform parent, Vector2 position, Vector2 size)
    {
        var line = ShapeSprites.CreateSquare("Line", parent, color, sortingOrder);
        if (glowMaterial != null) line.sharedMaterial = glowMaterial;
        line.transform.position = position;
        line.transform.localScale = new Vector3(size.x, size.y, 1f);
        line.enabled = false;
        return line;
    }

    float GridLineWidth()
    {
        var view = grid.GetComponent<GridView>();
        return view != null ? view.lineWidth : 0.04f;
    }

    /// <summary>모양 글자를 칸마다 켜짐/꺼짐으로 바꾼다. 맨 윗줄이 글자의 첫 줄이고, 크기가 다르면 가운데에 맞춘다.</summary>
    static bool[,] ParseDigit(string text, int columns, int rows)
    {
        var lit = new bool[columns, rows];
        if (string.IsNullOrEmpty(text)) return lit;

        string[] lines = text.Replace("\r", "").Split('\n');
        int width = 0;
        foreach (var line in lines) width = Mathf.Max(width, line.Trim().Length);
        int offsetX = (columns - width) / 2;
        int offsetY = (rows - lines.Length) / 2;

        for (int r = 0; r < lines.Length; r++)
        {
            string line = lines[r].Trim();
            for (int c = 0; c < line.Length; c++)
            {
                int x = c + offsetX;
                int y = rows - 1 - (r + offsetY);
                if (line[c] == '#' && x >= 0 && x < columns && y >= 0 && y < rows) lit[x, y] = true;
            }
        }
        return lit;
    }

    /// <summary>
    /// 세기를 끝내고 선을 지운다. playSong이면 노래를 튼다(준비 시간부터).
    /// 같은 프레임에 GridView가 원래 격자를 그리므로, 원래 격자와 똑같아진 선을 지워도 끊겨 보이지 않는다.
    /// </summary>
    void Finish(bool playSong)
    {
        IsActive = false;
        if (alphas != null)
        {
            System.Array.Clear(alphas, 0, alphas.Length);
            SetAlphas();
        }
        if (playSong && clock != null) clock.Play();
    }

    void OnDisable()
    {
        // 세는 도중에 끄면 바로 노래를 시작한다. (플레이를 멈출 때는 노래를 틀지 않는다)
        if (IsActive) Finish(clock != null && clock.isActiveAndEnabled && clock.IsLoaded);
    }

    void OnDestroy()
    {
        if (glowMaterial != null) Destroy(glowMaterial);
    }
}
