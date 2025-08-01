using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DamageableDummy : MonoBehaviour
{
    [Header("Stats")]
    public float maxHP = 100f;
    public float weight = 3.0f;

    [Header("Damage Settings")]
    public float minVelocityToDamage = 3f;
    public float baseDamageMultiplier = 10f;
    public float weaponBonusMultiplier = 2f;

    [Header("Respawn")]
    public float respawnDelay = 3f;

    [Header("Recovery")]
    public float knockdownAngle = 60f;
    public float recoverDelay = 2f;

    [Header("Bounce Settings")]
    [SerializeField] private float bounceMultiplier = 1.5f;

    private float currentHP;
    private bool recovering = false;
    private bool isLockedFromWeakHit = false;
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

        rb.mass = weight;
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
        rb.angularDrag = 5f;
        rb.drag = 1f;
    }

    void FixedUpdate()
    {
        if (recovering) return;

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

        float angleFromUp = Vector3.Angle(transform.up, Vector3.up);
        if (angleFromUp > knockdownAngle)
        {
            StartCoroutine(RightAfterDelay());
        }

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

            if (impactVelocity >= minVelocityToDamage)
            {
                float damage = impactVelocity * baseDamageMultiplier;
                if (holdable.lockRotation) damage *= weaponBonusMultiplier;
                TakeDamage(damage);
            }

            if (playerStrength < weight)
            {
                StartCoroutine(LockPhysicsTemporarily());
            }
            else
            {
                Vector3 pushDir = collision.relativeVelocity.normalized;
                float pushForce = impactVelocity * (playerStrength / weight) * 5f;
                rb.AddForce(pushDir * pushForce, ForceMode.Impulse);
            }

            return;
        }

        // === COLLISION WITH PLAYER BODY ===
        if (stats != null && playerStrength < weight)
        {
            StartCoroutine(LockPhysicsTemporarily());

            Rigidbody playerRb = collision.rigidbody;
            if (playerRb != null)
            {
                // 1. Disable movement and aim at dummy
                PlayerMovement pm = playerRb.GetComponent<PlayerMovement>();
                if (pm != null)
                {
                    pm.ApplyKnockStun(0.5f, transform.position);
                }

                // 2. Hover player up briefly
                Vector3 hoverPos = playerRb.position + Vector3.up * 2f;
                playerRb.MovePosition(hoverPos);
                playerRb.velocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;

                // 3. Launch after delay
                StartCoroutine(DelayedKnockbackLaunch(playerRb));
            }
        }
        else if (stats != null)
        {
            Vector3 pushDir = collision.relativeVelocity.normalized;
            float pushForce = collision.relativeVelocity.magnitude * (playerStrength / weight) * 3f;
            rb.AddForce(pushDir * pushForce, ForceMode.Impulse);
        }
    }

    IEnumerator LockPhysicsTemporarily()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        isLockedFromWeakHit = true;
        lockTimer = 0.5f;

        yield return null;
    }

    IEnumerator DelayedKnockbackLaunch(Rigidbody playerRb)
    {
        yield return new WaitForSeconds(0.5f);

        Vector3 away = (playerRb.position - transform.position);
        away.y = 0f; // flatten
        away.Normalize();

        float randomAngle = Random.Range(-30f, 30f);
        Vector3 launchDir = Quaternion.AngleAxis(randomAngle, Vector3.up) * away;

        Vector3 launchVector = (launchDir + Vector3.down * 0.5f).normalized * (weight * bounceMultiplier);
        playerRb.AddForce(launchVector, ForceMode.Impulse);

        // Slide effect
        playerRb.drag = 0.5f;
        playerRb.angularDrag = 5f;
    }

    IEnumerator RightAfterDelay()
    {
        recovering = true;
        yield return new WaitForSeconds(recoverDelay);

        Vector3 currentVel = rb.velocity;

        rb.isKinematic = true;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        transform.position += Vector3.up * 0.2f;
        yield return new WaitForSeconds(0.1f);

        rb.isKinematic = false;
        rb.velocity = currentVel * 0.25f;
        rb.angularVelocity = Vector3.zero;

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.drag = 5f;
        rb.angularDrag = 10f;

        yield return new WaitForSeconds(0.5f);

        rb.constraints = RigidbodyConstraints.None;
        rb.drag = 0f;
        rb.angularDrag = 0.5f;

        recovering = false;
    }

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
        transform.position = startPosition;
        transform.rotation = startRotation;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        currentHP = maxHP;
        gameObject.SetActive(true);
    }
}
