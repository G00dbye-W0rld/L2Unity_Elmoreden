using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ArmorgrpTable
{
    private static ArmorgrpTable _instance;
    public static ArmorgrpTable Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new ArmorgrpTable();
            }

            return _instance;
        }
    }

    private Dictionary<int, Armorgrp> _armorgrps;

    public Dictionary<int, Armorgrp> ArmorGrps { get { return _armorgrps; } }

    public void Initialize()
    {
        ReadArmorGrpDat();
    }

    public void ClearTable()
    {
        _armorgrps.Clear();
        _armorgrps = null;
        _instance = null;
    }

    private void ReadArmorGrpDat()
    {
        _armorgrps = new Dictionary<int, Armorgrp>();
        string dataPath = Path.Combine(Application.streamingAssetsPath, "Data/Meta/Armorgrp_Classic.txt");
        if (!File.Exists(dataPath))
        {
            Debug.LogWarning("File not found: " + dataPath);
            return;
        }

        using (StreamReader reader = new StreamReader(dataPath))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                Armorgrp armorgrp = new Armorgrp();
                armorgrp.Model = new string[ModelTable.RACE_COUNT];
                armorgrp.Texture = new string[ModelTable.RACE_COUNT];
                armorgrp.LowerModel = new string[ModelTable.RACE_COUNT];
                armorgrp.LowerTexture = new string[ModelTable.RACE_COUNT];
                string[] modTex;

                string[] keyvals = line.Split('\t');

                for (int i = 0; i < keyvals.Length; i++)
                {
                    if (!keyvals[i].Contains("="))
                    {
                        continue;
                    }

                    string[] keyval = keyvals[i].Split("=");
                    string key = keyval[0];
                    string value = keyval[1];

                    if (DatUtils.ParseBaseAbstractItemGrpDat(armorgrp, key, value))
                    {
                        continue;
                    }

                    switch (key)
                    {
                        case "body_part": //artifact_a1 = chest, artifact_a2 = legs, artifact_a3 = boots, head = head, artifactbook = gloves, rfinger, lfinger, rear, lear, onepiece,
                            armorgrp.BodyPart = ItemSlotParser.ParseBodyPart(value); //TODO for fullbody store 2 models and textures for one item
                            break;
                        case "m_HumnFigh": // {{[Fighter.MFighter_m002_g]};{[mfighter.mfighter_m002_t10_g]}}
                            SetRaceModels(armorgrp, CharacterModelType.MFighter, value);
                            break;
                        case "f_HumnFigh":
                            SetRaceModels(armorgrp, CharacterModelType.FFighter, value);
                            break;
                        case "m_DarkElf":
                            SetRaceModels(armorgrp, CharacterModelType.MDarkElf, value);
                            break;
                        case "f_DarkElf":
                            SetRaceModels(armorgrp, CharacterModelType.FDarkElf, value);
                            break;
                        case "m_Dorf":
                            SetRaceModels(armorgrp, CharacterModelType.MDwarf, value);
                            break;
                        case "f_Dorf":
                            SetRaceModels(armorgrp, CharacterModelType.FDwarf, value);
                            break;
                        case "m_Elf":
                            SetRaceModels(armorgrp, CharacterModelType.MElf, value);
                            break;
                        case "f_Elf":
                            SetRaceModels(armorgrp, CharacterModelType.FElf, value);
                            break;
                        case "m_HumnMyst":
                            SetRaceModels(armorgrp, CharacterModelType.MMagic, value);
                            break;
                        case "f_HumnMyst":
                            SetRaceModels(armorgrp, CharacterModelType.FMagic, value);
                            break;
                        case "m_OrcFigh":
                            SetRaceModels(armorgrp, CharacterModelType.MOrc, value);
                            break;
                        case "f_OrcFigh":
                            SetRaceModels(armorgrp, CharacterModelType.FOrc, value);
                            break;
                        case "m_OrcMage":
                            SetRaceModels(armorgrp, CharacterModelType.MShaman, value);
                            break;
                        case "f_OrcMage":
                            SetRaceModels(armorgrp, CharacterModelType.FShaman, value);
                            break;
                        case "mp_bonus": //mp_bonus=0
                            armorgrp.MpBonus = int.Parse(value);
                            break;
                    }
                }

                if (!ItemTable.Instance.ShouldLoadItem(armorgrp.ObjectId))
                {
                    continue;
                }

                _armorgrps.TryAdd(armorgrp.ObjectId, armorgrp);
            }

            Debug.Log($"Successfully imported {_armorgrps.Count} armorgrp(s)");
        }
    }

    // {{[modele]};{[texture]}} ou, pour une armure complete, {{[haut];[bas]};{[tex haut];[tex bas]}} :
    // la premiere moitie donne les modeles, la seconde les textures dans le meme ordre.
    private static void SetRaceModels(Armorgrp armorgrp, CharacterModelType race, string value)
    {
        string[] modTex = DatUtils.ParseArray(value);
        int half = modTex.Length / 2;
        if (half == 0)
        {
            return;
        }

        armorgrp.Model[(byte)race] = modTex[0];
        armorgrp.Texture[(byte)race] = modTex[half];
        if (half >= 2)
        {
            armorgrp.LowerModel[(byte)race] = modTex[1];
            armorgrp.LowerTexture[(byte)race] = modTex[half + 1];
        }
    }
}
