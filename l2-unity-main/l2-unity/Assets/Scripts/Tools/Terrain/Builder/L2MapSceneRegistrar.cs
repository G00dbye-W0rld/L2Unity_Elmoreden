#if (UNITY_EDITOR)
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Declare une region importee aupres du jeu.
///
/// POURQUOI
/// Une scene de region generee par le pipeline n'est pas encore jouable :
/// SceneLoader la charge PAR SON NOM (LoadSceneAsync en additif), ce qui
/// suppose deux inscriptions manuelles, faciles a oublier et sans aucun
/// message quand elles manquent :
///
///   1. EditorBuildSettings - une scene absente ne peut pas etre chargee du
///      tout a l'execution. 17_23 avait ete importee sans y figurer.
///   2. La _mapList du SceneLoader, serialisee dans
///      Resources/Prefab/Game.prefab - c'est elle qui decide des regions
///      effectivement montees au demarrage.
///
/// Le point 2 est volontairement conservateur : la region est ajoutee avec
/// enabled = 0, comme le sont deja 16_25 / 17_24 / 16_24 dans le prefab. On
/// declare la region sans changer ce qui se charge reellement au demarrage -
/// activer une region est une decision de gameplay, pas d'import.
public static class L2MapSceneRegistrar
{
    private const string GamePrefabPath = "Assets/Resources/Prefab/Game.prefab";

    /// Inscrit la scene dans les Build Settings si elle n'y est pas deja.
    /// Rend true si une modification a ete faite.
    public static bool RegisterInBuildSettings(string mapName)
    {
        string scenePath = $"Assets/Resources/Scenes/{mapName}.unity";

        if (!File.Exists(scenePath))
        {
            Debug.LogWarning($"[Scene] {scenePath} absente, pas d'inscription aux Build Settings.");
            return false;
        }

        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();

        if (scenes.Any(s => s.path == scenePath))
        {
            Debug.Log($"[Scene] {mapName} deja dans les Build Settings.");
            return false;
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        Debug.Log($"[Scene] {mapName} ajoutee aux Build Settings.");
        return true;
    }

    /// Ajoute la region a la _mapList du SceneLoader (Game.prefab), avec
    /// enabled = false. Rend true si une modification a ete faite.
    ///
    /// Passe par SerializedObject plutot que par une edition texte du prefab :
    /// c'est l'API qui garantit une serialisation valide, et elle reste
    /// correcte si la structure de SceneListObject evolue.
    public static bool RegisterInSceneLoader(string mapName)
    {
        GameObject gamePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GamePrefabPath);
        if (gamePrefab == null)
        {
            Debug.LogWarning($"[Scene] {GamePrefabPath} introuvable, _mapList non mise a jour.");
            return false;
        }

        SceneLoader loader = gamePrefab.GetComponentInChildren<SceneLoader>(true);
        if (loader == null)
        {
            Debug.LogWarning("[Scene] Aucun SceneLoader dans Game.prefab, _mapList non mise a jour.");
            return false;
        }

        SerializedObject so = new SerializedObject(loader);
        SerializedProperty mapList = so.FindProperty("_mapList");
        if (mapList == null || !mapList.isArray)
        {
            Debug.LogWarning("[Scene] Propriete _mapList introuvable sur SceneLoader.");
            return false;
        }

        for (int i = 0; i < mapList.arraySize; i++)
        {
            SerializedProperty existing = mapList.GetArrayElementAtIndex(i).FindPropertyRelative("name");
            if (existing != null && existing.stringValue == mapName)
            {
                Debug.Log($"[Scene] {mapName} deja dans la _mapList du SceneLoader.");
                return false;
            }
        }

        mapList.arraySize++;
        SerializedProperty added = mapList.GetArrayElementAtIndex(mapList.arraySize - 1);
        added.FindPropertyRelative("name").stringValue = mapName;

        // enabled = false : la region est declaree mais pas montee au
        // demarrage, comme 16_25/17_24/16_24. A activer a la main quand elle
        // est validee visuellement et que sa geodata serveur est en place.
        SerializedProperty enabled = added.FindPropertyRelative("enabled");
        if (enabled != null)
        {
            enabled.boolValue = false;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gamePrefab);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Scene] {mapName} ajoutee a la _mapList du SceneLoader (enabled = false). "
                  + "Passez-la a true dans Resources/Prefab/Game.prefab pour la charger au demarrage.");
        return true;
    }

    /// Les deux inscriptions d'un coup.
    public static void RegisterRegion(string mapName)
    {
        RegisterInBuildSettings(mapName);
        RegisterInSceneLoader(mapName);
    }
}
#endif
