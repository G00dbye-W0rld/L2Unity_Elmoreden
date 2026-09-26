using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Panoplies d'armure : quelles pieces vont ensemble et quelle competence elles donnent.
// Le fichier est genere depuis data/xml/armorSets.xml du serveur (voir Tools/GenerateArmorSets).
public class ArmorSetTable
{
    [Serializable]
    public class ArmorSet
    {
        public string name;
        public int skillId;
        public int shield;
        public int shieldSkillId;
        public int enchant6Skill;
        public int[] pieces;
    }

    [Serializable]
    private class ArmorSetList
    {
        public ArmorSet[] sets;
    }

    private static ArmorSetTable _instance;
    public static ArmorSetTable Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new ArmorSetTable();
                _instance.Load();
            }

            return _instance;
        }
    }

    private readonly Dictionary<int, ArmorSet> _byPiece = new Dictionary<int, ArmorSet>();

    private void Load()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Data/Meta/ArmorSets.json");
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[ArmorSetTable] {path} introuvable.");
            return;
        }

        ArmorSetList list = JsonUtility.FromJson<ArmorSetList>(File.ReadAllText(path));
        if (list?.sets == null)
        {
            return;
        }

        foreach (ArmorSet set in list.sets)
        {
            foreach (int piece in set.pieces)
            {
                _byPiece[piece] = set;
            }

            if (set.shield != 0)
            {
                _byPiece[set.shield] = set;
            }
        }

        Debug.Log($"Loaded {list.sets.Length} armor set(s).");
    }

    public ArmorSet GetSetByPiece(int itemId)
    {
        _byPiece.TryGetValue(itemId, out ArmorSet set);
        return set;
    }
}
