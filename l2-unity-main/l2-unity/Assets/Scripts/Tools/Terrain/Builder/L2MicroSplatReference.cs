#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JBooth.MicroSplat;
using UnityEditor;
using UnityEngine;
using static JBooth.MicroSplat.MicroSplatPropData;

/// Aligne le MicroSplat des regions importees sur celui des regions de
/// reference, et propage les reglages par texture d'une region a l'autre.
///
/// LE PROBLEME QUE CE FICHIER RESOUT
/// Les regions de reference ont ete reglees a la main dans l'editeur. Le
/// pipeline d'import, lui, ne coche presque aucune fonctionnalite MicroSplat.
/// Mesure des mots-cles actifs :
///
///   17_24 / 16_25 (reference) : 20 mots-cles
///   18_19 / 24_18 (importees) : 10 mots-cles
///
/// Les dix manquants sont precisement les reglages PAR TEXTURE :
/// _PERTEXSMOOTHSTR, _PERTEXNORMSTR, _PERTEXTINT, _PERTEXBRIGHTNESS,
/// _PERTEXCONTRAST, _PERTEXSATURATION, _PERTEXHEIGHTOFFSET,
/// _PERTEXHEIGHTCONTRAST, plus _TRIPLANAR et _TRIPLANARHEIGHTBLEND.
///
/// Consequence : sur une region importee, ecrire une valeur par texture dans le
/// propdata ne produit RIEN - son shader ne lit meme pas ces cases. C'est la
/// raison pour laquelle le correctif de brillance restait sans effet.
///
/// LE PIEGE A EVITER
/// Activer un mot-cle ne suffit pas : le propdata d'une region importee est
/// vierge, donc a ZERO. Or zero n'est pas neutre. _PERTEXNORMSTR a zero met la
/// force des normales a 0 et aplatit tout le relief. Il faut donc TOUJOURS
/// semer des valeurs saines en meme temps qu'on active les fonctionnalites -
/// et les regions de reference sont la meilleure source possible pour cela.
///
/// L'HOMOGENEITE
/// La bibliotheque est indexee par NOM DE TEXTURE FINALE (apres substitution).
/// Une meme texture rencontree dans deux regions recoit donc exactement les
/// memes reglages, quel que soit l'ordre de traitement. Regler une texture une
/// fois suffit a la regler partout.
public static class L2MicroSplatReference
{
    /// Regions faisant autorite. Ce sont celles reglees a la main ; elles ne
    /// sont jamais modifiees par cet outil, seulement lues.
    public static readonly string[] ReferenceRegions = { "17_24", "17_25", "16_24", "16_25" };

    /// Mots-cles a ne PAS recopier : ils decrivent le pipeline de rendu, pas
    /// l'apparence. Les recopier d'une region a l'autre produirait des shaders
    /// incoherents avec la version d'URP effectivement utilisee.
    private const string RenderLoopPrefix = "_MSRENDERLOOP";

    /// Voir L2TerrainGeneratorTool : decalage ADDITIF, d'ou le signe negatif.
    private const float MatteSmoothnessOffset = -0.95f;

    private static string MicroSplatPath(string mapName, string asset)
    {
        return Path.Combine("Data", "Maps", mapName, "TerrainData", "MicroSplatData", asset);
    }

    /// Reglages par texture releves sur les regions de reference.
    /// Cle : nom de la texture finale. Valeur : toutes les lignes du propdata.
    private class ReferenceLibrary
    {
        public readonly Dictionary<string, Color[]> byTexture = new Dictionary<string, Color[]>();
        public Color[] baseline;
        public readonly List<string> keywords = new List<string>();
    }

    private static ReferenceLibrary BuildLibrary()
    {
        var lib = new ReferenceLibrary();

        foreach (string region in ReferenceRegions)
        {
            var cfg = Resources.Load<TextureArrayConfig>(MicroSplatPath(region, "MicroSplatConfig"));
            var propData = Resources.Load<MicroSplatPropData>(MicroSplatPath(region, "MicroSplat_propdata"));
            var keywords = Resources.Load<MicroSplatKeywords>(MicroSplatPath(region, "MicroSplat_keywords"));

            if (keywords != null)
            {
                foreach (string k in keywords.keywords)
                {
                    if (!k.StartsWith(RenderLoopPrefix) && !lib.keywords.Contains(k))
                    {
                        lib.keywords.Add(k);
                    }
                }
            }

            if (cfg == null || propData == null)
            {
                continue;
            }

            for (int i = 0; i < cfg.sourceTextures.Count; i++)
            {
                Texture2D diffuse = cfg.sourceTextures[i].diffuse;
                if (diffuse == null)
                {
                    continue;
                }

                Color[] values = propData.GetAllValues(i);

                // La premiere region de reference fait foi : on n'ecrase pas
                // une entree deja relevee, pour que le resultat ne depende pas
                // de l'ordre de parcours.
                if (!lib.byTexture.ContainsKey(diffuse.name))
                {
                    lib.byTexture[diffuse.name] = values;
                }

                lib.baseline ??= values;
            }
        }

        return lib;
    }

    /// Aligne une region sur les references. Retourne false en cas d'echec.
    public static bool AlignFor(string mapName)
    {
        if (ReferenceRegions.Contains(mapName))
        {
            Debug.Log($"[Microsplat] {mapName} est une region de reference : laissee intacte.");
            return true;
        }

        ReferenceLibrary lib = BuildLibrary();
        if (lib.baseline == null || lib.keywords.Count == 0)
        {
            Debug.LogError("[Microsplat] Impossible de lire les regions de reference "
                           + $"({string.Join(", ", ReferenceRegions)}). Alignement annule.");
            return false;
        }

        GameObject terrainGo = GameObject.Find(L2TerrainGenerator.TerrainObjectName(mapName))
                               ?? GameObject.Find(mapName);
        Terrain terrain = terrainGo != null ? terrainGo.GetComponent<Terrain>() : null;
        MicroSplatTerrain mst = terrain != null ? terrain.GetComponent<MicroSplatTerrain>() : null;

        if (mst == null || mst.templateMaterial == null || mst.propData == null)
        {
            Debug.LogError($"[Microsplat] '{mapName}' n'a pas de MicroSplatTerrain exploitable dans la scene ouverte.");
            return false;
        }

        var cfg = Resources.Load<TextureArrayConfig>(MicroSplatPath(mapName, "MicroSplatConfig"));
        if (cfg == null)
        {
            Debug.LogError($"[Microsplat] TextureArrayConfig introuvable pour '{mapName}'.");
            return false;
        }

        // 1. Semer les valeurs AVANT d'activer les mots-cles : a l'instant ou
        //    une fonctionnalite s'active, le shader lit le propdata. S'il y
        //    trouve des zeros, le rendu casse (normales aplaties).
        int fromReference = 0, fromBaseline = 0, mattified = 0;
        for (int i = 0; i < cfg.sourceTextures.Count; i++)
        {
            Texture2D diffuse = cfg.sourceTextures[i].diffuse;
            string texName = diffuse != null ? diffuse.name : null;

            // L'ECHELLE UV NE DOIT PAS ETRE IMPORTEE.
            //
            // SetAllValues recopie TOUTES les lignes du propdata, et
            // PerTexVector2.SplatUVScale occupe la ligne 0. Sans cette
            // sauvegarde, aligner une region lui collait le carrelage de
            // Talking Island - dont les valeurs heritees sont tres basses
            // (1 a 7, soit un motif etire sur des centaines d'unites) alors que
            // les packs PBR du projet tournent entre 32 et 64.
            //
            // L'alignement doit transporter l'APPARENCE (brillance, force des
            // normales, teinte, contraste), pas la GEOMETRIE du carrelage :
            // celle-ci est deja resolue par la table de substitution, avec ses
            // trois niveaux surcharge de region / texture / pack.
            // La ligne 0 stocke l'echelle dans ses canaux r et g.
            Color uvRow = mst.propData.GetValue(i, (int)PerTexVector2.SplatUVScale / 4);
            Vector2 uvScale = new Vector2(uvRow.r, uvRow.g);

            if (texName != null && lib.byTexture.TryGetValue(texName, out Color[] values))
            {
                mst.propData.SetAllValues(i, values);
                fromReference++;
            }
            else
            {
                mst.propData.SetAllValues(i, lib.baseline);
                fromBaseline++;
            }

            // Une echelle nulle donnerait un terrain sans texture visible. Ca ne
            // devrait pas arriver - l'etape 06 en pose toujours une - mais si
            // c'est le cas, mieux vaut garder celle de la reference que d'ecrire
            // un zero.
            if (uvScale.x > 0f && uvScale.y > 0f)
            {
                mst.propData.SetValue(i, PerTexVector2.SplatUVScale, uvScale);
            }

            // Une texture sans carte de brillance verrait MicroSplat lire
            // l'alpha de sa diffuse - opaque, donc brillance 1.0, donc miroir.
            // Le decalage negatif la ramene a un sol mat.
            if (cfg.sourceTextures[i].smoothness == null)
            {
                mst.propData.SetValue(i, PerTexFloat.Smoothness, MatteSmoothnessOffset);
                mattified++;
            }
        }
        EditorUtility.SetDirty(mst.propData);

        // 2. Activer les fonctionnalites, puis regenerer le shader.
        MicroSplatKeywords targetKeywords = MicroSplatUtilities.FindOrCreateKeywords(mst.templateMaterial);
        int enabled = 0;
        foreach (string k in lib.keywords)
        {
            if (!targetKeywords.IsKeywordEnabled(k))
            {
                targetKeywords.EnableKeyword(k);
                enabled++;
            }
        }
        EditorUtility.SetDirty(targetKeywords);

        if (enabled > 0)
        {
            new MicroSplatShaderGUI.MicroSplatCompiler().Compile(mst.templateMaterial);
        }

        mst.Sync();

        Debug.Log($"[Microsplat] {mapName} aligne sur les references : "
                  + $"{enabled} fonctionnalite(s) activee(s), "
                  + $"{fromReference} texture(s) reglee(s) d'apres une reference, "
                  + $"{fromBaseline} d'apres le socle neutre, "
                  + $"{mattified} rendue(s) mate(s).");
        return true;
    }
}
#endif
