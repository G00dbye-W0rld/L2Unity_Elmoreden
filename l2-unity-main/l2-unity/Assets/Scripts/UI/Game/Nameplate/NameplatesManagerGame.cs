using UnityEngine;
using UnityEngine.UIElements;

public class NameplatesManagerGame : NameplatesManagerBase
{
    private PlayerNameplate playerNameplate;

    // Interrupteur ancien/nouveau systeme de nameplates - false par defaut
    // pour ne rien changer au comportement existant. Voir le plan de
    // remplacement des nameplates : filet de securite explicitement demande
    // apres qu'un correctif plus leger ait du etre integralement annule.
    [Header("Nameplates world-space (interrupteur)")]
    [SerializeField] private bool useWorldSpaceNameplates;
    [SerializeField] private WorldNameplateRenderer worldRenderer;
    private bool _worldRendererInitialized;

    private static NameplatesManagerGame instance;
    public static NameplatesManagerGame Instance => instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        Initialize();
    }

    private void FixedUpdate()
    {
        if (!IsSystemReady()) return;

        ScanForEntities();
        ProcessNameplateVisibility();

        // ProcessNameplateVisibility() ne culle que le dictionnaire
        // "nameplates" (systeme UI Toolkit) - les entites gerees par le
        // renderer world-space n'y rejoignent jamais, donc un passage
        // separe est necessaire pour elles.
        if (useWorldSpaceNameplates)
        {
            worldRenderer.CullOutOfRange(t => IsNameplateVisible(t));
        }

        HandlePlayerNameplate();
    }

    protected override bool IsSystemReady()
    {
        if (!L2GameUI.Instance.UILoaded) return false;

        if (mainCamera == null)
        {
            if (CameraController.Instance == null)
            {
                Debug.LogWarning("CameraController instance is null");
                return false;
            }

            var cameraComponent = CameraController.Instance.GetComponent<Camera>();
            if (cameraComponent == null)
            {
                Debug.LogWarning("Camera component not found on CameraController");
                return false;
            }

            mainCamera = cameraComponent;
            // Initialize du renderer world-space deplace plus bas : il a besoin
            // du playerTransform (fondu par distance, meme referentiel que le
            // cull) qui n'est resolu qu'apres ce bloc.
        }

        if (rootElement == null)
        {
            rootElement = L2GameUI.Instance.RootElement.Q<VisualElement>("NameplatesContainer");
            return false;
        }

        if (playerTransform == null && PlayerEntity.Instance?.transform != null)
        {
            playerTransform = PlayerEntity.Instance.transform;
        }

        if (playerTransform == null) return false;

        if (useWorldSpaceNameplates && !_worldRendererInitialized)
        {
            worldRenderer?.Initialize(mainCamera, playerTransform, nameplateViewDistance);
            _worldRendererInitialized = true;
        }

        return true;
    }

    protected override void ScanForEntities()
    {
        base.ScanForEntities();

        ProcessHoveredEntity();
        ProcessTargetedEntity();
    }

    private void ProcessHoveredEntity()
    {
        var hoveredObject = ClickManager.Instance.HoverObjectData;
        if (ShouldCreateNameplateForEntity(hoveredObject))
        {
            var entity = hoveredObject.ObjectTransform.GetComponent<Entity>();
            if (TryHandleEntityExternally(entity.Identity.Id, entity)) return;
            nameplates.GetOrAdd(entity.Identity.Id, _ => CreateNameplate(entity));
        }
    }

    protected virtual void ProcessTargetedEntity()
    {
        if (!TargetManager.Instance.HasTarget()) return;

        var targetTransform = TargetManager.Instance.Target.transform;
        if (targetTransform.TryGetComponent<Entity>(out var entity) &&
            entity.Identity.Id != GameClient.Instance.CurrentPlayerId)
        {
            if (TryHandleEntityExternally(entity.Identity.Id, entity)) return;
            nameplates.GetOrAdd(entity.Identity.Id, _ => CreateNameplate(entity));
        }
    }

    protected override bool TryHandleEntityExternally(int id, Entity entity)
    {
        if (!useWorldSpaceNameplates) return false;

        if (!worldRenderer.HasNameplate(id))
        {
            worldRenderer.CreateNameplate(id, entity);
        }

        return true;
    }

    protected override void TickExternalRenderer()
    {
        if (!useWorldSpaceNameplates) return;

        worldRenderer.Tick();
        UpdateWorldBubbleStates();
    }

    // Hover sert desormais d'etat par defaut TOUJOURS affiche sur chaque
    // nameplate active (plus une reaction au survol souris - le survol
    // souris reste gere separement par les anneaux au sol de ClickManager).
    // Target/Attack le remplacent quand pertinent : soit le joueur cible/
    // attaque ce PNJ, soit ce PNJ nous attaque (son Combat.TargetId cote
    // serveur pointe sur nous - EntityTargetSetPacket/UnsetPacket, signal
    // temps reel independant des degats effectivement recus).
    private void UpdateWorldBubbleStates()
    {
        var target = TargetManager.Instance;

        foreach (var kvp in worldRenderer.ActiveTargets())
        {
            Transform entityTransform = kvp.Value;
            if (entityTransform == null) continue;

            bool isCurrentTarget = target.HasTarget() && target.Target.transform == entityTransform;
            // IsAttackTargetSet() compare par Identity.Id - la comparaison de
            // references (AttackTarget == Target) echoue quand cible et cible
            // d'attaque sont deux instances Entity distinctes de la meme unite,
            // ce qui empechait la bulle rouge d'apparaitre.
            bool isAttackTarget = isCurrentTarget && target.IsAttackTargetSet() && !target.AttackTarget.Status.IsDead;

            bool npcAttacksPlayer = entityTransform.TryGetComponent(out Entity npcEntity) && EntityCombatQuery.IsAttackingPlayer(npcEntity);

            WorldNameplate.BubbleState state;
            if (isAttackTarget || npcAttacksPlayer)
            {
                state = WorldNameplate.BubbleState.Attack;
            }
            else if (isCurrentTarget)
            {
                state = WorldNameplate.BubbleState.Target;
            }
            else
            {
                state = WorldNameplate.BubbleState.Hover;
            }

            worldRenderer.SetBubbleState(kvp.Key, state);
        }
    }

    public override void RemoveNameplate(int id)
    {
        if (useWorldSpaceNameplates)
        {
            worldRenderer.RemoveNameplate(id);
        }
        else
        {
            base.RemoveNameplate(id);
        }
    }

    protected override bool CheckIfNotPlayer(Entity entity)
    {
        return entity.Identity.Id != GameClient.Instance.CurrentPlayerId;
    }

    private PlayerNameplate CreatePlayerNameplate(Entity entity)
    {
        var element = nameplateTemplate.Instantiate()[0];
        var nameplate = new PlayerNameplate(
            element,
            element.Q<Label>("EntityName"),
            element.Q<Label>("EntityTitle"),
            entity
        );
        rootElement.Add(element);
        return nameplate;
    }

    private void HandlePlayerNameplate()
    {
        if (useWorldSpaceNameplates)
        {
            UpdateWorldPlayerNameplate();
            return;
        }

        playerNameplate ??= CreatePlayerNameplate(PlayerEntity.Instance);
        UpdatePlayerNameplate();
    }

    private void UpdateWorldPlayerNameplate()
    {
        WorldPlayerNameplate wpn = worldRenderer.GetOrCreatePlayerNameplate(PlayerEntity.Instance);
        bool visible = IsNameplateVisible(wpn.Target);
        wpn.Root.SetActive(visible);

        if (!visible) return;

        worldRenderer.TickPlayerNameplate();

        if (wpn.GaugeEndTime - Time.time > 0)
        {
            wpn.UpdateGauge(Time.time);
        }
        else
        {
            wpn.HideGauge();
        }
    }

    private void UpdatePlayerNameplate()
    {
        playerNameplate.Visible = IsNameplateVisible(playerNameplate.Target);
        if (playerNameplate.Visible)
        {
            playerNameplate.Show();
            UpdateNameplatePosition(playerNameplate);
            UpdateNameplateStyle(playerNameplate);
            UpdatePlayerGauge();
        }
        else
        {
            playerNameplate.Hide();
        }
    }
    protected override bool IsNameplateVisible(Transform target)
    {
        if (target == null) return false;

        var isHovered = ClickManager.Instance.HoverObjectData?.ObjectTransform == target;
        if (isHovered) return true;

        var isTarget = TargetManager.Instance.HasTarget() &&
                      TargetManager.Instance.Target.transform == target;
        var isTooFar = Vector3.Distance(playerTransform.position, target.position) > nameplateViewDistance;

        return (!isTooFar || isTarget) && CameraController.Instance.IsObjectVisible(target);
    }

    protected override void UpdateNameplateStyle(Nameplate nameplate)
    {
        base.UpdateNameplateStyle(nameplate);

        var target = TargetManager.Instance;
        var isCurrentTarget = target.HasTarget() && target.Target.transform == nameplate.Target;

        if (isCurrentTarget)
        {
            UpdateTargetedNameplateStyle(nameplate, target);
        }
        else
        {
            nameplate.RemoveStyle("target-bubble-attack");
            nameplate.RemoveStyle("target-bubble-target");
        }

        UpdateHoveredNameplateStyle(nameplate);
    }

    protected void UpdateTargetedNameplateStyle(Nameplate nameplate, TargetManager target)
    {
        var isAttackTarget = target.AttackTarget == target.Target &&
                            !target.AttackTarget.Status.IsDead;

        if (isAttackTarget)
        {
            nameplate.SetStyle("target-bubble-attack");
        }
        else
        {
            nameplate.SetStyle("target-bubble-target");
            nameplate.RemoveStyle("target-bubble-attack");
        }
    }

    protected void UpdateHoveredNameplateStyle(Nameplate nameplate)
    {
        var isHovered = ClickManager.Instance.HoverObjectData?.ObjectTransform == nameplate.Target;
        if (isHovered)
        {
            nameplate.SetStyle("target-bubble-hover");
        }
        else
        {
            nameplate.RemoveStyle("target-bubble-hover");
        }
    }

    public void StartCasting(SetupGaugePacket.GaugeColor color, int durationMs)
    {
        if (useWorldSpaceNameplates)
        {
            worldRenderer.GetOrCreatePlayerNameplate(PlayerEntity.Instance).ShowGauge(color, Time.time, durationMs);
            return;
        }

        playerNameplate?.ShowGauge(color, Time.time, durationMs);
    }

    public void StopCasting()
    {
        if (useWorldSpaceNameplates)
        {
            worldRenderer.GetOrCreatePlayerNameplate(PlayerEntity.Instance).HideGauge();
            return;
        }

        playerNameplate?.HideGauge();
    }

    private void UpdatePlayerGauge()
    {
        if (playerNameplate == null) return;

        if (playerNameplate.GaugeEndTime - Time.time > 0)
        {
            playerNameplate.UpdateGauge(Time.time);
        }
        else
        {
            playerNameplate.HideGauge();
        }
    }

    private void OnDestroy()
    {
        nameplates.Clear();
        worldRenderer?.RemoveAll();
        worldRenderer?.RemovePlayerNameplate();
        instance = null;
    }

    public override void ClearNameplates()
    {
        base.ClearNameplates();

        if (playerNameplate != null)
        {
            if (playerNameplate.NameplateEle != null)
            {
                rootElement.Remove(playerNameplate.NameplateEle);
            }

            playerNameplate = null;
        }

        // Nettoyage des deux systemes systematiquement (pas seulement celui
        // actif) - evite des objets poolés orphelins si l'interrupteur
        // change entre deux appels.
        if (worldRenderer != null)
        {
            worldRenderer.RemoveAll();
            worldRenderer.RemovePlayerNameplate();
        }
    }
}
