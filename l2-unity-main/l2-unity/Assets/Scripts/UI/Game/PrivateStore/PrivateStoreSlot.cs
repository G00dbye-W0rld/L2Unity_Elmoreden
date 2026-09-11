using UnityEngine.UIElements;

public class PrivateStoreSlot : ProductSlot
{
    public PrivateStoreSlot(int position, VisualElement slotElement, L2SlotContainer container, SlotType slotType)
        : base(position, slotElement, container, slotType)
    {
    }

    public void AssignListedProduct(Product product, bool showPrice)
    {
        AssignProduct(product);

        if (showPrice && _tooltipManipulator != null)
        {
            string count = product.Count > 1 ? $" ({product.Count:n0})" : "";
            _tooltipManipulator.SetValue($"{_name}{count}\nPrix : {product.Price:n0} adena");
        }
    }

    public override void SwapBasket()
    {
        if (Product == null)
        {
            return;
        }

        (_currentSlotContainer as PrivateStoreSlotContainer)?.Handler?.OnSlotActivated(this);
    }
}
