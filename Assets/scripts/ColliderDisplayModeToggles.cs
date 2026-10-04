using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Radio-style toggles that set ColliderVisualizer.GlobalDisplayMode:
/// lines in front of everything, lines with depth (hidden behind geometry), or no lines.
/// Every ColliderVisualizer listens to the change, including objects spawned later.
/// </summary>
public class ColliderDisplayModeToggles : MonoBehaviour
{
    [System.Serializable]
    public class ModeToggle
    {
        public ColliderVisualizer.DisplayMode mode;
        public Toggle toggle;
    }

    [SerializeField] List<ModeToggle> toggles = new();
    [SerializeField] ColliderVisualizer.DisplayMode startMode = ColliderVisualizer.DisplayMode.InFront;

    void Start()
    {
        foreach (var mt in toggles)
        {
            if (mt.toggle == null) continue;
            var m = mt.mode; // capture for the lambda
            mt.toggle.onValueChanged.AddListener(isOn => { if (isOn) SetMode(m); });
        }
        SetMode(startMode);
    }

    void OnEnable()  => ColliderVisualizer.DisplayModeChanged += SyncToggles;
    void OnDisable() => ColliderVisualizer.DisplayModeChanged -= SyncToggles;

    public void SetMode(ColliderVisualizer.DisplayMode mode)
    {
        ColliderVisualizer.GlobalDisplayMode = mode;
        SyncToggles(mode);
    }

    // Keep the toggles right if the mode is changed from somewhere else (e.g. another script).
    void SyncToggles(ColliderVisualizer.DisplayMode mode)
    {
        foreach (var mt in toggles)
            if (mt.toggle) mt.toggle.SetIsOnWithoutNotify(mt.mode == mode);
    }

    // UnityEvent-friendly setters (enums can't be picked in a Button's OnClick list).
    public void SetInFront()   => SetMode(ColliderVisualizer.DisplayMode.InFront);
    public void SetWithDepth() => SetMode(ColliderVisualizer.DisplayMode.WithDepth);
    public void SetHidden()    => SetMode(ColliderVisualizer.DisplayMode.Hidden);
}
