using UnityEngine;

// Conversion entre une position du monde et un pixel de carte.
//
// Deux cartes, deux etalonnages : la minimap utilise l'image radar, la carte
// du monde l'image d'Interlude, qui n'a ni la meme echelle ni la meme origine.
//
// Les axes sont croises, comme dans les paquets du serveur (cf.
// TeleportToLocationPacket) : le X du jeu devient le Z d'Unity et se lit sur
// l'horizontale de l'image, le Y du jeu devient le X d'Unity et se lit sur la
// verticale.
//
// Chaque etalonnage vient de villes mesurees dans Photoshop, ajustees par
// moindres carres, les centres de ville etant calcules depuis les zones du
// serveur. Refaire ce calcul si une image est remplacee.
public static class MapProjection
{
    /// Etalonnage d'une image : echelle et origine, par axe.
    public struct Calibration
    {
        public float ScaleX;
        public float ScaleY;
        public float OriginX;
        public float OriginY;
        public float Width;
        public float Height;

        public Vector2 WorldToMap(Vector3 world)
        {
            return new Vector2(world.z * ScaleX + OriginX, world.x * ScaleY + OriginY);
        }

        public Vector3 MapToWorld(Vector2 pixel, float worldY)
        {
            return new Vector3((pixel.y - OriginY) / ScaleY, worldY, (pixel.x - OriginX) / ScaleX);
        }
    }

    /// Carte radar (WorldMap.png, 5120 x 4096). Quatre villes mesurees le
    /// 2026-09-13, ecart residuel de 4 px sur 5120.
    public static readonly Calibration Radar = new Calibration
    {
        ScaleX = 0.39343f,
        ScaleY = 0.39343f,
        OriginX = 2460.49f,
        OriginY = 1965.47f,
        Width = 5120f,
        Height = 4096f
    };

    /// Carte du monde d'Interlude (WorldMapInterlude.png, 1812 x 2620),
    /// assemblee depuis int_worldmap1..6. Trois villes mesurees le 2026-09-14,
    /// ecart residuel de 2 px.
    public static readonly Calibration World = new Calibration
    {
        ScaleX = 0.26336f,
        ScaleY = 0.26119f,
        OriginX = 652.05f,
        OriginY = 1312.01f,
        Width = 1812f,
        Height = 2620f
    };

    /// Raccourci pour la minimap, qui reste sur l'image radar.
    public static Vector2 WorldToMap(Vector3 world)
    {
        return Radar.WorldToMap(world);
    }

    public static float MapWidth { get { return Radar.Width; } }
    public static float MapHeight { get { return Radar.Height; } }
}
