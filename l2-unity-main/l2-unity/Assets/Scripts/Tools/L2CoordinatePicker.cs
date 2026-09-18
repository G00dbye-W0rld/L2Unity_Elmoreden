#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Releve de coordonnees serveur depuis la scene, sans lancer le jeu.
// Maj + clic dans la vue Scene capture un point ; la fenetre rend le bloc
// XML d'une zone (salle de clan, territoire) pret a coller.
public class L2CoordinatePicker : EditorWindow
{
    private const float Scale = 52.5f;

    private readonly List<Vector3> _points = new List<Vector3>();
    private Vector3 _hover;
    private bool _hasHover;
    private string _zoneName = "talking_agit_001";
    private int _clanHallId = 65;
    private int _ceiling = 400;
    private Vector2 _scroll;

    [MenuItem("L2/Debug/Coordonnees L2 (releve)", false, 930)]
    public static void Open()
    {
        GetWindow<L2CoordinatePicker>("Coordonnees L2").Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    // Le serveur est en unites Unreal : Unity = (y, z, x) / 52.5.
    public static Vector3Int ToServer(Vector3 unity)
    {
        return new Vector3Int(
            Mathf.RoundToInt(unity.z * Scale),
            Mathf.RoundToInt(unity.x * Scale),
            Mathf.RoundToInt(unity.y * Scale));
    }

    private void OnSceneGUI(SceneView view)
    {
        UnityEngine.Event e = UnityEngine.Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        RaycastHit hit;
        _hasHover = Physics.Raycast(ray, out hit, 5000f);
        if (_hasHover)
        {
            _hover = hit.point;
        }

        if (e.type == UnityEngine.EventType.MouseDown && e.button == 0 && e.shift && _hasHover)
        {
            _points.Add(_hover);
            e.Use();
            Repaint();
        }

        Handles.color = Color.cyan;
        for (int i = 0; i < _points.Count; i++)
        {
            Handles.SphereHandleCap(0, _points[i], Quaternion.identity, 0.6f, UnityEngine.EventType.Repaint);
            Handles.Label(_points[i] + Vector3.up, (i + 1) + " : " + ToServer(_points[i]));

            if (i > 0)
            {
                Handles.DrawLine(_points[i - 1], _points[i]);
            }
        }

        if (_points.Count > 2)
        {
            Handles.DrawLine(_points[_points.Count - 1], _points[0]);
        }

        view.Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Maj + clic gauche dans la vue Sc\u00e8ne : capture un point. Les coins se relevent le long des murs exterieurs.", MessageType.Info);

        _zoneName = EditorGUILayout.TextField("Nom de zone", _zoneName);
        _clanHallId = EditorGUILayout.IntField("Id de salle", _clanHallId);
        _ceiling = EditorGUILayout.IntField("Hauteur int\u00e9rieure", _ceiling);

        GameObject selection = Selection.activeGameObject;
        EditorGUILayout.LabelField("S\u00e9lection", selection != null ? selection.name + "  " + ToServer(selection.transform.position) : "aucune");
        EditorGUILayout.LabelField("Sous le curseur", _hasHover ? ToServer(_hover).ToString() : "-");

        EditorGUILayout.Space();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < _points.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField((i + 1) + " : " + ToServer(_points[i]));
            if (GUILayout.Button("Retirer", GUILayout.Width(70)))
            {
                _points.RemoveAt(i);
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Copier la zone"))
        {
            EditorGUIUtility.systemCopyBuffer = BuildZone();
            Debug.Log(BuildZone());
        }
        if (GUILayout.Button("Copier les points"))
        {
            EditorGUIUtility.systemCopyBuffer = BuildPoints();
        }
        if (GUILayout.Button("Vider"))
        {
            _points.Clear();
        }
        EditorGUILayout.EndHorizontal();
    }

    private string BuildZone()
    {
        if (_points.Count < 3)
        {
            return "Il faut au moins trois points.";
        }

        int floor = int.MaxValue;
        foreach (Vector3 point in _points)
        {
            floor = Mathf.Min(floor, ToServer(point).z);
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("\t<zone name=\"" + _zoneName + "\" shape=\"NPoly\" minZ=\"" + (floor - 40) + "\" maxZ=\"" + (floor + _ceiling) + "\">");
        sb.AppendLine("\t\t<stat name=\"clanHallId\" val=\"" + _clanHallId + "\"/>");
        foreach (Vector3 point in _points)
        {
            Vector3Int p = ToServer(point);
            sb.AppendLine("\t\t<node x=\"" + p.x + "\" y=\"" + p.y + "\"/>");
        }
        sb.AppendLine("\t</zone>");
        return sb.ToString();
    }

    private string BuildPoints()
    {
        StringBuilder sb = new StringBuilder();
        foreach (Vector3 point in _points)
        {
            Vector3Int p = ToServer(point);
            sb.AppendLine(p.x + " " + p.y + " " + p.z);
        }
        return sb.ToString();
    }
}
#endif
