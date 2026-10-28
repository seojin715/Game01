using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 하트의 목숨과 피격 처리를 맡는다.
///
/// 피격하면: 왼쪽 위 HP가 하나 줄고 → 게임과 노래가 잠깐 느려졌다 돌아오고(SlowMotion)
/// → 무적 시간 동안 추가 피격을 받지 않으며, 그동안 화면의 검정과 흰색이 반전되고 움직이는 것 뒤로 잔상이 번진다(모션 블러).
/// 목숨이 모두 없어지면 게임오버 신호(GameOver)를 보낸다.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("체력")]
    [Min(1)] public int maxLives = 3;

    [Header("피격 뒤 무적")]
    [Tooltip("피격 뒤 추가 피격을 받지 않는 시간 (초). 슬로우가 걸리면 그만큼 천천히 흐른다.")]
    [Min(0f)] public float invincibleTime = 1.5f;

    [Tooltip("켜면 무적 동안 화면의 검정과 흰색이 반전된다.")]
    public bool invertScreenWhileInvincible = true;

    [Header("무적 중 모션 블러 (잔상)")]
    [Tooltip("켜면 무적 동안 움직이는 것 뒤로 잔상이 번진다. (Renderer2D에 Afterimage Renderer Feature가 붙어 있어야 보인다)")]
    public bool motionBlurWhileInvincible = true;

    [Tooltip("지난 화면을 남기는 정도. 클수록 꼬리가 길다. 0.8이면 초당 60프레임 기준으로 매 프레임 80%를 남긴다.")]
    [Range(0f, 0.97f)] public float motionBlurStrength = 0.8f;

    [Tooltip("맞은 뒤 모션 블러가 최대가 되기까지 걸리는 시간 (초)")]
    [Min(0.001f)] public float motionBlurFadeIn = 0.08f;

    [Tooltip("무적이 끝나기 이 시간(초) 전부터 서서히 잔상이 줄어든다.")]
    [Min(0.001f)] public float motionBlurFadeOut = 0.4f;

    [Header("참조 (비워 두면 씬에서 찾는다)")]
    public HealthDisplay display;

    [Tooltip("피격 슬로우. 슬로우 길이와 정도는 SlowMotion에서 조절한다.")]
    public SlowMotion slowMotion;

    [Header("테스트")]
    [Tooltip("켜면 아래 키로 피격을 일으켜 볼 수 있다. (실제 피격과 똑같이 처리된다)")]
    public bool enableTestKey = true;

    public Key testKey = Key.H;

    /// <summary>남은 목숨</summary>
    public int Lives { get; private set; }

    /// <summary>무적 중인지</summary>
    public bool IsInvincible => invincibleLeft > 0f;

    /// <summary>목숨이 모두 없어졌는지</summary>
    public bool IsDead => Lives <= 0;

    /// <summary>피격해서 목숨이 줄었을 때. 남은 목숨을 넘겨준다.</summary>
    public event Action<int> Damaged;

    /// <summary>게임오버 신호. 마지막 목숨을 잃었을 때 한 번 보낸다.</summary>
    public event Action GameOver;

    float invincibleLeft;

    void Awake()
    {
        if (display == null) display = FindAnyObjectByType<HealthDisplay>();
        if (slowMotion == null) slowMotion = FindAnyObjectByType<SlowMotion>();
    }

    void Start()
    {
        Lives = maxLives;

        if (display != null) display.SetMaxLives(maxLives);
        else Debug.LogWarning("PlayerHealth: 씬에 HealthDisplay가 없어서 HP가 표시되지 않습니다.");
    }

    void Update()
    {
        if (enableTestKey)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[testKey].wasPressedThisFrame) TakeDamage();
        }

        // 무적 시간은 게임 시간으로 잰다. (나중에 일시정지하면 무적 시간도 함께 멈춘다)
        if (invincibleLeft > 0f)
        {
            invincibleLeft -= Time.deltaTime;
            if (invincibleLeft <= 0f) invincibleLeft = 0f;
        }

        ApplyScreenEffects();
    }

    /// <summary>
    /// 피격 처리. 무적 중이거나 이미 게임오버면 아무 일도 없다.
    /// 실제로 목숨이 줄었으면 true를 돌려준다.
    /// </summary>
    public bool TakeDamage()
    {
        if (IsDead || IsInvincible) return false;

        Lives--;
        if (display != null) display.SetLives(Lives);

        // 게임과 노래가 함께 잠깐 느려졌다 돌아온다.
        if (slowMotion != null) slowMotion.Trigger();

        // 무적과 화면 반전은 이번 프레임부터 바로 적용한다.
        invincibleLeft = invincibleTime;
        ApplyScreenEffects();

        Damaged?.Invoke(Lives);

        if (IsDead)
        {
            Debug.Log("피격! 게임오버");
            GameOver?.Invoke();
        }
        else
        {
            Debug.Log($"피격! 남은 목숨 {Lives}");
        }

        return true;
    }

    /// <summary>무적 상태에 맞춰 화면 반전과 블러를 정한다.</summary>
    void ApplyScreenEffects()
    {
        ScreenInvert.IsInverted = invertScreenWhileInvincible && IsInvincible;

        float motionBlur = 0f;
        if (motionBlurWhileInvincible && IsInvincible)
        {
            // 맞은 직후 빠르게 잔상이 길어지고, 무적이 끝나 갈 때 서서히 줄어든다.
            float elapsed = invincibleTime - invincibleLeft;
            float fadeIn = Mathf.Clamp01(elapsed / motionBlurFadeIn);
            float fadeOut = Mathf.Clamp01(invincibleLeft / motionBlurFadeOut);
            motionBlur = motionBlurStrength * Mathf.Min(fadeIn, fadeOut);
        }
        MotionAfterimage.Strength = motionBlur;
    }

    void OnDisable()
    {
        // 꺼지거나 플레이를 멈출 때 반전되거나 잔상이 남은 채로 있지 않게 한다.
        ScreenInvert.IsInverted = false;
        MotionAfterimage.Strength = 0f;
    }
}
