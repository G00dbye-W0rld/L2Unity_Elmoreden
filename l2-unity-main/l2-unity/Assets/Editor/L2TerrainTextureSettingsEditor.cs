using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Inspector de l'asset de reglages des textures de terrain.
///
/// POURQUOI IL EXISTE
/// L'Inspector par defaut affiche les 364 substitutions comme une liste brute,
/// sans recherche ni tri. Pour regler une texture il fallait la faire defiler,
/// ce qui rendait le reglage fin decourageant en pratique.
///
/// Cet editeur n'ajoute AUCUNE donnee : il ne fait que filtrer et presenter ce
/// que l'asset contient deja. Les valeurs restent editables une par une, et
/// tout ce qui est modifie ici est ecrit dans l'asset comme avant.
[CustomEditor(typeof(L2TerrainTextureSettings))]
public class L2TerrainTextureSettingsEditor : Editor
{
    private string _filter = string.Empty;
    private bool _showPacks = true;
    private bool _showRegions = true;
    private Vector2 _scroll;

    /// Nombre de regions ou chaque texture L2 apparait, pour afficher les plus
    /// structurantes en premier. Calcule une fois, puis garde en cache : le
    /// scan porte sur les fichiers de couches de 153 regions.
    private static Dictionary<string, int> _usage;

    private const int MaxShown = 40;

    private readonly List<int> _matches = new List<int>();
    private string _cachedFilter = null;
    private int _cachedCount = -1;

    /// Filtre et trie en lisant les listes C# plutot que l'API serialisee, qui
    /// est bien plus lente. Appele uniquement quand la recherche change.
    private void RebuildMatches(L2TerrainTextureSettings settings, int count)
    {
        _matches.Clear();
        string needle = _filter.Trim().ToLowerInvariant();

        for (int i = 0; i < count && i < settings.substitutions.Count; i++)
        {
            var s = settings.substitutions[i];
            string tex = s.l2Texture ?? string.Empty;
            string pack = s.pbrPack ?? string.Empty;

            if (needle.Length == 0
                || tex.ToLowerInvariant().Contains(needle)
                || pack.ToLowerInvariant().Contains(needle))
            {
                _matches.Add(i);
            }
        }

        // Les textures presentes dans le plus de regions d'abord : ce sont
        // celles dont un reglage change le plus de choses.
        _matches.Sort((a, b) =>
        {
            string ta = settings.substitutions[a].l2Texture ?? string.Empty;
            string tb = settings.substitutions[b].l2Texture ?? string.Empty;
            Usage.TryGetValue(ta, out int ua);
            Usage.TryGetValue(tb, out int ub);
            return ua != ub ? ub.CompareTo(ua) : string.Compare(ta, tb, System.StringComparison.Ordinal);
        });
    }

    private static Dictionary<string, int> Usage
    {
        get
        {
            if (_usage != null)
            {
                return _usage;
            }

            _usage = new Dictionary<string, int>();
            foreach (string guid in AssetDatabase.FindAssets(
                         "t:TerrainLayer", new[] { "Assets/Resources/Data/Maps" }))
            {
                string file = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));

                int marker = file.IndexOf("_layer_", System.StringComparison.Ordinal);
                if (marker < 0)
                {
                    continue;
                }

                string rest = file.Substring(marker + "_layer_".Length);
                int sep = rest.IndexOf('_');
                if (sep <= 0 || !int.TryParse(rest.Substring(0, sep), out _))
                {
                    continue;
                }

                string tex = rest.Substring(sep + 1);
                _usage.TryGetValue(tex, out int n);
                _usage[tex] = n + 1;
            }

            return _usage;
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var settings = (L2TerrainTextureSettings)target;

        EditorGUILayout.HelpBox(
            "Echelle a 0 = herite du defaut de son pack.\n"
            + "Priorite : surcharge de region > substitution > regles automatiques.",
            MessageType.None);

        // ---- Recherche -------------------------------------------------
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Rechercher", GUILayout.Width(70));
            _filter = EditorGUILayout.TextField(_filter);
            if (GUILayout.Button("X", GUILayout.Width(24)))
            {
                _filter = string.Empty;
                GUI.FocusControl(null);
            }
        }

        var subs = serializedObject.FindProperty("substitutions");

        // FILTRE CALCULE UNE SEULE FOIS, PAS A CHAQUE FRAME.
        //
        // OnInspectorGUI est appele en continu tant que l'Inspector est
        // visible. La premiere version reconstruisait la liste a chaque appel :
        // 364 entrees, trois lectures de SerializedProperty chacune, plus un tri
        // - soixante fois par seconde. L'editeur en devenait poussif.
        //
        // On ne recalcule donc que si la recherche a change ou si la taille de
        // la liste a bouge. Le filtrage lit les listes C# directement, bien plus
        // rapides que l'API serialisee.
        if (_filter != _cachedFilter || _cachedCount != subs.arraySize)
        {
            RebuildMatches(settings, subs.arraySize);
            _cachedFilter = _filter;
            _cachedCount = subs.arraySize;
        }

        EditorGUILayout.LabelField(
            $"Substitutions : {_matches.Count} / {subs.arraySize}"
            + (_matches.Count > MaxShown ? $"  (les {MaxShown} premieres affichees)" : string.Empty),
            EditorStyles.miniBoldLabel);

        // ---- Liste filtree ---------------------------------------------
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(420));
        foreach (int i in _matches.Take(MaxShown))
        {
            var e = subs.GetArrayElementAtIndex(i);
            var tex = e.FindPropertyRelative("l2Texture");
            var pack = e.FindPropertyRelative("pbrPack");
            var scale = e.FindPropertyRelative("scale");

            Usage.TryGetValue(tex.stringValue ?? string.Empty, out int regions);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(tex.stringValue, EditorStyles.boldLabel, GUILayout.Width(150));
                    EditorGUILayout.LabelField(
                        regions > 0 ? $"{regions} region(s)" : "inutilisee",
                        EditorStyles.miniLabel, GUILayout.Width(90));
                    EditorGUILayout.LabelField("echelle", GUILayout.Width(50));
                    scale.floatValue = EditorGUILayout.FloatField(scale.floatValue, GUILayout.Width(60));
                }
                EditorGUILayout.PropertyField(pack, new GUIContent("Pack PBR"));
            }
        }
        EditorGUILayout.EndScrollView();

        // ---- Les deux autres sections, repliees par defaut --------------
        EditorGUILayout.Space();
        _showPacks = EditorGUILayout.Foldout(_showPacks,
            $"Echelles par defaut, par pack ({settings.packDefaults.Count})", true);
        if (_showPacks)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("packDefaults"), GUIContent.none, true);
        }

        EditorGUILayout.Space();
        _showRegions = EditorGUILayout.Foldout(_showRegions,
            $"Surcharges par region ({settings.regionOverrides.Count})", true);
        if (_showRegions)
        {
            EditorGUILayout.HelpBox(
                "Prioritaire sur tout le reste. Sert quand une meme texture doit rendre "
                + "differemment selon la region.", MessageType.None);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("regionOverrides"), GUIContent.none, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
