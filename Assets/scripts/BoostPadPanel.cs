using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI for editing BoostPad properties at runtime: strength (slider) and ForceMode (radio-style toggle list).
/// Applies to the BoostPads in 'targets', or to every BoostPad in the scene if the list is empty.
/// A short explanation of the selected force mode is shown, since the four modes behave very differently.
/// </summary>
public class BoostPadPanel : MonoBehaviour
{
    [System.Serializable]
    public class ModeToggle
    {
        public ForceMode mode;
        public Toggle toggle;
    }

    [Tooltip("BoostPads to control. Leave empty to control all BoostPads in the scene.")]
    [SerializeField] List<BoostPad> targets = new();

    [Header("Strength")]
    [SerializeField] Slider strengthSlider;
    [SerializeField] TMP_Text strengthHeader, strengthMin, strengthMax, strengthValue;
    [SerializeField] float minStrength = 0f;
    [SerializeField] float maxStrength = 30f;
    [SerializeField] float defaultStrength = 15f;

    [Header("Force mode")]
    [SerializeField] List<ModeToggle> modeToggles = new();
    [SerializeField] ForceMode defaultMode = ForceMode.Force;
    [SerializeField] TMP_Text modeDescription;

    float strength;
    ForceMode mode;

    void Start()
    {
        if (targets.Count == 0) targets.AddRange(Object.FindObjectsByType<BoostPad>());

        if (strengthSlider)
        {
            strengthSlider.minValue = minStrength;
            strengthSlider.maxValue = maxStrength;
            strengthSlider.wholeNumbers = false;
            strengthSlider.onValueChanged.AddListener(SetStrength);
        }
        if (strengthHeader) strengthHeader.text = "Boost strength";
        if (strengthMin) strengthMin.text = minStrength.ToString("0");
        if (strengthMax) strengthMax.text = maxStrength.ToString("0");

        foreach (var mt in modeToggles)
        {
            if (mt.toggle == null) continue;
            var m = mt.mode; // capture for the lambda
            mt.toggle.onValueChanged.AddListener(isOn => { if (isOn) SetMode(m); });
        }

        ResetToDefaults();
    }

    /// <summary>Strength 15, ForceMode.Force (or whatever the defaults are set to).</summary>
    public void ResetToDefaults()
    {
        SetStrength(defaultStrength);
        SetMode(defaultMode);
    }

    public void SetStrength(float value)
    {
        strength = Mathf.Round(Mathf.Clamp(value, minStrength, maxStrength) * 2f) / 2f; // 0.5 steps
        if (strengthSlider) strengthSlider.SetValueWithoutNotify(strength);
        foreach (var bp in targets) if (bp) bp.Strength = strength;
        RefreshLabels();
    }

    public void SetMode(ForceMode newMode)
    {
        mode = newMode;
        foreach (var mt in modeToggles)
            if (mt.toggle) mt.toggle.SetIsOnWithoutNotify(mt.mode == mode);
        foreach (var bp in targets) if (bp) bp.Mode = mode;
        RefreshLabels();
    }

    void RefreshLabels()
    {
        if (strengthValue) strengthValue.text = $"{strength:0.#} {Unit(mode)}";
        if (modeDescription) modeDescription.text = Describe(mode);
    }

    static string Unit(ForceMode m) => m switch
    {
        ForceMode.Force          => "N",
        ForceMode.Acceleration   => "m/s²",
        ForceMode.Impulse        => "N·s",
        ForceMode.VelocityChange => "m/s",
        _ => ""
    };

    static string Describe(ForceMode m) => m switch
    {
        ForceMode.Force =>
            "<b>Force</b> - a continuous push in newtons. Heavier objects speed up less (a = F / m). " +
            "Gives the same result at any physics step rate.",
        ForceMode.Acceleration =>
            "<b>Acceleration</b> - a continuous push that ignores mass. Crates and rocks speed up exactly the same.",
        ForceMode.Impulse =>
            "<b>Impulse</b> - an instant kick, here given on <i>every</i> physics step. Heavier objects are kicked less. " +
            "Much stronger than Force, and it depends on the physics step rate!",
        ForceMode.VelocityChange =>
            "<b>Velocity Change</b> - adds speed directly on <i>every</i> physics step, ignoring mass. " +
            "Very strong, and it depends on the physics step rate!",
        _ => ""
    };
}
