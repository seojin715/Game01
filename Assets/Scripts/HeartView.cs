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

    /// <summary>하트 본체의 실제 크기 (월드 단위). 잔상 꼬리 두께를 여기에 맞춘다.</summary>
    public float BodySize => (grid != null ? grid.cellSize : 1f) * sizeRatio;

    static readonly int ColorProperty = Shader.PropertyToID("_Color");

    SpriteRenderer spriteRenderer;
    Material heartMaterial;
    bool materialsCreated;
    bool warnedMissingSprite;

    void Awake()
    {
        if (grid == null) grid = FindFirstObjectByType<GameGrid>();
        ApplyLook();
    }

    void LateUpdate()
    {
        // 플레이 중에 인스펙터에서 크기와 색을 바꿔도 바로 보이게 매 프레임 적용한다.
        ApplyLook();
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
            heartMaterial = SpriteMaterial.Create("HeartMaterial");
            GhostMaterial = SpriteMaterial.Create("HeartGhostMaterial");
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
        transform.localScale = SpriteMaterial.FitScale(spriteRenderer.sprite, cellSize * sizeRatio);

        // 렌더러 색은 1을 넘을 수 없으므로, 1보다 밝은 색은 머티리얼에 넣어 Bloom이 받게 한다.
        if (heartMaterial != null) heartMaterial.SetColor(ColorProperty, SpriteMaterial.Glow(tint, glowIntensity));
        spriteRenderer.sortingOrder = sortingOrder;
    }

    void OnDestroy()
    {
        if (heartMaterial != null) Destroy(heartMaterial);
        if (GhostMaterial != null) Destroy(GhostMaterial);
    }
}
