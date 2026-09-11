using System;
using UnityEngine;
using UnityEngine.UIElements;

public static class PrivateStoreDialogs
{
    private const int MoveQuantityMessageId = 72;
    private const string StylePath = "Data/UI/_Elements/Game/PrivateStoreWindow/PrivateStoreWindow";

    // L2Window ne garde que le premier enfant du modele : les <Style> de
    // l'UXML restent sur le conteneur abandonne. Les autres fenetres
    // passent par Lineage2_Game.uxml ; celles-ci chargent leur feuille elles-memes.
    public static void AttachStyle(VisualElement window)
    {
        StyleSheet sheet = Resources.Load<StyleSheet>(StylePath);
        if (sheet != null)
        {
            window.styleSheets.Add(sheet);
        }
        else
        {
            Debug.LogWarning($"[PrivateStore] Feuille de style introuvable : {StylePath}");
        }
    }

    // Un objet seul part directement ; une pile demande combien en deplacer.
    public static void AskQuantity(Product product, Action<int> then)
    {
        if (product.Count <= 1)
        {
            then(1);
            return;
        }

        L2InputAmountWindow.Instance.ShowWindow(QuantityMessage(product), product.Count, then, () => { });
    }

    // Quantite libre : un magasin d'achat peut demander plus qu'on n'en possede.
    public static void AskAnyQuantity(Product product, Action<int> then)
    {
        L2InputAmountWindow.Instance.ShowWindow(QuantityMessage(product), 0, then, () => { });
    }

    private static SystemMessage QuantityMessage(Product product)
    {
        SMParam[] smParams = { new SMParam(SMParam.SMParamType.TYPE_ITEM_NAME, product.ItemId) };
        return new SystemMessage(smParams, SystemMessageTable.Instance.GetSystemMessage(MoveQuantityMessageId));
    }
}
