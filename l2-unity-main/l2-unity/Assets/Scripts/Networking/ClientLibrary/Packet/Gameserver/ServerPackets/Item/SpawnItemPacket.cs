using UnityEngine;

// Envoye quand le joueur decouvre un objet DEJA present au sol (entre dans
// la zone, ou objet restaure au demarrage serveur) - pas d'infos de "dropper"
// ni d'animation associee, contrairement a DropItemPacket.
public class SpawnItemPacket : ServerPacket
{
    public int ObjectId { get; private set; }
    public int ItemTemplateId { get; private set; }
    public Vector3 Position { get; private set; }
    public bool IsStackable { get; private set; }
    public int Count { get; private set; }

    public SpawnItemPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ObjectId = ReadI();
        ItemTemplateId = ReadI();

        float z = ReadI() / 52.5f;
        float x = ReadI() / 52.5f;
        float y = ReadI() / 52.5f;
        Position = new Vector3(x, y, z);

        IsStackable = ReadI() == 1;
        Count = ReadI();
        ReadI(); // inconnu, toujours 0
    }
}
