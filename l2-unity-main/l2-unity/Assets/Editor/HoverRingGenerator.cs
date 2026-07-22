#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// Genere l'anneau au sol affiche au survol/ciblage d'un PNJ/monstre (cf.
// HoverGroundRing.cs). Meme esprit que WorldNameplatePrefabGenerator.cs.
//
// Un materiau/texture PAR ETAT (Hover/Target/Attack) prepare a la main -
// depose ton PNG directement au chemin RingXxxSrc correspondant ci-dessous
// avant de lancer ce menu. Tant qu'un fichier n'existe pas encore, un
// anneau procedural de secours (liseré + graduations) est genere a sa
// place, pour ne rien casser pendant que tu prepares les textures - il
// suffit de relancer ce menu une fois le PNG dispose pour qu'il prenne le
// relai automatiquement.
//
// Premier essai (abandonne) : materiau construit entierement au runtime
// (new Material + SetFloat, jamais sauvegarde comme asset) - s'affichait en
// CARRE OPAQUE au lieu d'un anneau transparent, la transparence ne prenait
// pas. La recette qui marche reellement dans ce projet (bulles/gauge de
// nameplate, cf. WorldNameplatePrefabGenerator.GetOrCreateQuadMaterial) sauve
// TOUJOURS le materiau comme asset .mat via AssetDatabase.CreateAsset avant
// de l'utiliser sur un prefab - reproduite ici a l'identique, en Unlit (pas
// Lit : un anneau de selection doit garder une couleur constante quel que
// soit l'eclairage de la scene, cf. note sur GetOrCreateQuadMaterial).
public class HoverRingGenerator
{
    const int TextureSize = 256;

    // Anneau procedural DE SECOURS (utilise tant qu'un PNG prepare a la main
    // n'existe pas encore au chemin correspondant) : liseré externe fin +
    // anneau principal + liseré interne fin, relies par des graduations
    // radiales (facon cadran/rune).
    const float OuterRingStart = 0.95f;
    const float OuterRingEnd = 1.0f;
    const float MainRingStart = 0.72f;
    const float MainRingEnd = 0.86f;
    const float InnerRingStart = 0.58f;
    const float InnerRingEnd = 0.62f;
    const float EdgeSoftness = 0.02f;
    const int TickCount = 12;
    const float TickHalfWidthRad = 0.05f;

    const string OutDir = "Assets/Resources/Data/UI/Assets/HoverRing";

    // Chemins des 3 PNG a preparer a la main (memes dimensions/style qu'un
    // seul dessin recolore, ou trois dessins distincts - au choix). RGBA,
    // fond transparent.
    const string RingHoverSrc = OutDir + "/RingHover.png";
    const string RingTargetSrc = OutDir + "/RingTarget.png";
    const string RingAttackSrc = OutDir + "/RingAttack.png";

    const string PrefabOutDir = "Assets/Resources/Prefab/Game";
    const string PrefabPath = PrefabOutDir + "/HoverRing.prefab";

    [MenuItem("Tools/L2Unity/Highlight/Generate HoverRing Prefab")]
    static void Generate()
    {
        EnsureFolder(OutDir);
        EnsureFolder(PrefabOutDir);

        Material hoverMat = GetOrCreateRingMaterial(RingHoverSrc);
        Material targetMat = GetOrCreateRingMaterial(RingTargetSrc);
        Material attackMat = GetOrCreateRingMaterial(RingAttackSrc);

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "HoverRing";
        // A plat au sol, face vers le haut (le Quad natif fait face a +Z
        // local) - sans incidence sur la visibilite (materiau double face,
        // _Cull=0), mais coherent avec Vector3.up.
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        Object.DestroyImmediate(go.GetComponent<Collider>());

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = hoverMat; // etat par defaut, ClickManager choisit le bon a l'usage
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[HoverRingGenerator] Prefab genere: {PrefabPath} (materiaux: {RingHoverSrc}, {RingTargetSrc}, {RingAttackSrc}).");
    }

    // Materiau Unlit transparent pour l'etat dont la texture source est
    // "srcPath" (ex: RingHover.png -> RingHover.mat, meme dossier). Si le
    // PNG n'existe pas encore, genere un anneau procedural de secours au
    // meme endroit - relancer ce menu une fois le vrai PNG dispose le
    // remplacera automatiquement (le fichier existe alors, plus besoin de
    // regenerer la texture, seul le materiau/prefab est reconstruit).
    static Material GetOrCreateRingMaterial(string texturePath)
    {
        Texture2D texture = GetOrCreateTexture(texturePath);
        string materialPath = Path.ChangeExtension(texturePath, ".mat");

        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        bool isNew = material == null;
        if (isNew)
        {
            material = new Material(unlitShader);
        }
        else if (material.shader != unlitShader)
        {
            material.shader = unlitShader;
        }

        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetTexture("_BaseMap", texture);

        if (isNew)
        {
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            EditorUtility.SetDirty(material);
        }
        return material;
    }

    // Charge le PNG s'il existe deja (prepare a la main - reimport avec les
    // bons parametres au cas ou), sinon genere l'anneau procedural de
    // secours a cet emplacement precis.
    static Texture2D GetOrCreateTexture(string path)
    {
        if (File.Exists(path))
        {
            AssetDatabase.ImportAsset(path);
            ApplyTextureImportSettings(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        return GenerateProceduralRingTexture(path);
    }

    static void ApplyTextureImportSettings(string path)
    {
        TextureImporter importer = (TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
        if (importer == null) return;

        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    // Bande annulaire avec bords adoucis : alpha 1 entre [start,end], degrade
    // sur "softness" de part et d'autre.
    static float RingBand(float dist, float start, float end, float softness)
    {
        float outer = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(end - softness, end, dist));
        float inner = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, start + softness, dist));
        return Mathf.Min(inner, outer);
    }

    // Texture procedurale DE SECOURS (liseré externe + anneau principal +
    // liseré interne + graduations radiales, facon cadran/rune) sauvegardee
    // comme vrai asset PNG importe - evite de dependre d'une texture
    // existante et garantit les bons parametres d'import (alpha =
    // transparence) tant que le PNG prepare a la main n'est pas encore la.
    static Texture2D GenerateProceduralRingTexture(string path)
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[TextureSize * TextureSize];
        float center = (TextureSize - 1) * 0.5f;
        float maxRadius = TextureSize * 0.5f;
        float twoPi = Mathf.PI * 2f;
        float tickStep = twoPi / TickCount;

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float dx = (x - center) / maxRadius;
                float dy = (y - center) / maxRadius;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float outerRing = RingBand(dist, OuterRingStart, OuterRingEnd, EdgeSoftness);
                float mainRing = RingBand(dist, MainRingStart, MainRingEnd, EdgeSoftness);
                float innerRing = RingBand(dist, InnerRingStart, InnerRingEnd, EdgeSoftness);

                float tickAlpha = 0f;
                if (dist > InnerRingEnd && dist < MainRingStart)
                {
                    float angle = Mathf.Atan2(dy, dx);
                    if (angle < 0f) angle += twoPi;
                    float nearestTick = Mathf.Round(angle / tickStep) * tickStep;
                    float diff = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, nearestTick * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    tickAlpha = diff < TickHalfWidthRad ? 1f : 0f;
                }

                float alpha = Mathf.Max(Mathf.Max(outerRing, mainRing), Mathf.Max(innerRing, tickAlpha));
                alpha *= dist <= 1f ? 1f : 0f;

                pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        byte[] png = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);
        File.WriteAllBytes(path, png);
        AssetDatabase.ImportAsset(path);
        ApplyTextureImportSettings(path);

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
