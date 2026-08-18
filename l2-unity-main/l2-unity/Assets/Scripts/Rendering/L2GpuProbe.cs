using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;

/// Enregistreur de vol pour les resets GPU.
///
/// POURQUOI
/// Quatre hypotheses ont ete formulees puis invalidees sur les crashs
/// "Failed to present D3D11 swapchain" du 12-14/08/2026 : le bruit de log, la
/// rafale de pipeline states MicroSplat, les cibles de rendu surdimensionnees
/// (MSAA x8, shadowmaps 8192), et la visualisation NavMesh. Chacune reposait
/// sur une inference a partir de ce que le log NE contenait pas.
///
/// Le probleme est que le log ne contient RIEN d'utile au moment du crash :
/// les "failed to create buffer" qui le precedent sont posterieurs a la perte
/// du device, et Windows confirme un TDR sans dire ce qui l'a provoque.
///
/// Cette sonde ecrit donc l'etat GPU a intervalle regulier. Quand le pilote
/// lachera, la derniere ligne du log dira ce que la machine etait en train de
/// dessiner - au lieu de nous laisser deviner une cinquieme fois.
///
/// A POSER sur un objet de la scene Game, ou n'importe ou : elle se rend
/// persistante et n'a aucune dependance.
public class L2GpuProbe : MonoBehaviour
{
    [Tooltip("Secondes entre deux releves. Trop court noierait le log, trop "
             + "long raterait le pic qui precede le reset.")]
    [SerializeField] private float _interval = 1f;

    [Tooltip("Ne journalise que si le temps par image depasse ce seuil (ms). "
             + "0 = tout journaliser. Utile pour ne garder que les pics.")]
    [SerializeField] private float _frameTimeThresholdMs = 0f;

    [Tooltip("Journalise aussi chaque chargement et dechargement de scene : "
             + "c'est la correlation la plus probable avec les resets.")]
    [SerializeField] private bool _logSceneChanges = true;

    [Header("Profileur")]
    [Tooltip("Ecrit les donnees du profileur dans un fichier, en continu. "
             + "INDISPENSABLE ici : quand le pilote lache, Unity meurt et emporte "
             + "les donnees gardees en memoire - or c'est justement cette image-la "
             + "qu'on veut examiner. Le fichier, lui, survit.")]
    [SerializeField] private bool _writeProfilerLog = false;

    private float _next;
    private float _worstFrameMs;
    private int _frames;
    private float _accumMs;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        if (_writeProfilerLog)
        {
            StartProfilerLog();
        }

        if (_logSceneChanges)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        Debug.Log("[GpuProbe] Demarree. " + DescribeDevice());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[GpuProbe] +scene {scene.name} ({SceneManager.sceneCount} chargees) - {Snapshot()}");
    }

    private void OnSceneUnloaded(Scene scene)
    {
        Debug.Log($"[GpuProbe] -scene {scene.name} ({SceneManager.sceneCount} chargees) - {Snapshot()}");
    }

    private void Update()
    {
        // Le temps par image est accumule a CHAQUE image, pas seulement au
        // releve : un pic de 800 ms entre deux releves serait invisible
        // autrement, alors que c'est precisement ce qu'on cherche.
        float ms = Time.unscaledDeltaTime * 1000f;
        _accumMs += ms;
        _frames++;

        if (ms > _worstFrameMs)
        {
            _worstFrameMs = ms;
        }

        if (Time.unscaledTime < _next)
        {
            return;
        }

        _next = Time.unscaledTime + _interval;

        float avg = _frames > 0 ? _accumMs / _frames : 0f;
        float worst = _worstFrameMs;

        _accumMs = 0f;
        _frames = 0;
        _worstFrameMs = 0f;

        if (_frameTimeThresholdMs > 0f && worst < _frameTimeThresholdMs)
        {
            return;
        }

        Debug.Log($"[GpuProbe] moy {avg:F1} ms | pic {worst:F1} ms | {Snapshot()}");
    }

    /// Etat instantane, compact : une seule ligne de log par releve.
    private string Snapshot()
    {
        var sb = new StringBuilder();

        sb.Append($"scenes {SceneManager.sceneCount}");

#if UNITY_EDITOR
        // UnityStats n'existe qu'en editeur, mais c'est la que les crashs se
        // produisent - et c'est la seule source de draw calls sans profiler.
        sb.Append($" | draws {UnityEditor.UnityStats.drawCalls}");
        sb.Append($" setpass {UnityEditor.UnityStats.setPassCalls}");
        sb.Append($" batches {UnityEditor.UnityStats.batches}");
        sb.Append($" tris {UnityEditor.UnityStats.triangles / 1000}k");
        sb.Append($" shadowCasters {UnityEditor.UnityStats.shadowCasters}");
#endif

        // La memoire graphique allouee par Unity ne couvre pas tout ce que le
        // pilote reserve (cibles de rendu comprises), mais sa DERIVE est le
        // signal recherche : une montee reguliere trahit une fuite.
        long gfx = Profiler.GetAllocatedMemoryForGraphicsDriver();
        sb.Append($" | gfx {gfx / (1024 * 1024)} Mo");
        sb.Append($" mono {Profiler.GetMonoUsedSizeLong() / (1024 * 1024)} Mo");
        sb.Append($" reserve {Profiler.GetTotalReservedMemoryLong() / (1024 * 1024)} Mo");

        // LE DISCRIMINANT.
        //
        // Le releve du 2026-08-14 a montre une image a 23 474 ms entouree
        // d'images a 15 ms, sans montee de gfx ni surcharge de draws. Deux
        // mecanismes peuvent produire cela :
        //
        //   - la creation d'un pipeline state cote pilote, au premier rendu
        //     d'un shader MicroSplat jamais vu (153 programmes distincts) ;
        //   - un ramassage de miettes sur un tas manage de 1,6 Go, ou de la
        //     pagination si la machine manque de RAM.
        //
        // Le compteur de GC les separe : s'il augmente pendant l'image
        // bloquee, c'est la memoire ; sinon c'est le pilote. Time.deltaTime
        // seul ne pouvait pas trancher, puisqu'il mesure du temps mural.
        sb.Append($" | gc {System.GC.CollectionCount(0)}/{System.GC.CollectionCount(1)}/{System.GC.CollectionCount(2)}");

        return sb.ToString();
    }

    /// Demarre l'ecriture continue du profileur sur disque.
    ///
    /// Le fichier .raw se recharge ensuite dans la fenetre Profiler
    /// (Load), y compris apres un crash - c'est tout l'interet.
    ///
    /// ATTENTION : le fichier grossit vite, de l'ordre de plusieurs centaines
    /// de Mo par minute. A n'activer que pour une session de diagnostic, et a
    /// supprimer ensuite.
    private void StartProfilerLog()
    {
        string dir = System.IO.Path.Combine(Application.dataPath, "..", "ProfilerLogs");
        System.IO.Directory.CreateDirectory(dir);

        string path = System.IO.Path.Combine(
            dir, $"session_{System.DateTime.Now:yyyyMMdd_HHmmss}");

        Profiler.logFile = path;
        Profiler.enableBinaryLog = true;
        Profiler.enabled = true;

        Debug.Log($"[GpuProbe] Profileur enregistre dans {path}.raw - "
                  + "a recharger via Window > Analysis > Profiler > Load apres le crash.");
    }

    private static string DescribeDevice()
    {
        return $"{SystemInfo.graphicsDeviceName} | {SystemInfo.graphicsDeviceType} | "
               + $"VRAM {SystemInfo.graphicsMemorySize} Mo | "
               + $"shadowmap max {SystemInfo.maxTextureSize}";
    }
}
