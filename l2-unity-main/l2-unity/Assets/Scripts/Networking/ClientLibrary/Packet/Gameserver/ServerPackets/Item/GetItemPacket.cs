using UnityEngine;

// Diffuse au moment du ramassage d'un objet au sol (le "swoop"). DeleteObject
// suit juste apres pour retirer l'objet du monde (RemoveObjectPacket, deja gere).
public class GetItemPacket : ServerPacket
{
    public int PickerObjectId { get; private set; }
    public int ItemObjectId { get; private set; }
    public Vector3 Position { get; private set; }

    public GetItemPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        PickerObjectId = ReadI();
        ItemObjectId = ReadI();

        float z = ReadI() / 52.5f;
        float x = ReadI() / 52.5f;
        float y = ReadI() / 52.5f;
        Position = new Vector3(x, y, z);
    }
}
