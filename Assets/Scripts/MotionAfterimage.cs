using UnityEngine;

/// <summary>
/// 잔상형 모션 블러의 강도. 피격 뒤 무적 동안 켜진다.
///
/// 실제로 화면을 섞어 그리는 것은 AfterimageRendererFeature이고,
/// Renderer2D에 그 기능이 붙어 있어야 화면에 보인다. 이 클래스는 강도만 정한다.
/// </summary>
public static class MotionAfterimage
{
    static float strength;

    /// <summary>
    /// 지난 화면을 남기는 정도 (0 ~ 0.97, 초당 60프레임 기준).
    /// 0이면 꺼지고, 클수록 꼬리가 길어진다.
    /// </summary>
    public static float Strength
    {
        get => strength;
        set => strength = Mathf.Clamp(value, 0f, 0.97f);
    }

    /// <summary>
    /// 이번 프레임에 지난 화면을 남길 비율.
    /// 프레임 속도가 달라도 1초 동안 남는 정도가 같도록 환산한다.
    /// </summary>
    public static float FramePersistence
    {
        get
        {
            if (strength <= 0f) return 0f;
            return Mathf.Pow(strength, Time.unscaledDeltaTime * 60f);
        }
    }

    // 지난 플레이에서 켜진 채로 멈췄어도 새 플레이는 꺼진 상태로 시작한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => strength = 0f;
}
