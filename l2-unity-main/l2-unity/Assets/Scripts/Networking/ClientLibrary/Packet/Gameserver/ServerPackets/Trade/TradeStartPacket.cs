using System.Collections.Generic;

// Ouverture de l'echange : partenaire et objets echangeables de l'inventaire.
public class TradeStartPacket : TradeItemPacket
{
    public int PartnerId { get; private set; }
    public List<Product> Items { get; private set; }

    public TradeStartPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        PartnerId = ReadI();

        int count = ReadH();
        Items = new List<Product>(count);
        for (int i = 0; i < count; i++)
        {
            Items.Add(ReadTradeItem());
        }
    }
}
