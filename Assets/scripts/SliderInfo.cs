using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SliderInfo : MonoBehaviour
{
    [SerializeField]private  TMPro.TMP_Text headertext, minvalue, maxvalue, currentvalue; 
    [SerializeField]private string header; 
    [SerializeField]private Slider slider;

    void Start()
    {
        headertext.text = header; 
        slider.onValueChanged.AddListener( delegate {ChangeValue();});     
        minvalue.text = slider.minValue.ToString();
        maxvalue.text = slider.maxValue.ToString(); 
        ChangeValue();   
    }

    private void ChangeValue()
    {
        currentvalue.text = slider.value.ToString();
    }

}
