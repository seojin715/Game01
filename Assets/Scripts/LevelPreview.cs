using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>레벨 에디터가 미리보기를 요청할 때 파일로 넘기는 내용</summary>
[Serializable]
public class LevelPreviewRequest
{
    public float startTime;   // 이 노래 시간부터 시작한다
    public string levelJson;  // 에디터에서 편집 중인 레벨 (저장하지 않은 내용 포함)
    public float speed = 1f;  // 진행 속도 (1이 원래 속도)
}

/// <summary>
/// 레벨 에디터의 미리보기 상태. (에디터에서만 쓰인다)
///
/// 에디터가 요청 파일(프로젝트의 Temp 폴더)을 쓰고 플레이 모드로 들어가면, 게임이 시작할 때 그 파일을 읽고 지운다.
/// 미리보기 중에는:
/// - 카운트다운과 준비 시간 없이 요청한 시간부터 노래와 장애물이 나온다. (LevelPlayer, SongClock, Countdown)
/// - 맞아도 목숨이 줄지 않고, 하트가 잠깐 번쩍이며 맞은 시간을 기록한다. 에디터 타임라인에 빨간 표시로 보인다.
/// - 표시 키(Level Player의 Preview Mark Key, 기본 M)를 누르면 그 시간을 기록한다. 에디터 타임라인에 하늘색 표시로 남는다.
/// - 에디터에서 레벨을 고치거나 다른 시간을 누르면, 플레이를 멈추지 않고 바로 반영한다. (Pending 값)
/// - 에디터에서 고른 속도로 게임과 노래가 함께 느리게 흐른다. (Speed, SlowMotion이 적용한다)
///
/// 그냥 ▶로 플레이하면 요청 파일이 없으므로 평소 게임과 똑같다.
/// </summary>
public static class LevelPreview
{
    /// <summary>미리보기 중인지</summary>
    public static bool Active { get { EnsureLoaded(); return active; } }

    /// <summary>미리보기를 시작한 노래 시간</summary>
    public static float StartTime { get { EnsureLoaded(); return startTime; } }

    /// <summary>미리보기할 레벨 JSON</summary>
    public static string LevelJson { get { EnsureLoaded(); return levelJson; } }

    /// <summary>미리보기 중 맞은 노래 시간들 (에디터 타임라인에 표시)</summary>
    public static readonly List<float> HitTimes = new List<float>();

    /// <summary>미리보기 중 표시 키를 누른 노래 시간들. 레벨 에디터 창이 가져가서 타임라인에 남기고 비운다.</summary>
    public static readonly List<float> MarkTimes = new List<float>();

    /// <summary>미리보기 중 에디터가 고친 레벨. LevelPlayer가 읽고 비운다.</summary>
    public static string PendingLevelJson;

    /// <summary>미리보기 중 에디터가 옮겨 달라고 한 노래 시간. LevelPlayer가 읽고 비운다.</summary>
    public static float? PendingSeek;

    /// <summary>미리보기 진행 속도. 1이 원래 속도이고, 평소 게임에서는 늘 1이다.</summary>
    public static float Speed { get { EnsureLoaded(); return speed; } set { speed = Mathf.Max(0.05f, value); } }

    // 같은 장애물이 여러 프레임 닿아도 한 번만 세도록 두는 간격 (노래 시간, 초)
    const float HitCooldown = 0.3f;

    static bool loaded;
    static bool active;
    static float startTime;
    static string levelJson;
    static float speed = 1f;
    static float lastHit = float.NegativeInfinity;

    /// <summary>에디터가 쓰고 게임이 읽는 요청 파일 위치 (프로젝트의 Temp 폴더, 유니티를 끄면 지워진다)</summary>
    public static string RequestPath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "LevelPreviewRequest.json");

    /// <summary>에디터에서 부른다. 다음 플레이를 미리보기로 시작하게 요청 파일을 쓴다.</summary>
    public static void WriteRequest(float time, string json, float previewSpeed)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RequestPath));
        File.WriteAllText(RequestPath, JsonUtility.ToJson(new LevelPreviewRequest { startTime = time, levelJson = json, speed = previewSpeed }));
    }

    /// <summary>미리보기 중 맞았을 때 기록한다. 새로 센 피격이면 true.</summary>
    public static bool RecordHit(float songTime)
    {
        if (songTime >= lastHit && songTime - lastHit < HitCooldown) return false;
        lastHit = songTime;
        HitTimes.Add(songTime);
        return true;
    }

    // 플레이를 시작할 때마다 새로 읽는다. (도메인 리로드를 꺼 두어서 지난 값이 남아 있을 수 있다)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        loaded = false;
        active = false;
        levelJson = null;
        speed = 1f;
        PendingLevelJson = null;
        PendingSeek = null;
        HitTimes.Clear();
        MarkTimes.Clear();
        lastHit = float.NegativeInfinity;
    }

    /// <summary>플레이 중 처음 물어볼 때 요청 파일을 읽고 지운다.</summary>
    static void EnsureLoaded()
    {
        // 레벨 에디터 미리보기는 유니티 에디터 안에서만 쓴다. (빌드한 게임은 늘 평소처럼)
        if (loaded || !Application.isPlaying || !Application.isEditor) return;
        loaded = true;

        string path = RequestPath;
        if (!File.Exists(path)) return;

        try
        {
            var request = JsonUtility.FromJson<LevelPreviewRequest>(File.ReadAllText(path));
            File.Delete(path);   // 다음에 그냥 ▶를 누르면 평소 게임이 되도록 바로 지운다.

            if (request != null && !string.IsNullOrEmpty(request.levelJson))
            {
                active = true;
                startTime = request.startTime;
                levelJson = request.levelJson;
                Speed = request.speed;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LevelPreview: 미리보기 요청을 읽지 못해 평소처럼 시작합니다. ({e.Message})");
        }
    }
}
