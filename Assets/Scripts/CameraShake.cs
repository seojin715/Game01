using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카메라를 짧게 흔든다. 카메라에 붙여 두고, 어디서든 CameraShake.Request로 흔들림을 요청한다.
///
/// - 여러 흔들림이 겹치면 더하지 않고 가장 큰 것 하나만 쓴다. (동시에 여러 레이저가 켜져도 과하지 않게)
/// - 흔들림은 실제 시간으로 잰다. 슬로우 중에도 짧게 끝난다.
/// - 다른 화면 요소(HP, 박자 깜빡임)가 흔들린 카메라 위치를 기준으로 자리 잡도록 먼저 실행한다.
///   그래서 HP는 화면에 고정된 채로 남고 게임 화면만 흔들린다.
/// </summary>
[DefaultExecutionOrder(-50)]
public class CameraShake : MonoBehaviour
{
    [Tooltip("모든 흔들림에 곱하는 배율. 0이면 흔들리지 않는다.")]
    [Range(0f, 2f)] public float multiplier = 1f;

    [Tooltip("흔들리는 빠르기 (초당 흔들림 수)")]
    [Min(1f)] public float frequency = 25f;

    struct ShakeRequest
    {
        public float amplitude;   // 월드 단위
        public float duration;    // 초
        public float startTime;   // 실제 시간
    }

    static CameraShake instance;

    readonly List<ShakeRequest> requests = new List<ShakeRequest>();
    Vector3 appliedOffset;
    float seed;

    /// <summary>
    /// 흔들림을 요청한다. amplitude는 최대로 밀리는 거리(월드 단위), duration은 길이(초).
    /// 흔들림은 시작할 때 가장 크고 점점 잦아든다. 카메라에 CameraShake가 없으면 아무 일도 없다.
    /// </summary>
    public static void Request(float amplitude, float duration)
    {
        if (instance == null || amplitude <= 0f || duration <= 0f) return;

        instance.requests.Add(new ShakeRequest
        {
            amplitude = amplitude,
            duration = duration,
            startTime = Time.unscaledTime
        });
    }

    void OnEnable()
    {
        instance = this;
        seed = Random.value * 100f;
    }

    void OnDisable()
    {
        // 흔든 만큼 되돌려 놓고 끈다.
        transform.position -= appliedOffset;
        appliedOffset = Vector3.zero;
        requests.Clear();
        if (instance == this) instance = null;
    }

    void LateUpdate()
    {
        // 지난 프레임에 밀어 둔 만큼 먼저 되돌린다. (원래 위치는 건드리지 않는다)
        transform.position -= appliedOffset;
        appliedOffset = Vector3.zero;

        float now = Time.unscaledTime;
        float amplitude = 0f;

        for (int i = requests.Count - 1; i >= 0; i--)
        {
            float progress = (now - requests[i].startTime) / requests[i].duration;
            if (progress >= 1f)
            {
                requests.RemoveAt(i);
                continue;
            }

            // 처음에 가장 크고 빠르게 잦아든다. 겹치면 가장 큰 것만 쓴다.
            float remaining = 1f - progress;
            amplitude = Mathf.Max(amplitude, requests[i].amplitude * remaining * remaining);
        }

        if (amplitude <= 0f) return;

        // 부드럽게 출렁이는 노이즈로 방향을 정한다. (-1 ~ 1)
        float t = now * frequency;
        float x = Mathf.PerlinNoise(seed, t) * 2f - 1f;
        float y = Mathf.PerlinNoise(seed + 37.1f, t) * 2f - 1f;

        appliedOffset = new Vector3(x, y, 0f) * (amplitude * multiplier);
        transform.position += appliedOffset;
    }
}
