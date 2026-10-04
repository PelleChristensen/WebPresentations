using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Shows "Stats window"-style rendering numbers for ONE camera (the in-world camera) at runtime.
///
/// Unity's render counters (triangles, vertices, SetPass calls ...) add up over the whole frame
/// for all cameras. We read them just before and just after the in-world camera renders and
/// take the difference, so the numbers belong to that camera only.
/// </summary>
public class InWorldCameraStats : MonoBehaviour
{
    [SerializeField] private Camera inWorldCamera;
    [SerializeField] private TMP_Text output;
    [Tooltip("Optional. If set, the text is only refreshed while this popup is open.")]
    [SerializeField] private PopupPanel popup;
    [Tooltip("Seconds between text updates (numbers are averaged over this period).")]
    [SerializeField] private float refreshInterval = 0.5f;

    // Counter name in Unity's profiler, and the label shown to students.
    private static readonly (string counter, string label)[] Counters =
    {
        ("Triangles Count",      "Triangles"),
        ("Vertices Count",       "Vertices"),
        ("SetPass Calls Count",  "SetPass calls"),
        ("Shadow Casters Count", "Shadow casters"),
    };

    private ProfilerRecorder[] recorders;
    private long[] startValues;
    private long[] sums;
    private int samples;

    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly Plane[] planes = new Plane[6];
    private float nextRescan, nextRefresh;
    private float frameTimeSum;
    private int frameCount;
    private readonly StringBuilder sb = new StringBuilder(512);

    private void OnEnable()
    {
        recorders = new ProfilerRecorder[Counters.Length];
        startValues = new long[Counters.Length];
        sums = new long[Counters.Length];
        for (int i = 0; i < Counters.Length; i++)
            recorders[i] = ProfilerRecorder.StartNew(ProfilerCategory.Render, Counters[i].counter);

        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        RenderPipelineManager.endCameraRendering += OnEndCamera;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RenderPipelineManager.endCameraRendering -= OnEndCamera;
        if (recorders != null)
            foreach (var r in recorders) r.Dispose();
    }

    private void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != inWorldCamera) return;
        for (int i = 0; i < recorders.Length; i++)
            startValues[i] = recorders[i].Valid ? recorders[i].CurrentValue : 0;
    }

    private void OnEndCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != inWorldCamera) return;
        for (int i = 0; i < recorders.Length; i++)
            if (recorders[i].Valid) sums[i] += recorders[i].CurrentValue - startValues[i];
        samples++;
    }

    private void Update()
    {
        frameTimeSum += Time.unscaledDeltaTime;
        frameCount++;

        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + refreshInterval;

        if (output != null && inWorldCamera != null && (popup == null || popup.IsOpen))
            output.text = BuildText();

        // Start a new averaging period.
        for (int i = 0; i < sums.Length; i++) sums[i] = 0;
        samples = 0;
        frameTimeSum = 0f;
        frameCount = 0;
    }

    private string BuildText()
    {
        sb.Clear();

        float avgFrame = frameCount > 0 ? frameTimeSum / frameCount : 0f;
        sb.Append("<b>Frame</b> (whole application)\n");
        Row("FPS", avgFrame > 0f ? (1f / avgFrame).ToString("0") : "-");
        Row("Frame time", (avgFrame * 1000f).ToString("0.0") + " ms");

        sb.Append("\n<b>In-world camera</b>\n");
        var rt = inWorldCamera.targetTexture;
        Row("Resolution", rt != null ? rt.width + " x " + rt.height : inWorldCamera.pixelWidth + " x " + inWorldCamera.pixelHeight);
        Row("Meshes sent to GPU", CountMeshesInFrustum().ToString("N0"));

        for (int i = 0; i < Counters.Length; i++)
        {
            string value = !recorders[i].Valid ? "n/a"
                         : samples == 0 ? "-"
                         : (sums[i] / samples).ToString("N0");
            Row(Counters[i].label, value);
        }

        sb.Append("\n<size=80%><i>Triangles and vertices include the shadow pass - try turning shadows off.</i></size>");
        return sb.ToString();
    }

    private void Row(string label, string value)
    {
        sb.Append(label).Append("<pos=60%>").Append(value).Append('\n');
    }

    // Same test Unity uses for frustum culling: layer in culling mask + bounding box inside the frustum.
    private int CountMeshesInFrustum()
    {
        if (Time.unscaledTime >= nextRescan)
        {
            nextRescan = Time.unscaledTime + 1f;
            renderers.Clear();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r is MeshRenderer || r is SkinnedMeshRenderer) renderers.Add(r);
        }

        GeometryUtility.CalculateFrustumPlanes(inWorldCamera, planes);
        int count = 0;
        foreach (var r in renderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff) continue;
            if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
            if ((inWorldCamera.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
            if (GeometryUtility.TestPlanesAABB(planes, r.bounds)) count++;
        }
        return count;
    }
}
