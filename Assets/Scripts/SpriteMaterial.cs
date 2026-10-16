using UnityEngine;

/// <summary>
/// 스프라이트를 빛나게 그리기 위한 공용 도구.
/// 하트와 HP 표시가 같은 방법으로 머티리얼과 크기를 다룬다.
/// </summary>
public static class SpriteMaterial
{
    /// <summary>2D 조명에 영향받지 않는 스프라이트 머티리얼을 새로 만든다.</summary>
    public static Material Create(string name)
    {
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            Debug.LogError("SpriteMaterial: 스프라이트 셰이더를 찾지 못했습니다.");
            return null;
        }
        return new Material(shader) { name = name };
    }

    /// <summary>
    /// Bloom이 번지도록 1보다 밝게 만든 색.
    /// 렌더러의 color는 1을 넘을 수 없어서 이 값은 머티리얼에 넣어야 한다.
    /// </summary>
    public static Color Glow(Color color, float intensity)
    {
        Color glow = color * intensity;
        glow.a = color.a;   // 투명도는 밝기와 상관없이 그대로 둔다.
        return glow;
    }

    /// <summary>스프라이트의 긴 쪽이 targetSize가 되도록 하는 스케일을 구한다.</summary>
    public static Vector3 FitScale(Sprite sprite, float targetSize)
    {
        if (sprite == null) return Vector3.one;

        float spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        float scale = spriteSize > 0f ? targetSize / spriteSize : 1f;
        return new Vector3(scale, scale, 1f);
    }
}
