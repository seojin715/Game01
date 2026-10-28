using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 코드에서 쓰는 단순 도형 스프라이트를 만들어 준다.
/// 그림 파일 없이 흰색 도형(정사각형, 원, 고리, 삼각형, 부메랑)을 만들어 여러 곳에서 돌려쓴다.
/// 모든 도형은 크기 1(월드 1단위)이고, 색은 렌더러에서 입힌다.
/// </summary>
public static class ShapeSprites
{
    // 원, 고리, 삼각형을 그릴 텍스처 크기. 경계는 한 픽셀을 여러 번 나눠 검사해 부드럽게 만든다.
    const int ShapeTextureSize = 128;
    const int ShapeSamples = 4;

    // 삼각형 꼭짓점. 뾰족한 끝이 오른쪽(+x)을 향해서, 진행 방향 각도만큼 돌리면 그 방향을 가리킨다.
    static readonly Vector2 TriangleTip = new Vector2(0.45f, 0f);
    static readonly Vector2 TriangleTop = new Vector2(-0.45f, 0.42f);
    static readonly Vector2 TriangleBottom = new Vector2(-0.45f, -0.42f);

    // 부메랑 모양: 뾰족한 머리 하나가 오른쪽(+x)을 향하고, 뒤쪽 가운데가 삼각형으로 파였으며,
    // 양쪽 발끝은 평평하게 잘린 꼭짓점 6개의 다각형이다. (머리부터 둘레를 따라 순서대로)
    static readonly Vector2[] ChevronPoints =
    {
        new Vector2(0.272f, 0f),        // 머리
        new Vector2(-0.272f, 0.45f),    // 위쪽 발 바깥
        new Vector2(-0.272f, 0.157f),   // 위쪽 발 안쪽
        new Vector2(-0.084f, 0f),       // 뒤쪽 파인 곳
        new Vector2(-0.272f, -0.157f),  // 아래쪽 발 안쪽
        new Vector2(-0.272f, -0.45f),   // 아래쪽 발 바깥
    };

    static Sprite squareSprite;
    static readonly Dictionary<string, Sprite> shapeSprites = new Dictionary<string, Sprite>();
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

    /// <summary>지름이 월드 1단위인 흰 원 (속이 찬 원)</summary>
    public static Sprite Circle => GetOrCreate("Circle", (x, y) => x * x + y * y <= 0.25f);

    /// <summary>
    /// 지름이 월드 1단위인 흰 고리 (속이 빈 원).
    /// thickness는 고리 두께이며 지름 대비 비율이다. (0.1이면 지름의 10%)
    /// </summary>
    public static Sprite Ring(float thickness)
    {
        thickness = Mathf.Clamp(thickness, 0.01f, 0.5f);
        float inner = 0.5f - thickness;
        return GetOrCreate($"Ring_{Mathf.RoundToInt(thickness * 1000f)}", (x, y) =>
        {
            float distance = Mathf.Sqrt(x * x + y * y);
            return distance <= 0.5f && distance >= inner;
        });
    }

    /// <summary>오른쪽을 가리키는 흰 삼각형 (속이 찬 삼각형). 크기 1 상자 안에 들어간다.</summary>
    public static Sprite Triangle => GetOrCreate("Triangle", (x, y) => InsideTriangle(new Vector2(x, y)));

    /// <summary>
    /// 오른쪽을 가리키는 흰 삼각형 테두리 (속이 빈 삼각형).
    /// thickness는 테두리 두께이며 상자 크기 대비 비율이다.
    /// </summary>
    public static Sprite TriangleOutline(float thickness)
    {
        thickness = Mathf.Clamp(thickness, 0.01f, 0.3f);
        return GetOrCreate($"TriangleOutline_{Mathf.RoundToInt(thickness * 1000f)}", (x, y) =>
        {
            var p = new Vector2(x, y);
            if (!InsideTriangle(p)) return false;

            // 세 변 중 가장 가까운 변까지의 거리가 두께보다 가까우면 테두리
            float nearest = Mathf.Min(
                DistanceToLine(p, TriangleTip, TriangleTop),
                DistanceToLine(p, TriangleTop, TriangleBottom),
                DistanceToLine(p, TriangleBottom, TriangleTip));
            return nearest <= thickness;
        });
    }

    /// <summary>머리가 오른쪽을 가리키는 흰 부메랑 모양 (속이 찬 모양). 크기 1 상자 안에 들어간다.</summary>
    public static Sprite Chevron => GetOrCreate("Boomerang", (x, y) => InsideChevron(new Vector2(x, y)));

    /// <summary>
    /// 머리가 오른쪽을 가리키는 흰 부메랑 테두리 (속이 빈 모양).
    /// thickness는 테두리 두께이며 상자 크기 대비 비율이다.
    /// </summary>
    public static Sprite ChevronOutline(float thickness)
    {
        thickness = Mathf.Clamp(thickness, 0.01f, 0.2f);
        return GetOrCreate($"BoomerangOutline_{Mathf.RoundToInt(thickness * 1000f)}", (x, y) =>
        {
            var p = new Vector2(x, y);
            if (!InsideChevron(p)) return false;

            // 둘레의 변 중 가장 가까운 변까지의 거리가 두께보다 가까우면 테두리
            // (오목한 모양이라 무한 직선이 아니라 선분까지의 거리로 잰다)
            float nearest = float.MaxValue;
            for (int i = 0; i < ChevronPoints.Length; i++)
            {
                Vector2 from = ChevronPoints[i];
                Vector2 to = ChevronPoints[(i + 1) % ChevronPoints.Length];
                nearest = Mathf.Min(nearest, DistanceToSegment(p, from, to));
            }
            return nearest <= thickness;
        });
    }

    /// <summary>
    /// 부메랑 모양 안인지 검사한다. 머리에서 다른 꼭짓점들로 부채꼴처럼 나눈 삼각형 중 하나에 들어 있으면 안이다.
    /// (머리에서는 모든 꼭짓점이 막힘없이 보여서 이렇게 나눌 수 있다)
    /// </summary>
    static bool InsideChevron(Vector2 p)
    {
        for (int i = 1; i < ChevronPoints.Length - 1; i++)
        {
            if (InsideTri(p, ChevronPoints[0], ChevronPoints[i], ChevronPoints[i + 1])) return true;
        }
        return false;
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    static bool InsideTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(b - a, p - a);
        float d2 = Cross(c - b, p - b);
        float d3 = Cross(a - c, p - c);
        return (d1 >= 0f && d2 >= 0f && d3 >= 0f) || (d1 <= 0f && d2 <= 0f && d3 <= 0f);
    }

    static bool InsideTriangle(Vector2 p)
    {
        float a = Cross(TriangleTop - TriangleTip, p - TriangleTip);
        float b = Cross(TriangleBottom - TriangleTop, p - TriangleTop);
        float c = Cross(TriangleTip - TriangleBottom, p - TriangleBottom);
        return (a >= 0f && b >= 0f && c >= 0f) || (a <= 0f && b <= 0f && c <= 0f);
    }

    static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

    static float DistanceToLine(Vector2 p, Vector2 a, Vector2 b) => Mathf.Abs(Cross(b - a, p - a)) / (b - a).magnitude;

    /// <summary>같은 도형은 한 번만 그리고 돌려쓴다.</summary>
    static Sprite GetOrCreate(string key, Func<float, float, bool> isInside)
    {
        if (!shapeSprites.TryGetValue(key, out var sprite) || sprite == null)
        {
            sprite = Rasterize("Runtime" + key, isInside);
            shapeSprites[key] = sprite;
        }
        return sprite;
    }

    /// <summary>
    /// 도형 판정 함수(좌표 -0.5 ~ 0.5)를 받아 흰색 + 알파 텍스처로 그린다.
    /// 한 픽셀을 여러 점으로 나눠 검사해서 경계를 부드럽게 만든다.
    /// </summary>
    static Sprite Rasterize(string name, Func<float, float, bool> isInside)
    {
        int size = ShapeTextureSize;
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < ShapeSamples; sy++)
                {
                    for (int sx = 0; sx < ShapeSamples; sx++)
                    {
                        float px = (x + (sx + 0.5f) / ShapeSamples) / size - 0.5f;
                        float py = (y + (sy + 0.5f) / ShapeSamples) / size - 0.5f;
                        if (isInside(px, py)) hits++;
                    }
                }

                byte alpha = (byte)(255 * hits / (ShapeSamples * ShapeSamples));
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        texture.SetPixels32(pixels);
        texture.Apply();

        // pixelsPerUnit을 size로 두면 스프라이트 한 장이 월드 1단위가 된다.
        var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = name;
        return sprite;
    }

    /// <summary>주어진 도형 스프라이트로 SpriteRenderer를 가진 오브젝트를 새로 만든다.</summary>
    public static SpriteRenderer CreateRenderer(string name, Transform parent, Sprite sprite, Color color, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        ApplyUnlit(renderer);
        return renderer;
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
        return CreateRenderer(name, parent, Square, color, sortingOrder);
    }

    /// <summary>스프라이트가 조명 때문에 어두워지지 않도록 Unlit 머티리얼을 적용한다.</summary>
    public static void ApplyUnlit(SpriteRenderer renderer)
    {
        var material = UnlitMaterial;
        if (material != null) renderer.sharedMaterial = material;
    }
}
