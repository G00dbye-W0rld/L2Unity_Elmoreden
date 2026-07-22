using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ClickManager : MonoBehaviour
{
    [SerializeField] private GameObject _locator;
    [SerializeField] private L2Particle _locatorBaseEffect;
    [SerializeField] private L2Particle _locatorReachedEffect;
    [SerializeField] private ObjectData _targetObjectData;
    [SerializeField] private ObjectData _hoverObjectData;

    public ObjectData HoverObjectData { get { return _hoverObjectData; } }

    private Vector3 _lastClickPosition = Vector3.zero;
    [SerializeField] private LayerMask _entityMask;
    [SerializeField] private LayerMask _clickThroughMask;
    private Camera _mainCamera;

    private WorldItem _highlightedItem;

    // Survol/ciblage PNJ/monstre : anneaux au sol (cf. HoverGroundRing). Le
    // glow par emission a ete abandonne (aplat de couleur plat qui ecrasait
    // la texture du personnage, pas reparable par reglage d'intensite),
    // tout comme la tentative de particules et le contour post-process
    // (Renderer Feature URP, blocages en cascade sans resultat visible).
    //
    // Deux instances (_hoverRing / _targetRing) : la cible actuelle et
    // l'entite survolee peuvent etre deux PNJ differents en meme temps -
    // meme logique de priorite que les anciennes bulles de nameplate
    // (target/attack decides en premier, hover l'emporte visuellement si
    // c'est la meme entite - cf. UpdateEntityHighlight/UpdateTargetRing).
    // Un materiau/texture PAR ETAT (prepares a la main, cf. HoverRingGenerator) -
    // si non assignes dans l'Inspector, charges automatiquement depuis
    // Resources au demarrage.
    [Header("Anneaux au sol (survol/cible/attaque)")]
    [SerializeField] private Material _hoverRingMaterial;
    [SerializeField] private Material _targetRingMaterial;
    [SerializeField] private Material _attackRingMaterial;
    // ClickArea (le vrai collider cliquable) n'est PAS dimensionne
    // dynamiquement sur CollisionRadius (cf. NetworkCombat.cs - seule sa
    // hauteur suit CollisionHeight, sa largeur/profondeur reste une valeur
    // fixe du prefab). Un multiplicateur > 1 fait donc depasser l'anneau
    // au-dela de la zone reellement cliquable pour certains PNJ, au point
    // de rendre le clic sur l'attaque plus difficile pres du bord - reduit
    // a 1 par defaut (anneau au raz du rayon de collision plutot que
    // dessus).
    [SerializeField] private float _hoverRingRadiusMultiplier = 1f;
    [SerializeField] private float _hoverRingMinRadius = 0.5f;
    [SerializeField] private float _hoverRingRotationSpeed = 30f;
    private HoverGroundRing _hoverRing;
    private HoverGroundRing _targetRing;

    const string RingMaterialResourceDir = "Data/UI/Assets/HoverRing";

    private static ClickManager _instance;
    public static ClickManager Instance { get { return _instance; } }

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

    void OnDestroy()
    {
        _instance = null;
    }

    void Start()
    {
        _locator = GameObject.Find("Locator");
        _locatorBaseEffect = _locator.transform.GetChild(0).gameObject.GetComponent<L2Particle>();
        _locatorReachedEffect = _locator.transform.GetChild(1).gameObject.GetComponent<L2Particle>();
        _mainCamera = CameraController.Instance.GetComponent<Camera>();

        if (_hoverRingMaterial == null) _hoverRingMaterial = Resources.Load<Material>($"{RingMaterialResourceDir}/RingHover");
        if (_targetRingMaterial == null) _targetRingMaterial = Resources.Load<Material>($"{RingMaterialResourceDir}/RingTarget");
        if (_attackRingMaterial == null) _attackRingMaterial = Resources.Load<Material>($"{RingMaterialResourceDir}/RingAttack");
        if (_hoverRingMaterial == null || _targetRingMaterial == null || _attackRingMaterial == null)
        {
            Debug.LogWarning("[ClickManager] Materiaux d'anneau introuvables (Inspector et Resources) - regenerer via Tools > L2Unity > Highlight > Generate HoverRing Prefab.");
        }

        _hoverRing = new HoverGroundRing(_hoverRingRotationSpeed);
        _hoverRing.SetMaterials(_hoverRingMaterial, _targetRingMaterial, _attackRingMaterial);
        _targetRing = new HoverGroundRing(_hoverRingRotationSpeed);
        _targetRing.SetMaterials(_hoverRingMaterial, _targetRingMaterial, _attackRingMaterial);

        HideLocator(false);
    }

    public void SetMasks(LayerMask entityMask, LayerMask clickThroughMask)
    {
        _entityMask = entityMask;
        _clickThroughMask = clickThroughMask;
    }

    void Update()
    {
        _hoverRing.Tick();
        _targetRing.Tick();
        UpdateTargetRing();

        if (L2GameUI.Instance.MouseOverUI || PlayerStateMachine.Instance != null && PlayerStateMachine.Instance.State == PlayerState.DEAD)
        {
            return;
        }

        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 1000f, ~_clickThroughMask))
        {
            int hitLayer = hit.collider.gameObject.layer;
            if (_entityMask == (_entityMask | (1 << hitLayer)))
            {
                _hoverObjectData = new ObjectData(hit.transform.parent.parent.gameObject); // click area -> model -> entity
            }
            else
            {
                _hoverObjectData = new ObjectData(hit.collider.gameObject);
            }

            UpdateEntityHighlight(_hoverObjectData);

            if (InputManager.Instance.LeftClickDown &&
                !InputManager.Instance.RightClickHeld)
            {
                _targetObjectData = _hoverObjectData;

                // Le tag "Pickup" doit primer sur le test de layer d'entite,
                // sinon un objet au sol dont le collider tombe sur un layer
                // inclus dans _entityMask est route vers OnClickOnEntity()
                // (-> tentative d'attaque cote serveur) au lieu du ramassage.
                if (_targetObjectData.ObjectTag == "Pickup")
                {
                    OnClickOnPickupItem();
                }
                else if (_entityMask == (_entityMask | (1 << hitLayer)) && _targetObjectData.ObjectTag != "Player")
                {
                    OnClickOnEntity();
                }
                else if (_targetObjectData != null)
                {
                    OnClickToMove(hit);
                }
            }

            if (_hoverObjectData.ObjectTransform != null && _hoverObjectData.ObjectTag == "Pickup")
            {
                CursorManager.Instance.ChangeCursor(CursorManager.CursorType.Pickup);
                UpdateItemHighlight(_hoverObjectData.ObjectTransform.GetComponent<WorldItem>());
            }
            else if (_hoverObjectData.ObjectTransform != null && _targetObjectData.ObjectTransform != null && _targetObjectData.ObjectTransform == _hoverObjectData.ObjectTransform)
            {
                UpdateItemHighlight(null);

                if (_hoverObjectData.ObjectTag == "Monster" && !_hoverObjectData.Entity.Status.IsDead)
                {
                    CursorManager.Instance.ChangeCursor(CursorManager.CursorType.Attack);
                }
                else if (_hoverObjectData.ObjectTag == "Npc")
                {
                    CursorManager.Instance.ChangeCursor(CursorManager.CursorType.Talk);
                }
                else
                {
                    CursorManager.Instance.ChangeCursor(CursorManager.CursorType.Default);
                }
            }
            else
            {
                UpdateItemHighlight(null);
                CursorManager.Instance.ChangeCursor(CursorManager.CursorType.Default);
            }
        }
        else
        {
            UpdateItemHighlight(null);
            UpdateEntityHighlight(null);
            CursorManager.Instance.ChangeCursor(CursorManager.CursorType.Default);
        }

        if (InputManager.Instance.Move || InputManager.Instance.MoveForward)
        {
            HideLocator(false);
        }
    }

    // Utilise pour le drop d'item au sol : ou pointe la souris actuellement,
    // en reutilisant le meme raycast que le clic-pour-se-deplacer (ignore le
    // meme masque "click through"). Pas de hit -> false, l'appelant retombe
    // sur la position du joueur.
    public bool TryGetMouseWorldPosition(out Vector3 worldPosition)
    {
        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, ~_clickThroughMask))
        {
            worldPosition = hit.point;
            return true;
        }

        worldPosition = Vector3.zero;
        return false;
    }

    public void OnClickToMove(RaycastHit hit)
    {
        _lastClickPosition = hit.point;
        //  PlayerCombatController.Instance.RunningToTarget = false;

        if (PlayerStateMachine.Instance != null)
        {
            PlayerStateMachine.Instance.NotifyEvent(Event.CLICK_TO_MOVE, _lastClickPosition);
            // PlayerStateMachine.Instance.ChangeIntention(Intention.INTENTION_MOVE_TO, _lastClickPosition);
        }

        if (TargetManager.Instance != null)
        {
            //TODO: Do it in attackstate exit
            TargetManager.Instance.ClearAttackTarget();
        }

        //  PathFinderController.Instance.MoveTo(_lastClickPosition);
        float angle = Vector3.Angle(hit.normal, Vector3.up);
        if (angle < 85f)
        {
            StartCoroutine(PlaceLocator(_lastClickPosition, hit.normal));
        }
        else
        {
            HideLocator(false);
        }
    }

    public void OnClickOnEntity()
    {
        // Debug.Log("Click on entity");
        if (TargetManager.Instance.HasTarget() && TargetManager.Instance.Target.transform == _targetObjectData.ObjectTransform)
        {
            PlayerActions.Instance.UseAction(ActionType.Attack);
        }
        else
        {
            TargetManager.Instance.SetTarget(_targetObjectData);
        }
    }

    private void UpdateItemHighlight(WorldItem item)
    {
        if (_highlightedItem != item)
        {
            if (_highlightedItem != null)
            {
                _highlightedItem.SetHighlighted(false);
            }

            _highlightedItem = item;

            if (_highlightedItem != null)
            {
                _highlightedItem.SetHighlighted(true);
            }
        }

        // Repositionne chaque frame (pas seulement au changement de cible) :
        // l'objet survole peut bouger legerement (rebond), la tooltip doit
        // suivre.
        if (_highlightedItem != null)
        {
            WorldItemTooltip.Instance.Show(_highlightedItem, _mainCamera);
        }
        else
        {
            WorldItemTooltip.Instance.Hide();
        }
    }

    // Anneau de la cible actuelle (independant du survol souris) - meme
    // priorite Target/Attack que les anciennes bulles de nameplate. Masque
    // si la cible est AUSSI l'entite survolee : dans ce cas
    // UpdateEntityHighlight() prend le relai visuel avec la couleur
    // Target/Attack sur l'anneau de survol, pour eviter deux anneaux
    // superposes sur la meme entite.
    private void UpdateTargetRing()
    {
        TargetManager target = TargetManager.Instance;
        if (target == null || !target.HasTarget())
        {
            _targetRing.Hide();
            return;
        }

        Transform targetTransform = target.Target.transform;
        bool hoverIsTarget = _hoverObjectData?.ObjectTransform == targetTransform;
        if (hoverIsTarget)
        {
            _targetRing.Hide();
            return;
        }

        bool isAttackTarget = IsAttackActive(target, target.Target) || EntityCombatQuery.IsAttackingPlayer(target.Target);
        _targetRing.SetState(isAttackTarget ? HoverGroundRing.RingState.Attack : HoverGroundRing.RingState.Target);

        float radius = Mathf.Max(target.Target.Appearance.CollisionRadius * _hoverRingRadiusMultiplier, _hoverRingMinRadius);
        _targetRing.Show(targetTransform.position, radius);
    }

    // Anneau du PNJ/monstre survole (Player exclu, meme filtre de tag que
    // le reste de la logique de survol ci-dessus). Reagit a n'importe quel
    // PNJ/monstre survole, pas seulement la cible actuelle - mais prend la
    // couleur Target/Attack (au lieu de Hover) si c'est aussi la cible
    // actuelle, priorite la plus haute, meme logique que les anciennes
    // bulles de nameplate.
    private void UpdateEntityHighlight(ObjectData hoverData)
    {
        bool isEntity = hoverData?.ObjectTransform != null && hoverData.Entity != null &&
                        (hoverData.ObjectTag == "Monster" || hoverData.ObjectTag == "Npc");

        if (!isEntity)
        {
            _hoverRing.Hide();
            return;
        }

        TargetManager target = TargetManager.Instance;
        bool isCurrentTarget = target != null && target.HasTarget() && target.Target.transform == hoverData.ObjectTransform;
        bool attacksPlayer = EntityCombatQuery.IsAttackingPlayer(hoverData.Entity);

        HoverGroundRing.RingState state = HoverGroundRing.RingState.Hover;
        if (isCurrentTarget)
        {
            bool isAttackTarget = IsAttackActive(target, target.Target) || attacksPlayer;
            state = isAttackTarget ? HoverGroundRing.RingState.Attack : HoverGroundRing.RingState.Target;
        }
        else if (attacksPlayer)
        {
            // PNJ survole qui n'est pas notre cible mais nous attaque quand
            // meme (aggro) - signale le danger independamment du ciblage.
            state = HoverGroundRing.RingState.Attack;
        }

        _hoverRing.SetState(state);
        float radius = Mathf.Max(hoverData.Entity.Appearance.CollisionRadius * _hoverRingRadiusMultiplier, _hoverRingMinRadius);
        _hoverRing.Show(hoverData.ObjectTransform.position, radius);
    }

    // L'etat "attaque" brut (IsAttackTargetSet) peut clignoter/redevenir
    // momentanement faux entre deux coups (retombant sur Target) - on le
    // maintient artificiellement pendant AttackHoldDuration apres la
    // derniere confirmation, borne a l'entite concernee (Identity.Id, pas
    // une comparaison de reference - cf. commentaire IsAttackTargetSet plus
    // haut) pour ne pas "coller" l'etat attaque a une nouvelle cible
    // choisie juste apres.
    private const float AttackHoldDuration = 3f;
    private float _lastAttackTime = -999f;
    private int _lastAttackEntityId = -1;

    private bool IsAttackActive(TargetManager target, Entity checkedEntity)
    {
        bool raw = target.IsAttackTargetSet() && !target.AttackTarget.Status.IsDead;
        if (raw)
        {
            _lastAttackTime = Time.time;
            _lastAttackEntityId = target.AttackTarget.Identity.Id;
        }

        bool holding = checkedEntity.Identity.Id == _lastAttackEntityId && Time.time - _lastAttackTime < AttackHoldDuration;
        return raw || holding;
    }

    // Suit exactement le meme flux que OnClickOnEntity()/InteractIntention
    // pour les NPC : on passe par le PlayerStateMachine (PickupIntention)
    // plutot que de gerer le deplacement nous-memes, pour que l'animation de
    // marche/course se joue normalement (elle est pilotee par MovingState,
    // pas par un simple appel a PathFinderController).
    public void OnClickOnPickupItem()
    {
        WorldItem worldItem = _targetObjectData.ObjectTransform != null
            ? _targetObjectData.ObjectTransform.GetComponent<WorldItem>()
            : null;

        if (worldItem == null)
        {
            return;
        }

        PickupIntention.Target = worldItem;
        PlayerStateMachine.Instance.ChangeIntention(Intention.INTENTION_PICKUP);
    }

    private IEnumerator PlaceLocator(Vector3 position, Vector3 normal)
    {
        _locator.SetActive(true);

        _locator.gameObject.transform.position = position;

        _locatorReachedEffect.gameObject.SetActive(false);
        _locatorBaseEffect.gameObject.SetActive(false);

        yield return new WaitForFixedUpdate();
        _locatorBaseEffect.gameObject.SetActive(true);

        _locatorBaseEffect.SurfaceNormal = normal;
        _locatorBaseEffect.ResetTimer();
    }

    public void HideLocator(bool targetReached)
    {
        if (targetReached)
        {
            Vector3 normal = _locatorBaseEffect.GetComponent<L2Particle>().SurfaceNormal;
            _locatorReachedEffect.gameObject.SetActive(true);

            _locatorReachedEffect.SurfaceNormal = normal;
            _locatorReachedEffect.ResetTimer();
        }
        else
        {
            _locator.SetActive(false);
            _locatorReachedEffect.gameObject.SetActive(false);
        }

        _locatorBaseEffect.gameObject.SetActive(false);
    }
}
