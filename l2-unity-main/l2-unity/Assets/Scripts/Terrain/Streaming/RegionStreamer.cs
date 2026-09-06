using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Charge et decharge les regions selon la position du joueur.
///
/// LE PROBLEME
/// SceneLoader.LoadGame charge d'un coup toutes les regions cochees et ne les
/// decharge JAMAIS - UnloadScene existe depuis le debut mais n'a jamais ete
/// appelee. Avec 5 regions cochees ca passe ; avec 153, le jeu ne demarre pas.
///
/// LA FENETRE SUIT LA DISTANCE, PAS UNE FORME
/// On charge toute region dont le BORD est a moins de PreloadDistance du
/// joueur. La fenetre s'adapte donc a sa position dans la region : une seule
/// au centre, deux pres d'un bord, quatre dans un angle. Voir le detail sur le
/// champ _preloadDistance.
///
/// LE DECHARGEMENT EST TEMPOREL
/// Une region sortie de la fenetre part apres UnloadDelay. C'est ce qui evite
/// le va-et-vient aux frontieres sans laisser les regions s'accumuler - la
/// premiere version, fondee sur un rayon, en gardait jusqu'a 25.
///
/// UN SEUL CHARGEMENT A LA FOIS
/// LoadSceneAsync active la scene des qu'elle est prete. Plusieurs regions
/// lancees ensemble s'activeraient dans la meme frame et provoqueraient un
/// gel. On enchaine donc les chargements un par un.
public class RegionStreamer : MonoBehaviour
{
    /// FENETRE PAR DISTANCE, PAS PAR FORME.
    ///
    /// Les formes fixes ont ete essayees et ecartees le 2026-08-12 :
    ///  - le carre (9 regions) a fait tomber le pilote graphique ;
    ///  - la croix (5) laissait un vide visible dans les angles, faute de
    ///    charger la diagonale.
    ///
    /// Une forme figee charge toujours les memes voisines, ou que le joueur se
    /// trouve dans sa region. Au centre on en charge quatre dont aucune n'est
    /// proche ; dans un angle il en manque une qui l'est.
    ///
    /// En raisonnant en distance au BORD de chaque region, la fenetre s'adapte
    /// d'elle-meme :
    ///
    ///     au centre d'une region  -> 1 region  (aucune voisine proche)
    ///     pres d'un bord          -> 2 regions
    ///     dans un angle           -> 4 regions (dont la diagonale)
    ///
    /// On charge donc moins en moyenne qu'avec la croix, tout en couvrant le
    /// cas qui manquait.
    [Header("Fenetre")]
    [Tooltip("Plancher de la fenetre, en unites Unity. Doit couvrir la distance "
             + "parcourue pendant le chargement : ~200 laisse plusieurs secondes "
             + "a vitesse de course. L'horizon du brouillard peut l'elargir.")]
    [SerializeField] private float _preloadDistance = 200f;

    /// LA FENETRE SUIT L'HORIZON DU BROUILLARD.
    ///
    /// Les deux distances repondent a la meme question - jusqu'ou le joueur
    /// voit-il ? - et les laisser diverger produit exactement le defaut
    /// observe : au-dela de fogEndDistance tout est masque, donc rien n'a
    /// besoin d'exister ; en deca, le terrain DOIT exister, sinon on voit le
    /// vide a travers un brouillard encore transparent.
    ///
    /// Mesure du 2026-08-12, avant correction : fenetre a 200, brouillard
    /// opaque seulement a 371,6. Au centre d'une region les voisines sont a
    /// 312 - donc pas chargees - et la bande 312-371 laissait voir le neant.
    /// Le reglage joueur de distance de vue (x0,6 a x2,4) etirait le
    /// brouillard jusqu'a 892 sans jamais elargir la fenetre.
    [Tooltip("Elargit la fenetre jusqu'a l'horizon du brouillard. A laisser "
             + "actif : c'est ce qui garantit qu'on ne voit jamais le vide.")]
    [SerializeField] private bool _followFogHorizon = true;

    /// HYSTERESIS DANS LE TEMPS, PAS DANS L'ESPACE.
    ///
    /// La premiere version gardait tout ce qui etait a distance <= 2, soit une
    /// zone de 5x5 - jusqu'a 25 regions. Mesure du 2026-08-12 : en jouant, le
    /// compte est monte a 8 sans qu'une seule region ne soit dechargee, et le
    /// pilote graphique a lache.
    ///
    /// Une region sortie de la fenetre part donc apres ce delai. Le compte
    /// reste plafonne au nombre de regions de la fenetre, et un joueur qui
    /// longe une frontiere ne declenche pas de va-et-vient : la region qu'il
    /// vient de quitter est encore la quand il revient.
    [Tooltip("Secondes avant de decharger une region sortie de la fenetre. "
             + "Evite le va-et-vient aux frontieres sans laisser les regions "
             + "s'accumuler.")]
    [SerializeField] private float _unloadDelay = 8f;

    [Header("Cadence")]
    [Tooltip("Secondes entre deux evaluations. Inutile de le faire chaque frame : "
             + "traverser une region demande plusieurs secondes.")]
    [SerializeField] private float _checkInterval = 0.5f;

    [Header("Debogage")]
    [SerializeField] private bool _verbose = false;

    /// Regions autorisees. Vide = aucune restriction.
    ///
    /// Sert de liste blanche : la colonne 15 et la rangee 26 sont hors des
    /// bornes du serveur (World.java, TILE_Y_MAX = 25) et ne doivent jamais
    /// etre chargees, meme si le joueur s'en approche.
    private HashSet<string> _allowed;

    private readonly HashSet<string> _loaded = new HashSet<string>();
    private readonly Queue<string> _toLoad = new Queue<string>();

    /// Un balayage d'assets est en cours. Empeche d'en empiler plusieurs.
    private bool _sweeping;

    /// Seuil de memoire graphique au-dela duquel un balayage se declenche, en
    /// mega-octets.
    ///
    /// Mesure du 2026-08-18 sur RTX 4050 portable : le regime etabli a quatre
    /// regions tient a 774 Mo pour un budget de 5152. On peut donc laisser la
    /// memoire monter tranquillement avant de payer le prix d'un balayage.
    [SerializeField] private int _sweepAboveMb = 2000;

    /// Delai minimal entre deux balayages, en secondes. Filet de securite si le
    /// seuil de memoire venait a osciller autour de sa valeur.
    [SerializeField] private float _sweepMinInterval = 45f;

    /// Nombre de dechargements au-dela duquel on balaye meme sous le seuil.
    /// Couvre le cas ou la mesure de memoire graphique serait indisponible.
    [SerializeField] private int _sweepEveryUnloads = 12;

    private float _lastSweepAt = -999f;
    private int _unloadsSinceSweep;

    /// Instant ou chaque region est sortie de la fenetre. Sert de compte a
    /// rebours avant dechargement.
    private readonly Dictionary<string, float> _leftWindowAt = new Dictionary<string, float>();
    private readonly List<string> _expired = new List<string>();

    /// Fenetre courante, recalculee a chaque evaluation. Champ plutot que
    /// variable locale : l'evaluation tourne deux fois par seconde, autant ne
    /// pas allouer un ensemble a chaque fois.
    private readonly HashSet<string> _window = new HashSet<string>();
    private bool _loading;
    private float _nextCheck;

    public static RegionStreamer Instance { get; private set; }

    /// Priorite du chargement asynchrone.
    ///
    /// LA CAUSE DES GELS, TROUVEE AU PROFILEUR LE 2026-08-16.
    ///
    /// Par defaut Unity utilise ThreadPriority.High : il consacre alors autant
    /// de temps que possible PAR IMAGE au chargement, quitte a la faire durer
    /// plusieurs secondes. Mesure sur une image bloquee :
    ///
    ///     PlayerLoop                          623 ms
    ///       CharacterSelector.Update()        613 ms   <- simple raycast
    ///         Loading.IsObjectAvailable       613 ms
    ///           Loading.LockPersistentManager 613 ms
    ///     [thread Loading] Application Preload Assets  6182 ms
    ///
    /// Le script ne chargeait rien : toucher une reference Unity appelle
    /// IsObjectAvailable, qui attend le verrou tenu par le prechargement de la
    /// scene de region. Le thread principal entier se bloquait avec lui, et le
    /// GPU n'attendait que d'etre nourri - ce qui, en D3D11, finissait en reset
    /// de pilote.
    ///
    /// En Low, Unity etale le meme travail sur beaucoup plus d'images : le
    /// chargement d'une region dure plus longtemps, mais ne gele plus le jeu.
    /// C'est le compromis normal d'un monde streame.
    [Header("Chargement")]
    [Tooltip("Low etale le chargement sur plus d'images et supprime les gels. "
             + "High (defaut Unity) charge le plus vite possible, au prix de "
             + "blocages de plusieurs secondes.")]
    [SerializeField] private ThreadPriority _loadingPriority = ThreadPriority.Low;

    private void Awake()
    {
        Application.backgroundLoadingPriority = _loadingPriority;

        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
        }
    }

    /// Declare les regions chargeables et celles deja en memoire.
    /// Appele par SceneLoader une fois le monde initial en place.
    public void Initialize(IEnumerable<string> allowedRegions, IEnumerable<string> alreadyLoaded)
    {
        _allowed = allowedRegions != null ? new HashSet<string>(allowedRegions) : null;

        _loaded.Clear();
        if (alreadyLoaded != null)
        {
            foreach (string r in alreadyLoaded)
            {
                _loaded.Add(r);
            }
        }

        // La fenetre sera recalculee au prochain tick, depuis la position reelle.
    }

    private void Update()
    {
        if (Time.time < _nextCheck)
        {
            return;
        }
        _nextCheck = Time.time + _checkInterval;

        PlayerEntity player = PlayerEntity.Instance;
        if (player == null)
        {
            return;
        }

        // La fenetre depend de la position DANS la region, pas seulement de la
        // region : on reevalue a chaque tick.
        Evaluate(player.transform.position);
        ExpireOutOfWindow();
        PumpQueue();
    }

    /// Distance jusqu'a laquelle le terrain doit exister.
    ///
    /// Statique et sans instance : SceneLoader s'en sert pour dimensionner la
    /// fenetre initiale avec exactement la meme regle. Sans cela le joueur
    /// apparaitrait avec une seule region, et les autres se chargeraient juste
    /// apres l'ecran de chargement - au pire moment.
    ///
    /// Le plancher n'est jamais franchi vers le bas : un brouillard tres court
    /// ne doit pas reduire la fenetre en deca de ce que la vitesse de course
    /// exige pendant un chargement.

    // Plafond de l'horizon, en unites Unity. 450 fait coincider la portee du
    // brouillard avec le bloc 3x3 complet. Il pilote aussi la distance de
    // coupe de la camera, via GameSettings.ApplyCameraReach.
    public const float MaxHorizon = 450f;

    /// Seuil d'opacite retenu pour les modes exponentiels : 99 %.
    ///
    /// Ces modes n'ont pas de distance de fin - le brouillard tend vers
    /// l'opacite sans jamais l'atteindre. On prend donc la distance a laquelle
    /// il masque 99 % de la scene ; au-dela, le vide ne se voit plus.
    ///
    ///     exp(-d*x)     = 0,01  ->  x = ln(100) / d       = 4,605 / d
    ///     exp(-(d*x)^2) = 0,01  ->  x = sqrt(ln(100)) / d = 2,146 / d
    private const float OpaqueExp = 4.60517f;
    private const float OpaqueExpSquared = 2.14597f;

    public static float HorizonDistance(float floor)
    {
        if (!RenderSettings.fog)
        {
            return floor;
        }

        float horizon;

        switch (RenderSettings.fogMode)
        {
            case FogMode.Linear:
                horizon = RenderSettings.fogEndDistance;
                break;

            case FogMode.Exponential:
                horizon = OpaqueExp / RenderSettings.fogDensity;
                break;

            case FogMode.ExponentialSquared:
                horizon = OpaqueExpSquared / RenderSettings.fogDensity;
                break;

            default:
                return floor;
        }

        // Une densite nulle donne un horizon infini ; Min le ramene au plafond
        // sans cas particulier.
        //
        // Le plancher l'emporte sur le plafond : la fenetre ne doit jamais
        // descendre sous ce que la vitesse de course exige pendant un
        // chargement, meme si quelqu'un fixe un plafond trop bas.
        return Mathf.Max(floor, Mathf.Min(horizon, MaxHorizon));
    }

    /// Densite produisant l'opacite a la distance voulue - l'inverse de
    /// HorizonDistance. Sert a raisonner en distance de visibilite plutot
    /// qu'en densite, qui ne parle a personne.
    public static float DensityForHorizon(FogMode mode, float distance)
    {
        if (distance <= 0f)
        {
            return 0f;
        }

        switch (mode)
        {
            case FogMode.Exponential:        return OpaqueExp / distance;
            case FogMode.ExponentialSquared: return OpaqueExpSquared / distance;
            default:                         return 0f;
        }
    }

    /// Densite minimale admissible : en deca, le brouillard porterait plus loin
    /// que la fenetre de streaming et laisserait voir le vide. Sert a brider le
    /// reglage de distance de vue.
    public static float MinFogDensity(FogMode mode)
    {
        return DensityForHorizon(mode, MaxHorizon);
    }

    private void Evaluate(Vector3 position)
    {
        RegionGrid.RegionAt(position, out int column, out int row);

        float window = _followFogHorizon
            ? HorizonDistance(_preloadDistance)
            : _preloadDistance;

        // Le rayon du balayage doit suivre la fenetre : le bloc 3x3 ne suffit
        // que tant qu'elle reste sous les 624 unites d'une region. En vue
        // lointaine l'horizon depasse 890, et une region a deux cases peut
        // alors etre a portee.
        int radius = Mathf.Max(1, Mathf.CeilToInt(window / RegionGrid.RegionSize));

        _window.Clear();
        for (int c = column - radius; c <= column + radius; c++)
        {
            for (int r = row - radius; r <= row + radius; r++)
            {
                if (RegionGrid.DistanceToRegion(position, c, r) > window)
                {
                    continue;
                }

                string name = RegionGrid.NameOf(c, r);
                if (!IsAllowed(name))
                {
                    continue;
                }

                _window.Add(name);

                if (!_loaded.Contains(name) && !_toLoad.Contains(name))
                {
                    _toLoad.Enqueue(name);
                }
            }
        }

        // Ce qui est hors fenetre demarre son compte a rebours ; ce qui y
        // revient l'annule.
        var current = new List<string>(_loaded);
        foreach (string name in current)
        {
            if (_window.Contains(name))
            {
                _leftWindowAt.Remove(name);
            }
            else if (!_leftWindowAt.ContainsKey(name))
            {
                _leftWindowAt[name] = Time.time;
            }
        }
    }

    /// Un chargement a la fois : on ne lance le suivant qu'une fois le
    /// precedent termine.
    private void PumpQueue()
    {
        if (_loading || _toLoad.Count == 0)
        {
            return;
        }

        string name = _toLoad.Dequeue();

        // La region a pu etre chargee entre-temps, ou sortir de la fenetre
        // pendant l'attente.
        if (_loaded.Contains(name))
        {
            return;
        }

        if (!SceneManager.GetSceneByName(name).IsValid())
        {
            _loading = true;
            AsyncOperation op = SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);

            if (op == null)
            {
                // Scene absente des build settings : on ne reessaiera pas.
                Debug.LogWarning($"[Streaming] '{name}' introuvable dans les build settings.");
                _loading = false;
                return;
            }

            op.completed += _ =>
            {
                _loaded.Add(name);
                _loading = false;

                // Les plans d'eau sont indexes une fois pour toutes par
                // WaterSurfaceQuery. Avec le streaming, ceux d'une region
                // arrivee APRES cette indexation resteraient invisibles : le
                // cache n'etant jamais vide, il n'etait jamais reconstruit.
                // Le joueur traversait alors l'eau sans nager ni voir le
                // brouillard sous-marin.
                WaterSurfaceQuery.Invalidate();
                if (_verbose)
                {
                    Debug.Log($"[Streaming] {name} chargee ({_loaded.Count} en memoire).");
                }
            };
        }
        else
        {
            _loaded.Add(name);
        }
    }

    /// Decharge ce qui est hors fenetre depuis assez longtemps.
    private void ExpireOutOfWindow()
    {
        if (_leftWindowAt.Count == 0)
        {
            return;
        }

        _expired.Clear();
        foreach (var kv in _leftWindowAt)
        {
            if (Time.time - kv.Value >= _unloadDelay)
            {
                _expired.Add(kv.Key);
            }
        }

        foreach (string name in _expired)
        {
            _leftWindowAt.Remove(name);
            Unload(name);
        }
    }

    private void Unload(string name)
    {
        _loaded.Remove(name);

        if (!SceneManager.GetSceneByName(name).IsValid())
        {
            return;
        }

        AsyncOperation unload = SceneManager.UnloadSceneAsync(name);

        // DECHARGER LA SCENE NE LIBERE PAS SES ASSETS.
        //
        // UnloadSceneAsync detruit les GameObjects ; les textures, meshes,
        // TerrainData, materiaux et shaders qu'ils referencaient restent en
        // memoire - et en VRAM - jusqu'a un balayage explicite. Une region
        // pese ~22 Mo de texture arrays MicroSplat, plus ses splatmaps, plus
        // un shader qui lui est propre.
        //
        // Sans cet appel, ce n'est pas le nombre de regions CHARGEES qui
        // compte mais le nombre de regions TRAVERSEES : le compteur reste a 4
        // pendant que la VRAM monte sans jamais redescendre. C'est ce qui a
        // tue le pilote le 2026-08-12 avec quatre regions seulement a l'ecran,
        // sur les 5,9 Go d'une RTX 4050 portable.
        if (unload != null)
        {
            unload.completed += _ =>
            {
                // Symetrique du chargement : le cache garderait sinon des
                // references detruites.
                WaterSurfaceQuery.Invalidate();
                ReleaseUnusedAssets();
            };
        }

        if (_verbose)
        {
            Debug.Log($"[Streaming] {name} dechargee ({_loaded.Count} en memoire).");
        }
    }

    /// Balayage des assets devenus orphelins.
    ///
    /// POURQUOI IL NE SE DECLENCHE PLUS A CHAQUE DECHARGEMENT
    /// Resources.UnloadUnusedAssets renvoie un AsyncOperation, mais sa partie
    /// lourde n'est PAS asynchrone : Unity parcourt tout le graphe d'objets
    /// charges sur le thread principal. Dans ce projet, ou Resources/ pese
    /// 13 Go, ce parcours dure des secondes.
    ///
    /// Mesure du 2026-08-18, sonde GPU, au dechargement de 18_24 :
    ///
    ///     regime etabli      10-12 ms par image
    ///     au balayage        pic a 2446 ms
    ///     puis              pic a 23614 ms, et retrait du peripherique
    ///
    /// Windows declenche son TDR apres environ deux secondes sans reponse du
    /// GPU ; les 23 secondes sont la duree de recuperation, donc la
    /// consequence. Le balayage detruit en outre des ressources GPU pendant que
    /// le rendu peut encore les referencer - ce que D3D12 tolere beaucoup moins
    /// que D3D11, d'ou l'apparition du symptome au changement d'API.
    ///
    /// LE COMPROMIS RETENU
    /// La fuite que ce balayage corrige est reelle : sans lui, c'est le nombre
    /// de regions TRAVERSEES qui compte, pas le nombre chargees. Mais elle est
    /// lente, et la memoire graphique tient a 774 Mo sur un budget de 5152.
    /// On laisse donc monter jusqu'a un seuil au lieu de payer l'a-coup a
    /// chaque region franchie.
    private void ReleaseUnusedAssets()
    {
        if (_sweeping)
        {
            return;
        }

        _unloadsSinceSweep++;

        if (Time.unscaledTime - _lastSweepAt < _sweepMinInterval)
        {
            return;
        }

        long graphicsMb =
            UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024 * 1024);

        bool overBudget = graphicsMb >= _sweepAboveMb;
        bool tooManyUnloads = _unloadsSinceSweep >= _sweepEveryUnloads;

        if (!overBudget && !tooManyUnloads)
        {
            return;
        }

        if (_verbose)
        {
            Debug.Log($"[Streaming] Balayage declenche : {graphicsMb} Mo graphiques, "
                      + $"{_unloadsSinceSweep} dechargement(s) depuis le dernier. "
                      + "Un a-coup est attendu.");
        }

        _lastSweepAt = Time.unscaledTime;
        _unloadsSinceSweep = 0;
        _sweeping = true;

        AsyncOperation sweep = Resources.UnloadUnusedAssets();
        if (sweep == null)
        {
            _sweeping = false;
            return;
        }

        sweep.completed += _ =>
        {
            _sweeping = false;

            if (_verbose)
            {
                Debug.Log("[Streaming] Assets orphelins liberes.");
            }
        };
    }

    private bool IsAllowed(string name)
    {
        return _allowed == null || _allowed.Contains(name);
    }

    /// Regions actuellement en memoire, pour le diagnostic.
    public IEnumerable<string> LoadedRegions => _loaded;
}
