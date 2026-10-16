using UnityEngine;

/// <summary>
/// 코드에서 쓰는 단순 도형 스프라이트를 만들어 준다.
/// 그림 파일 없이 흰 정사각형 하나를 만들어 격자 선, 하트, 잔상이 함께 돌려쓴다.
/// </summary>
public static class ShapeSprites
{
    static Sprite squareSprite;
    static Material unlitMaterial;

    /// <summary>한 변이 월드 1단위인 흰 정사각형. 스케일을 바꿔서 선이나 도형으로 쓴다.</summary>
    public static Sprite Square
    {
        get
        {
            if (squareSprite == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();

                // pixelsPerUnit을 1로 두면 localScale 1 = 월드 1단위가 된다.
                squareSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                squareSprite.name = "RuntimeSquare";
            }
            return squareSprite;
        }
    }

    /// <summary>2D 조명에 영향받지 않는 머티리얼. 색을 지정한 그대로 보이게 한다.</summary>
    public static Material UnlitMaterial
    {
        get
        {
            if (unlitMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader != null) unlitMaterial = new Material(shader) { name = "RuntimeSpriteUnlit" };
            }
            return unlitMaterial;
        }
    }

    /// <summary>흰 정사각형 SpriteRenderer를 가진 오브젝트를 새로 만든다.</summary>
    public static SpriteRenderer CreateSquare(string name, Transform parent, Color color, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = Square;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        ApplyUnlit(renderer);
        return renderer;
    }

    /// <summary>스프라이트가 조명 때문에 어두워지지 않도록 Unlit 머티리얼을 적용한다.</summary>
    public static void ApplyUnlit(SpriteRenderer renderer)
    {
        var material = UnlitMaterial;
        if (material != null) renderer.sharedMaterial = material;
    }
}
