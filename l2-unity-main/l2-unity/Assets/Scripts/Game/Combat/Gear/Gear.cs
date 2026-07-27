using System;
using System.Collections.Generic;
using UnityEngine;

public class Gear : MonoBehaviour
{
    [SerializeField] protected EntityReferenceHolder _referenceHolder;

    protected int _ownerId;

    [Header("Bones")]
    [SerializeField] protected Transform _rightHandBone;
    [SerializeField] protected Transform _leftHandBone;
    [SerializeField] protected Transform _shieldBone;

    [Header("Weapons")]
    [Header("Meta")]
    [SerializeField] private Weapon _rightHandWeaponData;
    [SerializeField] private Weapon _leftHandWeaponData;
    [SerializeField] protected float _weaponSizeRatio;
    [Header("Models")]
    [Header("Right hand")]
    [SerializeField] private WeaponType _rightHandType;
    [SerializeField] protected Transform _rightHandWeapon;
    [SerializeField] protected Transform _arrow;
    [Header("LeftHand")]
    [SerializeField] private WeaponType _leftHandType;
    [SerializeField] protected Transform _leftHandWeapon;

    // Aura d'enchant de l'arme active (droite uniquement, comme
    // Player.getEnchantEffect() cote serveur). Tous les reglages vivent dans
    // un asset UNIQUE (EnchantAuraSettings) et non ici : ce composant est
    // present sur 42 prefabs de personnage, y poser des champs obligerait a
    // les regler 42 fois, et les modifications faites en Play Mode y sont
    // jetees a l'arret du jeu.
    private const string AuraRootName = "weapon_enchant_aura";
    private static EnchantAuraSettings _auraSettings;
    private int _lastEnchantEffect = -1;

    protected NewBaseAnimationController AnimationController { get { return _referenceHolder.NewAnimationController; } }
    public WeaponType WeaponType { get { return (_leftHandType != WeaponType.none && _leftHandType != WeaponType.hand) ? _leftHandType : _rightHandType; } }
    public int OwnerId { get { return _ownerId; } set { _ownerId = value; } }

    public Transform RightHandBone { get { return _rightHandBone; } }
    public Transform LeftHandBone { get { return _leftHandBone; } }
    public Transform Arrow { get { return _arrow; } }

    public virtual void Initialize(int ownderId)
    {
        if (_referenceHolder == null)
        {
            TryGetComponent(out _referenceHolder);
            Debug.LogWarning($"[{transform.name}] EntityReferenceHolder was not assigned, please pre-assign it to avoid unecessary load.");
        }

        _ownerId = ownderId;

        GetLeftHandBone();
        GetRightHandBone();
    }

    // TODO: PRE-ASSIGN BONES IN PREFAB TO AVOID CPU LOAD

    public bool IsWeaponAlreadyEquipped(int itemId, bool leftSlot)
    {
        if (leftSlot)
        {
            if (_leftHandWeaponData == null)
            {
                Debug.Log("Left hand metadata is null, weapon not equiped.");
                return false;
            }

            bool idMatch = itemId == _leftHandWeaponData.Id;
            if (!idMatch)
            {
                Debug.Log("Left hand weapon id did not match, weapon not equiped.");
            }

            return idMatch;
        }
        else
        {
            if (_rightHandWeaponData == null)
            {
                Debug.Log("Right hand metadata is null, weapon not equiped.");
                return false;
            }

            bool idMatch = itemId == _rightHandWeaponData.Id;
            if (!idMatch)
            {
                Debug.Log("Right hand weapon id did not match, weapon not equiped.");
            }

            return idMatch;
        }
    }

    public virtual void EquipAllWeapons(Appearance appearance)
    {
        if (appearance.RHand != 0)
        {
            // Loading from table
            Weapon weapon = ItemTable.Instance.GetWeapon(appearance.RHand);
            if (weapon == null)
            {
                Debug.LogWarning($"Could find weapon {appearance.RHand} in DB for entity {_ownerId}.");
                return;
            }

            if (weapon.Weapongrp.WeaponType == WeaponType.dual || weapon.Weapongrp.WeaponType == WeaponType.fist)
            {
                UnequipWeapon(true);
                EquipWeapon(appearance.RHand, weapon, false);
            }

            if (weapon.Weapongrp.WeaponType == WeaponType.bow)
            {
                appearance.LHand = appearance.RHand;
                appearance.RHand = 0;
                UnequipWeapon(false);
            }
            else
            {
                EquipWeapon(appearance.RHand, weapon, false);
            }
        }
        else
        {
            UnequipWeapon(false);
        }


        if (appearance.LHand != 0)
        {
            // Loading from table
            Weapon weapon = ItemTable.Instance.GetWeapon(appearance.LHand);
            if (weapon == null)
            {
                Debug.LogWarning($"Could find weapon {appearance.LHand} in DB for entity {_ownerId}.");
                return;
            }

            EquipWeapon(appearance.LHand, weapon, true);
        }
        else if (!(appearance.RHand != 0 && _rightHandWeaponData != null && _rightHandWeaponData.Weapongrp.BodyPart == ItemSlot.SLOT_LR_HAND))
        {
            // Unequip the weapon in left hand if it's not duals or fists
            UnequipWeapon(true);
        }
    }

    public virtual void EquipArrow()
    {
        Debug.Log($"[{transform.name}] Equip arrow");
        GameObject arrowPrefab = ModelTable.Instance.GetItemModelById(17);
        if (arrowPrefab == null)
        {
            Debug.LogWarning($"Could not load arrow prefab in DB for entity {_ownerId}.");
            return;
        }

        GameObject go = GameObject.Instantiate(arrowPrefab);
        go.SetActive(false);
        go.transform.name = "arrow";

        _arrow = go.transform;
        _arrow.parent = RightHandBone;
        _arrow.localPosition = new Vector3(-0.0005f, 0, 0);
        _arrow.localRotation = new Quaternion(0, 0, 0, 0);
        _arrow.localScale = Vector3.one * GetWeaponSizeRatio();
    }

    private float GetWeaponSizeRatio()
    {
        if (_weaponSizeRatio == 0)
        {
            float collisionHeight = _referenceHolder.Entity.Appearance.CollisionHeight;
            float ratio = 1 + (collisionHeight - 0.45f) / 0.45f;

            // Debug.Log("WeaponSizeRatio: " + ratio);

            _weaponSizeRatio = ratio;
        }

        return _weaponSizeRatio;
    }

    public virtual void UnEquipArrow()
    {
        if (_arrow == null)
        {
            return;
        }

        Debug.Log($"[{transform.name}] Unequip arrow");
        GameObject.DestroyImmediate(_arrow.gameObject);
    }

    public virtual void ShowArrow()
    {
        if (_arrow == null)
        {
            EquipArrow();
        }

        // Debug.Log($"[{transform.name}] Show arrow");
        if (_arrow != null)
        {
            _arrow.gameObject.SetActive(true);
        }
    }

    public virtual void HideArrow()
    {
        // Debug.Log($"[{transform.name}] Hide arrow");
        if (_arrow != null)
        {
            _arrow.gameObject.SetActive(false);
        }
    }

    public virtual void EquipAllArmors(Appearance appearance) { }

    public virtual void EquipWeapon(int weaponId, Weapon weapon, bool leftSlot)
    {
        if (weaponId == 0)
        {
            return;
        }

        WeaponType weaponType = weapon.Weapongrp.WeaponType;
        if (IsWeaponAlreadyEquipped(weaponId, leftSlot))
        {
            Debug.Log($"Weapon {weaponId} of type {weaponType} is already equipped in {(leftSlot ? "left" : "right")} slot.");
            return;
        }
        else
        {
            Debug.Log($"Weapon {weaponId} of type {weaponType} was not equipped in {(leftSlot ? "left" : "right")} slot.");
        }

        UnequipWeapon(leftSlot);

        // On met a jour l'etat logique de l'arme AVANT de verifier la disponibilite du modele visuel :
        // meme si le prefab 3D est introuvable, l'arme reste mecaniquement equipee (juste invisible),
        // au lieu de laisser _rightHandWeaponData/_leftHandWeaponData sur une valeur perimee/incoherente
        // qui provoquait un InvalidCastException plus loin dans EquipAllWeapons.
        if (leftSlot)
        {
            _leftHandWeaponData = weapon;
            _leftHandType = weapon.Weapongrp.WeaponType;
        }
        else
        {
            _rightHandWeaponData = weapon;
            _rightHandType = weapon.Weapongrp.WeaponType;
        }

        if (weapon.Weapongrp.WeaponType != WeaponType.none)
        { // Do not update for shields
            UpdateWeaponType(weapon.Weapongrp.WeaponType);
        }

        GameObject[] weaponPrefabs = ModelTable.Instance.GetWeaponsById(weaponId); //TODO: For duals and fists
        if (weaponPrefabs == null)
        {
            Debug.LogWarning($"Could not load weapon prefab array for weaponId: {weaponId} (entity: {_ownerId}).");
            return;
        }

        for (int i = 0; i < weaponPrefabs.Length; i++)
        {
            if (weaponPrefabs[i] == null)
            {
                Debug.LogWarning($"Missing prefab at index {i} for weaponId: {weaponId} (entity: {_ownerId}).");
                return;
            }
        }

        // Instantiating weapon
        for (int i = 0; i < weaponPrefabs.Length; i++)
        {
            GameObject go = GameObject.Instantiate(weaponPrefabs[i]);
            go.SetActive(false);
            go.transform.name = "weapon";

            if (weapon.Weapongrp.WeaponType == WeaponType.none)
            {
                _leftHandWeapon = go.transform;
                go.transform.SetParent(GetShieldBone(), false);
            }
            else if (weapon.Weapongrp.WeaponType == WeaponType.bow || leftSlot)
            {
                _leftHandWeapon = go.transform;
                go.transform.SetParent(GetLeftHandBone(), false);
            }
            else if (weapon.Weapongrp.WeaponType == WeaponType.dual || weapon.Weapongrp.WeaponType == WeaponType.fist)
            {
                if (i == 0)
                {
                    _rightHandWeapon = go.transform;
                    go.transform.SetParent(GetRightHandBone(), false);
                }
                else
                {
                    _leftHandWeapon = go.transform;
                    go.transform.SetParent(GetLeftHandBone(), false);
                }
            }
            else
            {
                _rightHandWeapon = go.transform;
                go.transform.SetParent(GetRightHandBone(), false);
            }

            go.SetActive(true);

            go.transform.localScale *= GetWeaponSizeRatio();

            if (weaponType == WeaponType.bow)
            {
                EquipArrow();
            }
        }
    }

    protected virtual void UpdateWeaponType(WeaponType weaponType) { }

    public virtual void UpdateWeaponAnim(WeaponAnimType value) { }

    protected virtual Transform GetLeftHandBone()
    {
        if (_leftHandBone == null)
        {
            Debug.LogWarning($"[{transform.name}] Shield bone was not assigned, please pre-assign bones to avoid unecessary load.");
            _leftHandBone = transform.FindRecursive("Bow Bone");
        }

        if (_leftHandBone == null)
        {
            Debug.LogWarning($"[{transform.name}] Shield bone was not assigned, please pre-assign bones to avoid unecessary load.");
            _leftHandBone = transform.FindRecursive("bow_bone");
        }

        if (_leftHandBone == null)
        {
            Debug.LogWarning($"[{transform.name}] Shield bone was not assigned, please pre-assign bones to avoid unecessary load.");
            _leftHandBone = transform.FindRecursive("Sword Bone01");
        }
        return _leftHandBone;
    }

    protected virtual Transform GetRightHandBone()
    {
        if (_rightHandBone == null)
        {
            Debug.LogWarning($"[{transform.name}] Shield bone was not assigned, please pre-assign bones to avoid unecessary load.");
            _rightHandBone = transform.FindRecursive("Sword Bone");
        }
        return _rightHandBone;
    }

    protected virtual Transform GetShieldBone()
    {
        if (_shieldBone == null)
        {
            Debug.LogWarning($"[{transform.name}] Shield bone was not assigned, please pre-assign bones to avoid unecessary load.");
            _shieldBone = transform.FindRecursive("Shield Bone");
        }
        return _shieldBone;
    }

    public virtual void UnequipWeapon(bool leftSlot)
    {
        Transform weaponBone = leftSlot ? GetLeftHandBone() : GetRightHandBone();
        if (weaponBone == null)
        {
            return;
        }

        Transform weapon = weaponBone.Find("weapon") ?? (leftSlot ? GetShieldBone().Find("weapon") : null);

        if (weapon != null)
        {
            Destroy(weapon.gameObject);

            if (WeaponType == WeaponType.bow)
            {
                UnEquipArrow();
            }

            if (leftSlot)
            {
                _leftHandWeaponData = null;
                _leftHandType = WeaponType.hand;
                UpdateWeaponAnim(WeaponAnimParser.GetWeaponAnim(_rightHandType == WeaponType.none ? WeaponType.hand : _rightHandType));
            }
            else
            {
                _rightHandWeaponData = null;
                _rightHandType = WeaponType.hand;
                UpdateWeaponAnim(WeaponAnimParser.GetWeaponAnim(_leftHandType == WeaponType.none ? WeaponType.hand : _leftHandType));
            }
        }
    }

    public virtual void StartTrail()
    {
    }

    public virtual void StopTrail()
    {
    }

    public virtual void UpdateAppearance(Appearance oldAppearance, Appearance newAppearance)
    {
        if (oldAppearance.ShouldUpdateWeapons(newAppearance))
        {
            if (oldAppearance.ShouldUpdateColSize(newAppearance))
            {
                oldAppearance.CollisionHeight = newAppearance.CollisionHeight;
                oldAppearance.CollisionRadius = newAppearance.CollisionRadius;
            }

            EquipAllWeapons(newAppearance);
        }

        // Hors du if ci-dessus expres : un enchant reussi ne change pas
        // RHand/LHand (meme objet, juste son EnchantLevel), donc
        // ShouldUpdateWeapons() resterait faux alors que le niveau
        // d'enchant, lui, a bien change (le serveur rediffuse UserInfo/
        // PlayerInfo apres chaque tentative, cf. RequestEnchantItem.java).
        RefreshEnchantAura(newAppearance.EnchantEffect);
    }

    protected virtual void OnEnable()
    {
        // Rebuild immediat des que l'asset de reglages change dans
        // l'Inspector : c'est ce qui permet de regler l'aura en pleine
        // partie et de voir le resultat sans re-equiper l'arme.
        EnchantAuraSettings.SettingsChanged += OnAuraSettingsChanged;
    }

    protected virtual void OnDisable()
    {
        EnchantAuraSettings.SettingsChanged -= OnAuraSettingsChanged;
    }

    private void OnAuraSettingsChanged()
    {
        if (_lastEnchantEffect < 0) return;

        // Reconstruction complete plutot qu'un simple reglage : l'utilisateur
        // peut avoir change les prefabs eux-memes, pas seulement des valeurs.
        if (_rightHandWeapon != null) DestroyAuraRoot(_rightHandWeapon);
        if (_leftHandWeapon != null) DestroyAuraRoot(_leftHandWeapon);
        RefreshEnchantAura(_lastEnchantEffect);
    }

    private static EnchantAuraSettings GetAuraSettings()
    {
        if (_auraSettings != null) return _auraSettings;

        _auraSettings = Resources.Load<EnchantAuraSettings>(EnchantAuraSettings.ResourcesPath);
        if (_auraSettings == null)
        {
            Debug.LogWarning($"[Gear] Asset de reglages d'aura introuvable : Resources/{EnchantAuraSettings.ResourcesPath}. " +
                             "Creez-le via Assets > Create > L2Unity > Enchant Aura Settings et placez-le a ce chemin.");
        }
        return _auraSettings;
    }

    private static void DestroyAuraRoot(Transform weapon)
    {
        Transform existing = weapon.FindRecursive(AuraRootName);
        if (existing == null) return;

        // Detache AVANT de detruire : Destroy() n'agit qu'en fin de frame,
        // donc sans ce detachement le FindRecursive qui suit retrouverait
        // l'ancienne aura encore en place et le rebuild ne se ferait jamais.
        existing.SetParent(null);
        Destroy(existing.gameObject);
    }

    // Monte (ou demonte) l'aura d'enchant sur les armes portees. L'arme est
    // un GameObject neuf a chaque equipement (Destroy dans UnequipWeapon),
    // donc aucun nettoyage manuel n'est necessaire au changement d'arme.
    private void RefreshEnchantAura(int enchantEffect)
    {
        _lastEnchantEffect = enchantEffect;

        EnchantAuraSettings settings = GetAuraSettings();
        if (settings == null) return;

        RefreshEnchantAuraOn(_rightHandWeapon, settings, enchantEffect);

        // Main gauche : indispensable pour les ARCS (EquipAllWeapons les
        // bascule en LHand et vide RHand) et pour les DUALS (une lame par
        // main), qui sinon n'avaient aucun effet.
        // Exclu volontairement : les BOUCLIERS. Dans cette enum WeaponType,
        // 'none' designe un bouclier (cf. "Do not update for shields" dans
        // EquipWeapon) et 'hand' les mains nues - ni l'un ni l'autre ne doit
        // recevoir d'effet d'enchantement.
        if (_leftHandType != WeaponType.none && _leftHandType != WeaponType.hand)
        {
            RefreshEnchantAuraOn(_leftHandWeapon, settings, enchantEffect);
        }
        else if (_leftHandWeapon != null)
        {
            // Bouclier ramasse par un equipement precedent : on retire toute
            // aura qui aurait pu y etre montee quand ce slot portait une arme.
            Transform staleAura = _leftHandWeapon.FindRecursive(AuraRootName);
            if (staleAura != null) staleAura.gameObject.SetActive(false);
        }
    }

    private void RefreshEnchantAuraOn(Transform weapon, EnchantAuraSettings settings, int enchantEffect)
    {
        if (weapon == null) return;

        Transform auraRoot = weapon.FindRecursive(AuraRootName);
        bool visible = enchantEffect >= settings.minEnchantLevel;

        if (!visible)
        {
            if (auraRoot != null) auraRoot.gameObject.SetActive(false);
            return;
        }

        if (auraRoot == null)
        {
            auraRoot = BuildAura(weapon, settings);
            if (auraRoot == null) return;
        }

        auraRoot.gameObject.SetActive(true);
        ApplyAuraColor(auraRoot, settings, enchantEffect);
    }

    // Instancie un calque par entree activee dans l'asset de reglages.
    private Transform BuildAura(Transform weapon, EnchantAuraSettings settings)
    {
        if (settings.layers == null || settings.layers.Length == 0) return null;

        // Renderer le plus volumineux = la piece principale de l'arme (lame,
        // masse...) plutot qu'une garde ou un pommeau. Sert de source pour le
        // mode WeaponMesh et de reference de taille. Bounds en espace MONDE :
        // deja a l'echelle reelle, donc aucune division par l'echelle du
        // transform (piege qui avait fait exploser les dimensions avant).
        Renderer mainRenderer = null;
        Bounds worldBounds = default;
        bool hasBounds = false;
        foreach (Renderer candidate in weapon.GetComponentsInChildren<Renderer>(true))
        {
            if (candidate is ParticleSystemRenderer) continue;

            Bounds candidateBounds = candidate.bounds;
            if (!hasBounds)
            {
                worldBounds = candidateBounds;
                hasBounds = true;
            }
            else
            {
                worldBounds.Encapsulate(candidateBounds);
            }

            Vector3 s = candidateBounds.size;
            if (mainRenderer == null || s.x * s.y * s.z > GetVolume(mainRenderer.bounds))
            {
                mainRenderer = candidate;
            }
        }

        if (!hasBounds || mainRenderer == null) return null;

        Vector3 worldSize = worldBounds.size;
        float weaponLength = Mathf.Max(worldSize.x, Mathf.Max(worldSize.y, worldSize.z));
        if (weaponLength <= 0.0001f) return null;

        // Dimensions dans l'espace LOCAL de l'arme, pour la boite d'emission.
        // mesh.bounds reste accessible meme quand le mesh n'est pas
        // "Read/Write Enabled" (seule la lecture des triangles l'exige), donc
        // ce calcul fonctionne pour toutes les armes du projet.
        Vector3 localSize = GetWeaponLocalSize(weapon, worldSize);

        GameObject rootGo = new GameObject(AuraRootName);
        Transform root = rootGo.transform;
        root.SetParent(weapon, false);
        // Positionne sur le centre reel de l'arme. InverseTransformPoint est
        // sur pour un POINT (contrairement a InverseTransformVector pour une
        // taille, qui divise par l'echelle et faussait tout auparavant).
        root.localPosition = weapon.InverseTransformPoint(worldBounds.center);
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

        foreach (EnchantAuraLayer layer in settings.layers)
        {
            if (layer == null || !layer.enabled || layer.prefab == null) continue;
            BuildLayer(layer, root, mainRenderer, localSize, weaponLength);
        }

        return root;
    }

    // Taille de l'arme exprimee dans son propre espace local, obtenue en
    // transformant les coins des bounds de chaque mesh enfant.
    private static Vector3 GetWeaponLocalSize(Transform weapon, Vector3 fallbackWorldSize)
    {
        Matrix4x4 worldToWeapon = weapon.worldToLocalMatrix;
        Bounds localBounds = default;
        bool initialized = false;

        foreach (MeshFilter meshFilter in weapon.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null) continue;

            Bounds meshBounds = meshFilter.sharedMesh.bounds;
            Matrix4x4 meshToWeapon = worldToWeapon * meshFilter.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 signs = new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f);
                Vector3 point = meshToWeapon.MultiplyPoint3x4(meshBounds.center + Vector3.Scale(meshBounds.extents, signs));

                if (!initialized)
                {
                    localBounds = new Bounds(point, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    localBounds.Encapsulate(point);
                }
            }
        }

        return initialized ? localBounds.size : fallbackWorldSize;
    }

    private static float GetVolume(Bounds bounds)
    {
        Vector3 s = bounds.size;
        return s.x * s.y * s.z;
    }

    // Plafond de particules par systeme : garde-fou de performance, pas un
    // choix esthetique. Les prefabs du pack sont concus pour des effets de
    // decor a l'echelle d'une scene, pas pour etre colles sur une arme
    // portee par chaque personnage a l'ecran.
    private const int MaxParticlesPerSystem = 250;

    private void BuildLayer(EnchantAuraLayer layer, Transform root, Renderer mainRenderer, Vector3 localSize, float weaponLength)
    {
        GameObject instance = Instantiate(layer.prefab, root);
        instance.name = layer.prefab.name;

        // Les lumieres temps reel embarquees dans certains prefabs du pack
        // (FireFlies en a une) sont couteuses et, sur une arme, se lisent
        // comme un projecteur qui eclaire le personnage. Retirees par defaut.
        if (layer.removePrefabLights)
        {
            foreach (Light light in instance.GetComponentsInChildren<Light>(true))
            {
                Destroy(light);
            }
        }

        Transform t = instance.transform;
        t.localPosition = layer.positionOffset * weaponLength;
        t.localRotation = Quaternion.Euler(layer.rotationOffset);

        // L'echelle du transform ne sert QUE quand on ne pilote pas la forme
        // d'emission. Sinon les deux se multiplieraient : la boite calculee
        // aux dimensions de l'arme se retrouvait ensuite mise a l'echelle par
        // ce meme transform, donc enorme et hors de l'ecran. Quand on pilote
        // la forme, on garde le transform a 1 et c'est scaleRatio qui
        // dimensionne la zone d'emission (cf. ApplyShape).
        if (layer.shapeMode == EnchantAuraShapeMode.PrefabDefault && layer.scaleRatio > 0f)
        {
            Vector3 parentScale = root.lossyScale;
            float safeX = Mathf.Approximately(parentScale.x, 0f) ? 1f : parentScale.x;
            float safeY = Mathf.Approximately(parentScale.y, 0f) ? 1f : parentScale.y;
            float safeZ = Mathf.Approximately(parentScale.z, 0f) ? 1f : parentScale.z;
            float target = weaponLength * layer.scaleRatio;
            t.localScale = new Vector3(target / safeX, target / safeY, target / safeZ);
        }
        else
        {
            t.localScale = Vector3.one;
        }

        List<string> subNames = new List<string>();
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            // Nom du sous-effet tel qu'a saisir dans 'Disabled Sub Effects'.
            string subName = ps.gameObject.name;
            subNames.Add(subName);

            if (IsSubEffectDisabled(layer, subName))
            {
                // On eteint le SYSTEME, pas le GameObject : sur ces prefabs
                // la racine porte elle-meme un systeme de particules (ex.
                // 'EnergyExplosion', 'LightnigStormCloud'), et un
                // SetActive(false) sur elle desactivait TOUT le calque, ses
                // enfants compris - donc plus aucun effet du tout au lieu de
                // simplement retirer ce sous-effet.
                ParticleSystem.EmissionModule disabledEmission = ps.emission;
                disabledEmission.enabled = false;
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (ps.TryGetComponent(out ParticleSystemRenderer disabledRenderer))
                {
                    disabledRenderer.enabled = false;
                }
                continue;
            }

            // Materiau de remplacement AVANT la capture des couleurs par
            // EnchantAuraTintTarget (plus bas) : sinon la teinte repartirait
            // des couleurs de l'ancien materiau et non de celles du nouveau.
            if (layer.materialOverride != null && ps.TryGetComponent(out ParticleSystemRenderer psRenderer))
            {
                psRenderer.sharedMaterial = layer.materialOverride;
            }

            ApplyShape(ps, layer, mainRenderer, localSize, weaponLength);

            ParticleSystem.MainModule main = ps.main;

            if (layer.lifetimeMultiplier > 0f && !Mathf.Approximately(layer.lifetimeMultiplier, 1f))
            {
                main.startLifetimeMultiplier *= layer.lifetimeMultiplier;
            }

            if (layer.speedMultiplier >= 0f && !Mathf.Approximately(layer.speedMultiplier, 1f))
            {
                main.startSpeedMultiplier *= layer.speedMultiplier;
            }
            if (!Mathf.Approximately(layer.particleSizeMultiplier, 1f) && layer.particleSizeMultiplier > 0f)
            {
                main.startSizeMultiplier *= layer.particleSizeMultiplier;
            }

            if (!Mathf.Approximately(layer.densityMultiplier, 1f) && layer.densityMultiplier > 0f)
            {
                ParticleSystem.EmissionModule emission = ps.emission;
                emission.rateOverTimeMultiplier *= layer.densityMultiplier;
            }

            // Memorise les couleurs d'origine du materiau : sur ces prefabs
            // le rendu passe surtout par `_EmissionColor` (HDR, fixe), pas
            // par la couleur des particules - sans ca la teinte d'enchant
            // n'avait presque aucun effet visible.
            if (layer.applyEnchantColor)
            {
                EnchantAuraTintTarget tintTarget = ps.gameObject.AddComponent<EnchantAuraTintTarget>();
                tintTarget.Capture(ps);
            }

            main.maxParticles = Mathf.Min(main.maxParticles, MaxParticlesPerSystem);
            // Simulation en espace local : l'effet suit l'arme quand le
            // personnage bouge ou frappe, au lieu de rester fige derriere lui
            // (les prefabs du pack sont souvent en World pour du decor fixe).
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            // Play explicite : le prefab est instancie en cours de partie,
            // playOnAwake ne suffit pas toujours dans ce cas.
            ps.Play(true);
        }

        // Filet de securite contre une faute de frappe dans la liste : un nom
        // qui ne correspond a aucun sous-effet ne desactiverait rien, en
        // silence. On previent alors en listant les noms reellement valides.
        if (layer.disabledSubEffects != null)
        {
            foreach (string disabled in layer.disabledSubEffects)
            {
                if (string.IsNullOrWhiteSpace(disabled)) continue;
                if (subNames.Exists(n => string.Equals(n, disabled.Trim(), StringComparison.OrdinalIgnoreCase))) continue;

                Debug.LogWarning($"[EnchantAura] Calque '{layer.name}' : aucun sous-effet nomme '{disabled}'. " +
                                 $"Noms disponibles dans {layer.prefab.name} : {string.Join(", ", subNames)}");
            }
        }
    }

    private static bool IsSubEffectDisabled(EnchantAuraLayer layer, string subName)
    {
        if (layer.disabledSubEffects == null) return false;

        foreach (string disabled in layer.disabledSubEffects)
        {
            if (string.IsNullOrWhiteSpace(disabled)) continue;
            if (string.Equals(disabled.Trim(), subName, System.StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    // Un mesh doit etre "Read/Write Enabled" a l'import pour servir de source
    // d'emission (Unity doit echantillonner ses triangles). Les modeles
    // d'armes de ce projet sont importes avec isReadable = 0 : le mode
    // WeaponMesh n'emet alors AUCUNE particule, sans la moindre erreur en
    // console - seul un eventuel composant Light du prefab reste visible,
    // ce qui se lit comme "un projecteur et rien d'autre". D'ou le repli
    // automatique sur une boite, avec un avertissement explicite.
    private static bool _warnedAboutUnreadableMesh;

    private static void ApplyShape(ParticleSystem ps, EnchantAuraLayer layer, Renderer mainRenderer, Vector3 localSize, float weaponLength)
    {
        if (layer.shapeMode == EnchantAuraShapeMode.PrefabDefault) return;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;

        EnchantAuraShapeMode mode = layer.shapeMode;
        // Quand on pilote la forme, c'est scaleRatio qui dimensionne la zone
        // d'emission (le transform du calque reste a 1, cf. BuildLayer).
        float ratio = layer.scaleRatio > 0f ? layer.scaleRatio : 1f;

        if (mode == EnchantAuraShapeMode.WeaponMesh && !IsRendererMeshReadable(mainRenderer))
        {
            if (!_warnedAboutUnreadableMesh)
            {
                _warnedAboutUnreadableMesh = true;
                Debug.LogWarning(
                    "[EnchantAura] Mode 'WeaponMesh' inutilisable : les modeles d'armes ne sont pas importes en " +
                    "'Read/Write Enabled', Unity ne peut donc pas y echantillonner de particules (aucune emission, " +
                    "aucune erreur). Repli automatique sur 'Box'. C'est volontaire : activer Read/Write dupliquerait " +
                    "le mesh en RAM pour chaque arme du jeu, ce qu'on refuse pour un effet purement esthetique. " +
                    "Utilisez plutot les modes Box ou Edge, qui suivent la forme de l'arme pour un cout nul.");
            }
            mode = EnchantAuraShapeMode.Box;
        }

        switch (mode)
        {
            case EnchantAuraShapeMode.WeaponMesh:
                // Emission depuis le renderer REEL de l'arme : Unity gere
                // lui-meme sa transformation et son echelle, donc l'effet
                // epouse le contour exact quel que soit le type d'arme, sans
                // axe a deviner ni correction de rotation par type.
                if (mainRenderer is SkinnedMeshRenderer skinned)
                {
                    shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
                    shape.skinnedMeshRenderer = skinned;
                }
                else if (mainRenderer is MeshRenderer meshRenderer)
                {
                    shape.shapeType = ParticleSystemShapeType.MeshRenderer;
                    shape.meshRenderer = meshRenderer;
                }
                shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
                break;

            case EnchantAuraShapeMode.Box:
                // Boite calee sur les dimensions REELLES de l'arme. Pour une
                // lame, la boite englobante est naturellement longue et fine,
                // donc les particules suivent deja bien la forme - pour un
                // cout nul, contrairement a l'emission depuis le mesh.
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = localSize * ratio;
                break;

            case EnchantAuraShapeMode.Edge:
                // Ligne le long de l'axe le plus long de l'arme. L'Edge de
                // Unity est oriente sur X : on pivote donc pour l'aligner sur
                // l'axe reellement le plus long du modele (detecte, jamais
                // devine par type d'arme).
                shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
                shape.radius = Mathf.Max(localSize.x, Mathf.Max(localSize.y, localSize.z)) * 0.5f * ratio;
                if (localSize.y >= localSize.x && localSize.y >= localSize.z)
                {
                    shape.rotation = new Vector3(0f, 0f, 90f);
                }
                else if (localSize.z >= localSize.x && localSize.z >= localSize.y)
                {
                    shape.rotation = new Vector3(0f, 90f, 0f);
                }
                else
                {
                    shape.rotation = Vector3.zero;
                }
                break;

            case EnchantAuraShapeMode.Sphere:
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = weaponLength * 0.5f * ratio;
                shape.radiusThickness = 1f;
                break;
        }
    }

    private static bool IsRendererMeshReadable(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinned)
        {
            return skinned.sharedMesh != null && skinned.sharedMesh.isReadable;
        }

        if (renderer != null && renderer.TryGetComponent(out MeshFilter meshFilter))
        {
            return meshFilter.sharedMesh != null && meshFilter.sharedMesh.isReadable;
        }

        return false;
    }

    private static void ApplyAuraColor(Transform root, EnchantAuraSettings settings, int enchantEffect)
    {
        Color auraColor = settings.EvaluateColor(enchantEffect);

        foreach (EnchantAuraLayer layer in settings.layers)
        {
            if (layer == null || !layer.enabled || layer.prefab == null) continue;
            if (!layer.applyEnchantColor) continue;

            Transform layerTransform = root.Find(layer.prefab.name);
            if (layerTransform == null) continue;

            float intensity = Mathf.Max(0f, layer.colorIntensity);
            // L'opacite du calque se transporte dans l'alpha de la teinte :
            // c'est ce qui permet d'attenuer un effet qui masque l'arme sans
            // toucher a sa couleur ni a son intensite lumineuse.
            Color tint = auraColor;
            tint.a *= Mathf.Clamp01(layer.opacity);

            // La re-teinte passe par EnchantAuraTintTarget, qui repart des
            // couleurs d'origine du materiau (emission comprise) : elle
            // conserve la luminosite voulue par l'artiste et n'en change que
            // la teinte, sans jamais accumuler d'une passe a l'autre.
            foreach (EnchantAuraTintTarget target in layerTransform.GetComponentsInChildren<EnchantAuraTintTarget>(true))
            {
                target.ApplyTint(tint, intensity);
            }
        }
    }
}
