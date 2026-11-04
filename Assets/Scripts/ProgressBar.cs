using UnityEngine;

/// <summary>
/// 화면 맨 위에 노래 진행도를 가로 막대 하나로 보여 준다.
///
/// 옅은 바탕 막대 위로, 채워진 막대가 왼쪽부터 노래 재생 위치(SongClock.SongTime / Length)만큼 늘어난다.
/// 준비 시간(시계가 음수)에는 비어 있고, 카운트다운 중에는 숨겼다가 끝나면 서서히 나타난다.
/// 카메라가 비추는 영역을 기준으로 매 프레임 자리를 잡아서, 격자가 커져 카메라가 물러나도 화면 맨 위에 남는다.
/// 막대는 이 컴포넌트가 붙은 오브젝트의 자식이 아닌 별도 오브젝트 아래에 만든다. (붙인 오브젝트의 크기를 따라가지 않게)
/// </summary>
public class ProgressBar : MonoBehaviour
{
    [Header("모양 (월드 단위)")]
    [Tooltip("막대 두께")]
    [Min(0.005f)] public float height = 0.06f;

    [Tooltip("화면 맨 위에서 막대까지 거리")]
    [Min(0f)] public float topMargin = 0.18f;

    [Tooltip("화면 왼쪽·오른쪽 끝에서 막대 끝까지 거리")]
    [Min(0f)] public float sideMargin = 0.5f;

    [Header("색과 빛")]
    public Color color = Color.white;

    [Tooltip("바탕 막대(아직 지나지 않은 부분)의 불투명도")]
    [Range(0f, 1f)] public float trackAlpha = 0.2f;

    [Tooltip("채워진 막대의 밝기. 1보다 크면 Bloom이 번져서 빛난다.")]
    [Range(1f, 6f)] public float glowIntensity = 1.5f;

    [Tooltip("카운트다운이 끝난 뒤 막대가 서서히 나타나는 시간 (초)")]
    [Min(0.01f)] public float fadeInTime = 0.3f;

    [Tooltip("그리는 순서. 격자와 장애물보다 높게 둔다.")]
    public int sortingOrder = 50;

    [Header("참조 (비워 두면 씬에서 찾는다)")]
    public SongClock clock;

    [Tooltip("비워 두면 Main Camera를 쓴다.")]
    public Camera targetCamera;

    static readonly int ColorProperty = Shader.PropertyToID("_Color");

    Transform root;
    SpriteRenderer track;      // 바탕 막대 (화면 폭 전체)
    SpriteRenderer fill;       // 채워진 막대 (지나온 만큼)
    Material fillMaterial;     // 채워진 막대를 1보다 밝게 그리는 머티리얼
    float shown;               // 나타난 정도 (0~1)

    void Awake()
    {
        if (clock == null) clock = FindAnyObjectByType<SongClock>();

        root = new GameObject("ProgressBar").transform;
        track = ShapeSprites.CreateSquare("Track", root, color, sortingOrder);
        fill = ShapeSprites.CreateSquare("Fill", root, color, sortingOrder + 1);

        // 렌더러 색은 1을 넘을 수 없으므로, 채워진 막대의 밝기는 머티리얼 색에 넣는다.
        fillMaterial = SpriteMaterial.Create("ProgressBarFill");
        if (fillMaterial != null) fill.sharedMaterial = fillMaterial;

        track.enabled = false;
        fill.enabled = false;
    }

    void LateUpdate()
    {
        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null || !cam.orthographic || clock == null)
        {
            track.enabled = false;
            fill.enabled = false;
            return;
        }

        // 카운트다운 중(노래를 불러오는 동안 포함)에는 숨기고, 끝나면 서서히 나타난다.
        bool show = clock.IsLoaded && !Countdown.IsActive;
        shown = show ? Mathf.Min(1f, shown + Time.unscaledDeltaTime / fadeInTime) : 0f;
        float alpha = Mathf.SmoothStep(0f, 1f, shown);

        // 지나온 비율. 준비 시간(음수)에는 0이다.
        float progress = clock.Length > 0f ? Mathf.Clamp01(clock.SongTime / clock.Length) : 0f;

        // 카메라가 비추는 영역의 맨 위에, 양옆 여백을 두고 놓는다.
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        Vector3 center = cam.transform.position;
        float left = center.x - halfWidth + sideMargin;
        float width = Mathf.Max(0f, halfWidth * 2f - sideMargin * 2f);
        float y = center.y + halfHeight - topMargin - height * 0.5f;

        track.transform.position = new Vector3(left + width * 0.5f, y, 0f);
        track.transform.localScale = new Vector3(width, height, 1f);
        fill.transform.position = new Vector3(left + width * progress * 0.5f, y, 0f);
        fill.transform.localScale = new Vector3(width * progress, height, 1f);

        // 무적 중 화면 반전이면 다른 흰 요소처럼 색을 뒤집는다.
        Color c = ScreenInvert.Apply(color);
        track.color = new Color(c.r, c.g, c.b, trackAlpha * alpha);
        fill.color = new Color(c.r, c.g, c.b, alpha);
        track.enabled = alpha > 0f;
        fill.enabled = alpha > 0f && progress > 0f;
        track.sortingOrder = sortingOrder;
        fill.sortingOrder = sortingOrder + 1;

        if (fillMaterial != null) fillMaterial.SetColor(ColorProperty, SpriteMaterial.Glow(Color.white, glowIntensity));
    }

    void OnDestroy()
    {
        if (fillMaterial != null) Destroy(fillMaterial);
        if (root != null) Destroy(root.gameObject);
    }
}
