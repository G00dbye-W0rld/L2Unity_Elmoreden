#if (UNITY_EDITOR)
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class L2LightBuilder
{
    // LightRadius/LightBrightness du .unr n'utilisent pas les unites monde
    // (comme Location) ni l'echelle Unity (comme un Light.range/.intensity) :
    // ce sont des unites internes au moteur Unreal 1. Ces deux constantes
    // approximent la conversion a partir de conventions UE1 connues ; a
    // recalibrer visuellement sur 17_25 (seule region de reference qui a de
    // vraies donnees Light - 17_23 en a zero) si l'eclairage rendu parait
    // trop faible/fort ou trop court/long.
    private const float RadiusToUnrealUnits = 25f;
    private const float BrightnessToIntensity = 1f / 64f;

    [MenuItem("Shnok/[Debug][Light] (T3D) Build lights")]
    static void BuildLightsMenu()
    {
        string title = "Select T3D file";
        string directory = Path.Combine(Application.dataPath, "Resources/Data/Maps");
        string extension = "t3d";

        string fileToProcess = EditorUtility.OpenFilePanel(title, directory, extension);

        if (!string.IsNullOrEmpty(fileToProcess))
        {
            Debug.Log("Selected file: " + fileToProcess);
            BuildLightsFrom(fileToProcess);
        }
    }

    /// Etape Phase 2 sans dialogue. Voir L2MapBatchImporter.
    public static void BuildLightsFrom(string t3dPath)
    {
        List<L2Light> lights = L2T3DInfoParser.ParseLights(t3dPath);
        Build(lights);
    }

    private static void Build(List<L2Light> lights)
    {
        int removed = L2TerrainGenerator.DestroyContainers("Lights");
        if (removed > 0)
        {
            Debug.Log($"[Light] {removed} conteneur(s) precedent(s) supprime(s) avant regeneration.");
        }

        GameObject container = new GameObject("Lights");

        foreach (L2Light l2Light in lights)
        {
            GameObject go = new GameObject(l2Light.name);
            go.transform.parent = container.transform;
            go.transform.position = VectorUtils.ConvertPosToUnity(l2Light.position);

            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;

            float hue01 = (l2Light.hue % 256) / 255f;
            // LightSaturation Unreal : 0 = pleinement colore, 255 = blanc -
            // inverse de la saturation HSV standard.
            float saturation01 = 1f - Mathf.Clamp01(l2Light.saturation / 255f);
            light.color = Color.HSVToRGB(hue01, saturation01, 1f);

            light.intensity = l2Light.brightness * BrightnessToIntensity;
            light.range = (l2Light.radius * RadiusToUnrealUnits) / 52.5f;
        }

        Debug.Log($"[Light] {lights.Count} lumiere(s) construite(s).");
    }
}
#endif
