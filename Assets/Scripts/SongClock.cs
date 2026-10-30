using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 노래를 재생하고, 노래의 재생 위치(초)를 게임의 기준 시계로 제공한다.
/// 모든 장애물 타이밍은 이 시계(SongTime)를 기준으로 한다.
///
/// - 노래는 씬에서 직접 연결하지 않고 Assets/Audio 안의 파일 이름으로 불러온다.
///   노래 파일은 git에 올리지 않아서 컴퓨터마다 .meta가 달라지므로, 직접 연결하면 끊기기 때문이다.
/// - 오디오의 재생 위치는 몇십 밀리초 단위로 끊겨서 갱신된다. 그대로 쓰면 움직임이 덜컥거리므로
///   매 프레임 흐른 시간으로 예측하고, 실제 재생 위치 쪽으로 조금씩 당겨서 맞춘다.
/// - 노래 속도(pitch)가 바뀌면 시계도 그 비율로 흐른다. 슬로우가 걸려도 노래와 어긋나지 않는다.
/// - Play를 부르면 바로 노래를 틀지 않고 준비 시간(leadIn)만큼 시계가 음수에서 0까지 먼저 흐른다.
///   그동안 0초 근처 장애물의 발사기가 미리 들어올 수 있고, 시계가 0이 되는 순간 노래가 시작된다.
///
/// 다른 스크립트가 같은 프레임에 최신 시간을 읽도록 먼저 실행한다.
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(AudioSource))]
public class SongClock : MonoBehaviour
{
    [Header("노래")]
    [Tooltip("Assets/Audio 안의 노래 파일 이름 (확장자 포함)")]
    public string songFileName = "MEGALOVANIA.mp3";

    [Tooltip("켜면 불러오자마자 재생한다.")]
    public bool playOnStart = true;

    [Range(0f, 1f)] public float volume = 0.8f;

    [Header("준비 시간")]
    [Tooltip("노래를 틀기 전에 시계가 먼저 흐르는 시간 (초). 이 동안 곡 처음에 놓은 장애물의 발사기가 들어온다. (카운트다운이 끝난 뒤에 시작된다)")]
    [Min(0f)] public float leadIn = 2f;

    [Header("시계 보정")]
    [Tooltip("예측한 시간과 실제 재생 위치가 이보다 벌어지면 바로 맞춘다 (초)")]
    [Min(0.001f)] public float resyncThreshold = 0.05f;

    [Tooltip("예측한 시간을 실제 재생 위치 쪽으로 당기는 빠르기. 크면 더 정확하지만 덜 매끄럽다.")]
    [Min(0f)] public float correctionSpeed = 5f;

    /// <summary>노래의 현재 재생 위치 (초). 게임의 기준 시계.</summary>
    public float SongTime => (float)songTime;

    /// <summary>노래를 다 불러왔는지</summary>
    public bool IsLoaded => clip != null;

    /// <summary>노래 파일을 찾지 못했거나 불러오지 못했는지</summary>
    public bool LoadFailed { get; private set; }

    /// <summary>지금 노래가 재생 중인지 (준비 시간에는 false)</summary>
    public bool IsPlaying => source != null && source.isPlaying;

    /// <summary>시계가 흐르고 있는지 (준비 시간 포함)</summary>
    public bool IsRunning => leadingIn || IsPlaying;

    /// <summary>노래 길이 (초)</summary>
    public float Length => clip != null ? clip.length : 0f;

    /// <summary>노래 재생 속도. 1이 원래 속도, 0.5면 절반 (pitch도 같이 낮아진다).</summary>
    public float Speed => source != null ? source.pitch : 1f;

    AudioSource source;
    AudioClip clip;
    double songTime;
    bool leadingIn;   // 노래를 틀기 전 준비 시간 중인지

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
    }

    void Start()
    {
        StartCoroutine(LoadSong());
    }

    /// <summary>노래 재생 속도를 바꾼다. 시계도 같은 비율로 흐른다.</summary>
    public void SetSpeed(float speed)
    {
        if (source != null) source.pitch = Mathf.Max(0.01f, speed);
    }

    /// <summary>준비 시간을 거쳐 노래를 처음부터 재생한다. 시계는 -leadIn초부터 흐른다.</summary>
    public void Play()
    {
        if (clip == null) return;

        source.clip = clip;
        source.volume = volume;
        source.Stop();

        if (leadIn > 0f)
        {
            songTime = -leadIn;
            leadingIn = true;
            return;
        }

        StartAudio();
    }

    /// <summary>노래를 지금 시계 위치(0초 근처)부터 튼다.</summary>
    void StartAudio()
    {
        leadingIn = false;
        songTime = System.Math.Max(0.0, songTime);
        source.time = Mathf.Min((float)songTime, Mathf.Max(0f, clip.length - 0.01f));
        source.Play();
    }

    void Update()
    {
        if (clip == null) return;
        source.volume = volume;

        // 준비 시간: 노래 없이 시계만 흐르다가 0초가 되면 노래를 튼다. (슬로우가 걸리면 시계도 느려진다)
        if (leadingIn)
        {
            songTime += Time.unscaledDeltaTime * source.pitch;
            if (songTime >= 0.0) StartAudio();
            return;
        }

        // 멈춰 있으면 시계도 멈춘다. (끝나면 재생 위치가 0으로 돌아가므로 그 값은 읽지 않는다)
        if (!source.isPlaying) return;

        double actual = (double)source.timeSamples / clip.frequency;
        double predicted = songTime + Time.unscaledDeltaTime * source.pitch;
        double error = actual - predicted;

        if (Math.Abs(error) > resyncThreshold)
        {
            // 많이 벌어졌으면(멈칫, 첫 프레임 등) 바로 실제 위치로 맞춘다.
            songTime = actual;
        }
        else
        {
            // 조금 벌어진 건 프레임 속도와 무관하게 부드럽게 당긴다. 시간은 뒤로 가지 않게 한다.
            double k = 1.0 - Math.Exp(-correctionSpeed * Time.unscaledDeltaTime);
            songTime = Math.Max(songTime, predicted + error * k);
        }
    }

    /// <summary>파일 이름으로 노래를 찾아 불러온다.</summary>
    IEnumerator LoadSong()
    {
        string path = FindSongPath(songFileName);
        if (path == null)
        {
            LoadFailed = true;
            yield break;
        }

        using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, GetAudioType(path)))
        {
            // 통째로 불러와야 재생 위치를 정확히 읽을 수 있다.
            ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio = false;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"SongClock: 노래를 불러오지 못했습니다. ({request.error})\n{path}", this);
                LoadFailed = true;
                yield break;
            }

            clip = DownloadHandlerAudioClip.GetContent(request);
            clip.name = songFileName;
        }

        if (playOnStart) Play();
    }

    /// <summary>
    /// 노래 파일 위치를 찾는다. 에디터에서는 Assets/Audio를 쓴다.
    /// (빌드한 게임에서는 Assets 폴더가 없어서 StreamingAssets/Audio를 찾는다. 빌드는 나중 단계에서 다룬다.)
    /// </summary>
    string FindSongPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            Debug.LogError("SongClock: Song File Name 칸이 비어 있습니다.", this);
            return null;
        }

        string[] candidates =
        {
            Path.Combine(Application.dataPath, "Audio", fileName),
            Path.Combine(Application.streamingAssetsPath, "Audio", fileName)
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate)) return candidate;
        }

        Debug.LogError($"SongClock: 노래 파일 '{fileName}'을 찾지 못했습니다. Assets/Audio 폴더에 넣어 주세요.\n찾아본 곳:\n{string.Join("\n", candidates)}", this);
        return null;
    }

    static AudioType GetAudioType(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".mp3": return AudioType.MPEG;
            case ".ogg": return AudioType.OGGVORBIS;
            case ".wav": return AudioType.WAV;
            default: return AudioType.UNKNOWN;
        }
    }

    void OnDestroy()
    {
        // 코드로 불러온 노래는 직접 정리한다.
        if (clip != null) Destroy(clip);
    }
}
