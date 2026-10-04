using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A position where new physics objects are spawned.
/// Child objects (e.g. a sphere used to see the spawn point while designing) are removed on startup,
/// so only the transform remains.
/// The prefab list only accepts prefabs that have a ColliderVisualizer, so every spawned object
/// shows its collider.
/// Hook a UI Button's OnClick up to SpawnItem().
/// </summary>
[AddComponentMenu("Physics/Spawn Point")]
public class SpawnPoint : MonoBehaviour
{
    [Tooltip("Prefabs to spawn from. Only prefabs with a ColliderVisualizer can be added.")]
    [SerializeField] List<ColliderVisualizer> prefabs = new();

    [Tooltip("Remove all child objects on startup (design-time markers).")]
    [SerializeField] bool removeChildrenOnStart = true;

    [Tooltip("Give each spawned object a random rotation around the Y axis.")]
    [SerializeField] bool randomYaw = true;

    [Tooltip("If the spawn position is occupied, move the new object up until it is free. " +
             "Without this, overlapping colliders are pushed apart violently by the physics engine.")]
    [SerializeField] bool avoidOverlap = true;
    [SerializeField] float stackGap = 0.05f;
    [SerializeField] int maxStackAttempts = 20;

    [Tooltip("Optional parent for spawned objects, to keep the hierarchy tidy. Empty = scene root.")]
    [SerializeField] Transform spawnParent;

    readonly List<GameObject> spawned = new();
    readonly Collider[] overlapBuffer = new Collider[32];
    int spawnCounter;

    /// <summary>Objects spawned by this spawn point that still exist.</summary>
    public IReadOnlyList<GameObject> Spawned
    {
        get { spawned.RemoveAll(g => g == null); return spawned; }
    }

    void Awake()
    {
        if (!removeChildrenOnStart) return;
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    /// <summary>Spawns a random prefab from the list. Use this from a UI Button's OnClick.</summary>
    public void SpawnItem() => SpawnRandom();

    /// <summary>Spawns a random prefab from the list at this spawn point. Returns null if the list is empty.</summary>
    public GameObject SpawnRandom()
    {
        var candidates = prefabs.FindAll(p => p != null);
        if (candidates.Count == 0)
        {
            Debug.LogWarning($"[SpawnPoint] '{name}' has no prefabs to spawn.", this);
            return null;
        }

        var prefab = candidates[Random.Range(0, candidates.Count)];
        var rotation = transform.rotation;
        if (randomYaw) rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * rotation;

        var instance = Instantiate(prefab, transform.position, rotation, spawnParent);
        instance.name = $"{prefab.name} ({++spawnCounter})";
        if (avoidOverlap) MoveToFreeSpot(instance.gameObject);
        spawned.Add(instance.gameObject);
        return instance.gameObject;
    }

    /// <summary>Removes every object this spawn point has created.</summary>
    public void ClearSpawned()
    {
        foreach (var g in spawned) if (g != null) Destroy(g);
        spawned.Clear();
    }

    /// <summary>Moves the object upwards, one object-height at a time, until its colliders overlap nothing.</summary>
    void MoveToFreeSpot(GameObject go)
    {
        var cols = go.GetComponentsInChildren<Collider>();
        if (cols.Length == 0) return;

        for (int attempt = 0; attempt < maxStackAttempts; attempt++)
        {
            Physics.SyncTransforms(); // make the physics engine see the new position before querying
            var b = cols[0].bounds;
            foreach (var c in cols) b.Encapsulate(c.bounds);

            int n = Physics.OverlapBoxNonAlloc(b.center, b.extents * 0.98f, overlapBuffer,
                                               Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            bool blocked = false;
            for (int i = 0; i < n; i++)
                if (!overlapBuffer[i].transform.IsChildOf(go.transform)) { blocked = true; break; }
            if (!blocked) return;

            go.transform.position += Vector3.up * (b.size.y + stackGap);
        }
        Physics.SyncTransforms();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 0.6f);
    }
}
