using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ActionNameTable
{
    private static ActionNameTable _instance;

    public static ActionNameTable Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new ActionNameTable();
            }

            return _instance;
        }
    }

    private Dictionary<int, ActionData> _actions;

    public ActionData GetAction(ActionType type)
    {
        return _actions.ContainsKey((int)type) ? _actions[(int)type] : null;
    }

    public Dictionary<int, ActionData> Actions { get { return _actions; } }

    public void Initialize()
    {
        ReadActions();
    }

    private void ReadActions()
    {
        _actions = new Dictionary<int, ActionData>();
        string dataPath = Path.Combine(Application.streamingAssetsPath, "Data/Meta/ActionName_Classic-eu.txt");
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
                ActionData actionData = new ActionData();

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

                    switch (key)
                    {
                        case "id":
                            actionData.Id = int.Parse(value);
                            break;
                        case "cmd":
                            actionData.Name = DatUtils.CleanupString(value);
                            break;
                        case "icon":
                            string[] iconSplit = DatUtils.CleanupString(value).Split(".");
                            actionData.Icon = iconSplit.Length > 1 ? iconSplit[1] : iconSplit[0];
                            break;
                        case "name":
                            actionData.Descripion = DatUtils.CleanupString(value);
                            break;
                        case "type":
                            int.TryParse(value, out int type);
                            actionData.Type = type;
                            break;
                        case "category":
                            int.TryParse(value, out int category);
                            actionData.Category = category;
                            break;
                        case "desc":
                            actionData.Command = DatUtils.CleanupString(value);
                            break;
                    }
                }

                if (!_actions.ContainsKey(actionData.Id))
                {
                    _actions.TryAdd(actionData.Id, actionData);
                }
            }

            Debug.Log($"Successfully imported {_actions.Count} actionName(s)");
        }

        ReadFrenchOverrides();
    }

    // Remplace le nom affiche sans toucher a l'icone ni a la commande de chat.
    private void ReadFrenchOverrides()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Data/Meta/ActionName_fr.txt");
        if (!File.Exists(path))
        {
            return;
        }

        int count = 0;
        foreach (string line in File.ReadAllLines(path, System.Text.Encoding.UTF8))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            int tab = line.IndexOf('\t');
            if (tab <= 0 || !int.TryParse(line.Substring(0, tab), out int id))
            {
                continue;
            }

            if (_actions.TryGetValue(id, out ActionData action))
            {
                action.Name = line.Substring(tab + 1);
                count++;
            }
        }

        Debug.Log($"Loaded {count} french action name(s).");
    }
}
