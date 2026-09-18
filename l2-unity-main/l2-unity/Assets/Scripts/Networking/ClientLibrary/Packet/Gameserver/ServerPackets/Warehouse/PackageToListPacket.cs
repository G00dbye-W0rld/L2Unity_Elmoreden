using System.Collections.Generic;

// Autres personnages du compte auxquels un colis peut etre envoye (fret).
public class PackageToListPacket : ServerPacket
{
    public List<KeyValuePair<int, string>> Characters { get; private set; }

    public PackageToListPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        int count = ReadI();
        Characters = new List<KeyValuePair<int, string>>(count);

        for (int i = 0; i < count; i++)
        {
            int objectId = ReadI();
            Characters.Add(new KeyValuePair<int, string>(objectId, ReadS()));
        }
    }
}
