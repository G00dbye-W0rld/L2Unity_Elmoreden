using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Vitrine d'un marchand vue par un client : ses objets en haut, le panier en
// bas. Face a un magasin d'achat, le client lui vend ses propres objets.
public class PrivateStoreBuyWindow : L2PopupWindow, IPrivateStoreSlotHandler
{
    private PrivateStoreSlotContainer _storeContainer;
    private PrivateStoreSlotContainer _basketContainer;
    private Label _windowName;
    private Label _storeLabel;
    private Label _basketLabel;
    private Label _submitLabel;
    private Label _adenaLabel;
    private Label _totalLabel;
    private VisualElement _packageNotice;
    private int _storeObjectId;
    private bool _packaged;
    private PrivateStoreMode _mode;

    private static PrivateStoreBuyWindow _instance;
    public static PrivateStoreBuyWindow Instance { get { return _instance; } }

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
        _windowTemplate = LoadAsset("Data/UI/_Elements/Game/PrivateStoreWindow/PrivateStoreBuyWindow");
    }

    protected override void InitWindow(VisualElement root)
    {
        base.InitWindow(root);
        PrivateStoreDialogs.AttachStyle(_windowEle);

        VisualElement dragArea = GetElementByClass("drag-area");
        dragArea.AddManipulator(new DragManipulator(dragArea, _windowEle, this));

        RegisterCloseWindowEvent("btn-close-frame");
        RegisterClickWindowEvent(_windowEle, dragArea);

        RegisterButton("BuyButton", Submit);
        RegisterButton("CancelButton", () => HideWindow(false));

        _windowName = GetLabelById("windows-name-label");
        _storeLabel = GetElementById("StoreItems").Q<Label>("TabLabel");
        _basketLabel = GetElementById("Basket").Q<Label>("TabLabel");
        _submitLabel = GetElementById("BuyButton").Q<Label>("ButtonLabel");
        _adenaLabel = GetLabelById("AdenaCount");
        _totalLabel = GetLabelById("TotalPrice");
        _packageNotice = GetElementById("PackageNotice");

        _storeContainer = new PrivateStoreSlotContainer();
        _storeContainer.Initialize(GetElementById("StoreItems"), 6, 6, L2Slot.SlotType.StoreItem, this, true);

        _basketContainer = new PrivateStoreSlotContainer();
        _basketContainer.Initialize(GetElementById("Basket"), 6, 6, L2Slot.SlotType.StoreBasket, this, true);
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
        _storeContainer.SetProducts(null);
        _basketContainer.SetProducts(null);
    }

    public void Open(int storeObjectId, bool packaged, int adena, List<Product> items)
    {
        Open(PrivateStoreMode.Sell, storeObjectId, packaged, adena, items);
    }

    // Vente en lot : le serveur refuse tout achat partiel, le panier est donc
    // rempli d'office et fige.
    public void Open(PrivateStoreMode mode, int storeObjectId, bool packaged, int adena, List<Product> items)
    {
        bool sellStore = mode == PrivateStoreMode.Sell;

        _mode = mode;
        _storeObjectId = storeObjectId;
        _packaged = sellStore && packaged;

        _windowName.text = "Magasin de " + MerchantName(storeObjectId);
        _storeLabel.text = sellStore ? "Objets en vente" : "Objets recherch\u00e9s";
        _basketLabel.text = sellStore ? "Vos achats" : "Vos ventes";
        _submitLabel.text = sellStore ? "Acheter" : "Vendre";
        _adenaLabel.text = $"{adena:n0}";
        _packageNotice.style.display = _packaged ? DisplayStyle.Flex : DisplayStyle.None;

        // Un magasin d'achat demande un modele : deux exemplaires se regroupent.
        _storeContainer.KeyByItem = !sellStore;
        _basketContainer.KeyByItem = !sellStore;
        _storeContainer.SetProducts(_packaged ? null : items);
        _basketContainer.SetProducts(_packaged ? items : null);
        RefreshTotal();

        if (_isWindowHidden)
        {
            ShowWindow();
        }
    }

    private static string MerchantName(int objectId)
    {
        Entity target = TargetManager.Instance.HasTarget() ? TargetManager.Instance.Target : null;
        return target != null && target.Identity.Id == objectId ? target.Identity.Name : "?";
    }

    public void OnSlotActivated(PrivateStoreSlot slot)
    {
        Product product = slot.Product;

        // Count a 0 : le vendeur ne possede pas l'objet recherche.
        if (_packaged || product.Count <= 0)
        {
            return;
        }

        bool toBasket = slot.Type == L2Slot.SlotType.StoreItem;
        PrivateStoreSlotContainer from = toBasket ? _storeContainer : _basketContainer;
        PrivateStoreSlotContainer to = toBasket ? _basketContainer : _storeContainer;

        PrivateStoreDialogs.AskQuantity(product, (count) =>
        {
            from.Remove(product, count);
            to.Add(product, count, product.Price);
            RefreshTotal();
        });
    }

    private void RefreshTotal()
    {
        _totalLabel.text = $"{_basketContainer.TotalPrice:n0}";
    }

    private void Submit()
    {
        if (_basketContainer.Products.Count == 0)
        {
            return;
        }

        if (_mode == PrivateStoreMode.Sell)
        {
            GameClient.Instance.ClientPacketHandler.SendRequestPrivateStoreBuy(_storeObjectId, _basketContainer.Products);
        }
        else
        {
            GameClient.Instance.ClientPacketHandler.SendRequestPrivateStoreSell(_storeObjectId, _basketContainer.Products);
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
