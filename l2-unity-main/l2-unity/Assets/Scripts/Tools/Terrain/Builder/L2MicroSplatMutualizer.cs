#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// Mutualisation des configurations MicroSplat.
///
/// LE PROBLEME
/// Chaque region possede SON shader MicroSplat genere - 153 programmes
/// reellement distincts, pas des variantes : MicroSplat cuit ses
/// fonctionnalites dans le code source du shader et n'utilise aucun mot-cle
/// runtime (m_ValidKeywords est vide sur tous les materiaux).
///
/// A la premiere image ou les terrains deviennent visibles, le pilote doit
/// donc creer autant de pipeline states qu'il y a de regions a l'ecran. C'est
/// l'hypothese retenue pour les resets GPU du 12-13/08/2026, qui survenaient
/// 40 lignes de log apres "Stop Loading!" - donc au tout premier rendu.
///
/// LE PRINCIPE
/// Un seul tableau de textures partage, indexe par PACK PBR et non par nom de
/// texture L2. La substitution ramene deja 364 noms L2 a 32 packs ; c'est elle
/// qui rend la mutualisation possible, et elle n'existait pas avant ce mois-ci.
///
/// LES PALIERS
/// MicroSplat plafonne le nombre de textures echantillonnees par un mot-cle
/// compile : _MAX4TEXTURES a _MAX32TEXTURES. Comme l'index de couche du terrain
/// EST l'index dans le tableau (aucune table de remappage en Core), une region
/// doit porter autant de couches que le plus grand index qu'elle utilise. En
/// ordonnant le tableau par frequence d'usage, les packs courants occupent les
/// premieres tranches et la plupart des regions restent courtes.
///
/// Mesure du 2026-08-13 sur les 152 regions : 47 tiennent en 4 couches, 77 en
/// 12, 106 en 16, et la totalite en 28. Trois ou quatre shaders partages
/// suffisent donc a remplacer 153 programmes.
///
/// CET OUTIL N'ECRIT RIEN
/// Il calcule et rapporte. La reindexation des splatmaps - qui reecrit le
/// travail des level designers de 2006, seule donnee non regenerable du projet
/// - fait l'objet d'un outil separe, a n'ecrire qu'une fois cette analyse
/// verifiee a l'oeil.
public static class L2MicroSplatMutualizer
{
    private const string MapsFolder = "Assets/Resources/Data/Maps";

    /// Regions du prototype. Volontairement etroit : on mesure avant de
    /// convertir. Les quatre regions de reference (16_24, 16_25, 17_24, 17_25)
    /// en sont exclues - un shader partage imposerait son jeu de
    /// fonctionnalites, or elles ne doivent pas changer.
    private static readonly string[] TestRegions = { "17_22", "17_23", "18_22", "18_23" };

    /// Paliers offerts par MicroSplat, dans l'ordre croissant.
    private static readonly int[] Tiers = { 4, 8, 12, 16, 20, 24, 28, 32 };


    /// Ordre retenu pour le tableau partage, expose a l'execution.
    ///
    /// CRITIQUE : l'analyse et l'application DOIVENT produire le meme ordre.
    /// C'est lui qui definit ce que signifie chaque tranche ; deux ordres
    /// differents rendraient les splatmaps deja reindexees silencieusement
    /// fausses. On passe donc par cette unique fonction plutot que de dupliquer
    /// la regle de selection.
    public static List<string> BuildOrderForApply(Dictionary<string, List<string>> layersOf,
                                                  Dictionary<string, string> packOf)
    {
        List<string> byFrequency = BuildSharedOrder(layersOf, packOf);
        List<string> optimised = BuildOptimisedOrder(layersOf, packOf);

        return ScoreOrder(optimised, layersOf, packOf) <= ScoreOrder(byFrequency, layersOf, packOf)
            ? optimised
            : byFrequency;
    }

    /// Ordre du tableau partage : les packs les plus repandus d'abord.
    ///
    /// C'est ce qui permet aux paliers de fonctionner. Un ordre arbitraire
    /// placerait un pack rare en tranche basse et forcerait toutes les regions
    /// qui l'utilisent a porter des couches inutiles.
    private static List<string> BuildSharedOrder(Dictionary<string, List<string>> layersOf,
                                                 Dictionary<string, string> packOf)
    {
        var regionsPerPack = new Dictionary<string, HashSet<string>>();

        foreach (var kv in layersOf)
        {
            foreach (string l2 in kv.Value)
            {
                if (!packOf.TryGetValue(l2, out string pack) || string.IsNullOrEmpty(pack))
                {
                    continue;
                }

                if (!regionsPerPack.TryGetValue(pack, out HashSet<string> set))
                {
                    set = new HashSet<string>();
                    regionsPerPack[pack] = set;
                }
                set.Add(kv.Key);
            }
        }

        // Le tri secondaire par nom rend l'ordre reproductible : deux packs a
        // egalite ne doivent pas changer de tranche d'une execution a l'autre,
        // sans quoi les splatmaps deja reindexees deviendraient fausses.
        return regionsPerPack
            .OrderByDescending(kv => kv.Value.Count)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => kv.Key)
            .ToList();
    }

    /// Ordre optimise : on cherche a MINIMISER le total des textures de
    /// controle, pas a classer les packs par popularite.
    ///
    /// POURQUOI L'ORDRE PAR FREQUENCE NE SUFFIT PAS
    /// Le cout d'une region ne depend que du PLUS GRAND index qu'elle utilise :
    /// une region qui touche un seul pack rare porte toutes les couches
    /// jusqu'a lui, meme vides. Classer par popularite repousse donc les packs
    /// rares en fin de tableau et penalise lourdement les quelques regions qui
    /// s'en servent - mesure du 2026-08-13 : x1,68 de textures de controle.
    ///
    /// L'HEURISTIQUE
    /// A chaque position, on choisit le pack qui ACHEVE le plus de regions,
    /// c'est-a-dire celui apres lequel le plus de regions n'ont plus rien a
    /// attendre. Une region achevee cesse de grandir. A egalite on prend le
    /// plus repandu, puis le nom, pour rester reproductible.
    private static List<string> BuildOptimisedOrder(Dictionary<string, List<string>> layersOf,
                                                    Dictionary<string, string> packOf)
    {
        var packsOfRegion = new Dictionary<string, HashSet<string>>();
        var regionsOfPack = new Dictionary<string, HashSet<string>>();

        foreach (var kv in layersOf)
        {
            foreach (string l2 in kv.Value)
            {
                if (!packOf.TryGetValue(l2, out string pack) || string.IsNullOrEmpty(pack))
                {
                    continue;
                }

                if (!packsOfRegion.TryGetValue(kv.Key, out HashSet<string> ps))
                {
                    ps = new HashSet<string>();
                    packsOfRegion[kv.Key] = ps;
                }
                ps.Add(pack);

                if (!regionsOfPack.TryGetValue(pack, out HashSet<string> rs))
                {
                    rs = new HashSet<string>();
                    regionsOfPack[pack] = rs;
                }
                rs.Add(kv.Key);
            }
        }

        var remaining = new HashSet<string>(regionsOfPack.Keys);
        var placed = new HashSet<string>();
        var order = new List<string>();

        while (remaining.Count > 0)
        {
            string best = null;
            int bestCompleted = -1;
            int bestReach = -1;

            foreach (string candidate in remaining.OrderBy(p => p, StringComparer.Ordinal))
            {
                placed.Add(candidate);

                // Regions dont TOUS les packs seraient poses en placant celui-ci.
                int completed = regionsOfPack[candidate]
                    .Count(r => packsOfRegion[r].IsSubsetOf(placed));

                placed.Remove(candidate);

                int reach = regionsOfPack[candidate].Count;

                if (completed > bestCompleted || (completed == bestCompleted && reach > bestReach))
                {
                    best = candidate;
                    bestCompleted = completed;
                    bestReach = reach;
                }
            }

            order.Add(best);
            placed.Add(best);
            remaining.Remove(best);
        }

        return order;
    }

    /// Total des textures de controle qu'un ordre donne imposerait.
    private static int ScoreOrder(List<string> order, Dictionary<string, List<string>> layersOf,
                                  Dictionary<string, string> packOf)
    {
        var indexOf = new Dictionary<string, int>();
        for (int i = 0; i < order.Count; i++)
        {
            indexOf[order[i]] = i;
        }

        int total = 0;

        foreach (var kv in layersOf)
        {
            int max = -1;
            foreach (string l2 in kv.Value)
            {
                if (packOf.TryGetValue(l2, out string pack) && indexOf.TryGetValue(pack, out int idx))
                {
                    max = Math.Max(max, idx);
                }
            }

            if (max < 0)
            {
                continue;
            }

            int tier = Tiers.FirstOrDefault(t => t >= max + 1);
            total += tier > 0 ? (tier + 3) / 4 : 0;
        }

        return total;
    }

    private static Dictionary<string, string> BuildSubstitutionMap(L2TerrainTextureSettings settings)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in settings.substitutions)
        {
            if (!string.IsNullOrEmpty(s.l2Texture) && !string.IsNullOrEmpty(s.pbrPack))
            {
                map[s.l2Texture] = s.pbrPack;
            }
        }

        return map;
    }

    /// Les noms L2 dans l'ordre des couches, lus depuis les assets
    /// "{region}_layer_{index}_{nom}.asset" produits par l'import.
    ///
    /// C'est la meme source que RecoverL2LayerNames : le nom L2 d'ORIGINE, et
    /// non celui de la texture actuellement posee - qui a deja ete substituee
    /// et ne permettrait pas de retrouver la regle a appliquer.
    private static Dictionary<string, List<string>> ReadLayers(string[] regions)
    {
        var result = new Dictionary<string, List<string>>();

        foreach (string region in regions)
        {
            string folder = $"{MapsFolder}/{region}/TerrainData";
            if (!Directory.Exists(folder))
            {
                continue;
            }

            var pattern = new Regex($@"^{Regex.Escape(region)}_layer_(\d+)_(.+)$");
            var found = new List<(int index, string name)>();

            foreach (string path in Directory.GetFiles(folder, $"{region}_layer_*.asset"))
            {
                Match m = pattern.Match(Path.GetFileNameWithoutExtension(path));
                if (m.Success && int.TryParse(m.Groups[1].Value, out int idx))
                {
                    found.Add((idx, m.Groups[2].Value));
                }
            }

            if (found.Count > 0)
            {
                result[region] = found.OrderBy(f => f.index).Select(f => f.name).ToList();
            }
        }

        return result;
    }

    private static string[] EnumerateRegions()
    {
        if (!Directory.Exists(MapsFolder))
        {
            return new string[0];
        }

        return Directory.GetDirectories(MapsFolder)
            .Select(Path.GetFileName)
            .Where(n => Regex.IsMatch(n, @"^\d+_\d+$"))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
    }
}
#endif
