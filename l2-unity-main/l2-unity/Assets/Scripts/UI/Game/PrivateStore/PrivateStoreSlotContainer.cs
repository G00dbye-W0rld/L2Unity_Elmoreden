using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

public class PrivateStoreSlotContainer : L2SlotContainer
{
    private List<Product> _products = new List<Product>();
    private L2Slot.SlotType _slotType;
    private bool _showPrices;

    public IPrivateStoreSlotHandler Handler { get; private set; }
    public List<Product> Products { get { return _products; } }
    public long TotalPrice { get { return _products.Sum(p => (long)p.Price * p.Count); } }

    // Magasin d'achat : on achete un modele (objet + enchantement), pas un
    // exemplaire precis ; l'ObjectId n'est alors pas significatif.
    public bool KeyByItem { get; set; }

    public void Initialize(VisualElement container, int rowLength, int minimumContainerSize, L2Slot.SlotType slotType,
        IPrivateStoreSlotHandler handler, bool showPrices)
    {
        base.Initialize(container, rowLength, minimumContainerSize);
        _slotType = slotType;
        _showPrices = showPrices;
        Handler = handler;
    }

    public void SetProducts(IEnumerable<Product> products)
    {
        _products = products == null ? new List<Product>() : products.Select(p => new Product(p)).ToList();
        Refresh();
    }

    public void Add(Product product, int count, int price)
    {
        Product existing = Find(product);

        if (existing != null)
        {
            existing.Count += count;
            existing.Price = price;
        }
        else
        {
            Product added = new Product(product);
            added.Count = count;
            added.Price = price;
            _products.Add(added);
        }

        Refresh();
    }

    public void Remove(Product product, int count)
    {
        Product existing = Find(product);
        if (existing == null)
        {
            return;
        }

        existing.Count -= count;
        if (existing.Count <= 0)
        {
            _products.Remove(existing);
        }

        Refresh();
    }

    // Quantite restante envoyee par le serveur (echange) ; 0 retire l'objet.
    public void SetCount(int objectId, int count)
    {
        Product existing = _products.Find(p => p.ObjectId == objectId);
        if (existing == null)
        {
            return;
        }

        if (count <= 0)
        {
            _products.Remove(existing);
        }
        else
        {
            existing.Count = count;
        }

        Refresh();
    }

    // Une pile d'inventaire n'a qu'un ObjectId : il identifie aussi bien un
    // objet unique qu'un empilable.
    private Product Find(Product product)
    {
        return _products.Find(p => KeyByItem
            ? p.ItemId == product.ItemId && p.Enchant == product.Enchant
            : p.ObjectId == product.ObjectId);
    }

    private void Refresh()
    {
        List<ItemInstance> items = new List<ItemInstance>(_products.Count);

        for (int i = 0; i < _products.Count; i++)
        {
            Product p = _products[i];
            items.Add(new ItemInstance(p.ObjectId, p.ItemId, ItemLocation.Void, i, p.Count, p.Type1, p.Type2, false, p.BodyPart, p.Enchant, 0));
        }

        CreateSlots(items.Count, _slotType);
        AssignItemsToSlots(items);

        for (int i = 0; i < _products.Count && i < _slots.Length; i++)
        {
            ((PrivateStoreSlot)_slots[i]).AssignListedProduct(_products[i], _showPrices);
        }
    }
}
