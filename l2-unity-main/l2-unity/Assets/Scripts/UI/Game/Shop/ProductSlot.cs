using UnityEngine;
using System;
using UnityEngine.UIElements;

public class ProductSlot : InventorySlot
{
    public Product Product { get; private set; }
    public ProductSlot(int position, VisualElement slotElement, L2SlotContainer tab, SlotType slotType) : base(position, slotElement, tab, slotType)
    {
    }

    protected override void HandleLeftClick()
    {
        if (_currentSlotContainer != null)
        {
            _currentSlotContainer.SelectSlot(_position);
        }
    }

    protected override void HandleRightClick()
    {
    }

    protected override void HandleMiddleClick()
    {
    }

    protected override void AddTooltip(ItemInstance item)
    {
        base.AddTooltip(item);
    }

    public void AssignProduct(Product product)
    {
        Product = product;
    }

    protected override void HandleLeftDoubleClick()
    {
        SwapBasket();
    }

    // Un marchand a souvent un stock illimite : le serveur envoie alors 0, que la fenetre de
    // quantite prenait pour un maximum de 0. On se limite alors a ce que le joueur peut payer.
    private int AvailableCount()
    {
        if (Product.Count > 0)
        {
            return Product.Count;
        }

        int adena = 0;
        if (PlayerInventory.Instance != null)
        {
            ItemInstance purse = PlayerInventory.Instance.Items.Find(i => i.ItemId == ItemTable.ADENA_ID);
            adena = purse != null ? purse.Count : 0;
        }

        return Product.Price > 0 ? Mathf.Max(1, adena / Product.Price) : 9999;
    }

    public virtual void SwapBasket()
    {
        // Un objet d'INVENTAIRE non empilable (ObjectId reel, exemplaire
        // unique) part directement au panier : il n'y a pas de quantite a
        // choisir. En revanche un produit de MARCHAND (ObjectId a 0, simple
        // modele) peut etre achete en plusieurs exemplaires, meme s'il s'agit
        // d'une arme ou d'une armure - l'ancienne condition ne regardait que
        // le type et privait donc l'achat de toute saisie de quantite.
        bool isUniqueInventoryItem = Product.ObjectId != 0 && Product.Type1 != ItemType1.TYPE1_ITEM_QUESTITEM_ADENA;

        if (isUniqueInventoryItem)
        {
            ((ShopSlotContainer)_currentSlotContainer).AdjacentContainer.AddToBasket(Product, 1);
        }
        else
        {
            SMParam[] smParams = new SMParam[1];
            smParams[0] = new SMParam(SMParam.SMParamType.TYPE_ITEM_NAME, Product.ItemId);
            SystemMessage systemMessage = new SystemMessage(smParams, SystemMessageTable.Instance.SystemMessages[72]);

            L2InputAmountWindow.Instance.ShowWindow(systemMessage, AvailableCount(), (amount) =>
            {
                ((ShopSlotContainer)_currentSlotContainer).AdjacentContainer.AddToBasket(Product, amount);
            }, () => { });
        }
    }
}
