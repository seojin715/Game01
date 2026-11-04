using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>직사각형 포탄의 모양과 움직임 설정. LevelPlayer 인스펙터에서 조절한다.</summary>
[Serializable]
public class BlockSettings
{
    [Header("움직임")]
    [Tooltip("날아가는 속도 기본값 (칸/초). 레벨 데이터에 speed가 있으면 그 값을 쓴다.")]
    [Min(0.1f)] public float speed = 6f;

    [Header("크기 기본값 (칸, 레벨 데이터에 없을 때)")]
    [Tooltip("날아가는 방향의 길이. 위에서 날아오면 세로 길이다.")]
    [Min(0.1f)] public float length = 3f;

    [Tooltip("날아가는 방향과 수직인 두께. 위에서 날아오면 가로 길이다. 1이면 줄 하나를 꼭 채운다.")]
    [Min(0.1f)] public float thickness = 1f;

    [Header("모양")]
    [Tooltip("보이는 모양을 칸 경계보다 이만큼 안쪽으로 줄인다 (칸 대비, 한쪽). 격자 선이 보이게 한다.")]
    [Range(0f, 0.3f)] public float inset = 0.06f;

    [Tooltip("모서리를 둥글게 깎는 반지름 (칸 대비). 0이면 각진 직사각형이다.")]
    [Range(0f, 0.5f)] public float cornerRadius = 0.12f;

    [Header("포탈")]
    [Tooltip("포탈이 생기는 곳: 날아오는 쪽 화면 끝에서 안쪽으로 이만큼 (칸 대비). 격자에 너무 가까우면 격자 바로 바깥 반 칸에 생긴다.")]
    [Min(0f)] public float portalScreenMargin = 0.5f;

    [Tooltip("포탈이 직사각형 두께보다 양쪽으로 더 긴 정도 (칸 대비, 한쪽)")]
    [Min(0f)] public float portalExtra = 0.15f;

    [Tooltip("포탈 선의 굵기 (칸 대비)")]
    [Range(0.02f, 0.5f)] public float portalLineWidth = 0.1f;

    [Tooltip("점에서 양쪽으로 펼쳐지는 시간 (초). 다 펼쳐진 순간 직사각형이 나오기 시작한다.")]
    [Min(0.01f)] public float portalOpenTime = 0.35f;

    [Tooltip("직사각형이 다 나온 뒤 점으로 줄어들며 닫히는 시간 (초)")]
    [Min(0.01f)] public float portalCloseTime = 0.25f;

    [Tooltip("포탈 선의 밝기. 1보다 크면 Bloom이 번져서 빛난다.")]
    [Range(1f, 6f)] public float portalGlowIntensity = 2.5f;

    [Tooltip("포탈에서 직사각형이 나가는 쪽으로 번지는 빛의 길이 (칸 대비)")]
    [Min(0f)] public float glowLength = 1.2f;

    [Tooltip("번지는 빛의 진하기 (0~1)")]
    [Range(0f, 1f)] public float glowAlpha = 0.4f;

    [Header("밀기")]
    [Tooltip("밀 때 하트 중심과 직사각형 앞면 사이를 이만큼 띄운다 (칸 대비). 하트가 직사각형에 겹쳐 보이지 않게 한다.")]
    [Range(0f, 1f)] public float pushGap = 0.45f;

    [Header("닿은 표시")]
    [Tooltip("하트가 닿으면 직사각형 겉으로 이만큼 더 크게 반투명한 테두리가 떠오른다 (칸 대비, 한쪽)")]
    [Range(0f, 0.5f)] public float touchPadding = 0.12f;

    [Tooltip("닿은 표시의 진하기 (0~1)")]
    [Range(0f, 1f)] public float touchAlpha = 0.35f;

    [Tooltip("닿은 표시가 떠오르는 시간 (초)")]
    [Min(0.01f)] public float touchFadeIn = 0.06f;

    [Tooltip("떨어진 뒤 닿은 표시가 사라지는 시간 (초)")]
    [Min(0.01f)] public float touchFadeOut = 0.3f;

    [Tooltip("옆에서 들어가려다 막혔을 때 닿은 표시를 유지하는 시간 (초)")]
    [Min(0f)] public float bumpHoldTime = 0.12f;

    [Header("겹친 포탄")]
    [Tooltip("다른 포탄(일반 포탄, 유도탄, 부메랑, 분열탄)이 직사각형과 겹치면 이 색으로 그려서 직사각형 위에서도 보이게 한다.")]
    public Color overlapProjectileColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Header("소리")]
    [Tooltip("직사각형 앞면이 격자에 닿는 순간(time) 나는 소리")]
    public ObstacleSound sound = new ObstacleSound();

    [Header("그리는 순서")]
    [Tooltip("하트(10)보다 높게 둬서 끼었을 때 하트 위로 지나간다.")]
    public int sortingOrder = 15;

    [Tooltip("포탈 선. 직사각형보다 높게 둬서 직사각형이 포탈에서 나오는 것처럼 보이게 한다.")]
    public int portalSortingOrder = 16;

    [Tooltip("포탈에서 번지는 빛. 직사각형보다 낮게")]
    public int glowSortingOrder = 14;
}

/// <summary>
/// 직사각형 포탄 (block).
/// 발사기 없이, side 쪽 화면 끝 가까이에 '-' 모양 포탈이 점에서 양쪽으로 펼쳐지며 열리고,
/// 그 포탈에서 나와 line번 줄을 따라 격자를 가로질러 반대편 화면 밖으로 나간다. 포탈 뒤쪽 부분은 보이지 않는다.
/// 포탈은 직사각형이 나가는 쪽으로 빛이 번지고, 직사각형이 다 나오면 점으로 줄어들며 닫힌다.
/// time에 앞면이 격자(그때의 벽)에 닿는다. 길이(날아가는 방향)와 두께(수직 방향)는 칸 단위로 정한다.
///
/// - 부딪혀도 피해는 없고, 하트는 날아가는 방향으로 밀려난다.
/// - 직사각형이 차지한 칸은 막힌 칸이 되어 옆에서 들어갈 수 없다.
/// - 밀려날 칸이 없으면(벽, 바닥 포탄이 막은 칸, 다른 직사각형) 끼어서 목숨이 하나 줄고, 직사각형은 하트를 지나간다.
/// - 하트가 닿으면(밀리는 중이거나 옆에서 들어가려다 막히면) 직사각형 겉으로 반투명한 테두리가 부드럽게 떠오른다.
/// </summary>
public class BlockObstacle : LevelObstacle
{
    const float Epsilon = 0.01f;   // 칸에 이만큼은 걸쳐야 차지한 것으로 본다 (칸 단위)

    readonly BlockSettings settings;
    readonly bool horizontal;      // 왼쪽·오른쪽에서 날아오면 가로로 움직인다
    readonly float sign;           // 날아가는 방향 (칸 좌표가 커지는 쪽이면 +1)
    readonly float edgeCell;       // time의 벽 위치 (칸 단위, 날아가는 축)
    readonly float speedCells;     // 칸/초
    readonly float length;         // 칸
    readonly float acrossMin;      // 수직 방향 범위 (칸 단위)
    readonly float acrossMax;
    readonly float portalCell;     // 포탈 위치 (칸 단위, 날아가는 축)
    readonly float emergeTime;     // 앞면이 포탈에서 나오기 시작하는 노래 시간
    readonly float outTime;        // 뒷면까지 포탈에서 다 나온 노래 시간
    readonly Quaternion glowRotation;

    GameObject root;
    SpriteRenderer body;
    SpriteRenderer touch;          // 하트가 닿았을 때 겉으로 떠오르는 반투명 테두리
    SpriteRenderer portalLine;
    SpriteRenderer portalGlow;
    Material portalMaterial;       // 포탈 선을 1보다 밝게 그리는 머티리얼
    bool crushed;                  // 하트가 끼어서 지나가기로 했는지 (그 뒤로는 하트를 밀지 않는다)
    bool touchingNow;              // 이번 프레임에 하트가 닿아 있는지
    float touchLevel;              // 닿은 표시가 떠오른 정도 (0~1)
    float lastBumpTime = float.NegativeInfinity;   // 옆에서 막힌 마지막 노래 시간
    float lastPushTime = float.NegativeInfinity;   // 하트를 민 마지막 노래 시간
    float currentSongTime;
    float bodyAlong;               // 지금 그린 직사각형의 날아가는 방향 길이 (월드)
    float bodyAcross;              // 수직 방향 길이 (월드)
    bool bodyClipped;              // 뒤쪽이 포탈에 잘려 있는지 (그동안 뒤 모서리는 각지게)
    Rect bodyRect;                 // 지금 그린 직사각형의 영역 (월드)
    readonly List<Vector2Int> blockedCells = new List<Vector2Int>();   // 지금 막아 둔 칸
    readonly List<Vector2Int> coveredCells = new List<Vector2Int>();

    public BlockObstacle(ObstacleData data, ObstacleContext context, BlockSettings settings, GridSide side)
        : base(data, context)
    {
        this.settings = settings;
        var grid = context.grid;

        horizontal = GridSideUtil.IsHorizontal(side);
        sign = side == GridSide.Left || side == GridSide.Bottom ? 1f : -1f;
        edgeCell = grid.BoundsAt(data.time).Edge(side);
        speedCells = data.speed > 0f ? data.speed : settings.speed;
        length = data.length > 0f ? data.length : settings.length;

        float thickness = data.thickness > 0f ? data.thickness : settings.thickness;
        float laneCenter = data.line + 0.5f;
        acrossMin = laneCenter - thickness * 0.5f;
        acrossMax = laneCenter + thickness * 0.5f;

        // 포탈은 날아오는 쪽 화면 끝에서 조금 안쪽에 생긴다. (격자보다 바깥이어야 한다)
        float cell = context.CellSize;
        Vector2 inward = GridSideUtil.InwardDirection(side);
        glowRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(inward.y, inward.x) * Mathf.Rad2Deg);
        Vector2 edgePoint = EdgePoint(grid, side, data.line, data.time);
        float behind = Vector2.Distance(edgePoint, OutsideView(context.view, edgePoint, -inward, 0f)) / cell;
        float portalDistance = Mathf.Max(0.5f, behind - settings.portalScreenMargin);
        portalCell = edgeCell - sign * portalDistance;

        // 앞면이 포탈에서 나오기 시작해서, 뒷면이 반대편 화면 밖으로 나가면 끝난다.
        float ahead = Vector2.Distance(edgePoint, OutsideView(context.view, edgePoint, inward, 0f)) / cell;
        emergeTime = data.time - portalDistance / speedCells;
        outTime = emergeTime + length / speedCells;

        AppearTime = emergeTime - settings.portalOpenTime;
        EndTime = Mathf.Max(data.time + (ahead + length) / speedCells, outTime + settings.portalCloseTime);
    }

    protected override void OnSpawn()
    {
        root = new GameObject($"Block {Data.side} {Data.line} @{Data.time:0.##}");
        root.transform.SetParent(Context.container, false);
        portalGlow = ShapeSprites.CreateRenderer("PortalGlow", root.transform, ShapeSprites.GlowFan, Context.Color, settings.glowSortingOrder);
        touch = ShapeSprites.CreateRenderer("BlockTouch", root.transform, ShapeSprites.RoundedRect(true), Context.Color, settings.sortingOrder - 1);
        touch.enabled = false;
        body = ShapeSprites.CreateRenderer("Block", root.transform, ShapeSprites.RoundedRect(true), Context.Color, settings.sortingOrder);

        // 모서리가 둥근 9분할 그림을 늘려 쓴다. 로컬 x가 날아가는 방향이 되도록 돌려 둔다.
        body.drawMode = SpriteDrawMode.Sliced;
        touch.drawMode = SpriteDrawMode.Sliced;
        body.transform.rotation = glowRotation;
        touch.transform.rotation = glowRotation;
        portalLine = ShapeSprites.CreateRenderer("Portal", root.transform, ShapeSprites.Square, Context.Color, settings.portalSortingOrder);

        // 렌더러 색은 1을 넘을 수 없으므로, 포탈 선의 밝기는 머티리얼 색에 넣는다.
        portalMaterial = SpriteMaterial.Create("BlockPortal");
        if (portalMaterial != null)
        {
            portalMaterial.SetColor(ColorProperty, SpriteMaterial.Glow(Color.white, settings.portalGlowIntensity));
            portalLine.sharedMaterial = portalMaterial;
        }

        portalGlow.transform.rotation = glowRotation;   // 빛이 직사각형이 나가는 쪽으로 번진다.
        crushed = false;
        touchLevel = 0f;
        lastBumpTime = float.NegativeInfinity;
        lastPushTime = float.NegativeInfinity;
        if (Context.mover != null) Context.mover.Bumped += OnHeartBumped;
    }

    protected override void OnDespawn()
    {
        SetBlockedCells(null);   // 지워질 때 막아 둔 칸을 꼭 풀어 준다.
        if (Context.mover != null) Context.mover.Bumped -= OnHeartBumped;
        if (portalMaterial != null) UnityEngine.Object.Destroy(portalMaterial);
        portalMaterial = null;
        if (root != null) UnityEngine.Object.Destroy(root);
        root = null;
    }

    static readonly int ColorProperty = Shader.PropertyToID("_Color");

    public override void Tick(float songTime, float previousSongTime)
    {
        if (root == null) return;

        settings.sound.PlayAt(Data.time, songTime, previousSongTime);

        float front = edgeCell + sign * speedCells * (songTime - Data.time);
        float back = front - sign * length;

        currentSongTime = songTime;
        // 밀리는 하트는 한 칸 밀릴 때마다 앞으로 미끄러져 잠깐 떨어지므로,
        // 직사각형이 한 칸을 지나는 동안은 계속 닿아 있는 것으로 본다. (가만히 있어도 표시가 깜빡이지 않게)
        float pushHold = 1.2f / speedCells;
        touchingNow = (songTime >= lastBumpTime && songTime - lastBumpTime < settings.bumpHoldTime)
            || (songTime >= lastPushTime && songTime - lastPushTime < pushHold);

        UpdatePortal(songTime);
        UpdateBody(front, back);
        UpdateBlockedCells(front, back);
        PushHeart(front, back);
        UpdateTouch(songTime - previousSongTime);
    }

    /// <summary>하트가 이 직사각형이 막은 칸으로 들어가려다 막혔으면 닿은 것으로 본다.</summary>
    void OnHeartBumped(Vector2Int cell)
    {
        if (coveredCells.Contains(cell)) lastBumpTime = currentSongTime;
    }

    /// <summary>닿은 표시: 닿아 있으면 빠르게 떠오르고, 떨어지면 천천히 사라진다. 직사각형보다 조금 크게 겉으로 보인다.</summary>
    void UpdateTouch(float dt)
    {
        dt = Mathf.Max(0f, dt);   // 미리보기에서 시간을 뒤로 옮겼을 때
        touchLevel = touchingNow
            ? Mathf.Min(1f, touchLevel + dt / settings.touchFadeIn)
            : Mathf.Max(0f, touchLevel - dt / settings.touchFadeOut);

        bool visible = body.enabled && touchLevel > 0f;
        touch.enabled = visible;
        if (!visible) return;

        // 직사각형보다 한쪽마다 pad만큼 크고, 모서리도 그만큼 더 둥글게 해서 테두리 두께가 고르게 보이게 한다.
        float pad = settings.touchPadding * Context.CellSize;
        touch.transform.position = body.transform.position;
        SetRoundedSize(touch, bodyAlong + pad * 2f, bodyAcross + pad * 2f, settings.cornerRadius * Context.CellSize + pad, bodyClipped);
        touch.color = WithAlpha(Context.Color, settings.touchAlpha * Mathf.SmoothStep(0f, 1f, touchLevel));
    }

    /// <summary>
    /// 포탈: 점에서 양쪽으로 펼쳐지며 열리고(다 펼쳐진 순간 직사각형이 나오기 시작), 직사각형이 다 나오면 점으로 줄어들며 닫힌다.
    /// 열려 있는 만큼 직사각형이 나가는 쪽으로 빛이 번진다.
    /// </summary>
    void UpdatePortal(float t)
    {
        var grid = Context.grid;
        float cell = grid.cellSize;

        float open;    // 0 = 점, 1 = 다 펼쳐짐
        float alpha;   // 점이 나타나고 사라지는 정도
        if (t < emergeTime)
        {
            float k = Mathf.Clamp01((t - AppearTime) / settings.portalOpenTime);
            open = EaseOutCubic(k);
            alpha = Mathf.Clamp01(k * 5f);   // 처음 잠깐 점이 나타난다.
        }
        else if (t < outTime)
        {
            open = 1f;
            alpha = 1f;
        }
        else
        {
            float k = Mathf.Clamp01((t - outTime) / settings.portalCloseTime);
            open = 1f - EaseInOut(k);
            alpha = Mathf.Clamp01((1f - k) * 5f);   // 마지막에 점이 사라진다.
        }

        bool visible = alpha > 0f;
        portalLine.enabled = visible;
        portalGlow.enabled = visible && open > 0f;
        if (!visible) return;

        float lineWidth = settings.portalLineWidth * cell;
        float fullWidth = (acrossMax - acrossMin + settings.portalExtra * 2f) * cell;
        float width = Mathf.Max(lineWidth, fullWidth * open);   // 점(선 굵기만 한 정사각형)에서 펼쳐진다.
        float acrossCenter = (acrossMin + acrossMax) * 0.5f;

        Vector2 center = horizontal
            ? new Vector2(grid.EdgeToWorldX(portalCell), grid.EdgeToWorldY(acrossCenter))
            : new Vector2(grid.EdgeToWorldX(acrossCenter), grid.EdgeToWorldY(portalCell));

        portalLine.transform.position = center;
        portalLine.transform.localScale = horizontal ? new Vector3(lineWidth, width, 1f) : new Vector3(width, lineWidth, 1f);
        portalLine.color = WithAlpha(Context.Color, alpha);

        // 빛은 포탈 선에서 나가는 쪽으로만 번진다. (그림의 기준점이 왼쪽 가운데라 돌리면 나가는 쪽을 향한다)
        portalGlow.transform.position = center;
        portalGlow.transform.localScale = new Vector3(settings.glowLength * cell, width, 1f);
        portalGlow.color = WithAlpha(Context.Color, settings.glowAlpha * open * alpha);
    }

    /// <summary>직사각형을 그 순간의 자리에 그린다. 포탈 뒤쪽(아직 나오지 않은 부분)은 그리지 않는다.</summary>
    void UpdateBody(float front, float back)
    {
        var grid = Context.grid;
        float inset = settings.inset;

        // 나가는 방향을 +로 본 좌표에서, 포탈보다 앞에 나온 부분만 남긴다.
        float frontU = front * sign;
        float backU = Mathf.Max(back * sign, portalCell * sign);
        if (frontU - backU <= inset * 2f)
        {
            body.enabled = false;
            return;
        }

        float a = (backU + inset) * sign;
        float b = (frontU - inset) * sign;
        float alongMin = Mathf.Min(a, b);
        float alongMax = Mathf.Max(a, b);
        float acrossLow = acrossMin + inset;
        float acrossHigh = acrossMax - inset;

        Vector2 min = horizontal
            ? new Vector2(grid.EdgeToWorldX(alongMin), grid.EdgeToWorldY(acrossLow))
            : new Vector2(grid.EdgeToWorldX(acrossLow), grid.EdgeToWorldY(alongMin));
        Vector2 max = horizontal
            ? new Vector2(grid.EdgeToWorldX(alongMax), grid.EdgeToWorldY(acrossHigh))
            : new Vector2(grid.EdgeToWorldX(acrossHigh), grid.EdgeToWorldY(alongMax));

        float sizeX = Mathf.Max(0f, max.x - min.x);
        float sizeY = Mathf.Max(0f, max.y - min.y);
        bodyAlong = horizontal ? sizeX : sizeY;
        bodyAcross = horizontal ? sizeY : sizeX;
        bodyClipped = back * sign < portalCell * sign;   // 아직 포탈에서 다 나오지 않았다

        body.enabled = true;
        bodyRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        body.transform.position = (min + max) * 0.5f;
        SetRoundedSize(body, bodyAlong, bodyAcross, settings.cornerRadius * Context.CellSize, bodyClipped);
        body.color = Context.Color;
    }

    /// <summary>보이는 직사각형의 영역. 유도탄이 닿으면 터지고, 겹친 포탄은 회색으로 그린다.</summary>
    public override bool TryGetSolidRect(out Rect rect)
    {
        rect = bodyRect;
        return body != null && body.enabled;
    }

    /// <summary>
    /// 모서리가 둥근 사각형의 크기를 정한다. (along = 날아가는 방향, across = 수직 방향, 월드 단위)
    /// 그림은 둥근 반지름이 로컬 1단위라서, 반지름만큼 키우고(localScale) 크기는 그 비율로 나눠 넣는다.
    /// 반지름은 짧은 변의 절반을 넘지 않게 한다. clipped면 뒤쪽 모서리는 각지게 그린다.
    /// </summary>
    static void SetRoundedSize(SpriteRenderer renderer, float along, float across, float radius, bool clipped)
    {
        radius = Mathf.Max(0.0001f, Mathf.Min(radius, along * 0.5f, across * 0.5f));
        renderer.sprite = ShapeSprites.RoundedRect(!clipped);
        renderer.transform.localScale = new Vector3(radius, radius, 1f);
        renderer.size = new Vector2(along / radius, across / radius);
    }

    /// <summary>직사각형이 걸친 칸을 막는다. 옆에서 하트가 들어올 수 없다.</summary>
    void UpdateBlockedCells(float front, float back)
    {
        coveredCells.Clear();
        int alongFirst = Mathf.FloorToInt(Mathf.Min(front, back) + Epsilon);
        int alongLast = Mathf.CeilToInt(Mathf.Max(front, back) - Epsilon) - 1;
        int acrossFirst = Mathf.FloorToInt(acrossMin + Epsilon);
        int acrossLast = Mathf.CeilToInt(acrossMax - Epsilon) - 1;

        for (int a = alongFirst; a <= alongLast; a++)
        {
            for (int c = acrossFirst; c <= acrossLast; c++)
            {
                coveredCells.Add(horizontal ? new Vector2Int(a, c) : new Vector2Int(c, a));
            }
        }
        SetBlockedCells(coveredCells);
    }

    /// <summary>막아 둔 칸을 cells로 바꾼다. 그대로인 칸은 건드리지 않는다. (null이면 모두 푼다)</summary>
    void SetBlockedCells(List<Vector2Int> cells)
    {
        var grid = Context.grid;
        for (int i = blockedCells.Count - 1; i >= 0; i--)
        {
            if (cells != null && cells.Contains(blockedCells[i])) continue;
            grid.Unblock(blockedCells[i]);
            blockedCells.RemoveAt(i);
        }

        if (cells == null) return;
        foreach (var cell in cells)
        {
            if (blockedCells.Contains(cell)) continue;
            grid.Block(cell);
            blockedCells.Add(cell);
        }
    }

    /// <summary>
    /// 하트가 이 직사각형의 줄에 있고 앞면이 닿으면, 날아가는 방향으로 밀어낸다.
    /// 밀려날 칸에 들어갈 수 없으면 끼어서 피해를 주고, 그 뒤로는 하트를 지나간다.
    /// </summary>
    void PushHeart(float front, float back)
    {
        var mover = Context.mover;
        if (crushed || mover == null || mover.IsWrapping) return;

        Vector2Int cell = mover.Cell;
        int across = horizontal ? cell.y : cell.x;
        if (across + 1 <= acrossMin + Epsilon || across >= acrossMax - Epsilon) return;   // 다른 줄

        // 하트 칸이 직사각형 뒤쪽이면(이미 지나갔으면) 밀지 않는다.
        int along = horizontal ? cell.x : cell.y;
        if ((along + 0.5f - back) * sign <= 0f) return;

        // 앞면(과 띄울 간격)이 하트 칸 가운데를 지나면 한 칸씩 밀어낸다.
        float reach = front + sign * settings.pushGap;
        int target = along;
        while ((reach - (target + 0.5f)) * sign > 0f)
        {
            int next = target + (int)sign;
            Vector2Int nextCell = horizontal ? new Vector2Int(next, across) : new Vector2Int(across, next);
            if (!Context.grid.CanEnter(nextCell))
            {
                // 더 밀려날 곳이 없다: 끼어서 목숨이 하나 줄고, 직사각형은 하트를 지나간다.
                crushed = true;
                touchingNow = true;
                Context.onPlayerHit?.Invoke(this);
                return;
            }
            target = next;
        }

        if (target != along)
        {
            mover.PushTo(horizontal ? new Vector2Int(target, across) : new Vector2Int(across, target));
            touchingNow = true;
            lastPushTime = currentSongTime;
        }

        // 하트가 직사각형에 겹쳐 보이지 않게, 앞면에서 pushGap만큼 앞에 붙잡아 둔다.
        KeepAheadOfFront(reach, back);
    }

    /// <summary>하트 중심이 앞면 + 간격보다 뒤에 있으면(직사각형 뒷면보다는 앞이면) 그 자리로 옮겨 둔다.</summary>
    void KeepAheadOfFront(float reach, float back)
    {
        var mover = Context.mover;
        var grid = Context.grid;
        Vector3 position = mover.transform.position;
        float cell = grid.cellSize;

        float heartAlong = horizontal
            ? (position.x - grid.EdgeToWorldX(0f)) / cell
            : (position.y - grid.EdgeToWorldY(0f)) / cell;

        if ((heartAlong - back) * sign <= 0f) return;    // 직사각형 뒤쪽
        if ((heartAlong - reach) * sign >= 0f)
        {
            // 앞면에 거의 붙어 있으면(조금 떨어진 정도) 닿은 것으로 본다.
            if ((heartAlong - reach) * sign < 0.05f) touchingNow = true;
            return;
        }

        touchingNow = true;
        lastPushTime = currentSongTime;

        if (horizontal) position.x = grid.EdgeToWorldX(reach);
        else position.y = grid.EdgeToWorldY(reach);
        mover.PushPosition(position);
    }
}
