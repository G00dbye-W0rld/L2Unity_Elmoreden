using UnityEngine;

/// Conversion entre une position monde et une region, sans rien avoir charge.
///
/// POURQUOI CETTE CLASSE
/// Geodata.GetCurrentMap balaie _mapsOrigin, qui ne contient que les regions
/// DEJA chargees. Elle repond donc "dans quelle region suis-je parmi celles en
/// memoire" - ce qui est inutilisable pour decider quoi charger : la region ou
/// va le joueur est justement celle qui n'est pas encore la.
///
/// La grille du monde est parfaitement reguliere. Verifie le 2026-08-12 sur des
/// regions aux quatre coins de la carte (16_10, 18_13, 24_25, 26_16), avec un
/// ecart nul a 0,001 unite pres :
///
///     nom de region = {colonne}_{rangee}
///     X = (rangee  - 22) * 624,1524 + 2494,1716
///     Z = (colonne - 21) * 624,1524 +  621,7143
///
/// Attention a l'ordre : le PREMIER nombre du nom porte le Z, le SECOND porte
/// le X. C'est contre-intuitif et c'est une source d'erreur classique.
public static class RegionGrid
{
    /// Cote d'une region, en unites Unity. Meme valeur que Geodata._mapSize.
    public const float RegionSize = 624.1524f;

    // Region de reference et sa position, d'ou tout le reste se deduit.
    // Exprimer les constantes ainsi plutot qu'en decalages bruts rend la
    // formule verifiable : on peut ouvrir 21_22 et comparer.
    private const int RefColumn = 21;
    private const int RefRow = 22;
    private const float RefX = 2494.1716f;
    private const float RefZ = 621.7143f;

    /// Coin de la region, c'est-a-dire son point de coordonnees minimales.
    /// C'est la convention qu'utilise deja Geodata.IsInMapBounds : une position
    /// appartient a la region si elle est comprise entre l'origine et
    /// l'origine + RegionSize.
    public static Vector2 OriginOf(int column, int row)
    {
        return new Vector2(
            (row - RefRow) * RegionSize + RefX,
            (column - RefColumn) * RegionSize + RefZ);
    }

    /// Region contenant cette position. Toujours definie, meme hors du monde
    /// connu : c'est a l'appelant de verifier que la region existe.
    public static void RegionAt(Vector3 world, out int column, out int row)
    {
        row = Mathf.FloorToInt((world.x - RefX) / RegionSize) + RefRow;
        column = Mathf.FloorToInt((world.z - RefZ) / RegionSize) + RefColumn;
    }

    /// Nom de region au format du projet, "colonne_rangee".
    public static string NameOf(int column, int row)
    {
        return $"{column}_{row}";
    }

    public static string NameAt(Vector3 world)
    {
        RegionAt(world, out int column, out int row);
        return NameOf(column, row);
    }

    /// Decompose un nom de region. Retourne false sur tout ce qui n'est pas
    /// "nombre_nombre" - le dossier Maps contient aussi l2_lobby.
    public static bool TryParse(string regionName, out int column, out int row)
    {
        column = 0;
        row = 0;

        if (string.IsNullOrEmpty(regionName))
        {
            return false;
        }

        int sep = regionName.IndexOf('_');
        return sep > 0
               && int.TryParse(regionName.Substring(0, sep), out column)
               && int.TryParse(regionName.Substring(sep + 1), out row);
    }

    /// Distance en nombre de regions, au sens de Tchebychev : le nombre de pas
    /// d'un roi sur un echiquier. C'est la bonne mesure pour une fenetre
    /// carree - une region en diagonale est a distance 1, pas 2.
    public static int Distance(int columnA, int rowA, int columnB, int rowB)
    {
        return Mathf.Max(Mathf.Abs(columnA - columnB), Mathf.Abs(rowA - rowB));
    }

    /// Distance en UNITES entre une position et le bord d'une region.
    /// Zero si la position est dedans.
    ///
    /// POURQUOI C'EST LA BONNE MESURE
    /// Une fenetre de forme fixe charge toujours les memes voisines, ou que le
    /// joueur se trouve dans sa region. C'est doublement mauvais : au centre on
    /// charge quatre voisines dont aucune n'est proche, et dans un angle il
    /// manque la diagonale - d'ou le vide visible au coin.
    ///
    /// En raisonnant en distance, la fenetre s'adapte d'elle-meme : au centre
    /// d'une region personne n'est proche, sur un bord une seule voisine l'est,
    /// dans un angle trois le sont. On charge donc moins en moyenne, tout en
    /// couvrant le cas qui manquait.
    public static float DistanceToRegion(Vector3 world, int column, int row)
    {
        Vector2 origin = OriginOf(column, row);

        // Ecart a l'intervalle : nul si la coordonnee tombe dedans.
        float dx = Mathf.Max(origin.x - world.x, 0f, world.x - (origin.x + RegionSize));
        float dz = Mathf.Max(origin.y - world.z, 0f, world.z - (origin.y + RegionSize));

        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}
