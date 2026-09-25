using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Connects popup UI (two toggles + a status text) to RenderedMeshHighlighter.
/// Replaces the temporary OnGUI panel in RenderedMeshHighlighter.
/// </summary>
public class RenderedMeshHighlighterUI : MonoBehaviour
{
    [SerializeField] private RenderedMeshHighlighter highlighter;
    [SerializeField] private Toggle highlightEnabledToggle;
    [SerializeField] private Toggle showOccludedToggle;
    [SerializeField] private TMP_Text updatedInfo;

    [Tooltip("{0} = meshes sent, {1} = meshes in scene")]
    [SerializeField] private string sentFormat = "Meshes sent for rendering: {0} / {1}";
    [Tooltip("{0} = meshes in scene")]
    [SerializeField] private string offFormat = "Meshes in scene: {0}";

    private int lastSent = -1, lastTotal = -1;
    private bool lastEnabled;

    private void Start()
    {
        if (highlighter == null)
        {
            Debug.LogError("RenderedMeshHighlighterUI: no highlighter assigned.", this);
            enabled = false;
            return;
        }

        // Show the highlighter's current state without firing the toggle events.
        highlightEnabledToggle.SetIsOnWithoutNotify(highlighter.HighlightEnabled);
        showOccludedToggle.SetIsOnWithoutNotify(highlighter.ShowOccluded);

        highlightEnabledToggle.onValueChanged.AddListener(OnHighlightEnabledChanged);
        showOccludedToggle.onValueChanged.AddListener(OnShowOccludedChanged);

        RefreshInteractable();
        RefreshInfo(force: true);
    }

    private void OnDestroy()
    {
        if (highlightEnabledToggle != null) highlightEnabledToggle.onValueChanged.RemoveListener(OnHighlightEnabledChanged);
        if (showOccludedToggle != null) showOccludedToggle.onValueChanged.RemoveListener(OnShowOccludedChanged);
    }

    private void Update()
    {
        RefreshInfo(force: false);
    }

    private void OnHighlightEnabledChanged(bool isOn)
    {
        highlighter.HighlightEnabled = isOn;
        RefreshInteractable();
        RefreshInfo(force: true);
    }

    private void OnShowOccludedChanged(bool isOn)
    {
        highlighter.ShowOccluded = isOn;
    }

    // "Show hidden parts" only makes sense while the highlight is on.
    private void RefreshInteractable()
    {
        showOccludedToggle.interactable = highlighter.HighlightEnabled;
    }

    // Only rebuild the string when a number changed, to avoid garbage every frame.
    private void RefreshInfo(bool force)
    {
        if (updatedInfo == null) return;

        int sent = highlighter.SentCount;
        int total = highlighter.TotalCount;
        bool on = highlighter.HighlightEnabled;
        if (!force && sent == lastSent && total == lastTotal && on == lastEnabled) return;

        lastSent = sent;
        lastTotal = total;
        lastEnabled = on;
        updatedInfo.text = on ? string.Format(sentFormat, sent, total) : string.Format(offFormat, total);
    }
}
