#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

// Genere le prefab WorldNameplate (texte 3D world-space, alternative au
// systeme UI Toolkit existant - voir le plan de remplacement des
// nameplates). Construit via l'API Unity plutot qu'en ecrivant le YAML du
// prefab a la main, meme esprit que DropItemAssetGenerator.cs.
//
// Nom/titre : TextMeshPro. Icone/jauge : Quad natif + MeshRenderer (pas de
// SpriteRenderer - constate invisible en jeu dans ce projet, cf.
// BuildTransparentQuadMaterial). Les textures de jauge sont dupliquees (pas
// modifiees sur place) dans un dossier dedie, pour ne jamais toucher aux
// textures partagees utilisees ailleurs. L'icone (etoile+joyau) est
// reference directement depuis son propre dossier (deja dediee, generee
// par NameplateBubbleIconGenerator).
//
// A relancer regenere entierement le prefab - toute retouche manuelle faite
// directement dans l'Inspector du prefab genere sera perdue si on relance
// ce menu apres coup.
public class WorldNameplatePrefabGenerator
{
    const string FontPath = "Assets/Resources/Data/UI/Assets/Font/tahoma SDF.asset";

    // Icone unique (etoile+joyau+branche separatrice, remplace les deux
    // bulles gauche/droite d'origine) - un PNG PAR ETAT prepare a la main,
    // depose directement a chacun de ces 3 chemins. Tant qu'un fichier
    // d'etat specifique n'existe pas encore, on retombe sur le brouillon
    // procedural unique (BubbleIconDraftSrc, genere par
    // NameplateBubbleIconGenerator) pour ne rien casser en attendant.
    const string IconDir = "Assets/Resources/Data/UI/Assets/NameplateIcon";
    const string IconHoverSrc = IconDir + "/IconHover.png";
    const string IconTargetSrc = IconDir + "/IconTarget.png";
    const string IconAttackSrc = IconDir + "/IconAttack.png";
    const string BubbleIconDraftSrc = IconDir + "/BubbleIcon.png";

    // Le systeme UI Toolkit actuel reference "Gauge_DF_Small_CP", un dossier
    // qui n'existe pas dans le projet (asset deja manquant/casse
    // aujourd'hui) - on utilise donc la variante existante la plus proche.
    const string GaugeBgSrc = "Assets/Resources/Data/UI/Assets/Status/Gauge/Gauge_DF_CP/Gauge_DF_CP_bg_Center.png";
    const string GaugeFillSrc = "Assets/Resources/Data/UI/Assets/Status/Gauge/Gauge_DF_CP/Gauge_DF_CP_Center.png";

    const string SpriteOutDir = "Assets/Resources/Data/UI/Assets/WorldNameplate";
    const string MaterialOutDir = SpriteOutDir + "/Materials";
    const string TextMaterialPath = MaterialOutDir + "/NameplateText.mat";

    // Contour SDF sombre : la lisibilite des noms lointains tient bien plus au
    // CONTRASTE qu'a la taille de police - un liseré noir rend le texte lisible
    // sur n'importe quel fond de terrain. Regable ensuite dans l'Inspector du
    // materiau (Outline Thickness / Color) ou ici.
    const float OutlineWidth = 0.2f;
    const string PrefabOutDir = "Assets/Resources/Prefab/Game/Nameplate";
    const string PrefabPath = PrefabOutDir + "/WorldNameplate.prefab";

    [MenuItem("Tools/L2Unity/Nameplate/Generate WorldNameplate Prefab")]
    static void Generate()
    {
        EnsureFolder(SpriteOutDir);
        EnsureFolder("Assets/Resources/Prefab/Game");
        EnsureFolder(PrefabOutDir);

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogError($"[WorldNameplatePrefabGenerator] Police introuvable: {FontPath}");
            return;
        }

        Texture2D iconHoverTex = LoadIconTexture(IconHoverSrc);
        Texture2D iconTargetTex = LoadIconTexture(IconTargetSrc);
        Texture2D iconAttackTex = LoadIconTexture(IconAttackSrc);
        Texture2D gaugeBgTex = LoadOrCopyTexture(GaugeBgSrc, $"{SpriteOutDir}/GaugeBG.png");
        Texture2D gaugeFillTex = LoadOrCopyTexture(GaugeFillSrc, $"{SpriteOutDir}/GaugeFill.png");

        if (iconHoverTex == null || iconTargetTex == null || iconAttackTex == null)
        {
            Debug.LogError($"[WorldNameplatePrefabGenerator] Icone introuvable (ni {IconHoverSrc}/{IconTargetSrc}/{IconAttackSrc}, ni le brouillon {BubbleIconDraftSrc}) - generer au moins le brouillon via Tools > L2Unity > Highlight > Generate NameplateBubbleIcon Texture (draft).");
            return;
        }

        if (gaugeBgTex == null || gaugeFillTex == null)
        {
            Debug.LogError("[WorldNameplatePrefabGenerator] Une ou plusieurs textures source sont introuvables, generation annulee.");
            return;
        }

        int nameplateLayer = LayerMask.NameToLayer("Nameplate");
        if (nameplateLayer < 0)
        {
            Debug.LogWarning("[WorldNameplatePrefabGenerator] Layer \"Nameplate\" introuvable, utilisation du layer par defaut (0).");
            nameplateLayer = 0;
        }

        // Materiaux sauvegardes comme assets (indispensable : un prefab ne
        // peut referencer que des objets persistants, cf. GetOrCreateQuadMaterial).
        EnsureFolder(MaterialOutDir);
        Material gaugeBgMat = GetOrCreateQuadMaterial(gaugeBgTex, $"{MaterialOutDir}/GaugeBG.mat");
        Material gaugeFillMat = GetOrCreateQuadMaterial(gaugeFillTex, $"{MaterialOutDir}/GaugeFill.mat");

        // Un materiau PAR ETAT (Unlit - pas Lit, pour ne pas reagir a
        // l'eclairage de la scene et deformer la couleur, meme constat que
        // HoverGroundRing) : SetBubbleState echange sharedMaterial, meme
        // principe que l'ancien systeme a deux bulles.
        Material iconHoverMat = GetOrCreateIconMaterial(iconHoverTex, $"{IconDir}/IconHover.mat");
        Material iconTargetMat = GetOrCreateIconMaterial(iconTargetTex, $"{IconDir}/IconTarget.mat");
        Material iconAttackMat = GetOrCreateIconMaterial(iconAttackTex, $"{IconDir}/IconAttack.mat");

        // Variante de materiau de police AVEC contour, dediee aux nameplates
        // (ne modifie pas le materiau partage "tahoma SDF Material" utilise
        // ailleurs dans l'UI). La couleur du texte (blanc/vert) reste portee par
        // la couleur de sommet (tmp.color), donc un seul materiau contour suffit
        // pour le nom ET le titre.
        Material textMat = GetOrCreateOutlineTextMaterial(font);

        GameObject root = new GameObject("WorldNameplate");
        root.layer = nameplateLayer;

        // Ecart Titre/Nom leger reduit (0.16 -> 0.13), juge trop grand.
        GameObject title = CreateTextChild(root.transform, "Title", font, textMat, new Vector3(0f, 0.13f, 0f), TextAlignmentOptions.Center);
        title.GetComponent<TextMeshPro>().color = new Color(156f / 255f, 218f / 255f, 144f / 255f);
        title.layer = nameplateLayer;

        GameObject nameText = CreateTextChild(root.transform, "Name", font, textMat, Vector3.zero, TextAlignmentOptions.Center);
        nameText.GetComponent<TextMeshPro>().color = Color.white;
        nameText.layer = nameplateLayer;

        // Icone unique a gauche du nom, positionnee verticalement dans
        // l'ecart Titre/Nom (0.065 = milieu de 0/0.13). Textures en 160x155
        // (quasi carre) -> quad 0.1116x0.108 (ratio respecte, +20% par
        // rapport a 0.093x0.09, jugee trop petite) ; rapprochee du nom sur
        // la droite (x -0.4 -> -0.3).
        GameObject bubbleIcon = CreateQuadChild(root.transform, "BubbleIcon", iconHoverMat, nameplateLayer, new Vector3(-0.3f, 0.065f, 0f), new Vector2(0.1116f, 0.108f));
        bubbleIcon.GetComponent<MeshRenderer>().enabled = false;

        GameObject gauge = new GameObject("Gauge");
        gauge.layer = nameplateLayer;
        gauge.transform.SetParent(root.transform, false);
        gauge.transform.localPosition = new Vector3(0f, -0.2f, 0f);

        // GaugeFill est centre comme GaugeBG (pas de decalage a gauche - un
        // Quad n'a pas de notion de pivot comme un Sprite) : c'est
        // WorldPlayerNameplate.UpdateGauge() qui recalcule position+echelle
        // ensemble a chaque frame pour simuler un remplissage ancre a
        // gauche.
        // GaugeFill legerement decale vers la camera (-Z local : la racine
        // billboard pointe +Z a l'oppose de la camera) : coplanaire avec le
        // fond, les deux quads transparents z-fightaient -> clignotement
        // visible pendant le chargement.
        // Taille +30% (0.8x0.08 -> 1.04x0.104), jugee trop petite.
        GameObject gaugeBG = CreateQuadChild(gauge.transform, "GaugeBG", gaugeBgMat, nameplateLayer, Vector3.zero, new Vector2(1.04f, 0.104f));
        GameObject gaugeFill = CreateQuadChild(gauge.transform, "GaugeFill", gaugeFillMat, nameplateLayer, new Vector3(0f, 0f, -0.005f), new Vector2(1.04f, 0.104f));
        gaugeBG.GetComponent<MeshRenderer>().enabled = false;
        gaugeFill.GetComponent<MeshRenderer>().enabled = false;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[WorldNameplatePrefabGenerator] Prefab genere: {PrefabPath}.");
    }

    // Materiau SDF clone du materiau par defaut de la police, avec contour
    // active. Reutilise l'asset s'il existe deja (GUID stable). Le clone
    // conserve la reference a l'atlas de la police (_MainTex), indispensable au
    // rendu SDF.
    static Material GetOrCreateOutlineTextMaterial(TMP_FontAsset font)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(TextMaterialPath);
        bool isNew = material == null;
        if (isNew)
        {
            material = new Material(font.material) { name = "NameplateText" };
        }

        material.EnableKeyword("OUTLINE_ON");
        material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 1f));
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);

        if (isNew)
        {
            AssetDatabase.CreateAsset(material, TextMaterialPath);
        }
        else
        {
            EditorUtility.SetDirty(material);
        }
        return material;
    }

    static GameObject CreateTextChild(Transform parent, string childName, TMP_FontAsset font, Material textMaterial, Vector3 localPosition, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(childName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        // Pas de mise a l'echelle du transform : TextMeshPro (3D) exprime deja
        // fontSize directement en unites monde a scale=1. Le 0.01 precedent
        // donnait un texte d'environ 1.4cm de haut, invisible a distance
        // MMO normale - valeur a ajuster encore visuellement une fois en jeu.
        go.transform.localScale = Vector3.one;

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.font = font;
        tmp.fontSharedMaterial = textMaterial;
        tmp.fontSize = 0.85f;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.text = childName;

        // TextMeshPro (meme la variante 3D) s'appuie sur un RectTransform en
        // interne pour son alignement - Unity lui donne une taille/un pivot
        // par defaut non explicites, ce qui rendait le centrage et
        // l'espacement titre/nom impredictibles (source probable du
        // decalage a droite et du "titre trop haut" observes). On fixe
        // explicitement une taille et un pivot parfaitement centres.
        RectTransform rect = tmp.rectTransform;
        rect.sizeDelta = new Vector2(4f, 3f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);

        return go;
    }

    // CAUSE RACINE de l'invisibilite en jeu (toutes tentatives confondues,
    // SpriteRenderer comme MeshRenderer) : le materiau etait cree en memoire
    // (new Material) et assigne au renderer, mais JAMAIS sauvegarde comme
    // asset avant SaveAsPrefabAsset. Un prefab ne peut pas referencer un
    // objet non persistant : la reference etait serialisee en {fileID: 0}
    // (verifie dans le YAML du prefab), donc toutes les instances chargees
    // depuis le disque avaient un materiau NULL -> aucun draw call, objet
    // invisible. La Scene View montrait, elle, l'objet temporaire de la
    // session de generation, dont le materiau memoire existait encore -
    // d'ou le paradoxe Scene/Game. Les textes TMP, eux, referencent le
    // materiau SOUS-ASSET de la police (persistant), d'ou leur affichage
    // correct. Fix : chaque materiau est sauvegarde comme asset (meme
    // recette que DropItemAssetGenerator.BuildTransparentMaterial +
    // CreateAsset).
    static Material GetOrCreateQuadMaterial(Texture2D texture, string assetPath)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        bool isNew = material == null;
        if (isNew)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
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
            AssetDatabase.CreateAsset(material, assetPath);
        }
        else
        {
            EditorUtility.SetDirty(material);
        }
        return material;
    }

    // Meme recette de transparence que GetOrCreateQuadMaterial, mais en
    // Unlit : l'icone de nameplate ne doit pas reagir a l'eclairage de la
    // scene et deformer sa couleur/texture - constat identique sur
    // HoverGroundRing, cf. HoverRingGenerator.
    static Material GetOrCreateIconMaterial(Texture2D texture, string assetPath)
    {
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
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
            AssetDatabase.CreateAsset(material, assetPath);
        }
        else
        {
            EditorUtility.SetDirty(material);
        }
        return material;
    }

    // Charge le PNG d'etat specifique (IconHover/IconTarget/IconAttack.png)
    // s'il existe deja (prepare a la main), sinon retombe sur le brouillon
    // procedural unique (BubbleIcon.png) - permet de tester avant que les 3
    // variantes ne soient pretes.
    static Texture2D LoadIconTexture(string path)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture != null) return texture;

        return AssetDatabase.LoadAssetAtPath<Texture2D>(BubbleIconDraftSrc);
    }

    static GameObject CreateQuadChild(Transform parent, string childName, Material material, int layer, Vector3 localPosition, Vector2 worldSize)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = childName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.layer = layer;
        // CreatePrimitive ajoute un MeshCollider par defaut - retire, ces
        // enfants ne doivent jamais devenir des cibles de raycast.
        Object.DestroyImmediate(go.GetComponent<Collider>());

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = material;

        // Le Quad natif d'Unity fait 1x1 unite - la mise a l'echelle
        // correspond donc directement a la taille monde voulue, pas besoin
        // de diviser par une taille de sprite source.
        go.transform.localScale = new Vector3(worldSize.x, worldSize.y, 1f);

        return go;
    }

    static Texture2D LoadOrCopyTexture(string sourcePath, string destPath)
    {
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(destPath);
        if (existing != null) return existing;

        if (!File.Exists(sourcePath))
        {
            Debug.LogError($"[WorldNameplatePrefabGenerator] Texture source introuvable: {sourcePath}");
            return null;
        }

        AssetDatabase.CopyAsset(sourcePath, destPath);
        AssetDatabase.Refresh();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(destPath);
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
