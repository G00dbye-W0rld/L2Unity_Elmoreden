using UnityEngine;
using UnityEngine.UIElements;

public class InventorySlot : L2DraggableSlot
{
    protected L2SlotContainer _currentSlotContainer;
    private int _count;
    private long _remainingTime;
    private SlotClickSoundManipulator _slotClickSoundManipulator;
    private int _objectId;
    private ItemName _assignedItem;
    private ItemType1 _type1;
    private ItemType2 _type2;
    private int _enchantLevel;
    private Label _enchantLevelLabel;
    private Label _countLabel;
    private VisualElement _gradeLabel;
    private static readonly System.Collections.Generic.Dictionary<string, Texture2D> _gradeSymbols =
        new System.Collections.Generic.Dictionary<string, Texture2D>();
    public int Count { get { return _count; } }
    public long RemainingTime { get { return _remainingTime; } }
    public ItemType1 Type1 { get { return _type1; } }
    public ItemType2 Type2 { get { return _type2; } }
    public int ObjectId { get { return _objectId; } }

    public ItemName ItemName { get { return _assignedItem; } }
    public L2SlotContainer SlotContainer { get { return _currentSlotContainer; } }

    public InventorySlot(int position, VisualElement slotElement, L2SlotContainer slotContainer, SlotType slotType)
    : base(position, slotElement, slotType, false, true)
    {
        _currentSlotContainer = slotContainer;
        _empty = true;

        if (_slotClickSoundManipulator == null)
        {
            _slotClickSoundManipulator = new SlotClickSoundManipulator(_slotElement);
            _slotElement.AddManipulator(_slotClickSoundManipulator);
        }
    }

    public InventorySlot(int position, VisualElement slotElement, SlotType slotType)
    : base(position, slotElement, slotType, true, false)
    {
        _empty = true;
    }

    public virtual void AssignItem(ItemInstance item)
    {
        _slotElement.RemoveFromClassList("empty");

        if (item.ItemData != null)
        {
            _id = item.ItemData.Id;
            _name = item.ItemData.ItemName.Name;
            _description = item.ItemData.ItemName.Description;
            _icon = item.ItemData.Icon;
            _objectId = item.ObjectId;
            _empty = false;
            _type1 = item.Type1;
            _assignedItem = item.ItemData.ItemName;
        }
        else
        {
            Debug.LogWarning($"Item data is null for item {item.ItemId}.");
            _id = 0;
            _name = "Unkown";
            _description = "Unkown item.";
            _icon = "";
            _objectId = -1;
            _type1 = Type1;
            _assignedItem = new ItemName();
        }

        _count = item.Count;
        _remainingTime = item.RemainingTime;
        _enchantLevel = item.EnchantLevel;

        if (_slotElement != null)
        {
            StyleBackground background = new StyleBackground(IconTable.Instance.GetIcon(_id));
            _slotBg.style.backgroundImage = background;

            AddTooltip(item);
            UpdateEnchantLevelLabel();
            UpdateCountLabel();
            UpdateGradeLabel();

            _slotDragManipulator.enabled = true;
        }
    }

    // "+N" en surimpression sur l'icone (coin bas-gauche, convention L2
    // classique), construit une seule fois par slot puis reutilise -
    // L2SlotContainer.CreateSlots reconstruit entierement les VisualElement
    // de slot a chaque rafraichissement de la liste (cf. InventoryTab), donc
    // pas de risque de valeur d'enchant qui "fuit" d'un objet au suivant sur
    // un meme slot recycle (contrairement au bug de nameplates corrige plus
    // tot - ici chaque AssignItem() reecrit explicitement le texte).
    private void UpdateEnchantLevelLabel()
    {
        if (_enchantLevelLabel == null)
        {
            _enchantLevelLabel = new Label();
            _enchantLevelLabel.pickingMode = PickingMode.Ignore;
            // Position.Absolute qualifie completement (pas juste "Position.")
            // car L2Slot.Position (int, la position du slot dans le
            // conteneur) masque l'enum UnityEngine.UIElements.Position ici.
            _enchantLevelLabel.style.position = UnityEngine.UIElements.Position.Absolute;
            _enchantLevelLabel.style.left = 1;
            _enchantLevelLabel.style.bottom = 0;
            _enchantLevelLabel.style.fontSize = 10;
            _enchantLevelLabel.style.color = new Color(1f, 0.85f, 0.2f);
            _enchantLevelLabel.style.unityTextAlign = TextAnchor.LowerLeft;
            _enchantLevelLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _slotElement.Add(_enchantLevelLabel);
        }

        _enchantLevelLabel.style.display = _enchantLevel > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        _enchantLevelLabel.text = $"+{_enchantLevel}";
    }

    protected virtual void AddTooltip(ItemInstance item)
    {
        if (_tooltipManipulator != null)
        {
            _tooltipManipulator.SetValue(ItemTooltipInfo.Build(item, item.ItemData, _name, _enchantLevel));
        }
    }

    // Quantite en surimpression (coin bas-droit) : rien pour un exemplaire unique,
    // "99+" au-dela de 99, comme dans le client d'origine.
    private void UpdateCountLabel()
    {
        if (_countLabel == null)
        {
            _countLabel = new Label();
            _countLabel.pickingMode = PickingMode.Ignore;
            _countLabel.style.position = UnityEngine.UIElements.Position.Absolute;
            _countLabel.style.right = 1;
            _countLabel.style.bottom = 0;
            _countLabel.style.fontSize = 10;
            _countLabel.style.color = new Color(0.97f, 0.97f, 0.97f);
            _countLabel.style.unityTextAlign = TextAnchor.LowerRight;
            _countLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _slotElement.Add(_countLabel);
        }

        _countLabel.style.display = _count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
        _countLabel.text = _count > 99 ? "99+" : _count.ToString();
    }

    // Vignette de grade (coin haut-gauche), celle du client : symbol.grade_d et ses voisines.
    // Rien sur un objet sans grade.
    private void UpdateGradeLabel()
    {
        if (_gradeLabel == null)
        {
            _gradeLabel = new VisualElement();
            _gradeLabel.pickingMode = PickingMode.Ignore;
            _gradeLabel.style.position = UnityEngine.UIElements.Position.Absolute;
            _gradeLabel.style.left = 1;
            _gradeLabel.style.top = 1;
            _gradeLabel.style.width = 12;
            _gradeLabel.style.height = 12;
            _slotElement.Add(_gradeLabel);
        }

        ItemGrade grade = ItemTable.Instance.GetItem(_id)?.Itemgrp?.Grade ?? ItemGrade.none;
        Texture2D symbol = GradeSymbol(grade);
        _gradeLabel.style.display = symbol == null ? DisplayStyle.None : DisplayStyle.Flex;
        _gradeLabel.style.backgroundImage = symbol != null ? new StyleBackground(symbol) : new StyleBackground();
    }

    private static Texture2D GradeSymbol(ItemGrade grade)
    {
        if (grade == ItemGrade.none)
        {
            return null;
        }

        string name = grade.ToString().ToLower();
        if (!_gradeSymbols.TryGetValue(name, out Texture2D symbol))
        {
            symbol = Resources.Load<Texture2D>($"Data/SysTextures/Symbol/grade_{name}");
            _gradeSymbols[name] = symbol;
        }

        return symbol;
    }

    public override void ClearManipulators()
    {
        base.ClearManipulators();

        if (_slotClickSoundManipulator != null)
        {
            _slotElement.RemoveManipulator(_slotClickSoundManipulator);
            _slotClickSoundManipulator = null;
        }
    }

    protected override void HandleLeftClick()
    {
        if (TryHandleEnchantClick()) return;

        if (_currentSlotContainer != null)
        {
            _currentSlotContainer.SelectSlot(_position);
        }
    }

    // Partage avec GearSlot (qui redefinit HandleLeftClick) : tant qu'un
    // parchemin d'enchantement est arme (EnchantManager.IsSelecting), le
    // clic gauche suivant sur un objet non-vide devient "choisir cet objet
    // comme cible" au lieu du comportement normal de selection/equipement.
    protected bool TryHandleEnchantClick()
    {
        if (_empty || !EnchantManager.Instance.IsSelecting) return false;

        EnchantManager.Instance.SelectTarget(_objectId);
        return true;
    }

    protected override void HandleRightClick()
    {
        UseItem();
    }

    protected override void HandleMiddleClick()
    {
        if (!_empty)
        {
            PlayerInventory.Instance.DestroyItem(_objectId, 1);
        }
    }

    public virtual void UseItem()
    {
        if (!_empty)
        {
            PlayerInventory.Instance.UseItem(_objectId);
        }
    }
}
