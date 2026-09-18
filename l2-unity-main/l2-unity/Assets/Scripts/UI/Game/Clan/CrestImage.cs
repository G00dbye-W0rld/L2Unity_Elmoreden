using UnityEngine;

// Blasons : PNG pour ceux que le client envoie, DDS DXT1 pour ceux qui
// viennent du jeu d'origine. Les DDS sont decodes a la main plutot que confies
// a Unity : ils sont stockes ligne du haut en premier, Unity attend l'inverse,
// et l'image arriverait retournee.
public static class CrestImage
{
    private const int HeaderSize = 128;
    private const int MaxSide = 512;

    // Les dimensions sont sur 32 bits signes : une valeur negative doit etre
    // refusee comme une trop grande.
    private static bool ValidSide(int side)
    {
        return side >= 1 && side <= MaxSide;
    }

    private static int ReadBigEndian(byte[] data, int offset)
    {
        return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
    }

    public static Texture2D Decode(byte[] data)
    {
        if (data == null || data.Length < 8)
        {
            return null;
        }

        // Signature PNG.
        if (data[0] == 0x89 && data[1] == (byte)'P' && data[2] == (byte)'N' && data[3] == (byte)'G')
        {
            // Un PNG de quelques octets peut declarer des milliers de pixels :
            // on lit ses dimensions avant de laisser Unity le decompresser.
            if (data.Length < 24 || !ValidSide(ReadBigEndian(data, 16)) || !ValidSide(ReadBigEndian(data, 20)))
            {
                return null;
            }

            Texture2D png = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            png.filterMode = FilterMode.Bilinear;
            return png.LoadImage(data) ? png : null;
        }

        return DecodeDds(data);
    }

    private static Texture2D DecodeDds(byte[] dds)
    {
        if (dds == null || dds.Length <= HeaderSize || dds[0] != 'D' || dds[1] != 'D' || dds[2] != 'S')
        {
            return null;
        }

        int height = System.BitConverter.ToInt32(dds, 12);
        int width = System.BitConverter.ToInt32(dds, 16);
        string fourCc = System.Text.Encoding.ASCII.GetString(dds, 84, 4);

        if (width <= 0 || height <= 0 || fourCc != "DXT1")
        {
            Debug.LogWarning("[Clan] Blason non decodable : " + fourCc + " " + width + "x" + height);
            return null;
        }

        Color32[] pixels = new Color32[width * height];
        int blocksX = (width + 3) / 4;
        int blocksY = (height + 3) / 4;
        int offset = HeaderSize;

        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                if (offset + 8 > dds.Length)
                {
                    return null;
                }

                DecodeBlock(dds, offset, bx * 4, by * 4, width, height, pixels);
                offset += 8;
            }
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    // Un bloc DXT1 : deux couleurs sur 16 bits, puis 16 indices sur 2 bits.
    private static void DecodeBlock(byte[] data, int offset, int startX, int startY, int width, int height, Color32[] pixels)
    {
        ushort c0 = (ushort)(data[offset] | (data[offset + 1] << 8));
        ushort c1 = (ushort)(data[offset + 2] | (data[offset + 3] << 8));

        Color32[] palette = new Color32[4];
        palette[0] = FromRgb565(c0);
        palette[1] = FromRgb565(c1);

        if (c0 > c1)
        {
            palette[2] = Mix(palette[0], palette[1], 2, 1);
            palette[3] = Mix(palette[0], palette[1], 1, 2);
        }
        else
        {
            palette[2] = Mix(palette[0], palette[1], 1, 1);
            palette[3] = new Color32(0, 0, 0, 0);
        }

        for (int y = 0; y < 4; y++)
        {
            byte row = data[offset + 4 + y];

            for (int x = 0; x < 4; x++)
            {
                int px = startX + x;
                int py = startY + y;
                if (px >= width || py >= height)
                {
                    continue;
                }

                // Unity remplit de bas en haut : on inverse la ligne.
                pixels[(height - 1 - py) * width + px] = palette[(row >> (x * 2)) & 0x03];
            }
        }
    }

    private static Color32 FromRgb565(ushort value)
    {
        int r = (value >> 11) & 0x1f;
        int g = (value >> 5) & 0x3f;
        int b = value & 0x1f;

        return new Color32((byte)((r * 255) / 31), (byte)((g * 255) / 63), (byte)((b * 255) / 31), 255);
    }

    private static Color32 Mix(Color32 a, Color32 b, int weightA, int weightB)
    {
        int total = weightA + weightB;
        return new Color32(
            (byte)((a.r * weightA + b.r * weightB) / total),
            (byte)((a.g * weightA + b.g * weightB) / total),
            (byte)((a.b * weightA + b.b * weightB) / total),
            255);
    }
}
