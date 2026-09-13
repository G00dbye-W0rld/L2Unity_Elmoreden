using UnityEngine;

// Marqueurs de quete (RadarControl cote serveur, cf. RadarList.java).
// showRadar : 0 = poser un marqueur, 1 = en retirer un, 2 = tout effacer.
public class RadarControlPacket : ServerPacket
{
    public int ShowRadar { get; private set; }
    public int MarkerType { get; private set; }
    public Vector3 Position { get; private set; }

    public RadarControlPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ShowRadar = ReadI();
        MarkerType = ReadI();

        float x = ReadI();
        float y = ReadI();
        float z = ReadI();
        Position = VectorUtils.ConvertPosToUnity(new Vector3(x, y, z));
    }
}
