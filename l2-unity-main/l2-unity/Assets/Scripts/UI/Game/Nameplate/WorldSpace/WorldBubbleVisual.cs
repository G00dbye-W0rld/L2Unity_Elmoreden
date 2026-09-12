using UnityEngine;

// Habillage commun des encarts world-space : bulle de chat et nom de magasin.
// Le fond est decoupe en 9 morceaux, si bien que les coins arrondis gardent
// leur taille quand l'encart s'elargit, au lieu de s'etirer.
public static class WorldBubbleVisual
{
    public const float CornerSize = 0.05f;
    public const float TailWidth = 0.075f;
    public const float TailHeight = 0.05f;

    private const int TextureSize = 64;
    private const int TextureCorner = 18;
    private const float EdgeSoftness = 1.6f;

    private static Texture2D _texture;

    public static Texture2D Texture
    {
        get
        {
            if (_texture == null)
            {
                _texture = BuildTexture();
            }
            return _texture;
        }
    }

    public static Material CreateMaterial(Material source, int renderQueue)
    {
        // Le materiau de l'icone de nameplate sert de modele : lui est deja
        // transparent et visible en jeu (cf. BuildTransparentQuadMaterial).
        Material material = source != null
            ? new Material(source)
            : new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        material.name = "WorldBubble";
        material.SetTexture("_BaseMap", Texture);
        material.SetTexture("_MainTex", Texture);
        material.renderQueue = renderQueue;
        return material;
    }

    // Fond arrondi : 16 sommets, 9 quads. Les bords et les coins prennent
    // leur part de la texture, seul le centre est etire.
    public static void UpdateSlicedMesh(Mesh mesh, float width, float height)
    {
        float corner = Mathf.Min(CornerSize, Mathf.Min(width, height) * 0.5f);
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;

        float[] xs = { -halfWidth, -halfWidth + corner, halfWidth - corner, halfWidth };
        float[] ys = { -halfHeight, -halfHeight + corner, halfHeight - corner, halfHeight };
        float border = TextureCorner / (float)TextureSize;
        float[] uvs1D = { 0f, border, 1f - border, 1f };

        Vector3[] vertices = new Vector3[16];
        Vector2[] uvs = new Vector2[16];
        for (int iy = 0; iy < 4; iy++)
        {
            for (int ix = 0; ix < 4; ix++)
            {
                int i = iy * 4 + ix;
                vertices[i] = new Vector3(xs[ix], ys[iy], 0f);
                uvs[i] = new Vector2(uvs1D[ix], uvs1D[iy]);
            }
        }

        int[] triangles = new int[9 * 6];
        int t = 0;
        for (int iy = 0; iy < 3; iy++)
        {
            for (int ix = 0; ix < 3; ix++)
            {
                int i = iy * 4 + ix;
                triangles[t++] = i;
                triangles[t++] = i + 5;
                triangles[t++] = i + 4;
                triangles[t++] = i;
                triangles[t++] = i + 1;
                triangles[t++] = i + 5;
            }
        }

        Apply(mesh, vertices, uvs, triangles);
    }

    // Pointe sous la bulle. Elle echantillonne le centre de la texture, donc
    // une zone pleine, et garde une taille fixe.
    public static void UpdateTailMesh(Mesh mesh)
    {
        Vector3[] vertices =
        {
            new Vector3(-TailWidth * 0.5f, 0f, 0f),
            new Vector3(0f, -TailHeight, 0f),
            new Vector3(TailWidth * 0.5f, 0f, 0f)
        };

        Vector2[] uvs = { new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f) };
        int[] triangles = { 0, 1, 2 };

        Apply(mesh, vertices, uvs, triangles);
    }

    private static void Apply(Mesh mesh, Vector3[] vertices, Vector2[] uvs, int[] triangles)
    {
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private static Texture2D BuildTexture()
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = "WorldBubbleRounded",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float distance = RoundedRectDistance(x + 0.5f, y + 0.5f);
                float alpha = Mathf.Clamp01(0.5f - distance / EdgeSoftness);
                pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    // Distance signee au bord du rectangle arrondi, en pixels : negative a
    // l'interieur, positive dehors.
    private static float RoundedRectDistance(float x, float y)
    {
        float half = TextureSize * 0.5f;
        float dx = Mathf.Abs(x - half) - half + TextureCorner;
        float dy = Mathf.Abs(y - half) - half + TextureCorner;
        float outsideX = Mathf.Max(dx, 0f);
        float outsideY = Mathf.Max(dy, 0f);
        return Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY) + Mathf.Min(Mathf.Max(dx, dy), 0f) - TextureCorner;
    }
}
