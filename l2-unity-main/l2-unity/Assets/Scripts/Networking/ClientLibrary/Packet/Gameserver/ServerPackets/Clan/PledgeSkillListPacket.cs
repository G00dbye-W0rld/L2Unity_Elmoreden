using System.Collections.Generic;

// Competences du clan : la liste complete (sous-code 0x39) ou une seule
// competence apprise (0x3a), sous forme de paires id / niveau.
public class PledgeSkillListPacket : ServerPacket
{
    public bool IsAddition { get; private set; }
    public List<KeyValuePair<int, int>> Skills { get; private set; } = new List<KeyValuePair<int, int>>();

    public PledgeSkillListPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        IsAddition = ReadH() == 0x3a;

        int count = IsAddition ? 1 : ReadI();
        for (int i = 0; i < count && _iterator + 8 <= _packetData.Length; i++)
        {
            int id = ReadI();
            Skills.Add(new KeyValuePair<int, int>(id, ReadI()));
        }
    }
}
