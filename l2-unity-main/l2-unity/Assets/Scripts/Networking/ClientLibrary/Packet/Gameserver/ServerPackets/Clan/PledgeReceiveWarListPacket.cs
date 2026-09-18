using System.Collections.Generic;

// Guerres d'un onglet. Paquet etendu : sous-code 0x3e.
public class PledgeReceiveWarListPacket : ServerPacket
{
    public int Tab { get; private set; }
    public int Page { get; private set; }
    public List<string> Clans { get; private set; } = new List<string>();

    public PledgeReceiveWarListPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ReadH();
        Tab = ReadI();
        Page = ReadI();
        int count = ReadI();

        // Le serveur annonce un compte qui peut differer du nombre d'entrees
        // ecrites (clan dissous saute, calcul de page approximatif) : on
        // s'arrete aussi a la fin des donnees.
        for (int i = 0; i < count && _iterator < _packetData.Length; i++)
        {
            Clans.Add(ReadS());
            ReadI();
            ReadI();
        }
    }
}
