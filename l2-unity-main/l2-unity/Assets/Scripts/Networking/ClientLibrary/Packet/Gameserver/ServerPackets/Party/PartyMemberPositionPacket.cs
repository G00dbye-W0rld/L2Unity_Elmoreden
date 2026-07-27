using System.Collections.Generic;
using UnityEngine;

// Position de chaque membre du groupe, rediffusee toutes les ~12s cote serveur
// (Party.java, _positionBroadcastTask). Utile plus tard pour un radar/une
// mini-carte des membres - pas consomme davantage en Couche A.
public class PartyMemberPositionPacket : ServerPacket
{
    public Dictionary<int, Vector3> Positions { get; private set; } = new Dictionary<int, Vector3>();

    public PartyMemberPositionPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        int count = ReadI();

        for (int i = 0; i < count; i++)
        {
            int objectId = ReadI();
            float x = ReadI();
            float y = ReadI();
            float z = ReadI();
            Positions[objectId] = VectorUtils.ConvertPosToUnity(new Vector3(x, y, z));
        }
    }
}
