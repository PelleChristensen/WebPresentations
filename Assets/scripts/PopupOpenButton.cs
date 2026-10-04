using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Put this on a Button to make it open a PopupPanel.
/// While the panel is open the button stays in its "Selected" look (using the button's
/// own Selected colour / sprite / animation trigger). When the panel closes - by its close
/// button, by code, or by clicking this button again - the button returns to normal.
/// </summary>
[RequireComponent(typeof(Button))]
public class PopupOpenButton : MonoBehaviour
{
    [Tooltip("The popup this button opens.")]
    [SerializeField] private PopupPanel panel;
    [Tooltip("Clicking the button while the popup is open closes it again.")]
    [SerializeField] private bool clickAgainToClose = true;

    private Button button;
    private Selectable.Transition savedTransition;
    private bool showingSelected;

    public PopupPanel Panel
    {
        get => panel;
        set
        {
            if (panel == value) return;
            Unsubscribe();
            panel = value;
            if (isActiveAndEnabled) Subscribe();
        }
    }

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClick);
    }

    private void OnEnable() => Subscribe();

    private void OnDisable()
    {
        Unsubscribe();
        ClearSelectedLook();
    }

    private void Subscribe()
    {
        if (panel == null) return;
        panel.Opened += HandleOpened;
        panel.Closed += HandleClosed;
        if (panel.IsOpen) ApplySelectedLook();
    }

    private void Unsubscribe()
    {
        if (panel == null) return;
        panel.Opened -= HandleOpened;
        panel.Closed -= HandleClosed;
    }

    private void OnClick()
    {
        if (panel == null)
        {
            Debug.LogWarning("PopupOpenButton: no panel assigned.", this);
            return;
        }

        if (!panel.IsOpen) panel.Show();
        else if (clickAgainToClose) panel.Hide();
    }

    private void HandleOpened(PopupPanel p) => ApplySelectedLook();
    private void HandleClosed(PopupPanel p) => ClearSelectedLook();

    // The EventSystem forgets the selection as soon as the user clicks anything else
    // (e.g. a toggle inside the popup), so we hold the Selected look ourselves:
    // pause the button's own transitions and show its Selected state manually.
    private void ApplySelectedLook()
    {
        if (showingSelected) return;
        showingSelected = true;

        savedTransition = button.transition;
        button.transition = Selectable.Transition.None;

        switch (savedTransition)
        {
            case Selectable.Transition.ColorTint:
                if (button.targetGraphic != null)
                {
                    var c = button.colors;
                    button.targetGraphic.CrossFadeColor(c.selectedColor * c.colorMultiplier, c.fadeDuration, true, true);
                }
                break;

            case Selectable.Transition.SpriteSwap:
                if (button.image != null) button.image.overrideSprite = button.spriteState.selectedSprite;
                break;

            case Selectable.Transition.Animation:
                if (button.animator != null) button.animator.SetTrigger(button.animationTriggers.selectedTrigger);
                break;
        }
    }

    private void ClearSelectedLook()
    {
        if (!showingSelected) return;
        showingSelected = false;

        if (savedTransition == Selectable.Transition.SpriteSwap && button.image != null)
            button.image.overrideSprite = null;

        // Make sure the EventSystem doesn't still consider this button selected.
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            EventSystem.current.SetSelectedGameObject(null);

        // Restoring the transition makes the button fade back to its current state (normal/hover).
        button.transition = savedTransition;
    }
}
