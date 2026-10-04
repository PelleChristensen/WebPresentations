using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Switches between Cinemachine cameras by raising the priority of the chosen one.
/// The CinemachineBrain on the Main Camera blends to whichever camera has the highest priority.
/// Hook UI buttons up to SwitchTo(index), Next() or Previous().
/// </summary>
[AddComponentMenu("Camera/Camera Switcher")]
public class CameraSwitcher : MonoBehaviour
{
    [Tooltip("The cameras to switch between, in button order.")]
    [SerializeField] CinemachineCamera[] cameras;

    [Tooltip("Camera that is active when the scene starts.")]
    [SerializeField] int startIndex = 0;

    [SerializeField] int activePriority = 20;
    [SerializeField] int inactivePriority = 10;

    public int CurrentIndex { get; private set; } = -1;

    void Start() => SwitchTo(startIndex);

    /// <summary>Make camera number 'index' the live camera.</summary>
    public void SwitchTo(int index)
    {
        if (cameras == null || cameras.Length == 0) return;
        index = Mathf.Clamp(index, 0, cameras.Length - 1);

        for (int i = 0; i < cameras.Length; i++)
            if (cameras[i] != null)
                cameras[i].Priority = i == index ? activePriority : inactivePriority;

        CurrentIndex = index;
    }

    public void Next()     => SwitchTo((CurrentIndex + 1) % cameras.Length);
    public void Previous() => SwitchTo((CurrentIndex - 1 + cameras.Length) % cameras.Length);
}
