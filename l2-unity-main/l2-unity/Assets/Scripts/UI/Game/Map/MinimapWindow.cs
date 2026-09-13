using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Minimap ronde : un morceau de la carte du monde autour du joueur, tournant
// avec la camera, la fleche du joueur fixe au centre.
//
// Le principe : un conteneur rond qui masque ce qui depasse, une grande image
// de carte placee dedans de facon que le pixel du joueur tombe au centre, et
// une rotation du conteneur intermediaire. Tourner le conteneur autour de son
// centre revient donc a tourner la carte autour du joueur.
public class MinimapWindow : L2Window
{
    private const string StylePath = "Data/UI/_Elements/Game/MinimapWindow/MinimapWindow";
    private const string MapTexturePath = "Data/UI/Assets/Map/WorldMap";

    // Pixels de carte affiches pour un pixel d'ecran. Plus la valeur est
    // grande, plus on voit large.
    private static readonly float[] ZoomLevels = { 0.75f, 1.25f, 2f, 3f };

    private VisualElement _clip;
    private VisualElement _rotor;
    private VisualElement _mapImage;
    private VisualElement _arrow;
    private VisualElement _pin;
    private VisualElement _sharedPin;
    private VisualElement _clockIcon;
    private Label _clockLabel;
    private Label _zoneLabel;
    private Vector3 _lastZoneCheck = new Vector3(float.MaxValue, 0f, 0f);
    private VisualElement _markerLayer;
    private readonly List<VisualElement> _dots = new List<VisualElement>();
    private int _usedDots;
    private Texture2D _mapTexture;
    private int _zoomIndex = 1;

    // Decalage entre la texture et le cap, trouve au test : la fleche est
    // deja dans le bon sens.
    private const float ArrowTextureOffset = 0f;

    /// Moitie de la taille d'un point, pour le centrer sur sa position.
    private const float DotRadius = 6f;
    private const float PinMargin = 11f;

    private static MinimapWindow _instance;
    public static MinimapWindow Instance { get { return _instance; } }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    protected override void LoadAssets()
    {
        _windowTemplate = LoadAsset(StylePath);
    }

    protected override void InitWindow(VisualElement root)
    {
        base.InitWindow(root);

        // Le modele ne garde que son premier element : la feuille de style
        // declaree dans l'UXML est perdue, on la rattache a la main.
        StyleSheet sheet = Resources.Load<StyleSheet>(StylePath);
        if (sheet != null)
        {
            _windowEle.styleSheets.Add(sheet);
        }

        _clip = GetElementById("Clip");
        _rotor = GetElementById("Rotor");
        _mapImage = GetElementById("MapImage");
        _arrow = GetElementById("Arrow");
        _markerLayer = GetElementById("Markers");
        _pin = new VisualElement();
        _pin.AddToClassList("minimap-pin");
        _pin.pickingMode = PickingMode.Ignore;
        _markerLayer.Add(_pin);

        _sharedPin = new VisualElement();
        _sharedPin.AddToClassList("minimap-pin");
        _sharedPin.AddToClassList("shared");
        _sharedPin.pickingMode = PickingMode.Ignore;
        _markerLayer.Add(_sharedPin);

        _clockIcon = GetElementById("ClockIcon");
        _clockLabel = GetLabelById("ClockLabel");
        _zoneLabel = GetLabelById("ZoneLabel");

        _mapTexture = Resources.Load<Texture2D>(MapTexturePath);
        if (_mapTexture != null)
        {
            _mapImage.style.backgroundImage = new StyleBackground(_mapTexture);
        }
        else
        {
            Debug.LogWarning("[Minimap] Carte introuvable : " + MapTexturePath);
        }

        RegisterZoom("ZoomInBtn", -1);
        RegisterZoom("ZoomOutBtn", 1);
    }

    // Points du groupe et de quete, poses par rapport au joueur. Les elements
    // sont recycles : un point qui sort du disque est masque, pas detruit.
    private void UpdateMarkers(Vector2 playerPixel, float scale, float clipSize)
    {
        if (_markerLayer == null)
        {
            return;
        }

        _usedDots = 0;
        int selfId = PlayerEntity.Instance != null ? PlayerEntity.Instance.Identity.Id : 0;

        foreach (KeyValuePair<int, Vector3> member in MapMarkers.Party)
        {
            if (member.Key != selfId)
            {
                PlaceDot(member.Value, playerPixel, scale, clipSize, "minimap-dot-party");
            }
        }

        for (int i = 0; i < MapMarkers.Quest.Count; i++)
        {
            PlaceDot(MapMarkers.Quest[i], playerPixel, scale, clipSize, "minimap-dot-quest");
        }

        PlacePin(playerPixel, scale, clipSize);

        for (int i = _usedDots; i < _dots.Count; i++)
        {
            _dots[i].style.display = DisplayStyle.None;
        }
    }

    // Le marqueur personnel reste visible hors de portee : rabattu sur le bord
    // du disque, il donne la direction a suivre.
    private void PlacePin(Vector2 playerPixel, float scale, float clipSize)
    {
        ShowPin(_pin, MapMarkers.HasPersonal, MapMarkers.Personal, playerPixel, scale, clipSize);
        ShowPin(_sharedPin, MapMarkers.HasShared, MapMarkers.Shared, playerPixel, scale, clipSize);
    }

    private void ShowPin(VisualElement pin, bool visible, Vector3 world, Vector2 playerPixel, float scale, float clipSize)
    {
        if (pin == null)
        {
            return;
        }

        if (!visible)
        {
            pin.style.display = DisplayStyle.None;
            return;
        }

        Vector2 offset = MapProjection.WorldToMap(world) * scale - playerPixel;

        // Le radar est carre : on ramene le point sur le bord du carre, pas
        // sur un cercle, sinon il flotte loin du bord dans les coins.
        float limit = clipSize * 0.5f - PinMargin;
        float ratio = 1f;
        if (Mathf.Abs(offset.x) > limit)
        {
            ratio = Mathf.Min(ratio, limit / Mathf.Abs(offset.x));
        }

        if (Mathf.Abs(offset.y) > limit)
        {
            ratio = Mathf.Min(ratio, limit / Mathf.Abs(offset.y));
        }

        offset *= ratio;

        pin.style.display = DisplayStyle.Flex;
        pin.style.left = Mathf.Clamp(clipSize * 0.5f + offset.x - 10f, 0f, clipSize - 20f);
        pin.style.top = Mathf.Clamp(clipSize * 0.5f + offset.y - 20f, 0f, clipSize - 20f);
    }

    private void PlaceDot(Vector3 world, Vector2 playerPixel, float scale, float clipSize, string colorClass)
    {
        Vector2 offset = MapProjection.WorldToMap(world) * scale - playerPixel;
        float x = clipSize * 0.5f + offset.x;
        float y = clipSize * 0.5f + offset.y;

        if (x < 0f || y < 0f || x > clipSize || y > clipSize)
        {
            return;
        }

        VisualElement dot = RentDot();
        dot.RemoveFromClassList("minimap-dot-party");
        dot.RemoveFromClassList("minimap-dot-quest");
        dot.AddToClassList(colorClass);
        dot.style.display = DisplayStyle.Flex;
        dot.style.left = x - DotRadius;
        dot.style.top = y - DotRadius;
    }

    private VisualElement RentDot()
    {
        if (_usedDots < _dots.Count)
        {
            return _dots[_usedDots++];
        }

        VisualElement dot = new VisualElement();
        dot.AddToClassList("minimap-dot");
        dot.pickingMode = PickingMode.Ignore;
        _markerLayer.Add(dot);
        _dots.Add(dot);
        _usedDots++;
        return dot;
    }

    private void RegisterZoom(string id, int direction)

    {
        Button button = GetElementById(id) as Button;
        if (button == null)
        {
            return;
        }

        button.AddManipulator(new ButtonClickSoundManipulator(button));
        button.RegisterCallback<MouseUpEvent>(evt =>
        {
            _zoomIndex = Mathf.Clamp(_zoomIndex + direction, 0, ZoomLevels.Length - 1);
        }, TrickleDown.TrickleDown);
    }

    protected override IEnumerator BuildWindow(VisualElement root)
    {
        InitWindow(root);

        yield return new WaitForEndOfFrame();

        // Pas de WindowLoadComplete : fenetre ajoutee par code dans L2GameUI.
    }

    private void LateUpdate()
    {
        if (_mapImage == null || _mapTexture == null || PlayerEntity.Instance == null)
        {
            return;
        }

        float clipSize = _clip.resolvedStyle.width;
        if (clipSize <= 0f)
        {
            return;
        }

        // Echelle : combien un pixel de carte occupe a l'ecran.
        float scale = 1f / ZoomLevels[_zoomIndex];
        _mapImage.style.width = MapProjection.MapWidth * scale;
        _mapImage.style.height = MapProjection.MapHeight * scale;

        Vector3 world = PlayerEntity.Instance.transform.position;
        Vector2 pixel = MapProjection.WorldToMap(world) * scale;

        // Le pixel du joueur vient au centre du disque.
        _mapImage.style.left = clipSize * 0.5f - pixel.x;
        _mapImage.style.top = clipSize * 0.5f - pixel.y;

        // Carte fixe, toujours orientee au nord : seule la fleche tourne, et
        // elle suit la CAMERA, pas le personnage - elle montre donc ce qu'on
        // regarde, comme le radar du client d'origine.
        UpdateMarkers(pixel, scale, clipSize);

        Camera camera = Camera.main;
        if (camera != null)
        {
            _arrow.style.rotate = new StyleRotate(new Rotate(camera.transform.eulerAngles.y + ArrowTextureOffset));
        }

        UpdateClock();
        UpdateZoneName(world);
    }

    // Le lieu n'est recalcule que tous les 20 metres : la recherche parcourt
    // les deux cents zones du catalogue.
    private void UpdateZoneName(Vector3 world)
    {
        if (_zoneLabel == null || (world - _lastZoneCheck).sqrMagnitude < 400f)
        {
            return;
        }

        _lastZoneCheck = world;
        _zoneLabel.text = MapHuntingZones.NearestName(world);
    }

    // Heure du jeu, soleil ou lune, comme la carte du client d'origine.
    private void UpdateClock()
    {
        if (_clockLabel == null || WorldClock.Instance == null)
        {
            return;
        }

        System.TimeSpan time = WorldClock.Instance.TimeOfDay;
        int hour = time.Hours % 12;
        if (hour == 0)
        {
            hour = 12;
        }

        _clockLabel.text = string.Format("{0} {1:00}:{2:00}", time.Hours < 12 ? "AM" : "PM", hour, time.Minutes);

        if (_clockIcon != null)
        {
            _clockIcon.EnableInClassList("night", WorldClock.Instance.IsNightTime());
        }
    }
}
