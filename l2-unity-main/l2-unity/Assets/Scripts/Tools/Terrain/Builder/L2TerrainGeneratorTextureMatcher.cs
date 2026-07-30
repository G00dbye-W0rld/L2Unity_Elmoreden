using System.Collections.Generic;
using UnityEngine;
using static JBooth.MicroSplat.MicroSplatPropData;

public class L2TerrainGeneratorTextureMatcher
{
    public Dictionary<string, string> textureMatches;
    public Dictionary<string, float> scaleMatches;
    public Dictionary<string, List<PerTexFloatVal>> pertexFloatMatches;
    public Dictionary<string, List<PerTexColorVal>> pertextColorMatches;

    public struct PerTexFloatVal
    {
        public PerTexFloat ptf;
        public float value;
    }

    public struct PerTexColorVal
    {
        public PerTexColor ptf;
        public Color value;
    }


    private static L2TerrainGeneratorTextureMatcher _instance;
    public static L2TerrainGeneratorTextureMatcher Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new L2TerrainGeneratorTextureMatcher();
            }

            return _instance;
        }
    }

    private L2TerrainGeneratorTextureMatcher()
    {
        textureMatches = new Dictionary<string, string>();
        textureMatches.Add("Base", "Swamp_Soil_tjmhfcjl_1K");
        textureMatches.Add("SL_G", "Wild_Grass_pjwgW0_1K");
        textureMatches.Add("SL_S3", "Grass_Patchy_pjvtA0_1K");
        // textureMatches.Add("SL_WR", ""); //L2 texture is good enough
        // textureMatches.Add("WR_02", ""); //L2 texture is good enough
        textureMatches.Add("SL_S6", "Soil_Sand_pjErQ0_1K");
        textureMatches.Add("SL_G3", "Wild_Grass_pjwgW0_1K");
        textureMatches.Add("SL_G2", "Wild_Grass_pjwgW0_1K");
        textureMatches.Add("SL_S1", "Thai_Beach_Sand_tefnah1q_1K");
        textureMatches.Add("SL_R1", "Rough_Soil_Detail_Texture_se4mcazf0_1K");
        textureMatches.Add("SL_C", "Icelandic_Jagged_Slate_Rock_shfsaida_1K");
        textureMatches.Add("SL_G4", "grass_and_rubble_pjwdt0_1k");
        textureMatches.Add("SL_C1", "grass_and_rubble_pjwdt0_1k");

        // --- Gludio (region 17_23 et voisines) ---
        textureMatches.Add("GUG02", "Wild_Grass_pjwgW0_1K");
        textureMatches.Add("GUG107", "Wild_Grass_pjwgW0_1K");
        textureMatches.Add("GUS05", "Soil_Sand_pjErQ0_1K");
        textureMatches.Add("GUS103", "Soil_Sand_pjErQ0_1K");
        textureMatches.Add("GUS108", "Rough_Soil_Detail_Texture_se4mcazf0_1K");
        // GUG102/GUS110 : vues pour la premiere fois sur 17_22. Sans entree
        // ici, la couche correspondante n'a aucun .terrainlayer genere -
        // c'etait la cause du sol rose sur cette region (case de texture vide
        // dans le tableau lu par le shader MicroSplat).
        textureMatches.Add("GUG102", "Wild_Grass_pjwgW0_1K");
        textureMatches.Add("GUS110", "Soil_Sand_pjErQ0_1K");

        scaleMatches = new Dictionary<string, float>();
        scaleMatches.Add("Base", 3);
        scaleMatches.Add("SL_G", 5);
        scaleMatches.Add("SL_S3", 4);
        scaleMatches.Add("SL_WR", 3); //L2 texture is good enough
        scaleMatches.Add("WR_02", 3); //L2 texture is good enough
        scaleMatches.Add("SL_S6", 2);
        scaleMatches.Add("SL_G3", 5);
        scaleMatches.Add("SL_G2", 6);
        scaleMatches.Add("SL_S1", 1.2f);
        scaleMatches.Add("SL_R1", 1.25f);
        scaleMatches.Add("SL_C", 1);
        scaleMatches.Add("SL_G4", 7);
        scaleMatches.Add("SL_C1", 7);

        // --- Gludio (region 17_23 et voisines) ---
        // Meme convention de nommage que Speaking Island : GUG* = herbe,
        // GUS* = sol/sable.
        //
        // Valeur 64 retenue apres calage visuel sur 17_23. Elle est coherente
        // avec les donnees d'origine : dans le .t3d officiel, ces cinq
        // textures partagent toutes le meme champ Scale=32, la ou un premier
        // jet les avait dispersees entre 2 et 5 - d'ou des tuiles enormes et
        // desaccordees entre elles. Les regions Gludio suivantes reprennent
        // donc ces valeurs sans reglage manuel.
        scaleMatches.Add("GUG02", 64);
        scaleMatches.Add("GUG107", 64);
        scaleMatches.Add("GUS05", 64);
        scaleMatches.Add("GUS103", 64);
        scaleMatches.Add("GUS108", 64);
        scaleMatches.Add("GUG102", 64);
        scaleMatches.Add("GUS110", 64);

        pertexFloatMatches = new Dictionary<string, List<PerTexFloatVal>>();
        pertexFloatMatches.Add("Base", new List<PerTexFloatVal>
        {
            new PerTexFloatVal() { ptf = PerTexFloat.Brightness, value = 0.03f },
            new PerTexFloatVal() { ptf = PerTexFloat.ColorIntensity, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.Contrast, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.Saturation, value = 1.37f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightOffset, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightContrast, value = 1f },
        });
        pertexFloatMatches.Add("SL_G", new List<PerTexFloatVal>
        {
            new PerTexFloatVal() { ptf = PerTexFloat.Brightness, value = 0f },
            new PerTexFloatVal() { ptf = PerTexFloat.ColorIntensity, value = .33f },
            new PerTexFloatVal() { ptf = PerTexFloat.Contrast, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.Saturation, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightOffset, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightContrast, value = 0.37f },
        });
        pertexFloatMatches.Add("SL_S3", new List<PerTexFloatVal>
        {
            new PerTexFloatVal() { ptf = PerTexFloat.Brightness, value = -0.07f },
            new PerTexFloatVal() { ptf = PerTexFloat.ColorIntensity, value = 0.137f },
            new PerTexFloatVal() { ptf = PerTexFloat.Contrast, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.Saturation, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightOffset, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightContrast, value = 0.31f },
        });
        pertexFloatMatches.Add("SL_WR", new List<PerTexFloatVal>
        {
            new PerTexFloatVal() { ptf = PerTexFloat.Brightness, value = 0f },
            new PerTexFloatVal() { ptf = PerTexFloat.ColorIntensity, value = 0f },
            new PerTexFloatVal() { ptf = PerTexFloat.Contrast, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.Saturation, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightOffset, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightContrast, value = 1f },
        });
        pertexFloatMatches.Add("WR_02", new List<PerTexFloatVal>
        {
            new PerTexFloatVal() { ptf = PerTexFloat.Brightness, value = 0f },
            new PerTexFloatVal() { ptf = PerTexFloat.ColorIntensity, value = 0f },
            new PerTexFloatVal() { ptf = PerTexFloat.Contrast, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.Saturation, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightOffset, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightContrast, value = 1f },
        });
        pertexFloatMatches.Add("SL_S6", new List<PerTexFloatVal>
        {
            new PerTexFloatVal() { ptf = PerTexFloat.Brightness, value = 0.01f },
            new PerTexFloatVal() { ptf = PerTexFloat.ColorIntensity, value = 0.83f },
            new PerTexFloatVal() { ptf = PerTexFloat.Contrast, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.Saturation, value = 0.63f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightOffset, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightContrast, value = 1f },
        });
        pertexFloatMatches.Add("SL_G3", new List<PerTexFloatVal>
        {
            new PerTexFloatVal() { ptf = PerTexFloat.Brightness, value = 0f },
            new PerTexFloatVal() { ptf = PerTexFloat.ColorIntensity, value = 0.73f },
            new PerTexFloatVal() { ptf = PerTexFloat.Contrast, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.Saturation, value = 0.95f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightOffset, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightContrast, value = 1f },
        });
        pertexFloatMatches.Add("SL_G2", new List<PerTexFloatVal>
        {
            new PerTexFloatVal() { ptf = PerTexFloat.Brightness, value = 0f },
            new PerTexFloatVal() { ptf = PerTexFloat.ColorIntensity, value = 0.74f },
            new PerTexFloatVal() { ptf = PerTexFloat.Contrast, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.Saturation, value = 1.1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightOffset, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightContrast, value = 1f },
        });
        pertexFloatMatches.Add("SL_S1", new List<PerTexFloatVal>
        {
            new PerTexFloatVal() { ptf = PerTexFloat.Brightness, value = 0f },
            new PerTexFloatVal() { ptf = PerTexFloat.ColorIntensity, value = 0f },
            new PerTexFloatVal() { ptf = PerTexFloat.Contrast, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.Saturation, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightOffset, value = 0.394f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightContrast, value = 0.2f },
        });
        pertexFloatMatches.Add("SL_R1", new List<PerTexFloatVal>
        {
            new PerTexFloatVal() { ptf = PerTexFloat.Brightness, value = 0f },
            new PerTexFloatVal() { ptf = PerTexFloat.ColorIntensity, value = 0 },
            new PerTexFloatVal() { ptf = PerTexFloat.Contrast, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.Saturation, value = 1.1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightOffset, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightContrast, value = 1f },
        });
        pertexFloatMatches.Add("SL_C", new List<PerTexFloatVal>
        {
            new PerTexFloatVal() { ptf = PerTexFloat.Brightness, value = 0f },
            new PerTexFloatVal() { ptf = PerTexFloat.ColorIntensity, value = 0.46f },
            new PerTexFloatVal() { ptf = PerTexFloat.Contrast, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.Saturation, value = 0.83f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightOffset, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightContrast, value = 1f },
        });
        pertexFloatMatches.Add("SL_G4", new List<PerTexFloatVal>
        {
            new PerTexFloatVal() { ptf = PerTexFloat.Brightness, value = 0f },
            new PerTexFloatVal() { ptf = PerTexFloat.ColorIntensity, value = 0f },
            new PerTexFloatVal() { ptf = PerTexFloat.Contrast, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.Saturation, value = 1.1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightOffset, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightContrast, value = 1f },
        });
        pertexFloatMatches.Add("SL_C1", new List<PerTexFloatVal>
        {
            new PerTexFloatVal() { ptf = PerTexFloat.Brightness, value = 0f },
            new PerTexFloatVal() { ptf = PerTexFloat.ColorIntensity, value = 0f },
            new PerTexFloatVal() { ptf = PerTexFloat.Contrast, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.Saturation, value = 1.1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightOffset, value = 1f },
            new PerTexFloatVal() { ptf = PerTexFloat.HeightContrast, value = 1f },
        });

        pertextColorMatches = new Dictionary<string, List<PerTexColorVal>>();

        pertextColorMatches.Add("Base", new List<PerTexColorVal>
        {
            new PerTexColorVal() { ptf = PerTexColor.Tint, value =  Color.white },
        });
        pertextColorMatches.Add("SL_G", new List<PerTexColorVal>
        {
            new PerTexColorVal() { ptf = PerTexColor.Tint, value =  Color.white },
        });
        pertextColorMatches.Add("SL_S3", new List<PerTexColorVal>
        {
            new PerTexColorVal() { ptf = PerTexColor.Tint, value =  new Color(238 / 255f, 238 / 255f, 238 / 255f) },
        });
        pertextColorMatches.Add("SL_WR", new List<PerTexColorVal>
        {
            new PerTexColorVal() { ptf = PerTexColor.Tint, value =  Color.white },
        });
        pertextColorMatches.Add("SL_S6", new List<PerTexColorVal>
        {
            new PerTexColorVal() { ptf = PerTexColor.Tint, value =  new Color(253 / 255f, 242 / 255f, 242 / 255f) },
        });
        pertextColorMatches.Add("SL_G3", new List<PerTexColorVal>
        {
            new PerTexColorVal() { ptf = PerTexColor.Tint, value =  Color.white },
        });
        pertextColorMatches.Add("SL_G2", new List<PerTexColorVal>
        {
            new PerTexColorVal() { ptf = PerTexColor.Tint, value =  Color.white },
        });
        pertextColorMatches.Add("SL_S1", new List<PerTexColorVal>
        {
            new PerTexColorVal() { ptf = PerTexColor.Tint, value =  new Color(255 / 255f, 249 / 255f, 237 / 255f) },
        });
        pertextColorMatches.Add("SL_R1", new List<PerTexColorVal>
        {
            new PerTexColorVal() { ptf = PerTexColor.Tint, value =  Color.white },
        });
        pertextColorMatches.Add("SL_C", new List<PerTexColorVal>
        {
            new PerTexColorVal() { ptf = PerTexColor.Tint, value =  new Color(154 / 255f, 130 / 255f, 101 / 255f) },
        });
        pertextColorMatches.Add("SL_G4", new List<PerTexColorVal>
        {
            new PerTexColorVal() { ptf = PerTexColor.Tint, value =  Color.white },
        });
        pertextColorMatches.Add("SL_C1", new List<PerTexColorVal>
        {
            new PerTexColorVal() { ptf = PerTexColor.Tint, value =  Color.white },
        });
    }

    /// Verifie, AVANT de generer quoi que ce soit, que chaque texture de
    /// couche du terrain a une entree dans textureMatches.
    ///
    /// Sans entree, aucun .terrainlayer n'est genere pour cette couche : la
    /// case correspondante reste vide dans le tableau de textures lu par le
    /// shader MicroSplat, et le terrain rend rose a cet endroit (constate sur
    /// 17_22 avec GUG102/GUS110, deux textures Gludio jamais vues avant). Le
    /// symptome n'apparaissait qu'a l'inspection visuelle, une fois le
    /// pipeline termine. Cette methode le signale des le debut de l'import.
    ///
    /// scaleMatches est verifiee separement : son absence ne provoque qu'une
    /// echelle par defaut (DefaultSplatUvScale), un defaut cosmetique, pas une
    /// case vide - donc un simple avertissement, pas une alerte.
    public static List<string> FindMissingTextureMatches(L2TerrainInfo terrainInfo)
    {
        List<string> missing = new List<string>();
        if (terrainInfo?.uvLayers == null)
        {
            return missing;
        }

        foreach (var layer in terrainInfo.uvLayers)
        {
            string texName = layer.texture != null ? layer.texture.name : null;
            if (string.IsNullOrEmpty(texName))
            {
                continue;
            }

            if (!Instance.textureMatches.ContainsKey(texName) && !missing.Contains(texName))
            {
                missing.Add(texName);
            }
        }

        return missing;
    }

    /// Meme verification pour scaleMatches (defaut cosmetique, pas critique).
    public static List<string> FindMissingScaleMatches(L2TerrainInfo terrainInfo)
    {
        List<string> missing = new List<string>();
        if (terrainInfo?.uvLayers == null)
        {
            return missing;
        }

        foreach (var layer in terrainInfo.uvLayers)
        {
            string texName = layer.texture != null ? layer.texture.name : null;
            if (string.IsNullOrEmpty(texName))
            {
                continue;
            }

            if (!Instance.scaleMatches.ContainsKey(texName) && !missing.Contains(texName))
            {
                missing.Add(texName);
            }
        }

        return missing;
    }
}