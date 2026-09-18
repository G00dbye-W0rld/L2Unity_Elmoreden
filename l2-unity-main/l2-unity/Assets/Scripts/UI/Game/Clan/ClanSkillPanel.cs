using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Onglet Competences de la fenetre de clan : les competences deja apprises.
// L'apprentissage passe par le maitre de village, comme dans le jeu d'origine.
public class ClanSkillPanel
{
    private readonly ScrollView _list;
    private readonly Label _empty;
    private readonly Label _detailName;
    private readonly Label _detailDescription;

    private int _selected;

    public ClanSkillPanel(VisualElement page)
    {
        _list = page.Q<ScrollView>("SkillList");
        _empty = page.Q<Label>("SkillEmpty");
        _detailName = page.Q<Label>("SkillDetailName");
        _detailDescription = page.Q<Label>("SkillDetailDescription");
    }

    public void Refresh()
    {
        _list.Clear();
        bool any = false;

        foreach (KeyValuePair<int, int> skill in ClanData.Skills)
        {
            _list.Add(BuildRow(skill.Key, skill.Value));
            any = true;
        }

        _empty.text = any ? string.Empty : "Aucune comp\u00e9tence de clan. Le chef les apprend aupr\u00e8s d'un ma\u00eetre de village.";
        RefreshDetail();
    }

    private VisualElement BuildRow(int id, int level)
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("clan-member");
        row.AddToClassList("online");
        row.EnableInClassList("selected", id == _selected);
        row.RegisterCallback<MouseUpEvent>(evt =>
        {
            _selected = _selected == id ? 0 : id;
            Refresh();
        });

        VisualElement icon = new VisualElement();
        icon.AddToClassList("clan-skill-icon");
        icon.pickingMode = PickingMode.Ignore;
        Skillgrp grp = SkillgrpTable.Instance.GetSkill(id, level);
        if (grp != null)
        {
            Texture2D texture = IconTable.Instance.LoadTextureByName(grp.Icon);
            if (texture != null)
            {
                icon.style.backgroundImage = new StyleBackground(texture);
            }
        }
        row.Add(icon);

        row.Add(Cell(SkillName(id, level), "clan-col-name"));
        row.Add(Cell(level.ToString(), "clan-col-level"));
        return row;
    }

    private void RefreshDetail()
    {
        int level = 0;
        foreach (KeyValuePair<int, int> skill in ClanData.Skills)
        {
            if (skill.Key == _selected)
            {
                level = skill.Value;
            }
        }

        if (level == 0)
        {
            _selected = 0;
            _detailName.text = string.Empty;
            _detailDescription.text = string.Empty;
            return;
        }

        _detailName.text = SkillName(_selected, level) + " - niveau " + level;
        _detailDescription.text = Description(_selected, level);
    }

    // Seul le niveau 1 porte le texte ; chaque niveau fournit ses propres valeurs.
    private static string Description(int id, int level)
    {
        SkillNameData data = SkillNameTable.Instance.GetName(id, level);
        if (data == null)
        {
            return string.Empty;
        }

        string text = data.Desc;
        if (string.IsNullOrEmpty(text))
        {
            SkillNameData first = SkillNameTable.Instance.GetName(id, 1);
            text = first != null ? first.Desc : string.Empty;
        }

        string[] values = data.DescParams;
        for (int i = 0; values != null && i < values.Length && text != null; i++)
        {
            text = text.Replace("$s" + (i + 1), values[i]);
        }

        return text ?? string.Empty;
    }

    private static string SkillName(int id, int level)
    {
        SkillNameData data = SkillNameTable.Instance.GetName(id, level);
        return data != null && !string.IsNullOrEmpty(data.Name) ? data.Name : "Comp\u00e9tence " + id;
    }

    private static Label Cell(string text, string columnClass)
    {
        Label label = new Label(text);
        label.AddToClassList(columnClass);
        label.pickingMode = PickingMode.Ignore;
        return label;
    }
}
