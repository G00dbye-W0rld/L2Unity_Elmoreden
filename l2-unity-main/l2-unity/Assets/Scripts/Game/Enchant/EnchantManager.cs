using System;
using UnityEngine;

// Etat client du flux d'enchantement (un parchemin utilise, en attente qu'on
// clique l'objet cible), cote client - source de verite unique, meme patron
// que PartyManager. Le serveur gere deja toute la logique d'enchantement
// (chances, parchemins beni/cristal, destruction) - ce manager ne fait que
// suivre le fil "parchemin arme -> objet choisi -> resultat" pour piloter
// l'UI (InventorySlot/GearSlot interceptent le clic suivant tant qu'on est
// en selection, cf. TryHandleEnchantClick).
public class EnchantManager
{
    private static EnchantManager _instance;
    public static EnchantManager Instance
    {
        get
        {
            if (_instance == null) _instance = new EnchantManager();
            return _instance;
        }
    }

    // Id TEMPLATE du parchemin arme (PAS un ObjectId) - le serveur connait
    // deja le parchemin exact (Player.activeEnchantItem), cette valeur ne
    // sert cote client qu'a l'affichage (cf. Couche C).
    public int ScrollItemId { get; private set; }
    public bool IsSelecting { get; private set; }

    public event Action OnSelectionChanged;
    public event Action<EnchantResultCode> OnResult;

    public void BeginSelection(int scrollItemId)
    {
        ScrollItemId = scrollItemId;
        IsSelecting = true;
        OnSelectionChanged?.Invoke();
    }

    // Annulation cote CLIENT uniquement (aucun paquet dedie identifie cote
    // serveur pour desarmer un parchemin). Le serveur garde son
    // activeEnchantItem arme tant qu'aucun RequestEnchantItem n'arrive -
    // sans consequence : le prochain parchemin utilise le remplace
    // simplement (EnchantScrolls.java ecrase sans verifier), et il n'agit
    // sur rien d'autre en attendant.
    public void CancelSelection()
    {
        if (!IsSelecting) return;

        IsSelecting = false;
        ScrollItemId = 0;
        OnSelectionChanged?.Invoke();
    }

    // Appele par InventorySlot/GearSlot quand on clique un objet alors qu'on
    // est en selection. Sort du mode selection immediatement (pas d'attente
    // du resultat) : la confirmation reelle EST le clic sur l'objet, exactement
    // comme le protocole serveur le prevoit (pas de paquet de confirmation
    // separe avant RequestEnchantItem).
    public void SelectTarget(int objectId)
    {
        if (!IsSelecting) return;

        GameClient.Instance.ClientPacketHandler.SendRequestEnchantItem(objectId);
        CancelSelection();
    }

    public void OnEnchantResult(EnchantResultCode result)
    {
        // Filet de securite : le mode selection devrait deja etre retombe
        // via SelectTarget avant qu'un resultat arrive, mais le serveur peut
        // renvoyer CANCELLED sans qu'on ait nous-meme initie de requete
        // (ex. tentative pendant une transaction en cours) - s'assurer que
        // l'UI ne reste jamais bloquee en mode selection.
        if (IsSelecting)
        {
            CancelSelection();
        }

        OnResult?.Invoke(result);
    }
}
