using UnityEngine;

/// <summary>
/// 하트 이미지를 화면에 그린다. 칸 크기에 맞춰 크기를 잡고,
/// Bloom이 번지도록 1보다 밝은 색을 머티리얼에 넣는다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class HeartView : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("비워 두면 씬에서 GameGrid를 찾아 쓴다. 칸 크기에 맞춰 하트 크기를 정한다.")]
    public GameGrid grid;

    [Tooltip("하트 모양 이미지. 비워 두면 Sprite Renderer에 들어 있는 것을 쓴다.")]
    public Sprite sprite;

    [Header("크기")]
    [Tooltip("칸 대비 하트 크기. 이미지에 투명 여백이 많으면 값을 키워야 한다.")]
    [Range(0.05f, 5f)] public float sizeRatio = 0.7f;

    [Tooltip("충돌 판정 크기 (하트 크기 대비). 보이는 모양보다 약간 작게 둬서 억울한 피격을 줄인다.")]
    [Range(0.3f, 1f)] public float hitboxScale = 0.8f;

    [Header("이동 중 늘어나기")]
    public bool enableStretch = true;

    [Tooltip("속도 1칸/초마다 이동 방향으로 늘어나는 비율. 0.015면 20칸/초일 때 30% 늘어난다.")]
    [Range(0f, 0.1f)] public float stretchPerSpeed = 0.015f;

    [Tooltip("늘어나는 최대 비율. 0.3이면 원래 길이의 1.3배까지.")]
    [Range(0f, 1f)] public float maxStretch = 0.3f;

    [Tooltip("늘어난 만큼 옆으로 얇아지는 정도. 1이면 넓이가 그대로 유지되고, 0이면 옆으로는 그대로다.")]
    [Range(0f, 1f)] public float keepArea = 1f;

    [Tooltip("늘어나는 모양이 속도를 따라가는 빠르기. 크면 즉각적이고, 작으면 부드럽게 따라간다.")]
    [Min(0.1f)] public float stretchResponse = 30f;

    [Header("색과 빛")]
    [Tooltip("이미지 색에 곱해지는 색. 흰색이면 이미지 색 그대로 나온다.")]
    public Color tint = Color.white;

    [Tooltip("1보다 크면 Bloom이 번져서 빛나 보인다. Bloom을 켜야 효과가 보인다.")]
    [Range(1f, 8f)] public float glowIntensity = 2.5f;

    [Tooltip("그리는 순서. 격자 선보다 높게 둔다.")]
    public int sortingOrder = 10;

    /// <summary>잔상이 쓰는 하트 이미지</summary>
    public Sprite Sprite => spriteRenderer != null ? spriteRenderer.sprite : sprite;

    /// <summary>잔상이 쓰는 머티리얼. 빛나지 않고 색은 렌더러에서 입힌다.</summary>
    public Material GhostMaterial { get; private set; }

    public Color CurrentTint => tint;
    public int SortingOrder => sortingOrder;

    /// <summary>
    /// 충돌 판정용 반지름 (월드 단위). 하트 크기 기준이며 늘어나는 모양은 반영하지 않는다.
    /// 판정 중심은 하트의 현재 위치라서 칸 사이를 이동하는 중에도 그대로 판정된다.
    /// </summary>
    public float HitRadius => (grid != null ? grid.cellSize : 1f) * sizeRatio * 0.5f * hitboxScale;

    static readonly int ColorProperty = Shader.PropertyToID("_Color");

    SpriteRenderer spriteRenderer;
    Material heartMaterial;
    bool materialsCreated;
    bool warnedMissingSprite;

    PlayerMover mover;
    Vector2 stretch = Vector2.one;   // 지금 적용 중인 가로·세로 배율
    SpriteRenderer wrapClone;        // 텔레포트 중 원래 쪽 벽으로 빠져나가는 하트 모습

    void Awake()
    {
        if (grid == null) grid = FindAnyObjectByType<GameGrid>();
        mover = GetComponent<PlayerMover>();
        ApplyLook();
    }

    void LateUpdate()
    {
        UpdateStretch();

        // 플레이 중에 인스펙터에서 크기와 색을 바꿔도 바로 보이게 매 프레임 적용한다.
        ApplyLook();
        UpdateWrapClone();
    }

    /// <summary>
    /// 텔레포트로 벽을 넘어가는 동안, 원래 쪽에서 빠져나가는 하트 모습을 복사본으로 그린다.
    /// 하트와 복사본 모두 격자 밖은 잘려서, 한쪽 벽으로 나간 만큼 반대쪽 벽에서 들어오는 것처럼 보인다.
    /// </summary>
    void UpdateWrapClone()
    {
        bool wrapping = mover != null && mover.IsWrapping && spriteRenderer.sprite != null;
        if (!wrapping)
        {
            if (wrapClone != null) wrapClone.enabled = false;
            return;
        }

        if (wrapClone == null)
        {
            // 하트를 따라 움직이면 안 되므로 하트의 자식이 아닌 별도 오브젝트로 만든다.
            wrapClone = new GameObject("HeartWrapClone").AddComponent<SpriteRenderer>();
        }

        wrapClone.enabled = true;
        wrapClone.sprite = spriteRenderer.sprite;
        wrapClone.sharedMaterial = spriteRenderer.sharedMaterial;
        wrapClone.color = spriteRenderer.color;
        wrapClone.sortingOrder = spriteRenderer.sortingOrder;
        wrapClone.transform.SetPositionAndRotation(transform.position - mover.WrapOffset, transform.rotation);
        wrapClone.transform.localScale = transform.localScale;
    }

    /// <summary>
    /// 이동 속도에 비례해 이동 방향으로 늘어날 배율을 정한다.
    /// 가로로 움직이면 가로로 길어지고 세로로 얇아지며, 세로도 마찬가지다.
    /// </summary>
    void UpdateStretch()
    {
        Vector2 target = Vector2.one;

        if (enableStretch && mover != null)
        {
            float cellSize = grid != null ? grid.cellSize : 1f;
            Vector2 velocity = mover.Velocity;
            float cellsPerSecond = velocity.magnitude / cellSize;
            float amount = Mathf.Min(cellsPerSecond * stretchPerSpeed, maxStretch);

            if (amount > 0f)
            {
                Vector2 dir = velocity.normalized;
                float along = 1f + amount;                            // 이동 방향 배율
                float across = Mathf.Lerp(1f, 1f / along, keepArea);  // 옆 방향 배율

                // 방향 성분의 제곱으로 섞으면 가로·세로 이동은 정확히, 대각선은 그 사이로 늘어난다.
                target = new Vector2(
                    Mathf.Lerp(across, along, dir.x * dir.x),
                    Mathf.Lerp(across, along, dir.y * dir.y));
            }
        }

        // 프레임 속도와 무관하게 목표 배율을 부드럽게 따라간다.
        float k = 1f - Mathf.Exp(-stretchResponse * Time.deltaTime);
        stretch = Vector2.Lerp(stretch, target, k);
    }

    /// <summary>
    /// 필요한 부품(렌더러, 머티리얼)이 없으면 그 자리에서 갖춘다.
    /// 컴포넌트를 붙인 뒤에 스크립트가 바뀌어도 안전하게 동작한다.
    /// </summary>
    void EnsureSetup()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        // 머티리얼 만들기는 한 번만 시도한다. (실패해도 매 프레임 같은 에러가 쌓이지 않게)
        if (!materialsCreated)
        {
            materialsCreated = true;
            // 텔레포트로 벽을 넘을 때 격자 밖이 잘리도록, 격자 밖을 그리지 않는 머티리얼을 쓴다.
            heartMaterial = SpriteMaterial.CreateClipped("HeartMaterial");
            GhostMaterial = SpriteMaterial.CreateClipped("HeartGhostMaterial");
            if (heartMaterial != null) spriteRenderer.sharedMaterial = heartMaterial;
        }
    }

    /// <summary>칸 크기에 맞춘 하트 크기와, Bloom이 받을 밝은 색을 적용한다.</summary>
    void ApplyLook()
    {
        EnsureSetup();

        if (sprite != null && spriteRenderer.sprite != sprite) spriteRenderer.sprite = sprite;

        if (spriteRenderer.sprite == null)
        {
            // 매 프레임 같은 경고가 쌓이지 않도록 한 번만 알린다.
            if (!warnedMissingSprite)
            {
                Debug.LogWarning("HeartView: 하트 이미지가 없습니다. Sprite 칸에 이미지를 넣어 주세요.", this);
                warnedMissingSprite = true;
            }
            return;
        }
        warnedMissingSprite = false;

        float cellSize = grid != null ? grid.cellSize : 1f;
        Vector3 fit = SpriteMaterial.FitScale(spriteRenderer.sprite, cellSize * sizeRatio);
        transform.localScale = new Vector3(fit.x * stretch.x, fit.y * stretch.y, 1f);

        // 렌더러 색은 1을 넘을 수 없으므로, 1보다 밝은 색은 머티리얼에 넣어 Bloom이 받게 한다.
        if (heartMaterial != null) heartMaterial.SetColor(ColorProperty, SpriteMaterial.Glow(tint, glowIntensity));
        spriteRenderer.sortingOrder = sortingOrder;
    }

    void OnDestroy()
    {
        if (heartMaterial != null) Destroy(heartMaterial);
        if (GhostMaterial != null) Destroy(GhostMaterial);
        if (wrapClone != null) Destroy(wrapClone.gameObject);
    }
}
