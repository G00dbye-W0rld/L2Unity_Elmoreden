using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Carte du monde : l'image d'Interlude, une grille de reperage, la position du
// joueur et celles du groupe, plus la liste des villes avec leur plan.
//
// Le contenu est glisse dans l'emplacement du modele de fenetre : un enfant
// ajoute a ce modele en devient le voisin, pas le contenu.
public class WorldMapWindow : L2PopupWindow
{
    private const string StylePath = "Data/UI/_Elements/Game/WorldMapWindow/WorldMapWindow";
    private const string MapTexturePath = "Data/UI/Assets/Map/WorldMapInterlude";
    private const string PlanFolder = "Data/UI/Assets/Map/Towns/";
    private const string DungeonFolder = "Data/UI/Assets/Map/Dungeons/";

    private static readonly float[] ZoomLevels = { 0.5f, 0.75f, 1f, 1.5f, 2f };
    private const float MinWidth = 520f;
    private const float MaxWidth = 1600f;
    private const float MinHeight = 400f;
    private const float MaxHeight = 1100f;
    private const float RulerSize = 15f;
    private const float PlaceIconDrop = 14f;
    // La fleche de la carte pointe au nord dans sa texture, celle du radar a
    // l'est : d'ou le quart de tour d'ecart entre les deux.
    private const float ArrowTextureOffset = 90f;
    private const string AllChoice = "Tous";

    /// Une region du monde, en unites Unity : la maille de la grille.
    private const float RegionSize = 624.1524f;

    private VisualElement _viewport;
    private VisualElement _mapImage;
    private VisualElement _gridLayer;
    private VisualElement _placeLayer;
    private VisualElement _selfArrow;
    private Button _pin;
    private VisualElement _sharedPin;
    private VisualElement _markerLayer;
    private VisualElement _columnRuler;
    private VisualElement _rowRuler;
    private VisualElement _planLayer;
    private VisualElement _planImage;
    private Texture2D _planTexture;
    private float _planZoom = 1f;
    private Vector2 _planPan;
    private bool _planDragging;
    private Vector2 _planDragStart;
    private Vector2 _planPanStart;
    private ScrollView _zoneList;
    private DropdownField _territoryFilter;
    private DropdownField _levelFilter;
    private Label _hoverLabel;
    private Texture2D _mapTexture;

    private readonly List<VisualElement> _dots = new List<VisualElement>();
    private readonly List<Button> _placeButtons = new List<Button>();
    private readonly List<Vector3> _placeWorlds = new List<Vector3>();
    private readonly List<Label> _columnLabels = new List<Label>();
    private readonly List<Label> _rowLabels = new List<Label>();
    private int _usedDots;
    private int _zoomIndex = 2;
    private Vector2 _pan;
    private bool _showGrid = true;
    private bool _dragging;
    private bool _hasPendingCenter;
    private Vector3 _pendingCenter;
    private Vector2 _dragStart;
    private Vector2 _panStart;

    private static WorldMapWindow _instance;
    public static WorldMapWindow Instance { get { return _instance; } }

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

    private float Zoom { get { return ZoomLevels[_zoomIndex]; } }

    protected override void InitWindow(VisualElement root)
    {
        base.InitWindow(root);

        StyleSheet sheet = Resources.Load<StyleSheet>(StylePath);
        if (sheet != null)
        {
            _windowEle.styleSheets.Add(sheet);
        }
        else
        {
            Debug.LogError("[Carte] Feuille de style introuvable : " + StylePath + " - la fenetre s'affichera sans mise en forme.");
            _windowEle.style.width = 880f;
            _windowEle.style.height = 640f;
        }

        VisualElement dragArea = GetElementByClass("drag-area");
        dragArea.AddManipulator(new DragManipulator(dragArea, _windowEle, this));
        RegisterCloseWindowEvent("btn-close-frame");
        RegisterClickWindowEvent(_windowEle, dragArea);

        _viewport = GetElementById("Viewport");
        _mapImage = GetElementById("MapImage");
        _gridLayer = GetElementById("GridLayer");
        _placeLayer = GetElementById("PlaceLayer");
        _markerLayer = GetElementById("MarkerLayer");
        _columnRuler = GetElementById("ColumnRuler");
        _rowRuler = GetElementById("RowRuler");
        _planLayer = GetElementById("PlanLayer");
        _planImage = GetElementById("PlanImage");
        _zoneList = GetElementById("ZoneList") as ScrollView;
        _territoryFilter = GetElementById("TerritoryFilter") as DropdownField;
        _levelFilter = GetElementById("LevelFilter") as DropdownField;
        _hoverLabel = GetLabelById("HoverLabel");

        _mapTexture = Resources.Load<Texture2D>(MapTexturePath);
        if (_mapTexture != null)
        {
            _mapImage.style.backgroundImage = new StyleBackground(_mapTexture);
        }
        else
        {
            Debug.LogWarning("[Carte] Image introuvable : " + MapTexturePath);
        }

        BuildFilters();
        BuildZoneList();
        BuildPlaceButtons();
        BuildSelfArrow();
        BuildPin();
        RegisterTools();
        RegisterViewportInput();
        RegisterResize();

        _viewport.RegisterCallback<GeometryChangedEvent>(evt => OnViewportResized());

        // Les elements sous le curseur changent a chaque image : si l'un d'eux
        // disparait, la fenetre cesse d'etre survolee et le clic suivant part
        // dans le monde. On reaffirme le survol a chaque mouvement.
        _windowEle.RegisterCallback<PointerMoveEvent>(evt => L2GameUI.Instance.MouseOverUI = true);
    }


    // Navigateur de zones : le catalogue du client, filtre par territoire et
    // par niveau, comme l'onglet "Hunting Zone" d'origine.
    private void BuildFilters()
    {
        if (_territoryFilter == null || _levelFilter == null)
        {
            return;
        }

        List<string> territories = new List<string> { AllChoice };
        foreach (MapHuntingZones.Territory territory in MapHuntingZones.Territories)
        {
            territories.Add(territory.French);
        }

        _territoryFilter.choices = territories;
        _territoryFilter.index = 0;
        _territoryFilter.RegisterValueChangedCallback(evt => BuildZoneList());

        List<string> levels = new List<string> { AllChoice };
        for (int low = 1; low < 80; low += 10)
        {
            levels.Add(low + " - " + (low + 9));
        }

        _levelFilter.choices = levels;
        _levelFilter.index = 0;
        _levelFilter.RegisterValueChangedCallback(evt => BuildZoneList());
    }

    private void BuildZoneList()
    {
        if (_zoneList == null)
        {
            return;
        }

        _zoneList.Clear();

        int territory = _territoryFilter != null && _territoryFilter.index > 0
            ? MapHuntingZones.Territories[_territoryFilter.index - 1].Id
            : 0;

        int lowest = _levelFilter != null && _levelFilter.index > 0 ? _levelFilter.index * 10 - 9 : 0;

        foreach (MapHuntingZones.Zone entry in MapHuntingZones.Zones)
        {
            MapHuntingZones.Zone zone = entry;

            if (territory != 0 && zone.Territory != territory)
            {
                continue;
            }

            if (lowest != 0 && (zone.Level < lowest || zone.Level > lowest + 9))
            {
                continue;
            }

            Button row = new Button();
            row.text = zone.Level > 0 ? zone.French + "  (" + zone.Level + ")" : zone.French;
            row.tooltip = zone.Name + " - " + MapHuntingZones.Label(zone.Type) + " - " + MapHuntingZones.TerritoryName(zone.Territory);
            row.AddToClassList("worldmap-zone-btn");
            row.AddManipulator(new ButtonClickSoundManipulator(row));
            row.RegisterCallback<MouseUpEvent>(evt => CenterOn(zone.World), TrickleDown.TrickleDown);

            _zoneList.Add(row);
        }
    }

    // Le "+" natif du jeu pose sur chaque ville et chaque donjon, comme sur la
    // carte d'origine : il recentre la vue puis ouvre le plan.
    private void BuildPlaceButtons()
    {
        if (_placeLayer == null)
        {
            return;
        }

        AddPlaceButtons(MapPlaces.Towns, PlanFolder);
        AddPlaceButtons(MapPlaces.Dungeons, DungeonFolder);
    }

    private void AddPlaceButtons(MapPlaces.Place[] places, string folder)
    {
        foreach (MapPlaces.Place entry in places)
        {
            MapPlaces.Place place = entry;

            Button button = new Button();
            button.tooltip = place.Name;
            button.AddToClassList("worldmap-place-btn");
            button.AddManipulator(new ButtonClickSoundManipulator(button));
            button.RegisterCallback<MouseUpEvent>(evt =>
            {
                CenterOn(place.World);
                ShowPlan(folder, place.Plan);
            }, TrickleDown.TrickleDown);

            _placeLayer.Add(button);
            _placeButtons.Add(button);
            _placeWorlds.Add(place.World);
        }
    }

    private void BuildSelfArrow()
    {
        if (_markerLayer == null)
        {
            return;
        }

        _selfArrow = new VisualElement();
        _selfArrow.AddToClassList("worldmap-arrow");
        _selfArrow.pickingMode = PickingMode.Ignore;
        _markerLayer.Add(_selfArrow);
    }

    // Epingle du marqueur personnel : un clic dessus l'efface.
    private void BuildPin()
    {
        if (_placeLayer == null)
        {
            return;
        }

        _pin = new Button();
        _pin.tooltip = "Marqueur - clic pour effacer";
        _pin.AddToClassList("worldmap-pin");
        _pin.AddManipulator(new ButtonClickSoundManipulator(_pin));
        // Seul le clic gauche efface : le clic droit vient de poser l'epingle,
        // son relachement tombe dessus et l'effacait aussitot.
        _pin.RegisterCallback<MouseUpEvent>(evt =>
        {
            if (evt.button == 0)
            {
                MapMarkers.ClearPersonal();
            }
        }, TrickleDown.TrickleDown);
        _placeLayer.Add(_pin);

        _sharedPin = new VisualElement();
        _sharedPin.AddToClassList("worldmap-pin");
        _sharedPin.AddToClassList("shared");
        _sharedPin.pickingMode = PickingMode.Ignore;
        _placeLayer.Add(_sharedPin);
    }

    // Le groupe recoit le meme point : le serveur le rediffuse en marqueur de
    // radar. Hors groupe le paquet est ignore, le marqueur reste local.
    private static void SharePin(Vector3 world)
    {
        if (GameClient.Instance == null || GameClient.Instance.ClientPacketHandler == null)
        {
            return;
        }

        int height = PlayerEntity.Instance != null ? Mathf.RoundToInt(PlayerEntity.Instance.transform.position.y * 52.5f) : 0;
        GameClient.Instance.ClientPacketHandler.SendPartyMarker(Mathf.RoundToInt(world.z * 52.5f), Mathf.RoundToInt(world.x * 52.5f), height);
    }

    private void PlacePin()
    {
        ShowPin(_pin, MapMarkers.HasPersonal, MapMarkers.Personal);
        ShowPin(_sharedPin, MapMarkers.HasShared, MapMarkers.Shared);
    }

    private void ShowPin(VisualElement pin, bool visible, Vector3 world)
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

        Vector2 pixel = MapProjection.World.WorldToMap(world) * Zoom + _pan;
        pin.style.display = DisplayStyle.Flex;
        pin.style.left = pixel.x - 16f;
        pin.style.top = pixel.y - 30f;
    }

    // Le "+" se pose un peu sous le nom du lieu, ecrit au-dessus de lui sur
    // l'image : centre dessus, il le chevauchait.
    private void PositionPlaceButtons()
    {
        for (int i = 0; i < _placeButtons.Count; i++)
        {
            Vector2 place = MapProjection.World.WorldToMap(_placeWorlds[i]) + new Vector2(0f, PlaceIconDrop);
            Vector2 pixel = place * Zoom + _pan;
            _placeButtons[i].style.left = pixel.x - 13f;
            _placeButtons[i].style.top = pixel.y - 13f;
        }
    }

    private void RegisterTools()
    {
        RegisterTool("ZoomInBtn", () => SetZoom(_zoomIndex + 1));
        RegisterTool("ZoomOutBtn", () => SetZoom(_zoomIndex - 1));
        RegisterTool("CenterBtn", CenterOnPlayer);
        RegisterTool("TargetBtn", CenterOnTarget);
        RegisterTool("GridBtn", () =>
        {
            _showGrid = !_showGrid;
            _gridLayer.style.display = _showGrid ? DisplayStyle.Flex : DisplayStyle.None;
            GetElementById("GridBtn").EnableInClassList("active", _showGrid);
            ApplyLayout(false);
        });
    }

    private void RegisterTool(string id, System.Action action)
    {
        Button button = GetElementById(id) as Button;
        if (button == null)
        {
            return;
        }

        button.AddManipulator(new ButtonClickSoundManipulator(button));
        button.RegisterCallback<MouseUpEvent>(evt => action(), TrickleDown.TrickleDown);
    }

    // Deplacement a la souris, molette pour zoomer, survol pour lire le lieu.
    private void RegisterViewportInput()
    {
        // Un clic sur un bouton de lieu ne doit pas demarrer un deplacement :
        // la capture du pointeur lui volerait son relachement.
        _viewport.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0 || evt.target != _viewport)
            {
                return;
            }

            _dragging = true;
            _dragStart = evt.position;
            _panStart = _pan;
            _viewport.CapturePointer(evt.pointerId);
        });

        _viewport.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (_dragging)
            {
                _pan = _panStart + (Vector2)evt.position - _dragStart;
                ApplyLayout(false);
            }

            UpdateHover(evt.localPosition);
        });

        _viewport.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (_dragging)
            {
                _dragging = false;
                _viewport.ReleasePointer(evt.pointerId);
                ApplyLayout();
            }
        });

        _viewport.RegisterCallback<WheelEvent>(evt =>
        {
            SetZoom(_zoomIndex - (int)Mathf.Sign(evt.delta.y));
            evt.StopPropagation();
        });

        RegisterPlanInput();
    }

    // Molette pour zoomer dans le plan, souris pour s'y deplacer, bouton pour
    // revenir a la carte.
    private void RegisterPlanInput()
    {
        RegisterTool("PlanCloseBtn", HidePlan);

        _planLayer.RegisterCallback<WheelEvent>(evt =>
        {
            Vector2 focus = _planLayer.WorldToLocal(evt.mousePosition);
            SetPlanZoom(_planZoom * (evt.delta.y < 0f ? 1.25f : 0.8f), focus);
            evt.StopPropagation();
        });

        _planLayer.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0 || evt.target != _planLayer)
            {
                return;
            }

            _planDragging = true;
            _planDragStart = evt.position;
            _planPanStart = _planPan;
            _planLayer.CapturePointer(evt.pointerId);
        });

        _planLayer.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (_planDragging)
            {
                _planPan = _planPanStart + (Vector2)evt.position - _planDragStart;
                ApplyPlanLayout();
            }
        });

        _planLayer.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (_planDragging)
            {
                _planDragging = false;
                _planLayer.ReleasePointer(evt.pointerId);
            }
        });
    }

    private void RegisterResize()
    {
        VisualElement right = GetElementById("ResizeRight");
        if (right != null)
        {
            right.AddManipulator(new EdgeResizeManipulator(right, _windowEle, EdgeResizeManipulator.Edge.Right, MinWidth, MaxWidth, 0f, 0f));
        }

        VisualElement bottom = GetElementById("ResizeBottom");
        if (bottom != null)
        {
            bottom.AddManipulator(new EdgeResizeManipulator(bottom, _windowEle, EdgeResizeManipulator.Edge.Bottom, MinHeight, MaxHeight, 0f, 0f));
        }
    }

    protected override IEnumerator BuildWindow(VisualElement root)
    {
        InitWindow(root);

        yield return new WaitForEndOfFrame();

        CenterWindow();
        HideWindow(true);

        // Pas de WindowLoadComplete : fenetre ajoutee par code dans L2GameUI.
    }

    public void Open()
    {
        ShowWindow();
        CenterOnPlayer();
    }

    public override void ToggleHideWindow()
    {
        if (_isWindowHidden)
        {
            Open();
        }
        else
        {
            HideWindow(false);
        }
    }

    // La taille reelle n'existe qu'apres une passe de layout : avant, elle vaut
    // NaN ou zero, et tout calcul de position propageait ces valeurs jusqu'aux
    // styles, ce qui faisait disparaitre la carte puis figeait la fenetre.
    private bool TryGetViewportSize(out Vector2 size)
    {
        size = Vector2.zero;

        if (_viewport == null)
        {
            return false;
        }

        float width = _viewport.resolvedStyle.width;
        float height = _viewport.resolvedStyle.height;

        if (float.IsNaN(width) || float.IsNaN(height) || width < 1f || height < 1f)
        {
            return false;
        }

        size = new Vector2(width, height);
        return true;
    }

    // La carte ne peut pas quitter le cadre : elle le couvre, ou elle y est
    // centree quand le zoom la rend plus petite que lui.
    private void ClampPan(Vector2 size)
    {
        Vector2 map = new Vector2(MapProjection.World.Width, MapProjection.World.Height) * Zoom;

        if (float.IsNaN(_pan.x) || float.IsInfinity(_pan.x))
        {
            _pan.x = 0f;
        }

        if (float.IsNaN(_pan.y) || float.IsInfinity(_pan.y))
        {
            _pan.y = 0f;
        }

        _pan.x = map.x > size.x ? Mathf.Clamp(_pan.x, size.x - map.x, 0f) : (size.x - map.x) * 0.5f;
        _pan.y = map.y > size.y ? Mathf.Clamp(_pan.y, size.y - map.y, 0f) : (size.y - map.y) * 0.5f;
    }

    private void SetZoom(int index)
    {
        int clamped = Mathf.Clamp(index, 0, ZoomLevels.Length - 1);
        if (clamped == _zoomIndex)
        {
            return;
        }

        if (!TryGetViewportSize(out Vector2 size))
        {
            _zoomIndex = clamped;
            return;
        }

        // Garder le centre du cadre au meme endroit de la carte.
        float ratio = ZoomLevels[clamped] / ZoomLevels[_zoomIndex];
        Vector2 center = size * 0.5f;
        _pan = center - (center - _pan) * ratio;
        _zoomIndex = clamped;

        ApplyLayout();
    }

    private void CenterOnPlayer()
    {
        if (PlayerEntity.Instance != null)
        {
            CenterOn(PlayerEntity.Instance.transform.position);
        }
    }

    private void CenterOnTarget()
    {
        if (TryGetTarget(out Vector3 target))
        {
            CenterOn(target);
        }
    }

    private void CenterOn(Vector3 world)
    {
        if (!TryGetViewportSize(out Vector2 size))
        {
            _pendingCenter = world;
            _hasPendingCenter = true;
            return;
        }

        _hasPendingCenter = false;
        _pan = size * 0.5f - MapProjection.World.WorldToMap(world) * Zoom;
        ApplyLayout();
    }

    private void OnViewportResized()
    {
        if (_hasPendingCenter)
        {
            CenterOn(_pendingCenter);
        }
        else
        {
            ApplyLayout();
        }
    }

    private void ApplyLayout()
    {
        ApplyLayout(true);
    }

    // La grille vit dans l'image : elle suit donc la carte pendant les
    // deplacements sans etre refaite. Les reperes, eux, restent colles aux
    // bords du cadre et sont repositionnes a chaque image.
    private void ApplyLayout(bool rebuildGrid)
    {
        if (_mapImage == null || !TryGetViewportSize(out Vector2 size))
        {
            return;
        }

        ClampPan(size);

        _mapImage.style.width = MapProjection.World.Width * Zoom;
        _mapImage.style.height = MapProjection.World.Height * Zoom;
        _mapImage.style.left = _pan.x;
        _mapImage.style.top = _pan.y;

        PositionPlaceButtons();
        UpdateRulers(size);

        if (rebuildGrid)
        {
            BuildGrid();
        }
    }

    /// Bornes du quadrillage : les regions couvertes par l'image de la carte.
    private void GridBounds(out int firstColumn, out int lastColumn, out int firstRow, out int lastRow)
    {
        MapProjection.Calibration map = MapProjection.World;

        RegionGrid.RegionAt(map.MapToWorld(Vector2.zero, 0f), out firstColumn, out firstRow);
        RegionGrid.RegionAt(map.MapToWorld(new Vector2(map.Width, map.Height), 0f), out lastColumn, out lastRow);
    }

    /// Colonne en lettre, A pour la premiere colonne de l'image.
    private static string ColumnLetter(int column, int firstColumn)
    {
        return ((char)('A' + Mathf.Clamp(column - firstColumn, 0, 25))).ToString();
    }

    /// Case au format lettre + numero, A1 en haut a gauche de la carte.
    private static string CellName(int column, int row, int firstColumn, int firstRow)
    {
        return ColumnLetter(column, firstColumn) + (row - firstRow + 1);
    }

    // Une ligne par frontiere de region, en pixels de la carte : reconstruite
    // au zoom seulement, une trentaine d'elements.
    private void BuildGrid()
    {
        if (_gridLayer == null)
        {
            return;
        }

        _gridLayer.Clear();

        MapProjection.Calibration map = MapProjection.World;
        GridBounds(out int firstColumn, out int lastColumn, out int firstRow, out int lastRow);

        for (int column = firstColumn; column <= lastColumn + 1; column++)
        {
            VisualElement line = new VisualElement();
            line.AddToClassList("worldmap-grid-line");
            line.pickingMode = PickingMode.Ignore;
            line.style.left = ColumnEdge(column) * Zoom;
            line.style.top = 0;
            line.style.width = 1;
            line.style.height = map.Height * Zoom;
            _gridLayer.Add(line);
        }

        for (int row = firstRow; row <= lastRow + 1; row++)
        {
            VisualElement line = new VisualElement();
            line.AddToClassList("worldmap-grid-line");
            line.pickingMode = PickingMode.Ignore;
            line.style.left = 0;
            line.style.top = RowEdge(row) * Zoom;
            line.style.width = map.Width * Zoom;
            line.style.height = 1;
            _gridLayer.Add(line);
        }
    }

    /// Bord gauche d'une colonne de regions, en pixels de la carte.
    private static float ColumnEdge(int column)
    {
        return MapProjection.World.WorldToMap(new Vector3(0f, 0f, RegionGrid.OriginOf(column, 0).y)).x;
    }

    /// Bord haut d'une rangee de regions, en pixels de la carte.
    private static float RowEdge(int row)
    {
        return MapProjection.World.WorldToMap(new Vector3(RegionGrid.OriginOf(0, row).x, 0f, 0f)).y;
    }

    // Lettres en haut, numeros a gauche : les reperes flottent sur le bord du
    // cadre et restent lisibles pendant que la carte glisse dessous.
    private void UpdateRulers(Vector2 size)
    {
        if (_columnRuler == null || _rowRuler == null)
        {
            return;
        }

        bool visible = _showGrid;
        _columnRuler.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        _rowRuler.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (!visible)
        {
            return;
        }

        GridBounds(out int firstColumn, out int lastColumn, out int firstRow, out int lastRow);

        int used = 0;
        for (int column = firstColumn; column <= lastColumn; column++)
        {
            float center = (ColumnEdge(column) + ColumnEdge(column + 1)) * 0.5f * Zoom + _pan.x;
            if (center < RulerSize || center > size.x)
            {
                continue;
            }

            Label label = RentRulerLabel(_columnRuler, _columnLabels, used++);
            label.text = ColumnLetter(column, firstColumn);
            label.style.left = center - 10f;
            label.style.top = 1f;
            label.style.width = 20f;
        }

        HideExtraLabels(_columnLabels, used);

        used = 0;
        for (int row = firstRow; row <= lastRow; row++)
        {
            float center = (RowEdge(row) + RowEdge(row + 1)) * 0.5f * Zoom + _pan.y;
            if (center < RulerSize || center > size.y)
            {
                continue;
            }

            Label label = RentRulerLabel(_rowRuler, _rowLabels, used++);
            label.text = (row - firstRow + 1).ToString();
            label.style.left = 0f;
            label.style.top = center - 7f;
            label.style.width = RulerSize;
        }

        HideExtraLabels(_rowLabels, used);
    }

    private Label RentRulerLabel(VisualElement parent, List<Label> pool, int index)
    {
        if (index < pool.Count)
        {
            pool[index].style.display = DisplayStyle.Flex;
            return pool[index];
        }

        Label label = new Label();
        label.AddToClassList("worldmap-ruler-label");
        label.pickingMode = PickingMode.Ignore;
        parent.Add(label);
        pool.Add(label);
        return label;
    }

    private static void HideExtraLabels(List<Label> pool, int used)
    {
        for (int i = used; i < pool.Count; i++)
        {
            pool[i].style.display = DisplayStyle.None;
        }
    }


    private void LateUpdate()
    {
        if (_isWindowHidden || _markerLayer == null)
        {
            return;
        }

        KeepMouseOverUI();
        UpdatePinInput();

        _usedDots = 0;

        PlaceSelfArrow();
        PlacePin();

        int selfId = PlayerEntity.Instance != null ? PlayerEntity.Instance.Identity.Id : 0;
        foreach (KeyValuePair<int, Vector3> member in MapMarkers.Party)
        {
            if (member.Key != selfId)
            {
                PlaceDot(member.Value, "worldmap-dot-party");
            }
        }

        for (int i = 0; i < MapMarkers.Quest.Count; i++)
        {
            PlaceDot(MapMarkers.Quest[i], "worldmap-dot-quest");
        }

        Vector3 target;
        if (TryGetTarget(out target))
        {
            PlaceDot(target, "worldmap-dot-target");
        }

        for (int i = _usedDots; i < _dots.Count; i++)
        {
            _dots[i].style.display = DisplayStyle.None;
        }
    }

    // Le survol se perd des qu'un element disparait sous le curseur ou qu'une
    // liste deroulante s'ouvre par-dessus : sans cela le clic suivant partait
    // dans le monde et le personnage se deplacait.
    private void KeepMouseOverUI()
    {
        if (L2GameUI.Instance == null || !TryGetPanelPosition(out Vector2 panel))
        {
            return;
        }

        if (_windowEle.worldBound.Contains(panel))
        {
            L2GameUI.Instance.MouseOverUI = true;
        }
    }

    private bool TryGetPanelPosition(out Vector2 panel)
    {
        panel = Vector2.zero;

        if (_windowEle == null || _windowEle.panel == null)
        {
            return false;
        }

        Vector2 screen = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        panel = RuntimePanelUtils.ScreenToPanel(_windowEle.panel, screen);
        return true;
    }

    // Le clic droit passe par les entrees du jeu : UI Toolkit ne le transmet
    // pas de maniere fiable a la fenetre.
    private void UpdatePinInput()
    {
        if (InputManager.Instance == null || !InputManager.Instance.RightClickDown || _viewport == null)
        {
            return;
        }

        if (_planLayer != null && _planLayer.style.display == DisplayStyle.Flex)
        {
            return;
        }

        if (!TryGetPanelPosition(out Vector2 panel) || !_viewport.worldBound.Contains(panel))
        {
            return;
        }

        Vector2 local = panel - _viewport.worldBound.position;
        Vector3 target = MapProjection.World.MapToWorld((local - _pan) / Zoom, 0f);

        MapMarkers.SetPersonal(target);
        SharePin(target);
    }

    private static bool TryGetTarget(out Vector3 position)
    {
        position = Vector3.zero;

        if (TargetManager.Instance == null || TargetManager.Instance.Target == null)
        {
            return false;
        }

        position = TargetManager.Instance.Target.transform.position;
        return true;
    }

    // La fleche du radar montre ou regarde la camera, comme sur la minimap.
    private void PlaceSelfArrow()
    {
        if (_selfArrow == null || PlayerEntity.Instance == null)
        {
            return;
        }

        Vector2 pixel = MapProjection.World.WorldToMap(PlayerEntity.Instance.transform.position) * Zoom + _pan;
        bool visible = TryGetViewportSize(out Vector2 size)
            && pixel.x >= 0f && pixel.y >= 0f && pixel.x <= size.x && pixel.y <= size.y;

        _selfArrow.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (!visible)
        {
            return;
        }

        _selfArrow.style.left = pixel.x - 13f;
        _selfArrow.style.top = pixel.y - 13f;

        Camera camera = Camera.main;
        if (camera != null)
        {
            _selfArrow.style.rotate = new StyleRotate(new Rotate(camera.transform.eulerAngles.y + ArrowTextureOffset));
        }
    }

    private void PlaceDot(Vector3 world, string colorClass)
    {
        Vector2 pixel = MapProjection.World.WorldToMap(world) * Zoom + _pan;

        if (!TryGetViewportSize(out Vector2 size) || pixel.x < 0f || pixel.y < 0f || pixel.x > size.x || pixel.y > size.y)
        {
            return;
        }

        VisualElement dot = RentDot();
        dot.RemoveFromClassList("worldmap-dot-party");
        dot.RemoveFromClassList("worldmap-dot-quest");
        dot.RemoveFromClassList("worldmap-dot-target");
        dot.AddToClassList(colorClass);
        dot.style.display = DisplayStyle.Flex;
        dot.style.left = pixel.x - 7f;
        dot.style.top = pixel.y - 7f;
    }

    private VisualElement RentDot()
    {
        if (_usedDots < _dots.Count)
        {
            return _dots[_usedDots++];
        }

        VisualElement dot = new VisualElement();
        dot.AddToClassList("worldmap-dot");
        dot.pickingMode = PickingMode.Ignore;
        _markerLayer.Add(dot);
        _dots.Add(dot);
        _usedDots++;
        return dot;
    }

    // Sous le curseur : la region et les coordonnees du jeu, pas celles d'Unity.
    private void UpdateHover(Vector2 local)
    {
        if (_hoverLabel == null)
        {
            return;
        }

        Vector3 world = MapProjection.World.MapToWorld((local - _pan) / Zoom, 0f);
        RegionGrid.RegionAt(world, out int column, out int row);
        GridBounds(out int firstColumn, out int _, out int firstRow, out int __);

        _hoverLabel.text = string.Format("{0} ({1})   {2:F0} , {3:F0}", CellName(column, row, firstColumn, firstRow), RegionGrid.NameOf(column, row), world.z * 52.5f, world.x * 52.5f);
    }

    // Le plan s'ouvre ajuste au cadre, puis se zoome a la molette et se
    // deplace a la souris, comme la carte elle-meme.
    private void ShowPlan(string folder, string planName)
    {
        if (string.IsNullOrEmpty(planName) || _planLayer == null || _planImage == null)
        {
            return;
        }

        Texture2D plan = Resources.Load<Texture2D>(folder + planName);
        if (plan == null)
        {
            Debug.LogWarning("[Carte] Plan introuvable : " + planName);
            return;
        }

        _planTexture = plan;
        _planImage.style.backgroundImage = new StyleBackground(plan);
        _planLayer.style.display = DisplayStyle.Flex;

        if (TryGetViewportSize(out Vector2 size))
        {
            _planZoom = Mathf.Min(size.x / plan.width, size.y / plan.height);
            _planPan = (size - new Vector2(plan.width, plan.height) * _planZoom) * 0.5f;
        }

        ApplyPlanLayout();
    }

    private void SetPlanZoom(float zoom, Vector2 focus)
    {
        if (_planTexture == null || !TryGetViewportSize(out Vector2 size))
        {
            return;
        }

        float fit = Mathf.Min(size.x / _planTexture.width, size.y / _planTexture.height);
        float clamped = Mathf.Clamp(zoom, fit, 4f);
        if (Mathf.Approximately(clamped, _planZoom))
        {
            return;
        }

        // Le point sous le curseur ne bouge pas pendant le zoom.
        _planPan = focus - (focus - _planPan) * (clamped / _planZoom);
        _planZoom = clamped;
        ApplyPlanLayout();
    }

    private void ApplyPlanLayout()
    {
        if (_planTexture == null || _planImage == null || !TryGetViewportSize(out Vector2 size))
        {
            return;
        }

        Vector2 plan = new Vector2(_planTexture.width, _planTexture.height) * _planZoom;

        _planPan.x = plan.x > size.x ? Mathf.Clamp(_planPan.x, size.x - plan.x, 0f) : (size.x - plan.x) * 0.5f;
        _planPan.y = plan.y > size.y ? Mathf.Clamp(_planPan.y, size.y - plan.y, 0f) : (size.y - plan.y) * 0.5f;

        _planImage.style.width = plan.x;
        _planImage.style.height = plan.y;
        _planImage.style.left = _planPan.x;
        _planImage.style.top = _planPan.y;
    }

    private void HidePlan()
    {
        if (_planLayer != null)
        {
            _planLayer.style.display = DisplayStyle.None;
        }

        _planTexture = null;
    }
}
