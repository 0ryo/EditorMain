using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class UiRoundedTheme
{
    const string GeneratedSpriteNamePrefix = "__runtime_rounded_sprite_r";
    const string CircleSpriteName = "__runtime_circle_sprite";
    static readonly Dictionary<int, Sprite> SpriteCache = new Dictionary<int, Sprite>();
    static Sprite circleSprite;

    public static void ApplyToHierarchy(Transform root, float cornerRadius = DesignTokens.CornerRadius)
    {
        if (root == null) return;

        int radius = Mathf.Clamp(Mathf.RoundToInt(cornerRadius), 2, 64);
        var roundedSprite = GetOrCreateRoundedSprite(radius);
        if (roundedSprite == null) return;

        var images = root.GetComponentsInChildren<Image>(true);
        foreach (var image in images)
        {
            if (image == null) continue;
            if (!ShouldApply(image)) continue;

            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            image.useSpriteMesh = false;
            image.SetAllDirty();
        }
    }

    /// <summary>
    /// 指定した Image を完全な円形にする。コネクタや削除ボタンに使用。
    /// </summary>
    public static void ApplyCircleToElement(Image image)
    {
        if (image == null) return;

        var sprite = GetOrCreateCircleSprite();
        if (sprite == null) return;

        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.useSpriteMesh = false;
        image.SetAllDirty();
    }

    static bool ShouldApply(Image image)
    {
        if (image == null) return false;

        // コネクタと削除ボタンは円形にするため、角丸は適用しない
        var name = image.gameObject.name;
        if (name == "InputConnector" || name == "OutputConnector") return false;
        if (name == "Button_Delete" || name == "Button_Delete_Runtime") return false;
        if (name == "Button_RemoveCard" || name == "Button_RemoveCard_Runtime") return false;
        if (name == "Divider") return false; // 埋め込み条件間の区切り線には角丸を適用しない

        return true;
    }

    static Sprite GetOrCreateCircleSprite()
    {
        if (circleSprite != null) return circleSprite;

        int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = CircleSpriteName;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;

        var pixels = new Color[size * size];
        float center = size * 0.5f;
        float radiusF = center;

        for (int y = 0; y < size; y++)
        {
            float py = y + 0.5f - center;
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - center;
                float distance = Mathf.Sqrt(px * px + py * py);
                float alpha = Mathf.Clamp01(radiusF - distance + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        circleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f);

        circleSprite.name = CircleSpriteName;
        circleSprite.hideFlags = HideFlags.HideAndDontSave;
        return circleSprite;
    }

    static Sprite GetOrCreateRoundedSprite(int radius)
    {
        if (SpriteCache.TryGetValue(radius, out var cached) && cached != null)
        {
            return cached;
        }

        int size = Mathf.Max(128, radius * 8);
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = $"{GeneratedSpriteNamePrefix}{radius}";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;

        var pixels = new Color[size * size];
        float radiusF = radius;
        float left = radiusF;
        float right = size - radiusF;
        float bottom = radiusF;
        float top = size - radiusF;

        for (int y = 0; y < size; y++)
        {
            float py = y + 0.5f;
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;

                float dx = 0f;
                if (px < left) dx = left - px;
                else if (px > right) dx = px - right;

                float dy = 0f;
                if (py < bottom) dy = bottom - py;
                else if (py > top) dy = py - top;

                float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                float alpha = Mathf.Clamp01(radiusF + 0.5f - distance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        var border = new Vector4(radius, radius, radius, radius);
        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            border);

        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;

        SpriteCache[radius] = sprite;
        return sprite;
    }
}
