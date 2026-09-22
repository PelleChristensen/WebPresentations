using System.Collections.Generic;
using System.Xml;
using Unity.Cinemachine;
using UnityEngine;

public class CameraDirector : MonoBehaviour
{
         [SerializeField] private List<CinemachineCamera> cameras = new List<CinemachineCamera>();
         public CinemachineCamera startcamera; 
    
    void Start()
    {
        int i = 0; 
        foreach (var cam in cameras)
        {
            if (cam == startcamera)
            {
                cam.Priority = 9999;
            } 
            else
            {
                cam.Priority = i++;
            }
        }
        
    }

    public void SelectCamera(string target)
    {
        int i = 0; 
        foreach (var cam in cameras)
        {
            if (cam.name == target)
            {
                cam.Priority = 9999;
            } 
            else
            {
                cam.Priority = i++;
            }
        }
    }

}
