using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Draws a runtime wireframe of every collider on this GameObject (and optionally its children),
/// similar to the green gizmo you see in the Scene view - but visible in Play mode and in builds.
///
/// The line colour tells the student what kind of physics body the collider belongs to:
///   Static    - collider with no Rigidbody (never moves by physics)
///   Kinematic - Rigidbody with isKinematic = true (moved by script/animation, pushes others)
///   Dynamic   - Rigidbody driven by the physics simulation (gravity, forces, collisions)
///   Sleeping  - (optional) a dynamic body the physics engine has put to sleep
///
/// While a collider is in an active collision (touching another collider while at least one of the two
/// bodies is moving) it is drawn in the collision colour. When the contact ends, or both bodies come to
/// rest, it keeps that colour for a short hold time so brief impacts are still visible.
/// Trigger colliders use the same colour while any collider is inside them (OnTriggerEnter -> OnTriggerExit).
///
/// Lines are drawn with a screen-space thick-line shader (constant pixel width) and by default
/// "always on top" - the collider shape is physics data, separate from the rendered mesh.
///
/// Supports Box, Sphere, Capsule and Mesh colliders. Anything else falls back to its bounds.
/// Uses Graphics.RenderMesh, no per-edge GameObjects.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Physics/Collider Visualizer")]
public class ColliderVisualizer : MonoBehaviour
{
    public enum BodyType { Static, Kinematic, Dynamic, Sleeping }
    public enum DisplayMode { InFront, WithDepth, Hidden }

    // ---------------------------------------------------------------- Inspector

    [Header("Visibility")]
    [Tooltip("Turn the wireframe on/off for this object. Can be changed at runtime.")]
    [SerializeField] bool visible = true;

    /// <summary>Global switch for ALL ColliderVisualizers in the scene.</summary>
    public static bool GlobalVisible = true;

    /// <summary>How ALL visualizers draw their lines: in front of everything, with depth (hidden behind
    /// geometry), or not at all. Changing it raises DisplayModeChanged, which every visualizer listens to.</summary>
    public static DisplayMode GlobalDisplayMode
    {
        get => globalDisplayMode;
        set
        {
            if (globalDisplayMode == value) return;
            globalDisplayMode = value;
            DisplayModeChanged?.Invoke(value);
        }
    }
    public static event System.Action<DisplayMode> DisplayModeChanged;
    static DisplayMode globalDisplayMode = DisplayMode.InFront;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { globalDisplayMode = DisplayMode.InFront; GlobalVisible = true; }

    [Tooltip("Follow the global display mode (In front / With depth / Hidden), e.g. set from the UI. " +
             "Turn off to keep this object's own Always On Top setting.")]
    [SerializeField] bool followGlobalDisplayMode = true;
    bool hiddenByDisplayMode;

    [Tooltip("Also visualise colliders on child objects.")]
    [SerializeField] bool includeChildren = true;

    [Tooltip("Draw lines on top of all geometry. Shows that the collider is physics data, " +
             "independent of the rendered mesh.")]
    [SerializeField] bool alwaysOnTop = true;

    [Tooltip("Line width in screen pixels.")]
    [Range(1f, 12f)]
    [SerializeField] float lineThickness = 3f;

#if ENABLE_INPUT_SYSTEM
    [Tooltip("Key that toggles ALL visualizers on/off. Set to None to disable.")]
    [SerializeField] Key globalToggleKey = Key.C;
#else
    [Tooltip("Key that toggles ALL visualizers on/off. Set to None to disable.")]
    [SerializeField] KeyCode globalToggleKey = KeyCode.C;
#endif

    [Header("Colours per body type")]
    [SerializeField] Color staticColor    = new Color(0.55f, 0.65f, 0.80f, 1f); // grey-blue
    [SerializeField] Color kinematicColor = new Color(1.00f, 0.60f, 0.10f, 1f); // orange
    [SerializeField] Color dynamicColor   = new Color(0.57f, 0.96f, 0.53f, 1f); // Unity gizmo green
    [Tooltip("Use a separate colour when a dynamic Rigidbody is sleeping (at rest).")]
    [SerializeField] bool showSleeping = false;
    [SerializeField] Color sleepingColor  = new Color(0.30f, 0.50f, 0.30f, 1f); // dim green

    [Header("Collision highlight")]
    [Tooltip("Draw colliders in the collision colour while they are in an active collision.")]
    [SerializeField] bool highlightCollisions = true;
    [Tooltip("Draw trigger colliders in the collision colour while something is inside them.")]
    [SerializeField] bool highlightTriggers = true;
    [SerializeField] Color collisionColor = new Color(1f, 0.15f, 0.15f, 1f); // red
    [Tooltip("How long (real-time seconds) the collision colour is kept after the collision ends or comes to rest.")]
    [Min(0f)]
    [SerializeField] float collisionHoldTime = 0.1f;
    [Tooltip("A contact counts as 'at rest' (no longer red) when both bodies move slower than this (m/s) " +
             "or are sleeping.")]
    [Min(0f)]
    [SerializeField] float restSpeed = 0.05f;
    [Tooltip("Angular speed (rad/s) below which a body counts as not rotating.")]
    [Min(0f)]
    [SerializeField] float restAngularSpeed = 0.1f;

    [Header("Advanced")]
    [Tooltip("Optional custom line material. Must use the 'Hidden/ColliderVisualizer/ThickLine' shader " +
             "(or one with the same vertex layout). Leave empty to use the built-in one.")]
    [SerializeField] Material lineMaterialOverride;
    [Range(8, 128)]
    [SerializeField] int circleSegments = 48;

    // ---------------------------------------------------------------- Public API

    /// <summary>Show/hide this object's wireframe.</summary>
    public bool Visible { get => visible; set => visible = value; }

    public bool AlwaysOnTop { get => alwaysOnTop; set => alwaysOnTop = value; }
    public float LineThickness { get => lineThickness; set => lineThickness = Mathf.Max(1f, value); }

    public void Toggle() => visible = !visible;
    public static void ToggleAll() => GlobalVisible = !GlobalVisible;

    /// <summary>Body type of the first collider (handy for UI labels later).</summary>
    public BodyType CurrentBodyType =>
        entries.Count > 0 ? GetBodyType(entries[0].collider) : BodyType.Static;

    public Color GetColor(BodyType type) => type switch
    {
        BodyType.Kinematic => kinematicColor,
        BodyType.Dynamic   => dynamicColor,
        BodyType.Sleeping  => sleepingColor,
        _                  => staticColor,
    };

    /// <summary>
    /// True while the collider is in an active (moving) collision, or within the hold time after that ended.
    /// Call at most once per frame per collider - it also tracks when the collision ends.
    /// </summary>
    public bool IsHighlighted(Collider c)
    {
        if (c == null) return false;

        bool active = false;
        if (c.isTrigger)
        {
            // Trigger: active while anything is inside, moving or not.
            if (highlightTriggers && triggerOccupants.TryGetValue(c, out var inside))
            {
                inside.RemoveWhere(IsGone);
                active = inside.Count > 0;
            }
        }
        else if (highlightCollisions && touching.TryGetValue(c, out var others))
        {
            // Safety net: drop partners that were destroyed/disabled without an Exit message.
            others.RemoveWhere(IsGone);
            foreach (var o in others)
                if (!IsAtRest(c.attachedRigidbody) || !IsAtRest(o.attachedRigidbody)) { active = true; break; }
        }

        float now = Time.unscaledTime;
        wasActive.TryGetValue(c, out bool was);
        if (was && !active) releaseTime[c] = now;   // collision just ended or came to rest -> start hold
        wasActive[c] = active;

        if (active) return true;
        return releaseTime.TryGetValue(c, out var t) && now - t <= collisionHoldTime;
    }

    /// <summary>Call this if you add/remove colliders at runtime.</summary>
    [ContextMenu("Refresh Colliders")]
    public void RefreshColliders()
    {
        ReleaseEntries();
        var found = includeChildren ? GetComponentsInChildren<Collider>(true) : GetComponents<Collider>();
        foreach (var c in found)
        {
            entries.Add(new Entry { collider = c });
            ownColliders.Add(c);

            // Unity sends collision messages to the collider's own GameObject and to its Rigidbody's
            // GameObject. If neither is this object (e.g. a static child), forward them with a relay.
            if (Application.isPlaying && c.gameObject != gameObject &&
                (c.attachedRigidbody == null || c.attachedRigidbody.gameObject != gameObject) &&
                !relays.Exists(r => r.gameObject == c.gameObject))
            {
                var relay = c.gameObject.AddComponent<ColliderVisualizerRelay>();
                relay.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
                relay.target = this;
                relays.Add(relay);
            }
        }

        if (entries.Count == 0)
            Debug.LogWarning($"[ColliderVisualizer] No Collider found on '{name}'" +
                             (includeChildren ? " or its children." : "."), this);
    }

    // ---------------------------------------------------------------- Internals

    class Entry
    {
        public Collider collider;
        public Mesh mesh;          // per-entry mesh (capsule / mesh collider); null = shared mesh
        public Vector4 cacheKey;   // detects when a capsule needs rebuilding
        public Mesh cachedSource;  // detects when a MeshCollider's mesh changes
    }

    readonly List<Entry> entries = new();
    readonly Dictionary<BodyType, MaterialPropertyBlock> blocks = new();
    MaterialPropertyBlock collisionBlock;

    // Collision tracking: our collider -> the colliders it currently touches,
    // whether that was an active collision last frame, and when the last active collision ended.
    readonly HashSet<Collider> ownColliders = new();
    readonly Dictionary<Collider, HashSet<Collider>> touching = new();
    readonly Dictionary<Collider, HashSet<Collider>> triggerOccupants = new(); // our trigger -> colliders inside it
    readonly Dictionary<Collider, bool> wasActive = new();
    readonly Dictionary<Collider, float> releaseTime = new();
    readonly List<ColliderVisualizerRelay> relays = new();
    static readonly System.Predicate<Collider> IsGone =
        o => o == null || !o.enabled || !o.gameObject.activeInHierarchy;

    static Mesh unitCube, unitSphere;
    static Material sharedMat, sharedMatOnTop;
    static readonly int ColorId     = Shader.PropertyToID("_Color");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
    static readonly int ZTestId     = Shader.PropertyToID("_ZTest");
    const string LineShaderName = "Hidden/ColliderVisualizer/ThickLine";

    void Reset()
    {
        // Friendly warning when the component is added in the editor.
        if (GetComponentInChildren<Collider>(true) == null)
            Debug.LogWarning("[ColliderVisualizer] This object has no Collider to visualise. " +
                             "Add a BoxCollider, SphereCollider, CapsuleCollider or MeshCollider.", this);
    }

    void OnEnable()
    {
        DisplayModeChanged += ApplyDisplayMode;
        ApplyDisplayMode(globalDisplayMode);
        RefreshColliders();
    }

    void OnDisable()
    {
        DisplayModeChanged -= ApplyDisplayMode;
        ReleaseEntries();
    }

    /// <summary>Called when the global display mode changes (and on enable).</summary>
    void ApplyDisplayMode(DisplayMode mode)
    {
        if (!followGlobalDisplayMode) return;
        hiddenByDisplayMode = mode == DisplayMode.Hidden;
        if (mode != DisplayMode.Hidden) alwaysOnTop = mode == DisplayMode.InFront;
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (globalToggleKey != Key.None && Keyboard.current != null && Keyboard.current[globalToggleKey].wasPressedThisFrame)
            ToggleAllOncePerFrame();
#else
        if (globalToggleKey != KeyCode.None && Input.GetKeyDown(globalToggleKey))
            ToggleAllOncePerFrame();
#endif
    }

    // Many visualizers share the same key - make sure the toggle only happens once per frame.
    static int lastToggleFrame = -1;
    static void ToggleAllOncePerFrame()
    {
        if (lastToggleFrame == Time.frameCount) return;
        lastToggleFrame = Time.frameCount;
        ToggleAll();
    }

    // LateUpdate = after physics + animation have moved the object this frame.
    void LateUpdate()
    {
        if (!visible || !GlobalVisible || hiddenByDisplayMode) return;

        var mat = GetMaterial();
        if (mat == null) return;

        foreach (var e in entries)
        {
            var c = e.collider;
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;

            if (!TryGetMeshAndMatrix(e, out var mesh, out var matrix)) continue;

            var rp = new RenderParams(mat)
            {
                matProps = IsHighlighted(c) ? GetCollisionBlock() : GetBlock(GetBodyType(c)),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = c.gameObject.layer,
            };
            Graphics.RenderMesh(rp, mesh, 0, matrix);
        }
    }

    void OnDestroy() => ReleaseEntries();

    BodyType GetBodyType(Collider c)
    {
        var rb = c.attachedRigidbody;
        if (rb == null) return BodyType.Static;
        if (rb.isKinematic) return BodyType.Kinematic;
        if (showSleeping && rb.IsSleeping()) return BodyType.Sleeping;
        return BodyType.Dynamic;
    }

    /// <summary>Static colliders, sleeping bodies and (nearly) motionless bodies count as at rest.</summary>
    bool IsAtRest(Rigidbody rb)
    {
        if (rb == null || rb.IsSleeping()) return true;
        if (rb.isKinematic) return false; // can't read a reliable velocity - treat a kinematic mover as moving
        return rb.linearVelocity.sqrMagnitude <= restSpeed * restSpeed &&
               rb.angularVelocity.sqrMagnitude <= restAngularSpeed * restAngularSpeed;
    }

    MaterialPropertyBlock GetBlock(BodyType type)
    {
        if (!blocks.TryGetValue(type, out var b))
        {
            b = new MaterialPropertyBlock();
            blocks[type] = b;
        }
        ApplyProps(b, GetColor(type)); // re-applied every frame so Inspector edits are live
        return b;
    }

    MaterialPropertyBlock GetCollisionBlock()
    {
        collisionBlock ??= new MaterialPropertyBlock();
        ApplyProps(collisionBlock, collisionColor);
        return collisionBlock;
    }

    void ApplyProps(MaterialPropertyBlock b, Color col)
    {
        b.SetColor(ColorId, col);
        b.SetColor(BaseColorId, col);
        b.SetFloat(ThicknessId, lineThickness);
    }

    // ---------------------------------------------------------------- Collisions

    void OnCollisionEnter(Collision collision) => HandleContacts(collision);
    void OnCollisionStay(Collision collision)  => HandleContacts(collision);
    void OnCollisionExit(Collision collision)  => HandleExit(collision);

    /// <summary>Registers which of our colliders touch collision.collider. Also called by relays.</summary>
    internal void HandleContacts(Collision collision)
    {
        var other = collision.collider;
        for (int i = 0; i < collision.contactCount; i++)
        {
            var mine = collision.GetContact(i).thisCollider;
            if (mine == null || !ownColliders.Contains(mine)) continue;

            if (!touching.TryGetValue(mine, out var set))
                touching[mine] = set = new HashSet<Collider>();
            set.Add(other);
        }
    }

    /// <summary>Exit has no contact points, so remove the other collider from every set it is in.
    /// The hold timer is started by IsHighlighted when it sees the collision is no longer active.</summary>
    internal void HandleExit(Collision collision)
    {
        var other = collision.collider;
        foreach (var kv in touching)
            kv.Value.Remove(other);
    }

    void OnTriggerEnter(Collider other) => HandleTriggerEnter(gameObject, other);
    void OnTriggerStay(Collider other)  => HandleTriggerEnter(gameObject, other);
    void OnTriggerExit(Collider other)  => HandleTriggerExit(other);

    /// <summary>
    /// Trigger messages don't say which of our colliders was entered, only the GameObject that received
    /// the message. Pick our trigger colliders on that object (or on its Rigidbody's object); if there are
    /// several, keep the ones whose bounds overlap the other collider. Also called by relays.
    /// Our non-trigger colliders entering someone else's trigger are ignored.
    /// </summary>
    internal void HandleTriggerEnter(GameObject receiver, Collider other)
    {
        if (other == null) return;
        int matches = 0;
        foreach (var mine in ownColliders)
            if (IsTriggerOn(mine, receiver)) matches++;

        foreach (var mine in ownColliders)
        {
            if (!IsTriggerOn(mine, receiver)) continue;
            if (matches > 1 && !mine.bounds.Intersects(other.bounds)) continue;
            if (!triggerOccupants.TryGetValue(mine, out var set))
                triggerOccupants[mine] = set = new HashSet<Collider>();
            set.Add(other);
        }
    }

    internal void HandleTriggerExit(Collider other)
    {
        foreach (var kv in triggerOccupants)
            kv.Value.Remove(other);
    }

    static bool IsTriggerOn(Collider c, GameObject receiver) =>
        c != null && c.isTrigger &&
        (c.gameObject == receiver || (c.attachedRigidbody != null && c.attachedRigidbody.gameObject == receiver));

    // ---------------------------------------------------------------- Shapes

    bool TryGetMeshAndMatrix(Entry e, out Mesh mesh, out Matrix4x4 matrix)
    {
        var t = e.collider.transform;
        Vector3 s = Abs(t.lossyScale);

        switch (e.collider)
        {
            case BoxCollider box:
                if (unitCube == null) unitCube = BuildUnitCube();
                mesh = unitCube;
                // Box follows the full transform (incl. non-uniform scale) exactly like Unity's.
                matrix = t.localToWorldMatrix * Matrix4x4.TRS(box.center, Quaternion.identity, box.size);
                return true;

            case SphereCollider sphere:
            {
                if (unitSphere == null) unitSphere = BuildUnitSphere(circleSegments);
                mesh = unitSphere;
                // Unity scales sphere colliders uniformly by the largest axis.
                float r = sphere.radius * Mathf.Max(s.x, s.y, s.z);
                matrix = Matrix4x4.TRS(t.TransformPoint(sphere.center), t.rotation, Vector3.one * r);
                return true;
            }

            case CapsuleCollider cap:
            {
                // Unity: height scales with the axis scale, radius with the largest of the other two.
                float axisScale, radScale;
                Quaternion axisRot;
                switch (cap.direction)
                {
                    case 0:  axisScale = s.x; radScale = Mathf.Max(s.y, s.z); axisRot = Quaternion.Euler(0, 0, -90); break;
                    case 2:  axisScale = s.z; radScale = Mathf.Max(s.x, s.y); axisRot = Quaternion.Euler(90, 0, 0);  break;
                    default: axisScale = s.y; radScale = Mathf.Max(s.x, s.z); axisRot = Quaternion.identity;         break;
                }
                float r = cap.radius * radScale;
                float h = Mathf.Max(cap.height * axisScale, 2f * r);

                var key = new Vector4(r, h, circleSegments, 0);
                if (e.mesh == null || e.cacheKey != key)
                {
                    DestroyMesh(e.mesh);
                    e.mesh = BuildCapsule(r, h, circleSegments);
                    e.cacheKey = key;
                }
                mesh = e.mesh;
                matrix = Matrix4x4.TRS(t.TransformPoint(cap.center), t.rotation * axisRot, Vector3.one);
                return true;
            }

            case MeshCollider mc:
            {
                var src = mc.sharedMesh;
                if (src == null) { mesh = null; matrix = default; return false; }
                if (e.mesh == null || e.cachedSource != src)
                {
                    DestroyMesh(e.mesh);
                    e.mesh = BuildWireFromMesh(src);
                    e.cachedSource = src;
                }
                mesh = e.mesh;
                matrix = t.localToWorldMatrix;
                return true;
            }

            default:
            {
                // Terrain, Wheel, etc. - draw the world-space bounds as a fallback.
                var b = e.collider.bounds;
                if (unitCube == null) unitCube = BuildUnitCube();
                mesh = unitCube;
                matrix = Matrix4x4.TRS(b.center, Quaternion.identity, b.size);
                return true;
            }
        }
    }

    static void AddBoxLines(List<Vector3> v, List<int> idx, Vector3 center, Vector3 size)
    {
        int start = v.Count;
        for (int i = 0; i < 8; i++)
            v.Add(center + Vector3.Scale(size, new Vector3((i & 1) == 0 ? -0.5f : 0.5f,
                                                           (i & 2) == 0 ? -0.5f : 0.5f,
                                                           (i & 4) == 0 ? -0.5f : 0.5f)));
        int[] e =
        {
            0,1, 2,3, 4,5, 6,7,   // edges along X
            0,2, 1,3, 4,6, 5,7,   // edges along Y
            0,4, 1,5, 2,6, 3,7,   // edges along Z
        };
        foreach (var i in e) idx.Add(start + i);
    }

    static Mesh BuildUnitCube()
    {
        var v = new List<Vector3>(); var idx = new List<int>();
        AddBoxLines(v, idx, Vector3.zero, Vector3.one);
        return MakeLineMesh("CV_UnitCube", v, idx);
    }

    static Mesh BuildUnitSphere(int seg)
    {
        var v = new List<Vector3>(); var idx = new List<int>();
        AddCircle(v, idx, Vector3.zero, Vector3.right, Vector3.forward, 1f, seg); // XZ (horizontal)
        AddCircle(v, idx, Vector3.zero, Vector3.right, Vector3.up,      1f, seg); // XY
        AddCircle(v, idx, Vector3.zero, Vector3.forward, Vector3.up,    1f, seg); // ZY
        return MakeLineMesh("CV_UnitSphere", v, idx);
    }

    static Mesh BuildCapsule(float r, float h, int seg)
    {
        var v = new List<Vector3>(); var idx = new List<int>();
        float half = h * 0.5f - r;               // distance from centre to each hemisphere centre
        var top = Vector3.up * half;
        var bot = Vector3.down * half;

        AddCircle(v, idx, top, Vector3.right, Vector3.forward, r, seg);
        AddCircle(v, idx, bot, Vector3.right, Vector3.forward, r, seg);

        // 4 straight side lines
        foreach (var d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
        {
            idx.Add(v.Count); v.Add(top + d * r);
            idx.Add(v.Count); v.Add(bot + d * r);
        }

        // Hemisphere arcs (half circles) in the XY and ZY planes
        AddArc(v, idx, top, Vector3.right,   Vector3.up,   r, 0f, Mathf.PI, seg / 2);
        AddArc(v, idx, top, Vector3.forward, Vector3.up,   r, 0f, Mathf.PI, seg / 2);
        AddArc(v, idx, bot, Vector3.right,   Vector3.down, r, 0f, Mathf.PI, seg / 2);
        AddArc(v, idx, bot, Vector3.forward, Vector3.down, r, 0f, Mathf.PI, seg / 2);

        return MakeLineMesh("CV_Capsule", v, idx);
    }

    static Mesh BuildWireFromMesh(Mesh src)
    {
        if (!src.isReadable)
        {
            Debug.LogWarning($"[ColliderVisualizer] Mesh '{src.name}' is not Read/Write enabled - " +
                             "showing its bounds instead. Enable Read/Write in the model import settings.");
            var bv = new List<Vector3>(); var bi = new List<int>();
            AddBoxLines(bv, bi, src.bounds.center, src.bounds.size);
            return MakeLineMesh("CV_Bounds_" + src.name, bv, bi);
        }

        var srcVerts = src.vertices;
        var tris = src.triangles;
        var edges = new HashSet<long>();
        var idx = new List<int>(tris.Length * 2);

        for (int i = 0; i < tris.Length; i += 3)
        {
            AddEdge(tris[i],     tris[i + 1]);
            AddEdge(tris[i + 1], tris[i + 2]);
            AddEdge(tris[i + 2], tris[i]);
        }

        void AddEdge(int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (edges.Add(key)) { idx.Add(a); idx.Add(b); }
        }

        return MakeLineMesh("CV_" + src.name, new List<Vector3>(srcVerts), idx);
    }

    static void AddCircle(List<Vector3> v, List<int> idx, Vector3 c, Vector3 a, Vector3 b, float r, int seg)
        => AddArc(v, idx, c, a, b, r, 0f, Mathf.PI * 2f, seg);

    static void AddArc(List<Vector3> v, List<int> idx, Vector3 c, Vector3 a, Vector3 b,
                       float r, float from, float to, int seg)
    {
        seg = Mathf.Max(seg, 2);
        int start = v.Count;
        for (int i = 0; i <= seg; i++)
        {
            float t = Mathf.Lerp(from, to, i / (float)seg);
            v.Add(c + (a * Mathf.Cos(t) + b * Mathf.Sin(t)) * r);
        }
        for (int i = 0; i < seg; i++) { idx.Add(start + i); idx.Add(start + i + 1); }
    }

    /// <summary>
    /// Turns a line list (pairs of indices into v) into a quad mesh for the thick-line shader:
    /// 4 vertices per segment, each storing start point (position), end point (uv1),
    /// end flag (uv0.x) and side (uv0.y). The shader expands the quad in screen space.
    /// </summary>
    static Mesh MakeLineMesh(string name, List<Vector3> v, List<int> lineIdx)
    {
        int segCount = lineIdx.Count / 2;
        var pos  = new List<Vector3>(segCount * 4);
        var uv0  = new List<Vector2>(segCount * 4);
        var uv1  = new List<Vector3>(segCount * 4);
        var tris = new List<int>(segCount * 6);
        var bounds = new Bounds();
        bool first = true;

        for (int s = 0; s < segCount; s++)
        {
            Vector3 a = v[lineIdx[2 * s]], b = v[lineIdx[2 * s + 1]];
            int i0 = pos.Count;
            // start/-1, start/+1, end/-1, end/+1
            pos.Add(a); uv0.Add(new Vector2(0, -1)); uv1.Add(b);
            pos.Add(a); uv0.Add(new Vector2(0,  1)); uv1.Add(b);
            pos.Add(a); uv0.Add(new Vector2(1, -1)); uv1.Add(b);
            pos.Add(a); uv0.Add(new Vector2(1,  1)); uv1.Add(b);
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
            tris.Add(i0 + 2); tris.Add(i0 + 1); tris.Add(i0 + 3);

            if (first) { bounds = new Bounds(a, Vector3.zero); first = false; }
            bounds.Encapsulate(a); bounds.Encapsulate(b);
        }

        var m = new Mesh { name = name, hideFlags = HideFlags.DontSave };
        if (pos.Count > 65535) m.indexFormat = IndexFormat.UInt32;
        m.SetVertices(pos);
        m.SetUVs(0, uv0);
        m.SetUVs(1, uv1);
        m.SetTriangles(tris, 0, false);
        // Positions only hold the start points, so set bounds explicitly (used for culling).
        bounds.Expand(0.01f);
        m.bounds = bounds;
        return m;
    }

    // ---------------------------------------------------------------- Material & cleanup

    Material GetMaterial()
    {
        if (lineMaterialOverride != null) return lineMaterialOverride;

        if (sharedMat == null || sharedMatOnTop == null)
        {
            sharedMat      = CreateLineMaterial(onTop: false);
            sharedMatOnTop = CreateLineMaterial(onTop: true);
        }
        return alwaysOnTop ? sharedMatOnTop : sharedMat;
    }

    static Material CreateLineMaterial(bool onTop)
    {
        var shader = Shader.Find(LineShaderName);
        if (shader == null)
        {
            Debug.LogError($"[ColliderVisualizer] Shader '{LineShaderName}' not found. " +
                           "Make sure ColliderVisualizerLine.shader is in a Resources folder.");
            return null;
        }

        var m = new Material(shader) { name = onTop ? "CV_LineOnTop" : "CV_Line", hideFlags = HideFlags.HideAndDontSave };
        m.SetFloat(ZTestId, (float)(onTop ? CompareFunction.Always : CompareFunction.LessEqual));
        m.renderQueue = (int)RenderQueue.Transparent + (onTop ? 100 : 0);
        return m;
    }

    void ReleaseEntries()
    {
        foreach (var e in entries) DestroyMesh(e.mesh);
        entries.Clear();

        foreach (var r in relays)
            if (r != null) { if (Application.isPlaying) Destroy(r); else DestroyImmediate(r); }
        relays.Clear();
        ownColliders.Clear();
        touching.Clear();
        triggerOccupants.Clear();
        wasActive.Clear();
        releaseTime.Clear();
    }

    static void DestroyMesh(Mesh m)
    {
        if (m == null) return;
        if (Application.isPlaying) Destroy(m); else DestroyImmediate(m);
    }

    static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
}
