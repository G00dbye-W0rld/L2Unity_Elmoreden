using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

[System.Serializable]
public class ShopSlotContainer : L2SlotContainer
{
    [SerializeField] private List<Product> _products;
    [SerializeField] private L2Slot.SlotType _slotType;
    [SerializeField] private ShopTab.ShopTabType _shopType;
    [SerializeField] private Action _updateCallback;
    [SerializeField] private int _contentWeight;
    [SerializeField] private int _contentPrice;
    [SerializeField] private List<ItemInstance> _items;
    [SerializeField] private ShopSlotContainer _adjacentContainer;

    public int ContentWeight { get { return _contentWeight; } }
    public int ContentPrice { get { return _contentPrice; } }
    public ShopSlotContainer AdjacentContainer { get { return _adjacentContainer; } }
    public List<Product> Products { get { return _products; } }

    public void Initialize(VisualElement container, int rowLength, ShopTab.ShopTabType shopType, L2Slot.SlotType type, int minimumContainerSize, ShopSlotContainer adjacentContainer, Action updateCallback)
    {
        base.Initialize(container, rowLength, minimumContainerSize);

        _slotType = type;
        _updateCallback = updateCallback;
        _adjacentContainer = adjacentContainer;
        _shopType = shopType;
    }

    public void RefreshProducts(Product[] products)
    {
        if (products != null)
        {
            _products = products.ToList();
        }
        else
        {
            _products = new List<Product>();
        }

        RefreshProducts();
    }

    private void RefreshProducts()
    {
        _items = new List<ItemInstance>();

        for (int i = 0; i < _products.Count; i++)
        {
            Product p = _products[i];
            ItemInstance item = new ItemInstance(p.ObjectId, p.ItemId, ItemLocation.Void, i, p.Count, p.Type1, p.Type2, false, p.BodyPart, 0, 0);
            _items.Add(item);
        }

        CreateSlots(_items.Count, _slotType);

        AssignItemsToSlots(_items);
        AssignProductsToSlots();
    }

    private void AddOrUpdateProduct(Product product, int quantity)
    {
        // 0 = l'utilisateur a valide une quantite nulle : c'est une
        // annulation. L'ancien code la convertissait en 1, donc valider 0
        // achetait quand meme un exemplaire.
        if (quantity == 0)
        {
            return;
        }

        int slot = GetProductSlot(product);
        if (slot != -1)
        {
            // Le produit est deja la : on ajuste sa quantite, quel que soit
            // son type. L'ancien code ne le faisait que pour les objets
            // empilables (TYPE1_ITEM_QUESTITEM_ADENA) et, pour tous les
            // autres, tombait sur le _products.Add() final : une arme ajoutee
            // deux fois creait deux lignes distinctes au lieu d'incrementer,
            // et une quantite negative (retrait) creait une ligne au Count
            // negatif, faussant le total calcule par CalculatePrice().
            int newCount = _products[slot].Count + quantity;
            if (newCount <= 0)
            {
                RemoveProduct(slot);
            }
            else
            {
                _products[slot].Count = newCount;
            }
            return;
        }

        // Retirer quelque chose qui n'est pas dans la liste n'a pas de sens :
        // sans ce garde-fou on inserait un produit de quantite negative.
        if (quantity < 0)
        {
            return;
        }

        Product p = new Product(product);
        p.Count = quantity;
        _products.Add(p);
    }

    private void RemoveProduct(int slot)
    {
        _products.RemoveAt(slot);
    }

    // Identification d'un produit deja present dans la liste.
    //
    // Onglet VENTE : les produits viennent de l'inventaire, ou chaque objet
    // NON empilable possede son propre ObjectId - et le serveur l'attend
    // (RequestSellItemPacket ecrit ObjectId + ItemId + Count). Deux epees
    // identiques ne doivent donc surtout pas fusionner en une ligne.
    // Onglet ACHAT : les produits sont des modeles sans ObjectId
    // (BuyListPacket ne le renseigne jamais, il vaut 0) et le serveur ne veut
    // que ItemId + Count - on regroupe alors par ItemId.
    private int GetProductSlot(Product product)
    {
        bool hasObjectId = product.ObjectId != 0;

        for (int i = 0; i < _products.Count; i++)
        {
            if (hasObjectId)
            {
                if (_products[i].ObjectId == product.ObjectId) return i;
            }
            else if (_products[i].ItemId == product.ItemId)
            {
                return i;
            }
        }

        return -1;
    }

    public void AddToBasket(Product product, int count)
    {
        AddOrUpdateProduct(product, count);
        RefreshProducts();
        CalculateWeight();
        CalculatePrice();

        if (_slotType == L2Slot.SlotType.Basket && _shopType == ShopTab.ShopTabType.SELL)
        {
            //Updating left tab (inventory) after adding or removing items from the basket
            _adjacentContainer.AddToBasket(product, -count);
        }

        _updateCallback();
    }

    public void RemoveFromBasket(Product product, int index)
    {
        RemoveProduct(index);
        RefreshProducts();
        CalculateWeight();
        CalculatePrice();

        if (_slotType == L2Slot.SlotType.Basket && _shopType == ShopTab.ShopTabType.SELL)
        {
            //Updating left tab (inventory) after adding or removing items from the basket
            _adjacentContainer.AddToBasket(product, product.Count);
        }

        _updateCallback();
    }

    private void CalculateWeight()
    {
        _contentWeight = _items.Sum(s => s.ItemData.Itemgrp.Weight * s.Count);
    }

    private void CalculatePrice()
    {
        _contentPrice = _products.Sum(s => s.Price * s.Count);
    }

    private void AssignProductsToSlots()
    {
        if (_products == null)
        {
            return;
        }

        for (int i = 0; i < _products.Count; i++)
        {
            if (i < _slots.Length)
            {
                ((ProductSlot)_slots[i]).AssignProduct(_products[i]);
            }
            else
            {
                Debug.LogWarning("Product slot index out of range");
            }
        }
    }
}