#if (UNITY_EDITOR)
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public abstract class AssetImporter {
    protected static void ImportFiles(string inputFolder, List<string> files, bool overwrite) {
        // Chemins des .fbx deposes, pour leur activer les colliders juste
        // apres (voir ConfigureMeshColliders).
        List<string> importedMeshes = new List<string>();

        foreach(var filePath in files) {
            string path = filePath;
            if(!File.Exists(path)) {
                string pathRectified = path.Replace(inputFolder, String.Empty);
                string[] split = pathRectified.Split("\\");
                path = Path.Combine(inputFolder, split[1], "Texture", split[2]);

                if(!File.Exists(path)) {
                    Debug.LogError("File " + path + " doesn't exist.");
                    continue;
                }
            }

            string relativePath = Path.GetRelativePath(inputFolder, path.Replace("\\Texture", string.Empty));
            string destination = null;
            if(Path.GetExtension(path).ToLower() == ".fbx") {
                destination = Path.Combine("Assets", "Resources", "Data", "StaticMeshes", relativePath);
                Debug.Log(destination);
            } else if(Path.GetExtension(path).ToLower() == ".png") {
                destination = Path.Combine("Assets", "Resources", "Data", "Textures", relativePath);
                Debug.Log(destination);
            } else if(Path.GetExtension(path).ToLower() == ".txt") {
                destination = Path.Combine("Assets", "Resources", "Data", "Textures", relativePath);
                Debug.Log(destination);
            }

            if(destination != null) {
                if(!Directory.Exists(Path.GetDirectoryName(destination))) {
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                }

                try {
                    File.Copy(path, destination, overwrite);
                } catch(IOException e) {
                    Debug.LogWarning(destination + " already exists or " + e.Message);
                }

                // On enregistre le .fbx meme si la copie a echoue parce qu'il
                // existait deja : son .meta peut dater d'un import precedent,
                // sans colliders, et doit etre corrige lui aussi.
                if(destination.ToLower().EndsWith(".fbx") && File.Exists(destination)) {
                    importedMeshes.Add(destination.Replace('\\', '/'));
                }
            }
        }

        ConfigureMeshColliders(importedMeshes);
    }

    /// Active la generation de colliders sur les modeles importes.
    ///
    /// Par defaut, Unity importe un FBX avec addCollider = false : les objets
    /// poses dans la scene n'ont alors AUCUN collider et le joueur traverse
    /// tout - murs, maisons, rochers. Les regions de reference (17_25 :
    /// 1115 MeshCollider pour 1154 MeshRenderer) les portent bien, c'est donc
    /// l'import FBX qui doit les produire, pas un ajout manuel apres coup.
    ///
    /// Applique aussi aux modeles deja presents dont le .meta date d'un import
    /// precedent sans colliders - sinon une region reimportee resterait
    /// traversable sans aucun signal.
    protected static void ConfigureMeshColliders(List<string> meshPaths) {
        if(meshPaths == null || meshPaths.Count == 0) {
            return;
        }

        AssetDatabase.Refresh();

        int configured = 0;
        try {
            AssetDatabase.StartAssetEditing();

            foreach(string meshPath in meshPaths) {
                ModelImporter importer = UnityEditor.AssetImporter.GetAtPath(meshPath) as ModelImporter;
                if(importer == null) {
                    continue;
                }

                if(importer.addCollider) {
                    continue; // deja correct, on evite une reimportation inutile
                }

                importer.addCollider = true;
                importer.SaveAndReimport();
                configured++;
            }
        } finally {
            // StopAssetEditing DOIT s'executer meme en cas d'exception, sinon
            // la base d'assets reste bloquee en mode edition pour toute la
            // session Unity.
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();
        Debug.Log($"[Colliders] {configured} modele(s) reimporte(s) avec collider "
                  + $"({meshPaths.Count - configured} deja correct(s)).");
    }

    protected static string FindInSubDirectories(string baseFolder, string fileName) {
        try {
            string[] files = Directory.GetFiles(baseFolder, fileName, SearchOption.AllDirectories);

            if(files.Length > 0) {
                foreach(string file in files) {
                    return file;
                }
            } else {
                Debug.Log("File not found in the specified directory.");
            }
        } catch(DirectoryNotFoundException e) {
            Debug.Log("Directory not found: " + e.Message);
        } catch(Exception ex) {
            Debug.LogError("An error occurred: " + ex.Message);
        }

        return null;
    }

    protected static string GetParentFolder(string path) {
        string currentDirectory = path;
        if(Path.GetExtension(path) != string.Empty) {
            currentDirectory = Path.GetDirectoryName(path);
        }

        string parentFolderFullPath = Directory.GetParent(currentDirectory).FullName;
        string parentFolderRelativePath = Path.Combine("Assets", Path.GetRelativePath(Application.dataPath, parentFolderFullPath));
        return parentFolderRelativePath;
    }

    protected static string GetFolderName(string path) {
        string folderName = Path.GetFileName(path);
        return folderName;
    }
}
#endif
