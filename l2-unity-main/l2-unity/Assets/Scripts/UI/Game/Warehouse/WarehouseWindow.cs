using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Depot ou retrait chez un gardien d'entrepot : objets disponibles en haut,
// selection en bas. Construite comme PrivateStoreBuyWindow.
public class WarehouseWindow : L2PopupWindow, IPrivateStoreSlotHandler
{
    // Tarifs du serveur par ligne deposee : 30 adena (entrepot), FreightPrice (colis).
    private const int DepositFeePerItem = 30;
    private const int FreightFeePerItem = 1000;

    private PrivateStoreSlotContainer _sourceContainer;
    private PrivateStoreSlotContainer _basketContainer;
    private Label _windowName;
    private Label _sourceLabel;
    private Label _basketLabel;
    private Label _submitLabel;
    private Label _adenaLabel;
    private Label _feeLabel;
    private VisualElement _feeRow;
    private VisualElement _recipientRow;
    private DropdownField _recipient;
    private List<KeyValuePair<int, string>> _recipients = new List<KeyValuePair<int, string>>();
    private bool _deposit;
    private bool _package;

    private static WarehouseWindow _instance;
    public static WarehouseWindow Instance { get { return _instance; } }

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
        _windowTemplate = LoadAsset("Data/UI/_Elements/Game/WarehouseWindow/WarehouseWindow");
    }

    protected override void InitWindow(VisualElement root)
    {
        base.InitWindow(root);
        PrivateStoreDialogs.AttachStyle(_windowEle);

        VisualElement dragArea = GetElementByClass("drag-area");
        dragArea.AddManipulator(new DragManipulator(dragArea, _windowEle, this));

        RegisterCloseWindowEvent("btn-close-frame");
        RegisterClickWindowEvent(_windowEle, dragArea);

        RegisterButton("SubmitButton", Submit);
        RegisterButton("CancelButton", () => HideWindow(false));

        _windowName = GetLabelById("windows-name-label");
        _sourceLabel = GetElementById("SourceItems").Q<Label>("TabLabel");
        _basketLabel = GetElementById("Basket").Q<Label>("TabLabel");
        _submitLabel = GetElementById("SubmitButton").Q<Label>("ButtonLabel");
        _adenaLabel = GetLabelById("AdenaCount");
        _feeLabel = GetLabelById("FeeCount");
        _feeRow = GetElementById("FeeRow");
        _recipientRow = GetElementById("RecipientRow");
        _recipient = _windowEle.Q<DropdownField>("Recipient");

        _sourceContainer = new PrivateStoreSlotContainer();
        _sourceContainer.Initialize(GetElementById("SourceItems"), 6, 24, L2Slot.SlotType.StoreItem, this, false);

        _basketContainer = new PrivateStoreSlotContainer();
        _basketContainer.Initialize(GetElementById("Basket"), 6, 6, L2Slot.SlotType.StoreBasket, this, false);
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
        _sourceContainer.SetProducts(null);
        _basketContainer.SetProducts(null);
    }

    public void Open(WarehouseType type, bool deposit, int adena, List<Product> items)
    {
        _deposit = deposit;
        SetPackageMode(false);

        _windowName.text = WarehouseName(type) + (deposit ? " : d\u00e9p\u00f4t" : " : retrait");
        _sourceLabel.text = deposit ? "Inventaire" : "Objets en entrep\u00f4t";
        _basketLabel.text = deposit ? "Objets \u00e0 d\u00e9poser" : "Objets \u00e0 retirer";
        _submitLabel.text = deposit ? "D\u00e9poser" : "Retirer";
        _adenaLabel.text = $"{adena:n0}";
        _feeRow.style.display = deposit ? DisplayStyle.Flex : DisplayStyle.None;

        _sourceContainer.SetProducts(items);
        _basketContainer.SetProducts(null);
        RefreshFee();

        if (_isWindowHidden)
        {
            ShowWindow();
        }
    }

    // Envoi de colis : le serveur donne d'abord les destinataires, puis la
    // liste des objets envoyables pour le premier d'entre eux.
    public void PrepareFreight(List<KeyValuePair<int, string>> characters)
    {
        _recipients = characters;
        if (_recipients.Count > 0)
        {
            GameClient.Instance.ClientPacketHandler.SendRequestPackageSendableItemList(_recipients[0].Key);
        }
    }

    public void OpenFreightDeposit(int targetId, int adena, List<Product> items)
    {
        _deposit = true;
        SetPackageMode(true);

        List<string> names = _recipients.ConvertAll(c => c.Value);
        _recipient.choices = names;
        _recipient.index = Mathf.Max(0, _recipients.FindIndex(c => c.Key == targetId));

        _windowName.text = "Fret : envoi de colis";
        _sourceLabel.text = "Inventaire";
        _basketLabel.text = "Objets \u00e0 envoyer";
        _submitLabel.text = "Envoyer";
        _adenaLabel.text = $"{adena:n0}";
        _feeRow.style.display = DisplayStyle.Flex;

        _sourceContainer.SetProducts(items);
        _basketContainer.SetProducts(null);
        RefreshFee();

        if (_isWindowHidden)
        {
            ShowWindow();
        }
    }

    private void SetPackageMode(bool package)
    {
        _package = package;
        _recipientRow.style.display = package ? DisplayStyle.Flex : DisplayStyle.None;
        _windowEle.EnableInClassList("warehouse-window-package", package);
    }

    private static string WarehouseName(WarehouseType type)
    {
        switch (type)
        {
            case WarehouseType.Clan:
                return "Entrep\u00f4t du clan";
            case WarehouseType.Castle:
                return "Entrep\u00f4t du ch\u00e2teau";
            case WarehouseType.Freight:
                return "Fret";
            default:
                return "Entrep\u00f4t personnel";
        }
    }

    public void OnSlotActivated(PrivateStoreSlot slot)
    {
        Product product = slot.Product;
        bool toBasket = slot.Type == L2Slot.SlotType.StoreItem;
        PrivateStoreSlotContainer from = toBasket ? _sourceContainer : _basketContainer;
        PrivateStoreSlotContainer to = toBasket ? _basketContainer : _sourceContainer;

        PrivateStoreDialogs.AskQuantity(product, (count) =>
        {
            from.Remove(product, count);
            to.Add(product, count, 0);
            RefreshFee();
        });
    }

    private void RefreshFee()
    {
        int fee = _package ? FreightFeePerItem : DepositFeePerItem;
        _feeLabel.text = $"{_basketContainer.Products.Count * fee:n0}";
    }

    private void Submit()
    {
        if (_basketContainer.Products.Count == 0)
        {
            return;
        }

        if (_package)
        {
            int index = _recipient.index;
            if (index < 0 || index >= _recipients.Count)
            {
                return;
            }

            GameClient.Instance.ClientPacketHandler.SendRequestPackageSend(_recipients[index].Key, _basketContainer.Products);
        }
        else
        {
            GameClient.Instance.ClientPacketHandler.SendWarehouseList(_deposit, _basketContainer.Products);
        }

        HideWindow(false);
    }

    public override void ShowWindow()
    {
        base.ShowWindow();
        AudioManager.Instance.PlayUISound("window_open");
        L2GameUI.Instance.WindowOpened(this);
    }

    public override void HideWindow(bool silent)
    {
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
