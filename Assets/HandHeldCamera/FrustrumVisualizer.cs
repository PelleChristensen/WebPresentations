using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FrustrumVisualizer : MonoBehaviour
{
    [SerializeField] private Camera localcamera;
    [SerializeField] private LineRenderer lineRenderer, linerenderer2;

    [Header("UI (optional) - synced to the camera's values at startup")]
    [SerializeField] private Slider fovSlider;
    [SerializeField] private Slider nearClipSlider;
    [SerializeField] private Slider farClipSlider;
    [SerializeField] private Toggle perspectiveToggle;
    [SerializeField] private Toggle orthographicToggle;

    private readonly List<Vector3> positions = new List<Vector3>(16);

    // The camera itself is the source of truth for perspective/orthographic.
    public bool IsOrtographic => localcamera.orthographic;

    /// <summary>
    /// Kept for the existing UI events. The argument is ignored: the camera's
    /// own 'orthographic' flag is read instead, so the lines always match the camera.
    /// </summary>
    public void SetPerspective(bool isortho)
    {
        UpdateCameraData();
    }

    void Awake()
    {
        // Awake runs before SliderInfo.Start, so the value labels pick up the synced values.
        SyncUiToCamera();
    }

    void Start()
    {
        UpdateCameraData();
    }

    void LateUpdate()
    {
        // Keep the lines attached to the camera if it is moved or rotated.
        if (localcamera.transform.hasChanged)
        {
            localcamera.transform.hasChanged = false;
            UpdateCameraData();
        }
    }

    /// <summary>
    /// Makes the sliders/toggles show the camera's actual settings. If a camera value
    /// is outside a slider's range, the clamped slider value is written back to the camera.
    /// </summary>
    private void SyncUiToCamera()
    {
        if (fovSlider != null)
        {
            fovSlider.SetValueWithoutNotify(localcamera.fieldOfView);
            localcamera.fieldOfView = fovSlider.value;
        }
        if (nearClipSlider != null)
        {
            nearClipSlider.SetValueWithoutNotify(localcamera.nearClipPlane);
            localcamera.nearClipPlane = nearClipSlider.value;
        }
        if (farClipSlider != null)
        {
            farClipSlider.SetValueWithoutNotify(localcamera.farClipPlane);
            localcamera.farClipPlane = farClipSlider.value;
        }
        if (perspectiveToggle != null) perspectiveToggle.SetIsOnWithoutNotify(!localcamera.orthographic);
        if (orthographicToggle != null) orthographicToggle.SetIsOnWithoutNotify(localcamera.orthographic);
    }

    public void UpdateCameraData()
    {
        if (IsOrtographic)
        {
            SetupOrthographicCameraFrustum();
        }
        else
        {
            SetupPerspectiveCameraFrustum();
        }
    }

    private void SetupOrthographicCameraFrustum()
    {
        float halfHeight = localcamera.orthographicSize;
        float halfWidth = halfHeight * localcamera.aspect;

        BuildFrustum(halfWidth, halfHeight, localcamera.nearClipPlane,
                     halfWidth, halfHeight, localcamera.farClipPlane);
    }

    private void SetupPerspectiveCameraFrustum()
    {
        float tanHalfFov = Mathf.Tan(localcamera.fieldOfView * 0.5f * Mathf.Deg2Rad);

        float nearHalfHeight = tanHalfFov * localcamera.nearClipPlane;
        float nearHalfWidth = nearHalfHeight * localcamera.aspect;

        float farHalfHeight = tanHalfFov * localcamera.farClipPlane;
        float farHalfWidth = farHalfHeight * localcamera.aspect;

        BuildFrustum(nearHalfWidth, nearHalfHeight, localcamera.nearClipPlane,
                     farHalfWidth, farHalfHeight, localcamera.farClipPlane);
    }

    /// <summary>
    /// Builds the 8 frustum corners in the camera's own space (right/up/forward),
    /// so the lines follow the camera's rotation, and connects them as one line strip.
    /// </summary>
    private void BuildFrustum(float nearHalfWidth, float nearHalfHeight, float near,
                              float farHalfWidth, float farHalfHeight, float far)
    {
        Vector3 neartopleft     = GetPoint(-nearHalfWidth,  nearHalfHeight, near);
        Vector3 neartopright    = GetPoint( nearHalfWidth,  nearHalfHeight, near);
        Vector3 nearbottomright = GetPoint( nearHalfWidth, -nearHalfHeight, near);
        Vector3 nearbottomleft  = GetPoint(-nearHalfWidth, -nearHalfHeight, near);

        Vector3 fartopleft      = GetPoint(-farHalfWidth,  farHalfHeight, far);
        Vector3 fartopright     = GetPoint( farHalfWidth,  farHalfHeight, far);
        Vector3 farbottomright  = GetPoint( farHalfWidth, -farHalfHeight, far);
        Vector3 farbottomleft   = GetPoint(-farHalfWidth, -farHalfHeight, far);

        positions.Clear();
        positions.Add(neartopleft);
        positions.Add(neartopright);
        positions.Add(nearbottomright);
        positions.Add(nearbottomleft);
        positions.Add(neartopleft);
        positions.Add(fartopleft);
        positions.Add(fartopright);
        positions.Add(farbottomright);
        positions.Add(farbottomleft);
        positions.Add(fartopleft);
        positions.Add(fartopright);
        positions.Add(neartopright);
        positions.Add(nearbottomright);
        positions.Add(farbottomright);
        positions.Add(farbottomleft);
        positions.Add(nearbottomleft);

        UpdateLine(positions);
    }

    private Vector3 GetPoint(float x, float y, float z)
    {
        Transform t = localcamera.transform;
        return t.position + t.right * x + t.up * y + t.forward * z;
    }

    private void UpdateLine(List<Vector3> list)
    {
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = list.Count;
        for (int i = 0; i < list.Count; i++)
            lineRenderer.SetPosition(i, list[i]);
    }
}
