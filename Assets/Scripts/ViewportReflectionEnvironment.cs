using UnityEngine;

// Small, generated HDR studio environment. No scene capture or per-frame convolution.
public static class ViewportReflectionEnvironment
{
    const int Size = 64;
    const int Samples = 64;
    static readonly Vector3 Key = new Vector3(-0.4f, 0.8f, 0.45f).normalized;
    static readonly Vector3 Fill = new Vector3(0.8f, 0.25f, -0.5f).normalized;

    public static Cubemap Create()
    {
        var cube = new Cubemap(Size, TextureFormat.RGBAHalf, true)
        {
            name = "Viewport Studio Reflection", hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp
        };
        for (int mip = 0; mip < cube.mipmapCount; mip++)
        {
            int size = Mathf.Max(1, Size >> mip);
            // Invert URP's perceptual-roughness-to-mip remapping.
            float fraction = (float)mip / (cube.mipmapCount - 1);
            float roughness = (1.7f - Mathf.Sqrt(2.89f - 2.8f * fraction)) / 1.4f;
            for (int face = 0; face < 6; face++)
            {
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    var direction = Direction(face, 2f * (x + 0.5f) / size - 1, 2f * (y + 0.5f) / size - 1);
                    pixels[y * size + x] = mip == 0 ? Radiance(direction) : Filter(direction, roughness);
                }
                cube.SetPixels(pixels, (CubemapFace)face, mip);
            }
        }
        cube.Apply(false, true); // Keep the GGX-filtered mips; release the CPU copy.
        return cube;
    }

    static Vector3 Direction(int face, float u, float v)
    {
        switch ((CubemapFace)face)
        {
            case CubemapFace.PositiveX: return new Vector3(1, -v, -u).normalized;
            case CubemapFace.NegativeX: return new Vector3(-1, -v, u).normalized;
            case CubemapFace.PositiveY: return new Vector3(u, 1, v).normalized;
            case CubemapFace.NegativeY: return new Vector3(u, -1, -v).normalized;
            case CubemapFace.PositiveZ: return new Vector3(u, -v, 1).normalized;
            default: return new Vector3(-u, -v, -1).normalized;
        }
    }

    static Color Radiance(Vector3 direction)
    {
        float sky = 0.18f + 0.28f * (direction.y * 0.5f + 0.5f);
        float key = 4f * Mathf.Pow(Mathf.Max(0, Vector3.Dot(direction, Key)), 32);
        float fill = 1.5f * Mathf.Pow(Mathf.Max(0, Vector3.Dot(direction, Fill)), 12);
        return new Color(sky + key + fill * 0.9f, sky + key * 0.98f + fill * 0.95f, sky + key * 0.95f + fill, 1);
    }

    static Color Filter(Vector3 normal, float roughness)
    {
        var tangent = Vector3.Cross(Mathf.Abs(normal.y) < 0.99f ? Vector3.up : Vector3.right, normal).normalized;
        var bitangent = Vector3.Cross(normal, tangent);
        float alpha = roughness * roughness;
        Color sum = Color.clear;
        float weight = 0;
        for (int i = 0; i < Samples; i++)
        {
            float phi = 2 * Mathf.PI * i / Samples;
            uint bits = (uint)i;
            float radical = 0, place = 0.5f;
            while (bits != 0) { radical += (bits & 1) * place; bits >>= 1; place *= 0.5f; }
            float cos = Mathf.Sqrt((1 - radical) / (1 + (alpha * alpha - 1) * radical));
            float sin = Mathf.Sqrt(Mathf.Max(0, 1 - cos * cos));
            var half = tangent * (Mathf.Cos(phi) * sin) + bitangent * (Mathf.Sin(phi) * sin) + normal * cos;
            var light = 2 * Vector3.Dot(normal, half) * half - normal;
            float nDotL = Mathf.Max(0, Vector3.Dot(normal, light));
            if (nDotL <= 0) continue;
            sum += Radiance(light) * nDotL; weight += nDotL;
        }
        Color result = sum / Mathf.Max(weight, 0.00001f);
        result.a = 1;
        return result;
    }
}
