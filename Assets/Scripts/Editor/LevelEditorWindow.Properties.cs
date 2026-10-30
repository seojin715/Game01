using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 레벨 에디터 오른쪽의 설정 칸.
/// 고른 장애물의 종류, 시간, 위치, 경고 시간 등을 바꾼다.
/// 위치(쪽과 줄, 바닥 포탄 칸)는 그 시간의 격자 그림에서 눌러서 고른다. 벽 이동이 있으면 그때의 격자 크기로 그린다.
/// 값이 0인 선택 항목은 게임에서 Level Player 인스펙터의 기본값을 쓴다.
/// </summary>
public partial class LevelEditorWindow
{
    const float PanelWidth = 300f;

    static readonly string[] SideKeys = { "left", "right", "top", "bottom" };
    static readonly string[] WallSideNames = { "왼쪽 벽", "오른쪽 벽", "위 벽", "아래 벽" };

    /// <summary>격자 범위 (칸 단위 벽 위치). 칸 x는 minX 이상 maxX 미만이다.</summary>
    struct GridRect
    {
        public int minX, maxX, minY, maxY;
        public int Columns => maxX - minX;
        public int RowCount => maxY - minY;
        public bool HasColumn(int x) => x >= minX && x < maxX;
        public bool HasRow(int y) => y >= minY && y < maxY;
    }

    /// <summary>씬의 Level Player와 Game Grid에서 읽은 기본값. 씬에 없으면 코드의 기본값을 쓴다.</summary>
    struct Defaults
    {
        public CannonSettings cannon;
        public LaserSettings laser;
        public FloorSettings floor;
        public HomingSettings homing;
        public SpikeSettings spike;
        public BoomerangSettings boomerang;
        public float wallWarnTime, wallMoveTime;
        public int columns, rows, maxColumns, maxRows;
        public float leadIn;   // 노래 시작 전 준비 시간 (SongClock)
    }

    enum PickMode { SideLine, Laser, Cell, Preview }

    Defaults defaults;
    readonly List<ObstacleData> walls = new List<ObstacleData>();   // 벽 이동 이벤트 (시간 순)

    // 설정 칸에 무엇을 그릴지는 Layout 때 정해 두고 그 프레임 동안 바꾸지 않는다.
    // (같은 프레임 안에서 그리는 칸 수가 달라지면 유니티 GUI 배치가 어긋나기 때문)
    int panelMode;    // 0: 없음, 1: 하나, 2: 여러 개
    int panelIndex;

    /// <summary>OnGUI마다 처음에 부른다. 벽 이벤트와 씬 기본값을 새로 모은다.</summary>
    void PrepareFrame()
    {
        walls.Clear();
        foreach (var o in obstacles) if (o.type == "wall") walls.Add(o);
        walls.Sort((a, b) => a.time.CompareTo(b.time));

        var player = FindAnyObjectByType<LevelPlayer>();
        var grid = FindAnyObjectByType<GameGrid>();
        var clock = FindAnyObjectByType<SongClock>();
        defaults = new Defaults
        {
            leadIn = clock != null ? clock.leadIn : 2f,
            cannon = player != null ? player.cannon : new CannonSettings(),
            laser = player != null ? player.laser : new LaserSettings(),
            floor = player != null ? player.floor : new FloorSettings(),
            homing = player != null ? player.homing : new HomingSettings(),
            spike = player != null ? player.spike : new SpikeSettings(),
            boomerang = player != null ? player.boomerang : new BoomerangSettings(),
            wallWarnTime = grid != null ? grid.defaultWallWarnTime : 1f,
            wallMoveTime = grid != null ? grid.wallMoveTime : 0.5f,
            columns = grid != null ? grid.columns : 5,
            rows = grid != null ? grid.rows : 5,
            maxColumns = grid != null ? grid.maxColumns : 7,
            maxRows = grid != null ? grid.maxRows : 7,
        };

        if (Event.current.type == EventType.Layout)
        {
            selection.RemoveAll(i => i < 0 || i >= obstacles.Count);
            panelMode = selection.Count == 0 ? 0 : selection.Count == 1 ? 1 : 2;
            panelIndex = selection.Count > 0 ? selection[0] : -1;
        }
    }

    Defaults SceneDefaults() => defaults;

    /// <summary>
    /// 노래 시간 time일 때 격자 범위. 그 시간까지 도착한 벽 이동을 차례로 적용한다.
    /// (게임의 GameGrid와 같은 규칙: 움직인 벽의 반대쪽은 그대로, 그 방향 칸 수가 size가 된다)
    /// </summary>
    GridRect BoundsAt(float time)
    {
        var b = new GridRect { minX = 0, maxX = defaults.columns, minY = 0, maxY = defaults.rows };
        foreach (var w in walls)
        {
            if (w.time > time + 0.0001f) break;
            if (!GridSideUtil.TryParse(w.side, out var side)) continue;

            int limit = GridSideUtil.IsHorizontal(side) ? defaults.maxColumns : defaults.maxRows;
            if (w.size < 1 || w.size > limit) continue;

            switch (side)
            {
                case GridSide.Right: b.maxX = b.minX + w.size; break;
                case GridSide.Left: b.minX = b.maxX - w.size; break;
                case GridSide.Top: b.maxY = b.minY + w.size; break;
                default: b.minY = b.maxY - w.size; break;
            }
        }
        return b;
    }

    /// <summary>그 시간의 격자에 장애물이 가리키는 줄이나 칸이 있는지. 없으면 게임에서 나오지 않는다.</summary>
    bool IsPlacementValid(ObstacleData o)
    {
        var b = BoundsAt(o.time);
        switch (o.type)
        {
            case "portal":
            case "dark":
                return true;

            case "floor":
                return o.cell != null && o.cell.Length >= 2 && b.HasColumn(o.cell[0]) && b.HasRow(o.cell[1]);

            case "laser":
                if (!LaserObstacle.TryParseAxis(o.axis, out bool row)) return false;
                return row ? b.HasRow(o.line) : b.HasColumn(o.line);

            case "wall":
            {
                if (!GridSideUtil.TryParse(o.side, out var side)) return false;
                int limit = GridSideUtil.IsHorizontal(side) ? defaults.maxColumns : defaults.maxRows;
                return o.size >= 1 && o.size <= limit;
            }

            default:
            {
                if (!GridSideUtil.TryParse(o.side, out var side)) return false;
                return GridSideUtil.IsHorizontal(side) ? b.HasRow(o.line) : b.HasColumn(o.line);
            }
        }
    }

    static int RowIndexOf(string type) => Array.FindIndex(Rows, r => r.type == type);

    // ───────────────────────── 설정 칸 ─────────────────────────

    void DrawPropertiesPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(PanelWidth));
        propertiesScroll = EditorGUILayout.BeginScrollView(propertiesScroll);

        if (panelMode == 1 && panelIndex >= 0 && panelIndex < obstacles.Count) DrawObstacle(panelIndex);
        else if (panelMode == 2) DrawManySelected();
        else DrawNothingSelected();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    void DrawNothingSelected()
    {
        EditorGUILayout.LabelField("고른 장애물 없음", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "· 타임라인 빈 곳을 더블클릭하면 그 줄의 장애물이 추가됩니다.\n" +
            "· 줄 이름 옆 + 버튼은 주황 커서 시간에 추가합니다.\n" +
            "· 장애물을 누르면 여기에서 시간과 위치를 바꿀 수 있습니다.\n" +
            "· 같은 종류를 추가하면 바로 앞 장애물의 값을 이어받습니다.",
            MessageType.Info);

        EditorGUILayout.LabelField("장애물 수", obstacles.Count.ToString());
        EditorGUILayout.LabelField("곡 길이", $"{SongLength:0.0}초");

        int invalid = obstacles.Count(o => !IsPlacementValid(o));
        EditorGUILayout.HelpBox(invalid > 0
                ? $"{invalid}개는 그 시간의 격자에 줄이나 칸이 없어서 게임에서 나오지 않습니다. (빨간 테두리, ! 표시)"
                : "모든 장애물이 그 시간의 격자 안에 있습니다.",
            invalid > 0 ? MessageType.Warning : MessageType.None);
    }

    void DrawManySelected()
    {
        EditorGUILayout.LabelField($"{selection.Count}개 고름", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("끌면 함께 옮겨집니다. Ctrl+C로 복사하고, 주황 커서를 옮긴 뒤 Ctrl+V로 같은 간격 그대로 붙여 넣을 수 있습니다.", MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("복사")) CopySelection();
        if (GUILayout.Button("삭제"))
        {
            DeleteSelection();
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();
    }

    void DrawObstacle(int index)
    {
        var o = obstacles[index];
        int row = RowIndexOf(o.type);

        EditorGUILayout.LabelField("장애물", EditorStyles.boldLabel);

        // 종류를 바꾸면 시간만 남기고 그 종류의 모양으로 새로 만든다.
        EditorGUI.BeginChangeCheck();
        int newRow = EditorGUILayout.Popup("종류", Mathf.Max(0, row), Rows.Select(r => r.label).ToArray());
        if (EditorGUI.EndChangeCheck() && newRow != row)
        {
            RecordUndo("종류 바꾸기");
            obstacles[index] = CreateObstacle(Rows[newRow].type, o.time);
            GUIUtility.ExitGUI();
        }

        DrawTimeField(o);
        GUILayout.Space(6f);

        switch (o.type)
        {
            case "cannon":
                DrawGridPicker(o, PickMode.SideLine);
                EditOptional("속도 (칸/초)", o.speed, defaults.cannon.speed, v => o.speed = v);
                break;

            case "homing":
                DrawGridPicker(o, PickMode.SideLine);
                EditOptional("처음 속도 (칸/초)", o.speed, defaults.homing.startSpeed, v => o.speed = v);
                break;

            case "boomerang":
                DrawGridPicker(o, PickMode.SideLine);
                EditOptional("발사 속도 (칸/초)", o.speed, defaults.boomerang.speed, v => o.speed = v);
                EditOptional("되돌아오는 칸", o.range, defaults.boomerang.defaultRange, v => o.range = v);
                break;

            case "spike":
                DrawGridPicker(o, PickMode.SideLine);
                EditOptional("경고 시간 (초)", o.warnTime, defaults.spike.defaultWarnTime, v => o.warnTime = v);
                EditOptional("유지 시간 (초)", o.holdTime, defaults.spike.defaultHoldTime, v => o.holdTime = v);
                break;

            case "laser":
                DrawGridPicker(o, PickMode.Laser);
                EditOptional("경고 시간 (초)", o.warnTime, defaults.laser.defaultWarnTime, v => o.warnTime = v);
                EditOptional("유지 시간 (초)", o.holdTime, defaults.laser.defaultHoldTime, v => o.holdTime = v);
                break;

            case "floor":
                DrawGridPicker(o, PickMode.Cell);
                EditOptional("경고 시간 (초)", o.warnTime, defaults.floor.defaultWarnTime, v => o.warnTime = v);
                EditOptional("막는 시간 (초)", o.blockTime, defaults.floor.defaultBlockTime, v => o.blockTime = v);
                break;

            case "wall":
                DrawWallFields(o);
                break;

            case "portal":
                EditorGUI.BeginChangeCheck();
                bool enabled = EditorGUILayout.Toggle("켜기", o.enabled);
                if (EditorGUI.EndChangeCheck())
                {
                    RecordUndo("텔레포트 켜기/끄기");
                    o.enabled = enabled;
                }
                EditorGUILayout.HelpBox(o.enabled
                    ? "이 시간부터 상하좌우 벽이 반대편과 이어집니다. 다음에 끄는 이벤트까지 유지됩니다."
                    : "이 시간부터 텔레포트가 꺼집니다.", MessageType.None);
                break;

            case "dark":
                EditorGUI.BeginChangeCheck();
                bool dark = EditorGUILayout.Toggle("어둡게", o.enabled);
                if (EditorGUI.EndChangeCheck())
                {
                    RecordUndo("어둠 켜기/끄기");
                    o.enabled = dark;
                }
                EditorGUILayout.HelpBox(o.enabled
                    ? "이 시간부터 맵이 어두워집니다. 포탄과 발사기가 더 밝게 빛나고, 포탄·발사기·하트 주변의 격자 선만 보입니다. 다음에 끄는 이벤트까지 유지됩니다."
                    : "이 시간부터 다시 밝아집니다.", MessageType.None);
                break;

            default:
                EditorGUILayout.HelpBox($"알 수 없는 종류 '{o.type}'입니다. 게임에서는 건너뜁니다.", MessageType.Warning);
                break;
        }

        if (!IsPlacementValid(o))
        {
            EditorGUILayout.HelpBox("그 시간의 격자에 이 줄이나 칸이 없어서 게임에서 나오지 않습니다. 벽 이동으로 격자가 줄어든 때인지 확인해 주세요.", MessageType.Warning);
        }

        GUILayout.Space(8f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("복사")) CopySelection();
        if (GUILayout.Button("삭제"))
        {
            DeleteSelection();
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>시간 칸과 배치 단위만큼 앞뒤로 옮기는 버튼. 아래에 몇 번째 박인지와 그 종류에서 time의 뜻을 보여 준다.</summary>
    void DrawTimeField(ObstacleData o)
    {
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        float time = EditorGUILayout.FloatField(new GUIContent("시간 (초)", TimeMeaning(o.type)), o.time);
        float step = SnapStep > 0f ? SnapStep : 0.01f;
        if (GUILayout.Button(new GUIContent("◀", "배치 단위만큼 앞으로"), EditorStyles.miniButtonLeft, GUILayout.Width(24f))) time = Snap(o.time - step);
        if (GUILayout.Button(new GUIContent("▶", "배치 단위만큼 뒤로"), EditorStyles.miniButtonRight, GUILayout.Width(24f))) time = Snap(o.time + step);
        EditorGUILayout.EndHorizontal();
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("시간 바꾸기");
            o.time = ClampTime(RoundTime(time));
        }

        EditorGUILayout.LabelField(" ", $"{BeatAt(o.time) + 1f:0.##}번째 박 · {TimeMeaning(o.type)}", EditorStyles.miniLabel);
    }

    static string TimeMeaning(string type)
    {
        switch (type)
        {
            case "cannon": return "포탄이 격자에 닿는 순간";
            case "homing": return "유도탄이 발사되는 순간";
            case "boomerang": return "부메랑이 처음 격자에 닿는 순간";
            case "spike": return "가시가 다 튀어나온 순간";
            case "laser": return "레이저가 켜지는 순간";
            case "floor": return "바닥 포탄이 튀어나오는 순간";
            case "wall": return "벽이 도착하는 순간";
            case "portal": return "텔레포트가 켜지거나 꺼지는 순간";
            case "dark": return "어두워지거나 밝아지기 시작하는 순간";
            default: return "";
        }
    }

    /// <summary>0이면 기본값을 쓰는 선택 값. 옆에 지금 쓰일 기본값을 보여 준다.</summary>
    void EditOptional(string label, float value, float defaultValue, Action<float> apply)
    {
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        float v = EditorGUILayout.FloatField(new GUIContent(label, "0이면 Level Player 인스펙터의 기본값을 씁니다."), value);
        GUILayout.Label(value > 0f ? "" : $"기본 {defaultValue:0.##}", EditorStyles.miniLabel, GUILayout.Width(52f));
        EditorGUILayout.EndHorizontal();
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo(label + " 바꾸기");
            apply(Mathf.Max(0f, (float)Math.Round(v, 4)));
        }
    }

    void DrawWallFields(ObstacleData o)
    {
        int sideIndex = Mathf.Max(0, Array.IndexOf(SideKeys, o.side));

        EditorGUI.BeginChangeCheck();
        int newSide = EditorGUILayout.Popup("움직일 벽", sideIndex, WallSideNames);
        bool horizontal = newSide < 2;
        int limit = horizontal ? defaults.maxColumns : defaults.maxRows;
        int size = EditorGUILayout.IntSlider(horizontal ? "움직인 뒤 가로 칸 수" : "움직인 뒤 세로 칸 수", Mathf.Clamp(o.size, 1, limit), 1, limit);
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("벽 이동 바꾸기");
            o.side = SideKeys[newSide];
            o.size = size;
        }

        EditOptional("경고 시간 (초)", o.warnTime, defaults.wallWarnTime, v => o.warnTime = v);

        var b = BoundsAt(o.time);
        EditorGUILayout.LabelField("움직인 뒤 격자", $"가로 {b.Columns}칸 × 세로 {b.RowCount}칸");
        DrawGridPicker(o, PickMode.Preview);
    }

    // ───────────────────────── 격자 그림 ─────────────────────────

    /// <summary>
    /// 그 시간의 격자를 그리고, 눌러서 위치를 고른다.
    /// SideLine: 바깥 칸(벽 바로 밖)을 눌러 발사기·가시의 쪽과 줄을 고른다.
    /// Laser: 바깥 칸을 눌러 줄과 발사기 쪽을 고른다. (왼쪽/오른쪽이면 가로줄, 위/아래면 세로줄)
    /// Cell: 안쪽 칸을 눌러 바닥 포탄 칸을 고른다.
    /// Preview: 보여 주기만 한다. (벽 이동)
    /// </summary>
    void DrawGridPicker(ObstacleData o, PickMode mode)
    {
        var e = Event.current;
        var b = BoundsAt(o.time);
        int columns = Mathf.Max(1, b.Columns);
        int rowCount = Mathf.Max(1, b.RowCount);
        float cell = Mathf.Floor(Mathf.Min(30f, (PanelWidth - 40f) / (columns + 2), 230f / (rowCount + 2)));

        string caption = mode == PickMode.Cell ? "위치 (칸을 누르세요)" : mode == PickMode.Preview ? "" : "위치 (바깥 칸을 누르세요)";
        if (caption != "") EditorGUILayout.LabelField(caption, EditorStyles.miniBoldLabel);

        Rect area = GUILayoutUtility.GetRect((columns + 2) * cell, (rowCount + 2) * cell, GUILayout.ExpandWidth(false));
        area.x += Mathf.Max(0f, (PanelWidth - 24f - area.width) * 0.5f);   // 가운데로

        // 칸 좌표 (gx, gy)의 그림 위치. 바깥 칸은 minX - 1, maxX, minY - 1, maxY다.
        Rect CellRect(int gx, int gy) => new Rect(area.x + (gx - b.minX + 1) * cell + 1f, area.y + (b.maxY - gy) * cell + 1f, cell - 2f, cell - 2f);

        // 지금 고른 바깥 칸
        bool hasSide = GridSideUtil.TryParse(o.side, out var side);
        if (mode == PickMode.Laser && !hasSide && LaserObstacle.TryParseAxis(o.axis, out bool laserRow))
        {
            side = LaserObstacle.DefaultSide(laserRow);
            hasSide = true;
        }

        Color color = TypeColor(o.type);
        bool showSlots = mode == PickMode.SideLine || mode == PickMode.Laser;

        if (e.type == EventType.Repaint)
        {
            for (int gy = b.minY - 1; gy <= b.maxY; gy++)
            {
                for (int gx = b.minX - 1; gx <= b.maxX; gx++)
                {
                    bool insideX = b.HasColumn(gx), insideY = b.HasRow(gy);
                    bool inside = insideX && insideY;
                    bool slot = insideX != insideY;   // 바깥 칸 (모서리는 뺀다)
                    if (!inside && !(slot && showSlots)) continue;

                    Rect r = CellRect(gx, gy);
                    Color fill = inside ? new Color(0.24f, 0.24f, 0.24f) : new Color(0.17f, 0.17f, 0.17f);

                    if (inside && IsHighlightedCell(o, mode, b, side, hasSide, gx, gy)) fill = Color.Lerp(fill, color, 0.45f);
                    if (slot && hasSide && IsSelectedSlot(o, side, b, gx, gy)) fill = color;

                    EditorGUI.DrawRect(r, fill);

                    if (slot)
                    {
                        // 바깥 칸에는 줄 번호를 적는다.
                        int line = insideY ? gy : gx;
                        GUI.Label(r, line.ToString(), CenteredMini(slot && hasSide && IsSelectedSlot(o, side, b, gx, gy) ? Color.black : Color.gray));
                    }
                    else if (mode == PickMode.Cell)
                    {
                        GUI.Label(r, $"{gx},{gy}", CenteredMini(new Color(1f, 1f, 1f, 0.35f)));
                    }
                }
            }

            // 벽 이동 미리보기: 움직인 벽을 굵게 칠한다.
            if (mode == PickMode.Preview && GridSideUtil.TryParse(o.side, out var wallSide))
            {
                Rect inner = new Rect(area.x + cell, area.y + cell, columns * cell, rowCount * cell);
                const float t = 3f;
                Rect bar = wallSide == GridSide.Left ? new Rect(inner.x - t, inner.y, t, inner.height)
                    : wallSide == GridSide.Right ? new Rect(inner.xMax, inner.y, t, inner.height)
                    : wallSide == GridSide.Top ? new Rect(inner.x, inner.y - t, inner.width, t)
                    : new Rect(inner.x, inner.yMax, inner.width, t);
                EditorGUI.DrawRect(bar, color);
            }
        }

        // 눌러서 고르기
        if (mode != PickMode.Preview && e.type == EventType.MouseDown && e.button == 0 && area.Contains(e.mousePosition))
        {
            int gx = Mathf.FloorToInt((e.mousePosition.x - area.x) / cell) + b.minX - 1;
            int gy = b.maxY - Mathf.FloorToInt((e.mousePosition.y - area.y) / cell);
            bool insideX = b.HasColumn(gx), insideY = b.HasRow(gy);

            if (mode == PickMode.Cell && insideX && insideY)
            {
                RecordUndo("칸 고르기");
                o.cell = new[] { gx, gy };
                e.Use();
            }
            else if (showSlots && insideX != insideY)
            {
                GridSide picked = insideY ? (gx < b.minX ? GridSide.Left : GridSide.Right) : (gy < b.minY ? GridSide.Bottom : GridSide.Top);
                RecordUndo("위치 고르기");
                o.side = SideKeys[(int)picked];
                o.line = insideY ? gy : gx;
                if (mode == PickMode.Laser) o.axis = GridSideUtil.IsHorizontal(picked) ? "row" : "col";
                e.Use();
            }
            GUI.FocusControl(null);
        }

        EditorGUILayout.LabelField(PlacementText(o, mode, side, hasSide), EditorStyles.centeredGreyMiniLabel);
        GUILayout.Space(4f);
    }

    static Color TypeColor(string type)
    {
        int i = RowIndexOf(type);
        return i >= 0 ? Rows[i].color : Color.white;
    }

    /// <summary>고른 바깥 칸인지</summary>
    static bool IsSelectedSlot(ObstacleData o, GridSide side, GridRect b, int gx, int gy)
    {
        switch (side)
        {
            case GridSide.Left: return gx == b.minX - 1 && gy == o.line;
            case GridSide.Right: return gx == b.maxX && gy == o.line;
            case GridSide.Bottom: return gy == b.minY - 1 && gx == o.line;
            default: return gy == b.maxY && gx == o.line;
        }
    }

    /// <summary>
    /// 장애물이 지나가거나 닿는 안쪽 칸을 옅게 칠한다.
    /// 일반 포탄·레이저: 그 줄 전체, 부메랑: 되돌아오는 칸까지, 가시: 벽 옆 첫 칸, 바닥 포탄: 고른 칸
    /// </summary>
    bool IsHighlightedCell(ObstacleData o, PickMode mode, GridRect b, GridSide side, bool hasSide, int gx, int gy)
    {
        if (mode == PickMode.Cell) return o.cell != null && o.cell.Length >= 2 && o.cell[0] == gx && o.cell[1] == gy;
        if (!hasSide || mode == PickMode.Preview) return false;

        bool horizontal = GridSideUtil.IsHorizontal(side);
        if ((horizontal ? gy : gx) != o.line) return false;

        // 벽에서 몇 번째 칸인지 (1부터)
        int depth = side == GridSide.Left ? gx - b.minX + 1
            : side == GridSide.Right ? b.maxX - gx
            : side == GridSide.Bottom ? gy - b.minY + 1
            : b.maxY - gy;

        switch (o.type)
        {
            case "spike": return depth == 1;
            case "boomerang": return depth <= Mathf.CeilToInt(o.range > 0f ? o.range : defaults.boomerang.defaultRange);
            case "homing": return false;
            default: return true;
        }
    }

    static string PlacementText(ObstacleData o, PickMode mode, GridSide side, bool hasSide)
    {
        if (mode == PickMode.Preview) return "";
        if (mode == PickMode.Cell) return o.cell != null && o.cell.Length >= 2 ? $"칸 [{o.cell[0]}, {o.cell[1]}]" : "칸을 골라 주세요";
        if (!hasSide) return "바깥 칸을 골라 주세요";

        string sideName = side == GridSide.Left ? "왼쪽" : side == GridSide.Right ? "오른쪽" : side == GridSide.Top ? "위" : "아래";
        string lineName = GridSideUtil.IsHorizontal(side) ? $"가로줄 {o.line}" : $"세로줄 {o.line}";
        return mode == PickMode.Laser ? $"{lineName} · 발사기 {sideName}" : $"{sideName} 벽 · {lineName}";
    }

    static readonly Dictionary<Color, GUIStyle> centeredStyles = new Dictionary<Color, GUIStyle>();

    static GUIStyle CenteredMini(Color color)
    {
        if (!centeredStyles.TryGetValue(color, out var style))
        {
            style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0, 0, 0, 0) };
            style.normal.textColor = color;
            centeredStyles[color] = style;
        }
        return style;
    }
}
