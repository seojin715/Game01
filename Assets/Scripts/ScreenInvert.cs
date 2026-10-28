using UnityEngine;

/// <summary>
/// 화면의 검정과 흰색을 서로 바꾸는 상태. 피격 뒤 무적 동안 켜진다.
///
/// 화면 전체를 뒤집지 않고, 검정·흰색을 쓰는 것(배경, 격자 선, 장애물, 박자 깜빡임)만
/// 이 상태를 보고 색을 뒤집는다. 그래야 빨간 하트와 HP는 그대로 빨갛게 남는다.
/// </summary>
public static class ScreenInvert
{
    /// <summary>지금 반전 중인지</summary>
    public static bool IsInverted { get; set; }

    /// <summary>반전 중이면 색을 뒤집어(검정↔흰색) 돌려준다. 투명도는 그대로 둔다.</summary>
    public static Color Apply(Color color)
    {
        if (!IsInverted) return color;
        return new Color(1f - color.r, 1f - color.g, 1f - color.b, color.a);
    }

    // 이 프로젝트는 플레이를 시작할 때 스크립트 상태를 초기화하지 않도록 설정되어 있다.
    // 지난 플레이의 반전 상태가 남지 않게 플레이를 시작할 때마다 직접 끈다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => IsInverted = false;
}
