using System.Collections.Generic;

// Contenu de l'infobulle d'un objet : nom, description et statistiques, pretes a afficher.
// Une statistique est "active" quand l'objet est equipe (bleu) ; sinon elle est seulement
// potentielle (gris clair).
public class ItemTooltipInfo
{
    public struct Stat
    {
        public string Label;
        public string Value;
        public bool Active;
    }

    public string Name;
    public string Description;
    public string Icon;
    public string Kind;
    public string Grade;
    public int Count;
    public bool Equipped;
    public List<Stat> Stats = new List<Stat>();
    public string SetName;
    public string SetBonus;
    public bool SetComplete;
    public List<Stat> SetPieces = new List<Stat>();

    public static ItemTooltipInfo Build(ItemInstance item, AbstractItem data, string name, int enchantLevel)
    {
        var info = new ItemTooltipInfo
        {
            Name = enchantLevel > 0 ? $"+{enchantLevel} {name}" : name,
            Description = data?.ItemName != null ? data.ItemName.Description : string.Empty,
            Icon = data != null ? data.Icon : string.Empty,
            Count = item != null ? item.Count : 0,
            Equipped = item != null && item.Equipped,
            Kind = Describe(item, data),
        };

        ItemStatData stats = data?.ItemStatData;
        if (stats != null)
        {
            info.Add("Attaque physique", stats.PAtk);
            info.Add("Attaque magique", stats.MAtk);
            info.Add("Défense physique", stats.PDef);
            info.Add("Défense magique", stats.MDef);
            info.Add("Défense du bouclier", stats.ShieldDef);
            info.Add("Taux de blocage", stats.ShieldDefRate);
            info.Add("Vitesse d'attaque", stats.PAtkSpd);
            info.Add("Critique", stats.PCrit);
            info.Add("Précision", stats.PHit);
            info.Add("Esquive", stats.PAvoid);
            info.Add("Vitesse", stats.Speed);
        }

        // Ce que les tables d'armes et d'armures ajoutent aux statistiques brutes.
        if (data is Weapon weapon && weapon.Weapongrp != null)
        {
            info.Add("Consommation de MP", weapon.Weapongrp.MpConsume);
            info.Add("Soulshots", weapon.Weapongrp.Soulshot);
            info.Add("Spiritshots", weapon.Weapongrp.Spiritshot);
        }

        if (data is Armor armor && armor.Armorgrp != null)
        {
            info.Add("Bonus de MP", armor.Armorgrp.MpBonus);
        }

        if (data?.Itemgrp != null)
        {
            info.Add("Poids", data.Itemgrp.Weight);
            info.Grade = GradeName(data.Itemgrp.Grade);
        }

        info.BuildSet(data);
        return info;
    }

    // Panoplie : les pieces portees apparaissent en bleu, celles qui manquent en gris clair,
    // et le bonus n'est actif que si tout est reuni.
    private void BuildSet(AbstractItem data)
    {
        ArmorSetTable.ArmorSet set = data != null ? ArmorSetTable.Instance.GetSetByPiece(data.Id) : null;
        if (set == null)
        {
            return;
        }

        var worn = new HashSet<int>();
        if (PlayerInventory.Instance != null)
        {
            foreach (ItemInstance item in PlayerInventory.Instance.Items)
            {
                if (item.Equipped)
                {
                    worn.Add(item.ItemId);
                }
            }
        }

        SetName = set.name;
        SetComplete = true;
        foreach (int piece in set.pieces)
        {
            bool equipped = worn.Contains(piece);
            SetComplete &= equipped;
            SetPieces.Add(new Stat { Label = PieceName(piece), Value = equipped ? "porté" : "manquant", Active = equipped });
        }

        if (set.shield != 0)
        {
            bool equipped = worn.Contains(set.shield);
            SetPieces.Add(new Stat { Label = PieceName(set.shield), Value = equipped ? "porté" : "en option", Active = equipped });
        }

        SetBonus = SkillText(set.skillId);
    }

    // Les objets gardent leur ItemName apres le nettoyage memoire, contrairement a ItemNameTable.
    private static string PieceName(int itemId)
    {
        ItemName name = ItemTable.Instance.GetItem(itemId)?.ItemName;
        if (name == null || string.IsNullOrEmpty(name.Name))
        {
            name = ItemNameTable.Instance.GetItemName(itemId);
        }

        return name != null && !string.IsNullOrEmpty(name.Name) ? name.Name : $"Objet {itemId}";
    }

    private static string SkillText(int skillId)
    {
        Skill skill = skillId != 0 ? SkillTable.Instance.GetSkill(skillId) : null;
        SkillNameData text = skill?.SkillNameDatas != null && skill.SkillNameDatas.Length > 0 ? skill.SkillNameDatas[0] : null;
        if (text == null)
        {
            return string.Empty;
        }

        return string.IsNullOrEmpty(text.Desc) ? text.Name : text.Desc;
    }

    private static string GradeName(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.d: return "Grade D";
            case ItemGrade.c: return "Grade C";
            case ItemGrade.b: return "Grade B";
            case ItemGrade.a: return "Grade A";
            case ItemGrade.s: return "Grade S";
            default: return "Sans grade";
        }
    }

    private void Add(string label, int value)
    {
        if (value != 0)
        {
            Stats.Add(new Stat { Label = label, Value = value > 0 ? $"+{value}" : value.ToString(), Active = Equipped });
        }
    }

    private void Add(string label, float value)
    {
        if (value != 0f)
        {
            Stats.Add(new Stat { Label = label, Value = value > 0f ? $"+{value:0.#}" : $"{value:0.#}", Active = Equipped });
        }
    }

    // Ligne sous le nom : emplacement et type, comme dans le client d'origine.
    private static string Describe(ItemInstance item, AbstractItem data)
    {
        if (item == null)
        {
            return string.Empty;
        }

        switch (item.Type2)
        {
            case ItemType2.TYPE2_WEAPON:
                return WeaponTypeName(data as Weapon, item.BodyPart);
            case ItemType2.TYPE2_SHIELD_ARMOR:
                return BodyPartName(item.BodyPart);
            case ItemType2.TYPE2_ACCESSORY:
                return "Accessoire";
            case ItemType2.TYPE2_QUEST:
                return "Objet de quête";
            default:
                return data is EtcItem ? "Objet" : string.Empty;
        }
    }

    // Ce que le joueur veut lire : arc, dague, hast... et non le mot "arme".
    private static string WeaponTypeName(Weapon weapon, ItemSlot slot)
    {
        WeaponType type = weapon?.Weapongrp != null ? weapon.Weapongrp.WeaponType : WeaponType.none;
        switch (type)
        {
            case WeaponType.sword: return slot == ItemSlot.SLOT_LR_HAND ? "Épée à deux mains" : "Épée";
            case WeaponType.bigword: return "Épée à deux mains";
            case WeaponType.blunt: return slot == ItemSlot.SLOT_LR_HAND ? "Masse à deux mains" : "Masse";
            case WeaponType.bigblunt: return "Masse à deux mains";
            case WeaponType.dagger: return "Dague";
            case WeaponType.bow: return "Arc";
            case WeaponType.pole: return "Arme d'hast";
            case WeaponType.dual: return "Épées jumelles";
            case WeaponType.fist: return "Arme de poing";
            case WeaponType.dualfist: return "Poings jumelés";
            case WeaponType.hand: return "Mains nues";
            default: return slot == ItemSlot.SLOT_LR_HAND ? "Arme à deux mains" : "Arme";
        }
    }

    private static string BodyPartName(ItemSlot slot)
    {
        switch (slot)
        {
            case ItemSlot.SLOT_CHEST: return "Haut du corps";
            case ItemSlot.SLOT_LEGS: return "Bas du corps";
            case ItemSlot.SLOT_FULL_ARMOR: return "Armure complète";
            case ItemSlot.SLOT_HEAD: return "Tête";
            case ItemSlot.SLOT_GLOVES: return "Gants";
            case ItemSlot.SLOT_FEET: return "Bottes";
            case ItemSlot.SLOT_L_HAND: return "Bouclier";
            default: return "Armure";
        }
    }
}
