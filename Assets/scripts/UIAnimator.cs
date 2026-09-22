using DG.Tweening;
using UnityEngine;

public class UIAnimator : MonoBehaviour
{
    [SerializeField]private CanvasGroup canvasGroup;


    void Start()
    {
        
    }

    public void Show()
    {
        //TODO refactor
        canvasGroup.interactable = true; 
        canvasGroup.blocksRaycasts = true;
        canvasGroup.DOFade(1.0f,0.3f);
        
    }

    public void Hide()
    {
        canvasGroup.interactable = false; 
        canvasGroup.blocksRaycasts = false;
        canvasGroup.DOFade(0.0f,0.3f);
    }
}
