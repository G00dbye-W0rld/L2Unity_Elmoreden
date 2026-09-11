using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Preparation du magasin prive, vente ou achat : inventaire en haut, liste du
// magasin en bas, disposition reprise de la PrivateShopWnd d'Interlude.
public class PrivateStoreWindow : L2PopupWindow, IPrivateStoreSlotHandler
{
    private const int SalePriceMessageId = 322;
    private const int PurchasePriceMessageId = 585;
    private const int PriceWarningMessageId = 569;
    private const float LowPriceRatio = 0.5f;
    private const float HighPriceRatio = 2f;
    private const int MaxTitleLength = 29;

    private PrivateStoreSlotContainer _inventoryContainer;
    private PrivateStoreSlotContainer _storeContainer;
    private Label _windowName;
    private Label _storeListLabel;
    private Label _adenaLabel;
    private Label _totalLabel;
    private VisualElement _packageRow;
    private Toggle _packageToggle;
    private TextField _titleField;
    private bool _titleFocused;
    private PrivateStoreMode _mode;

    // Le serveur est en mode gestion tant que la fenetre est ouverte : la
    // fermer sans lancer le magasin doit le lui signaler.
    private bool _managing;

    private static PrivateStoreWindow _instance;
    public static PrivateStoreWindow Instance { get { return _instance; } }

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
        _windowTemplate = LoadAsset("Data/UI/_Elements/Game/PrivateStoreWindow/PrivateStoreWindow");
    }

    protected override void InitWindow(VisualElement root)
    {
        base.InitWindow(root);
        PrivateStoreDialogs.AttachStyle(_windowEle);

        VisualElement dragArea = GetElementByClass("drag-area");
        dragArea.AddManipulator(new DragManipulator(dragArea, _windowEle, this));

        RegisterCloseWindowEvent("btn-close-frame");
        RegisterClickWindowEvent(_windowEle, dragArea);

        RegisterButton("OkButton", Launch);
        RegisterButton("CancelButton", () => HideWindow(false));

        _windowName = GetLabelById("windows-name-label");
        _storeListLabel = GetElementById("StoreList").Q<Label>("TabLabel");
        _adenaLabel = GetLabelById("AdenaCount");
        _totalLabel = GetLabelById("TotalPrice");
        _packageRow = GetElementById("PackageRow");
        _packageToggle = _windowEle.Q<Toggle>("PackageToggle");

        _titleField = GetElementById("TitleInput").Q<TextField>("L2Input");
        _titleField.maxLength = MaxTitleLength;
        _titleField.RegisterCallback<FocusEvent>(evt =>
        {
            _titleFocused = true;
            L2GameUI.Instance.IsTyping = true;
        });
        _titleField.RegisterCallback<BlurEvent>(evt =>
        {
            _titleFocused = false;
            L2GameUI.Instance.IsTyping = false;
        });

        _inventoryContainer = new PrivateStoreSlotContainer();
        _inventoryContainer.Initialize(GetElementById("InventoryList"), 6, 24, L2Slot.SlotType.StoreItem, this, false);

        _storeContainer = new PrivateStoreSlotContainer();
        _storeContainer.Initialize(GetElementById("StoreList"), 6, 6, L2Slot.SlotType.StoreBasket, this, true);
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
        _inventoryContainer.SetProducts(null);
        _storeContainer.SetProducts(null);
    }

    public void Open(int adena, bool packageSale, List<Product> inventory, List<Product> store)
    {
        Open(PrivateStoreMode.Sell, adena, packageSale, inventory, store);
    }

    public void Open(PrivateStoreMode mode, int adena, bool packageSale, List<Product> inventory, List<Product> store)
    {
        bool sell = mode == PrivateStoreMode.Sell;

        _mode = mode;
        _managing = true;
        _windowName.text = sell ? "Magasin priv\u00e9 : vente" : "Magasin priv\u00e9 : achat";
        _storeListLabel.text = sell ? "Objets en vente" : "Objets \u00e0 acheter";
        _packageRow.style.display = sell ? DisplayStyle.Flex : DisplayStyle.None;
        _packageToggle.value = sell && packageSale;
        _adenaLabel.text = $"{adena:n0}";

        _storeContainer.KeyByItem = !sell;
        _inventoryContainer.SetProducts(inventory);
        _storeContainer.SetProducts(store);
        RefreshTotal();

        if (_isWindowHidden)
        {
            ShowWindow();
        }
    }

    public void OnSlotActivated(PrivateStoreSlot slot)
    {
        Product product = slot.Product;
        bool fromInventory = slot.Type == L2Slot.SlotType.StoreItem;

        if (fromInventory && _mode == PrivateStoreMode.Sell)
        {
            PrivateStoreDialogs.AskQuantity(product, (count) => AskPrice(product, count));
        }
        else if (fromInventory)
        {
            PrivateStoreDialogs.AskAnyQuantity(product, (count) => AskPrice(product, count));
        }
        else
        {
            PrivateStoreDialogs.AskQuantity(product, (count) =>
            {
                _storeContainer.Remove(product, count);

                // En achat, l'objet n'a jamais quitte l'inventaire.
                if (_mode == PrivateStoreMode.Sell)
                {
                    _inventoryContainer.Add(product, count, product.ReferencePrice);
                }

                RefreshTotal();
            });
        }
    }

    // Le prix de reference est propose ; un prix trop ecarte demande confirmation.
    private void AskPrice(Product product, int count)
    {
        int messageId = _mode == PrivateStoreMode.Sell ? SalePriceMessageId : PurchasePriceMessageId;
        SystemMessage message = new SystemMessage(new SMParam[0], SystemMessageTable.Instance.GetSystemMessage(messageId));

        L2InputAmountWindow.Instance.ShowWindow(message, 0, product.ReferencePrice, (price) =>
        {
            if (IsPriceUnusual(price, product.ReferencePrice))
            {
                L2ConfirmWindow.Instance.ShowWindow(PriceWarningMessageId, () => AddToStore(product, count, price), () => { });
            }
            else
            {
                AddToStore(product, count, price);
            }
        }, () => { });
    }

    private static bool IsPriceUnusual(int price, int referencePrice)
    {
        return referencePrice > 0 && (price < referencePrice * LowPriceRatio || price > referencePrice * HighPriceRatio);
    }

    private void AddToStore(Product product, int count, int price)
    {
        if (_mode == PrivateStoreMode.Sell)
        {
            _inventoryContainer.Remove(product, count);
        }

        _storeContainer.Add(product, count, price);
        RefreshTotal();
    }

    private void RefreshTotal()
    {
        _totalLabel.text = $"{_storeContainer.TotalPrice:n0}";
    }

    // Le nom part avant la liste : c'est la liste qui le diffuse aux autres joueurs.
    private void Launch()
    {
        if (_storeContainer.Products.Count == 0)
        {
            return;
        }

        string title = _titleField.value.Trim();
        if (title.Length > MaxTitleLength)
        {
            title = title.Substring(0, MaxTitleLength);
        }

        GameClientPacketHandler client = GameClient.Instance.ClientPacketHandler;

        if (_mode == PrivateStoreMode.Sell)
        {
            client.SendSetPrivateStoreMsgSell(title);
            client.SendSetPrivateStoreListSell(_packageToggle.value, _storeContainer.Products);
        }
        else
        {
            client.SendSetPrivateStoreMsgBuy(title);
            client.SendSetPrivateStoreListBuy(_storeContainer.Products);
        }

        _managing = false;
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
        if (_managing)
        {
            _managing = false;

            if (_mode == PrivateStoreMode.Sell)
            {
                GameClient.Instance.ClientPacketHandler.SendRequestPrivateStoreQuitSell();
            }
            else
            {
                GameClient.Instance.ClientPacketHandler.SendRequestPrivateStoreQuitBuy();
            }
        }

        if (_titleFocused)
        {
            _titleFocused = false;
            L2GameUI.Instance.IsTyping = false;
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
