using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;


public class FrustrumVisualizer : MonoBehaviour
{
    [SerializeField] private Camera localcamera; 
    [SerializeField] private LineRenderer lineRenderer, linerenderer2;

    private bool isOrtographic = false;
    private List<Vector3> frustrumpositions = new List<Vector3>(8);

    public bool IsOrtographic { get => isOrtographic; set => isOrtographic = value; }

    private Vector3 temp; 
    public void SetPerspective(bool isortho)
    {
        IsOrtographic = isortho;
        UpdateCameraData();
    }

    void Start()
    {
        UpdateCameraData();
    }

    public void UpdateCameraData()
    {
        if(IsOrtographic)
        {
            SetupOrthographicCameraFrustum();
        } else
        {
            SetupPerspectiveCameraFrustum();
        }
    }

    private void SetupOrthographicCameraFrustum()
    {

        float height = localcamera.orthographicSize * 2f;
        float width = height * localcamera.aspect;

    Vector3 neartopleft, neartopright, nearbottomright, nearbottomleft;
    Vector3 fartopleft, fartopright, farbottomright, farbottomleft;

    neartopleft.x = localcamera.transform.position.x - width * 0.5f;
    neartopleft.y = localcamera.transform.position.y + height * 0.5f;
    neartopleft.z = localcamera.transform.position.z + localcamera.nearClipPlane;

    neartopright.x = localcamera.transform.position.x + width * 0.5f;
    neartopright.y = localcamera.transform.position.y + height * 0.5f;
    neartopright.z = localcamera.transform.position.z + localcamera.nearClipPlane;

    nearbottomright.x = localcamera.transform.position.x + width * 0.5f;
    nearbottomright.y = localcamera.transform.position.y - height * 0.5f;
    nearbottomright.z = localcamera.transform.position.z + localcamera.nearClipPlane;

    nearbottomleft.x = localcamera.transform.position.x - width * 0.5f;
    nearbottomleft.y = localcamera.transform.position.y - height * 0.5f;
    nearbottomleft.z = localcamera.transform.position.z + localcamera.nearClipPlane;

    fartopleft.x = localcamera.transform.position.x - width * 0.5f;
    fartopleft.y = localcamera.transform.position.y + height * 0.5f;
    fartopleft.z = localcamera.transform.position.z + localcamera.farClipPlane;

    fartopright.x = localcamera.transform.position.x + width * 0.5f;
    fartopright.y = localcamera.transform.position.y + height * 0.5f;
    fartopright.z = localcamera.transform.position.z + localcamera.farClipPlane;

    farbottomright.x = localcamera.transform.position.x + width * 0.5f;
    farbottomright.y = localcamera.transform.position.y - height * 0.5f;
    farbottomright.z = localcamera.transform.position.z + localcamera.farClipPlane;

    farbottomleft.x = localcamera.transform.position.x - width * 0.5f;
    farbottomleft.y = localcamera.transform.position.y - height * 0.5f;
    farbottomleft.z = localcamera.transform.position.z + localcamera.farClipPlane;

    List<Vector3> positions = new List<Vector3>();

    positions.Add(neartopleft);
    positions.Add(neartopright);
    positions.Add(nearbottomright);
    positions.Add(nearbottomleft);
    positions.Add(neartopleft);
    positions.Add(fartopleft);
    positions.Add(fartopright);
    positions.Add(farbottomright);
    positions.Add(farbottomleft);
    positions.Add(fartopleft);
    positions.Add(fartopright);
    positions.Add(neartopright);
    positions.Add(nearbottomright);
    positions.Add(farbottomright);
    positions.Add(farbottomleft);
    positions.Add(nearbottomleft);

    UpdateLine(positions);

    }

    private void SetupPerspectiveCameraFrustum()
    {
        float nearHeight = 2f * Mathf.Tan(localcamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * localcamera.nearClipPlane;

        float nearWidth = nearHeight * localcamera.aspect;

        float farHeight = 2f * Mathf.Tan(localcamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * localcamera.farClipPlane; 

        float farWidth = farHeight * localcamera.aspect;

        Debug.Log("Cameraplanes updated: Near Height : " + nearHeight + " near width: " + nearWidth + " far height: " + farHeight + " far width: " + farWidth); 

        Vector3 neartopleft, neartopright, nearbottomright, nearbottomleft; 
        Vector3 fartopleft, fartopright, farbottomright, farbottomleft;

        neartopleft.x = localcamera.transform.position.x - nearWidth * 0.5f; 
        neartopleft.y = localcamera.transform.position.y + nearHeight * .5f;
        neartopleft.z = localcamera.transform.position.z + localcamera.nearClipPlane; 

        neartopright.x = localcamera.transform.position.x + nearWidth * .5f;
        neartopright.y = localcamera.transform.position.y + nearHeight * .5f;
        neartopright.z = localcamera.transform.position.z + localcamera.nearClipPlane; 

        nearbottomright.x = localcamera.transform.position.x + nearWidth * 0.5f;
        nearbottomright.y = localcamera.transform.position.y - nearHeight * .5f; 
        nearbottomright.z = localcamera.transform.position.z + localcamera.nearClipPlane;

        nearbottomleft.x = localcamera.transform.position.x - nearWidth * 0.5f;
        nearbottomleft.y = localcamera.transform.position.y - nearHeight * .5f; 
        nearbottomleft.z = localcamera.transform.position.z + localcamera.nearClipPlane;


        fartopleft.x = localcamera.transform.position.x - farWidth * 0.5f; 
        fartopleft.y = localcamera.transform.position.y + farHeight * .5f;
        fartopleft.z = localcamera.transform.position.z + localcamera.farClipPlane; 

        fartopright.x = localcamera.transform.position.x + farWidth * .5f;
        fartopright.y = localcamera.transform.position.y + farHeight * .5f;
        fartopright.z = localcamera.transform.position.z + localcamera.farClipPlane; 

        farbottomright.x = localcamera.transform.position.x + farWidth * 0.5f;
        farbottomright.y = localcamera.transform.position.y - farHeight * .5f; 
        farbottomright.z = localcamera.transform.position.z + localcamera.farClipPlane;

        farbottomleft.x = localcamera.transform.position.x - farWidth * 0.5f;
        farbottomleft.y = localcamera.transform.position.y - farHeight * .5f; 
        farbottomleft.z = localcamera.transform.position.z + localcamera.farClipPlane;


        List<Vector3> positions = new List<Vector3>();

        positions.Add(neartopleft);
        positions.Add(neartopright);
        positions.Add(nearbottomright);
        positions.Add(nearbottomleft);
        positions.Add(neartopleft);
        positions.Add(fartopleft);
        positions.Add(fartopright);
        positions.Add(farbottomright);
        positions.Add(farbottomleft);
        positions.Add(fartopleft);
        //test overdraw
        positions.Add(fartopright);
        positions.Add(neartopright);
        positions.Add(nearbottomright);
        positions.Add(farbottomright);   
        positions.Add(farbottomleft);
        positions.Add(nearbottomleft);

        UpdateLine(positions); 
    }

    private void UpdateLine(List<Vector3> list)
    {
        
        lineRenderer.positionCount = list.Count;
        lineRenderer.SetPositions(list.ToArray());

    }

/*
    Vector3 GetPoint(float halfWidth, float halfHeight, float z)
    {
    return localcamera.transform.position
         + localcamera.transform.right   * halfWidth
         + localcamera.transform.up      * halfHeight
         + localcamera.transform.forward * z;
    }
    */



}
