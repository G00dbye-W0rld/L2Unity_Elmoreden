using UnityEngine;

// Etat initial d'une porte. Le paquet d'origine ne porte pas de position :
// le serveur du projet ajoute x/y/z a la fin pour que le client retrouve le
// bon battant dans la scene (cf. DoorInfo.java).
public class DoorInfoPacket : ServerPacket
{
    public int ObjectId { get; private set; }
    public int DoorId { get; private set; }
    public bool Opened { get; private set; }
    public Vector3 Position { get; private set; }

    public DoorInfoPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ObjectId = ReadI();
        DoorId = ReadI();
        ReadI(); // affichage des points de vie
        ReadI(); // ciblable
        Opened = ReadI() == 0;
        ReadI(); // points de vie maximum
        ReadI(); // points de vie
        ReadI();
        ReadI();

        float x = ReadI();
        float y = ReadI();
        float z = ReadI();
        Position = VectorUtils.ConvertPosToUnity(new Vector3(x, y, z));
    }
}
