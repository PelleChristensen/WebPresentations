using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class InfoPage : MonoBehaviour
{
    [SerializeField]private UIAnimator animator; 
    [SerializeField] private Button closeButton; 
    [SerializeField] private Button nextButton, previousButton; 
    [SerializeField] private TMPro.TMP_Text text;
    [SerializeField] private List<InfoText> infoTexts = new List<InfoText>();
    public bool ShowFromStart = false; 
    private int currenttext = 0;
    
    void Start()
    {
        if(infoTexts.Count > 0)
        {
            SetText(currenttext);
            UpdateNextButton();
        }
        if(ShowFromStart)
        {
            animator.Show();
        }
    }

    public void Next ()
    {
        if(currenttext < infoTexts.Count -1)
        {
            currenttext++;
            SetText(currenttext);
            UpdateNextButton();
        }
    }

    public void Previous ()
    {
        if(currenttext > 0)
        {
            currenttext--;
            SetText(currenttext);
            UpdateNextButton(); 
        }
    }

    private void SetText(int target)
    {
        text.text = infoTexts[target].Text;
    }

    private void UpdateNextButton()
    {
        if(infoTexts.Count > 1)
        {
            if(currenttext < infoTexts.Count -1)
            {
                nextButton.enabled = true;
                nextButton.interactable = true; 
                previousButton.enabled = true;
                previousButton.interactable = true;
            }
            else
            {
                nextButton.enabled = false;
                nextButton.interactable = false; 
                previousButton.enabled = true;
                previousButton.interactable = true;
            }
            if(currenttext == 0)
            {
                previousButton.enabled = false;
                previousButton.interactable = false;
            }
        }
        else
        {
            nextButton.enabled = false; 
            previousButton.enabled = false;
        }
    }


}
