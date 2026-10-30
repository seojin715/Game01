using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 레벨 에디터의 타임라인.
/// 맨 위 눈금(초)과 곡 파형, 그 아래 장애물 종류별 줄이 있다. 가로가 곡 시간이다.
/// 박자 선은 오프셋부터 BPM 간격으로 긋고, 4박마다 진하게, 배치 단위마다 옅게 긋는다.
/// 같은 줄에서 장애물이 겹치면 아래 칸으로 내려서 모두 보이게 한다.
/// </summary>
public partial class LevelEditorWindow
{
    const float HeaderWidth = 96f;      // 왼쪽 줄 이름 칸
    const float RulerHeight = 20f;
    const float WaveHeight = 56f;
    const float LaneHeight = 18f;       // 줄 안에서 장애물 한 칸 높이
    const float RowPadding = 3f;
    const float ScrollbarHeight = 15f;
    const float MinZoom = 15f;
    const float MaxZoom = 1000f;
    const int PeaksPerSecond = 200;     // 파형을 미리 계산해 두는 촘촘함 (초당)

    /// <summary>타임라인 줄 (장애물 종류). 순서대로 위에서 아래로 그린다.</summary>
    static readonly (string type, string label, Color color)[] Rows =
    {
        ("cannon", "일반 포탄", new Color(0.95f, 0.80f, 0.35f)),
        ("laser", "레이저", new Color(0.45f, 0.85f, 1.00f)),
        ("floor", "바닥 포탄", new Color(0.60f, 0.90f, 0.50f)),
        ("homing", "유도탄", new Color(1.00f, 0.62f, 0.30f)),
        ("spike", "가시", new Color(0.85f, 0.62f, 1.00f)),
        ("boomerang", "부메랑", new Color(0.40f, 0.95f, 0.80f)),
        ("wall", "벽 이동", new Color(0.80f, 0.80f, 0.80f)),
        ("portal", "텔레포트", new Color(0.55f, 0.75f, 1.00f)),
        ("dark", "어둠", new Color(0.62f, 0.58f, 0.78f)),
    };

    static readonly Color BackgroundColor = new Color(0.13f, 0.13f, 0.13f);
    static readonly Color HeaderColor = new Color(0.18f, 0.18f, 0.18f);
    static readonly Color CursorColor = new Color(1f, 0.55f, 0.15f);
    static readonly Color WaveColor = new Color(0.45f, 0.62f, 0.85f, 0.85f);

    /// <summary>화면에 놓인 장애물 하나 (rect는 줄 영역 안의 좌표)</summary>
    struct Marker
    {
        public int index;
        public int row;
        public Rect rect;
        public string label;
    }

    readonly List<Marker> markers = new List<Marker>();
    readonly float[] rowTops = new float[Rows.Length + 1];   // 줄마다 위쪽 y (마지막은 전체 높이)
    readonly List<float> laneEnds = new List<float>();
    readonly List<int> rowIndices = new List<int>();

    // 끌어서 옮기기
    bool dragMoved;
    float dragStartX;
    int dragAnchor;
    int dragUndoGroup;
    readonly Dictionary<int, float> dragOriginal = new Dictionary<int, float>();

    // 곡 파형 (곡이 바뀔 때만 다시 계산한다)
    float[] peaks;
    string peaksKey;
    bool waveformFailed;

    GUIStyle markerLabelStyle;

    void DrawTimeline(Rect rect)
    {
        var e = Event.current;
        float trackWidth = Mathf.Max(1f, rect.width - HeaderWidth);

        Rect ruler = new Rect(rect.x, rect.y, rect.width, RulerHeight);
        Rect wave = new Rect(rect.x, ruler.yMax, rect.width, WaveHeight);
        Rect rowsView = new Rect(rect.x, wave.yMax, rect.width, Mathf.Max(0f, rect.yMax - ScrollbarHeight - wave.yMax));
        Rect scrollbar = new Rect(rect.x + HeaderWidth, rect.yMax - ScrollbarHeight, trackWidth, ScrollbarHeight);

        HandleWheel(rect, trackWidth);

        // 배경, 곡 끝 너머, 노래 시작 전 준비 시간
        if (e.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(rect, BackgroundColor);
            float songEndX = TimeToX(rect.x, SongLength);
            if (songEndX < rect.xMax)
            {
                float from = Mathf.Max(songEndX, rect.x + HeaderWidth);
                EditorGUI.DrawRect(new Rect(from, rect.y, rect.xMax - from, rect.height), new Color(0f, 0f, 0f, 0.35f));
            }

            // 0초 앞은 노래가 나오기 전 준비 시간이다. 여기에도 장애물을 놓을 수 있다. (그보다 앞은 어둡게 막아 둔다)
            float zeroX = TimeToX(rect.x, 0f);
            float leftX = rect.x + HeaderWidth;
            if (zeroX > leftX)
            {
                float leadX = Mathf.Max(leftX, TimeToX(rect.x, -LeadIn));
                if (leadX > leftX) EditorGUI.DrawRect(new Rect(leftX, rect.y, leadX - leftX, rect.height), new Color(0f, 0f, 0f, 0.45f));   // 준비 시간보다 앞 (놓을 수 없음)
                EditorGUI.DrawRect(new Rect(leadX, rect.y + RulerHeight, zeroX - leadX, rect.height - RulerHeight), new Color(0.3f, 0.6f, 1f, 0.08f));
                EditorGUI.DrawRect(new Rect(zeroX, rect.y, 1f, rect.height), new Color(1f, 1f, 1f, 0.35f));
                if (zeroX - leadX > 110f)
                {
                    GUI.Label(new Rect(leadX + 4f, rect.y + RulerHeight + 2f, zeroX - leadX - 8f, 16f), $"준비 시간 {LeadIn:0.#}초 (노래 전)", EditorStyles.miniLabel);
                }
            }
        }

        DrawRuler(ruler);
        DrawWaveform(wave);
        HandleCursorDrag(ruler, wave);

        LayoutMarkers();
        DrawRows(rowsView);

        // 아래 가로 스크롤 막대와 커서 시간
        float visible = trackWidth / pixelsPerSecond;
        scrollTime = GUI.HorizontalScrollbar(scrollbar, scrollTime, visible, -LeadIn, Mathf.Max(SongLength + 2f, visible - LeadIn));
        ClampScroll(trackWidth);
        GUI.Label(new Rect(rect.x + 4f, rect.yMax - ScrollbarHeight, HeaderWidth - 6f, ScrollbarHeight),
            $"{cursorTime:0.00}초 · {BeatAt(cursorTime) + 1f:0.##}박", EditorStyles.miniLabel);
    }

    // ───────────────────────── 시간 ↔ 좌표 ─────────────────────────

    /// <summary>시간 t의 x 좌표. left는 타임라인 왼쪽 끝 x (창 좌표면 rect.x, 줄 영역 안이면 0)</summary>
    float TimeToX(float left, float t) => left + HeaderWidth + (t - scrollTime) * pixelsPerSecond;

    float XToTime(float left, float x) => scrollTime + (x - left - HeaderWidth) / pixelsPerSecond;

    /// <summary>곡 길이. 곡 파일이 없으면 마지막 장애물 뒤로 넉넉히 잡는다.</summary>
    float SongLength
    {
        get
        {
            var clip = GetClip();
            if (clip != null) return clip.length;

            float last = 0f;
            foreach (var o in obstacles) last = Mathf.Max(last, o.time);
            return Mathf.Max(60f, last + 10f);
        }
    }

    /// <summary>노래 시작 전 준비 시간 (씬의 SongClock 설정). 타임라인은 -준비 시간부터 보여 준다.</summary>
    float LeadIn => Mathf.Max(0f, defaults.leadIn);

    void ClampScroll(float trackWidth)
    {
        float visible = trackWidth / pixelsPerSecond;
        scrollTime = Mathf.Clamp(scrollTime, -LeadIn, Mathf.Max(-LeadIn, SongLength + 2f - visible));
    }

    /// <summary>휠: 좌우 이동, Ctrl+휠: 마우스 위치를 기준으로 확대/축소, Shift+휠: 줄 위아래 이동</summary>
    void HandleWheel(Rect rect, float trackWidth)
    {
        var e = Event.current;
        if (e.type != EventType.ScrollWheel || !rect.Contains(e.mousePosition) || e.shift) return;

        float delta = Mathf.Abs(e.delta.y) > Mathf.Abs(e.delta.x) ? e.delta.y : e.delta.x;
        if (e.control || e.command)
        {
            float mouseTime = XToTime(rect.x, e.mousePosition.x);
            pixelsPerSecond = Mathf.Clamp(pixelsPerSecond * (delta > 0f ? 1f / 1.15f : 1.15f), MinZoom, MaxZoom);
            scrollTime = mouseTime - (e.mousePosition.x - rect.x - HeaderWidth) / pixelsPerSecond;
        }
        else
        {
            scrollTime += delta * 30f / pixelsPerSecond;   // 휠 한 칸에 30픽셀쯤
        }

        ClampScroll(trackWidth);
        e.Use();
        Repaint();
    }

    // ───────────────────────── 눈금, 박자 선, 파형 ─────────────────────────

    void DrawRuler(Rect ruler)
    {
        if (Event.current.type != EventType.Repaint) return;

        EditorGUI.DrawRect(ruler, HeaderColor);
        GUI.Label(new Rect(ruler.x + 4f, ruler.y + 2f, HeaderWidth, ruler.height), "시간 (초)", EditorStyles.miniLabel);

        // 글자가 겹치지 않도록 눈금 간격을 고른다.
        float[] steps = { 0.25f, 0.5f, 1f, 2f, 5f, 10f, 15f, 30f, 60f };
        float step = steps[steps.Length - 1];
        foreach (float s in steps)
        {
            if (s * pixelsPerSecond >= 60f) { step = s; break; }
        }

        float left = ruler.x + HeaderWidth;
        float t0 = Mathf.Floor(scrollTime / step) * step;
        for (float t = t0; ; t += step)
        {
            float x = TimeToX(ruler.x, t);
            if (x > ruler.xMax) break;
            if (x < left) continue;

            EditorGUI.DrawRect(new Rect(x, ruler.yMax - 6f, 1f, 6f), new Color(1f, 1f, 1f, 0.5f));
            GUI.Label(new Rect(x + 2f, ruler.y + 1f, 60f, ruler.height), t.ToString(step < 1f ? "0.##" : "0"), EditorStyles.miniLabel);
        }

        DrawCursorLine(ruler.x, ruler.y, ruler.height);
    }

    /// <summary>
    /// 박자 선. 4박(마디)마다 진하게, 1박마다 보통, 배치 단위(1/2박, 1/4박)마다 옅게 긋는다.
    /// 너무 촘촘해지는 선은 건너뛴다.
    /// </summary>
    void DrawBeatLines(float left, float minX, float maxX, float y, float height)
    {
        if (Event.current.type != EventType.Repaint) return;

        float beat = BeatLength;
        if (beat <= 0f) return;

        float step = SnapStep;
        if (step > 0f && step < beat) DrawLinesEvery(step, left, minX, maxX, y, height, new Color(1f, 1f, 1f, 0.05f));
        DrawLinesEvery(beat, left, minX, maxX, y, height, new Color(1f, 1f, 1f, 0.12f));
        DrawLinesEvery(beat * 4f, left, minX, maxX, y, height, new Color(1f, 1f, 1f, 0.28f));
    }

    void DrawLinesEvery(float step, float left, float minX, float maxX, float y, float height, Color color)
    {
        if (step * pixelsPerSecond < 5f) return;   // 5픽셀보다 촘촘하면 긋지 않는다.

        float t0 = XToTime(left, minX);
        float t1 = XToTime(left, maxX);
        int k0 = Mathf.CeilToInt((t0 - level.offset) / step);
        int k1 = Mathf.FloorToInt((t1 - level.offset) / step);
        for (int k = k0; k <= k1; k++)
        {
            float t = level.offset + k * step;
            if (t < -LeadIn) continue;
            EditorGUI.DrawRect(new Rect(TimeToX(left, t), y, 1f, height), color);
        }
    }

    void DrawWaveform(Rect wave)
    {
        if (Event.current.type != EventType.Repaint) return;

        EditorGUI.DrawRect(new Rect(wave.x, wave.y, HeaderWidth, wave.height), HeaderColor);
        GUI.Label(new Rect(wave.x + 4f, wave.y + 2f, HeaderWidth - 6f, 18f), "곡", EditorStyles.miniBoldLabel);

        float left = wave.x + HeaderWidth;
        EditorGUI.DrawRect(new Rect(left, wave.yMax - 1f, wave.width - HeaderWidth, 1f), new Color(1f, 1f, 1f, 0.1f));
        DrawBeatLines(wave.x, left, wave.xMax, wave.y, wave.height);

        EnsureWaveform();
        if (peaks == null)
        {
            string message = GetClip() == null
                ? "곡을 고르면 파형이 보입니다."
                : waveformFailed ? "파형을 읽지 못했습니다. 곡의 Load Type이 Streaming이면 Decompress On Load로 바꿔 주세요." : "";
            GUI.Label(new Rect(left + 6f, wave.y + 4f, wave.width - HeaderWidth - 12f, 18f), message, EditorStyles.miniLabel);
        }
        else
        {
            // 한 픽셀 폭마다 그 시간 구간에서 가장 큰 소리 크기를 세로 막대로 그린다.
            float mid = wave.y + wave.height * 0.5f;
            float half = wave.height * 0.5f - 3f;
            for (float x = left; x < wave.xMax; x += 1f)
            {
                int b0 = Mathf.FloorToInt(XToTime(wave.x, x) * PeaksPerSecond);
                int b1 = Mathf.FloorToInt(XToTime(wave.x, x + 1f) * PeaksPerSecond);
                if (b1 < 0 || b0 >= peaks.Length) continue;

                float peak = 0f;
                for (int b = Mathf.Max(0, b0); b <= Mathf.Min(peaks.Length - 1, Mathf.Max(b0, b1)); b++) peak = Mathf.Max(peak, peaks[b]);

                float h = Mathf.Max(0.5f, peak * half);
                EditorGUI.DrawRect(new Rect(x, mid - h, 1f, h * 2f), WaveColor);
            }
        }

        DrawCursorLine(wave.x, wave.y, wave.height);
    }

    void DrawCursorLine(float left, float y, float height)
    {
        float x = TimeToX(left, cursorTime);
        if (x >= left + HeaderWidth) EditorGUI.DrawRect(new Rect(x, y, 2f, height), CursorColor);
    }

    /// <summary>눈금이나 파형을 누르거나 끌면 주황 커서를 그 시간(배치 단위에 맞춤)으로 옮긴다.</summary>
    void HandleCursorDrag(Rect ruler, Rect wave)
    {
        var e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        Rect area = new Rect(ruler.x + HeaderWidth, ruler.y, ruler.width - HeaderWidth, ruler.height + wave.height);

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseDown:
                if (e.button != 0 || !area.Contains(e.mousePosition)) break;
                GUI.FocusControl(null);
                GUIUtility.hotControl = id;
                cursorTime = Snap(XToTime(ruler.x, e.mousePosition.x));
                e.Use();
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl != id) break;
                cursorTime = Snap(XToTime(ruler.x, e.mousePosition.x));
                e.Use();
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl != id) break;
                GUIUtility.hotControl = 0;
                e.Use();
                break;
        }
    }

    AudioClip cachedClip;
    string cachedClipName;

    AudioClip GetClip()
    {
        if (string.IsNullOrEmpty(level.audioFile)) return null;
        if (cachedClipName != level.audioFile || cachedClip == null)
        {
            cachedClipName = level.audioFile;
            cachedClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/" + level.audioFile);
        }
        return cachedClip;
    }

    void EnsureWaveform()
    {
        var clip = GetClip();
        string key = clip != null ? clip.name + "/" + clip.samples : null;
        if (key == peaksKey) return;

        peaksKey = key;
        peaks = null;
        waveformFailed = false;
        if (clip == null) return;

        peaks = ComputePeaks(clip);
        waveformFailed = peaks == null;
    }

    /// <summary>곡 전체를 조금씩 읽어서, 1/PeaksPerSecond초마다 가장 큰 소리 크기(0~1)를 구한다.</summary>
    static float[] ComputePeaks(AudioClip clip)
    {
        if (clip.loadState != AudioDataLoadState.Loaded && !clip.LoadAudioData()) return null;

        int channels = Mathf.Max(1, clip.channels);
        int frames = clip.samples;
        int framesPerPeak = Mathf.Max(1, clip.frequency / PeaksPerSecond);
        var result = new float[(frames + framesPerPeak - 1) / framesPerPeak];

        const int chunkFrames = 65536;
        float[] buffer = null;
        for (int start = 0; start < frames; start += chunkFrames)
        {
            int count = Mathf.Min(chunkFrames, frames - start);
            if (buffer == null || buffer.Length != count * channels) buffer = new float[count * channels];
            if (!clip.GetData(buffer, start)) return null;

            for (int i = 0; i < buffer.Length; i++)
            {
                int peak = (start + i / channels) / framesPerPeak;
                float value = Mathf.Abs(buffer[i]);
                if (value > result[peak]) result[peak] = value;
            }
        }

        // 가장 큰 소리가 꽉 차도록 맞춘다.
        float max = 0f;
        foreach (float v in result) max = Mathf.Max(max, v);
        if (max > 0f) for (int i = 0; i < result.Length; i++) result[i] /= max;
        return result;
    }

    // ───────────────────────── 장애물 줄 ─────────────────────────

    /// <summary>
    /// 장애물마다 줄과 칸(겹치면 아래 칸)을 정해서 그릴 자리를 계산한다.
    /// 줄 영역 안의 좌표(왼쪽 위가 0, 0)로 계산한다.
    /// </summary>
    void LayoutMarkers()
    {
        if (markerLabelStyle == null)
        {
            markerLabelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0, 0, 0, 0) };
            markerLabelStyle.normal.textColor = Color.black;
        }

        markers.Clear();
        float y = 0f;
        for (int r = 0; r < Rows.Length; r++)
        {
            rowTops[r] = y;

            rowIndices.Clear();
            for (int i = 0; i < obstacles.Count; i++) if (obstacles[i].type == Rows[r].type) rowIndices.Add(i);
            rowIndices.Sort((a, b) => obstacles[a].time.CompareTo(obstacles[b].time));

            // 왼쪽부터 놓으면서, 앞의 것과 겹치지 않는 가장 위 칸에 넣는다.
            laneEnds.Clear();
            foreach (int i in rowIndices)
            {
                string label = MarkerLabel(obstacles[i]);
                float width = Mathf.Max(18f, markerLabelStyle.CalcSize(new GUIContent(label)).x + 8f);
                float x = TimeToX(0f, obstacles[i].time) - width * 0.5f;

                int lane = laneEnds.FindIndex(end => end <= x - 1f);
                if (lane < 0)
                {
                    lane = laneEnds.Count;
                    laneEnds.Add(0f);
                }
                laneEnds[lane] = x + width;

                markers.Add(new Marker
                {
                    index = i,
                    row = r,
                    label = label,
                    rect = new Rect(x, y + RowPadding + lane * LaneHeight, width, LaneHeight - 2f)
                });
            }

            y += RowPadding * 2f + Mathf.Max(1, laneEnds.Count) * LaneHeight;
        }
        rowTops[Rows.Length] = y;
    }

    /// <summary>타임라인 칸에 적을 짧은 글자</summary>
    static string MarkerLabel(ObstacleData o)
    {
        switch (o.type)
        {
            case "laser":
                string laserSide = string.IsNullOrEmpty(o.side) ? "" : SideLetter(o.side);
                return (o.axis == "col" || o.axis == "column" ? "세" : "가") + o.line + laserSide;
            case "floor":
                return o.cell != null && o.cell.Length >= 2 ? $"{o.cell[0]},{o.cell[1]}" : "?";
            case "wall":
                return $"{SideLetter(o.side)}={o.size}";
            case "portal":
            case "dark":
                return o.enabled ? "켬" : "끔";
            default:
                return SideLetter(o.side) + o.line;
        }
    }

    static string SideLetter(string side)
    {
        switch (side)
        {
            case "left": return "L";
            case "right": return "R";
            case "top": return "T";
            case "bottom": return "B";
            default: return "?";
        }
    }

    void DrawRows(Rect view)
    {
        var e = Event.current;
        float contentHeight = rowTops[Rows.Length];
        bool needsVertical = contentHeight > view.height;
        Rect content = new Rect(0f, 0f, view.width - (needsVertical ? 14f : 0f), contentHeight);

        rowsScroll = GUI.BeginScrollView(view, rowsScroll, content, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);

        float width = content.width;
        if (e.type == EventType.Repaint)
        {
            // 줄 배경 (번갈아 조금씩 밝게)
            for (int r = 0; r < Rows.Length; r++)
            {
                var row = new Rect(0f, rowTops[r], width, rowTops[r + 1] - rowTops[r]);
                if (r % 2 == 1) EditorGUI.DrawRect(row, new Color(1f, 1f, 1f, 0.03f));
                EditorGUI.DrawRect(new Rect(0f, row.yMax - 1f, width, 1f), new Color(0f, 0f, 0f, 0.35f));
            }

            DrawBeatLines(0f, HeaderWidth, width, 0f, contentHeight);
            DrawDurations();
            DrawMarkers();
            DrawCursorLine(0f, 0f, contentHeight);
        }

        HandleRowMouse(width);
        DrawRowHeaders(contentHeight);

        GUI.EndScrollView();
    }

    /// <summary>경고 시간(장애물 앞, 옅게)과 유지 시간(장애물 뒤)을 가는 막대로 보여 준다.</summary>
    void DrawDurations()
    {
        var defaults = SceneDefaults();
        foreach (var m in markers)
        {
            var o = obstacles[m.index];
            float warn = 0f, hold = 0f;
            switch (o.type)
            {
                case "laser":
                    warn = o.warnTime > 0f ? o.warnTime : defaults.laser.defaultWarnTime;
                    hold = o.holdTime > 0f ? o.holdTime : defaults.laser.defaultHoldTime;
                    break;
                case "spike":
                    warn = o.warnTime > 0f ? o.warnTime : defaults.spike.defaultWarnTime;
                    hold = o.holdTime > 0f ? o.holdTime : defaults.spike.defaultHoldTime;
                    break;
                case "floor":
                    warn = o.warnTime > 0f ? o.warnTime : defaults.floor.defaultWarnTime;
                    hold = o.blockTime > 0f ? o.blockTime : defaults.floor.defaultBlockTime;
                    break;
                case "wall":
                    warn = (o.warnTime > 0f ? o.warnTime : defaults.wallWarnTime) + defaults.wallMoveTime;
                    break;
            }

            float x = TimeToX(0f, o.time);
            float y = m.rect.center.y;
            Color color = Rows[m.row].color;
            if (warn > 0f) EditorGUI.DrawRect(new Rect(x - warn * pixelsPerSecond, y - 1f, warn * pixelsPerSecond, 2f), new Color(color.r, color.g, color.b, 0.35f));
            if (hold > 0f) EditorGUI.DrawRect(new Rect(x, y - 2f, hold * pixelsPerSecond, 4f), new Color(color.r, color.g, color.b, 0.7f));
        }
    }

    void DrawMarkers()
    {
        foreach (var m in markers)
        {
            if (m.rect.xMax < HeaderWidth) continue;

            var o = obstacles[m.index];
            bool selected = selection.Contains(m.index);
            bool valid = IsPlacementValid(o);
            Color color = Rows[m.row].color;

            EditorGUI.DrawRect(m.rect, selected ? Color.Lerp(color, Color.white, 0.55f) : color);
            if (selected) DrawOutline(m.rect, Color.white, 2f);
            else if (!valid) DrawOutline(m.rect, new Color(1f, 0.25f, 0.25f), 2f);

            GUI.Label(m.rect, valid ? m.label : m.label + "!", markerLabelStyle);

            // 정확한 시간 위치 표시
            EditorGUI.DrawRect(new Rect(TimeToX(0f, o.time) - 0.5f, m.rect.yMax - 4f, 1f, 4f), Color.black);
        }
    }

    static void DrawOutline(Rect r, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(r.x - thickness, r.y - thickness, r.width + thickness * 2f, thickness), color);
        EditorGUI.DrawRect(new Rect(r.x - thickness, r.yMax, r.width + thickness * 2f, thickness), color);
        EditorGUI.DrawRect(new Rect(r.x - thickness, r.y, thickness, r.height), color);
        EditorGUI.DrawRect(new Rect(r.xMax, r.y, thickness, r.height), color);
    }

    /// <summary>왼쪽 줄 이름과, 주황 커서 시간에 그 종류를 추가하는 + 버튼</summary>
    void DrawRowHeaders(float contentHeight)
    {
        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(new Rect(0f, 0f, HeaderWidth, contentHeight), HeaderColor);
        }

        for (int r = 0; r < Rows.Length; r++)
        {
            float top = rowTops[r];
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(0f, top + 4f, 4f, LaneHeight - 4f), Rows[r].color);
                EditorGUI.DrawRect(new Rect(0f, rowTops[r + 1] - 1f, HeaderWidth, 1f), new Color(0f, 0f, 0f, 0.35f));
            }

            GUI.Label(new Rect(8f, top + RowPadding, HeaderWidth - 34f, LaneHeight), Rows[r].label, EditorStyles.miniLabel);
            var add = new GUIContent("+", $"주황 커서 시간({cursorTime:0.00}초)에 {Rows[r].label} 추가");
            if (GUI.Button(new Rect(HeaderWidth - 24f, top + RowPadding, 20f, LaneHeight - 2f), add, EditorStyles.miniButton))
            {
                AddObstacle(Rows[r].type, cursorTime);
            }
        }
    }

    /// <summary>
    /// 줄 영역의 마우스 동작.
    /// 장애물 클릭: 고르기 (Ctrl/Shift면 추가로 고르기), 끌기: 고른 것 모두 옮기기 (배치 단위에 맞춤)
    /// 빈 곳 클릭: 고르기 해제 + 주황 커서 옮기기, 빈 곳 더블클릭: 그 줄의 종류를 추가, 오른쪽 클릭: 메뉴
    /// </summary>
    void HandleRowMouse(float width)
    {
        var e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseDown:
            {
                Vector2 p = e.mousePosition;
                if (p.x < HeaderWidth || p.x > width || p.y > rowTops[Rows.Length]) break;

                int hit = HitMarker(p);
                int row = RowAt(p.y);
                float time = Snap(XToTime(0f, p.x));
                GUI.FocusControl(null);   // 글자 칸 입력을 끝내서 Delete, Ctrl+C가 타임라인에 오게 한다.

                if (e.button == 1)
                {
                    ShowContextMenu(hit, row, time);
                    e.Use();
                    break;
                }
                if (e.button != 0) break;

                bool additive = e.control || e.command || e.shift;
                if (hit >= 0)
                {
                    int index = markers[hit].index;
                    if (additive)
                    {
                        if (!selection.Remove(index)) selection.Add(index);
                    }
                    else if (!selection.Contains(index))
                    {
                        selection.Clear();
                        selection.Add(index);
                    }

                    // 고른 것들을 끌어서 옮길 준비
                    if (selection.Contains(index))
                    {
                        GUIUtility.hotControl = id;
                        dragMoved = false;
                        dragStartX = p.x;
                        dragAnchor = index;
                        dragOriginal.Clear();
                        foreach (int i in selection) dragOriginal[i] = obstacles[i].time;
                        Undo.IncrementCurrentGroup();
                        dragUndoGroup = Undo.GetCurrentGroup();
                    }
                }
                else if (e.clickCount == 2 && row >= 0)
                {
                    AddObstacle(Rows[row].type, time);
                }
                else
                {
                    if (!additive) selection.Clear();
                    cursorTime = time;
                }

                e.Use();
                break;
            }

            case EventType.MouseDrag:
            {
                if (GUIUtility.hotControl != id) break;

                float dx = e.mousePosition.x - dragStartX;
                if (!dragMoved && Mathf.Abs(dx) < 3f) break;   // 클릭하다 살짝 움직인 것은 무시
                dragMoved = true;

                // 잡은 장애물이 배치 단위에 맞도록 옮기고, 나머지는 같은 만큼 옮긴다.
                RecordUndo("장애물 옮기기");
                float anchorTime = dragOriginal[dragAnchor];
                float delta = Snap(anchorTime + dx / pixelsPerSecond) - anchorTime;
                foreach (var pair in dragOriginal)
                {
                    if (pair.Key < obstacles.Count) obstacles[pair.Key].time = ClampTime(RoundTime(pair.Value + delta));
                }
                e.Use();
                break;
            }

            case EventType.MouseUp:
                if (GUIUtility.hotControl != id) break;
                GUIUtility.hotControl = 0;
                if (dragMoved) Undo.CollapseUndoOperations(dragUndoGroup);   // 한 번 끈 것을 Ctrl+Z 한 번에 되돌린다.
                dragMoved = false;
                e.Use();
                break;
        }
    }

    /// <summary>그 점에 있는 장애물 (나중에 그린 것이 위). 없으면 -1.</summary>
    int HitMarker(Vector2 p)
    {
        for (int i = markers.Count - 1; i >= 0; i--)
        {
            if (markers[i].rect.Contains(p)) return i;
        }
        return -1;
    }

    int RowAt(float y)
    {
        for (int r = 0; r < Rows.Length; r++)
        {
            if (y >= rowTops[r] && y < rowTops[r + 1]) return r;
        }
        return -1;
    }

    void ShowContextMenu(int hit, int row, float time)
    {
        if (hit >= 0 && !selection.Contains(markers[hit].index))
        {
            selection.Clear();
            selection.Add(markers[hit].index);
        }

        var menu = new GenericMenu();
        if (row >= 0)
        {
            string type = Rows[row].type;
            menu.AddItem(new GUIContent($"여기({time:0.00}초)에 {Rows[row].label} 추가"), false, () => AddObstacle(type, time));
        }

        if (clipboard.Count > 0) menu.AddItem(new GUIContent($"여기({time:0.00}초)에 붙여넣기"), false, () => PasteAt(time));
        else menu.AddDisabledItem(new GUIContent("붙여넣기"));

        if (selection.Count > 0)
        {
            menu.AddSeparator("");
            menu.AddItem(new GUIContent($"복사 ({selection.Count}개)"), false, CopySelection);
            menu.AddItem(new GUIContent($"삭제 ({selection.Count}개)"), false, DeleteSelection);
        }

        menu.ShowAsContext();
    }
}
