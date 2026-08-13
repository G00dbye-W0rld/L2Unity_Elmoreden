#if (UNITY_EDITOR) 
using System.IO;
using UnityEditor;
using UnityEngine;

public class L2MaterialBuilder
{

    [MenuItem("L2/Import/02 Material - Generate materials", false, 22)]
    public static void SetupMaterials()
    {

        bool overwrite = false;
        if (overwrite)
        {
            ClearMaterials();
        }

        ProcessProps(overwrite);

        CreateBaseMaterials(overwrite);

        // Les materiaux textures viennent seulement d'etre crees : c'est le
        // moment de rebrancher les modeles qui pointaient sur les coquilles
        // vides generees a l'etape 01.
        RebindModelMaterials();
    }

    /// Repare les modeles lies a un materiau vide.
    ///
    /// A l'import d'un FBX, Unity cherche un materiau du meme nom dans tout le
    /// projet (materialSearch = Everywhere) ; s'il n'en trouve pas, il en cree
    /// un vide a cote du modele. Or a l'etape 01 les materiaux textures
    /// n'existent pas encore - ils sont produits par l'etape 02. Chaque modele
    /// se retrouve donc lie a un materiau sans texture, et l'objet apparait
    /// gris. Relancer l'import n'y change rien : le materiau vide existe
    /// desormais et continue d'etre trouve en priorite.
    ///
    /// On supprime donc ces coquilles vides - uniquement lorsqu'un materiau du
    /// meme nom, lui texture, existe sous Data/Textures - puis on force la
    /// reimportation des modeles concernes pour qu'Unity refasse la liaison.
    [MenuItem("L2/Import/02b Material - Rebrancher les materiaux des modeles", false, 23)]
    static void RebindModelMaterials()
    {
        const string meshRoot = "Assets/Resources/Data/StaticMeshes";
        const string textureRoot = "Assets/Resources/Data/Textures";

        if (!Directory.Exists(meshRoot))
        {
            Debug.LogWarning($"[Materiaux] {meshRoot} introuvable.");
            return;
        }

        string[] emptyGuids = AssetDatabase.FindAssets("t:Material", new[] { meshRoot });
        System.Collections.Generic.HashSet<string> foldersToReimport =
            new System.Collections.Generic.HashSet<string>();
        int deleted = 0;
        int keptWithoutReplacement = 0;

        foreach (string guid in emptyGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || HasMainTexture(mat))
            {
                continue;
            }

            string name = Path.GetFileNameWithoutExtension(path);
            if (!TexturedReplacementExists(name, textureRoot))
            {
                // Pas de remplacant : le supprimer laisserait le modele sans
                // aucun materiau, ce qui serait pire. On signale et on garde.
                keptWithoutReplacement++;
                continue;
            }

            // <package>/Materials/<nom>.mat -> on reimportera <package>.
            string packageFolder = Path.GetDirectoryName(Path.GetDirectoryName(path));
            foldersToReimport.Add(packageFolder.Replace('\\', '/'));

            AssetDatabase.DeleteAsset(path);
            deleted++;
        }

        if (deleted == 0)
        {
            Debug.Log($"[Materiaux] Aucun materiau vide a rebrancher ({keptWithoutReplacement} sans remplacant).");
            return;
        }

        AssetDatabase.Refresh();

        int reimported = 0;
        foreach (string folder in foldersToReimport)
        {
            foreach (string fbx in Directory.GetFiles(folder, "*.fbx", SearchOption.TopDirectoryOnly))
            {
                AssetDatabase.ImportAsset(fbx.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
                reimported++;
            }
        }

        AssetDatabase.Refresh();

        Debug.Log($"[Materiaux] {deleted} materiau(x) vide(s) supprime(s), "
                  + $"{reimported} modele(s) reimporte(s) dans {foldersToReimport.Count} package(s). "
                  + (keptWithoutReplacement > 0
                        ? $"{keptWithoutReplacement} sans remplacant, conserve(s)."
                        : ""));
    }

    /// Vrai si le materiau porte une texture dans son slot principal.
    /// On teste les deux noms possibles : _BaseMap (URP) et _MainTex (legacy).
    static bool HasMainTexture(Material mat)
    {
        if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null)
        {
            return true;
        }
        return mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null;
    }

    static bool TexturedReplacementExists(string name, string textureRoot)
    {
        if (!Directory.Exists(textureRoot))
        {
            return false;
        }

        foreach (string guid in AssetDatabase.FindAssets($"t:Material {name}", new[] { textureRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.Equals(Path.GetFileNameWithoutExtension(path), name,
                               System.StringComparison.OrdinalIgnoreCase))
            {
                continue; // FindAssets fait une recherche approximative.
            }

            Material candidate = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (candidate != null && HasMainTexture(candidate))
            {
                return true;
            }
        }

        return false;
    }

    static void ClearMaterials()
    {
        string[] materialGUIDs = AssetDatabase.FindAssets("t:Material", new string[] { "Assets/Resources/Data/Textures", "Assets/Resources/Data/SysTextures" });
        for (int i = 0; i < materialGUIDs.Length; i++)
        {
            string materialPath = AssetDatabase.GUIDToAssetPath(materialGUIDs[i]);

            string ignorePath = Path.Combine(Path.GetDirectoryName(materialPath), ".ignore");
            if (File.Exists(ignorePath))
            {
                Debug.Log("Ignoring folder");
                continue;
            }

            AssetDatabase.DeleteAsset(materialPath);
        }
    }

    static void ProcessProps(bool overwrite)
    {
        string[] propsTxtGUIDs = AssetDatabase.FindAssets("t:TextAsset", new string[] { "Assets/Resources/Data/Textures", "Assets/Resources/Data/SysTextures" });
        //Debug.Log("Found " + propsTxtGUIDs.Length + " props.");

        for (int i = 0; i < propsTxtGUIDs.Length; i++)
        {
            string propsPath = AssetDatabase.GUIDToAssetPath(propsTxtGUIDs[i]);
            string materialPath = Path.Combine(
                   Path.GetDirectoryName(propsPath),
                   Path.GetFileNameWithoutExtension(propsPath)
                       .Replace(".props", string.Empty)
                       .Replace("_sh", string.Empty) + ".mat");

            // overwrite=false economise du travail deja fait CORRECTEMENT, mais
            // sautait aussi les materiaux VIDES crees par un run anterieur au
            // correctif du LoadTexture a deux emplacements (2026-07-30) : un
            // materiau casse une fois restait casse pour toujours, meme apres
            // la correction du code, puisqu'un simple File.Exists() l'ecartait
            // avant meme de regarder son contenu. Constate sur Ru_wood0022.mat
            // (G_Ruin_T), genere vide le 29/07 puis jamais reexamine malgre la
            // texture RU_wood_002 existant juste a cote.
            //
            // Calculee une seule fois : un second garde plus bas (avant
            // AssetDatabase.CreateAsset) doit prendre exactement la meme
            // decision, sinon celui-la continuerait a proteger le materiau
            // casse meme apres que celui-ci ait ete corrige ici.
            bool materialExists = File.Exists(materialPath);
            bool alreadyTextured = false;
            if (materialExists)
            {
                Material existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                alreadyTextured = existing != null && HasMainTexture(existing);
                if (!overwrite && alreadyTextured)
                {
                    continue;
                }
            }

            bool isTransparent = false;
            bool isSpecular = false;
            bool isDoubleFace = false;
            bool isUnlit = false;
            string textureName = null;
            string specularTextureName = null;

            using (StreamReader reader = new StreamReader(propsPath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] parts = line.Split("=");

                    // Tous les .props.txt ne sont PAS de simples "cle=valeur"
                    // a plat : certains meshes multi-materiaux utilisent un
                    // format par blocs, avec des lignes ne contenant qu'une
                    // accolade. Sans ce garde, parts[1] levait une
                    // IndexOutOfRangeException qui faisait echouer TOUTE la
                    // region - constate le 04/08/2026 sur les 9 regions de
                    // 24_18 a 24_26, a cause de Textures/deco01/frame0*.
                    // Une ligne vide produit le meme effet.
                    if (parts.Length < 2)
                    {
                        continue;
                    }

                    string key = parts[0].Trim();
                    string value = parts[1].Trim();

                    if (key.StartsWith("Diffuse") || key.StartsWith("Material"))
                    {
                        if (value.StartsWith("Texture"))
                        {
                            string texRef = value.Substring(8);
                            texRef = texRef.Substring(0, texRef.Length - 1);
                            string[] texRefEntries = texRef.Split('.');
                            textureName = texRefEntries[texRefEntries.Length - 1];
                            Debug.Log("Texture: " + textureName);
                        }
                    }
                    else if (key.StartsWith("SpecularityMask"))
                    {
                        if (value.StartsWith("Texture"))
                        {
                            string texRef = value.Substring(8);
                            texRef = texRef.Substring(0, texRef.Length - 1);
                            string[] texRefEntries = texRef.Split('.');
                            specularTextureName = texRefEntries[texRefEntries.Length - 1];
                            isSpecular = true;
                            Debug.Log("Specular texture: " + specularTextureName);
                        }
                    }
                    else if (key.StartsWith("Opacity"))
                    {
                        if (value.StartsWith("Texture"))
                        {
                            Debug.LogWarning("Transparent: " + textureName);
                            isTransparent = true;
                        }
                    }
                    else if (key.StartsWith("TwoSided"))
                    {
                        isDoubleFace = (value == "true");
                    }
                    else if (key.StartsWith("AlphaTest"))
                    {
                        if (!isTransparent)
                        {
                            isTransparent = (value == "true");
                        }
                        Debug.Log("AlphaTest:" + value);
                    }
                    else if (key.StartsWith("OutputBlending"))
                    {
                        Debug.Log("OutputBlending:");
                        if (value.StartsWith("OB_Brighten"))
                        {
                            isUnlit = true;
                        }
                        else if (value.StartsWith("OB_Masked"))
                        {
                            isTransparent = true;
                        }
                    }
                }
            }

            Texture2D texture = LoadTexture(materialPath, textureName);
            Material material;

            // Build Material
            if (isTransparent)
            {
                material = BuildTransprentMaterial();
            }
            else if (isUnlit)
            {
                material = BuildUnlitMaterial(isDoubleFace);
            }
            else
            {
                Texture2D specularMap = null;
                if (isSpecular)
                {
                    specularMap = LoadTexture(materialPath, specularTextureName);
                }
                material = BuildLitMaterial(specularMap, isDoubleFace);
            }

            material.mainTexture = texture;

            if (materialExists)
            {
                // Meme decision qu'en tete de boucle (materialExists /
                // alreadyTextured) : sans reprendre exactement les memes
                // conditions ici, ce garde continuerait a proteger un
                // materiau vide que le premier avait pourtant laisse passer
                // pour correction.
                if (!isTransparent && !overwrite && alreadyTextured)
                {
                    continue;
                }
                Debug.LogWarning("Delete " + texture);
                AssetDatabase.DeleteAsset(materialPath);
            }

            if (!Directory.Exists(Path.GetDirectoryName(materialPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(materialPath));
            }

            AssetDatabase.CreateAsset(material, materialPath);
        }
    }

    static Texture2D LoadTexture(string materialPath, string textureName)
    {
        // Deux dispositions coexistent selon le package umodel : le .props.txt
        // (et donc le .mat construit ici) est soit dans un sous-dossier
        // "Materials/" du package de textures (la texture est alors un cran
        // au-dessus), soit ecrit directement a la racine du package (la
        // texture est alors a cote). Remonter systematiquement d'un niveau
        // fonctionne pour le premier cas mais atterrit un cran trop haut pour
        // le second - la recherche se faisait dans "Textures/" au lieu de
        // "Textures/<package>/", et le materiau restait sans texture (blanc).
        string materialDirectory = Path.GetDirectoryName(materialPath);
        string parentFolder = Directory.GetParent(materialDirectory).FullName;

        string texture1 = Path.Combine(parentFolder, textureName + ".png");
        string texture2 = Path.Combine(materialDirectory, textureName + ".png");
        string texturePath = File.Exists(texture1) ? texture1 : texture2;

        texturePath = Path.Combine("Assets", Path.GetRelativePath(Application.dataPath, texturePath));

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        return texture;
    }

    // UNITY 2022.3f
    static Material BuildTransprentMaterialLegacy()
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Nature/SpeedTree8_PBRLit"));
        material.SetFloat("_AlphaClip", 0.5f);
        material.SetInt("_WindQuality", 0);
        material.SetInt("EFFECT_EXTRA_TEX", 0);
        material.SetInt("_NormalMapKwToggle", 0);
        material.SetInt("_HueVariationKwToggle", 0);
        material.SetFloat("_AlphaClipThreshold", 0.5f);
        material.SetFloat("_Glossiness", 0f);
        Debug.Log($"_AlphaClip: {material.GetFloat("_AlphaClip")}");
        Debug.Log($"_WindQuality: {material.GetInt("_WindQuality")}");
        Debug.Log($"EFFECT_EXTRA_TEX: {material.GetInt("EFFECT_EXTRA_TEX")}");
        Debug.Log($"_NormalMapKwToggle: {material.GetInt("_NormalMapKwToggle")}");
        Debug.Log($"_HueVariationKwToggle: {material.GetInt("_HueVariationKwToggle")}");
        Debug.Log($"_AlphaClipThreshold: {material.GetFloat("_AlphaClipThreshold")}");

        return material;
    }

    static Material BuildTransprentMaterial()
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetFloat("_Cull", 2f);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0);
        material.SetFloat("_EnvironmentReflections", 0f);
        material.SetFloat("_SpecularHighlights", 0f);
        // Enable alpha clipping
        material.SetFloat("_AlphaClip", 1); // or true, depending on the shader

        // Set the alpha clip threshold
        material.SetFloat("_Cutoff", 0.5f);

        return material;
    }

    static Material BuildLitMaterial(Texture2D specularMap, bool isDoubleFace)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        float state = isDoubleFace ? 0f : 2f;
        material.SetFloat("_Cull", state);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0);
        material.SetFloat("_EnvironmentReflections", 0f);
        material.SetFloat("_SpecularHighlights", 0f);

        if (specularMap != null)
        {
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.EnableKeyword("_SPECULAR_SETUP");
            material.SetFloat("_WorkflowMode", 0);
            material.SetFloat("_Smoothness", 1);
            material.SetTexture("_SpecGlossMap", specularMap);
            material.SetTexture("_METALLICSPECGLOSSMAP", specularMap);
            material.SetTexture("_Specular", specularMap);
        }

        return material;
    }

    static Material BuildUnlitMaterial(bool isDoubleFace)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f);
        material.SetFloat("_BlendOp", 0f);
        material.SetFloat("_ColorMode", 0f);
        material.SetFloat("_DstBlend", 1f);
        material.SetFloat("_DstBlendAlpha", 1f);
        float state = isDoubleFace ? 0f : 2f;
        material.SetFloat("_Cull", state);
        //material.SetFloat("_AlphaClip", 1f);
        return material;
    }

    static void CreateBaseMaterials(bool overwrite)
    {
        string[] textureGUIDs = AssetDatabase.FindAssets("t:Texture2D", new string[] { "Assets/Resources/Data/Textures", "Assets/Resources/Data/SysTextures" });
        for (int i = 0; i < textureGUIDs.Length; i++)
        {
            string texturePath = AssetDatabase.GUIDToAssetPath(textureGUIDs[i]);
            string materialDirectory = Path.Combine(Path.GetDirectoryName(texturePath), "Materials");
            string materialPath = Path.Combine(materialDirectory, Path.GetFileNameWithoutExtension(texturePath) + ".mat");

            string ignorePath = Path.Combine(Path.GetDirectoryName(texturePath), ".ignore");
            if (File.Exists(ignorePath))
            {
                Debug.Log("Ignoring folder");
                continue;
            }

            if (materialPath.EndsWith("_ori.mat") || materialPath.EndsWith("_sp.mat"))
            {
                Debug.Log("Skipping materials with props");
                continue;
            }

            if (!overwrite && File.Exists(materialPath))
            {
                continue;
            }

            if (!Directory.Exists(materialDirectory))
            {
                Directory.CreateDirectory(materialDirectory);
            }

            Material material;
            if (materialPath.EndsWith("_h.mat") ||
                materialPath.EndsWith("_ah.mat") ||
                materialPath.EndsWith("_bh.mat") ||
                materialPath.EndsWith("_ah_u00.mat") ||
                materialPath.EndsWith("_bh_u00.mat"))
            {
                material = BuildTransprentMaterial();
            }
            else
            {
                material = BuildLitMaterial(null, false);
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            material.mainTexture = texture;
            AssetDatabase.CreateAsset(material, materialPath);
        }
    }
}
#endif