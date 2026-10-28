using UnityEngine;

/// <summary>
/// (테스트용) 노래 시계를 보고 박마다 화면을 깜빡인다.
/// 노래 시계가 박자와 맞는지, 슬로우 뒤에도 어긋나지 않는지 눈으로 확인하는 용도다.
///
/// 깜빡임 밝기를 "이번 박이 시작된 뒤 얼마나 지났는지"로 직접 계산한다.
/// 그래서 프레임이 튀거나 슬로우가 걸려도 노래 위치와 정확히 맞는다.
/// </summary>
public class BeatFlash : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("비워 두면 씬에서 SongClock을 찾아 쓴다.")]
    public SongClock clock;

    [Tooltip("비워 두면 Main Camera를 쓴다.")]
    public Camera targetCamera;

    [Header("박자")]
    [Tooltip("노래의 BPM (1분에 몇 박인지)")]
    [Min(1f)] public float bpm = 120f;

    [Tooltip("첫 박이 나오는 노래 위치 (초). 깜빡임이 박보다 빠르면 키우고, 늦으면 줄인다.")]
    public float offset = 0f;

    [Header("깜빡임")]
    public Color color = Color.white;

    [Tooltip("박이 시작될 때의 밝기")]
    [Range(0f, 1f)] public float maxAlpha = 0.2f;

    [Tooltip("깜빡임이 사라지기까지 걸리는 길이 (한 박 대비). 0.5면 반 박 동안 사라진다.")]
    [Range(0.05f, 1f)] public float fadeLength = 0.5f;

    [Tooltip("그리는 순서. 격자 선(-10)보다 낮게 둬서 격자와 하트를 가리지 않는다.")]
    public int sortingOrder = -20;

    /// <summary>지금 몇 번째 박인지 (첫 박이 0). 첫 박 전이면 -1.</summary>
    public int CurrentBeat { get; private set; } = -1;

    SpriteRenderer flash;

    void Awake()
    {
        if (clock == null) clock = FindAnyObjectByType<SongClock>();

        // 화면 전체를 덮는 사각형. 카메라를 따라가야 하므로 어디에도 붙이지 않는다.
        flash = ShapeSprites.CreateSquare("BeatFlash", null, color, sortingOrder);
    }

    void LateUpdate()
    {
        FitToCamera();

        float alpha = 0f;
        CurrentBeat = -1;

        if (clock != null && clock.IsPlaying)
        {
            float beatLength = 60f / bpm;
            float beats = (clock.SongTime - offset) / beatLength;

            if (beats >= 0f)
            {
                CurrentBeat = Mathf.FloorToInt(beats);
                float sinceBeat = beats - CurrentBeat;   // 이번 박이 시작된 뒤 지난 정도 (0~1 박)

                // 박이 시작될 때 가장 밝고 빠르게 어두워진다.
                float fade = Mathf.Clamp01(1f - sinceBeat / fadeLength);
                alpha = maxAlpha * fade * fade;
            }
        }

        var c = ScreenInvert.Apply(color);   // 무적 중 화면 반전이면 색을 뒤집는다.
        c.a = alpha;
        flash.color = c;
        flash.sortingOrder = sortingOrder;
    }

    /// <summary>카메라가 비추는 영역을 꽉 채운다.</summary>
    void FitToCamera()
    {
        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null || !cam.orthographic) return;

        float height = cam.orthographicSize * 2f;
        float width = height * cam.aspect;
        Vector3 center = cam.transform.position;

        flash.transform.position = new Vector3(center.x, center.y, 0f);
        flash.transform.localScale = new Vector3(width, height, 1f);
    }

    void OnDestroy()
    {
        if (flash != null) Destroy(flash.gameObject);
    }
}
