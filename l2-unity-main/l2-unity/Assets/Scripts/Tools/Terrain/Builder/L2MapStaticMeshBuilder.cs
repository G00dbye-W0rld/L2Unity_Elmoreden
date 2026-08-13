using System.IO;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
public class L2MapStaticMeshBuilder : MonoBehaviour
{
    [MenuItem("L2/Debug/StaticMeshes - (JSON) Build static meshes", false, 900)]
    static void BuildSoundsMenu()
    {
        string title = "Select ambient sound list";
        string directory = Path.Combine(Application.dataPath, "Resources/Data/Maps");
        string extension = "json";

        string fileToProcess = EditorUtility.OpenFilePanel(title, directory, extension);

        if (!string.IsNullOrEmpty(fileToProcess))
        {
            Debug.Log("Selected file: " + fileToProcess);
            L2JSONStaticMeshActorImporter meshActorParser = new L2JSONStaticMeshActorImporter();
            L2StaticMeshActor data = meshActorParser.GetL2StaticMeshActorFromFile(fileToProcess);
            GenerateStaticMeshes(data);
        }
    }

    public static void GenerateStaticMeshes(L2StaticMeshActor staticMeshActor)
    {
        float ueToUnityUnitScale = 1 / 52.5f;
        Vector3 basePosition = new Vector3(staticMeshActor.y, staticMeshActor.z, staticMeshActor.x) * ueToUnityUnitScale;
        GameObject staticMeshesGo = new GameObject("StaticMeshes");
        GameObject container = new GameObject("StaticMeshes");
        staticMeshesGo.transform.parent = container.transform;

        foreach (var staticMesh in staticMeshActor.staticMeshes)
        {
            BuildSingleStaticMesh(staticMesh, container);
        }
    }

    public static void BuildSingleStaticMesh(L2StaticMesh staticMesh, GameObject container)
    {
        // Filet de securite : le parseur ecarte deja les acteurs sans mesh
        // (vestiges bDeleteMe des .unr officiels), mais un seul null arrivant
        // jusqu'ici levait une NullReferenceException qui faisait echouer
        // l'import de la region entiere - constate sur 16_21 le 01/08/2026.
        // Sur 153 regions a importer, aucun acteur isole ne doit pouvoir
        // couter une region complete.
        if (staticMesh == null || string.IsNullOrEmpty(staticMesh.staticMesh))
        {
            return;
        }

        Vector3 basePosition = Vector3.zero;
        string meshPath = StaticMeshUtils.GetMeshPath(staticMesh.staticMesh);
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(meshPath);
        if (go != null)
        {
            Vector3 position = new Vector3(staticMesh.y, staticMesh.z, staticMesh.x) * (1 / 52.5f) * L2TerrainGeneratorTool.MAP_SCALE;
            Vector3 eulerAngles = go.transform.eulerAngles + VectorUtils.ConvertRotToUnity(staticMesh.eulerAngles);

            // Correction empirique (constatee visuellement sur 17_22/17_23,
            // 2026-07-30) : tous les static meshes ressortaient tournes de 90
            // par rapport a leur orientation reelle dans le client. Le calcul
            // Pitch/Yaw/Roll -> Unity ci-dessus est interne coherent (il
            // reproduit fidelement les valeurs du .t3d), mais cela ne prouve
            // pas sa conformite a l'orientation reelle du client - seule
            // l'observation visuelle directe le permet, et c'est elle qui a
            // revele l'ecart. Applique uniquement ici : les cameras
            // (L2CameraBuilder) utilisent la meme fonction
            // VectorUtils.ConvertRotToUnity et n'ont pas ce defaut signale.
            eulerAngles.y += 90f;

            float meshDataScaleMultiplier = staticMesh.scale != 0 ? staticMesh.scale : 1f;
            float meshDataScaleX = staticMesh.scaleX != 0 ? staticMesh.scaleX : 1f;
            float meshDataScaleY = staticMesh.scaleY != 0 ? staticMesh.scaleY : 1f;
            float meshDataScaleZ = staticMesh.scaleZ != 0 ? staticMesh.scaleZ : 1f;
            Vector3 meshDataScale = new Vector3(meshDataScaleX, meshDataScaleY, meshDataScaleZ);

            GameObject instantiated = GameObject.Instantiate(go, position + basePosition, Quaternion.Euler(eulerAngles));
            instantiated.name = staticMesh.staticMesh;
            instantiated.transform.localScale = Vector3.Scale(instantiated.transform.localScale, meshDataScale) *
                meshDataScaleMultiplier *
                //ueToUnityUnitScale *
                L2TerrainGeneratorTool.MAP_SCALE;

            instantiated.transform.parent = container.transform;

            ApplyLayer(instantiated, staticMesh.staticMesh);
        }
        else
        {
            Debug.LogError("Can't find StaticMesh FBX " + staticMesh.staticMesh + " at path " + meshPath);
        }
    }

    // Layers du projet (ProjectSettings/TagManager.asset).
    private const int LayerTerrain = 3;
    private const int LayerStaticMesh = 7;

    /// Affecte le layer d'un objet importe, recursivement.
    ///
    /// Sans cela, tout arrivait sur le layer 0 (Default) et le
    /// GeodataGenerator - qui filtre par walkableMask / obstacleMask /
    /// allowWalkMask - ne voyait donc AUCUN de ces objets : la geodata client
    /// ignorait purement et simplement bâtiments et rochers.
    ///
    /// Regles relevees sur la region de reference 17_25 :
    ///   - 1015 objets sur 7 (StaticMesh)  -> le defaut
    ///   -   10 objets sur 3 (Terrain)     -> les ponts, surfaces marchables
    ///   -  149 objets sur 16 (Unwalkable) -> les "trunk", poses par AddTrunks
    ///     (pas ici : ils n'existent pas encore a ce stade)
    private static void ApplyLayer(GameObject instantiated, string meshName)
    {
        int layer = LayerStaticMesh;

        // Un pont doit rester marchable : sur le layer StaticMesh il serait
        // traite comme un obstacle et la geodata bloquerait la traversee.
        if (meshName != null && meshName.ToLower().Contains("bridge"))
        {
            layer = LayerTerrain;
        }

        SetLayerRecursively(instantiated, layer);
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
#endif