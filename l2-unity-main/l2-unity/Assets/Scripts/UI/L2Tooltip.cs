using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

[System.Serializable]
public class L2ToolTip : L2PopupWindow
{

    private VisualElement _skillTooltip;
    private VisualElement _labelTooltip;
    private VisualElement _effectTooltip;
    private VisualElement _itemTooltip;
    private VisualElement _value;
    private VisualElement _tooltipTarget;
    private Coroutine _updateStyleCoroutine;
    private Coroutine _updateTimerCoroutine;

    private static L2ToolTip _instance;
    public static L2ToolTip Instance { get { return _instance; } }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    private void OnDestroy()
    {
        if (_updateStyleCoroutine != null) StopCoroutine(_updateStyleCoroutine);
        if (_updateTimerCoroutine != null) StopCoroutine(_updateTimerCoroutine);

        _instance = null;
    }

    protected override void LoadAssets()
    {
        _windowTemplate = LoadAsset("Data/UI/_Elements/Components/L2Tooltip/L2Tooltip");
    }

    protected override IEnumerator BuildWindow(VisualElement root)
    {
        InitWindow(root);

        yield return new WaitForEndOfFrame();

        _value = GetElementById("Content");
        _skillTooltip = GetElementById("SkillTooltip");
        _labelTooltip = GetElementById("LabelTooltip");
        _effectTooltip = GetElementById("EffectTooltip");
        _itemTooltip = GetElementById("ItemTooltip");
    }

    public void UpdateTooltip<T>(L2Slot.SlotType type, T value, VisualElement target)
    {
        _windowEle.style.left = -1000;
        _windowEle.style.opacity = 0;

        _tooltipTarget = target;

        _skillTooltip.style.display = DisplayStyle.None;
        _labelTooltip.style.display = DisplayStyle.None;
        _effectTooltip.style.display = DisplayStyle.None;
        _itemTooltip.style.display = DisplayStyle.None;

        switch (type)
        {
            case L2Slot.SlotType.Skill:
                DisplaySkillTooltip(value);
                break;
            case L2Slot.SlotType.Effect:
                DisplayEffectTooltip(value);
                break;
            default:
                DisplayDefaultTooltip(value);
                break;
        }

        ShowWindow();

        if (_updateStyleCoroutine != null)
        {
            StopCoroutine(_updateStyleCoroutine);
        }

        _updateStyleCoroutine = StartCoroutine(UpdateToolTipCoroutine(target));
    }

    private void DisplaySkillTooltip<T>(T value)
    {
        SkillWindowInfo val = value as SkillWindowInfo;
        if (val is null)
        {
            _skillTooltip.style.display = DisplayStyle.None;
            return;
        }

        AddSkillTooltip(val);
        _skillTooltip.style.display = DisplayStyle.Flex;
    }

    // Infobulle d'objet : nom, type, statistiques et description. Une statistique deja
    // active (objet equipe) est en bleu, une statistique seulement potentielle en gris clair.
    private static readonly Color ActiveStatColor = new Color(0.55f, 0.72f, 1f);
    private static readonly Color PotentialStatColor = new Color(0.72f, 0.72f, 0.72f);

    private static VisualElement StatLine(string label, string value, bool active)
    {
        VisualElement line = new VisualElement();
        line.AddToClassList("skill-tooltip-group");

        Label name = new Label(label);
        name.AddToClassList("tooltip-group-label");
        name.style.color = active ? ActiveStatColor : PotentialStatColor;

        Label amount = new Label(value);
        amount.AddToClassList("tooltip-group-value");
        amount.style.color = active ? ActiveStatColor : PotentialStatColor;

        line.Add(name);
        line.Add(amount);
        return line;
    }

    private void DisplayItemTooltip(ItemTooltipInfo info)
    {
        GetLabelById("ItemTooltipName").text = info.Count > 1 ? $"{info.Name} ({info.Count:n0})" : info.Name;

        // "Arc, grade d"
        var parts = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrEmpty(info.Kind)) parts.Add(info.Kind);
        if (!string.IsNullOrEmpty(info.Grade)) parts.Add(info.Grade.ToLower());

        Label kind = GetLabelById("ItemTooltipKind");
        kind.text = string.Join(", ", parts);
        kind.style.display = parts.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;

        VisualElement icon = GetElementById("ItemTooltipIcon");
        Texture2D texture = IconTable.Instance.LoadTextureByName(info.Icon);
        icon.style.backgroundImage = texture != null ? new StyleBackground(texture) : new StyleBackground();

        VisualElement stats = GetElementById("ItemTooltipStats");
        stats.Clear();
        foreach (ItemTooltipInfo.Stat stat in info.Stats)
        {
            stats.Add(StatLine(stat.Label + " :", stat.Value, stat.Active));
        }

        // Panoplie : les pieces portees en bleu, celles qui manquent en gris clair.
        if (!string.IsNullOrEmpty(info.SetName))
        {
            stats.Add(StatLine("Panoplie :", info.SetName, info.SetComplete));
            foreach (ItemTooltipInfo.Stat piece in info.SetPieces)
            {
                stats.Add(StatLine("   " + piece.Label, piece.Value, piece.Active));
            }

            if (!string.IsNullOrEmpty(info.SetBonus))
            {
                Label bonus = new Label(info.SetBonus);
                bonus.style.color = info.SetComplete ? ActiveStatColor : PotentialStatColor;
                bonus.style.whiteSpace = WhiteSpace.Normal;
                bonus.AddToClassList("skill-tooltip-group");
                stats.Add(bonus);
            }
        }

        stats.style.display = stats.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;

        Label description = GetLabelById("ItemTooltipDescription");
        description.text = info.Description;
        GetElementById("ItemTooltipDescriptionContainer").style.display =
            string.IsNullOrEmpty(info.Description) ? DisplayStyle.None : DisplayStyle.Flex;

        _itemTooltip.style.display = DisplayStyle.Flex;
    }

    private void DisplayDefaultTooltip<T>(T value)
    {
        if (value is ItemTooltipInfo itemInfo)
        {
            DisplayItemTooltip(itemInfo);
            return;
        }

        string stringVal;

        if (value is SkillWindowInfo info)
        {
            stringVal = info.Name;
        }
        else
        {
            stringVal = value as string;
        }

        if (stringVal != string.Empty)
        {
            GetLabelById("Content").text = stringVal;
            _labelTooltip.style.display = DisplayStyle.Flex;
        }
        else
        {
            _labelTooltip.style.display = DisplayStyle.None;
        }
    }

    IEnumerator UpdateToolTipCoroutine(VisualElement target)
    {
        while (true)
        {
            if (_isWindowHidden)
            {
                yield return new WaitForEndOfFrame();
                continue;
            }

            yield return new WaitForEndOfFrame();

            int margin = 0;
            float leftPos = Math.Min(target.worldBound.x, Math.Max(Screen.width - _windowEle.resolvedStyle.width, 0));
            float topPosUp = Math.Max(target.worldBound.y - _windowEle.resolvedStyle.height - margin, 0);
            float topPosDown = target.worldBound.y + target.resolvedStyle.height + margin;
            float topPos = (target.worldBound.y >= _windowEle.resolvedStyle.height + margin) ? topPosUp : topPosDown;

            _windowEle.style.left = leftPos;
            _windowEle.style.top = topPos;
            _windowEle.style.opacity = 1;
        }
    }

    IEnumerator UpdateTimerCoroutine(Buff effect)
    {
        VisualElement durationContainer = GetElementById("EffectTooltipDurationContainer");
        Label durationLabel = GetLabelById("EffectTooltipDurationValue");

        while (true)
        {
            if (_isWindowHidden || _effectTooltip.style.display == DisplayStyle.None || durationContainer.style.display == DisplayStyle.None)
            {
                yield return new WaitForEndOfFrame();
                continue;
            }

            float duration = effect.StartTime + effect.Duration - Time.unscaledTime;
            float hours = duration * 0.00027777778f;
            float remainderAfterHours = duration - ((int)hours * 3600);
            float minutes = Mathf.Floor(remainderAfterHours * 0.0166666667f);
            float seconds = duration - ((int)hours * 3600) - (minutes * 60);
            StringBuilder durationStr = new();
            if ((int)hours > 0)
            {
                durationStr.Append((int)hours);
                durationStr.Append("h ");
            }
            if ((int)minutes > 0)
            {
                durationStr.Append((int)minutes);
                durationStr.Append("m ");
            }
            durationStr.Append(Mathf.Ceil(seconds + 0.8f));
            durationStr.Append("s");
            durationLabel.text = durationStr.ToString();

            yield return new WaitForSeconds(1);
        }
    }

    public void HideWindow(VisualElement exitElement)
    {
        if (exitElement == _tooltipTarget)
        {
            base.HideWindow(false);

            if (_updateStyleCoroutine != null)
            {
                StopCoroutine(_updateStyleCoroutine);
            }
        }
    }

    public void AddSkillTooltip(SkillWindowInfo skill)
    {
        StyleBackground background = new StyleBackground(IconTable.Instance.LoadTextureByName(skill.Icon));
        GetElementById("SkillTooltipIcon").style.backgroundImage = background;
        GetLabelById("SkillTooltipName").text = skill.Name;
        GetLabelById("SkillTooltipLevelValue").text = skill.Level.ToString();
        GetLabelById("SkillTooltipType").text = skill.GetSkillType();
        Label desc = GetLabelById("SkillTooltipDescription");
        desc.text = skill.ComposeDescription();

        if (skill.MpCost > 0)
        {
            GetLabelById("SkillTooltipMpCostValue").text = skill.MpCost.ToString();
            GetElementById("SkillTooltipMpCost").style.display = DisplayStyle.Flex;
        }
        else
        {
            GetElementById("SkillTooltipMpCost").style.display = DisplayStyle.None;
        }

        if (skill.Range > 0)
        {
            GetLabelById("SkillTooltipRangeValue").text = skill.Range.ToString();
            GetElementById("SkillTooltipRange").style.display = DisplayStyle.Flex;
        }
        else
        {
            GetElementById("SkillTooltipRange").style.display = DisplayStyle.None;
        }

        if (skill.HitTime > 0)
        {
            GetLabelById("SkillTooltipCastingTimeValue").text = skill.HitTime.ToString(CultureInfo.InvariantCulture);
            GetElementById("SkillTooltipCastingTime").style.display = DisplayStyle.Flex;
        }
        else
        {
            GetElementById("SkillTooltipCastingTime").style.display = DisplayStyle.None;
        }

        if (skill.IsPassiveSkill() || skill.Type == SkillType.CraftAndItems)
        {
            GetElementById("SkillTooltipReuseTime").style.display = DisplayStyle.None;
        }
        else
        {
            GetLabelById("SkillTooltipReuseTimeValue").text = skill.ReuseDelay.ToString(CultureInfo.InvariantCulture);
            GetElementById("SkillTooltipReuseTime").style.display = DisplayStyle.Flex;
        }
    }

    private void DisplayEffectTooltip<T>(T value)
    {
        Buff val = value as Buff;
        if (val is null)
        {
            _effectTooltip.style.display = DisplayStyle.None;
            return;
        }

        AddEffectTooltip(val);

        _effectTooltip.style.display = DisplayStyle.Flex;
    }

    public void AddEffectTooltip(Buff effect)
    {
        if (_updateTimerCoroutine != null)
        {
            StopCoroutine(_updateTimerCoroutine);
        }

        GetLabelById("EffectTooltipName").text = effect.Name;
        GetLabelById("EffectTooltipLevelValue").text = effect.Level.ToString();
        VisualElement durationContainer = GetElementById("EffectTooltipDurationContainer");

        if (effect.Duration <= -1)
        {
            durationContainer.style.display = DisplayStyle.None;
        }
        else
        {
            _updateTimerCoroutine = StartCoroutine(UpdateTimerCoroutine(effect));
            durationContainer.style.display = DisplayStyle.Flex;
        }


        GetLabelById("EffectTooltipDescription").text = effect.Description;
    }
}
