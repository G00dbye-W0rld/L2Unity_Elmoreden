#if (UNITY_EDITOR) 
[System.Serializable]
public class Poly {
    public string name;
    public int polyCount;
    public PolyData[] polyData;
    public override string ToString() {
        // polyData reste nul quand le .t3d decrit un Brush sans bloc Polygon
        // (geometrie stockee dans un objet Model separe). L'ancien foreach
        // levait alors une NullReferenceException depuis un simple Debug.Log,
        // ce qui interrompait tout l'import des brushes pour un message.
        if (polyData == null) {
            return $"Name: {name}, polyCount: {polyCount}, polyData: <aucun>";
        }

        string polyDataString = "";
        foreach (var poly in polyData) {
            // Etait une affectation et non une concatenation : seul le dernier
            // polygone apparaissait dans la trace.
            polyDataString += poly.ToString() + " ";
        }
        return $"Name: {name}, polyCount: {polyCount}, polyData: {polyDataString}";
    }
}
#endif