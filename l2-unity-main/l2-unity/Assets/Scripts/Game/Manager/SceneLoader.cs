using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [SerializeField] private string _menuScene = "Menu";
    [SerializeField] private string _lobbyScene = "l2_lobby";
    [SerializeField] private string _gameScene = "Game";
    [SerializeField] private List<SceneListObject> _mapList = new List<SceneListObject>();
    /// Regions AUTORISEES, derivees des cases cochees. Ce n'est plus la liste
    /// de ce qui est charge - c'est le streaming qui en decide - mais celle de
    /// ce qui a le droit de l'etre.
    [SerializeField] private List<string> _mapsToLoad = new List<string>();

    [Tooltip("Distance au bord d'une region en deca de laquelle elle est chargee "
             + "avant l'apparition du joueur. Doit valoir la meme chose que le "
             + "Preload Distance du RegionStreamer.")]
    [SerializeField] private float _initialPreloadDistance = 200f;

    private int _totalLoadedScenes = 0;
    private List<string> _initialWindow = new List<string>();

    public string GameScene { get { return _gameScene; } }

    public static SceneLoader _instance;
    public static SceneLoader Instance { get { return _instance; } }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(this);
        }

        FillMapsToLoadList();
    }

    private void FillMapsToLoadList()
    {
        _mapsToLoad = new List<string>();
        for (int i = 0; i < _mapList.Count; i++)
        {
            var map = _mapList[i];

            if (!map.enabled)
            {
                continue;
            }

            _mapsToLoad.Add(map.name);
        }
    }

    public void LoadMenu()
    {
        // GameManager.Instance.OnStartingGame();
        SwitchScene(_menuScene, (AsyncOperation o) =>
        {
            GameManager.Instance.NotifyEvent(GameEvent.LOADING_STARTED);

            LoadScene(_lobbyScene, (AsyncOperation operation) =>
            {
                // -> Loading complete in UI script to avoid null references
                GameManager.Instance.NotifyEvent(GameEvent.WORLD_LOADED);
            });
        });
    }

    /// Charge la fenetre initiale autour du point d'apparition, puis passe la
    /// main au streaming.
    ///
    /// AVANT
    /// Toutes les regions cochees etaient chargees d'un coup et ne l'etaient
    /// jamais plus. Avec 5 cases cochees ca tenait ; avec 153 le jeu ne demarre
    /// pas.
    ///
    /// POURQUOI ON PEUT CALCULER LA FENETRE ICI
    /// La position d'apparition est connue AVANT le chargement : le serveur
    /// l'envoie a la selection du personnage (OnCharSelected renseigne
    /// GameClient.PlayerInfo), et LoadGame n'est appelee qu'ensuite, depuis
    /// EnteringWorldState.
    ///
    /// C'est ce qui garantit que le joueur apparait dans une region deja
    /// chargee, et jamais dans le vide.
    public void LoadGame()
    {
        _totalLoadedScenes = 0;

        List<string> initial = ResolveInitialWindow();

        SwitchScene(_gameScene, ((AsyncOperation o) =>
        {
            GameManager.Instance.NotifyEvent(GameEvent.LOADING_STARTED);

            _initialWindow = initial;

            if (initial.Count == 0)
            {
                // Aucune region autour du point d'apparition : on laisse le jeu
                // demarrer plutot que de rester bloque sur l'ecran de
                // chargement. Le streaming corrigera des que le joueur bougera.
                Debug.LogError("[Streaming] Aucune region a charger autour du point d'apparition. "
                               + "Verifiez la liste blanche du SceneLoader.");
                StartCoroutine(StartGame());
                return;
            }

            foreach (string map in initial)
            {
                LoadScene(map, (AsyncOperation operation) =>
                {
                    OnInitialWorldload(operation, map);
                });
            }
        }));
    }

    /// Les regions a charger avant l'apparition du joueur : un bloc centre sur
    /// son point de depart, filtre par la liste blanche.
    private List<string> ResolveInitialWindow()
    {
        var window = new List<string>();

        Vector3 spawn = GameClient.Instance != null
            ? GameClient.Instance.PlayerInfo.Identity.Position
            : Vector3.zero;

        RegionGrid.RegionAt(spawn, out int column, out int row);

        var allowed = new HashSet<string>(_mapsToLoad);

        // Meme regle que le streamer, horizon du brouillard compris : sans
        // cela la fenetre initiale serait la plus etroite des deux, et les
        // regions manquantes se chargeraient juste APRES l'ecran de
        // chargement - c'est-a-dire pendant que le joueur regarde.
        float horizon = RegionStreamer.HorizonDistance(_initialPreloadDistance);
        int radius = Mathf.Max(1, Mathf.CeilToInt(horizon / RegionGrid.RegionSize));

        for (int c = column - radius; c <= column + radius; c++)
        {
            for (int r = row - radius; r <= row + radius; r++)
            {
                // Meme critere que le streamer - la distance au bord - sinon le
                // chargement initial poserait des regions qu'il dechargerait
                // aussitot, ou en oublierait qu'il chargerait dans la foulee.
                if (RegionGrid.DistanceToRegion(spawn, c, r) > horizon)
                {
                    continue;
                }

                string name = RegionGrid.NameOf(c, r);

                // La liste cochee ne decide plus de CE QUI est charge, mais de
                // ce qui est CHARGEABLE : elle tient a l'ecart les regions hors
                // des bornes du serveur (colonne 15, rangee 26).
                if (allowed.Contains(name))
                {
                    window.Add(name);
                }
            }
        }

        Debug.Log($"[Streaming] Apparition en {RegionGrid.NameOf(column, row)} : "
                  + $"{window.Count} region(s) chargee(s) d'emblee.");

        return window;
    }

    public void SwitchScene(string sceneName, Action<AsyncOperation> p)
    {
        if (SceneManager.GetActiveScene().name != sceneName)
        {
            Debug.Log("Switching to scene " + sceneName);
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            asyncLoad.completed += p;
        }
        else
        {
            Debug.Log("Skipping scene switch " + sceneName);
            AsyncOperation dummyOperation = new AsyncOperation();
            p.Invoke(dummyOperation);
        }
    }

    private void LoadScene(string sceneName, Action<AsyncOperation> p)
    {
        Debug.Log("Loading scene " + sceneName);

        // Does the scene need to be loaded ?
        if (!SceneManager.GetSceneByName(sceneName).IsValid())
        {
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            asyncLoad.completed += p;
        }
        else
        {
            Debug.Log("Skipping scene load " + sceneName);
            AsyncOperation dummyOperation = new AsyncOperation();
            p.Invoke(dummyOperation);
        }
    }

    private void UnloadScene(string sceneName)
    {
        Debug.Log("Unoading scene " + sceneName);

        if (!SceneManager.GetSceneByName(sceneName).IsValid())
        {
            return;
        }
        else
        {
            SceneManager.UnloadSceneAsync(sceneName);
        }
    }

    private void OnInitialWorldload(AsyncOperation operation, string sceneName)
    {
        Debug.Log("Initial scene " + sceneName + " loaded. " + "Load count: " + ++_totalLoadedScenes);

        // On attend la FENETRE initiale, pas la liste des regions autorisees :
        // celle-ci en compte 140, on n'en charge que neuf.
        if (_totalLoadedScenes >= _initialWindow.Count)
        {
            StartCoroutine(StartGame());
        }
    }

    IEnumerator StartGame()
    {
        yield return new WaitForSeconds(.3f); //TODO: wait for everything to be loaded instead of waitforseconds

        // Le streaming prend le relais : a partir d'ici, c'est lui qui charge et
        // decharge selon les deplacements du joueur.
        if (RegionStreamer.Instance != null)
        {
            RegionStreamer.Instance.Initialize(_mapsToLoad, _initialWindow);
        }
        else
        {
            Debug.LogWarning("[Streaming] Aucun RegionStreamer dans la scene : le monde restera "
                             + "limite a la fenetre initiale.");
        }

        Debug.LogWarning("All scenes loaded, sending LoadWorld packet.");

        if (World.Instance != null && !World.Instance.OfflineMode)
        {
            GameManager.Instance.NotifyEvent(GameEvent.LOADING_COMPLETE);
        }
    }
}
