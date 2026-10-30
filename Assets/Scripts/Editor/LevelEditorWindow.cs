using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 레벨 에디터 창. 유니티 위쪽 메뉴의 ProjectZ > Level Editor로 연다.
///
/// 위: 파일(새로, 열기, 저장), 배치 단위, 확대
/// 그 아래: 레벨 정보(제목, 곡, BPM, 오프셋, 장애물 색)
/// 가운데: 타임라인 (곡 파형 + 장애물 종류별 줄)          → LevelEditorWindow.Timeline.cs
/// 오른쪽: 고른 장애물의 설정 (격자 그림에서 위치 고르기) → LevelEditorWindow.Properties.cs
///
/// 결과는 Assets/Levels의 JSON으로 저장하고, 게임(LevelPlayer)이 그대로 읽는다.
/// 편집한 내용은 창에 직렬화해 두어서 Ctrl+Z로 되돌릴 수 있고, 스크립트가 다시 컴파일돼도 남는다.
/// </summary>
public partial class LevelEditorWindow : EditorWindow
{
    const string LevelsFolder = "Assets/Levels";
    const string AudioFolder = "Assets/Audio";
    static readonly string[] AudioExtensions = { ".mp3", ".wav", ".ogg", ".aif", ".aiff" };

    /// <summary>장애물을 놓거나 옮길 때 시간을 맞추는 단위</summary>
    enum SnapMode { Beat1, Beat2, Beat4, Free }
    static readonly string[] SnapLabels = { "1박", "1/2박", "1/4박", "자유 (초)" };

    // 편집 중인 레벨. 장애물은 편집하기 쉽게 리스트로 따로 들고 있다가 저장할 때 합친다.
    [SerializeField] string levelPath = "";
    [SerializeField] LevelData level;
    [SerializeField] List<ObstacleData> obstacles = new List<ObstacleData>();
    [SerializeField] List<int> selection = new List<int>();   // 고른 장애물의 번호 (obstacles 안 위치)
    [SerializeField] bool dirty;                              // 저장하지 않은 변경이 있는지

    // 보기 설정
    [SerializeField] SnapMode snapMode = SnapMode.Beat2;
    [SerializeField] float pixelsPerSecond = 120f;   // 확대 정도 (1초가 몇 픽셀인지)
    [SerializeField] float scrollTime;               // 타임라인 왼쪽 끝의 시간
    [SerializeField] float cursorTime;               // 붙여넣기와 + 버튼이 쓰는 시간 (주황 세로선)
    [SerializeField] Vector2 rowsScroll;
    [SerializeField] Vector2 propertiesScroll;

    // 복사한 장애물 (창을 닫아도 에디터를 켜 둔 동안 남는다)
    static readonly List<ObstacleData> clipboard = new List<ObstacleData>();

    string[] audioFiles = new string[0];

    [MenuItem("ProjectZ/Level Editor")]
    static void OpenWindow()
    {
        var window = GetWindow<LevelEditorWindow>();
        window.minSize = new Vector2(820f, 420f);
        window.Show();
    }

    void OnEnable()
    {
        if (level == null) ResetToNewLevel();
        Undo.undoRedoPerformed += OnUndoRedo;
        RefreshAudioFiles();
        UpdateTitle();
    }

    void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    void OnFocus() => RefreshAudioFiles();

    void OnUndoRedo()
    {
        selection.RemoveAll(i => i < 0 || i >= obstacles.Count);
        UpdateTitle();
        Repaint();
    }

    void OnGUI()
    {
        PrepareFrame();
        HandleCommands();

        DrawToolbar();
        DrawLevelInfo();

        EditorGUILayout.BeginHorizontal();
        Rect timelineRect = GUILayoutUtility.GetRect(200f, 100000f, 200f, 100000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        DrawTimeline(timelineRect);
        DrawPropertiesPanel();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(
            "더블클릭: 추가 · 끌기: 이동 · Ctrl+클릭: 여러 개 고르기 · Delete: 삭제 · Ctrl+C / Ctrl+V: 복사 / 주황선에 붙여넣기 · 휠: 좌우 이동 · Ctrl+휠: 확대 · Ctrl+Z: 되돌리기",
            EditorStyles.miniLabel);
    }

    // ───────────────────────── 위쪽 도구 막대와 레벨 정보 ─────────────────────────

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("새로", EditorStyles.toolbarButton, GUILayout.Width(44f))) NewLevel();
        if (GUILayout.Button("열기…", EditorStyles.toolbarButton, GUILayout.Width(50f))) OpenLevel();
        if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(44f))) Save();
        if (GUILayout.Button("다른 이름으로 저장…", EditorStyles.toolbarButton, GUILayout.Width(120f))) SaveAs();

        GUILayout.Space(8f);
        string fileLabel = string.IsNullOrEmpty(levelPath) ? "(아직 저장하지 않은 새 레벨)" : levelPath;
        GUILayout.Label(fileLabel + (dirty ? "  *" : ""), EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();

        GUILayout.Label("배치 단위", EditorStyles.miniLabel);
        snapMode = (SnapMode)EditorGUILayout.Popup((int)snapMode, SnapLabels, EditorStyles.toolbarPopup, GUILayout.Width(80f));

        GUILayout.Space(8f);
        GUILayout.Label("확대", EditorStyles.miniLabel);
        float zoom = GUILayout.HorizontalSlider(Mathf.Log(pixelsPerSecond), Mathf.Log(MinZoom), Mathf.Log(MaxZoom), GUILayout.Width(110f));
        pixelsPerSecond = Mathf.Exp(zoom);

        EditorGUILayout.EndHorizontal();
    }

    void DrawLevelInfo()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        float labelWidth = EditorGUIUtility.labelWidth;

        EditorGUIUtility.labelWidth = 32f;
        string title = EditorGUILayout.TextField("제목", level.title, GUILayout.MinWidth(150f));

        // 곡: Assets/Audio 안의 파일 중에서 고른다. 레벨에는 파일 이름만 저장한다.
        EditorGUIUtility.labelWidth = 20f;
        var options = new List<string> { "(없음)" };
        options.AddRange(audioFiles);
        int current = string.IsNullOrEmpty(level.audioFile) ? 0 : Array.IndexOf(audioFiles, level.audioFile) + 1;
        if (current == 0 && !string.IsNullOrEmpty(level.audioFile))
        {
            options.Add(level.audioFile + " (파일 없음)");
            current = options.Count - 1;
        }
        int picked = EditorGUILayout.Popup("곡", current, options.ToArray(), GUILayout.MinWidth(160f));
        string audioFile = picked == 0 ? "" : picked <= audioFiles.Length ? audioFiles[picked - 1] : level.audioFile;

        EditorGUIUtility.labelWidth = 30f;
        float bpm = EditorGUILayout.FloatField("BPM", level.bpm, GUILayout.Width(90f));
        EditorGUIUtility.labelWidth = 40f;
        float offset = EditorGUILayout.FloatField(new GUIContent("오프셋", "첫 박이 나오는 곡 위치 (초). 박자 선을 맞추는 데만 쓴다."), level.offset, GUILayout.Width(100f));
        EditorGUIUtility.labelWidth = 70f;
        Color color = EditorGUILayout.ColorField(new GUIContent("장애물 색"), ParseColor(level.obstacleColor), true, false, false, GUILayout.Width(130f));

        EditorGUIUtility.labelWidth = labelWidth;
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("레벨 정보 바꾸기");
            level.title = title;
            level.audioFile = audioFile;
            level.bpm = Mathf.Max(1f, bpm);
            level.offset = offset;
            level.obstacleColor = "#" + ColorUtility.ToHtmlStringRGB(color).ToLowerInvariant();
        }

        EditorGUILayout.EndHorizontal();
    }

    void RefreshAudioFiles()
    {
        string folder = FullPath(AudioFolder);
        if (!Directory.Exists(folder))
        {
            audioFiles = new string[0];
            return;
        }

        audioFiles = Directory.GetFiles(folder)
            .Where(f => AudioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Select(Path.GetFileName)
            .OrderBy(n => n)
            .ToArray();
    }

    static Color ParseColor(string hex)
    {
        return !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
    }

    // ───────────────────────── 파일 ─────────────────────────

    void ResetToNewLevel()
    {
        level = new LevelData { title = "새 레벨", audioFile = "", bpm = 120f, offset = 0f, obstacleColor = "#ffffff" };
        obstacles = new List<ObstacleData>();
        selection = new List<int>();
        levelPath = "";
        dirty = false;
        cursorTime = 0f;
        scrollTime = 0f;
    }

    void NewLevel()
    {
        if (!ConfirmDiscard()) return;
        ResetToNewLevel();
        Undo.ClearUndo(this);
        UpdateTitle();
    }

    void OpenLevel()
    {
        if (!ConfirmDiscard()) return;

        string path = EditorUtility.OpenFilePanel("레벨 열기", FullPath(LevelsFolder), "json");
        if (string.IsNullOrEmpty(path)) return;

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("레벨 열기", $"파일을 읽지 못했습니다.\n{e.Message}", "확인");
            return;
        }

        var loaded = LevelData.FromJson(json, out string error);
        if (loaded == null)
        {
            EditorUtility.DisplayDialog("레벨 열기", error, "확인");
            return;
        }

        level = loaded;
        obstacles = new List<ObstacleData>(loaded.obstacles);
        level.obstacles = new ObstacleData[0];
        foreach (var o in obstacles) if (o.type != null) o.type = o.type.Trim().ToLowerInvariant();

        selection = new List<int>();
        levelPath = ToProjectPath(path);
        dirty = false;
        cursorTime = 0f;
        scrollTime = 0f;
        Undo.ClearUndo(this);
        UpdateTitle();
    }

    /// <summary>저장한다. 아직 파일이 없으면 저장할 곳을 묻는다. 저장했으면 true.</summary>
    bool Save()
    {
        if (string.IsNullOrEmpty(levelPath)) return SaveAs();
        return WriteTo(levelPath);
    }

    bool SaveAs()
    {
        string name = string.IsNullOrEmpty(levelPath) ? "new_level" : Path.GetFileNameWithoutExtension(levelPath);
        string path = EditorUtility.SaveFilePanelInProject("레벨 저장", name, "json", "레벨 JSON을 저장할 곳을 고르세요. (Assets/Levels)", LevelsFolder);
        if (string.IsNullOrEmpty(path)) return false;
        return WriteTo(path);
    }

    bool WriteTo(string path)
    {
        // 시간 순으로 정렬해서 쓴다. (같은 시간이면 원래 순서대로)
        level.obstacles = obstacles.Select((o, i) => (o, i)).OrderBy(p => p.o.time).ThenBy(p => p.i).Select(p => p.o).ToArray();
        string json = LevelJsonWriter.Write(level);
        level.obstacles = new ObstacleData[0];

        try
        {
            File.WriteAllText(FullPath(path), json, new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("레벨 저장", $"저장하지 못했습니다.\n{e.Message}", "확인");
            return false;
        }

        if (path.StartsWith("Assets/")) AssetDatabase.ImportAsset(path);
        levelPath = path;
        dirty = false;
        UpdateTitle();
        ShowNotification(new GUIContent("저장했습니다"));
        return true;
    }

    /// <summary>저장하지 않은 변경이 있으면 저장할지 묻는다. 계속해도 되면 true.</summary>
    bool ConfirmDiscard()
    {
        if (!dirty) return true;

        int choice = EditorUtility.DisplayDialogComplex("저장하지 않은 변경", "지금 레벨에 저장하지 않은 변경이 있습니다. 저장할까요?", "저장", "취소", "저장 안 함");
        if (choice == 0) return Save();
        return choice == 2;
    }

    static string FullPath(string path)
    {
        if (Path.IsPathRooted(path)) return path;
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, path);
    }

    /// <summary>프로젝트 안의 파일이면 "Assets/..." 경로로 바꾼다. 밖이면 전체 경로 그대로.</summary>
    static string ToProjectPath(string fullPath)
    {
        string normalized = fullPath.Replace('\\', '/');
        string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/') + "/";
        return normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase) ? normalized.Substring(projectRoot.Length) : normalized;
    }

    void UpdateTitle()
    {
        titleContent = new GUIContent(dirty ? "레벨 에디터 *" : "레벨 에디터");
    }

    // ───────────────────────── 편집 동작 ─────────────────────────

    /// <summary>바꾸기 전에 불러서 Ctrl+Z로 되돌릴 수 있게 하고, 저장 안 됨 표시를 한다.</summary>
    void RecordUndo(string name)
    {
        Undo.RecordObject(this, name);
        dirty = true;
        UpdateTitle();
    }

    /// <summary>Ctrl+C, Ctrl+V, Delete, Ctrl+A를 처리한다. 글자 칸에 입력 중이면 그쪽에 맡긴다.</summary>
    void HandleCommands()
    {
        var e = Event.current;
        if (GUIUtility.keyboardControl != 0) return;

        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && selection.Count > 0)
        {
            DeleteSelection();
            e.Use();
            GUIUtility.ExitGUI();   // 장애물 수가 바뀌었으니 이번 이벤트의 나머지 그리기는 건너뛴다.
        }

        if (e.type != EventType.ValidateCommand && e.type != EventType.ExecuteCommand) return;
        bool execute = e.type == EventType.ExecuteCommand;

        switch (e.commandName)
        {
            case "Copy":
                if (selection.Count == 0) return;
                if (execute) CopySelection();
                break;

            case "Cut":
                if (selection.Count == 0) return;
                if (execute) { CopySelection(); DeleteSelection(); }
                break;

            case "Paste":
                if (clipboard.Count == 0) return;
                if (execute) PasteAt(cursorTime);
                break;

            case "Delete":
            case "SoftDelete":
                if (selection.Count == 0) return;
                if (execute) DeleteSelection();
                break;

            case "SelectAll":
                if (execute) SelectAll();
                break;

            default:
                return;
        }

        e.Use();

        // 실행했으면 장애물 수가 바뀌었을 수 있으니 이번 이벤트의 나머지 그리기는 건너뛴다.
        if (execute) GUIUtility.ExitGUI();
    }

    void AddObstacle(string type, float time)
    {
        RecordUndo("장애물 추가");
        obstacles.Add(CreateObstacle(type, time));
        selection.Clear();
        selection.Add(obstacles.Count - 1);
        Repaint();
    }

    /// <summary>
    /// 새 장애물을 만든다. 같은 종류가 그 전에 있으면 그 값(쪽, 줄, 경고 시간 등)을 이어받아서
    /// 비슷한 장애물을 연달아 놓기 쉽게 하고, 없으면 기본 모양으로 만든다.
    /// </summary>
    ObstacleData CreateObstacle(string type, float time)
    {
        ObstacleData previous = null;
        foreach (var o in obstacles)
        {
            if (o.type == type && o.time <= time && (previous == null || o.time >= previous.time)) previous = o;
        }

        ObstacleData created = previous != null ? Clone(previous) : DefaultObstacle(type);
        created.time = ClampTime(time);
        return created;
    }

    static ObstacleData DefaultObstacle(string type)
    {
        var o = new ObstacleData { type = type, side = "left", line = 2 };
        switch (type)
        {
            case "homing": o.side = "top"; break;
            case "boomerang": o.side = "right"; break;
            case "laser": o.axis = "row"; o.side = "left"; break;
            case "floor": o.cell = new[] { 2, 2 }; o.side = ""; break;
            case "wall": o.side = "right"; o.size = 5; break;
            case "portal":
            case "dark": o.side = ""; o.enabled = true; break;
        }
        return o;
    }

    static ObstacleData Clone(ObstacleData o) => JsonUtility.FromJson<ObstacleData>(JsonUtility.ToJson(o));

    void DeleteSelection()
    {
        if (selection.Count == 0) return;

        RecordUndo("장애물 삭제");
        foreach (int i in selection.Distinct().OrderByDescending(i => i))
        {
            if (i >= 0 && i < obstacles.Count) obstacles.RemoveAt(i);
        }
        selection.Clear();
        Repaint();
    }

    void CopySelection()
    {
        clipboard.Clear();
        clipboard.AddRange(selection.Where(i => i >= 0 && i < obstacles.Count).Select(i => Clone(obstacles[i])).OrderBy(o => o.time));
        ShowNotification(new GUIContent($"{clipboard.Count}개 복사했습니다"));
    }

    /// <summary>복사한 장애물들을 사이 간격 그대로, 가장 앞의 것이 time에 오도록 붙인다.</summary>
    void PasteAt(float time)
    {
        if (clipboard.Count == 0) return;

        RecordUndo("장애물 붙여넣기");
        float first = clipboard.Min(o => o.time);
        selection.Clear();
        foreach (var source in clipboard)
        {
            var o = Clone(source);
            o.time = ClampTime(RoundTime(time + (source.time - first)));
            obstacles.Add(o);
            selection.Add(obstacles.Count - 1);
        }
        Repaint();
    }

    void SelectAll()
    {
        selection.Clear();
        for (int i = 0; i < obstacles.Count; i++) selection.Add(i);
        Repaint();
    }

    // ───────────────────────── 시간 맞추기 ─────────────────────────

    float BeatLength => level.bpm > 0f ? 60f / level.bpm : 0f;

    /// <summary>배치 단위 길이 (초). 자유 배치면 0.</summary>
    float SnapStep
    {
        get
        {
            switch (snapMode)
            {
                case SnapMode.Beat1: return BeatLength;
                case SnapMode.Beat2: return BeatLength / 2f;
                case SnapMode.Beat4: return BeatLength / 4f;
                default: return 0f;
            }
        }
    }

    /// <summary>시간을 배치 단위(오프셋 기준 박자)에 맞춘다. 자유 배치면 0.001초 단위로만 반올림한다.</summary>
    float Snap(float time)
    {
        float step = SnapStep;
        if (step <= 0f) return ClampTime((float)Math.Round(time, 3));

        float k = Mathf.Round((time - level.offset) / step);
        float snapped = RoundTime(level.offset + k * step);
        if (snapped < -LeadIn) snapped = RoundTime(level.offset + Mathf.Ceil((-LeadIn - level.offset) / step) * step);   // 준비 시간 앞으로는 못 가게
        return ClampTime(snapped);
    }

    /// <summary>
    /// 장애물을 놓을 수 있는 가장 이른 시간은 준비 시간의 시작(-준비 시간)이다.
    /// 노래가 나오기 전 준비 시간에도 장애물을 놓을 수 있다. (그 시간은 음수로 저장된다)
    /// </summary>
    float ClampTime(float time) => Mathf.Max(-LeadIn, time);

    /// <summary>JSON이 깔끔하도록 소수 넷째 자리까지만 남긴다.</summary>
    static float RoundTime(float time) => (float)Math.Round(time, 4);

    /// <summary>그 시간이 몇 번째 박인지 (첫 박이 0)</summary>
    float BeatAt(float time) => BeatLength > 0f ? (time - level.offset) / BeatLength : 0f;
}
