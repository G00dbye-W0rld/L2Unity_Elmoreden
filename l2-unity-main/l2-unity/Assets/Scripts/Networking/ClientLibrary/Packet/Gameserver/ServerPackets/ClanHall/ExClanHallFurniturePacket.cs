using System.Collections.Generic;

// Paquet du projet (0xFE:0x5d) : mobilier pose dans une salle de clan.
public class ExClanHallFurniturePacket : ServerPacket
{
    public int HallId { get; private set; }
    public List<ClanHallFurniture.Placed> Placed { get; private set; }
    public int WorkbenchObjectId { get; private set; }

    public ExClanHallFurniturePacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ReadH();

        HallId = ReadI();
        int count = ReadI();
        Placed = new List<ClanHallFurniture.Placed>(count);
        for (int i = 0; i < count; i++)
        {
            Placed.Add(new ClanHallFurniture.Placed { Slot = ReadI(), ItemId = ReadI(), ChestObjectId = ReadI() });
        }

        if (_packetData.Length - _iterator >= 4)
        {
            WorkbenchObjectId = ReadI();
        }
    }
}
