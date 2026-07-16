using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class SkinnedMeshSync : MonoBehaviour
{
    [SerializeField] private Transform _bodyPartsContainer;
    [SerializeField] private SkinnedMeshRenderer _rootSkinnedRenderer;
    [SerializeField] private SkinnedMeshRenderer[] _destSkinnedRenderer;
    [SerializeField] private Transform _rootBone;

    [Header("Attache rigide visage/cheveux (comportement du moteur L2 d'origine)")]
    [Tooltip("Quand actif, les pieces visage/cheveux (_f, _ah, _bh) sont attachees RIGIDEMENT a l'os 'Bip01_head' (tous les sommets suivent cet os), au lieu d'evaluer leurs poids de skin. Les poids de skin de ces pieces sont incoherents avec le squelette anime dans les donnees du client source (verifie numeriquement : couture cou de ~4.5 cm en pose skinnee contre ~2.3 cm - niveau bind - en attache rigide). Le moteur d'origine attache ces pieces rigidement, comme le chemin 'Hair' deja utilise pour les cheveux sans armature.")]
    [SerializeField] private bool _rigidFaceHairAttach = true;

    [Header("Re-bind visage/cheveux sur le rig des animations")]
    [Tooltip("Prefab de piece 'ah' de la race (ex: FOrc_m000_m00_ah). Son mesh est binde sur le rig D'ORIGINE, celui sur lequel les clips d'animation ont ete exportes - contrairement au corps/visage/cheveux B, re-bindes sur un rig aux poses de repos differentes (jusqu'a ~9 cm / 5 deg d'ecart a la tete). Quand ce champ est assigne, les pieces visage/cheveux empruntent les bindposes de cette piece pour les os partages, ce qui les fait suivre les animations comme la piece 'ah' (la seule qui s'affichait correctement).")]
    [SerializeField] private GameObject _bindDonorPiece;

    [Header("Correctif calibration visage/cheveux (TEST)")]
    [Tooltip("Rotation additionnelle (degres, espace local de l'os 'head') appliquee uniquement aux pieces visage/cheveux (_f, _ah, _bh) pour recaler leur bindpose sur l'os 'head' du squelette partage.")]
    [SerializeField] private Vector3 _headCorrectionEuler = Vector3.zero;
    [Tooltip("Position additionnelle (espace local de l'os 'head') appliquee uniquement aux pieces visage/cheveux.")]
    [SerializeField] private Vector3 _headCorrectionPosition = Vector3.zero;

    void OnValidate()
    {
        // Unity interdit d'appeler StartCoroutine directement depuis OnValidate,
        // donc on utilise EditorApplication.delayCall. On appelle DoSync()
        // directement (pas SyncMesh()/sa coroutine) : SyncMesh() attend un
        // WaitForEndOfFrame qui ne se declenche jamais tant que le Play mode
        // est en PAUSE (la Player Loop est gelee), donc le correctif ne
        // s'appliquerait jamais visuellement si on reglait les valeurs a
        // l'arret sur une image - ce qui etait precisement le cas signale.
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            EditorApplication.delayCall += DelayedResync;
        }
#endif
    }

#if UNITY_EDITOR
    private void DelayedResync()
    {
        if (this != null && _bodyPartsContainer != null)
        {
            DoSync();
        }
    }
#endif

    public Transform RootBone { get { return _rootBone; } }

    void Start()
    {
        SyncMesh();
    }

    void Update()
    {
#if (UNITY_EDITOR)
        if (!EditorApplication.isPlaying)
        {
            SyncMesh();
        }
#endif
    }

    public void SyncMesh()
    {
        // Only refresh once the entity is enabled
        if (this.gameObject.activeSelf && this.gameObject.activeInHierarchy)
        {
            StartCoroutine(SyncTask());
        }
    }

    private IEnumerator SyncTask()
    {
        if (_bodyPartsContainer == null)
        {
            _bodyPartsContainer = transform.parent.GetChild(1);
        }
        if (_rootSkinnedRenderer == null)
        {
            _rootSkinnedRenderer = transform.GetChild(1).GetComponent<SkinnedMeshRenderer>();
        }

        // float startTime = Time.time;
        // while (_bodyPartsContainer.childCount > 8)
        // {
        yield return new WaitForEndOfFrame();
        //     if (Time.time - startTime < 1.0f)
        //     {
        //         Debug.LogWarning("Could not sync mesh.");
        //         yield break;
        //     }
        // }

        DoSync();
    }

    private void DoSync()
    {
        // Debug.LogWarning($"[{transform.name}] SyncMesh");
        _bodyPartsContainer.gameObject.SetActive(true);

        // Le "_Anim.prefab" (squelette partage) est genere en instanciant tel quel
        // le FBX des bottes nues (<Type>_m000_b.fbx, cf. OrcShamanPrefabGenerator.
        // GenerateAnimPrefabs), uniquement pour recuperer la hierarchie d'os. Ce
        // mesh de bottes nues n'a jamais ete desactive : il reste visible et se
        // superpose aux bottes reellement equipees (cause probable du bug de
        // texture "bottes").
        if (_rootSkinnedRenderer.enabled)
        {
            _rootSkinnedRenderer.enabled = false;
        }

        // Retrieving SkinnedMeshRenderers
        _destSkinnedRenderer = new SkinnedMeshRenderer[8];

        for (byte i = 0; i < _bodyPartsContainer.childCount; i++)
        {
            Transform child = _bodyPartsContainer.GetChild(i);
            _destSkinnedRenderer[i] = child.GetComponent<SkinnedMeshRenderer>();
        }

        // Updating body parts bones and bounds
        Bounds bounds = new Bounds();
        bounds.center = new Vector3(0, 0, 0f);
        bounds.extents = new Vector3(0.0025f, 0.002f, 0.004f);
        bounds.size = bounds.extents * 2f;

        // Dans ce pipeline (import PSK/Blender), renderer.bones n'est jamais cable a
        // l'import : c'est un tableau de references vides que SkinnedMeshSync doit
        // remplir entierement a partir du squelette racine. Une simple copie
        // positionnelle (ou une troncature) suppose que l'ordre des os de la piece
        // est un prefixe coherent du squelette de reference, ce qui n'est pas
        // toujours vrai. Quand PieceBoneNames est present (piece regeneree via
        // l'etape 1 apres correctif), on remappe par NOM d'os plutot que par
        // position. Sinon, on retombe sur la troncature positionnelle (comportement
        // precedent, pour ne rien casser sur les pieces pas encore regenerees).
        //
        // Le nom d'os ne suffit pas a lui seul : meme avec le bon os assigne, le
        // bindpose (matrice "pose de repos") du mesh de la piece peut ne pas
        // correspondre exactement a celle du squelette partage (donnees d'export
        // divergentes entre le corps et le visage/cheveux), causant un decalage
        // rigide constant (visible surtout quand le squelette bouge, ex: course).
        // On reutilise donc le bindpose du mesh du CORPS (deja valide visuellement)
        // pour chaque os partage par son nom, plutot que le bindpose d'origine de
        // la piece.
        Transform[] rootBones = _rootSkinnedRenderer.bones;
        Matrix4x4[] rootBindposes = _rootSkinnedRenderer.sharedMesh != null ? _rootSkinnedRenderer.sharedMesh.bindposes : null;
        Dictionary<string, Transform> rootBonesByName = null;
        Dictionary<string, Matrix4x4> rootBindposeByName = null;

        // Bindposes du rig d'origine (celui des clips d'animation), lues depuis la
        // piece donneuse 'ah'. Prioritaires sur tout le reste pour les pieces
        // visage/cheveux : c'est le correctif de fond du decalage tete Orc/Shaman.
        Dictionary<string, Matrix4x4> donorBindposeByName = null;
        if (_bindDonorPiece != null)
        {
            // Deux formes de donneur acceptees :
            //  - prefab de piece genere (etape 1) : noms d'os dans PieceBoneNames ;
            //  - FBX source : noms lus directement sur les os cables de son
            //    SkinnedMeshRenderer (potentiellement sur un enfant).
            PieceBoneNames donorNames = _bindDonorPiece.GetComponent<PieceBoneNames>();
            SkinnedMeshRenderer donorSmr = _bindDonorPiece.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (donorSmr == null || donorSmr.sharedMesh == null)
            {
                Debug.LogWarning($"[SkinnedMeshSync] piece donneuse '{_bindDonorPiece.name}': pas de SkinnedMeshRenderer/mesh, re-bind ignore.");
            }
            else
            {
                Matrix4x4[] donorBindposes = donorSmr.sharedMesh.bindposes;
                string[] names = null;
                if (donorNames != null && donorNames.boneNames != null && donorNames.boneNames.Length == donorBindposes.Length)
                {
                    names = donorNames.boneNames;
                }
                else if (donorSmr.bones != null && donorSmr.bones.Length == donorBindposes.Length)
                {
                    names = new string[donorSmr.bones.Length];
                    for (int d = 0; d < donorSmr.bones.Length; d++)
                    {
                        names[d] = donorSmr.bones[d] != null ? donorSmr.bones[d].name : null;
                    }
                }

                if (names != null)
                {
                    donorBindposeByName = new Dictionary<string, Matrix4x4>();
                    for (int d = 0; d < donorBindposes.Length; d++)
                    {
                        if (names[d] == null) continue;
                        string key = NormalizeBoneName(names[d]);
                        if (!donorBindposeByName.ContainsKey(key))
                        {
                            donorBindposeByName.Add(key, donorBindposes[d]);
                        }
                    }
                    Debug.Log($"[SkinnedMeshSync] {transform.root.name}: donneur '{_bindDonorPiece.name}' charge, {donorBindposeByName.Count} bindposes du rig d'origine disponibles.");
                }
                else
                {
                    Debug.LogWarning($"[SkinnedMeshSync] piece donneuse '{_bindDonorPiece.name}': impossible d'associer noms d'os et bindposes (ni PieceBoneNames ni bones[] cables de meme taille), re-bind ignore.");
                }
            }
        }

        foreach (var renderer in _destSkinnedRenderer)
        {
            if (renderer != null)
            {
                _rootBone = _rootSkinnedRenderer.rootBone;
                renderer.rootBone = _rootSkinnedRenderer.rootBone;
                renderer.transform.localScale = Vector3.one;
                // TEST: l'etape 1 (generation des prefabs de pieces) applique une
                // rotation locale de -90 degres (conversion d'axe PSK -> Unity) sur le
                // GameObject du renderer. Le scale est deja reinitialise ci-dessus mais
                // la rotation ne l'etait jamais - on teste si cette rotation residuelle
                // influence quand meme le rendu du skin (meme avec des os externes).
                renderer.transform.localRotation = Quaternion.identity;

                int requiredBoneCount = renderer.sharedMesh != null ? renderer.sharedMesh.bindposeCount : rootBones.Length;
                PieceBoneNames boneNamesCache = renderer.GetComponent<PieceBoneNames>();
                bool isFaceOrHairPiece = IsFaceOrHairPiece(renderer.name);

                if (boneNamesCache != null && boneNamesCache.boneNames != null && boneNamesCache.boneNames.Length == requiredBoneCount)
                {
                    if (rootBonesByName == null)
                    {
                        // Les noms d'os different selon la source d'export : espaces
                        // ("Bip01 Pelvis") pour les pieces d'equipement contre underscores
                        // ("Bip01_Pelvis") pour le squelette de reference, avec en plus
                        // une casse incoherente meme au sein du squelette de reference
                        // (ex: "bip01_spine" vs "Bip01_Spine1"). On normalise donc les
                        // deux cotes avant comparaison.
                        rootBonesByName = new Dictionary<string, Transform>();
                        for (int r = 0; r < rootBones.Length; r++)
                        {
                            Transform bone = rootBones[r];
                            if (bone == null) continue;
                            string key = NormalizeBoneName(bone.name);
                            if (!rootBonesByName.ContainsKey(key))
                            {
                                rootBonesByName.Add(key, bone);
                            }
                        }

                        if (rootBindposes != null && rootBindposes.Length == rootBones.Length)
                        {
                            rootBindposeByName = new Dictionary<string, Matrix4x4>();
                            for (int r = 0; r < rootBones.Length; r++)
                            {
                                Transform bone = rootBones[r];
                                if (bone == null) continue;
                                string key = NormalizeBoneName(bone.name);
                                if (!rootBindposeByName.ContainsKey(key))
                                {
                                    rootBindposeByName.Add(key, rootBindposes[r]);
                                }
                            }
                        }
                    }

                    // On cache le bindpose D'ORIGINE (avant tout clonage/correctif) au
                    // premier passage. Sans ca, un second SyncMesh() (ex: reglage live du
                    // correctif visage/cheveux via OnValidate) relirait renderer.sharedMesh
                    // qui est deja notre clone corrige, et cumulerait le correctif a
                    // chaque appel au lieu de repartir de la valeur d'origine.
                    if (boneNamesCache.pristineBindposes == null)
                    {
                        boneNamesCache.pristineBindposes = renderer.sharedMesh.bindposes;
                    }

                    Transform[] remappedBones = new Transform[requiredBoneCount];
                    Matrix4x4[] originalBindposes = boneNamesCache.pristineBindposes;
                    Matrix4x4[] remappedBindposes = new Matrix4x4[requiredBoneCount];
                    int donorApplied = 0;

                    bool hasHeadCorrection = isFaceOrHairPiece &&
                        (_headCorrectionEuler != Vector3.zero || _headCorrectionPosition != Vector3.zero);
                    Matrix4x4 headCorrection = hasHeadCorrection
                        ? Matrix4x4.TRS(_headCorrectionPosition, Quaternion.Euler(_headCorrectionEuler), Vector3.one)
                        : Matrix4x4.identity;

                    for (int i = 0; i < requiredBoneCount; i++)
                    {
                        string boneName = boneNamesCache.boneNames[i];
                        string normalizedName = boneName != null ? NormalizeBoneName(boneName) : null;

                        remappedBindposes[i] = originalBindposes[i];

                        if (normalizedName != null && rootBonesByName.TryGetValue(normalizedName, out Transform match))
                        {
                            remappedBones[i] = match;

                            Matrix4x4 refBindpose = default;
                            bool hasRefBindpose = rootBindposeByName != null && rootBindposeByName.TryGetValue(normalizedName, out refBindpose);

                            Matrix4x4 targetBindpose = (hasRefBindpose && refBindpose != originalBindposes[i])
                                ? refBindpose
                                : originalBindposes[i];

                            // Re-bind sur le rig des animations : pour les pieces visage/
                            // cheveux, le bindpose du rig d'origine (piece 'ah') remplace
                            // celui du rig re-exporte. Les deux rigs different (ex FOrc :
                            // 8.8 cm / 4.5 deg a l'os tete) et les clips suivent le rig
                            // d'origine - sans ce remplacement, la tete flotte/derive.
                            if (isFaceOrHairPiece && donorBindposeByName != null && donorBindposeByName.TryGetValue(normalizedName, out Matrix4x4 donorBindpose))
                            {
                                targetBindpose = donorBindpose;
                                donorApplied++;
                            }

                            // Correctif de calibration (TEST): on insere une petite rotation/
                            // translation dans l'espace LOCAL de l'os 'head', mais uniquement
                            // pour les pieces visage/cheveux (_f, _ah, _bh). newBindpose =
                            // correction * bindpose revient a traiter "bone * correction" comme
                            // un os legerement nudge, rien que pour le calcul de skinning de
                            // CE mesh (le corps, qui utilise le meme os 'head', n'est pas affecte).
                            if (hasHeadCorrection && normalizedName.Contains("head"))
                            {
                                targetBindpose = headCorrection * targetBindpose;
                            }

                            remappedBindposes[i] = targetBindpose;
                        }
                        else
                        {
                            Debug.LogWarning($"[SkinnedMeshSync] {renderer.name}: os '{boneName ?? "NULL"}' (index {i}) introuvable dans le squelette de reference.");
                        }
                    }
                    // Attache rigide : tous les sommets suivent l'os head, avec le bindpose
                    // head PROPRE a la piece (auto-coherent meme si la piece est bindee sur
                    // une autre generation de rig, ex. cheveux 'ah'). Ecrase le remap par
                    // poids ci-dessus : bones[i] = head et bindpose[i] = bindpose(head)
                    // pour tous les i => le skinning devient une transformation rigide.
                    if (isFaceOrHairPiece && _rigidFaceHairAttach)
                    {
                        Transform headBone = null;
                        Matrix4x4 headBindpose = Matrix4x4.identity;
                        bool headFound = false;
                        for (int i = 0; i < requiredBoneCount; i++)
                        {
                            string bn = boneNamesCache.boneNames[i];
                            if (bn != null && NormalizeBoneName(bn) == "bip01_head")
                            {
                                headBindpose = originalBindposes[i];
                                rootBonesByName.TryGetValue("bip01_head", out headBone);
                                headFound = headBone != null;
                                break;
                            }
                        }
                        if (headFound)
                        {
                            for (int i = 0; i < requiredBoneCount; i++)
                            {
                                remappedBones[i] = headBone;
                                remappedBindposes[i] = headBindpose;
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[SkinnedMeshSync] {renderer.name}: os 'bip01_head' introuvable (piece ou squelette), attache rigide impossible - skinning par poids conserve.");
                        }
                    }

                    renderer.bones = remappedBones;

                    // On compare au bindpose ACTUEL du mesh assigne (pas au pristine) : au
                    // premier passage c'est l'asset d'origine, mais lors d'un re-sync (ex:
                    // reglage live du correctif) c'est deja notre clone precedent. Ca permet
                    // de detecter aussi bien "il faut appliquer un correctif" que "il faut
                    // revenir en arriere" (correctif remis a zero), sans jamais cumuler.
                    Matrix4x4[] currentBindposes = renderer.sharedMesh.bindposes;
                    bool bindposeChanged = !BindposesEqual(currentBindposes, remappedBindposes);

                    if (bindposeChanged)
                    {
                        // On clone le mesh pour ne pas modifier l'asset partage
                        // (utilise par toutes les instances de cette piece).
                        Mesh clonedMesh = Object.Instantiate(renderer.sharedMesh);
                        clonedMesh.bindposes = remappedBindposes;
                        renderer.sharedMesh = clonedMesh;
                    }

                    if (isFaceOrHairPiece && donorBindposeByName != null)
                    {
                        Debug.Log($"[SkinnedMeshSync] {renderer.name}: {donorApplied}/{requiredBoneCount} os re-bindes sur le rig du donneur, bindposes {(bindposeChanged ? "MODIFIES (mesh clone)" : "inchanges")}.");
                    }
                }
                else if (requiredBoneCount == rootBones.Length)
                {
                    renderer.bones = rootBones;
                }
                else if (requiredBoneCount < rootBones.Length)
                {
                    Transform[] truncatedBones = new Transform[requiredBoneCount];
                    System.Array.Copy(rootBones, truncatedBones, requiredBoneCount);
                    renderer.bones = truncatedBones;
                }
                else
                {
                    Debug.LogWarning($"[SkinnedMeshSync] {renderer.name}: la piece attend {requiredBoneCount} os mais le squelette de reference n'en a que {rootBones.Length}.");
                    renderer.bones = rootBones;
                }

                renderer.localBounds = bounds;
            }
        }
    }

    private static string NormalizeBoneName(string name)
    {
        return name.Replace(' ', '_').ToLowerInvariant();
    }

    // Pieces visage ("..._f") et cheveux ("..._ah" / "..._bh", potentiellement
    // suivies d'un suffixe de variante avant "(Clone)"). Les pieces d'armure
    // (_u, _l, _g, _b pour boots) sont volontairement exclues : elles utilisent
    // aussi l'os "head" (poids residuels pres du col) mais s'affichent deja
    // correctement, on ne veut pas leur appliquer le correctif tete.
    private static bool IsFaceOrHairPiece(string rendererName)
    {
        string name = rendererName.Replace("(Clone)", "");
        return name.EndsWith("_f") || name.EndsWith("_ah") || name.EndsWith("_bh");
    }

    private static bool BindposesEqual(Matrix4x4[] a, Matrix4x4[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
}
