using UnityEngine;

/// <summary>
/// Added at runtime by <see cref="ColliderVisualizer"/> to child colliders that would otherwise
/// not send their collision/trigger messages to the visualizer (e.g. static children without a Rigidbody).
/// Do not add this manually.
/// </summary>
[AddComponentMenu("")]
public class ColliderVisualizerRelay : MonoBehaviour
{
    [System.NonSerialized] public ColliderVisualizer target;

    void OnCollisionEnter(Collision collision) { if (target != null) target.HandleContacts(collision); }
    void OnCollisionStay(Collision collision)  { if (target != null) target.HandleContacts(collision); }
    void OnCollisionExit(Collision collision)  { if (target != null) target.HandleExit(collision); }

    void OnTriggerEnter(Collider other) { if (target != null) target.HandleTriggerEnter(gameObject, other); }
    void OnTriggerStay(Collider other)  { if (target != null) target.HandleTriggerEnter(gameObject, other); }
    void OnTriggerExit(Collider other)  { if (target != null) target.HandleTriggerExit(other); }
}
