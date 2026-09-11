using UnityEngine;

// Etal cosmetique pose sous un marchand : tapis et caisses. Le serveur ne le
// connait pas, il n'a donc aucun collider. Enfant de l'entite, il disparait avec elle.
public class PrivateStoreStall : MonoBehaviour
{
    private const string MeshRoot = "Data/StaticMeshes/";
    private const string MaterialRoot = "Data/Textures/";
    private const float CarpetSize = 1.4f;
    private const float BoxHeight = 0.35f;

    private GameObject _stall;
    private OperateType _shown = OperateType.None;

    public static void Refresh(Entity entity)
    {
        OperateType type = StallType(entity.OperateType);
        PrivateStoreStall stall = entity.GetComponent<PrivateStoreStall>();

        if (stall == null)
        {
            if (type == OperateType.None)
            {
                return;
            }

            stall = entity.gameObject.AddComponent<PrivateStoreStall>();
        }

        stall.Show(type);
    }

    // La vente en lot partage l'etal de la vente ; les modes de gestion n'en ont pas.
    private static OperateType StallType(OperateType type)
    {
        switch (type)
        {
            case OperateType.Sell:
            case OperateType.PackageSell:
                return OperateType.Sell;
            case OperateType.Buy:
                return OperateType.Buy;
            case OperateType.Manufacture:
                return OperateType.Manufacture;
            default:
                return OperateType.None;
        }
    }

    private void Show(OperateType type)
    {
        if (type == _shown)
        {
            return;
        }

        if (_stall != null)
        {
            Destroy(_stall);
            _stall = null;
        }

        _shown = type;

        if (type != OperateType.None)
        {
            _stall = Build(type);
        }
    }

    private GameObject Build(OperateType type)
    {
        GameObject root = new GameObject("PrivateStoreStall");
        root.transform.SetParent(transform, false);

        Vector3 ground = transform.position;
        ground.y = World.Instance.GetGroundHeight(ground);
        root.transform.position = ground;

        Bounds carpet;

        switch (type)
        {
            case OperateType.Buy:
                carpet = AddPiece(root, "orcguild_obj_s/orcguild_carpet001", new[] { "orcguild_obj_t/Materials/orcguild_carpet" }, CarpetSize, true, false);
                break;
            case OperateType.Manufacture:
                carpet = AddPiece(root, "interior_Aa_s/circlecarpet", new[] { "interior_Aa_t/Materials/interior_Aa_bank_carpet" }, CarpetSize, true, false);
                break;
            default:
                carpet = AddPiece(root, "speaking_magic_s/SI_Magic_carpet02", new[] { "speaking_magic_t/Materials/sp_magic014", "speaking_magic_t/Materials/sp_magic015" }, CarpetSize, true, false);
                break;
        }

        // Ht_vi_box01 regroupe deja trois caisses : une seule piece, au coin
        // arriere du tapis. Les caisses de Barkas ont un materiau givre.
        Bounds crates = AddPiece(root, "Hunter_Village_S/Ht_vi_box01", new[] { "Hunter_Village_t/Materials/Ht_Vi_box" }, BoxHeight, false, true);

        // Coin arriere gauche, en debordant du bord : le centre du tapis, ou
        // le marchand est assis, reste degage.
        Transform cratesTransform = root.transform.GetChild(1);
        cratesTransform.localPosition += new Vector3(-carpet.extents.x + crates.extents.x * 0.8f, 0f, -carpet.extents.z - crates.extents.z * 0.4f);
        cratesTransform.localRotation = Quaternion.Euler(0f, 12f, 0f);

        return root;
    }

    // Mise a l'echelle sur la plus grande dimension au sol (tapis) ou sur la
    // hauteur (caisse), puis pose au sol, centree sur l'origine de l'etal.
    private static Bounds AddPiece(GameObject root, string meshPath, string[] materialPaths, float size, bool byFootprint, bool castShadows)
    {
        GameObject piece = new GameObject(meshPath.Substring(meshPath.LastIndexOf('/') + 1));
        piece.transform.SetParent(root.transform, false);

        Mesh mesh = Resources.Load<Mesh>(MeshRoot + meshPath);
        if (mesh == null)
        {
            Mesh[] meshes = Resources.LoadAll<Mesh>(MeshRoot + meshPath);
            mesh = meshes.Length > 0 ? meshes[0] : null;
        }

        if (mesh == null)
        {
            Debug.LogWarning($"[PrivateStoreStall] Mesh introuvable : {meshPath}");
            return new Bounds(Vector3.zero, Vector3.one * size);
        }

        piece.AddComponent<MeshFilter>().sharedMesh = mesh;

        Material[] materials = new Material[materialPaths.Length];
        for (int i = 0; i < materialPaths.Length; i++)
        {
            materials[i] = Resources.Load<Material>(MaterialRoot + materialPaths[i]);
        }

        MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;

        Bounds b = mesh.bounds;
        float reference = byFootprint ? Mathf.Max(b.size.x, b.size.z) : b.size.y;
        float scale = reference > 0.0001f ? size / reference : 1f;

        piece.transform.localScale = Vector3.one * scale;
        piece.transform.localPosition = new Vector3(-b.center.x * scale, -b.min.y * scale + 0.01f, -b.center.z * scale);

        return new Bounds(Vector3.zero, b.size * scale);
    }
}
