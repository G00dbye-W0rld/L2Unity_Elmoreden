using System;
using UnityEngine;

// Comment la source d'emission d'un calque est posee sur l'arme.
public enum EnchantAuraShapeMode
{
    // Emission depuis la SURFACE REELLE du mesh de l'arme.
    //
    // ATTENTION : necessite que le modele d'arme soit importe en
    // "Read/Write Enabled", ce qui duplique le mesh en memoire vive pour
    // CHAQUE arme du jeu. Ecarte volontairement sur ce projet (l'effet est
    // purement esthetique, il ne doit rien couter aux performances). Laisse
    // en place pour le cas ou, mais si le mesh n'est pas lisible le code
    // bascule automatiquement sur Box en le signalant : aucune particule ne
    // serait emise sinon, et sans la moindre erreur en console.
    WeaponMesh,
    // Boite calee sur les dimensions reelles de l'arme. Pour une lame, la
    // boite englobante est naturellement longue et fine : les particules
    // suivent donc deja bien la forme, pour un cout nul.
    Box,
    // Ligne suivant l'axe le plus long de l'arme (le fil de la lame). Le
    // mode le plus economique, et le plus proche d'un "glow" qui court le
    // long de l'arme.
    Edge,
    // Sphere centree sur l'arme - pour des effets qui flottent autour
    // (lucioles, motes) plutot que de la longer.
    Sphere,
    // Aucune modification : garde exactement la forme d'emission telle
    // qu'elle est authoree dans le prefab du Particle Pack.
    PrefabDefault
}

// Un calque = un prefab du Particle Pack pose sur l'arme.
[Serializable]
public class EnchantAuraLayer
{
    [Tooltip("Nom libre, sert uniquement a s'y retrouver dans cette liste.")]
    public string name = "Calque";

    [Tooltip("Decoche pour desactiver ce calque sans le supprimer.")]
    public bool enabled = true;

    [Tooltip("Prefab a poser sur l'arme. Glissez-y n'importe quel prefab du Particle Pack (Plugins/UnityTechnologies/ParticlePack/EffectExamples/...), par exemple FireFlies, TinyFlames, ElectricalSparks, ParticlesLight, SparksEffect.")]
    public GameObject prefab;

    [Tooltip("Comment poser la source d'emission sur l'arme. WeaponMesh = l'effet epouse la forme exacte de l'arme (recommande pour un aura). PrefabDefault = ne touche a rien, garde la forme d'origine du prefab.")]
    public EnchantAuraShapeMode shapeMode = EnchantAuraShapeMode.WeaponMesh;

    [Tooltip("Taille de la zone d'emission, en fraction des dimensions de l'arme. 1 = exactement la taille de l'arme, 1.2 = deborde un peu autour. (En mode PrefabDefault, ce champ met a l'echelle tout le prefab a la place.)")]
    public float scaleRatio = 1f;

    [Tooltip("Decalage de position, en fraction de la longueur de l'arme (X/Y/Z dans l'espace local de l'arme).")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("Rotation appliquee au calque, en degres (espace local de l'arme).")]
    public Vector3 rotationOffset = Vector3.zero;

    [Tooltip("Applique la couleur d'enchant (degrade bleu->rouge) a ce calque. Decochez pour garder les couleurs d'origine du prefab.")]
    public bool applyEnchantColor = true;

    [Tooltip("Multiplicateur de luminosite de la couleur d'enchant sur ce calque. 1 = couleur telle quelle, >1 = plus lumineux/sature.")]
    public float colorIntensity = 1f;

    [Tooltip("Multiplie le debit d'emission d'origine du prefab. 1 = debit d'origine, 2 = deux fois plus dense, 0.5 = moitie moins.")]
    public float densityMultiplier = 1f;

    [Tooltip("Multiplie la taille des particules d'origine du prefab. 1 = taille d'origine.")]
    public float particleSizeMultiplier = 1f;

    [Tooltip("Retire les lumieres temps reel embarquees dans le prefab (FireFlies en contient une). Sur une arme, elles se lisent comme un projecteur qui eclaire le personnage, et coutent cher. Decochez seulement si vous voulez vraiment que l'arme eclaire la scene.")]
    public bool removePrefabLights = true;

    [Tooltip("Opacite globale du calque. 1 = opacite d'origine du prefab, 0.3 = tres transparent. Baissez si l'effet masque l'arme.")]
    [Range(0f, 1f)]
    public float opacity = 1f;

    [Tooltip("Multiplie la duree de vie des particules. <1 = elles disparaissent plus vite (utile pour eviter qu'un nuage s'etale jusqu'au sol).")]
    public float lifetimeMultiplier = 1f;

    [Tooltip("Multiplie la vitesse de depart des particules. <1 = l'effet reste colle a l'arme, >1 = il se disperse davantage.")]
    public float speedMultiplier = 1f;

    [Tooltip("Remplace le materiau de toutes les particules de ce calque. Laissez vide pour garder ceux du prefab. Sert a essayer un autre rendu sans creer de prefab : par exemple Lightning.mat (dans ParticlePack/Materials) pour un rendu d'eclairs. La couleur d'enchant est ensuite appliquee par-dessus, comme sur n'importe quel materiau.")]
    public Material materialOverride;

    [Tooltip("Noms des sous-effets a DESACTIVER dans ce prefab (un par ligne). Le systeme racine porte le nom du prefab lui-meme. Exemple pour LightnigStormCloud : mettez 'LightnigStormCloud' pour retirer le nuage noir et 'Rain' pour retirer la pluie, en ne gardant que 'Particle Lights' (les eclairs). Un nom qui ne correspond a rien declenche un avertissement en console listant les noms valides du prefab.")]
    public string[] disabledSubEffects = new string[0];
}

// Reglages de l'aura d'arme enchantee - asset UNIQUE et partage.
//
// Pourquoi un ScriptableObject plutot que des champs sur Gear : le composant
// Gear est present sur 42 prefabs de personnage (Player_/User_/Pawn_ x race x
// genre). Des champs poses dessus devraient etre re-regles 42 fois, et les
// modifications faites en Play Mode sur une instance de scene sont jetees par
// Unity a l'arret du jeu. Un asset, lui, est un fichier unique, lu par tous
// les personnages, et ses modifications PERSISTENT meme editees en pleine
// partie - c'est ce qui permet de regler l'effet en direct sans repasser par
// le code.
[CreateAssetMenu(fileName = "enchant_aura_settings", menuName = "L2Unity/Enchant Aura Settings")]
public class EnchantAuraSettings : ScriptableObject
{
    // Chemin de chargement (sous un dossier Resources). L'asset doit se
    // trouver a Assets/Resources/<ce chemin>.asset pour etre trouve.
    public const string ResourcesPath = "Data/Effects/weapon_enchant_aura/enchant_aura_settings";

    [Header("Seuils")]
    [Tooltip("Niveau d'enchant a partir duquel l'aura apparait.")]
    public int minEnchantLevel = 4;

    [Tooltip("Niveau d'enchant auquel le degrade atteint sa couleur finale. Au-dela, la couleur reste celle de la fin du degrade.")]
    public int maxEnchantLevel = 20;

    [Header("Couleur")]
    [Tooltip("Couleur de l'aura selon le niveau d'enchant : gauche = niveau minimum (bleu), droite = niveau maximum et au-dela (rouge). Double-cliquez pour editer les paliers.")]
    public Gradient colorByEnchantLevel = CreateDefaultGradient();

    [Header("Calques")]
    [Tooltip("Les effets poses sur l'arme. Ajoutez une entree par prefab du Particle Pack que vous voulez superposer.")]
    public EnchantAuraLayer[] layers = new EnchantAuraLayer[0];

    // Signale a Gear qu'il faut reconstruire les auras : declenche des qu'un
    // champ de cet asset change dans l'Inspector, y compris en Play Mode,
    // pour un apercu immediat sans re-equiper l'arme.
    public static event Action SettingsChanged;

    private void OnValidate()
    {
        if (minEnchantLevel < 0) minEnchantLevel = 0;
        if (maxEnchantLevel <= minEnchantLevel) maxEnchantLevel = minEnchantLevel + 1;
        SettingsChanged?.Invoke();
    }

    // Couleur de l'aura pour un niveau d'enchant donne. Au-dela de
    // maxEnchantLevel, reste sur la couleur de fin (toujours rouge).
    public Color EvaluateColor(int enchantLevel)
    {
        float t = Mathf.InverseLerp(minEnchantLevel, maxEnchantLevel, enchantLevel);
        return colorByEnchantLevel.Evaluate(Mathf.Clamp01(t));
    }

    private static Gradient CreateDefaultGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.25f, 0.5f, 1f), 0f),   // bleu
                new GradientColorKey(new Color(0.5f, 0.9f, 1f), 0.25f), // cyan
                new GradientColorKey(new Color(0.8f, 0.5f, 1f), 0.5f),  // violet
                new GradientColorKey(new Color(1f, 0.6f, 0.2f), 0.75f), // orange
                new GradientColorKey(new Color(1f, 0.15f, 0.1f), 1f)    // rouge
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
        return gradient;
    }
}
