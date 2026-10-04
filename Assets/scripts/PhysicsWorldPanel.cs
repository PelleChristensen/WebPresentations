using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows the physics engine's world settings and lets students change some of them at runtime:
///  - physics steps per second (Time.fixedDeltaTime)
///  - gravity strength, plus a zero-gravity toggle
/// It also measures how many physics steps run per rendered frame, to show that the physics
/// simulation runs on its own fixed clock, separate from rendering.
/// Original settings are restored when this object is destroyed (e.g. leaving play mode or the scene).
/// </summary>
public class PhysicsWorldPanel : MonoBehaviour
{
    [System.Serializable]
    public class SliderRow
    {
        public Slider slider;
        public TMP_Text header, minLabel, maxLabel, valueLabel;
    }

    [Header("Info")]
    [SerializeField] TMP_Text infoText;
    [Tooltip("How often (seconds) the info text is refreshed.")]
    [SerializeField] float infoRefreshInterval = 0.25f;

    [Header("Physics steps per second")]
    [SerializeField] SliderRow stepsRow;
    [SerializeField] int minSteps = 10;
    [SerializeField] int maxSteps = 50;

    [Header("Gravity")]
    [SerializeField] SliderRow gravityRow;
    [SerializeField] float maxGravity = 20f;
    [SerializeField] Toggle zeroGravityToggle;

    [Header("Other")]
    [SerializeField] Button resetButton;

    // Defaults captured at start, restored on Reset and OnDestroy.
    float defaultFixedDeltaTime;
    Vector3 defaultGravity;
    Vector3 gravityDirection = Vector3.down;

    // Measurements
    int stepsThisFrame, stepsLastFrame;
    int frameCount, stepCount;
    float windowStart, measuredFps, measuredStepsPerSecond, nextInfoTime;

    void Awake()
    {
        defaultFixedDeltaTime = Time.fixedDeltaTime;
        defaultGravity = Physics.gravity;
        if (defaultGravity.sqrMagnitude > 0f) gravityDirection = defaultGravity.normalized;
    }

    void Start()
    {
        SetupRow(stepsRow, "Physics steps per second", minSteps, maxSteps, true);
        SetupRow(gravityRow, "Gravity strength", 0f, maxGravity, false);

        if (stepsRow.slider)   stepsRow.slider.onValueChanged.AddListener(OnStepsChanged);
        if (gravityRow.slider) gravityRow.slider.onValueChanged.AddListener(OnGravityChanged);
        if (zeroGravityToggle) zeroGravityToggle.onValueChanged.AddListener(OnZeroGravityChanged);
        if (resetButton)       resetButton.onClick.AddListener(ResetToDefaults);

        SyncUIFromEngine();
        windowStart = Time.unscaledTime;
    }

    void OnDestroy()
    {
        // Don't leave changed engine settings behind (they are global, not per scene).
        Time.fixedDeltaTime = defaultFixedDeltaTime;
        Physics.gravity = defaultGravity;
    }

    // ---------------------------------------------------------------- Measuring

    void FixedUpdate() => stepsThisFrame++;

    void Update()
    {
        // FixedUpdate runs 0..n times before each Update, so this is "physics steps in this frame".
        stepsLastFrame = stepsThisFrame;
        stepCount += stepsThisFrame;
        stepsThisFrame = 0;
        frameCount++;

        float elapsed = Time.unscaledTime - windowStart;
        if (elapsed >= 0.5f)
        {
            measuredFps = frameCount / elapsed;
            measuredStepsPerSecond = stepCount / elapsed;
            frameCount = stepCount = 0;
            windowStart = Time.unscaledTime;
        }

        if (Time.unscaledTime >= nextInfoTime)
        {
            nextInfoTime = Time.unscaledTime + infoRefreshInterval;
            RefreshInfo();
        }
    }

    void RefreshInfo()
    {
        if (infoText == null) return;
        var g = Physics.gravity;
        infoText.text =
            $"Physics step: every {Time.fixedDeltaTime * 1000f:0} ms ({1f / Time.fixedDeltaTime:0} per second)\n" +
            $"Measured: {measuredStepsPerSecond:0} physics steps/s at {measuredFps:0} frames/s\n" +
            $"Physics steps last frame: {stepsLastFrame}\n" +
            $"Gravity: ({g.x:0.##}, {g.y:0.##}, {g.z:0.##}) m/s²\n" +
            $"Solver iterations: {Physics.defaultSolverIterations} (velocity: {Physics.defaultSolverVelocityIterations})\n" +
            $"Sleep threshold: {Physics.sleepThreshold:0.###}   Bounce threshold: {Physics.bounceThreshold:0.#} m/s";
    }

    // ---------------------------------------------------------------- Controls

    void OnStepsChanged(float value)
    {
        int steps = Mathf.Clamp(Mathf.RoundToInt(value), minSteps, maxSteps);
        Time.fixedDeltaTime = 1f / steps;
        if (stepsRow.valueLabel) stepsRow.valueLabel.text = $"{steps} /s";
        RefreshInfo();
    }

    void OnGravityChanged(float value)
    {
        value = Mathf.Round(value * 10f) / 10f; // 0.1 m/s² steps
        gravityRow.slider.SetValueWithoutNotify(value);
        if (gravityRow.valueLabel) gravityRow.valueLabel.text = $"{value:0.0} m/s²";
        ApplyGravity();
    }

    void OnZeroGravityChanged(bool zero)
    {
        if (gravityRow.slider) gravityRow.slider.interactable = !zero;
        ApplyGravity();
    }

    void ApplyGravity()
    {
        bool zero = zeroGravityToggle != null && zeroGravityToggle.isOn;
        float strength = gravityRow.slider ? gravityRow.slider.value : defaultGravity.magnitude;
        Physics.gravity = zero ? Vector3.zero : gravityDirection * strength;
        WakeAllBodies(); // sleeping bodies ignore gravity changes until something wakes them
        RefreshInfo();
    }

    /// <summary>Restore the settings the scene started with.</summary>
    public void ResetToDefaults()
    {
        Time.fixedDeltaTime = defaultFixedDeltaTime;
        Physics.gravity = defaultGravity;
        WakeAllBodies();
        SyncUIFromEngine();
    }

    // ---------------------------------------------------------------- Helpers

    void SyncUIFromEngine()
    {
        int steps = Mathf.Clamp(Mathf.RoundToInt(1f / Time.fixedDeltaTime), minSteps, maxSteps);
        if (stepsRow.slider) stepsRow.slider.SetValueWithoutNotify(steps);
        if (stepsRow.valueLabel) stepsRow.valueLabel.text = $"{steps} /s";

        float g = Mathf.Round(Physics.gravity.magnitude * 10f) / 10f;
        if (zeroGravityToggle) zeroGravityToggle.SetIsOnWithoutNotify(g == 0f && defaultGravity != Vector3.zero);
        if (g == 0f) g = defaultGravity.magnitude;
        if (gravityRow.slider)
        {
            gravityRow.slider.SetValueWithoutNotify(g);
            gravityRow.slider.interactable = zeroGravityToggle == null || !zeroGravityToggle.isOn;
        }
        if (gravityRow.valueLabel) gravityRow.valueLabel.text = $"{g:0.0} m/s²";
        RefreshInfo();
    }

    static void SetupRow(SliderRow row, string header, float min, float max, bool whole)
    {
        if (row == null || row.slider == null) return;
        row.slider.minValue = min;
        row.slider.maxValue = max;
        row.slider.wholeNumbers = whole;
        if (row.header)   row.header.text = header;
        if (row.minLabel) row.minLabel.text = min.ToString("0");
        if (row.maxLabel) row.maxLabel.text = max.ToString("0");
    }

    static void WakeAllBodies()
    {
        foreach (var rb in Object.FindObjectsByType<Rigidbody>())
            if (!rb.isKinematic) rb.WakeUp();
    }
}
