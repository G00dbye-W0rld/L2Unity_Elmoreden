using System.Collections.Generic;

// Inventaire restant apres un ajout a l'offre. Type 2 : l'objet sort de la
// liste (Count remis a 0) ; type 3 : Count est la quantite encore disponible.
public class TradeUpdatePacket : TradeItemPacket
{
    public List<Product> Items { get; private set; }

    public TradeUpdatePacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        int count = ReadH();
        Items = new List<Product>(count);

        for (int i = 0; i < count; i++)
        {
            int change = ReadH();
            Product p = ReadTradeItem();
            if (change != 3)
            {
                p.Count = 0;
            }
            Items.Add(p);
        }
    }
}
