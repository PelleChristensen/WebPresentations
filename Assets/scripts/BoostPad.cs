using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pushes dynamic Rigidbodies along a direction for as long as they are inside this trigger collider.
/// The force is applied every physics step (FixedUpdate) between OnTriggerEnter and OnTriggerExit.
/// Kinematic bodies are ignored - they are moved by script, not by forces.
/// Strength and ForceMode can be changed at runtime (e.g. from BoostPadPanel).
/// </summary>
[RequireComponent(typeof(Collider))]
[AddComponentMenu("Physics/Boost Pad")]
public class BoostPad : MonoBehaviour
{
    [Tooltip("Direction of the boost. Default is the positive world Z axis.")]
    [SerializeField] Vector3 boostDirection = Vector3.forward;

    [Tooltip("If on, the direction rotates with the pad (local space). If off, it is in world space.")]
    [SerializeField] bool directionInLocalSpace = false;

    [Tooltip("Strength of the boost. Unit depends on the force mode (N, m/s², N·s or m/s).")]
    [Min(0f)]
    [SerializeField] float strength = 15f;

    [Tooltip("Force: continuous push in newtons, heavier objects accelerate less (a = F/m).\n" +
             "Acceleration: continuous, ignores mass.\n" +
             "Impulse: instant kick (N·s), applied every physics step, depends on mass.\n" +
             "VelocityChange: instant velocity change (m/s), applied every physics step, ignores mass.")]
    [SerializeField] ForceMode forceMode = ForceMode.Force;

    // Rigidbody -> how many of its colliders are inside (a body can have several colliders).
    readonly Dictionary<Rigidbody, int> inside = new();
    readonly List<Rigidbody> buffer = new();

    public float Strength { get => strength; set => strength = Mathf.Max(0f, value); }
    public ForceMode Mode { get => forceMode; set => forceMode = value; }

    /// <summary>World-space direction the pad pushes in.</summary>
    public Vector3 WorldDirection =>
        (directionInLocalSpace ? transform.TransformDirection(boostDirection) : boostDirection).normalized;

    void Reset()
    {
        // Make sure the collider is a trigger when the component is added.
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        inside[rb] = inside.TryGetValue(rb, out var n) ? n + 1 : 1;
    }

    void OnTriggerExit(Collider other)
    {
        var rb = other.attachedRigidbody;
        if (rb == null || !inside.TryGetValue(rb, out var n)) return;
        if (n <= 1) inside.Remove(rb); else inside[rb] = n - 1;
    }

    void FixedUpdate()
    {
        if (inside.Count == 0 || strength <= 0f) return;
        var force = WorldDirection * strength;

        buffer.Clear();
        buffer.AddRange(inside.Keys);
        foreach (var rb in buffer)
        {
            if (rb == null || !rb.gameObject.activeInHierarchy) { inside.Remove(rb); continue; } // destroyed while inside
            if (rb.isKinematic) continue;
            rb.AddForce(force, forceMode); // also wakes the body if it was sleeping
        }
    }

    void OnDisable() => inside.Clear();

    void OnDrawGizmosSelected()
    {
        var c = GetComponent<Collider>();
        var from = c != null ? c.bounds.center : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(from, from + WorldDirection * 2f);
        Gizmos.DrawSphere(from + WorldDirection * 2f, 0.08f);
    }
}
