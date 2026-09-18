using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Les portes du monde sont de simples meshes statiques importes de la carte :
/// le serveur ne connait que leur position, le client doit retrouver le bon
/// battant dans la scene puis le faire pivoter lui-meme.
public class DoorManager : MonoBehaviour
{
    private const float BindRadius = 2f;
    private const float PairRadius = 2f;
    private const float OpenAngle = 90f;
    private const float OpenDuration = 1.2f;

    private class Door
    {
        public int Id;
        public int ObjectId;
        public Vector3 Position;
        public bool Opened;
        public Transform Leaf;
        public Vector3 ClosedPosition;
        public Quaternion ClosedRotation;
        public Vector3 Hinge;
        public Bounds LocalBounds;
        public Matrix4x4 ClosedMatrix;
        public float Direction = 1f;
        public float Angle;
        public Coroutine Motion;
    }

    private static DoorManager _instance;
    public static DoorManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("DoorManager");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<DoorManager>();
            }

            return _instance;
        }
    }

    private readonly Dictionary<int, Door> _doors = new Dictionary<int, Door>();

    /// Premiere annonce d'une porte : on note sa position et on tente le
    /// rattachement. La region n'est pas forcement chargee, d'ou les essais.
    public void Register(int objectId, int doorId, Vector3 position, bool opened)
    {
        if (!_doors.TryGetValue(doorId, out Door door))
        {
            door = new Door { Id = doorId };
            _doors[doorId] = door;
        }

        door.ObjectId = objectId;
        door.Position = position;
        door.Opened = opened;

        if (door.Leaf == null)
        {
            StartCoroutine(BindWhenAvailable(door));
        }
    }

    /// Sans creer le gestionnaire : le survol de la souris l'interroge a chaque image.
    public static bool TryFindDoor(Transform leaf, out int objectId, out Vector3 position)
    {
        objectId = 0;
        position = Vector3.zero;
        return _instance != null && _instance.TryGetObjectId(leaf, out objectId, out position);
    }

    /// Le battant clique, s'il appartient a une porte connue du serveur.
    public bool TryGetObjectId(Transform leaf, out int objectId, out Vector3 position)
    {
        foreach (Door door in _doors.Values)
        {
            if (door.Leaf != null && door.Leaf == leaf)
            {
                objectId = door.ObjectId;
                position = door.ClosedPosition;
                return true;
            }
        }

        objectId = 0;
        position = Vector3.zero;
        return false;
    }

    public void SetState(int doorId, bool opened)
    {
        if (!_doors.TryGetValue(doorId, out Door door) || door.Opened == opened)
        {
            return;
        }

        door.Opened = opened;
        Animate(door);
    }

    private IEnumerator BindWhenAvailable(Door door)
    {
        for (int attempt = 0; attempt < 30 && door.Leaf == null; attempt++)
        {
            Bind(door);
            if (door.Leaf == null)
            {
                yield return new WaitForSeconds(1f);
            }
        }

        if (door.Leaf == null)
        {
            Debug.LogWarning($"Porte {door.Id} : aucun battant trouve autour de {door.Position}.");
            yield break;
        }

        // L'etat initial peut deja etre "ouvert" (porte laissee ouverte).
        if (door.Opened)
        {
            Animate(door, true);
        }
    }

    private void Bind(Door door)
    {
        Transform best = null;
        float bestDistance = float.MaxValue;

        foreach (Collider collider in Physics.OverlapSphere(door.Position, BindRadius))
        {
            if (collider.name.IndexOf("door", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            float distance = Vector3.Distance(collider.transform.position, door.Position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = collider.transform;
            }
        }

        if (best == null)
        {
            return;
        }

        door.Leaf = best;
        door.ClosedPosition = best.position;
        door.ClosedRotation = best.rotation;
        door.Hinge = best.position;

        Mesh mesh = null;
        MeshFilter filter = best.GetComponent<MeshFilter>();
        if (filter != null) mesh = filter.sharedMesh;
        if (mesh == null && best.GetComponent<Collider>() is MeshCollider meshCollider) mesh = meshCollider.sharedMesh;
        door.LocalBounds = mesh != null ? mesh.bounds : new Bounds();
        door.ClosedMatrix = best.localToWorldMatrix;

        ResolveHinges(door);
    }

    /// Le pivot des meshes de porte n'est pas forcement au gond : on le deduit
    /// des bornes du mesh, a l'extremite du battant opposee a l'autre battant.
    private void ResolveHinges(Door door)
    {
        foreach (Door other in _doors.Values)
        {
            if (other == door || other.Leaf == null)
            {
                continue;
            }

            Vector3 axis = Centre(other) - Centre(door);
            axis.y = 0f;
            if (axis.magnitude > PairRadius)
            {
                continue;
            }

            axis.Normalize();
            door.Hinge = FarEnd(door, -axis);
            other.Hinge = FarEnd(other, axis);
            door.Direction = 1f;
            other.Direction = -1f;
            return;
        }
    }

    private static Vector3 Centre(Door door)
    {
        return door.ClosedMatrix.MultiplyPoint3x4(door.LocalBounds.center);
    }

    private static Vector3 FarEnd(Door door, Vector3 axis)
    {
        Vector3 centre = Centre(door);
        Vector3 extents = door.LocalBounds.extents;
        float reach = 0f;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = door.LocalBounds.center + new Vector3(
                (i & 1) == 0 ? -extents.x : extents.x,
                (i & 2) == 0 ? -extents.y : extents.y,
                (i & 4) == 0 ? -extents.z : extents.z);
            reach = Mathf.Max(reach, Vector3.Dot(door.ClosedMatrix.MultiplyPoint3x4(corner) - centre, axis));
        }

        Vector3 hinge = centre + axis * reach;
        hinge.y = door.ClosedPosition.y;
        return hinge;
    }

    private void Animate(Door door, bool immediate = false)
    {
        if (door.Leaf == null)
        {
            return;
        }

        if (door.Motion != null)
        {
            StopCoroutine(door.Motion);
        }

        door.Motion = StartCoroutine(Swing(door, immediate));
    }

    private IEnumerator Swing(Door door, bool immediate)
    {
        float from = door.Angle;
        float to = door.Opened ? OpenAngle * door.Direction : 0f;
        float elapsed = immediate ? OpenDuration : 0f;

        while (elapsed < OpenDuration)
        {
            elapsed += Time.deltaTime;
            Apply(door, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / OpenDuration)));
            yield return null;
        }

        Apply(door, to);
        door.Motion = null;
    }

    private void Apply(Door door, float angle)
    {
        door.Angle = angle;
        Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
        door.Leaf.SetPositionAndRotation(
            door.Hinge + rotation * (door.ClosedPosition - door.Hinge),
            rotation * door.ClosedRotation);
    }
}
