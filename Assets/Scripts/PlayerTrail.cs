using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이동 중 지나간 자리에 하트 모양 복사본(잔상)을 촘촘하게 남긴다.
///
/// - 잔상은 시간이 아니라 "움직인 거리"마다 남긴다. 한 프레임에 많이 움직여도
///   그 사이를 나눠서 같은 간격으로 놓기 때문에 빠를 때도 끊겨 보이지 않는다.
/// - 잔상은 시간이 지날수록 투명해지고 작아진다. 투명도는 곡선으로 정한다.
/// - 잔상 오브젝트는 풀(pool)에 모아 두고 재사용한다. 매번 만들고 지우지 않는다.
///
/// 하트의 늘어난 모양까지 그대로 복사하려고 HeartView보다 늦게 실행한다.
/// </summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(PlayerMover))]
[RequireComponent(typeof(HeartView))]
public class PlayerTrail : MonoBehaviour
{
    [Header("잔상")]
    public bool enableTrail = true;

    [Tooltip("잔상 사이 간격 (월드 단위). 작을수록 촘촘하다. 칸 크기 1 기준 0.06이면 한 칸에 약 16개.")]
    [Min(0.005f)] public float spacing = 0.06f;

    [Tooltip("잔상 하나가 완전히 사라지는 데 걸리는 시간 (초)")]
    [Min(0.01f)] public float lifetime = 0.25f;

    [Tooltip("이보다 느리게 움직일 때는 잔상을 남기지 않는다 (초당 월드 거리)")]
    [Min(0f)] public float minSpeed = 0.5f;

    [Header("사라지는 모양")]
    [Tooltip("잔상이 처음 생길 때의 불투명도")]
    [Range(0f, 1f)] public float startAlpha = 0.45f;

    [Tooltip("시간(0~1)에 따른 불투명도 배율. 기본값은 처음에 빨리 줄고 끝에는 천천히 사라진다.")]
    public AnimationCurve alphaOverLife = DefaultAlphaCurve();

    [Tooltip("사라질 때의 크기 비율. 0.6이면 처음 크기의 60%까지 줄어든다.")]
    [Range(0f, 1f)] public float endScale = 0.6f;

    [Header("풀링")]
    [Tooltip("시작할 때 미리 만들어 두는 잔상 수")]
    [Min(0)] public int prewarmCount = 32;

    [Tooltip("동시에 보일 수 있는 잔상의 최대 수. 넘으면 가장 오래된 것을 재사용한다.")]
    [Min(1)] public int maxActive = 64;

    /// <summary>잔상 하나. 풀에 들어갔다 나왔다 하며 재사용된다.</summary>
    class Ghost
    {
        public GameObject gameObject;
        public Transform transform;
        public SpriteRenderer renderer;
        public Vector3 baseScale;
        public float age;
    }

    readonly List<Ghost> active = new List<Ghost>();     // 지금 보이는 잔상 (오래된 것이 앞)
    readonly Stack<Ghost> pool = new Stack<Ghost>();      // 쉬고 있는 잔상

    HeartView view;
    Transform container;
    Vector3 lastPosition;
    float distanceSinceSpawn;

    /// <summary>처음엔 빠르게 줄고 끝에는 천천히 0이 되는 기본 곡선</summary>
    static AnimationCurve DefaultAlphaCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 1f, -3.5f, -3.5f),
            new Keyframe(0.35f, 0.3f, -0.9f, -0.9f),
            new Keyframe(1f, 0f, -0.1f, -0.1f));
    }

    void Awake()
    {
        view = GetComponent<HeartView>();

        // 잔상은 하트를 따라 움직이지 않도록 하트의 자식이 아닌 별도 오브젝트에 둔다.
        container = new GameObject("PlayerTrail").transform;

        for (int i = 0; i < prewarmCount; i++) pool.Push(CreateGhost());
        lastPosition = transform.position;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        UpdateGhosts(dt);
        SpawnAlongPath(dt);
    }

    /// <summary>지난 프레임 위치에서 지금 위치까지 같은 간격으로 잔상을 놓는다.</summary>
    void SpawnAlongPath(float dt)
    {
        Vector3 from = lastPosition;
        Vector3 to = transform.position;
        lastPosition = to;

        float segment = Vector3.Distance(from, to);
        float speed = dt > 0f ? segment / dt : 0f;

        // 멈춰 있거나 꺼져 있으면 남기지 않고, 다음에 움직일 때 처음부터 센다.
        if (!enableTrail || segment <= 0f || speed < minSpeed || view.Sprite == null)
        {
            distanceSinceSpawn = 0f;
            return;
        }

        float step = Mathf.Max(spacing, 0.005f);
        float next = step - distanceSinceSpawn;   // 이번 구간에서 다음 잔상을 놓을 거리

        while (next <= segment)
        {
            float along = next / segment;

            // 구간 앞쪽에 놓이는 잔상일수록 그만큼 먼저 생긴 셈이라 나이를 조금 더 준다.
            float age = (1f - along) * dt;
            Spawn(Vector3.Lerp(from, to, along), age);
            next += step;
        }

        distanceSinceSpawn = segment - (next - step);
    }

    /// <summary>풀에서 잔상을 하나 꺼내 지금 하트 모양 그대로 놓는다.</summary>
    void Spawn(Vector3 position, float age)
    {
        Ghost ghost;
        if (active.Count >= maxActive)
        {
            // 너무 많으면 가장 오래된 잔상을 바로 재사용한다.
            ghost = active[0];
            active.RemoveAt(0);
        }
        else
        {
            ghost = pool.Count > 0 ? pool.Pop() : CreateGhost();
        }

        ghost.transform.position = position;
        ghost.baseScale = transform.localScale;   // 늘어난 모양까지 그대로 복사한다.
        ghost.transform.localScale = ghost.baseScale;
        ghost.age = age;

        ghost.renderer.sprite = view.Sprite;
        ghost.renderer.sharedMaterial = view.GhostMaterial;   // 잔상은 빛나지 않는 머티리얼을 쓴다.
        ghost.renderer.sortingOrder = view.SortingOrder - 1;  // 하트보다 아래
        ApplyFade(ghost);

        ghost.gameObject.SetActive(true);
        active.Add(ghost);
    }

    /// <summary>잔상의 나이를 늘리고, 수명이 다한 것은 풀로 돌려보낸다.</summary>
    void UpdateGhosts(float dt)
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var ghost = active[i];
            ghost.age += dt;

            if (ghost.age >= lifetime)
            {
                Release(ghost);
                active.RemoveAt(i);
                continue;
            }

            ApplyFade(ghost);
        }
    }

    /// <summary>나이에 맞춰 투명도(곡선)와 크기(점점 작게)를 정한다.</summary>
    void ApplyFade(Ghost ghost)
    {
        float t = Mathf.Clamp01(ghost.age / lifetime);

        var color = view.CurrentTint;
        color.a = startAlpha * Mathf.Clamp01(alphaOverLife.Evaluate(t));
        ghost.renderer.color = color;

        ghost.transform.localScale = ghost.baseScale * Mathf.Lerp(1f, endScale, t);
    }

    Ghost CreateGhost()
    {
        var go = new GameObject("Ghost");
        go.transform.SetParent(container, false);
        go.SetActive(false);

        return new Ghost
        {
            gameObject = go,
            transform = go.transform,
            renderer = go.AddComponent<SpriteRenderer>()
        };
    }

    void Release(Ghost ghost)
    {
        // 씬을 닫을 때는 잔상 오브젝트가 먼저 지워져 있을 수 있다.
        if (ghost.gameObject == null) return;

        ghost.gameObject.SetActive(false);
        pool.Push(ghost);
    }

    void OnDisable()
    {
        // 꺼질 때 보이던 잔상은 모두 풀로 돌려보낸다.
        foreach (var ghost in active) Release(ghost);
        active.Clear();
        distanceSinceSpawn = 0f;
    }

    void OnEnable()
    {
        // 다시 켜질 때 꺼져 있던 동안 움직인 거리만큼 잔상이 한꺼번에 생기지 않게 한다.
        lastPosition = transform.position;
    }

    void OnDestroy()
    {
        if (container != null) Destroy(container.gameObject);
    }
}
