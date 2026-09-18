// Nombre de membres par rang, de 1 a 9. Paquet etendu : sous-code 0x3b.
public class PledgePowerGradeListPacket : ServerPacket
{
    public int[] MembersPerRank { get; private set; } = new int[10];

    public PledgePowerGradeListPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ReadH();

        int count = ReadI();
        for (int i = 0; i < count; i++)
        {
            int rank = ReadI();
            int members = ReadI();

            if (rank >= 0 && rank < MembersPerRank.Length)
            {
                MembersPerRank[rank] = members;
            }
        }
    }
}
