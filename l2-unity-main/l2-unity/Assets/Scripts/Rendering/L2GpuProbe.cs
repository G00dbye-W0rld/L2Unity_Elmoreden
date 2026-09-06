using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;

// Enregistreur de vol pour les resets pilote. Quand le device meurt, le log
// Unity ne dit rien d'utile : cette sonde ecrit l'etat GPU en continu, et un
// releve detaille avec la pose de la camera des qu'une image depasse le seuil.
public class L2GpuProbe : MonoBehaviour
{
    [SerializeField] private float _interval = 1f;
    [SerializeField] private bool _logSceneChanges = true;

    [Tooltip("Toute image depassant ce seuil (ms) est relevee seule. 0 desactive.")]
    [SerializeField] private float _spikeThresholdMs = 250f;

    [Tooltip("Images precedant le pic a joindre au releve : la trajectoire d'approche.")]
    [SerializeField] private int _spikeTrailFrames = 30;

    [SerializeField] private int _maxSpikeRecords = 200;

    [Tooltip("Enregistrement binaire du profileur. Plusieurs centaines de Mo par minute.")]
    [SerializeField] private bool _writeProfilerLog = false;

    private struct FrameSample
    {
        public float Ms;
        public Vector3 Position;
        public float Yaw;
        public bool Valid;
    }

    private static L2GpuProbe _instance;

    private float _next;
    private float _worstFrameMs;
    private int _frames;
    private float _accumMs;

    private FrameSample[] _trail;
    private int _trailWrite;
    private int _trailCount;
    private int _spikeCount;
    private System.IO.StreamWriter _spikeLog;

    private void Awake()
    {
        // LoadGame recharge Game.unity a chaque entree en jeu : sans garde, une
        // seconde sonde nait et chaque image serait comptee deux fois.
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
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

        if (_spikeThresholdMs > 0f)
        {
            _trail = new FrameSample[Mathf.Max(1, _spikeTrailFrames)];
            OpenSpikeLog();
        }

        Debug.Log("[GpuProbe] Demarree. " + DescribeDevice());
    }

    private void OnDestroy()
    {
        if (_instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;

        CloseSpikeLog();
        _instance = null;
    }

    private void OnApplicationQuit()
    {
        CloseSpikeLog();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[GpuProbe] +scene {scene.name} ({SceneManager.sceneCount}) - {Snapshot()}");
    }

    private void OnSceneUnloaded(Scene scene)
    {
        Debug.Log($"[GpuProbe] -scene {scene.name} ({SceneManager.sceneCount}) - {Snapshot()}");
    }

    private void Update()
    {
        float ms = Time.unscaledDeltaTime * 1000f;
        _accumMs += ms;
        _frames++;

        if (ms > _worstFrameMs)
        {
            _worstFrameMs = ms;
        }

        TrackFrame(ms);

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

        Debug.Log($"[GpuProbe] moy {avg:F1} ms | pic {worst:F1} ms | {Snapshot()}");
    }

    // unscaledDeltaTime lu pendant l'image N donne la duree de N-1 : la pose
    // fautive est celle du tour precedent, pas la courante.
    private void TrackFrame(float ms)
    {
        if (_trail == null)
        {
            return;
        }

        int previous = (_trailWrite - 1 + _trail.Length) % _trail.Length;

        if (_trailCount > 0)
        {
            _trail[previous].Ms = ms;

            if (ms >= _spikeThresholdMs && _spikeCount < _maxSpikeRecords)
            {
                RecordSpike(_trail[previous]);
            }
        }

        Camera cam = Camera.main;

        _trail[_trailWrite] = new FrameSample
        {
            Ms = 0f,
            Position = cam != null ? cam.transform.position : Vector3.zero,
            Yaw = cam != null ? cam.transform.eulerAngles.y : 0f,
            Valid = cam != null,
        };

        _trailWrite = (_trailWrite + 1) % _trail.Length;

        if (_trailCount < _trail.Length)
        {
            _trailCount++;
        }
    }

    private void RecordSpike(FrameSample slow)
    {
        _spikeCount++;

        Camera cam = Camera.main;
        string etat = Snapshot();
        string region = slow.Valid ? RegionGrid.NameAt(slow.Position) : "(inconnue)";

        Debug.LogWarning($"[GpuProbe] PIC #{_spikeCount} : {slow.Ms:F1} ms | region {region} | "
                         + $"cap {slow.Yaw:F1} deg | pos {Fmt(slow.Position)} | {etat}");

        if (_spikeLog == null)
        {
            return;
        }

        _spikeLog.WriteLine();
        _spikeLog.WriteLine($"--- PIC #{_spikeCount} | image {slow.Ms:F1} ms | t = {Time.unscaledTime:F1} s");
        _spikeLog.WriteLine($"    position  {Fmt(slow.Position)}   region {region}   cap {slow.Yaw:F1} deg");
        _spikeLog.WriteLine($"    etat      {etat}");
        _spikeLog.WriteLine($"    config    {DescribeConfig(cam)}");
        _spikeLog.WriteLine($"    scenes    {DescribeScenes()}");
        _spikeLog.WriteLine($"    trajet    {_trailCount} images precedentes :");

        WriteTrail();
    }

    private void WriteTrail()
    {
        int oldest = (_trailWrite - _trailCount + _trail.Length) % _trail.Length;

        for (int i = 0; i < _trailCount; i++)
        {
            FrameSample s = _trail[(oldest + i) % _trail.Length];

            if (s.Valid)
            {
                _spikeLog.WriteLine($"      -{_trailCount - 1 - i,-3} {s.Ms,8:F1} ms  "
                                    + $"{Fmt(s.Position)}  cap {s.Yaw:F1} deg");
            }
        }
    }

    private string Snapshot()
    {
        var sb = new StringBuilder($"scenes {SceneManager.sceneCount}");

#if UNITY_EDITOR
        sb.Append($" | draws {UnityEditor.UnityStats.drawCalls}");
        sb.Append($" setpass {UnityEditor.UnityStats.setPassCalls}");
        sb.Append($" batches {UnityEditor.UnityStats.batches}");
        sb.Append($" tris {UnityEditor.UnityStats.triangles / 1000}k");
        sb.Append($" shadowCasters {UnityEditor.UnityStats.shadowCasters}");
#endif

        sb.Append($" | gfx {Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024 * 1024)} Mo");
        sb.Append($" mono {Profiler.GetMonoUsedSizeLong() / (1024 * 1024)} Mo");
        sb.Append($" reserve {Profiler.GetTotalReservedMemoryLong() / (1024 * 1024)} Mo");

        // Un compteur de GC qui monte pendant l'image bloquee designe la
        // memoire ; sinon c'est le pilote.
        sb.Append($" | gc {System.GC.CollectionCount(0)}/{System.GC.CollectionCount(1)}/{System.GC.CollectionCount(2)}");

        return sb.ToString();
    }

    private static string Fmt(Vector3 v)
    {
        return $"({v.x:F1} ; {v.y:F1} ; {v.z:F1})";
    }

    private static string DescribeScenes()
    {
        var sb = new StringBuilder();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            sb.Append(i > 0 ? ", " : "").Append(SceneManager.GetSceneAt(i).name);
        }

        return sb.ToString();
    }

    private static string DescribeDevice()
    {
        return $"{SystemInfo.graphicsDeviceName} | {SystemInfo.graphicsDeviceType} | "
               + $"VRAM {SystemInfo.graphicsMemorySize} Mo";
    }

    // La camera est passee en argument : Camera.main peut renvoyer la
    // LoadingCamera, dont la distance de coupe n'est pas celle du joueur.
    private static string DescribeConfig(Camera cam)
    {
        RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;

        return $"camera {(cam != null ? cam.name : "(aucune)")} "
               + $"far clip {(cam != null ? cam.farClipPlane : 0f)} | "
               + $"MSAA {QualitySettings.antiAliasing}x | "
               + $"pipeline {(pipeline != null ? pipeline.name : "(builtin)")} | "
               + $"brouillard {(RenderSettings.fog ? RenderSettings.fogDensity.ToString() : "off")}";
    }

    // AutoFlush est indispensable : un TDR tue le processus sans lui laisser
    // fermer le fichier, et c'est le dernier releve qui nous interesse.
    private void OpenSpikeLog()
    {
        try
        {
            string dir = System.IO.Path.Combine(Application.dataPath, "..", "GpuProbeLogs");
            System.IO.Directory.CreateDirectory(dir);

            string path = System.IO.Path.Combine(dir, $"pics_{System.DateTime.Now:yyyyMMdd_HHmmss}.log");

            _spikeLog = new System.IO.StreamWriter(path, false) { AutoFlush = true };
            _spikeLog.WriteLine($"# {System.DateTime.Now} - {DescribeDevice()}");
            _spikeLog.WriteLine($"# seuil {_spikeThresholdMs} ms, trajet {_spikeTrailFrames} images");

            Debug.Log($"[GpuProbe] Pics enregistres dans {System.IO.Path.GetFullPath(path)}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GpuProbe] Fichier de pics impossible a ouvrir : {e.Message}");
            _spikeLog = null;
        }
    }

    private void CloseSpikeLog()
    {
        if (_spikeLog == null)
        {
            return;
        }

        _spikeLog.WriteLine();
        _spikeLog.WriteLine($"# Fin de session - {_spikeCount} pic(s).");
        _spikeLog.Dispose();
        _spikeLog = null;
    }

    private void StartProfilerLog()
    {
        string dir = System.IO.Path.Combine(Application.dataPath, "..", "ProfilerLogs");
        System.IO.Directory.CreateDirectory(dir);

        Profiler.logFile = System.IO.Path.Combine(dir, $"session_{System.DateTime.Now:yyyyMMdd_HHmmss}");
        Profiler.enableBinaryLog = true;
        Profiler.enabled = true;

        Debug.Log($"[GpuProbe] Profileur -> {Profiler.logFile}.raw");
    }
}
