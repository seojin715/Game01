using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 게임과 노래를 함께 잠깐 느리게 했다가 원래 속도로 되돌린다.
///
/// 게임 시간(Time.timeScale)과 노래 속도(pitch)를 항상 같은 값으로 바꾼다.
/// 노래 시계는 노래 속도대로 흐르므로, 느려진 동안에도 게임과 노래가 어긋나지 않는다.
/// 슬로우 진행 자체는 실제 시간으로 잰다. (느려진 시간으로 재면 슬로우가 길어지기 때문)
/// </summary>
public class SlowMotion : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("비워 두면 씬에서 SongClock을 찾아 쓴다.")]
    public SongClock clock;

    [Header("슬로우")]
    [Tooltip("느려졌다가 원래 속도로 돌아오기까지 걸리는 시간 (실제 초)")]
    [Min(0.01f)] public float duration = 0.7f;

    [Tooltip("가장 느려졌을 때의 속도. 0.5면 절반 속도이고 노래 음도 그만큼 낮아진다.")]
    [Range(0.05f, 1f)] public float slowestSpeed = 0.3f;

    [Tooltip("시간(0~1)에 따른 느려짐 정도. 1이면 가장 느리고 0이면 원래 속도다.\n기본값은 빠르게 느려졌다가 천천히 돌아온다.")]
    public AnimationCurve slowOverTime = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.15f, 1f),
        new Keyframe(1f, 0f));

    [Header("테스트")]
    [Tooltip("켜면 아래 키로 슬로우만 따로 걸어 볼 수 있다. 피격 때는 PlayerHealth가 자동으로 건다.")]
    public bool enableTestKey = true;

    public Key testKey = Key.T;

    /// <summary>슬로우가 진행 중인지</summary>
    public bool IsActive { get; private set; }

    /// <summary>지금 적용 중인 속도 (1이 원래 속도)</summary>
    public float CurrentSpeed { get; private set; } = 1f;

    float elapsed;

    void Awake()
    {
        if (clock == null) clock = FindAnyObjectByType<SongClock>();
    }

    /// <summary>슬로우를 건다. 진행 중에 다시 부르면 처음부터 다시 시작한다.</summary>
    public void Trigger()
    {
        elapsed = 0f;
        IsActive = true;
    }

    void Update()
    {
        if (enableTestKey)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[testKey].wasPressedThisFrame) Trigger();
        }

        if (!IsActive) return;

        elapsed += Time.unscaledDeltaTime;
        float t = elapsed / duration;

        if (t >= 1f)
        {
            IsActive = false;
            ApplySpeed(1f);
            return;
        }

        float amount = Mathf.Clamp01(slowOverTime.Evaluate(t));
        ApplySpeed(Mathf.Lerp(1f, slowestSpeed, amount));
    }

    /// <summary>게임 시간과 노래 속도를 같은 값으로 맞춘다.</summary>
    void ApplySpeed(float speed)
    {
        CurrentSpeed = speed;
        Time.timeScale = speed;
        if (clock != null) clock.SetSpeed(speed);
    }

    void OnDisable()
    {
        // 꺼지거나 플레이를 멈출 때 느린 상태로 남지 않게 되돌린다.
        IsActive = false;
        ApplySpeed(1f);
    }
}
