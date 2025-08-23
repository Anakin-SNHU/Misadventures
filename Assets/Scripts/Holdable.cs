using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Holdable : MonoBehaviour
{
    private Rigidbody rb;              // Cached Rigidbody reference
    private Transform followTarget;    // Target transform the object should follow when held

    [Header("Object Stats")]
    public float weight = 1.0f;        // Determines how heavy the object is (affects pickup behavior)
    public bool lockRotation = false;  // If true, object will rotate to match hold target
    public bool isWeapon = false;      // Flag to identify if object can be treated as a weapon

    [Header("Health Settings")]
    public float maxDurability = 5f;   // Maximum durability (health) of the object
    private float currentDurability;   // Tracks durability at runtime

    [Header("Force Settings")]
    [SerializeField] private float baseSpringStrength = 800f; // Base force pulling object toward hold point
    [SerializeField] private float baseDamper = 60f;          // Resistance force to smooth movement
    [SerializeField] private float maxForce = 1000f;          // Max allowed force (prevents physics explosions)

    private float strengthRatio = 1f;  // How strong the player is compared to the object’s weight

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        currentDurability = maxDurability;
    }

    /// Called when the player picks up the object.
    /// Calculates strength scaling and disables gravity so it can be carried smoothly.
    /// </summary>
    public void PickUp(Transform target)
    {
        followTarget = target;

        // Retrieve player’s strength (defaults to 1 if no PlayerStats found)
        PlayerStats stats = target.GetComponentInParent<PlayerStats>();
        float strength = (stats != null) ? stats.strength : 1f;

        // Ratio determines how well the player can hold this object
        strengthRatio = Mathf.Clamp(strength / Mathf.Max(weight, 0.01f), 0.1f, 2.5f);

        // Adjust Rigidbody settings for held state
        rb.useGravity = false;
        rb.drag = 1f;
        rb.angularDrag = 8f;
    }

    /// Called when object is dropped.
    /// Restores normal physics behavior.
    /// </summary>
    public void Drop()
    {
        followTarget = null;
        rb.useGravity = true;
        rb.drag = 0.5f;
    }

    void FixedUpdate()
    {
        if (followTarget == null) return;

        // --- Physics-based follow system ---
        Vector3 targetPos = followTarget.position;
        Vector3 toTarget = targetPos - rb.position;

        // Scale spring + damping by strength ratio
        float springStrength = baseSpringStrength * strengthRatio;
        float damper = baseDamper * strengthRatio;

        // Spring force pulls object to hold point
        Vector3 springForce = toTarget * springStrength;
        // Damping force resists velocity, smoothing out jitter
        Vector3 dampingForce = -rb.velocity * damper;

        // Total force, clamped to avoid unstable physics
        Vector3 totalForce = springForce + dampingForce;
        totalForce = Vector3.ClampMagnitude(totalForce, maxForce);

        rb.AddForce(totalForce, ForceMode.Force);

        // Extra downward force if player is too weak to hold object steadily
        if (strengthRatio < 0.8f)
        {
            rb.AddForce(Vector3.down * weight * (1f - strengthRatio), ForceMode.Acceleration);
        }

        // Optional: lock rotation to match hold target’s orientation
        if (lockRotation)
        {
            Quaternion targetRot = followTarget.rotation;
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, Time.fixedDeltaTime * 5f));
        }
    }

    /// Damages object durability. If durability reaches zero, the object is destroyed.
    /// </summary>
    public void ApplyDurabilityDamage(float amount)
    {
        currentDurability -= amount;

        if (currentDurability <= 0f)
        {
            Destroy(gameObject); // Could be replaced with break animation, particles, etc.
        }
    }

    // --- Utility methods ---
    public float GetCurrentDurability() => currentDurability;
    public bool IsWeapon() => isWeapon;
}
