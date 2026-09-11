// Valeurs identiques a l'enum OperateType du serveur (octet de CharInfo/UserInfo).
public enum OperateType : byte
{
    None = 0,
    Sell = 1,
    SellManage = 2,
    Buy = 3,
    BuyManage = 4,
    Manufacture = 5,
    ManufactureManage = 6,
    Observe = 7,
    PackageSell = 8
}
