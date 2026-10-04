using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Reusable popup panel. Shows/hides through the CanvasGroup (alpha, interactable,
/// blocksRaycasts) and plays a DOTween punch-scale so the panel "pops" forward.
///
/// Intended as a base class: derive from it and override OnBeforeShow / OnAfterShow /
/// OnBeforeHide / OnAfterHide to fill in content for specific kinds of popups.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class PopupPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("Optional. The panel's close button - Hide() is hooked up automatically.")]
    [SerializeField] private Button closeButton;
    [Tooltip("The transform that gets punched. Defaults to this panel's RectTransform.")]
    [SerializeField] private RectTransform animatedTarget;

    [Header("Behaviour")]
    [SerializeField] private bool startHidden = true;
    [Tooltip("Ignore Time.timeScale (useful if the game is paused while the popup is open).")]
    [SerializeField] private bool useUnscaledTime = true;
    [Tooltip("Opening this popup closes any other open popup with the same group name. Leave empty to allow it to stay open alongside others.")]
    [SerializeField] private string exclusiveGroup = "popups";

    [Header("Show")]
    [SerializeField] private float fadeInDuration = 0.2f;
    [Tooltip("How much bigger the panel punches out, relative to its size (0.1 = 10%).")]
    [SerializeField] private float punchAmount = 0.1f;
    [SerializeField] private float punchDuration = 0.4f;
    [SerializeField] private int punchVibrato = 6;
    [Range(0f, 1f)] [SerializeField] private float punchElasticity = 0.5f;

    [Header("Hide")]
    [SerializeField] private float fadeOutDuration = 0.15f;
    [Tooltip("Scale the panel shrinks to while fading out (1 = no shrink).")]
    [SerializeField] private float hideScale = 0.95f;

    [Header("Events")]
    public UnityEvent onShown;
    public UnityEvent onHidden;

    /// <summary>Raised the moment Show() starts (before the animation).</summary>
    public event System.Action<PopupPanel> Opened;
    /// <summary>Raised the moment Hide() starts, or when hidden immediately while open.</summary>
    public event System.Action<PopupPanel> Closed;

    public bool IsOpen { get; private set; }

    private Sequence sequence;
    private Vector3 baseScale = Vector3.one;

    // All popups that are currently open (or opening), across the scene.
    private static readonly System.Collections.Generic.List<PopupPanel> openPanels = new System.Collections.Generic.List<PopupPanel>();

    // Clear the static list when entering play mode (needed if domain reload is disabled).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => openPanels.Clear();

    public string ExclusiveGroup { get => exclusiveGroup; set => exclusiveGroup = value; }

    protected virtual void Reset()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        animatedTarget = transform as RectTransform;
        closeButton = GetComponentInChildren<Button>(true);
    }

    protected virtual void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (animatedTarget == null) animatedTarget = transform as RectTransform;
        baseScale = animatedTarget.localScale;

        if (closeButton != null) closeButton.onClick.AddListener(Hide);

        if (startHidden) SetHiddenImmediate();
        else SetVisibleImmediate();
    }

    protected virtual void OnDestroy()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(Hide);
        sequence?.Kill();
        openPanels.Remove(this);
    }

    // ---------- Public API (usable from UnityEvents, e.g. a Button's OnClick) ----------

    [ContextMenu("Show")]
    public void Show()
    {
        if (IsOpen) return;
        IsOpen = true;

        CloseOthersInGroup();
        openPanels.Add(this);

        OnBeforeShow();
        Opened?.Invoke(this);
        StartSequence();

        // Clickable straight away, so the user can interact during the pop.
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        animatedTarget.localScale = baseScale;

        sequence.Append(canvasGroup.DOFade(1f, fadeInDuration).SetEase(Ease.OutQuad));
        sequence.Join(animatedTarget.DOPunchScale(baseScale * punchAmount, punchDuration, punchVibrato, punchElasticity));
        sequence.OnComplete(() =>
        {
            animatedTarget.localScale = baseScale;
            OnAfterShow();
            onShown?.Invoke();
        });
    }

    [ContextMenu("Hide")]
    public void Hide()
    {
        if (!IsOpen) return;
        IsOpen = false;

        openPanels.Remove(this);

        OnBeforeHide();
        Closed?.Invoke(this);
        StartSequence();

        // Stop clicks immediately so nothing can be pressed while fading out.
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        sequence.Append(canvasGroup.DOFade(0f, fadeOutDuration).SetEase(Ease.InQuad));
        sequence.Join(animatedTarget.DOScale(baseScale * hideScale, fadeOutDuration).SetEase(Ease.InQuad));
        sequence.OnComplete(() =>
        {
            animatedTarget.localScale = baseScale;
            OnAfterHide();
            onHidden?.Invoke();
        });
    }

    [ContextMenu("Toggle")]
    public void Toggle()
    {
        if (IsOpen) Hide();
        else Show();
    }

    public void SetHiddenImmediate()
    {
        sequence?.Kill();
        bool wasOpen = IsOpen;
        IsOpen = false;
        openPanels.Remove(this);
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        if (animatedTarget != null) animatedTarget.localScale = baseScale;
        if (wasOpen) Closed?.Invoke(this);
    }

    public void SetVisibleImmediate()
    {
        sequence?.Kill();
        bool wasOpen = IsOpen;
        if (!wasOpen)
        {
            CloseOthersInGroup();
            openPanels.Add(this);
        }
        IsOpen = true;
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        if (animatedTarget != null) animatedTarget.localScale = baseScale;
        if (!wasOpen) Opened?.Invoke(this);
    }

    // ---------- Hooks for derived popups ----------

    /// <summary>Called before the show animation starts - fill in content here.</summary>
    protected virtual void OnBeforeShow() { }
    /// <summary>Called when the show animation has finished.</summary>
    protected virtual void OnAfterShow() { }
    /// <summary>Called before the hide animation starts.</summary>
    protected virtual void OnBeforeHide() { }
    /// <summary>Called when the panel is fully hidden.</summary>
    protected virtual void OnAfterHide() { }

    // ---------- Internals ----------

    /// <summary>Closes every open popup in the given group (all groups if null).</summary>
    public static void CloseAll(string group = null)
    {
        foreach (var p in openPanels.ToArray())
            if (p != null && (group == null || p.exclusiveGroup == group)) p.Hide();
    }

    private void CloseOthersInGroup()
    {
        if (string.IsNullOrEmpty(exclusiveGroup)) return;
        foreach (var p in openPanels.ToArray())
            if (p != null && p != this && p.exclusiveGroup == exclusiveGroup) p.Hide();
    }

    private void StartSequence()
    {
        // Kill any running show/hide so rapid clicks never leave the panel half-visible.
        sequence?.Kill();
        animatedTarget.DOKill();
        canvasGroup.DOKill();

        sequence = DOTween.Sequence()
            .SetUpdate(useUnscaledTime)
            .SetLink(gameObject);
    }
}
