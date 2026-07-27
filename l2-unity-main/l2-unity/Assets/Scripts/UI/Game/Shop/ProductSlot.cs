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

            L2InputAmountWindow.Instance.ShowWindow(systemMessage, Product.Count, (amount) =>
            {
                ((ShopSlotContainer)_currentSlotContainer).AdjacentContainer.AddToBasket(Product, amount);
            }, () => { });
        }
    }
}
