using UnityEngine;

/// <summary>
/// 화면 왼쪽 위에 남은 목숨을 하트 아이콘으로 나란히 보여 준다.
/// 아이콘은 카메라 위치를 기준으로 잡아서 화면 비율이 바뀌어도 제자리에 남는다.
/// 아이콘은 이 컴포넌트가 붙은 오브젝트의 자식이 아닌 별도 오브젝트 아래에 만든다.
/// 그래서 플레이어에 붙여도 하트 크기나 늘어나는 모양을 따라가지 않는다.
/// </summary>
public class HealthDisplay : MonoBehaviour
{
    [Header("모양")]
    [Tooltip("HP 아이콘 이미지. 플레이어 하트와 같은 이미지를 쓴다.")]
    public Sprite sprite;

    [Tooltip("아이콘 개수. PlayerHealth가 있으면 그쪽 목숨 수로 맞춰진다.")]
    [Min(1)] public int iconCount = 3;

    [Tooltip("아이콘 하나의 크기 (월드 단위)")]
    [Min(0.01f)] public float iconSize = 0.5f;

    [Tooltip("아이콘 사이 간격 (아이콘 크기 대비)")]
    [Range(0f, 2f)] public float spacing = 0.35f;

    [Header("위치")]
    [Tooltip("화면 왼쪽 위 모서리에서 떨어진 거리 (월드 단위)")]
    public Vector2 margin = new Vector2(0.5f, 0.5f);

    [Header("색과 빛")]
    [Tooltip("이미지 색에 곱해지는 색. 흰색이면 이미지 색 그대로 나온다.")]
    public Color tint = Color.white;

    [Range(1f, 8f)] public float glowIntensity = 2.5f;

    [Tooltip("그리는 순서. 격자와 하트보다 높게 둔다.")]
    public int sortingOrder = 50;

    [Header("카메라")]
    [Tooltip("비워 두면 Main Camera를 쓴다.")]
    public Camera targetCamera;

    static readonly int ColorProperty = Shader.PropertyToID("_Color");

    SpriteRenderer[] icons;
    Transform iconRoot;
    Material iconMaterial;
    Sprite builtSprite;
    int lives = int.MaxValue;
    bool warnedMissingSprite;

    void Awake()
    {
        Rebuild();
    }

    void LateUpdate()
    {
        // 이미지나 개수가 바뀌면 다시 만들고, 자리와 색은 매 프레임 맞춘다.
        // (이미지가 없을 때는 다시 만들어도 소용없으니 건너뛴다.)
        bool needsRebuild = sprite != builtSprite
                            || (sprite != null && (icons == null || icons.Length != iconCount));
        if (needsRebuild) Rebuild();

        if (iconMaterial != null) iconMaterial.SetColor(ColorProperty, SpriteMaterial.Glow(tint, glowIntensity));
        PlaceIcons();
    }

    /// <summary>아이콘 개수를 목숨 수에 맞춘다.</summary>
    public void SetMaxLives(int maxLives)
    {
        iconCount = Mathf.Max(1, maxLives);
        Rebuild();
        SetLives(iconCount);
    }

    /// <summary>남은 목숨만큼만 아이콘을 보여 준다.</summary>
    public void SetLives(int remaining)
    {
        lives = remaining;
        if (icons == null) return;

        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i] != null) icons[i].enabled = i < remaining;
        }
    }

    /// <summary>아이콘 오브젝트를 다시 만든다.</summary>
    void Rebuild()
    {
        if (iconMaterial == null) iconMaterial = SpriteMaterial.Create("HealthIconMaterial");

        // 아이콘을 담을 오브젝트. 어디에도 붙이지 않아서 크기가 항상 1이다.
        if (iconRoot == null) iconRoot = new GameObject("HealthIcons").transform;

        if (icons != null)
        {
            foreach (var icon in icons)
            {
                if (icon != null) Destroy(icon.gameObject);
            }
        }

        builtSprite = sprite;

        if (sprite == null)
        {
            icons = new SpriteRenderer[0];
            if (!warnedMissingSprite)
            {
                Debug.LogWarning("HealthDisplay: HP 아이콘 이미지가 없습니다. Sprite 칸에 이미지를 넣어 주세요.", this);
                warnedMissingSprite = true;
            }
            return;
        }
        warnedMissingSprite = false;

        icons = new SpriteRenderer[iconCount];
        for (int i = 0; i < iconCount; i++)
        {
            var go = new GameObject($"HealthIcon_{i}");
            go.transform.SetParent(iconRoot, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = iconMaterial;
            renderer.sortingOrder = sortingOrder;
            icons[i] = renderer;
        }

        SetLives(Mathf.Min(lives, iconCount));   // 다시 만들어도 남은 목숨 수를 유지한다.
        PlaceIcons();
    }

    /// <summary>카메라가 비추는 영역의 왼쪽 위부터 아이콘을 가로로 늘어놓는다.</summary>
    void PlaceIcons()
    {
        if (icons == null || icons.Length == 0 || sprite == null) return;

        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null || !cam.orthographic) return;

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        Vector3 cameraCenter = cam.transform.position;

        // 왼쪽 위 모서리에서 여백만큼 들어온 자리가 첫 아이콘의 바깥쪽 끝이다.
        float left = cameraCenter.x - halfWidth + margin.x;
        float top = cameraCenter.y + halfHeight - margin.y;
        float step = iconSize * (1f + spacing);

        var scale = SpriteMaterial.FitScale(sprite, iconSize);

        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i] == null) continue;

            icons[i].transform.position = new Vector3(left + iconSize * 0.5f + i * step, top - iconSize * 0.5f, 0f);
            icons[i].transform.localScale = scale;
            icons[i].sortingOrder = sortingOrder;
        }
    }

    void OnDestroy()
    {
        if (iconMaterial != null) Destroy(iconMaterial);
        if (iconRoot != null) Destroy(iconRoot.gameObject);
    }
}
