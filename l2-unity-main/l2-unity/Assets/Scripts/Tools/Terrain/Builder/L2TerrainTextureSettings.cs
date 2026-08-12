using System.Collections.Generic;
using UnityEngine;

/// Table de substitution des textures de terrain, editable dans l'Inspector.
///
/// POURQUOI CET ASSET
/// Les correspondances vivaient uniquement en dur dans
/// L2TerrainGeneratorTextureMatcher : chaque essai demandait de modifier du
/// C#, d'attendre la recompilation d'Unity, puis de relancer l'outil. Regler
/// une echelle a l'oeil devenait un cycle de plusieurs minutes.
///
/// Ici, les memes donnees sont editables directement dans l'Inspector : on
/// change une valeur, on relance "Re-appliquer les substitutions", on regarde.
///
/// L'asset est FACULTATIF : s'il n'existe pas, les tables ecrites en dur
/// s'appliquent telles quelles. S'il existe, ses entrees les COMPLETENT et
/// les REMPLACENT (une meme texture L2 declaree des deux cotes prend la
/// valeur de l'asset).
public class L2TerrainTextureSettings : ScriptableObject
{
    /// Chemin attendu par le matcher. Hors de Resources/ volontairement :
    /// c'est une donnee d'outillage, elle n'a rien a faire dans un build.
    public const string AssetPath =
        "Assets/Scripts/Tools/Terrain/Builder/L2TerrainTextureSettings.asset";

    [System.Serializable]
    public class Substitution
    {
        [Tooltip("Nom court de la texture L2 d'origine, ex. \"Obase_1\"")]
        public string l2Texture;

        [Tooltip("Nom du pack PBR dans Data/External/Textures, ex. \"Wild_Grass_pjwgW0_1K\"")]
        public string pbrPack;

        [Tooltip("Echelle de carrelage. Laisser a 0 pour utiliser celle du pack.")]
        public float scale;
    }

    [System.Serializable]
    public class PackDefault
    {
        [Tooltip("Nom du pack PBR")]
        public string pbrPack;

        [Tooltip("Echelle appliquee a toutes les textures utilisant ce pack, "
                 + "sauf echelle explicite.")]
        public float scale = 64f;
    }

    [System.Serializable]
    public class RegionOverride
    {
        [Tooltip("Identifiant de region, ex. \"22_13\"")]
        public string region;

        [Tooltip("Nom court de la texture L2")]
        public string l2Texture;

        [Tooltip("Pack PBR a utiliser DANS CETTE REGION uniquement")]
        public string pbrPack;

        [Tooltip("Echelle. 0 = celle du pack.")]
        public float scale;
    }

    [Header("Substitutions globales (texture L2 -> pack PBR)")]
    [Tooltip("S'applique a toutes les regions.")]
    public List<Substitution> substitutions = new List<Substitution>();

    [Header("Echelles par defaut, par pack PBR")]
    [Tooltip("Reglage de reference d'un pack. Evite de repeter la meme valeur "
             + "sur chaque texture qui l'utilise.")]
    public List<PackDefault> packDefaults = new List<PackDefault>();

    [Header("Surcharges par region (prioritaires sur tout le reste)")]
    [Tooltip("Pour les cas ou une meme texture L2 doit rendre differemment "
             + "selon la region - ex. une zone enneigee et une zone verdoyante "
             + "partageant Obase_1.")]
    public List<RegionOverride> regionOverrides = new List<RegionOverride>();
}
