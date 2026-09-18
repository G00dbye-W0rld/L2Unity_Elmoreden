using System.Collections.Generic;

public class RequestPackageSendPacket : ClientPacket
{
    public RequestPackageSendPacket(int targetId, List<Product> products) : base((byte)GameClientPacketType.RequestPackageSend)
    {
        WriteI(targetId);
        WriteI(products.Count);

        products.ForEach((p) =>
        {
            WriteI(p.ObjectId);
            WriteI(p.Count);
        });

        BuildPacket();
    }
}
