using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>장애물 하나의 소리 설정. LevelPlayer 인스펙터에서 조절한다.</summary>
[Serializable]
public class ObstacleSound
{
    [Tooltip("나는 소리 (Assets/Audio/SFX). 비워 두면 소리가 나지 않는다.")]
    public AudioClip clip;

    [Tooltip("소리 크기 (0~1). 배경 노래를 가리지 않게 작게 둔다. 같은 소리는 한 프레임에 한 번만 난다.")]
    [Range(0f, 1f)] public float volume = 0.15f;

    [Tooltip("소리를 원래 순간보다 얼마나 일찍(-) 또는 늦게(+) 낼지 (노래 시간, 초). 소리가 서서히 커지는 파일이면 앞당긴다.")]
    [Range(-0.5f, 0.5f)] public float offset;

    /// <summary>노래 시간이 moment + offset을 지나는 프레임에 소리를 낸다.</summary>
    public void PlayAt(float moment, float songTime, float previousSongTime)
    {
        float soundTime = moment + offset;
        if (previousSongTime < soundTime && songTime >= soundTime) SoundEffects.Play(clip, volume);
    }
}

/// <summary>
/// 짧은 효과음을 튼다. 어디서든 SoundEffects.Play(소리, 음량)으로 부른다.
///
/// - 효과음 전용 AudioSource 하나를 처음 쓸 때 만들어서 겹쳐 튼다. (PlayOneShot)
/// - 같은 소리가 한 프레임에 여러 번 불리면(레이저 여러 줄이 동시에 켜질 때 등) 한 번만 틀어서 갑자기 커지지 않게 한다.
/// - 피격 슬로우가 걸리면 노래처럼 효과음도 그 속도로 느리고 낮게 난다.
/// 효과음 파일은 Assets/Audio/SFX에 둔다. (노래와 달리 git에 함께 올린다)
/// </summary>
public static class SoundEffects
{
    static AudioSource source;
    static readonly Dictionary<AudioClip, int> lastPlayedFrame = new Dictionary<AudioClip, int>();

    /// <summary>clip을 volume(0~1) 크기로 한 번 튼다. clip이 비어 있으면 아무것도 하지 않는다.</summary>
    public static void Play(AudioClip clip, float volume)
    {
        if (clip == null || volume <= 0f) return;

        // 같은 프레임에 같은 소리는 한 번만
        if (lastPlayedFrame.TryGetValue(clip, out int frame) && frame == Time.frameCount) return;
        lastPlayedFrame[clip] = Time.frameCount;

        if (source == null)
        {
            var go = new GameObject("SoundEffects");
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;   // 화면 어디서 나든 같은 크기 (2D 소리)
        }

        source.pitch = Mathf.Max(0.01f, Time.timeScale);   // 슬로우가 걸리면 효과음도 같이 느려진다.
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    // 플레이를 다시 시작하면 지난 기록을 지운다. (도메인 리로드를 꺼 두어서 남아 있을 수 있다)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        source = null;
        lastPlayedFrame.Clear();
    }
}
