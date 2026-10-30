using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 격자를 흰 선으로만 그린다. 칸은 채우지 않는다.
///
/// 벽이 움직이므로 매 프레임 지금 격자 범위에 맞춰 선을 다시 놓는다.
/// 바깥 선(벽)은 부드럽게 미끄러지고, 벽이 지나간 안쪽 선은 사라진다.
/// 줄어들 칸에는 벽이 움직이기 전부터 반투명한 경고 띠를 깐다.
/// 선 오브젝트는 미리 만들어 두고 돌려쓴다.
///
/// 텔레포트 기믹이 켜지면 바깥 벽 네 변만 더 두껍고 빛나는 흰 선으로 바뀐다. 각 변의 가운데에서 양쪽 꼭짓점으로 퍼져 나가
/// 결국 벽 전체가 바뀌고, 꺼질 때는 거꾸로 가운데로 줄어든다. 안쪽 칸 경계선은 그대로다. 하트가 끝 칸에 있으면
/// 그 줄의 양쪽 벽에서 그 칸 길이만큼의 벽 선이 조금 굵어지며 은은하게 빛난다. (넘어갈 수 있는 문)
/// 실제로 넘어가는 순간에는 빛나던 양쪽 벽 선이 잠깐 더 밝고 굵어졌다가 돌아온다.
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

    [Header("줄어들 칸 경고")]
    [Tooltip("경고 띠의 불투명도. 벽이 도착하기 직전에 이 값이 되고, 처음엔 그 절반에서 시작한다.")]
    [Range(0f, 1f)] public float warningAlpha = 0.25f;

    [Tooltip("경고 띠를 그리는 순서. 격자 선보다 아래")]
    public int warningSortingOrder = -12;

    [Header("텔레포트 벽")]
    [Tooltip("텔레포트가 켜졌을 때 바깥 벽 선의 색. 무적 중 화면 반전이면 뒤집힌다.")]
    public Color portalWallColor = Color.white;

    [Tooltip("텔레포트가 켜졌을 때 바깥 벽 선의 두께 (격자 선 두께 대비)")]
    [Range(1f, 6f)] public float portalWallThickness = 2f;

    [Tooltip("텔레포트가 켜졌을 때 바깥 벽 선의 밝기. 1보다 크면 Bloom이 번져서 빛난다.")]
    [Range(1f, 4f)] public float portalWallIntensity = 1.5f;

    [Header("텔레포트 문 (벽 선 빛)")]
    [Tooltip("하트가 끝 칸에 있을 때 빛나는 벽 선의 두께 (격자 선 두께 대비)")]
    [Range(1f, 6f)] public float gateThickness = 2.5f;

    [Tooltip("빛나는 벽 선의 밝기. 1보다 크면 Bloom이 은은하게 번진다.")]
    [Range(1f, 4f)] public float gateIntensity = 1.6f;

    [Tooltip("하트가 끝 칸에 오거나 떠날 때 벽 선이 빛나고 꺼지는 시간 (초)")]
    [Min(0.01f)] public float gateFadeTime = 0.15f;

    [Header("넘어갈 때 번쩍임")]
    [Tooltip("넘어가는 순간 양쪽 벽 선이 번쩍였다가 원래대로 돌아오는 시간 (초)")]
    [Min(0.01f)] public float pulseTime = 0.2f;

    [Tooltip("번쩍일 때 밝기 배율")]
    [Range(1f, 4f)] public float pulseBrightness = 2f;

    [Tooltip("번쩍일 때 두께 배율")]
    [Range(1f, 3f)] public float pulseThickness = 1.6f;

    [Tooltip("벽 선 빛을 그리는 순서. 격자 선(-10) 위, 하트(10) 아래")]
    public int glowSortingOrder = -9;

    GameGrid grid;
    PlayerMover mover;
    Material glowMaterial;
    readonly List<SpriteRenderer> glows = new List<SpriteRenderer>();
    readonly Dictionary<(bool vertical, int line), float> pulseStarts = new Dictionary<(bool, int), float>();   // 줄마다 번쩍이기 시작한 시간
    MaterialPropertyBlock gateProperties;
    readonly Dictionary<(bool vertical, int line), float> glowLevels = new Dictionary<(bool, int), float>();   // 줄마다 빛 세기 (0~1)
    readonly List<(bool vertical, int line)> glowKeys = new List<(bool, int)>();
    static readonly int ColorProperty = Shader.PropertyToID("_Color");
    readonly List<SpriteRenderer> lines = new List<SpriteRenderer>();
    readonly List<SpriteRenderer> wallGlows = new List<SpriteRenderer>();   // 텔레포트 중 빛나는 벽 토막
    int usedWallGlows;
    readonly List<SpriteRenderer> warningStrips = new List<SpriteRenderer>();
    readonly List<ShrinkWarning> warnings = new List<ShrinkWarning>();
    int usedLines;

    void Awake()
    {
        grid = GetComponent<GameGrid>();
        mover = FindAnyObjectByType<PlayerMover>();
        glowMaterial = SpriteMaterial.Create("PortalGate");
        gateProperties = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        if (mover != null) mover.Teleported += OnTeleported;
    }

    void OnDisable()
    {
        if (mover != null) mover.Teleported -= OnTeleported;
    }

    void OnDestroy()
    {
        if (glowMaterial != null) Destroy(glowMaterial);
    }

    void LateUpdate()
    {
        var b = grid.CurrentBounds;
        var portal = grid.CurrentPortal;

        // 무적 중 화면 반전이면 색을 뒤집는다.
        // (텔레포트 색은 반전하지 않는다. 반전하면 빨강 계열이 되어 하트와 헷갈리기 때문)
        Color color = ScreenInvert.Apply(lineColor);
        usedLines = 0;
        usedWallGlows = 0;

        // 세로 선: 왼쪽 벽, 안쪽 칸 경계들, 오른쪽 벽
        float verticalLength = b.Height * grid.cellSize + lineWidth;
        float midY = grid.EdgeToWorldY((b.minY + b.maxY) * 0.5f);
        foreach (float x in Boundaries(b.minX, b.maxX))
        {
            var position = new Vector2(grid.EdgeToWorldX(x), midY);
            bool wall = x == b.minX || x == b.maxX;
            if (wall) PlaceWall(position, Vector2.up, verticalLength, color, portal.blend);
            else PlaceLine(position, new Vector2(lineWidth, verticalLength), color);
        }

        // 가로 선: 아래 벽, 안쪽 칸 경계들, 위 벽
        float horizontalLength = b.Width * grid.cellSize + lineWidth;
        float midX = grid.EdgeToWorldX((b.minX + b.maxX) * 0.5f);
        foreach (float y in Boundaries(b.minY, b.maxY))
        {
            var position = new Vector2(midX, grid.EdgeToWorldY(y));
            bool wall = y == b.minY || y == b.maxY;
            if (wall) PlaceWall(position, Vector2.right, horizontalLength, color, portal.blend);
            else PlaceLine(position, new Vector2(horizontalLength, lineWidth), color);
        }

        // 쓰지 않은 선은 숨긴다.
        for (int i = usedLines; i < lines.Count; i++) lines[i].enabled = false;
        for (int i = usedWallGlows; i < wallGlows.Count; i++) wallGlows[i].enabled = false;

        DrawWarnings();
        DrawPortalGates(b, portal);
    }

    /// <summary>min과 max 위치(벽)와 그 사이의 칸 경계(정수 위치)를 차례로 돌려준다.</summary>
    static IEnumerable<float> Boundaries(float min, float max)
    {
        yield return min;
        for (int k = Mathf.FloorToInt(min) + 1; k < max - 0.01f; k++)
        {
            if (k > min + 0.01f) yield return k;   // 벽과 거의 겹치는 경계는 벽 하나로 친다.
        }
        yield return max;
    }

    /// <summary>
    /// 바깥 벽 선 하나를 놓는다. 빛나는 굵은 선이 벽 가운데에서 양쪽 끝으로 spread(0~1)만큼 퍼져 있고,
    /// 나머지 양쪽 끝 부분은 원래 선이다. (빛나는 토막 하나와 원래 선 두 토막으로 나눠 그린다)
    /// length는 꼭짓점을 덮도록 선 두께만큼 더한 길이다.
    /// </summary>
    void PlaceWall(Vector2 center, Vector2 along, float length, Color color, float spread)
    {
        Vector2 across = new Vector2(Mathf.Abs(along.y), Mathf.Abs(along.x));   // 선과 수직 방향
        float wallLength = length - lineWidth;            // 꼭짓점에서 꼭짓점까지
        float glowWidth = lineWidth * portalWallThickness;
        spread = Mathf.Clamp01(spread);

        // 빛나는 토막: 다 퍼지면 꼭짓점을 굵은 두께만큼 덮어서 네 변이 빈틈없이 이어진다.
        float glowLength = wallLength * spread + glowWidth * spread;
        if (glowLength > 0.0001f)
        {
            PlaceWallGlow(center, along * glowLength + across * glowWidth);
        }

        // 원래 선: 빛이 아직 닿지 않은 양쪽 끝 부분
        float rest = (length - wallLength * spread) * 0.5f;
        if (rest > 0.0001f)
        {
            float offset = wallLength * spread * 0.5f + rest * 0.5f;   // 가운데에서 남은 토막 가운데까지
            PlaceLine(center + along * offset, along * rest + across * lineWidth, color);
            PlaceLine(center - along * offset, along * rest + across * lineWidth, color);
        }
    }

    /// <summary>텔레포트 중 빛나는 굵은 벽 토막 하나를 놓는다.</summary>
    void PlaceWallGlow(Vector2 position, Vector2 size)
    {
        if (usedWallGlows >= wallGlows.Count)
        {
            var glow = ShapeSprites.CreateSquare($"PortalWall_{wallGlows.Count}", transform, portalWallColor, sortingOrder);
            if (glowMaterial != null) glow.sharedMaterial = glowMaterial;
            wallGlows.Add(glow);
        }

        var renderer = wallGlows[usedWallGlows++];
        renderer.enabled = true;
        renderer.color = ScreenInvert.Apply(portalWallColor);   // 화면 반전 중에는 검게 보인다.
        renderer.sortingOrder = sortingOrder;
        renderer.transform.position = position;
        renderer.transform.localScale = new Vector3(size.x, size.y, 1f);

        // 같은 머티리얼을 쓰면서 1보다 밝게 그려서 Bloom이 번지게 한다.
        gateProperties.Clear();
        gateProperties.SetColor(ColorProperty, SpriteMaterial.Glow(Color.white, portalWallIntensity));
        renderer.SetPropertyBlock(gateProperties);
    }

    void PlaceLine(Vector2 position, Vector2 size, Color color)
    {
        if (usedLines >= lines.Count)
        {
            lines.Add(ShapeSprites.CreateSquare($"Line_{lines.Count}", transform, lineColor, sortingOrder));
        }

        var line = lines[usedLines++];
        line.enabled = true;
        line.color = color;
        line.sortingOrder = sortingOrder;
        line.transform.position = position;
        line.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    /// <summary>줄어들 칸에 반투명 띠를 그린다. 벽이 도착할 때가 가까울수록 진해진다.</summary>
    void DrawWarnings()
    {
        grid.GetShrinkWarnings(warnings);

        for (int i = 0; i < warnings.Count; i++)
        {
            if (i >= warningStrips.Count)
            {
                warningStrips.Add(ShapeSprites.CreateSquare($"ShrinkWarning_{i}", transform, lineColor, warningSortingOrder));
            }

            var strip = warningStrips[i];
            var cells = warnings[i].cells;
            var c = ScreenInvert.Apply(lineColor);
            c.a = warningAlpha * Mathf.Lerp(0.5f, 1f, warnings[i].progress);

            strip.enabled = true;
            strip.color = c;
            strip.sortingOrder = warningSortingOrder;
            strip.transform.position = new Vector2(grid.EdgeToWorldX(cells.center.x), grid.EdgeToWorldY(cells.center.y));
            strip.transform.localScale = new Vector3(cells.width * grid.cellSize, cells.height * grid.cellSize, 1f);
        }

        for (int i = warnings.Count; i < warningStrips.Count; i++) warningStrips[i].enabled = false;
    }

    /// <summary>
    /// 하트가 끝 칸에 있으면, 그 줄의 양쪽 벽에서 그 칸 길이만큼의 벽 선을 빛나게 한다. (텔레포트로 이어진 방향만)
    /// 하트가 오고 떠날 때 부드럽게 켜지고 꺼진다.
    /// </summary>
    void DrawPortalGates(GridBounds b, PortalState portal)
    {
        // 이번 프레임에 빛나야 하는 줄을 정한다.
        bool rowGate = false, columnGate = false;
        Vector2Int cell = mover != null ? mover.Cell : Vector2Int.zero;
        if (mover != null && portal.horizontal) rowGate = cell.x == b.FirstColumn || cell.x == b.LastColumn;
        if (mover != null && portal.vertical) columnGate = cell.y == b.FirstRow || cell.y == b.LastRow;

        // 줄마다 빛 세기를 목표 쪽으로 부드럽게 옮긴다. (화면 연출이라 실제 시간으로 잰다)
        float step = Time.unscaledDeltaTime / gateFadeTime;
        if (rowGate && !glowLevels.ContainsKey((false, cell.y))) glowLevels[(false, cell.y)] = 0f;
        if (columnGate && !glowLevels.ContainsKey((true, cell.x))) glowLevels[(true, cell.x)] = 0f;

        glowKeys.Clear();
        glowKeys.AddRange(glowLevels.Keys);
        foreach (var key in glowKeys)
        {
            bool wanted = key.vertical ? columnGate && key.line == cell.x : rowGate && key.line == cell.y;
            float level = Mathf.MoveTowards(glowLevels[key], wanted ? 1f : 0f, step);
            if (level <= 0f && !wanted) glowLevels.Remove(key);
            else glowLevels[key] = level;
        }

        float now = Time.unscaledTime;

        // 줄마다 양쪽 벽 위에 그 칸 길이만큼의 굵은 선을 놓는다.
        float thickness = lineWidth * gateThickness;
        float length = grid.cellSize;
        int used = 0;
        foreach (var pair in glowLevels)
        {
            float alpha = Mathf.SmoothStep(0f, 1f, pair.Value);
            float pulse = PulseAmount(pair.Key, now);   // 넘어간 직후면 0보다 크다.
            float width = thickness * Mathf.Lerp(1f, pulseThickness, pulse);
            float brightness = gateIntensity * Mathf.Lerp(1f, pulseBrightness, pulse);

            // 모서리 칸이면 문을 꼭짓점 너머까지 조금 늘려서, 꼭짓점에 겹친 벽 선(흰색)이 문 끝에 남아 보이지 않게 덮는다.
            int line = pair.Key.line;
            float corner = Mathf.Max(width, lineWidth * portalWallThickness) * 0.5f;
            bool lowCorner = (pair.Key.vertical ? line - b.minX : line - b.minY) < 0.01f;            // 칸의 왼쪽(아래쪽) 끝이 꼭짓점
            bool highCorner = (pair.Key.vertical ? b.maxX - (line + 1) : b.maxY - (line + 1)) < 0.01f; // 칸의 오른쪽(위쪽) 끝이 꼭짓점
            float lowExtra = lowCorner ? corner : 0f;
            float highExtra = highCorner ? corner : 0f;
            float gateLength = length + lowExtra + highExtra;
            float shift = (highExtra - lowExtra) * 0.5f;   // 한쪽만 늘어나면 그만큼 가운데가 옮겨진다.

            if (pair.Key.vertical)
            {
                float x = grid.CellToWorld(line, 0).x + shift;
                PlaceGate(ref used, new Vector2(x, grid.EdgeToWorldY(b.minY)), new Vector2(gateLength, width), alpha, brightness);
                PlaceGate(ref used, new Vector2(x, grid.EdgeToWorldY(b.maxY)), new Vector2(gateLength, width), alpha, brightness);
            }
            else
            {
                float y = grid.CellToWorld(0, line).y + shift;
                PlaceGate(ref used, new Vector2(grid.EdgeToWorldX(b.minX), y), new Vector2(width, gateLength), alpha, brightness);
                PlaceGate(ref used, new Vector2(grid.EdgeToWorldX(b.maxX), y), new Vector2(width, gateLength), alpha, brightness);
            }
        }

        for (int i = used; i < glows.Count; i++) glows[i].enabled = false;
    }

    /// <summary>빛나는 벽 선 하나를 놓는다. brightness가 1보다 크면 Bloom이 번진다.</summary>
    void PlaceGate(ref int used, Vector2 position, Vector2 size, float alpha, float brightness)
    {
        if (used >= glows.Count)
        {
            var gate = ShapeSprites.CreateSquare($"PortalGate_{glows.Count}", transform, grid.portalColor, glowSortingOrder);
            if (glowMaterial != null) gate.sharedMaterial = glowMaterial;
            glows.Add(gate);
        }

        var renderer = glows[used++];
        var color = grid.portalColor;
        color.a = alpha;

        renderer.enabled = alpha > 0f;
        renderer.color = color;
        renderer.sortingOrder = glowSortingOrder;
        renderer.transform.position = position;
        renderer.transform.localScale = new Vector3(size.x, size.y, 1f);

        // 같은 머티리얼을 쓰면서 이 선만 밝기를 다르게 한다.
        gateProperties.Clear();
        gateProperties.SetColor(ColorProperty, SpriteMaterial.Glow(Color.white, brightness));
        renderer.SetPropertyBlock(gateProperties);
    }

    /// <summary>넘어가는 순간, 넘어간 줄의 양쪽 벽 선을 번쩍이게 한다.</summary>
    void OnTeleported(Vector3 offset)
    {
        Vector2Int cell = mover.Cell;   // 이미 반대편 끝 칸으로 바뀌어 있다.
        bool horizontalMove = Mathf.Abs(offset.x) >= Mathf.Abs(offset.y);
        var key = horizontalMove ? (false, cell.y) : (true, cell.x);

        pulseStarts[key] = Time.unscaledTime;
        if (!glowLevels.ContainsKey(key)) glowLevels[key] = 1f;   // 아직 빛나지 않던 줄이면 바로 켠다.
    }

    /// <summary>
    /// 그 줄이 번쩍이는 정도 (0~1). 넘어간 순간 1이고 pulseTime 동안 부드럽게 0으로 돌아온다.
    /// (화면 연출이라 실제 시간으로 잰다)
    /// </summary>
    float PulseAmount((bool vertical, int line) key, float now)
    {
        if (!pulseStarts.TryGetValue(key, out float start)) return 0f;

        float k = (now - start) / pulseTime;
        if (k >= 1f)
        {
            pulseStarts.Remove(key);
            return 0f;
        }

        float fade = 1f - k;
        return fade * fade;
    }
}
