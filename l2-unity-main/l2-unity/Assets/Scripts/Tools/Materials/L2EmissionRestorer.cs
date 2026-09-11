#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Recable l'auto-illumination perdue a l'import. Les .props.txt du client
// declarent SelfIllumination, que l'importeur a ignore.
//
// Le point delicat est le masque : le client ecrit aussi SelfIlluminationMask
// sur la meme texture, ce qui designe son canal ALPHA. Sur un mur a fenetres,
// seules les vitres ont de l'alpha. URP n'a pas de canal de masque separe -
// _EmissionMap utilise le RVB tel quel - donc on genere une texture RVB x A.
public static class L2EmissionRestorer
{
    private const string Root = "Assets/Resources/Data/Textures";
    private const string OutDir = "Assets/Resources/Data/Textures/_Emission";
    private const string Suffix = ".props.txt";

    // Marqueur lu par EmissiveDayNight : sans lui il attenuerait aussi les
    // flammes et les effets magiques, qui doivent briller de jour comme de nuit.
    public const string Tag = "L2DayNight";

    // Blanc a intensite 1.6 dans le selecteur HDR : 2^1.6 = 3.0314.
    private static readonly Color Emission = new Color(3.031433f, 3.031433f, 3.031433f, 1f);

    [MenuItem("L2/Outils/Emission - rapport (ne modifie rien)", false, 520)]
    private static void Report()
    {
        Run(false);
    }

    [MenuItem("L2/Outils/Emission - appliquer (masque alpha)", false, 521)]
    private static void Apply()
    {
        if (EditorUtility.DisplayDialog("Restaurer l'auto-illumination",
                "Genere les cartes d'emission masquees et modifie les materiaux.\n\n"
                + "Lancez \"tout annuler\" d'abord si une passe precedente a ete faite.",
                "Appliquer", "Annuler"))
        {
            Run(true);
        }
    }

    [MenuItem("L2/Outils/Emission - tout annuler", false, 522)]
    private static void Revert()
    {
        if (!EditorUtility.DisplayDialog("Annuler l'auto-illumination",
                "Retire l'emission et le marqueur de tous les materiaux traites.",
                "Annuler l'emission", "Ne rien faire"))
        {
            return;
        }

        int n = 0;

        foreach (string file in Directory.GetFiles(Root, "*" + Suffix, SearchOption.AllDirectories))
        {
            string matPath = file.Substring(0, file.Length - Suffix.Length).Replace('\\', '/') + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            if (mat == null || mat.GetTag(Tag, false, "") != "1")
            {
                continue;
            }

            mat.DisableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", null);
            mat.SetColor("_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            mat.SetOverrideTag(Tag, "");

            EditorUtility.SetDirty(mat);
            n++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Emission] ANNULE sur {n} materiau(x). Les cartes generees restent dans {OutDir}.");
    }

    private static void Run(bool write)
    {
        string[] files = Directory.GetFiles(Root, "*" + Suffix, SearchOption.AllDirectories);

        var detail = new StringBuilder();
        int masked = 0, plain = 0, already = 0, noMat = 0, unresolved = 0, unreadable = 0;

        try
        {
            for (int f = 0; f < files.Length; f++)
            {
                string file = files[f];
                string self = ReadValue(file, "SelfIllumination");

                if (string.IsNullOrEmpty(self))
                {
                    continue;
                }

                string matPath = file.Substring(0, file.Length - Suffix.Length).Replace('\\', '/') + ".mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

                if (mat == null)
                {
                    noMat++;
                    continue;
                }

                if (mat.GetTag(Tag, false, "") == "1")
                {
                    already++;
                    continue;
                }

                // 159 materiaux sur 163 illuminent avec leur propre diffus : on
                // reprend la texture que l'importeur a deja resolue, plutot que
                // de refaire sa correspondance de chemins.
                string diffuse = ReadValue(file, "Diffuse");
                Texture source = self == diffuse ? mat.GetTexture("_BaseMap") : FindTexture(self);

                if (source == null)
                {
                    unresolved++;
                    detail.AppendLine($"   NON RESOLU  {Path.GetFileName(matPath)}  <- {self}");
                    continue;
                }

                EditorUtility.DisplayProgressBar("Emission", Path.GetFileName(matPath),
                    (float)f / files.Length);

                Texture map = write
                    ? BuildEmissionMap(source, ref masked, ref plain, ref unreadable, detail)
                    : Probe(source, ref masked, ref plain, ref unreadable);

                if (map == null)
                {
                    unresolved++;
                    continue;
                }

                if (!write)
                {
                    continue;
                }

                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_EmissionMap", map);
                mat.SetColor("_EmissionColor", Emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetOverrideTag(Tag, "1");

                EditorUtility.SetDirty(mat);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (write)
        {
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"[Emission] {(write ? "APPLIQUE" : "RAPPORT")} - "
                  + $"{masked} masque(s) alpha | {plain} sans masque | {already} deja fait | "
                  + $"{noMat} sans .mat | {unresolved} non resolu | {unreadable} illisible\n{detail}");
    }

    // Compte sans rien ecrire, pour le rapport.
    private static Texture Probe(Texture source, ref int masked, ref int plain, ref int unreadable)
    {
        Texture2D decoded = Decode(source);

        if (decoded == null)
        {
            unreadable++;
            return source;
        }

        if (HasAlphaMask(decoded))
        {
            masked++;
        }
        else
        {
            plain++;
        }

        Object.DestroyImmediate(decoded);

        return source;
    }

    private static Texture BuildEmissionMap(Texture source, ref int masked, ref int plain,
        ref int unreadable, StringBuilder detail)
    {
        string srcPath = AssetDatabase.GetAssetPath(source);
        Texture2D decoded = Decode(source);

        // TGA et formats non decodables : on retombe sur la texture brute,
        // quitte a ce que la surface entiere emette.
        if (decoded == null)
        {
            unreadable++;
            detail.AppendLine($"   NON DECODABLE  {Path.GetFileName(srcPath)} - texture brute utilisee");
            return source;
        }

        if (!HasAlphaMask(decoded))
        {
            plain++;
            Object.DestroyImmediate(decoded);
            return source;
        }

        Color32[] px = decoded.GetPixels32();

        for (int i = 0; i < px.Length; i++)
        {
            float a = px[i].a / 255f;
            px[i] = new Color32((byte)(px[i].r * a), (byte)(px[i].g * a), (byte)(px[i].b * a), 255);
        }

        decoded.SetPixels32(px);
        decoded.Apply();

        Directory.CreateDirectory(OutDir);

        string outPath = $"{OutDir}/{Path.GetFileNameWithoutExtension(srcPath)}_emis.png";
        File.WriteAllBytes(outPath, decoded.EncodeToPNG());
        Object.DestroyImmediate(decoded);

        AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);

        // Qualifie : le projet a sa propre classe AssetImporter, qui masque
        // celle de UnityEditor.
        if (UnityEditor.AssetImporter.GetAtPath(outPath) is TextureImporter imp)
        {
            imp.sRGBTexture = true;
            imp.alphaSource = TextureImporterAlphaSource.None;
            imp.SaveAndReimport();
        }

        masked++;

        return AssetDatabase.LoadAssetAtPath<Texture>(outPath);
    }

    // On decode les octets du fichier plutot que de lire l'asset importe : cela
    // evite d'avoir a basculer isReadable sur 91 textures.
    private static Texture2D Decode(Texture source)
    {
        string path = AssetDatabase.GetAssetPath(source);

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        if (tex.LoadImage(File.ReadAllBytes(path)))
        {
            return tex;
        }

        Object.DestroyImmediate(tex);

        return null;
    }

    private static bool HasAlphaMask(Texture2D tex)
    {
        Color32[] px = tex.GetPixels32();

        for (int i = 0; i < px.Length; i++)
        {
            if (px[i].a < 250)
            {
                return true;
            }
        }

        return false;
    }

    // Ligne du client : SelfIllumination = Texture'Light_A.StLight03'
    private static string ReadValue(string file, string key)
    {
        foreach (string line in File.ReadLines(file))
        {
            if (!line.StartsWith(key + " = Texture'"))
            {
                continue;
            }

            int start = line.IndexOf('\'') + 1;
            int end = line.LastIndexOf('\'');

            return end > start ? line.Substring(start, end - start) : null;
        }

        return null;
    }

    // Nom Unreal "Paquet.Texture" : seul le dernier segment nomme l'asset.
    private static Texture FindTexture(string unrealName)
    {
        int dot = unrealName.LastIndexOf('.');
        string name = dot >= 0 ? unrealName.Substring(dot + 1) : unrealName;

        foreach (string guid in AssetDatabase.FindAssets($"{name} t:Texture"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (Path.GetFileNameWithoutExtension(path) == name)
            {
                return AssetDatabase.LoadAssetAtPath<Texture>(path);
            }
        }

        return null;
    }
}
#endif
