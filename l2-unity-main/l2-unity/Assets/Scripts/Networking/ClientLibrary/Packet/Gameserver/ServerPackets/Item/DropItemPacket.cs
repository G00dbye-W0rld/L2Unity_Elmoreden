using UnityEngine;

// Envoye aux joueurs deja a portee au moment exact ou un objet tombe au sol
// (mort de monstre, drop joueur, familier, arme maudite). Contrairement a
// SpawnItemPacket, contient l'ID de celui qui a fait tomber l'objet (utilise
// cote serveur pour une eventuelle animation de "jet", non geree ici pour
// l'instant).
public class DropItemPacket : ServerPacket
{
    public int DropperObjectId { get; private set; }
    public int ItemObjectId { get; private set; }
    public int ItemTemplateId { get; private set; }
    public Vector3 Position { get; private set; }
    public bool IsStackable { get; private set; }
    public int Count { get; private set; }

    public DropItemPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        DropperObjectId = ReadI();
        ItemObjectId = ReadI();
        ItemTemplateId = ReadI();

        float z = ReadI() / 52.5f;
        float x = ReadI() / 52.5f;
        float y = ReadI() / 52.5f;
        Position = new Vector3(x, y, z);

        IsStackable = ReadI() == 1;
        Count = ReadI();
        ReadI(); // inconnu, toujours 1
    }
}
