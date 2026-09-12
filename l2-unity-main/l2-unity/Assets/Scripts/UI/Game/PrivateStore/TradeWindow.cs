using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Echange direct : inventaire, votre offre, offre de l'autre joueur. Le
// serveur fait foi : un objet n'apparait dans l'offre qu'a sa confirmation,
// et une fois propose il ne peut plus etre retire.
public class TradeWindow : L2PopupWindow, IPrivateStoreSlotHandler
{
    private PrivateStoreSlotContainer _inventory;
    private PrivateStoreSlotContainer _ownOffer;
    private PrivateStoreSlotContainer _otherOffer;
    private Label _windowName;
    private Label _otherOfferLabel;
    private Label _ownStatus;
    private Label _otherStatus;
    private bool _active;
    private bool _ownConfirmed;

    private static TradeWindow _instance;
    public static TradeWindow Instance { get { return _instance; } }

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
        if (_instance == this)
        {
            _instance = null;
        }
    }

    protected override void LoadAssets()
    {
        _windowTemplate = LoadAsset("Data/UI/_Elements/Game/PrivateStoreWindow/TradeWindow");
    }

    protected override void InitWindow(VisualElement root)
    {
        base.InitWindow(root);
        PrivateStoreDialogs.AttachStyle(_windowEle);

        VisualElement dragArea = GetElementByClass("drag-area");
        dragArea.AddManipulator(new DragManipulator(dragArea, _windowEle, this));

        RegisterCloseWindowEvent("btn-close-frame");
        RegisterClickWindowEvent(_windowEle, dragArea);

        RegisterButton("OkButton", Confirm);
        RegisterButton("CancelButton", () => HideWindow(false));

        _windowName = GetLabelById("windows-name-label");
        _otherOfferLabel = GetElementById("OtherOffer").Q<Label>("TabLabel");
        _ownStatus = GetLabelById("OwnStatus");
        _otherStatus = GetLabelById("OtherStatus");

        _inventory = new PrivateStoreSlotContainer();
        _inventory.Initialize(GetElementById("InventoryList"), 6, 24, L2Slot.SlotType.StoreItem, this, false);

        _ownOffer = new PrivateStoreSlotContainer();
        _ownOffer.Initialize(GetElementById("OwnOffer"), 6, 12, L2Slot.SlotType.StoreBasket, this, false);

        _otherOffer = new PrivateStoreSlotContainer();
        _otherOffer.Initialize(GetElementById("OtherOffer"), 6, 12, L2Slot.SlotType.StoreBasket, this, false);
    }

    private void RegisterButton(string id, System.Action action)
    {
        Button button = GetElementById(id).Q<Button>("L2Button");
        button.AddManipulator(new ButtonClickSoundManipulator(button));
        button.RegisterCallback<MouseUpEvent>(evt => action(), TrickleDown.TrickleDown);
    }

    protected override IEnumerator BuildWindow(VisualElement root)
    {
        InitWindow(root);

        yield return new WaitForEndOfFrame();

        CenterWindow();
        _inventory.SetProducts(null);
        _ownOffer.SetProducts(null);
        _otherOffer.SetProducts(null);
    }

    public void Open(int partnerId, List<Product> inventory)
    {
        string name = WorldSpawner.Instance.TryGetEntity(partnerId, out Entity partner) ? partner.Identity.Name : "?";

        _windowName.text = "\u00c9change avec " + name;
        _otherOfferLabel.text = "Offre de " + name;
        _active = true;
        _ownConfirmed = false;
        SetStatus(_ownStatus, false);
        SetStatus(_otherStatus, false);

        _inventory.SetProducts(inventory);
        _ownOffer.SetProducts(null);
        _otherOffer.SetProducts(null);

        if (_isWindowHidden)
        {
            ShowWindow();
        }
    }

    public void AddOwn(Product product)
    {
        _ownOffer.Add(product, product.Count, 0);
    }

    public void AddOther(Product product)
    {
        _otherOffer.Add(product, product.Count, 0);
    }

    public void UpdateInventory(List<Product> items)
    {
        foreach (Product item in items)
        {
            _inventory.SetCount(item.ObjectId, item.Count);
        }
    }

    public void OnOwnConfirmed()
    {
        _ownConfirmed = true;
        SetStatus(_ownStatus, true);
    }

    public void OnOtherConfirmed()
    {
        SetStatus(_otherStatus, true);
    }

    // Fin de l'echange annoncee par le serveur : rien a lui renvoyer.
    public void Close()
    {
        _active = false;
        HideWindow(false);
    }

    public void OnSlotActivated(PrivateStoreSlot slot)
    {
        if (!_active || _ownConfirmed || slot.Type != L2Slot.SlotType.StoreItem)
        {
            return;
        }

        Product product = slot.Product;
        PrivateStoreDialogs.AskQuantity(product, (count) =>
        {
            GameClient.Instance.ClientPacketHandler.SendAddTradeItem(product.ObjectId, count);
        });
    }

    private void Confirm()
    {
        if (_active && !_ownConfirmed)
        {
            GameClient.Instance.ClientPacketHandler.SendTradeDone(true);
        }
    }

    private static void SetStatus(Label label, bool confirmed)
    {
        label.text = confirmed ? "Valid\u00e9" : "En attente";
        label.EnableInClassList("trade-status-confirmed", confirmed);
    }

    public override void ShowWindow()
    {
        base.ShowWindow();
        AudioManager.Instance.PlayUISound("window_open");
        L2GameUI.Instance.WindowOpened(this);
    }

    public override void HideWindow(bool silent)
    {
        if (_active)
        {
            _active = false;
            GameClient.Instance.ClientPacketHandler.SendTradeDone(false);
        }

        if (_isWindowHidden)
        {
            return;
        }

        base.HideWindow(silent);

        if (!silent)
        {
            AudioManager.Instance.PlayUISound("window_close");
        }

        L2GameUI.Instance.WindowClosed(this);
    }
}
