#if (UNITY_EDITOR) 
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[System.Serializable]
public class AmbientDataContainer
{
    public AmbientSound[] data;
}

public class L2JSONAmbientSoundImporter : AssetImporter
{

    // umodel export folder
    // Exports umodel des sons (separes des meshes/textures).
    static string dataFolder = @"D:\Jeux\MAP_L2Unity\export_sound";
    // copy ambient sounds to
    static string scriptExportFolder = @"D:\Jeux\MAP_L2Unity\export_sound\unityexport";

    [MenuItem("L2/Outils/AmbientSound - (JSON) Import sounds", false, 401)]
    static void ImportSoundsMenu()
    {
        string title = "Select ambient sound list";
        string directory = Path.Combine(Application.dataPath, "Resources/Data/Maps");
        string extension = "json";

        string fileToProcess = EditorUtility.OpenFilePanel(title, directory, extension);

        if (!string.IsNullOrEmpty(fileToProcess))
        {
            Debug.Log("Selected file: " + fileToProcess);
            AmbientSound[] sounds = L2TerrainInfoParser.ParseAmbientSoundFile(fileToProcess);
            ImportAmbientSoundFiles(sounds, dataFolder);
        }
    }


    [MenuItem("L2/Import/09 AmbientSound - (T3D) Import sounds", false, 30)]
    static void ImportSoundsMenuT3D()
    {
        string title = "Select ambient sound list";
        string directory = Path.Combine(Application.dataPath, "Resources/Data/Maps");
        string extension = "t3d";

        string fileToProcess = EditorUtility.OpenFilePanel(title, directory, extension);

        if (!string.IsNullOrEmpty(fileToProcess))
        {
            Debug.Log("Selected file: " + fileToProcess);

            List<AmbientSound> sounds = L2T3DInfoParser.ParseAmbientSounds(fileToProcess);
            ImportAmbientSoundFiles(sounds.ToArray(), dataFolder);
        }
    }

    private static void ImportAmbientSoundFiles(AmbientSound[] sounds, string dataFolder)
    {
        List<string> soundNames = new List<string>();
        foreach (AmbientSound sound in sounds)
        {
            Debug.Log(sound);
            Debug.Log(sound.ambientSoundName);
            string[] parts = sound.ambientSoundName.Split(".");
            soundNames.Add(parts[0].Trim() + "." + parts[2].Trim());
        }

        soundNames = soundNames.Distinct().ToList();
        foreach (string soundname in soundNames)
        {
            Debug.Log(soundname);
            string[] parts = soundname.Split(".");
            string fileToCopy = Path.Combine(dataFolder, parts[0], "Sound", parts[1] + ".wav");
            Debug.Log(fileToCopy);
            if (!File.Exists(fileToCopy))
            {
                Debug.LogError($"File missing at {fileToCopy}");
            }
            else
            {
                string outputFolder = Path.Combine(scriptExportFolder, parts[0]);
                string outputFile = Path.Combine(outputFolder, parts[1] + ".wav");
                if (!Directory.Exists(outputFolder))
                {
                    Directory.CreateDirectory(outputFolder);
                }
                File.Copy(fileToCopy, outputFile, true);
            }
        }
        Debug.LogWarning($"Need to {soundNames.Count} load ambient sounds.");

        Debug.LogWarning($"Ambient {soundNames.Count} sound files individually copied into folder {scriptExportFolder}");
    }

}
#endif