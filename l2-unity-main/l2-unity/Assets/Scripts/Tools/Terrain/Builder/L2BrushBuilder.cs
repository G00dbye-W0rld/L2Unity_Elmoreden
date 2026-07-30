#if (UNITY_EDITOR) 
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

public class L2BrushBuilder
{
    [MenuItem("Shnok/[Debug][Brush] (JSON) Build brushes")]
    static void ImportBrushTextures()
    {
        string title = "Select Brush list";
        string directory = Path.Combine(Application.dataPath, "Resources/Data/Maps");
        string extension = "json";

        string fileToProcess = EditorUtility.OpenFilePanel(title, directory, extension);

        if (!string.IsNullOrEmpty(fileToProcess))
        {
            Debug.Log("Selected file: " + fileToProcess);

            Brush[] brushes = L2JSONBrushImporter.ParseBrushFile(fileToProcess);

            Build(brushes);
        }
    }

    [MenuItem("Shnok/07. [Brush] (T3D) Build brushes")]
    static void ImportBrushTexturesT3D()
    {
        string title = "Select T3D file";
        string directory = Path.Combine(Application.dataPath, "Resources/Data/Maps");
        string extension = "t3d";

        string fileToProcess = EditorUtility.OpenFilePanel(title, directory, extension);

        if (!string.IsNullOrEmpty(fileToProcess))
        {
            Debug.Log("Selected file: " + fileToProcess);
            BuildBrushesFrom(fileToProcess);
        }
    }

    /// Etape 07 sans dialogue. Voir L2MapBatchImporter.
    public static void BuildBrushesFrom(string fileToProcess)
    {
        Brush[] brushes = L2T3DInfoParser.ParseBrushInfo(fileToProcess).ToArray();

        // Selon l'outil qui a produit le .t3d, la geometrie des brushes est
        // soit ecrite inline (blocs "Begin Polygon"), soit laissee dans un
        // objet Model separe et seulement referencee - auquel cas le
        // parser ne recupere que des coquilles vides. Dans ce second cas
        // on bascule sur le Brushes.json depose a cote par
        // l2-brush-export, qui contient bien les polygones.
        bool hasGeometry = false;
        foreach (Brush b in brushes)
        {
            if (b.model != null && b.model.poly != null && b.model.poly.polyData != null
                && b.model.poly.polyData.Length > 0)
            {
                hasGeometry = true;
                break;
            }
        }

        if (!hasGeometry)
        {
            // Le .t3d peut etre choisi soit dans le projet, soit dans le
            // dossier de travail des maps, ou le fichier de brushes porte
            // le nom de la region ("17_23.json") et non "Brushes.json".
            // On accepte les trois emplacements plutot que d'exiger que
            // l'utilisateur devine lequel designer.
            string mapName = Path.GetFileNameWithoutExtension(fileToProcess);
            string beside = Path.GetDirectoryName(fileToProcess);

            string[] candidates =
            {
                Path.Combine(beside, "Brushes.json"),
                Path.Combine(beside, mapName + ".json"),
                Path.Combine(Application.dataPath, "Resources/Data/Maps", mapName, "Meta", "Brushes.json"),
            };

            string json = null;
            foreach (string c in candidates)
            {
                if (File.Exists(c)) { json = c; break; }
            }

            if (json == null)
            {
                Debug.LogError("[Brush] Le .t3d ne contient aucun polygone et aucun fichier de brushes "
                               + "n'a ete trouve. Cherche : " + string.Join(" | ", candidates));
                return;
            }

            Debug.Log($"[Brush] Le .t3d ne contient aucun polygone, bascule sur {json}");
            brushes = L2JSONBrushImporter.ParseBrushFile(json);
        }

        Build(brushes);
    }

    static void Build(Brush[] brushes)
    {
        if (brushes == null)
        {
            Debug.LogError("[Brush] Aucun brush lu, rien a construire.");
            return;
        }

        // Comme pour les static meshes : sans suppression prealable, relancer
        // l'etape empilait un second conteneur "Brushes" sur le premier.
        int removed = L2TerrainGenerator.DestroyContainers("Brushes");
        if (removed > 0)
        {
            Debug.Log($"[Brush] {removed} conteneur(s) precedent(s) supprime(s) avant regeneration.");
        }

        GameObject brushContainer = new GameObject("Brushes");

        foreach (Brush b in brushes)
        {
            if (b.position == Vector3.zero)
            {
                Debug.LogWarning(b.name + " position is null");
                continue;
            }
            if (b.polyFlags != null)
            {
                List<string> polyFlags = new List<string>(b.polyFlags);
                if (polyFlags.Contains("PF_Invisible"))
                {
                    continue;
                }
                if (polyFlags.Contains("PF_NotSolid"))
                {
                    continue;
                }
            }

            GameObject brush = new GameObject(b.name);
            brush.transform.parent = brushContainer.transform;
            brush.transform.position = VectorUtils.ConvertPosToUnity(b.position) - VectorUtils.ConvertPosToUnity(b.prePivot);

            Debug.Log(b.name);
            Model model = b.model;
            Poly poly = model.poly;

            for (int i = 0; i < poly.polyData.Length; i++)
            {
                PolyData polyData = poly.polyData[i];

                // Adjust verticles
                for (int p = 0; p < polyData.vertices.Length; p++)
                {
                    polyData.vertices[p] = VectorUtils.ConvertPosToUnity(polyData.vertices[p]);
                }

                List<string> pPolyFlags = new List<string>(polyData.polyFlags);

                // Skip invisible faces
                if (pPolyFlags.Contains("PF_Unlit") || pPolyFlags.Contains("PF_Invisible") || pPolyFlags.Contains("PF_NotSolid"))
                {
                    continue;
                }

                if (b.csgOper == "CSG_Subtract")
                {
                    // Only draw bottom face
                    // if (i != poly.polyData.Length - 1) {
                    //continue;
                    //}
                }

                //GameObject mesh = createMesh(b.csgOper, polyData);

                GameObject mesh = createProbuilderMesh(b.csgOper, polyData, i);
                mesh.transform.parent = brush.transform;
                mesh.transform.localPosition = Vector3.zero;
            }
        }

    }

    static GameObject createProbuilderMesh(string csgOper, PolyData polyData, int index)
    {

        Material material = GetMaterialForTexture(polyData.texture);

        Vector3 adjustedNormal = VectorUtils.ConvertToUnityUnscaled(polyData.normal);
        Vector3 adjustedU = VectorUtils.ConvertToUnityUnscaled(polyData.textureU);
        Vector3 adjustedV = VectorUtils.ConvertToUnityUnscaled(polyData.textureV);

        Quaternion rt = Quaternion.FromToRotation(Vector3.forward, adjustedNormal);
        Vector3 rotatedU = rt * adjustedU;
        Vector3 rotatedV = rt * adjustedV;
        if (adjustedNormal.y < 0)
        {
            rotatedU.x = -rotatedU.x;
        }
        else
        {
            rotatedU.y = -rotatedU.y;
        }

        // Create Vertices
        List<Vertex> vertexList = new List<Vertex>();
        foreach (var v in polyData.vertices)
        {
            Vertex vertex = new Vertex();
            vertex.position = v;
            vertexList.Add(vertex);
        }

        // Create faces
        Face[] faces = new Face[1];
        Face face = new Face(GenerateTris(csgOper, polyData));

        AutoUnwrapSettings aus = new AutoUnwrapSettings
        {
            anchor = AutoUnwrapSettings.Anchor.UpperLeft,
            scale = Vector2.one,
            offset = Vector2.zero,
            rotation = 0,
            fill = AutoUnwrapSettings.Fill.Tile,
            useWorldSpace = false
        };
        face.uv = aus;
        face.manualUV = true;
        faces[0] = face;

        // Create empty sharedVertices and sharedTextures lists
        List<SharedVertex> sharedVertices = new List<SharedVertex>();
        List<SharedVertex> sharedTextures = new List<SharedVertex>();

        // Create Mesh
        ProBuilderMesh pbm = ProBuilderMesh.Create(
            vertexList,
            faces,
            sharedVertices,
            sharedTextures
        );

        List<Vector4> uvs = new List<Vector4>();
        for (int i = 0; i < vertexList.Count; i++)
        {
            uvs.Add(new Vector4(rotatedU.x, rotatedU.y, rotatedV.x, rotatedV.y));
        }
        pbm.SetUVs(0, uvs);
        pbm.Refresh();

        Material[] sharedMaterials = pbm.GetComponent<Renderer>().sharedMaterials;
        sharedMaterials[0] = material;
        pbm.GetComponent<Renderer>().sharedMaterials = sharedMaterials;

        pbm.ToMesh();
        pbm.Refresh();
        pbm.RebuildWithPositionsAndFaces(polyData.vertices, faces);

        pbm.gameObject.transform.name = index.ToString();
        return pbm.gameObject;
    }

    private static int[] GenerateTris(string csgOper, PolyData polyData)
    {
        int[] triangles;

        if (csgOper == "CSG_Subtract")
        {
            if (polyData.vertices.Length == 4)
            {
                triangles = new int[6]
                {
                    2, 1, 0, // First triangle (top-left, top-right, bottom-left)
                    0, 3, 2  // Second triangle (bottom-left, top-right, bottom-right)
                };
            }
            else
            {
                triangles = new int[3]
                {
                    2, 1, 0, // First triangle (top-left, top-right, bottom-left)
                };
            }
        }
        else
        {
            if (polyData.vertices.Length == 4)
            {
                triangles = new int[6]
                {
                    0, 1, 2, // First triangle (top-left, top-right, bottom-left)
                    2, 3, 0  // Second triangle (bottom-left, top-right, bottom-right)
                };
            }
            else
            {
                triangles = new int[3]
                {
                    0, 1, 2, // First triangle (top-left, top-right, bottom-left)
                };
            }
        }

        return triangles;
    }

    private static Material GetMaterialForTexture(string texture)
    {
        string materialPath = TextureUtils.GetMaterialPath(texture);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefab/Red.mat");
            Debug.LogError("Missing material for " + texture);
        }

        return material;
    }
}
#endif