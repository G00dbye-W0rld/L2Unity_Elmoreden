using System.Collections.Generic;
using UnityEngine;
using static JBooth.MicroSplat.MicroSplatPropData;

public class L2TerrainGeneratorTextureMatcher
{
    public Dictionary<string, string> textureMatches;
    public Dictionary<string, float> scaleMatches;
    public Dictionary<string, List<PerTexFloatVal>> pertexFloatMatches;
    public Dictionary<string, List<PerTexColorVal>> pertextColorMatches;

    /// Surcharges PAR REGION, prioritaires sur les tables globales ci-dessus.
    ///
    /// Une meme texture L2 sert souvent dans des dizaines de regions aux
    /// ambiances differentes : Obase_1 apparait dans 38 regions, dont
    /// certaines enneigees et d'autres verdoyantes. Un choix global unique ne
    /// peut pas convenir partout. Ces tables permettent d'affiner region par
    /// region SANS toucher au reglage general.
    ///
    /// Cle exterieure = identifiant de region ("17_23"), cle interieure = nom
    /// court de la texture L2 ("Obase_1"). Voir GetTextureMatch/GetScaleMatch.
    public Dictionary<string, Dictionary<string, string>> regionTextureMatches;
    public Dictionary<string, Dictionary<string, float>> regionScaleMatches;

    /// Echelle de carrelage par defaut, PAR PACK PBR.
    ///
    /// Sert de repli quand aucune echelle n'est definie pour la texture L2.
    /// C'est ici qu'il faut regler un pack une bonne fois pour toutes, plutot
    /// que de repeter la valeur sur chaque texture qui l'utilise.
    ///
    /// ATTENTION - deux baremes incompatibles cohabitent aujourd'hui dans
    /// scaleMatches : les textures de Talking Island sont a 1-7, celles de
    /// Gludio a 64. Ce n'est pas un choix artistique mais un heritage (les
    /// valeurs Gludio ont ete calibrees separement). Une echelle de 1 etire
    /// la texture UNE SEULE FOIS sur les 624 unites du terrain - c'est la
    /// cause du probleme d'echelle constate sur Icelandic (SL_C etait a 1).
    public Dictionary<string, float> packDefaultScales;

    /// Substitution a utiliser pour une texture donnee DANS une region donnee.
    /// Cherche d'abord une surcharge propre a la region, puis retombe sur la
    /// table globale. Renvoie false si aucune substitution n'est definie (la
    /// texture L2 d'origine est alors conservee).
    public bool TryGetTextureMatch(string mapName, string textureName, out string pbrPack)
    {
        if (mapName != null
            && regionTextureMatches.TryGetValue(mapName, out var perRegion)
            && perRegion.TryGetValue(textureName, out pbrPack))
        {
            return true;
        }

        if (textureMatches.TryGetValue(textureName, out pbrPack))
        {
            return true;
        }

        return TryGetRuleMatch(textureName, out pbrPack);
    }

    /// Correspondances par REGLE, appliquees en dernier recours.
    ///
    /// POURQUOI DES REGLES PLUTOT QUE DES ENTREES
    /// Le monde compte 378 textures de terrain distinctes, et leur repartition
    /// est tres plate : les 20 plus repandues ne couvrent que 29% des couches,
    /// les 120 premieres 69%. Couvrir le monde a la main demanderait donc des
    /// centaines de lignes, et chaque nouvelle region en rajouterait.
    ///
    /// Mais le nommage du client est remarquablement regulier :
    /// <zone><type>_<index>, ou le type est une lettre. DI_G3 / DI_S5 / DI_C1
    /// pour Dion, OG_53 / OS_12 / OC_03 pour Oren, GI_C1 / GI_S2 pour Giran.
    /// Certaines sont meme explicites : F_T_Rock_01, F_T_Grass_02.
    ///
    /// Une dizaine de regles classent ainsi 93,7% des couches du monde
    /// (mesure du 2026-08-09 : 917 couches sur 979).
    ///
    /// L'ORDRE EST SIGNIFIANT : la premiere regle qui correspond gagne. Les
    /// motifs les plus specifiques passent donc devant - "st" (pierre) avant
    /// "s" (terre), sinon toute pierre serait classee en terre.
    /// Le prefixe d'une texture donne sa ZONE de jeu, et les regles s'en
    /// servent pour varier les packs. Sans ca, les 182 couches de falaise du
    /// monde partageraient la meme roche et toutes les regions se
    /// ressembleraient - c'est ce que donnaient les 9 packs d'origine.
    ///
    /// Poids des zones (couches) : Oren/Orc 241, Schuttgart 116, Gludio 84,
    /// Goddard 74, Talking Island 57, Aden 54, Rune 51, Dion 44, Innadril 41,
    /// Giran 37, Primeval 12.
    ///
    /// Talking Island (SL_*, WR_*) n'apparait PAS ici : ses textures ont des
    /// entrees explicites, validees a la main, qui passent avant les regles.
    private static readonly (string pattern, string pack)[] _rules =
    {
        // ---- Cas a ne PAS substituer -------------------------------------
        // Textures uniques a une region (vues d'ensemble prerendues) : rien a
        // substituer, elles ne se repetent pas.
        (@"_map$|_top$",              ""),

        // ---- Neige (Schuttgart) ------------------------------------------
        // Doit passer AVANT tout le reste : "SCSN01" contient un "s" qui le
        // ferait classer en terre.
        (@"sn[0-9_]|sn$|snow",        "Fresh_Windswept_Snow_ugspafgdy_1K"),

        // Schuttgart est la zone enneigee du jeu, mais seulement ses SOLS :
        // "SCST*" designe de la pierre (les paves de la ville) et doit garder
        // un rendu mineral. D'ou [n0-9] plutot qu'un ".*" : "scst" ne matche
        // pas, il tombera sur la regle "st" plus bas.
        (@"^scht.*_s[_0-9]|^scs[n0-9]", "Trampled_Snow_vcqnfdk_1K"),

        // ---- Prefixes de zone finissant par "c" --------------------------
        // PIEGE : les regles de falaise cherchent un "c" suivi d'un chiffre ou
        // d'un underscore, en supposant que ce "c" est le marqueur de TYPE
        // (OC_03 = Oren Cliff 03). Mais deux zones portent un "c" dans leur
        // PREFIXE : "ORC_" et "ADC_". Le "c_" de leur prefixe declenche alors
        // `^o.*c[_0-9]` et le `c[_0-9]` generique, et toute la zone - herbes et
        // sols compris - se retrouve classee en falaise.
        //
        // Mesure du 2026-08-17 : 10 regions rendues d'une seule texture, leurs
        // 8 a 10 couches ecrasees sur la meme tranche (18_14, 19_13/14/15,
        // 20_13/14/15, 21_17, 25_16/17). Les autres prefixes en "c_" (AC_,
        // GOC_, INNC_, OC_, RUC_) sont, eux, de VRAIES falaises - verifie sur
        // les 378 noms du monde, aucun autre faux positif.
        //
        // Ces regles doivent rester AVANT toute regle de falaise.
        (@"^orc_c",                   "Layered_Rock_Cliff_tjtmcg3g_1K"),
        (@"^orc_g",                   "Mossy_Forest_Floor_vfylbge_1K"),
        (@"^orc_s",                   "Ground_Roots_smspdebp_1K"),
        (@"^adc_c",                   "Rock_Cliff_xccibbi_1K"),
        (@"^adc_g",                   "Grass_Dried_pjvvl0_1K"),
        (@"^adc_r",                   "Rocky_Steppe_ulgmbhwn_1K"),
        (@"^adc_s",                   "Rocky_Sand_vd4pbdt_1K"),

        // ---- Noms explicites ---------------------------------------------
        (@"rock|cliff",               "Rock_Cliff_xccibbi_1K"),
        (@"sand",                     "Thai_Beach_Sand_tefnah1q_1K"),
        (@"soil",                     "Grassy_Soil_xbreair_1K"),
        (@"grass",                    "Uncut_Grass_oeeb70_1K"),
        (@"base",                     "Forest_Floor_sfjmafua_1K"),

        // ---- Falaises, variees par zone ----------------------------------
        (@"^o.*c[_0-9]|^orc.*c",      "Layered_Rock_Cliff_tjtmcg3g_1K"),
        (@"^di.*c[_0-9]|^di_c",       "Gouged_Rock_Cliff_vlcpcbc_1K"),
        (@"^gi.*c[_0-9]|^gic",        "Rock_Cliff_xccibbi_1K"),
        (@"^inn.*c[_0-9]",            "Mine_Rock_Wall_uebmddyn_1K"),
        (@"^go.*c[_0-9]",             "Icelandic_Jagged_Slate_Rock_shfsaida_1K"),
        (@"c[_0-9]|c$",               "Rock_Cliff_xccibbi_1K"),

        // ---- Herbes, variees par zone ------------------------------------
        (@"^o.*g[_0-9]",              "Mossy_Forest_Floor_vfylbge_1K"),
        (@"^a.*g[_0-9]",              "Grass_Dried_pjvvl0_1K"),
        (@"^ru.*g[_0-9]",             "Uncut_Grass_oilpt20_1K"),
        (@"^di.*g[_0-9]|^gi.*g",      "Uncut_Grass_oeeb70_1K"),
        (@"^go.*g[_0-9]",             "grass_and_rubble_pjwey0_1k"),
        (@"g[_0-9]|g$",               "Wild_Grass_pjwgW0_1K"),

        // ---- Pierre et chemins -------------------------------------------
        // "st" = stone. Passe avant "s" (terre), sinon toute pierre serait
        // classee en terre.
        (@"^go.*st|^gost",            "Gravel_Ground_vi0maebg_1K"),
        (@"st[_0-9]|st$",             "Forest_Path_ugsnfawlw_1K"),

        // ---- Terres, variees par zone ------------------------------------
        (@"r[_0-9]|r$",               "Rocky_Steppe_ulgmbhwn_1K"),
        (@"^o.*s[_0-9]|^obase",       "Ground_Roots_smspdebp_1K"),
        (@"^a.*s[_0-9]",              "Rocky_Sand_vd4pbdt_1K"),
        (@"^inn.*s[_0-9]",            "Moist_Fallen_Leaves_rmqlw0p0_1K"),
        (@"^ru.*s[_0-9]",             "Dry_Fallen_Leaves_vetladiaw_1K"),
        (@"^di.*s[_0-9]|^gi.*s",      "Soil_Sand_pjErQ0_1K"),
        (@"s[_0-9]|s$",               "Grassy_Soil_xbreair_1K"),
    };

    private bool TryGetRuleMatch(string textureName, out string pbrPack)
    {
        pbrPack = null;
        if (string.IsNullOrEmpty(textureName))
        {
            return false;
        }

        string lower = textureName.ToLowerInvariant();
        foreach (var rule in _rules)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(lower, rule.pattern))
            {
                continue;
            }

            // Une regle peut deliberement ne designer aucun pack (neige,
            // textures uniques). On considere alors qu'il n'y a pas de
            // substitution - mais on arrete la recherche : les regles
            // suivantes, plus generales, donneraient un faux positif.
            if (string.IsNullOrEmpty(rule.pack))
            {
                return false;
            }

            pbrPack = rule.pack;
            return true;
        }

        return false;
    }

    /// Echelle de carrelage, resolue dans cet ordre :
    ///   1. surcharge de region      (le plus specifique)
    ///   2. scaleMatches             (par texture L2)
    ///   3. packDefaultScales        (par pack PBR, le plus general)
    ///
    /// Le niveau 3 corrige un defaut de conception : l'echelle etait indexee
    /// par texture L2 alors qu'elle decrit une propriete du PACK. Un rocher a
    /// une taille de motif naturelle, qu'il remplace SL_C ou autre chose.
    /// Sans ce niveau, corriger un pack obligeait a modifier chaque texture
    /// L2 qui l'utilise, et deux textures pointant sur le meme pack pouvaient
    /// avoir des echelles differentes - d'ou des rendus incoherents.
    public bool TryGetScaleMatch(string mapName, string textureName, out float scale)
    {
        if (mapName != null
            && regionScaleMatches.TryGetValue(mapName, out var perRegion)
            && perRegion.TryGetValue(textureName, out scale))
        {
            return true;
        }

        if (scaleMatches.TryGetValue(textureName, out scale))
        {
            return true;
        }

        // Repli sur l'echelle par defaut du pack vers lequel pointe la texture.
        if (TryGetTextureMatch(mapName, textureName, out string pbrPack)
            && packDefaultScales.TryGetValue(pbrPack, out scale))
        {
            return true;
        }

        scale = 0f;
        return false;
    }

    /// Declare une surcharge pour une region. Cree la table de la region au
    /// besoin - evite d'avoir a l'initialiser a la main dans le constructeur.
    private void AddRegionOverride(string mapName, string textureName, string pbrPack, float scale)
    {
        if (!regionTextureMatches.ContainsKey(mapName))
        {
            regionTextureMatches[mapName] = new Dictionary<string, string>();
            regionScaleMatches[mapName] = new Dictionary<string, float>();
        }

        regionTextureMatches[mapName][textureName] = pbrPack;
        regionScaleMatches[mapName][textureName] = scale;
    }

    /// Applique par-dessus les tables en dur le contenu de l'asset de reglages
    /// (L2TerrainTextureSettings), s'il existe.
    ///
    /// L'asset COMPLETE et REMPLACE : une texture declaree des deux cotes prend
    /// la valeur de l'asset, ce qui permet de corriger un reglage sans toucher
    /// au code. S'il n'existe pas, rien ne change.
    ///
    /// Une echelle laissee a 0 dans l'asset signifie "utiliser celle du pack"
    /// - on n'ecrit alors PAS dans scaleMatches, pour laisser TryGetScaleMatch
    /// retomber sur packDefaultScales.
    private void ApplySettingsAsset()
    {
#if UNITY_EDITOR
        var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<L2TerrainTextureSettings>(
            L2TerrainTextureSettings.AssetPath);

        if (settings == null)
        {
            return;
        }

        foreach (var p in settings.packDefaults)
        {
            if (!string.IsNullOrEmpty(p.pbrPack))
            {
                packDefaultScales[p.pbrPack] = p.scale;
            }
        }

        foreach (var s in settings.substitutions)
        {
            if (string.IsNullOrEmpty(s.l2Texture) || string.IsNullOrEmpty(s.pbrPack))
            {
                continue;
            }

            textureMatches[s.l2Texture] = s.pbrPack;

            if (s.scale > 0f)
            {
                scaleMatches[s.l2Texture] = s.scale;
            }
            else
            {
                // Retire une eventuelle echelle en dur pour que le defaut du
                // pack reprenne la main.
                scaleMatches.Remove(s.l2Texture);
            }
        }

        foreach (var o in settings.regionOverrides)
        {
            if (string.IsNullOrEmpty(o.region) || string.IsNullOrEmpty(o.l2Texture)
                || string.IsNullOrEmpty(o.pbrPack))
            {
                continue;
            }

            AddRegionOverride(o.region, o.l2Texture, o.pbrPack, o.scale);
        }

        Debug.Log($"[Textures] Reglages charges depuis l'asset : "
                  + $"{settings.substitutions.Count} substitution(s), "
                  + $"{settings.packDefaults.Count} pack(s), "
                  + $"{settings.regionOverrides.Count} surcharge(s) de region.");
#endif
    }

    /// Force la relecture des tables au prochain acces.
    ///
    /// Le matcher est un singleton construit une seule fois par session
    /// d'editeur : sans ca, une modification de l'asset resterait sans effet
    /// jusqu'au rechargement d'Unity. Appele par les outils de
    /// re-substitution.
    public static void Reload()
    {
        _instance = null;
    }

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

        // --- Surcharges par region ------------------------------------------
        // Vides par defaut : tant qu'aucune ligne n'est ajoutee ici, le
        // comportement est strictement identique a avant (tables globales).
        //
        // Pour affiner une region, ajouter une ligne :
        //   AddRegionOverride("22_13", "Obase_1", "Icelandic_Jagged_Slate_Rock_shfsaida_1K", 64);
        //                     region   texture L2  pack PBR                                  echelle
        //
        // La surcharge l'emporte sur la table globale pour CETTE region
        // uniquement ; toutes les autres gardent le reglage general.
        regionTextureMatches = new Dictionary<string, Dictionary<string, string>>();
        regionScaleMatches = new Dictionary<string, Dictionary<string, float>>();

        // --- Echelles par defaut, par pack PBR --------------------------------
        // Valeurs de depart alignees sur le barme Gludio (64), le seul qui ait
        // ete calibre en regardant le rendu. Elles ne s'appliquent QUE si la
        // texture L2 n'a pas d'entree dans scaleMatches - donc rien ne change
        // tant que les anciennes valeurs 1-7 sont en place.
        // A ajuster a l'oeil : c'est ici qu'on regle un pack pour de bon.
        packDefaultScales = new Dictionary<string, float>();
        packDefaultScales.Add("Icelandic_Jagged_Slate_Rock_shfsaida_1K", 32);
        packDefaultScales.Add("Wild_Grass_pjwgW0_1K", 64);
        packDefaultScales.Add("Soil_Sand_pjErQ0_1K", 64);
        packDefaultScales.Add("Rough_Soil_Detail_Texture_se4mcazf0_1K", 64);
        packDefaultScales.Add("Grass_Patchy_pjvtA0_1K", 64);
        packDefaultScales.Add("grass_and_rubble_pjwdt0_1k", 64);
        packDefaultScales.Add("Thai_Beach_Sand_tefnah1q_1K", 64);
        packDefaultScales.Add("Swamp_Soil_tjmhfcjl_1K", 32);

        // Les 20 packs ajoutes le 2026-08-10 n'avaient aucun defaut : leurs
        // textures retombaient donc sur DefaultSplatUvScale (32) au lieu du 64
        // qui est la valeur de reference des packs Megascans du projet.
        //
        // C'est ce que montrait l'asset de reglages : une echelle a 0 ne veut
        // pas dire "non renseignee", elle veut dire "herite du pack". Sans
        // entree ici, l'heritage n'avait rien a heriter.
        //
        // Corriger a ce niveau plutot que sur les 364 textures : un pack se
        // regle en un endroit, et toutes ses textures suivent.
        packDefaultScales.Add("Sandy_Soil_pkcbc0_1K", 64);
        packDefaultScales.Add("Dry_Fallen_Leaves_vetladiaw_1K", 64);
        packDefaultScales.Add("Forest_Floor_sfjmafua_1K", 64);
        packDefaultScales.Add("Forest_Path_ugsnfawlw_1K", 64);
        packDefaultScales.Add("Fresh_Windswept_Snow_ugspafgdy_1K", 64);
        packDefaultScales.Add("Trampled_Snow_vcqnfdk_1K", 64);
        packDefaultScales.Add("Gouged_Rock_Cliff_vlcpcbc_1K", 64);
        packDefaultScales.Add("Layered_Rock_Cliff_tjtmcg3g_1K", 64);
        packDefaultScales.Add("Rock_Cliff_xccibbi_1K", 64);
        packDefaultScales.Add("Mine_Rock_Wall_uebmddyn_1K", 64);
        packDefaultScales.Add("grass_and_rubble_pjwey0_1k", 64);
        packDefaultScales.Add("Grass_Dried_pjvvl0_1K", 64);
        packDefaultScales.Add("Grassy_Soil_xbreair_1K", 64);
        packDefaultScales.Add("Gravel_Ground_vi0maebg_1K", 64);
        packDefaultScales.Add("Ground_Roots_smspdebp_1K", 64);
        packDefaultScales.Add("Moist_Fallen_Leaves_rmqlw0p0_1K", 64);
        packDefaultScales.Add("Mossy_Forest_Floor_vfylbge_1K", 64);
        packDefaultScales.Add("Rocky_Sand_vd4pbdt_1K", 64);
        packDefaultScales.Add("Rocky_Steppe_ulgmbhwn_1K", 64);
        packDefaultScales.Add("Uncut_Grass_oeeb70_1K", 64);
        packDefaultScales.Add("Uncut_Grass_oilpt20_1K", 64);

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

        // EN DERNIER : l'asset de reglages doit pouvoir remplacer tout ce qui
        // precede. Sans asset, cet appel ne fait rien.
        ApplySettingsAsset();
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

            // Passe par TryGetTextureMatch (et non par la table globale seule)
            // pour qu'une texture couverte par une surcharge de region ne soit
            // pas signalee a tort comme manquante.
            if (!Instance.TryGetTextureMatch(terrainInfo.mapName, texName, out _)
                && !missing.Contains(texName))
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

            if (!Instance.TryGetScaleMatch(terrainInfo.mapName, texName, out _)
                && !missing.Contains(texName))
            {
                missing.Add(texName);
            }
        }

        return missing;
    }
}