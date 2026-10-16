using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 하트의 목숨을 관리한다. 피격하면 왼쪽 위 HP 표시가 하나 줄고,
/// 모두 없어지면 게임오버다. (슬로우와 무적 연출은 단계 8에서 붙인다.)
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("체력")]
    [Min(1)] public int maxLives = 3;

    [Header("표시")]
    [Tooltip("비워 두면 씬에서 HealthDisplay를 찾아 쓴다.")]
    public HealthDisplay display;

    [Header("테스트")]
    [Tooltip("켜면 아래 키를 누를 때마다 목숨이 하나씩 줄어든다. 단계 8에서 실제 피격으로 바뀐다.")]
    public bool enableTestKey = true;

    public Key testKey = Key.H;

    /// <summary>남은 목숨</summary>
    public int Lives { get; private set; }

    void Awake()
    {
        if (display == null) display = FindFirstObjectByType<HealthDisplay>();
    }

    void Start()
    {
        Lives = maxLives;

        if (display != null) display.SetMaxLives(maxLives);
        else Debug.LogWarning("PlayerHealth: 씬에 HealthDisplay가 없어서 HP가 표시되지 않습니다.");
    }

    void Update()
    {
        if (!enableTestKey) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[testKey].wasPressedThisFrame) TakeDamage();
    }

    /// <summary>피격 처리. 목숨을 하나 줄인다.</summary>
    public void TakeDamage()
    {
        if (Lives <= 0) return;

        Lives--;
        if (display != null) display.SetLives(Lives);

        Debug.Log(Lives > 0 ? $"피격! 남은 목숨 {Lives}" : "피격! 게임오버");
    }
}
