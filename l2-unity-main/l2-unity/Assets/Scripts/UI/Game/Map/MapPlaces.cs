using UnityEngine;

// Lieux affichables sur la carte : nom, position dans le monde, et plan
// detaille quand il en existe un. Les positions viennent des zones du serveur
// (data/xml/zones/TownZone.xml), converties en coordonnees Unity.
public static class MapPlaces
{
    public struct Place
    {
        public string Name;
        public Vector3 World;
        public string Plan;
    }

    /// Les coordonnees sont celles du jeu, divisees par 52,5 et axes croises :
    /// le X du jeu devient le Z d'Unity, son Y devient le X d'Unity.
    private static Place Town(string name, int l2x, int l2y, string plan)
    {
        return new Place
        {
            Name = name,
            World = new Vector3(l2y / 52.5f, 0f, l2x / 52.5f),
            Plan = plan
        };
    }

    public static readonly Place[] Towns =
    {
        Town("\u00cele Parlante", -84327, 242833, "talkin_island_village"),
        Town("Gludin", -80417, 151635, "gludin_village"),
        Town("Gludio", -14522, 123414, "gludio_castle_town"),
        Town("Dion", 18112, 144278, "dion_castle_town"),
        Town("Floran", 17629, 170150, "floran_village"),
        Town("Giran", 84273, 148288, "giran_castle_town"),
        Town("Heine", 112094, 221381, "heine"),
        Town("Oren", 80896, 54436, "town_of_oren"),
        Town("Village des Chasseurs", 117113, 76299, "hunters_village"),
        Town("Aden", 147229, 26073, "town_of_aden"),
        Town("Rune", 39840, -48430, "rune_village"),
        Town("Goddard", 147728, -56548, "godard_castle_town"),
        Town("Schuttgart", 87626, -141761, "town_of_schuttgart"),
        Town("Village Elfe", 45365, 50045, "elven_village"),
        Town("Village des Elfes Noirs", 11247, 17086, "dark_elven_village"),
        Town("Village Orque", -45586, -113739, "orc_village"),
        Town("Village Nain", 116440, -180511, "dwarven_village")
    };

    /// Donjons ayant un plan detaille. Positions prises dans les teleports du
    /// serveur. Le laboratoire de magie noire n'y figure pas : sa position
    /// reste a preciser, son plan existe deja.
    public static readonly Place[] Dungeons =
    {
        Town("Ruines Elfiques", -113686, 235723, "elven_ruin"),
        Town("Forteresse Elfique", 29074, 74958, "elven_underground_fortress"),
        Town("Caverne des \u00c9preuves", 9340, -112509, "dungeon_of_trial"),
        Town("Mines de Charbon Abandonn\u00e9es", 139714, -177456, "abaodoned_mime")
    };
}
