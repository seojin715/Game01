using UnityEngine;

/// <summary>
/// 하트가 지나간 길을 하나로 이어진 꼬리로 남긴다.
/// 하트 복사본을 여러 개 찍는 대신 TrailRenderer로 띠를 그려서,
/// 복사본 하나하나가 보이지 않고 뒤로 갈수록 가늘고 흐려지며 사라진다.
///
/// 꼬리는 플레이어의 크기 조절에 휘둘리지 않도록 별도 오브젝트에 둔다.
/// </summary>
[RequireComponent(typeof(PlayerMover))]
[RequireComponent(typeof(HeartView))]
public class PlayerTrail : MonoBehaviour
{
    [Header("잔상")]
    public bool enableTrail = true;

    [Tooltip("꼬리가 남아 있는 시간 (초). 길수록 꼬리가 길어진다.")]
    [Min(0.01f)] public float trailTime = 0.15f;

    [Header("두께")]
    [Tooltip("하트 쪽 꼬리 두께 (하트 크기 대비)")]
    [Range(0f, 1.5f)] public float startWidth = 0.55f;

    [Tooltip("꼬리 끝 두께 (하트 크기 대비). 0이면 뾰족하게 사라진다.")]
    [Range(0f, 1.5f)] public float endWidth = 0f;

    [Header("색")]
    [Tooltip("꼬리 색. 하트 이미지 색에 맞춰 두면 자연스럽다.")]
    public Color trailColor = new Color(1f, 0f, 0f, 1f);

    [Tooltip("하트 쪽 꼬리의 진하기. 꼬리 끝으로 갈수록 0이 된다.")]
    [Range(0f, 1f)] public float startAlpha = 0.55f;

    [Header("그리기")]
    [Tooltip("이보다 느리게 움직이면 꼬리를 새로 그리지 않는다 (초당 월드 거리)")]
    [Min(0f)] public float minSpeed = 0.5f;

    [Tooltip("꼬리에 점을 찍는 최소 간격 (월드 단위). 작을수록 매끄럽다.")]
    [Min(0.001f)] public float minVertexDistance = 0.02f;

    HeartView view;
    TrailRenderer trail;
    Transform follower;

    Vector3 lastPosition;
    Color gradientColor;
    float gradientAlpha = -1f;
    bool cleared;

    void Awake()
    {
        view = GetComponent<HeartView>();
        lastPosition = transform.position;
        BuildTrail();
    }

    /// <summary>꼬리를 그릴 오브젝트를 만든다. 플레이어의 자식이 아니라 따로 둔다.</summary>
    void BuildTrail()
    {
        var go = new GameObject("PlayerTrail");
        follower = go.transform;
        follower.position = transform.position;

        trail = go.AddComponent<TrailRenderer>();
        trail.sharedMaterial = view.GhostMaterial;
        trail.alignment = LineAlignment.View;        // 2D 카메라를 정면으로 바라보게
        trail.textureMode = LineTextureMode.Stretch;
        trail.numCornerVertices = 4;                 // 꺾이는 곳을 둥글게
        trail.numCapVertices = 4;                    // 끝을 둥글게
        trail.autodestruct = false;
        trail.generateLightingData = false;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;

        ApplyGradient();
    }

    void LateUpdate()
    {
        // 하트를 따라가며 그 궤적이 꼬리가 된다.
        follower.position = transform.position;

        if (!cleared)
        {
            // 시작할 때 원점에서 끌려오는 꼬리가 생기지 않도록 한 번 지운다.
            trail.Clear();
            cleared = true;
        }

        // HeartView가 먼저 준비되지 않았을 수 있으므로 머티리얼은 생길 때 받아 둔다.
        if (trail.sharedMaterial == null && view.GhostMaterial != null) trail.sharedMaterial = view.GhostMaterial;

        float moved = Vector3.Distance(transform.position, lastPosition);
        float speed = Time.deltaTime > 0f ? moved / Time.deltaTime : 0f;
        lastPosition = transform.position;

        // 멈춰 있으면 새로 그리지 않는다. 남아 있던 꼬리는 그대로 흐려지며 사라진다.
        trail.emitting = enableTrail && speed >= minSpeed;

        trail.time = trailTime;
        trail.minVertexDistance = minVertexDistance;
        trail.sortingOrder = view.SortingOrder - 1;   // 하트보다 아래

        float bodySize = view.BodySize;
        trail.startWidth = bodySize * startWidth;
        trail.endWidth = bodySize * endWidth;

        ApplyGradient();
    }

    /// <summary>꼬리 색과, 끝으로 갈수록 투명해지는 그라디언트를 적용한다.</summary>
    void ApplyGradient()
    {
        // 값이 바뀔 때만 다시 만든다. (매 프레임 새로 만들면 낭비다)
        if (trailColor == gradientColor && Mathf.Approximately(startAlpha, gradientAlpha)) return;

        gradientColor = trailColor;
        gradientAlpha = startAlpha;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(trailColor, 0f),
                new GradientColorKey(trailColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(startAlpha, 0f),   // 하트 쪽
                new GradientAlphaKey(0f, 1f)            // 꼬리 끝
            });

        trail.colorGradient = gradient;
    }

    void OnDestroy()
    {
        if (follower != null) Destroy(follower.gameObject);
    }
}
