using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Holdable : MonoBehaviour
{
    private Rigidbody rb;
    private Transform followTarget;

    [Header("Object Stats")]
    public float weight = 1.0f;
    public bool lockRotation = false;
    public bool isWeapon = false;

    [Header("Health Settings")]
    public float maxDurability = 5f;
    private float currentDurability;

    [Header("Force Settings")]
    [SerializeField] private float baseSpringStrength = 800f;
    [SerializeField] private float baseDamper = 60f;
    [SerializeField] private float maxForce = 1000f;

    private float strengthRatio = 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        currentDurability = maxDurability;
    }

    public void PickUp(Transform target)
    {
        followTarget = target;

        PlayerStats stats = target.GetComponentInParent<PlayerStats>();
        float strength = (stats != null) ? stats.strength : 1f;

        strengthRatio = Mathf.Clamp(strength / Mathf.Max(weight, 0.01f), 0.1f, 2.5f);

        rb.useGravity = false;
        rb.drag = 1f;
        rb.angularDrag = 8f;
    }

    public void Drop()
    {
        followTarget = null;
        rb.useGravity = true;
        rb.drag = 0.5f;
    }

    void FixedUpdate()
    {
        if (followTarget == null) return;

        Vector3 targetPos = followTarget.position;
        Vector3 toTarget = targetPos - rb.position;

        float springStrength = baseSpringStrength * strengthRatio;
        float damper = baseDamper * strengthRatio;

        Vector3 springForce = toTarget * springStrength;
        Vector3 dampingForce = -rb.velocity * damper;

        Vector3 totalForce = springForce + dampingForce;
        totalForce = Vector3.ClampMagnitude(totalForce, maxForce);
        rb.AddForce(totalForce, ForceMode.Force);

        if (strengthRatio < 0.8f)
        {
            rb.AddForce(Vector3.down * weight * (1f - strengthRatio), ForceMode.Acceleration);
        }

        if (lockRotation)
        {
            Quaternion targetRot = followTarget.rotation;
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, Time.fixedDeltaTime * 5f));
        }
    }

    public void ApplyDurabilityDamage(float amount)
    {
        currentDurability -= amount;

        if (currentDurability <= 0f)
        {
            Destroy(gameObject); // Can later play VFX/sound here
        }
    }

    public float GetCurrentDurability() => currentDurability;
    public bool IsWeapon() => isWeapon;
}
