#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// Premier jet PROCEDURAL de la future icone unique de nameplate (etoile +
// joyau central + une branche tres longue et HORIZONTALE servant de
// separateur nom/titre - cf. discussion : remplace les deux bulles
// gauche/droite actuelles par une seule, a gauche du nom, positionnee dans
// l'ecart Titre/Nom). Genere uniquement la texture PNG en asset reel,
// destinee a etre reprise/retravaillee a la main sous Photoshop - PAS
// ENCORE CABLEE dans WorldNameplatePrefabGenerator/WorldNameplate.cs, qui
// restent inchanges pour l'instant (a part l'ecart Titre/Nom agrandi en
// prevision, cf. WorldNameplatePrefabGenerator.cs).
//
// Canevas en PAYSAGE (pas carre) : l'etoile (corps compact) est placee pres
// du bord gauche, la branche longue s'etend HORIZONTALEMENT vers la droite
// sur la majeure partie de la largeur - pensee pour passer entre Titre
// (au-dessus) et Nom (en dessous) dans le nameplate, comme un trait
// separateur.
public class NameplateBubbleIconGenerator
{
    const int TextureWidth = 480;
    const int TextureHeight = 160;

    const int StarPoints = 5;
    const float StarOuterRadius = 0.55f;
    const float StarInnerRadius = 0.22f;
    const float JewelRadius = 0.3f;
    const float EdgeSoftness = 0.03f;

    // Branche longue (separateur), pointe vers la DROITE (+X en espace
    // pixel/texture) - passera entre Titre et Nom dans le nameplate.
    const float BranchLength = 5.2f;
    const float BranchBaseHalfWidth = 0.22f;

    const string OutDir = "Assets/Resources/Data/UI/Assets/NameplateIcon";
    const string TexturePath = OutDir + "/BubbleIcon.png";

    [MenuItem("Tools/L2Unity/Highlight/Generate NameplateBubbleIcon Texture (draft)")]
    static void Generate()
    {
        EnsureFolder(OutDir);

        Texture2D texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[TextureWidth * TextureHeight];

        float cx = TextureWidth * 0.14f;
        float cy = TextureHeight * 0.5f;
        float scale = TextureHeight * 0.5f;

        for (int y = 0; y < TextureHeight; y++)
        {
            for (int x = 0; x < TextureWidth; x++)
            {
                float dx = (x - cx) / scale;
                float dy = (y - cy) / scale;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                float starAlpha = StarAlpha(dist, angle);
                float jewelAlpha = DiscAlpha(dist, JewelRadius);
                float branchAlpha = BranchAlpha(dx, dy);

                float alpha = Mathf.Max(Mathf.Max(starAlpha, jewelAlpha), branchAlpha);

                pixels[y * TextureWidth + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        byte[] png = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);
        File.WriteAllBytes(TexturePath, png);
        AssetDatabase.ImportAsset(TexturePath);

        TextureImporter importer = (TextureImporter)UnityEditor.AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 512;
        importer.SaveAndReimport();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[NameplateBubbleIconGenerator] Texture (brouillon) generee : {TexturePath}. A retravailler sous Photoshop - non encore cablee dans le prefab de nameplate.");
    }

    // Etoile a N branches : rayon module par cos(N * angle), rempli du
    // centre vers ce rayon avec un bord adouci.
    static float StarAlpha(float dist, float angle)
    {
        float t = (Mathf.Cos(StarPoints * angle) + 1f) * 0.5f;
        float radius = Mathf.Lerp(StarInnerRadius, StarOuterRadius, t);
        return Mathf.Clamp01((radius + EdgeSoftness - dist) / EdgeSoftness);
    }

    // Disque plein (joyau central), bord adouci.
    static float DiscAlpha(float dist, float radius)
    {
        return Mathf.Clamp01((radius + EdgeSoftness - dist) / EdgeSoftness);
    }

    // Branche longue en triangle effile : projection le long de l'axe +X
    // (along) et perpendiculaire (perp) depuis le centre de l'etoile -
    // largeur qui retrecit lineairement jusqu'a une pointe au bout de
    // BranchLength.
    static float BranchAlpha(float dx, float dy)
    {
        float along = dx;
        float perp = Mathf.Abs(dy);

        if (along <= 0f || along >= BranchLength) return 0f;

        float widthAtAlong = BranchBaseHalfWidth * (1f - along / BranchLength);
        float edge = widthAtAlong - perp;
        return Mathf.Clamp01(edge / EdgeSoftness);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string name = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
