using System;
using System.Collections.Generic;

// Etat du groupe du joueur local, cote client - source de verite unique
// (Couche B). Le reste du jeu (nameplates, fenetre Party, actions) interroge
// cette classe plutot que de dupliquer l'etat ailleurs (ex. sur Entity).
// Alimentee par les handlers de GameServerPacketHandler.cs (Couche A).
public class PartyManager
{
    private static PartyManager _instance;
    public static PartyManager Instance
    {
        get
        {
            if (_instance == null) _instance = new PartyManager();
            return _instance;
        }
    }

    private readonly Dictionary<int, PartyMemberInfo> _otherMembers = new Dictionary<int, PartyMemberInfo>();

    public bool IsInParty { get; private set; }
    public int LeaderObjectId { get; private set; }
    public PartyLootRule LootRule { get; private set; }

    // Ne contient JAMAIS le joueur local (le serveur ne se renvoie pas
    // lui-meme dans PartySmallWindowAll/Add, cf. Party.java).
    public IReadOnlyDictionary<int, PartyMemberInfo> OtherMembers => _otherMembers;

    public bool IsLeader => IsInParty && PlayerEntity.Instance != null && LeaderObjectId == PlayerEntity.Instance.Identity.Id;

    public event Action OnPartyChanged;

    public bool IsMember(int objectId)
    {
        if (!IsInParty) return false;
        if (PlayerEntity.Instance != null && objectId == PlayerEntity.Instance.Identity.Id) return true;
        return _otherMembers.ContainsKey(objectId);
    }

    // A privilegier des que possible par rapport a IsMember(int) : les
    // ObjectId L2 viennent d'un pool PARTAGE entre tous les types d'entite
    // (joueurs, PNJ, monstres...) et sont recycles en permanence. Un PNJ
    // peut donc, apres coup, heriter du meme ObjectId qu'un membre du
    // groupe encore connecte (ex. coloration cyan d'un PNJ au hasard, bug
    // observe et corrige le 2026-07-25) - IsMember(int) seul ne peut pas
    // distinguer ce cas puisqu'il ne voit qu'un nombre. Verifie donc en
    // plus que l'entite est bien un joueur avant de comparer l'ObjectId.
    public bool IsMember(Entity entity)
    {
        if (entity == null) return false;
        EntityType type = entity.Identity.EntityType;
        if (type != EntityType.Player && type != EntityType.User) return false;

        return IsMember(entity.Identity.Id);
    }

    public void SetFullState(int leaderObjectId, PartyLootRule lootRule, List<PartyMemberInfo> members)
    {
        IsInParty = true;
        LeaderObjectId = leaderObjectId;
        LootRule = lootRule;

        _otherMembers.Clear();
        foreach (PartyMemberInfo member in members)
        {
            _otherMembers[member.ObjectId] = member;
        }

        OnPartyChanged?.Invoke();
    }

    public void AddMember(int leaderObjectId, PartyLootRule lootRule, PartyMemberInfo member)
    {
        IsInParty = true;
        LeaderObjectId = leaderObjectId;
        LootRule = lootRule;

        // Le serveur ne renvoie jamais le joueur local dans ce paquet, mais on
        // se protege quand meme d'un ajout de soi-meme par securite.
        if (PlayerEntity.Instance == null || member.ObjectId != PlayerEntity.Instance.Identity.Id)
        {
            _otherMembers[member.ObjectId] = member;
        }

        OnPartyChanged?.Invoke();
    }

    public void RemoveMember(int objectId)
    {
        _otherMembers.Remove(objectId);
        OnPartyChanged?.Invoke();
    }

    public void Disband()
    {
        IsInParty = false;
        LeaderObjectId = 0;
        _otherMembers.Clear();
        OnPartyChanged?.Invoke();
    }

    // Pas de OnPartyChanged ici (contrairement aux autres methodes) : cet
    // evenement declenche un RefreshMembers() complet (rebuild du DOM) cote
    // PartyWindow, beaucoup trop lourd pour un paquet qui peut arriver a
    // chaque coup encaisse par chaque membre. PartyWindow relit deja ce
    // dictionnaire par sondage (UpdateVitals(), toutes les 0.5s dans
    // FixedUpdate) donc la mise a jour est visible sans evenement dedie.
    public void UpdateMemberVitals(int objectId, int hp, int maxHp, int mp, int maxMp, int cp, int maxCp)
    {
        if (!_otherMembers.TryGetValue(objectId, out PartyMemberInfo member)) return;

        member.Hp = hp;
        member.MaxHp = maxHp;
        member.Mp = mp;
        member.MaxMp = maxMp;
        member.Cp = cp;
        member.MaxCp = maxCp;
    }
}
