using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Teaching aid: marks every mesh that the in-world camera sends for rendering
/// (i.e. every mesh that survives its CPU frustum culling).
///
/// Unity's frustum culling tests each renderer's axis-aligned bounding box (AABB)
/// against the six planes of the camera frustum, plus the camera's culling mask.
/// This script performs the same test and draws a glow overlay on the result.
///
/// The overlay is drawn with Graphics.RenderMesh restricted to the viewer camera,
/// so the in-world camera (and its RenderTexture on the monitor) is never affected.
/// No layers, materials or renderer settings in the scene are changed.
/// </summary>
public class RenderedMeshHighlighter : MonoBehaviour
{
    [Header("Cameras")]
    [Tooltip("The camera whose culling we visualise (renders to the monitor).")]
    [SerializeField] private Camera inWorldCamera;
    [Tooltip("The camera that shows the highlight (the main camera). Leave empty to use Camera.main.")]
    [SerializeField] private Camera viewerCamera;

    [Header("Look")]
    [SerializeField] private Shader highlightShader;
    [SerializeField] private Color visibleColor = new Color(0.2f, 0.9f, 1f, 1f);
    [SerializeField] private Color occludedColor = new Color(1f, 0.55f, 0.1f, 0.4f);

    [Header("State")]
    [SerializeField] private bool highlightEnabled = true;
    [Tooltip("Also show parts of sent meshes that are hidden behind other geometry (x-ray).")]
    [SerializeField] private bool showOccluded = false;
    [SerializeField] private bool showGui = true;
    [Tooltip("Seconds between scans for renderers that were added/removed at runtime.")]
    [SerializeField] private float rescanInterval = 1f;

    public bool HighlightEnabled { get => highlightEnabled; set => highlightEnabled = value; }
    public bool ShowOccluded { get => showOccluded; set => showOccluded = value; }
    public int SentCount { get; private set; }
    public int TotalCount { get; private set; }

    private class Entry
    {
        public Renderer renderer;
        public MeshFilter filter;
        public SkinnedMeshRenderer skinned;
        public Mesh bakedMesh;
    }

    private readonly Dictionary<Renderer, Entry> entries = new Dictionary<Renderer, Entry>();
    private readonly List<Renderer> toRemove = new List<Renderer>();
    private readonly Plane[] planes = new Plane[6];
    private Material visibleMaterial, occludedMaterial;
    private float nextScanTime;
    private GUIStyle headerStyle;

    private void OnEnable()
    {
        if (highlightShader == null) highlightShader = Shader.Find("GameCraft/RenderedMeshHighlight");
        if (highlightShader == null)
        {
            Debug.LogError("RenderedMeshHighlighter: highlight shader missing.", this);
            enabled = false;
            return;
        }

        visibleMaterial = new Material(highlightShader) { name = "Highlight (visible)" };
        visibleMaterial.SetFloat("_ZTest", (float)CompareFunction.LessEqual);

        occludedMaterial = new Material(highlightShader) { name = "Highlight (occluded)" };
        occludedMaterial.SetFloat("_ZTest", (float)CompareFunction.Greater);
        occludedMaterial.SetFloat("_FillAlpha", 0.08f);
        occludedMaterial.SetFloat("_RimIntensity", 0.6f);
        occludedMaterial.renderQueue = visibleMaterial.renderQueue - 1;

        ApplyColors();
        Rescan();
    }

    private void OnDisable()
    {
        foreach (var e in entries.Values)
            if (e.bakedMesh != null) Destroy(e.bakedMesh);
        entries.Clear();
        if (visibleMaterial != null) Destroy(visibleMaterial);
        if (occludedMaterial != null) Destroy(occludedMaterial);
    }

    private void OnValidate()
    {
        if (visibleMaterial != null) ApplyColors();
    }

    private void ApplyColors()
    {
        visibleMaterial.SetColor("_Color", visibleColor);
        occludedMaterial.SetColor("_Color", occludedColor);
    }

    private void Rescan()
    {
        nextScanTime = Time.unscaledTime + rescanInterval;

        toRemove.Clear();
        foreach (var r in entries.Keys)
            if (r == null) toRemove.Add(r);
        foreach (var r in toRemove)
        {
            if (entries[r].bakedMesh != null) Destroy(entries[r].bakedMesh);
            entries.Remove(r);
        }

        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (entries.ContainsKey(r)) continue;

            if (r is SkinnedMeshRenderer smr)
            {
                entries.Add(r, new Entry { renderer = r, skinned = smr });
            }
            else if (r is MeshRenderer)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null) entries.Add(r, new Entry { renderer = r, filter = mf });
            }
            // Particles, lines, UI etc. are ignored - we only visualise meshes.
        }
        TotalCount = entries.Count;
    }

    /// <summary>
    /// Same test Unity does on the CPU before sending a renderer to the GPU:
    /// active + enabled + layer in the culling mask + AABB intersects the frustum.
    /// </summary>
    private bool IsSentToInWorldCamera(Renderer r)
    {
        if (!r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff) return false;
        if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) return false;
        if ((inWorldCamera.cullingMask & (1 << r.gameObject.layer)) == 0) return false;
        return GeometryUtility.TestPlanesAABB(planes, r.bounds);
    }

    private void LateUpdate()
    {
        SentCount = 0;
        if (Time.unscaledTime >= nextScanTime) Rescan();

        if (viewerCamera == null) viewerCamera = Camera.main;
        if (!highlightEnabled || inWorldCamera == null || viewerCamera == null) return;
        if (!inWorldCamera.isActiveAndEnabled) return;

        GeometryUtility.CalculateFrustumPlanes(inWorldCamera, planes);

        foreach (var e in entries.Values)
        {
            if (e.renderer == null || !IsSentToInWorldCamera(e.renderer)) continue;
            SentCount++;
            Draw(e);
        }
    }

    private void Draw(Entry e)
    {
        Mesh mesh;
        Matrix4x4 matrix;
        int subMeshStart = 0;
        int subMeshCount;

        if (e.skinned != null)
        {
            if (e.bakedMesh == null) e.bakedMesh = new Mesh { name = e.skinned.name + " (highlight bake)" };
            e.skinned.BakeMesh(e.bakedMesh, true);
            mesh = e.bakedMesh;
            var t = e.skinned.transform;
            matrix = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
            subMeshCount = mesh.subMeshCount;
        }
        else
        {
            mesh = e.filter.sharedMesh;
            if (mesh == null) return;

            var mr = (MeshRenderer)e.renderer;
            if (mr.isPartOfStaticBatch)
            {
                // Static batching pre-transforms vertices into one combined mesh.
                matrix = Matrix4x4.identity;
                subMeshStart = mr.subMeshStartIndex;
                subMeshCount = Mathf.Min(mr.sharedMaterials.Length, mesh.subMeshCount - subMeshStart);
            }
            else
            {
                matrix = e.renderer.localToWorldMatrix;
                subMeshCount = mesh.subMeshCount;
            }
        }

        int layer = e.renderer.gameObject.layer;
        for (int i = 0; i < subMeshCount; i++)
        {
            int sub = subMeshStart + i;
            Graphics.RenderMesh(MakeParams(visibleMaterial, layer), mesh, sub, matrix);
            if (showOccluded)
                Graphics.RenderMesh(MakeParams(occludedMaterial, layer), mesh, sub, matrix);
        }
    }

    private RenderParams MakeParams(Material mat, int layer)
    {
        return new RenderParams(mat)
        {
            camera = viewerCamera,              // <- only the main camera draws this
            layer = layer,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            lightProbeUsage = LightProbeUsage.Off,
            reflectionProbeUsage = ReflectionProbeUsage.Off,
            motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
        };
    }

    private void OnGUI()
    {
        if (!showGui) return;

        float scale = Mathf.Max(1f, Screen.height / 1080f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        headerStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };

        GUILayout.BeginArea(new Rect(10, 10, 330, 110), GUI.skin.box);
        GUILayout.Label("Render visualisation", headerStyle);
        highlightEnabled = GUILayout.Toggle(highlightEnabled, " Highlight meshes sent to the InWorldCamera");
        GUI.enabled = highlightEnabled;
        showOccluded = GUILayout.Toggle(showOccluded, " Also show hidden (occluded) parts");
        GUILayout.Label(highlightEnabled
            ? $"Meshes sent for rendering: {SentCount} / {TotalCount}"
            : $"Meshes in scene: {TotalCount}");
        GUI.enabled = true;
        GUILayout.EndArea();
    }
}
