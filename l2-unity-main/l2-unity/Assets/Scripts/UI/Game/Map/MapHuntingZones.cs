using UnityEngine;

// Catalogue des zones du client Interlude (system/huntingzone-e.dat) :
// territoire, type, niveau conseille et position. Les noms restent en
// anglais, comme l'image de la carte.
public static class MapHuntingZones
{
    public enum Kind { Field, Dungeon, Town, Harbor, Fortress, Arena, Landmark }

    public struct Zone
    {
        public string Name;
        public string French;
        public int Territory;
        public Kind Type;
        public int Level;
        public Vector3 World;
    }

    public struct Territory
    {
        public int Id;
        public string Name;
        public string French;
    }

    public static readonly Territory[] Territories =
    {
        new Territory { Id = 1, Name = "Dion Territory", French = "Territoire de Dion" },
        new Territory { Id = 16, Name = "Giran Territory", French = "Territoire de Giran" },
        new Territory { Id = 30, Name = "Gludio Territory", French = "Territoire de Gludio" },
        new Territory { Id = 67, Name = "Oren Territory", French = "Territoire d'Oren" },
        new Territory { Id = 97, Name = "Aden Territory", French = "Territoire d'Aden" },
        new Territory { Id = 121, Name = "Innadril Territory", French = "Territoire d'Innadril" },
        new Territory { Id = 138, Name = "Schuttgart Territory", French = "Territoire de Schuttgart" },
        new Territory { Id = 179, Name = "Border", French = "Fronti\u00e8re" },
        new Territory { Id = 180, Name = "Goddard Territory", French = "Territoire de Goddard" },
        new Territory { Id = 200, Name = "Rune Territory", French = "Territoire de Rune" }
    };

    private static Zone Z(string name, string french, int territory, Kind type, int level, int l2x, int l2y)
    {
        return new Zone
        {
            Name = name,
            French = french,
            Territory = territory,
            Type = type,
            Level = level,
            World = new Vector3(l2y / 52.5f, 0f, l2x / 52.5f)
        };
    }

    public static readonly Zone[] Zones =
    {
        Z("Execution Grounds", "Lieu d'Ex\u00e9cution", 1, Kind.Field, 25, 50568, 152408),
        Z("Partisan's Hideaway", "Repaire des Partisans", 1, Kind.Field, 23, 50081, 116859),
        Z("Cruma Marshlands", "Marais de Cruma", 1, Kind.Field, 25, 5106, 126916),
        Z("Cruma Tower", "Tour de Cruma", 1, Kind.Dungeon, 40, 17225, 114173),
        Z("Mandragora Farm", "Ferme aux Mandragores", 1, Kind.Field, 20, 38291, 148029),
        Z("Town of Dion", "Ville de Dion", 1, Kind.Town, 0, 16856, 144673),
        Z("Floran Village", "Village de Floran", 1, Kind.Town, 0, 17308, 170368),
        Z("Dion Castle", "Ch\u00e2teau de Dion", 1, Kind.Town, 0, 22310, 155917),
        Z("Tanor Canyon", "Canyon de Tanor", 1, Kind.Field, 46, 59170, 164817),
        Z("Bee Hive", "La Ruche", 1, Kind.Field, 34, 22944, 182122),
        Z("Dion Hills", "Collines de Dion", 1, Kind.Field, 20, 29928, 151415),
        Z("Floran Agricultural Area", "Terres Agricoles de Floran", 1, Kind.Field, 30, 10610, 156322),
        Z("Plains of Dion", "Plaines de Dion", 1, Kind.Field, 23, 630, 179184),
        Z("Fortress of Resistance", "Forteresse de la R\u00e9sistance", 1, Kind.Fortress, 0, 43962, 108861),
        Z("Hardin's Academy", "Acad\u00e9mie de Hardin", 16, Kind.Town, 0, 105918, 109759),
        Z("Dragon Valley", "Vall\u00e9e des Dragons", 16, Kind.Field, 45, 79745, 115299),
        Z("Antharas' Lair", "Antre d'Antharas", 16, Kind.Dungeon, 60, 131557, 114509),
        Z("Antharas' Nest", "Nid d'Antharas", 16, Kind.Dungeon, 79, 177160, 114922),
        Z("Death Pass", "Passe de la Mort", 16, Kind.Field, 35, 67933, 117045),
        Z("Pirate Tunnel", "Tunnel des Pirates", 16, Kind.Landmark, 0, 41528, 198358),
        Z("Devil's Isle", "\u00cele du Diable", 16, Kind.Dungeon, 45, 43408, 206881),
        Z("Giran Harbor", "Port de Giran", 16, Kind.Harbor, 0, 47938, 186864),
        Z("Giran Castle Town", "Ville de Giran", 16, Kind.Town, 0, 83475, 147966),
        Z("Giran Arena", "Ar\u00e8ne de Giran", 16, Kind.Arena, 0, 73579, 142709),
        Z("Giran Castle", "Ch\u00e2teau de Giran", 16, Kind.Town, 0, 112077, 144869),
        Z("Breka's Stronghold", "Bastion de Breka", 16, Kind.Field, 30, 85546, 131328),
        Z("Gorgon Flower Garden", "Jardin des Fleurs Gorgones", 16, Kind.Field, 31, 113553, 134813),
        Z("Ruins of Despair", "Ruines du D\u00e9sespoir", 30, Kind.Field, 15, -19120, 136816),
        Z("Ruins of Agony", "Ruines de l'Agonie", 30, Kind.Field, 15, -42628, 119766),
        Z("Wasteland", "Terres D\u00e9sol\u00e9es", 30, Kind.Field, 25, -22726, 190368),
        Z("The Ant Nest", "La Fourmili\u00e8re", 30, Kind.Dungeon, 29, -9959, 176184),
        Z("Gludin Village", "Village de Gludin", 30, Kind.Town, 0, -80684, 149770),
        Z("Gludin Harbor", "Port de Gludin", 30, Kind.Harbor, 0, -91101, 150344),
        Z("Town of Gludio", "Ville de Gludio", 30, Kind.Town, 0, -12787, 122779),
        Z("Abandoned Camp", "Camp Abandonn\u00e9", 30, Kind.Field, 21, -49853, 147089),
        Z("Orc Barracks", "Caserne Orque", 30, Kind.Field, 24, -89763, 105359),
        Z("Forgotten Temple", "Temple Oubli\u00e9", 30, Kind.Dungeon, 27, -53001, 191425),
        Z("Fellmere Lake", "Lac de Fellmere", 30, Kind.Landmark, 0, -57798, 127629),
        Z("Gludin Arena", "Ar\u00e8ne de Gludin", 30, Kind.Arena, 0, -87328, 142266),
        Z("Gludio Castle", "Ch\u00e2teau de Gludio", 30, Kind.Town, 0, -18341, 113946),
        Z("Windy Hill", "Colline des Vents", 30, Kind.Field, 26, -88539, 83389),
        Z("Red Rock Ridge", "Cr\u00eate de Roche Rouge", 30, Kind.Field, 30, -44829, 188171),
        Z("Langk Lizardmen Dwellings", "Habitations des Hommes-L\u00e9zards Langk", 30, Kind.Field, 16, -44763, 203497),
        Z("Maille Lizardmen Barracks", "Caserne des Hommes-L\u00e9zards Maille", 30, Kind.Field, 24, -25283, 106820),
        Z("Talking Island", "\u00cele Parlante", 30, Kind.Landmark, 0, -84141, 244623),
        Z("Talking Island Village", "Village de l'\u00cele Parlante", 30, Kind.Town, 0, -84141, 244623),
        Z("Cedric's Training Hall", "Salle d'Entra\u00eenement de Cedric", 30, Kind.Town, 0, -72674, 256819),
        Z("Einhovant's School of Magic", "\u00c9cole de Magie d'Einhovant", 30, Kind.Town, 0, -89041, 248907),
        Z("Obelisk of Victory", "Ob\u00e9lisque de la Victoire", 30, Kind.Landmark, 0, -99586, 237637),
        Z("Elven Ruins", "Ruines Elfiques", 30, Kind.Dungeon, 9, -112367, 234703),
        Z("Talking Island Harbor", "Port de l'\u00cele Parlante", 30, Kind.Harbor, 0, -96811, 259153),
        Z("Talking Island, Western Territory", "\u00cele Parlante, Territoire Ouest", 30, Kind.Field, 8, -104344, 226217),
        Z("Talking Island, Eastern Territory", "\u00cele Parlante, Territoire Est", 30, Kind.Field, 1, -95336, 240478),
        Z("Fellmere Harvesting Grounds", "Champs de Fellmere", 30, Kind.Field, 17, -63736, 101522),
        Z("Windmill Hill", "Colline du Moulin", 30, Kind.Field, 15, -72417, 173629),
        Z("Ruins of Agony Bend", "D\u00e9tour des Ruines de l'Agonie", 30, Kind.Field, 19, -50174, 129303),
        Z("Evil Hunting Grounds", "Terrain de Chasse Maudit", 30, Kind.Field, 14, -6989, 109503),
        Z("Entrance to the Ruins of Despair", "Entr\u00e9e des Ruines du D\u00e9sespoir", 30, Kind.Field, 19, -36652, 135591),
        Z("Windawood Manor", "Manoir de Windawood", 30, Kind.Field, 22, -24794, 156502),
        Z("Ol Mahum Checkpoint", "Poste des Ol Mahum", 30, Kind.Field, 22, -6661, 201880),
        Z("Ant Incubator", "Couvoir des Fourmis", 30, Kind.Dungeon, 36, -26489, 195307),
        Z("Singing Waterfall", "Cascade Chantante", 30, Kind.Landmark, 0, -111728, 244330),
        Z("The Neutral Zone", "Zone Neutre", 30, Kind.Field, 15, -10612, 75881),
        Z("Elven Forest", "For\u00eat Elfique", 67, Kind.Field, 8, 21362, 51122),
        Z("Shadow of the Mother Tree", "Ombre de l'Arbre M\u00e8re", 67, Kind.Field, 1, 50953, 42105),
        Z("Elven Village", "Village Elfe", 67, Kind.Town, 0, 46951, 51550),
        Z("Elven Fortress", "Forteresse Elfique", 67, Kind.Dungeon, 10, 29294, 74968),
        Z("Iris Lake", "Lac Iris", 67, Kind.Landmark, 0, 51469, 82600),
        Z("The Dark Forest", "For\u00eat Sombre", 67, Kind.Field, 8, -22224, 14168),
        Z("The Shilen Temple", "Temple de Shilen", 67, Kind.Town, 0, 25934, 11037),
        Z("Dark Elf Village", "Village des Elfes Noirs", 67, Kind.Town, 0, 9709, 15566),
        Z("School of Dark Arts", "\u00c9cole des Arts Sombres", 67, Kind.Dungeon, 10, -47543, 58478),
        Z("Swampland", "Mar\u00e9cages", 67, Kind.Field, 13, -19048, 48198),
        Z("Altar of Rites", "Autel des Rites", 67, Kind.Landmark, 0, -44566, 77508),
        Z("Sea of Spores", "Mer de Spores", 67, Kind.Field, 40, 64328, 26803),
        Z("Bandit Stronghold", "Repaire des Bandits", 67, Kind.Fortress, 0, 87091, -20354),
        Z("Ivory Tower", "Tour d'Ivoire", 67, Kind.Town, 0, 85391, 16228),
        Z("Town of Oren", "Ville d'Oren", 67, Kind.Town, 0, 82971, 53207),
        Z("Oren Castle", "Ch\u00e2teau d'Oren", 67, Kind.Town, 0, 78189, 36936),
        Z("Plains of the Lizardmen", "Plaines des Hommes-L\u00e9zards", 67, Kind.Field, 35, 87252, 85514),
        Z("Skyshadow Meadow", "Prairie d'Ombreciel", 67, Kind.Field, 54, 89914, 46276),
        Z("Shilen's Garden", "Jardin de Shilen", 67, Kind.Field, 1, 23863, 11068),
        Z("Black Rock Hill", "Colline de Roche Noire", 67, Kind.Field, 13, -29466, 66678),
        Z("Spider Nest", "Nid d'Araign\u00e9es", 67, Kind.Field, 16, -61095, 75104),
        Z("Timak Outpost", "Avant-poste de Timak", 67, Kind.Field, 40, 67097, 68815),
        Z("Ivory Tower Crater", "Crat\u00e8re de la Tour d'Ivoire", 67, Kind.Field, 40, 85391, 16228),
        Z("Forest of Evil", "For\u00eat du Mal", 67, Kind.Field, 45, 93218, 16969),
        Z("Outlaw Forest", "For\u00eat des Hors-la-loi", 67, Kind.Field, 50, 91539, -12204),
        Z("Misty Mountains", "Montagnes Brumeuses", 67, Kind.Landmark, 0, 61740, 94946),
        Z("Starlight Waterfall", "Cascade des \u00c9toiles", 67, Kind.Landmark, 0, 58502, 53453),
        Z("Undine Waterfall", "Cascade des Ondines", 67, Kind.Landmark, 0, -7233, 57006),
        Z("The Gods' Falls", "Chutes des Dieux", 67, Kind.Landmark, 0, 70456, 6591),
        Z("Tower of Insolence", "Tour de l'Insolence", 97, Kind.Dungeon, 60, 114649, 11115),
        Z("Blazing Swamp", "Marais Ardent", 97, Kind.Field, 65, 159455, -12931),
        Z("Devastated Castle", "Ch\u00e2teau D\u00e9vast\u00e9", 97, Kind.Fortress, 0, 178358, -14192),
        Z("The Forbidden Gateway", "Porte Interdite", 97, Kind.Field, 60, 185319, 20218),
        Z("The Giant's Cave", "Caverne des G\u00e9ants", 97, Kind.Dungeon, 55, 181737, 46469),
        Z("The Enchanted Valley", "Vall\u00e9e Enchant\u00e9e", 97, Kind.Field, 45, 124904, 61992),
        Z("The Cemetery", "Le Cimeti\u00e8re", 97, Kind.Field, 50, 167047, 20304),
        Z("The Forest of Mirrors", "For\u00eat des Miroirs", 97, Kind.Field, 40, 142065, 81300),
        Z("Anghel Waterfall", "Cascade d'Anghel", 97, Kind.Landmark, 0, 166304, 91741),
        Z("Town of Aden", "Ville d'Aden", 97, Kind.Town, 0, 146783, 25808),
        Z("Hunters Village", "Village des Chasseurs", 97, Kind.Town, 0, 117088, 76931),
        Z("Eastern Border Outpost", "Avant-poste de la Fronti\u00e8re Est", 97, Kind.Landmark, 0, 158141, -24543),
        Z("Coliseum", "Colis\u00e9e", 97, Kind.Arena, 0, 146440, 46723),
        Z("Narsell Lake", "Lac de Narsell", 97, Kind.Landmark, 0, 146440, 46723),
        Z("Aden Castle", "Ch\u00e2teau d'Aden", 97, Kind.Town, 0, 147461, 9898),
        Z("Ancient Battleground", "Ancien Champ de Bataille", 97, Kind.Field, 64, 106517, -2871),
        Z("Forsaken Plains", "Plaines Abandonn\u00e9es", 97, Kind.Field, 55, 167285, 37109),
        Z("Silent Valley", "Vall\u00e9e du Silence", 97, Kind.Field, 74, 170838, 55776),
        Z("Hunters Valley", "Vall\u00e9e des Chasseurs", 97, Kind.Field, 40, 114306, 86573),
        Z("Plains of Glory", "Plaines de la Gloire", 97, Kind.Field, 45, 135580, 19467),
        Z("Fields of Massacre", "Champs du Massacre", 97, Kind.Field, 55, 183543, -14974),
        Z("War-Torn Plains", "Plaines Ravag\u00e9es", 97, Kind.Field, 45, 156898, 11217),
        Z("Western Border Outpost", "Avant-poste de la Fronti\u00e8re Ouest", 97, Kind.Landmark, 0, 112405, -16607),
        Z("Field of Silence", "Champ du Silence", 121, Kind.Field, 35, 91088, 182384),
        Z("Field of Whispers", "Champ des Murmures", 121, Kind.Field, 35, 74592, 207656),
        Z("Garden of Eva", "Jardin d'Eva", 121, Kind.Dungeon, 40, 84413, 234334),
        Z("Alligator Island", "\u00cele des Alligators", 121, Kind.Field, 40, 115583, 192261),
        Z("Heine", "Heine", 121, Kind.Town, 0, 111455, 219400),
        Z("Innadril Castle", "Ch\u00e2teau d'Innadril", 121, Kind.Town, 0, 115988, 246899),
        Z("Alligator Beach", "Plage des Alligators", 121, Kind.Field, 38, 116267, 201177),
        Z("Dion Arena", "Ar\u00e8ne de Dion", 1, Kind.Arena, 0, 12443, 183467),
        Z("The Pa'agrio Temple", "Temple de Pa'agrio", 138, Kind.Town, 0, -55699, -114967),
        Z("Orc Village", "Village Orque", 138, Kind.Town, 0, -45158, -112583),
        Z("The Immortal Plateau", "Plateau des Immortels", 138, Kind.Landmark, 0, -8830, -119289),
        Z("Cave of Trials", "Caverne des \u00c9preuves", 138, Kind.Dungeon, 10, 9340, -112509),
        Z("Frozen Waterfalls", "Cascades Gel\u00e9es", 138, Kind.Landmark, 0, 11810, -139730),
        Z("Valley of Heroes", "Vall\u00e9e des H\u00e9ros", 138, Kind.Field, 1, -39347, -107274),
        Z("Immortal Plateau, Northern Region", "Plateau des Immortels, R\u00e9gion Nord", 138, Kind.Field, 8, -9465, -134046),
        Z("Immortal Plateau, Southern Region", "Plateau des Immortels, R\u00e9gion Sud", 138, Kind.Field, 18, -4190, -80040),
        Z("Strip Mine", "Mine \u00e0 Ciel Ouvert", 138, Kind.Landmark, 0, 106561, -173949),
        Z("Dwarven Village", "Village Nain", 138, Kind.Town, 0, 115120, -178224),
        Z("Spine Mountains", "Montagnes de l'\u00c9chine", 138, Kind.Landmark, 0, 147493, -200840),
        Z("Abandoned Coal Mines", "Mines de Charbon Abandonn\u00e9es", 138, Kind.Dungeon, 10, 139714, -177456),
        Z("Mithril Mines", "Mines de Mithril", 138, Kind.Dungeon, 23, 171946, -173352),
        Z("Frozen Valley", "Vall\u00e9e Gel\u00e9e", 138, Kind.Field, 1, 112971, -174924),
        Z("Western Mining Zone", "Zone Mini\u00e8re Ouest", 138, Kind.Field, 8, 128527, -204036),
        Z("Eastern Mining Zone", "Zone Mini\u00e8re Est", 138, Kind.Field, 18, 175836, -205837),
        Z("Mining Zone Passage", "Passage des Mines", 138, Kind.Landmark, 0, 113826, -171150),
        Z("Plunderous Plains", "Plaines du Pillage", 138, Kind.Field, 30, 115298, -160886),
        Z("Frozen Labyrinth", "Labyrinthe Gel\u00e9", 138, Kind.Field, 53, 123037, -118112),
        Z("The Ice Queen's Castle", "Ch\u00e2teau de la Reine des Glaces", 138, Kind.Field, 57, 102728, -126242),
        Z("Pavel Ruins", "Ruines de Pavel", 138, Kind.Field, 46, 91129, -123951),
        Z("Caron's Dungeon", "Donjon de Caron", 138, Kind.Landmark, 0, 76021, -110477),
        Z("Den of Evil", "Antre du Mal", 138, Kind.Field, 40, 68693, -110438),
        Z("Crypts of Disgrace", "Cryptes de la Disgr\u00e2ce", 138, Kind.Field, 25, 47692, -115745),
        Z("Valley of the Lords", "Vall\u00e9e des Seigneurs", 138, Kind.Landmark, 0, 32173, -122954),
        Z("Schuttgart Castle", "Ch\u00e2teau de Schuttgart", 138, Kind.Town, 0, 77630, -150885),
        Z("Town of Schuttgart", "Ville de Schuttgart", 138, Kind.Town, 0, 88249, -142713),
        Z("Archaic Laboratory", "Laboratoire Archa\u00efque", 138, Kind.Dungeon, 50, 90418, -107317),
        Z("Frost Lake", "Lac Gel\u00e9", 138, Kind.Landmark, 0, 108251, -120886),
        Z("Sky Wagon Relic", "\u00c9pave du Chariot C\u00e9leste", 138, Kind.Field, 39, 121618, -141554),
        Z("Necropolis of Sacrifice", "N\u00e9cropole du Sacrifice", 30, Kind.Dungeon, 21, -41184, 206752),
        Z("Necropolis of Devotion", "N\u00e9cropole de la D\u00e9votion", 67, Kind.Dungeon, 60, -56064, 78720),
        Z("The Patriot's Necropolis", "N\u00e9cropole du Patriote", 30, Kind.Dungeon, 52, -25472, 77728),
        Z("Catacomb of Dark Omens", "Catacombe des Sombres Pr\u00e9sages", 67, Kind.Dungeon, 72, -22480, 13872),
        Z("Catacomb of the Branded", "Catacombe des Marqu\u00e9s", 16, Kind.Dungeon, 42, 43200, 170688),
        Z("Catacomb of the Heretic", "Catacombe de l'H\u00e9r\u00e9tique", 1, Kind.Dungeon, 30, 39232, 143568),
        Z("The Pilgrim's Necropolis", "N\u00e9cropole du P\u00e8lerin", 1, Kind.Dungeon, 32, 45600, 126944),
        Z("The Saint's Necropolis", "N\u00e9cropole du Saint", 121, Kind.Dungeon, 70, 79296, 209584),
        Z("Necropolis of Worship", "N\u00e9cropole du Culte", 121, Kind.Dungeon, 42, 107514, 174329),
        Z("Necropolis of Martyrdom", "N\u00e9cropole du Martyre", 16, Kind.Dungeon, 65, 114496, 132416),
        Z("Catacomb of the Apostate", "Catacombe de l'Apostat", 67, Kind.Dungeon, 51, 74672, 78032),
        Z("Catacomb of the Forbidden Path", "Catacombe du Chemin Interdit", 97, Kind.Dungeon, 72, 110912, 84912),
        Z("Catacomb of the Witch", "Catacombe de la Sorci\u00e8re", 97, Kind.Dungeon, 60, 136672, 79328),
        Z("The Disciple's Necropolis", "N\u00e9cropole du Disciple", 97, Kind.Dungeon, 70, 168560, -17968),
        Z("Oracle of Dawn", "Oracle de l'Aube", 179, Kind.Dungeon, 0, -79184, 111952),
        Z("Oracle of Dusk", "Oracle du Cr\u00e9puscule", 179, Kind.Dungeon, 0, -80208, 87120),
        Z("Dimensional Rift", "Faille Dimensionnelle", 179, Kind.Dungeon, 30, 0, 0),
        Z("Olympiad Stadium", "Stade de l'Olympiade", 179, Kind.Arena, 0, -21453, -21058),
        Z("Town  of Goddard", "Ville de Goddard", 180, Kind.Town, 0, 148024, -55281),
        Z("Goddard Castle", "Ch\u00e2teau de Goddard", 180, Kind.Town, 0, 147450, -47331),
        Z("Garden of Beasts", "Jardin des B\u00eates", 180, Kind.Field, 67, 132997, -60608),
        Z("Hot Springs", "Sources Chaudes", 180, Kind.Field, 73, 151778, -106829),
        Z("Rainbow Springs Chateau", "Ch\u00e2teau des Sources Arc-en-ciel", 180, Kind.Fortress, 0, 139997, -124860),
        Z("Forge of the Gods", "Forge des Dieux", 180, Kind.Dungeon, 78, 168902, -116703),
        Z("Hall of Flames", "Salle des Flammes", 180, Kind.Dungeon, 80, 189964, -116820),
        Z("Valakas' Lair", "Antre de Valakas", 180, Kind.Dungeon, 85, 215378, -116635),
        Z("Ketra Orc Outpost", "Avant-poste des Orques Ketra", 180, Kind.Field, 77, 146990, -67128),
        Z("Ketra Orc Village", "Village des Orques Ketra", 180, Kind.Town, 0, 149548, -82014),
        Z("Imperial Tomb", "Tombeau Imp\u00e9rial", 180, Kind.Dungeon, 76, 186699, -75915),
        Z("Pilgrim's Temple", "Temple du P\u00e8lerin", 180, Kind.Landmark, 0, 168982, -86455),
        Z("Wall of Argos", "Mur d'Argos", 180, Kind.Field, 68, 165054, -47861),
        Z("Shrine of Loyalty", "Sanctuaire de la Loyaut\u00e9", 180, Kind.Field, 73, 190112, -61776),
        Z("Varka Silenos Barracks", "Caserne des Varka Silenos", 180, Kind.Field, 77, 125740, -40864),
        Z("Varka Silenos Village", "Village des Varka Silenos", 180, Kind.Town, 0, 108155, -53670),
        Z("Four Sepulchers", "Quatre S\u00e9pulcres", 180, Kind.Dungeon, 83, 178127, -84435),
        Z("Devil's Pass", "Passe du Diable", 180, Kind.Field, 60, 102621, -60798),
        Z("The Last Imperial Tomb", "Dernier Tombeau Imp\u00e9rial", 180, Kind.Dungeon, 80, 174141, -88685),
        Z("Rune Township", "Ville de Rune", 200, Kind.Town, 0, 43835, -47749),
        Z("Rune Castle", "Ch\u00e2teau de Rune", 200, Kind.Town, 0, 14659, -49230),
        Z("Rune Harbor", "Port de Rune", 200, Kind.Harbor, 0, 36839, -38435),
        Z("Windtail Waterfall", "Cascade de Ventqueue", 200, Kind.Landmark, 0, 40723, -94881),
        Z("Beast Farm", "Ferme des B\u00eates", 200, Kind.Field, 63, 43805, -88010),
        Z("Wild Beast Reserve", "R\u00e9serve des B\u00eates Sauvages", 200, Kind.Fortress, 0, 55133, -93217),
        Z("Valley of Saints", "Vall\u00e9e des Saints", 200, Kind.Field, 60, 79981, -82301),
        Z("Forest of the Dead", "For\u00eat des Morts", 200, Kind.Field, 63, 52107, -54328),
        Z("Cursed Village", "Village Maudit", 200, Kind.Town, 0, 57670, -41672),
        Z("Fortress of the Dead", "Forteresse des Morts", 200, Kind.Fortress, 0, 57966, -28378),
        Z("Swamp of Screams", "Marais des Cris", 200, Kind.Field, 66, 69340, -50203),
        Z("Monastery of Silence", "Monast\u00e8re du Silence", 200, Kind.Dungeon, 80, 125480, -75834),
        Z("The Pagan Temple", "Temple Pa\u00efen", 200, Kind.Dungeon, 81, 35630, -49748),
        Z("Stakato Nest", "Nid des Stakatos", 200, Kind.Dungeon, 72, 90134, -45130),
        Z("Tour Boat Dock", "Embarcad\u00e8re", 121, Kind.Harbor, 0, 111418, 225960),
        Z("Ice Merchant Cabin", "Cabane du Marchand de Glace", 138, Kind.Landmark, 0, 113750, -109163),
        Z("Brigand Stronghold", "Repaire des Brigands", 138, Kind.Landmark, 0, 126272, -159336),
        Z("Primeval Isle", "\u00cele Primitive", 200, Kind.Field, 83, 8480, -14624),
        Z("Primeval Isle Wharf", "Quai de l'\u00cele Primitive", 200, Kind.Harbor, 0, 10448, -24960)
    };

    /// Libelle francais du type, pour l'interface.
    public static string Label(Kind type)
    {
        switch (type)
        {
            case Kind.Dungeon: return "Donjon";
            case Kind.Town: return "Ville";
            case Kind.Harbor: return "Port";
            case Kind.Fortress: return "Forteresse";
            case Kind.Arena: return "Ar00e8ne";
            case Kind.Landmark: return "Lieu";
            default: return "Zone de chasse";
        }
    }

    /// Lieu le plus proche d'une position : le fichier du client ne donne
    /// aucune emprise, seulement un point par zone.
    public static string NearestName(Vector3 world)
    {
        float best = float.MaxValue;
        string name = string.Empty;

        for (int i = 0; i < Zones.Length; i++)
        {
            float dx = Zones[i].World.x - world.x;
            float dz = Zones[i].World.z - world.z;
            float distance = dx * dx + dz * dz;

            if (distance < best)
            {
                best = distance;
                name = Zones[i].French;
            }
        }

        return name;
    }

    public static string TerritoryName(int id)
    {
        for (int i = 0; i < Territories.Length; i++)
        {
            if (Territories[i].Id == id)
            {
                return Territories[i].French;
            }
        }

        return string.Empty;
    }
}
