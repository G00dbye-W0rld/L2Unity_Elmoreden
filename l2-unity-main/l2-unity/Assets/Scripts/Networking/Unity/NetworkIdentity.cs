using UnityEngine;

[System.Serializable]
public class NetworkIdentity
{
    [SerializeField] private EntityType _entityType;
    [SerializeField] private int _id;
    [SerializeField] private string _name;
    [SerializeField] private string _title;

    [Header("Relation")]
    [SerializeField] private bool _isHpShowable;
    [SerializeField] private int _pvpFlag;
    [SerializeField] private int _relation;
    [SerializeField] private bool _autoAttackable;

    [Header("Npc")]
    [SerializeField] private int _npcId;
    [SerializeField] private string _npcClass;

    [Header("Player")]
    [SerializeField] private byte _playerClass;
    [SerializeField] private bool _isMage;

    [Header("Clan")]
    [SerializeField] private int _clanId;
    [SerializeField] private int _clanCrestId;
    [SerializeField] private int _allyId;
    [SerializeField] private int _allyCrestId;
    [SerializeField] private int _clanPrivileges;

    [Header("Transform")]
    [SerializeField] private Vector3 _position = new Vector3(0, 0, 0);
    [SerializeField] private float _heading;

    [SerializeField] private bool _owned = false;

    public EntityType EntityType { get => _entityType; set => _entityType = value; }
    public int Id { get => _id; set => _id = value; }
    public int NpcId { get => _npcId; set => _npcId = value; }
    public string NpcClass { get => _npcClass; set => _npcClass = value; }
    public string Name { get => _name; set => _name = value; }
    public string Title { get => _title; set => _title = value; }
    public Vector3 Position { get => _position; set => _position = value; }
    public float Heading { get => _heading; set => _heading = value; }
    public bool Owned { get => _owned; set => _owned = value; }
    public byte PlayerClass { get => _playerClass; set => _playerClass = value; }
    public bool IsMage { get => _isMage; set => _isMage = value; }
    public int ClanId { get => _clanId; set => _clanId = value; }
    public int ClanCrestId { get => _clanCrestId; set => _clanCrestId = value; }
    public int AllyId { get => _allyId; set => _allyId = value; }
    public int AllyCrestId { get => _allyCrestId; set => _allyCrestId = value; }
    public int ClanPrivileges { get => _clanPrivileges; set => _clanPrivileges = value; }
    public bool IsHpShowable { get => _isHpShowable; set => _isHpShowable = value; }
    public int PvpFlag { get => _pvpFlag; set => _pvpFlag = value; }

    /// Relation vue par le joueur local (RelationChanged) : non recopiee par
    /// UpdateIdentity, les paquets d'apparition ne la portent pas.
    public int Relation { get => _relation; set => _relation = value; }
    public bool AutoAttackable { get => _autoAttackable; set => _autoAttackable = value; }
    public bool IsMutualWar { get { return (_relation & RelationChangedPacket.RELATION_MUTUAL_WAR) != 0; } }
    public bool IsOneSidedWar { get { return !IsMutualWar && (_relation & RelationChangedPacket.RELATION_1SIDED_WAR) != 0; } }

    public NetworkIdentity() { }

    public void UpdateIdentity(NetworkIdentity identity)
    {
        _entityType = identity.EntityType;
        _id = identity.Id;
        _npcId = identity.NpcId;
        _npcClass = identity.NpcClass;
        _name = identity.Name;
        _title = identity.Title;
        _position = identity.Position;
        _heading = identity.Heading;
        _owned = identity.Owned;
        _playerClass = identity.PlayerClass;
        _isMage = identity.IsMage;
        _pvpFlag = identity.PvpFlag;
        _clanId = identity.ClanId;
        _clanCrestId = identity.ClanCrestId;
        _allyId = identity.AllyId;
        _allyCrestId = identity.AllyCrestId;
        _clanPrivileges = identity.ClanPrivileges;
    }

    public void UpdateForNpcs(NetworkIdentity identity)
    {
        _position = identity.Position;
        _heading = identity.Heading;
    }

    public void SetPosX(float x)
    {
        _position.x = x;
    }

    public void SetPosY(float y)
    {
        _position.y = y;
    }

    public void SetPosZ(float z)
    {
        _position.z = z;
    }
}
