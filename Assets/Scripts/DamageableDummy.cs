using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DamageableDummy : MonoBehaviour
{
    [Header("Stats")]
    public float maxHP = 100f;    // Dummy’s max hit points
    public float weight = 3.0f;   // Affects knockback physics

    [Header("Damage Settings")]
    public float minVelocityToDamage = 3f;   // Minimum impact velocity required to apply damage
    public float baseDamageMultiplier = 10f; // Scales velocity into damage
    public float weaponBonusMultiplier = 2f; // Extra damage multiplier if hit by weapon-type object

    [Header("Respawn")]
    public float respawnDelay = 3f; // Time before dummy respawns after dying

    [Header("Recovery")]
    public float knockdownAngle = 60f; // Angle from upright considered "knocked down"
    public float recoverDelay = 2f;    // Delay before dummy tries to stand back up

    [Header("Bounce Settings")]
    [SerializeField] private float bounceMultiplier = 1.5f; // Force multiplier for launching players away

    // Runtime state
    private float currentHP;
    private bool recovering = false;      // Whether dummy is currently standing up
    private bool isLockedFromWeakHit = false; // Prevents movement briefly when hit weakly
    private float lockTimer = 0f;

    private Rigidbody rb;
    private Vector3 startPosition;
    private Quaternion startRotation;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        startPosition = transform.position;
        startRotation = transform.rotation;
        currentHP = maxHP;

        // Adjust Rigidbody to feel heavier and more stable
        rb.mass = weight;
        rb.centerOfMass = new Vector3(0, -0.5f, 0); // Lowers center of mass
        rb.angularDrag = 5f;
        rb.drag = 1f;
    }

    void FixedUpdate()
    {
        if (recovering) return;

        // Handle temporary lock (dummy becomes immovable briefly)
        if (isLockedFromWeakHit)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            lockTimer -= Time.fixedDeltaTime;

            if (lockTimer <= 0f)
            {
                isLockedFromWeakHit = false;
                rb.isKinematic = false;
            }
            return;
        }

        // Check if dummy is knocked over
        float angleFromUp = Vector3.Angle(transform.up, Vector3.up);
        if (angleFromUp > knockdownAngle)
        {
            StartCoroutine(RightAfterDelay()); // Try to get back up after delay
        }

        // If standing, reduce wobbling by damping angular velocity
        if (!rb.isKinematic && angleFromUp < knockdownAngle * 0.5f)
        {
            rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, Time.fixedDeltaTime * 5f);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.rigidbody == null) return;

        Holdable holdable = collision.rigidbody.GetComponent<Holdable>();
        PlayerStats stats = collision.rigidbody.GetComponentInParent<PlayerStats>();
        float playerStrength = (stats != null) ? stats.strength : 0f;

        // === DAMAGE FROM HELD OBJECT ===
        if (holdable != null)
        {
            float impactVelocity = collision.relativeVelocity.magnitude;

            // Apply damage if impact speed is high enough
            if (impactVelocity >= minVelocityToDamage)
            {
                float damage = impactVelocity * baseDamageMultiplier;
                if (holdable.lockRotation) damage *= weaponBonusMultiplier; // Bonus if it's a weapon
                TakeDamage(damage);
            }

            // Weak players can’t push dummy -> dummy locks movement briefly
            if (playerStrength < weight)
            {
                StartCoroutine(LockPhysicsTemporarily());
            }
            else
            {
                // Strong players can push dummy with impulse force
                Vector3 pushDir = collision.relativeVelocity.normalized;
                float pushForce = impactVelocity * (playerStrength / weight) * 5f;
                rb.AddForce(pushDir * pushForce, ForceMode.Impulse);
            }

            return;
        }

        // === COLLISION WITH PLAYER BODY ===
        if (stats != null && playerStrength < weight)
        {
            // Lock dummy temporarily
            StartCoroutine(LockPhysicsTemporarily());

            Rigidbody playerRb = collision.rigidbody;
            if (playerRb != null)
            {
                // 1. Stun player and face them toward dummy
                PlayerMovement pm = playerRb.GetComponent<PlayerMovement>();
                if (pm != null)
                {
                    pm.ApplyKnockStun(0.5f, transform.position);
                }

                // 2. Hover player upward briefly before launch
                Vector3 hoverPos = playerRb.position + Vector3.up * 2f;
                playerRb.MovePosition(hoverPos);
                playerRb.velocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;

                // 3. Launch player backward after short delay
                StartCoroutine(DelayedKnockbackLaunch(playerRb));
            }
        }
        else if (stats != null) // Strong player collides
        {
            // Apply push force to dummy based on collision
            Vector3 pushDir = collision.relativeVelocity.normalized;
            float pushForce = collision.relativeVelocity.magnitude * (playerStrength / weight) * 3f;
            rb.AddForce(pushDir * pushForce, ForceMode.Impulse);
        }
    }

    // === Coroutine: Temporarily disable dummy physics ===
    IEnumerator LockPhysicsTemporarily()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        isLockedFromWeakHit = true;
        lockTimer = 0.5f;

        yield return null;
    }

    // === Coroutine: Launches player away after knockback delay ===
    IEnumerator DelayedKnockbackLaunch(Rigidbody playerRb)
    {
        yield return new WaitForSeconds(0.5f);

        // Determine direction away from dummy
        Vector3 away = (playerRb.position - transform.position);
        away.y = 0f; // Flatten
        away.Normalize();

        // Add randomness to knockback direction
        float randomAngle = Random.Range(-30f, 30f);
        Vector3 launchDir = Quaternion.AngleAxis(randomAngle, Vector3.up) * away;

        // Combine with downward push to slam player
        Vector3 launchVector = (launchDir + Vector3.down * 0.5f).normalized * (weight * bounceMultiplier);
        playerRb.AddForce(launchVector, ForceMode.Impulse);

        // Temporary drag for sliding effect
        playerRb.drag = 0.5f;
        playerRb.angularDrag = 5f;
    }

    // === Coroutine: Dummy recovers to standing after delay ===
    IEnumerator RightAfterDelay()
    {
        recovering = true;
        yield return new WaitForSeconds(recoverDelay);

        Vector3 currentVel = rb.velocity;

        // Reset rotation to upright
        rb.isKinematic = true;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        transform.position += Vector3.up * 0.2f; // Nudge up to avoid clipping
        yield return new WaitForSeconds(0.1f);

        rb.isKinematic = false;
        rb.velocity = currentVel * 0.25f;
        rb.angularVelocity = Vector3.zero;

        // Stabilize with temporary constraints
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.drag = 5f;
        rb.angularDrag = 10f;

        yield return new WaitForSeconds(0.5f);

        // Restore normal physics
        rb.constraints = RigidbodyConstraints.None;
        rb.drag = 0f;
        rb.angularDrag = 0.5f;

        recovering = false;
    }

    // === Damage system ===
    void TakeDamage(float amount)
    {
        currentHP -= amount;
        Debug.Log($"{name} took {amount:F1} damage. HP: {currentHP:F1}");
        if (currentHP <= 0f) Die();
    }

    void Die()
    {
        Debug.Log($"{name} died.");
        gameObject.SetActive(false);
        Invoke(nameof(Respawn), respawnDelay);
    }

    void Respawn()
    {
        // Reset position, rotation, velocity, and HP
        transform.position = startPosition;
        transform.rotation = startRotation;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        currentHP = maxHP;
        gameObject.SetActive(true);
    }
}
