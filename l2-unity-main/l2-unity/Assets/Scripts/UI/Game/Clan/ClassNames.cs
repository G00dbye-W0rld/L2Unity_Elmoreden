// Noms francais des classes d'Interlude, par identifiant de classe du serveur.
public static class ClassNames
{
    private static readonly System.Collections.Generic.Dictionary<int, string> _names = new System.Collections.Generic.Dictionary<int, string>
    {
        { 0, "Combattant humain" }, { 1, "Guerrier" }, { 2, "Gladiateur" }, { 3, "Seigneur de guerre" },
        { 4, "Chevalier" }, { 5, "Paladin" }, { 6, "Vengeur noir" }, { 7, "Voleur" },
        { 8, "Chasseur de tr\u00e9sors" }, { 9, "\u0152il de faucon" }, { 10, "Mage humain" }, { 11, "Sorcier" },
        { 12, "Ensorceleur" }, { 13, "N\u00e9cromancien" }, { 14, "D\u00e9moniste" }, { 15, "Clerc" },
        { 16, "\u00c9v\u00eaque" }, { 17, "Proph\u00e8te" }, { 18, "Combattant elfe" }, { 19, "Chevalier elfe" },
        { 20, "Chevalier du temple" }, { 21, "Chanteur d'\u00e9p\u00e9e" }, { 22, "\u00c9claireur elfe" }, { 23, "Arpenteur des plaines" },
        { 24, "R\u00f4deur d'argent" }, { 25, "Mage elfe" }, { 26, "Sorcier elfe" }, { 27, "Chanteur de sorts" },
        { 28, "Invocateur \u00e9l\u00e9mentaire" }, { 29, "Oracle" }, { 30, "Ancien" }, { 31, "Combattant elfe noir" },
        { 32, "Chevalier de Palus" }, { 33, "Chevalier de Shilen" }, { 34, "Danseur des lames" }, { 35, "Assassin" },
        { 36, "Marcheur des ab\u00eemes" }, { 37, "R\u00f4deur fant\u00f4me" }, { 38, "Mage elfe noir" }, { 39, "Sorcier elfe noir" },
        { 40, "Hurleur de sorts" }, { 41, "Invocateur fant\u00f4me" }, { 42, "Oracle de Shilen" }, { 43, "Ancien de Shilen" },
        { 44, "Combattant orc" }, { 45, "Pillard orc" }, { 46, "Destructeur" }, { 47, "Moine orc" },
        { 48, "Tyran" }, { 49, "Mage orc" }, { 50, "Chaman orc" }, { 51, "Suzerain" },
        { 52, "Crieur de guerre" }, { 53, "Combattant nain" }, { 54, "Charognard" }, { 55, "Chasseur de primes" },
        { 56, "Artisan" }, { 57, "Forgeron de guerre" },
        { 88, "Duelliste" }, { 89, "Cuirass\u00e9 de l'effroi" }, { 90, "Chevalier ph\u00e9nix" }, { 91, "Chevalier infernal" },
        { 92, "Sagittaire" }, { 93, "Aventurier" }, { 94, "Archimage" }, { 95, "Faucheur d'\u00e2mes" },
        { 96, "Seigneur des arcanes" }, { 97, "Cardinal" }, { 98, "Hi\u00e9rophante" }, { 99, "Templier d'Eva" },
        { 100, "Muse de l'\u00e9p\u00e9e" }, { 101, "Cavalier du vent" }, { 102, "Sentinelle du clair de lune" }, { 103, "Muse mystique" },
        { 104, "Ma\u00eetre \u00e9l\u00e9mentaire" }, { 105, "Sainte d'Eva" }, { 106, "Templier de Shilen" }, { 107, "Danseur spectral" },
        { 108, "Chasseur fant\u00f4me" }, { 109, "Sentinelle fant\u00f4me" }, { 110, "Hurleur de temp\u00eate" }, { 111, "Ma\u00eetre spectral" },
        { 112, "Saint de Shilen" }, { 113, "Titan" }, { 114, "Grand Khavatari" }, { 115, "Dominateur" },
        { 116, "Crieur de mort" }, { 117, "Chercheur de fortune" }, { 118, "Maestro" }
    };

    public static string Get(int classId)
    {
        string name;
        return _names.TryGetValue(classId, out name) ? name : "Classe " + classId;
    }
}
