using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Interaction Settings")]
    public Transform holdPoint;           // Position where held objects will stay
    public float pickupRange = 3f;        // Max distance for picking up objects

    [Header("Hold Distance Settings")]
    public Transform holdPointRoot;       // Origin point for calculating hold position
    public float holdDistance = 2f;       // Current distance between player and held object
    public float minHoldDistance = 1f;    // Closest allowed distance to player
    public float maxHoldDistance = 4f;    // Farthest allowed distance
    public float breakDistance = 5f;      // If object drifts further than this, drop it

    [Header("Layers")]
    public LayerMask interactableLayer;   // Defines which objects can be picked up

    // Runtime variables
    private Rigidbody heldObject;         // Rigidbody of currently held object
    private Holdable heldComponent;       // Script attached to the object that defines its pickup/drop behavior

    void Update()
    {
        // --- Handle scroll input to adjust hold distance ---
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            // Increase/decrease hold distance within limits
            holdDistance = Mathf.Clamp(holdDistance + scroll * 1.5f, minHoldDistance, maxHoldDistance);
        }

        // --- If holding an object, update its position ---
        if (heldObject != null)
        {
            // Position holdPoint in front of player at chosen distance
            holdPoint.position = holdPointRoot.position + holdPointRoot.forward * holdDistance;

            // If object drifts too far away, drop it
            if (Vector3.Distance(heldObject.position, holdPoint.position) > breakDistance)
            {
                DropObject();
            }
        }

        // --- Handle input for picking up / dropping ---
        if (Input.GetMouseButtonDown(0)) // Left click pressed
        {
            TryPickup();
        }

        if (Input.GetMouseButtonUp(0)) // Left click released
        {
            DropObject();
        }
    }

    void TryPickup()
    {
        // Cast a ray from the center of the screen
        Ray ray = Camera.main.ScreenPointToRay(
            new Vector3(Screen.width / 2, Screen.height / 2, 0)
        );

        // Check if ray hits an interactable object within pickup range
        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, interactableLayer))
        {
            // Set hold distance to where the hit occurred (clamped to limits)
            float distanceToHit = Vector3.Distance(holdPointRoot.position, hit.point);
            holdDistance = Mathf.Clamp(distanceToHit, minHoldDistance, maxHoldDistance);

            // If hit object has a Rigidbody, store it
            if (hit.rigidbody != null)
            {
                heldObject = hit.rigidbody;
                heldComponent = heldObject.GetComponent<Holdable>();

                // Only hold if it has a Holdable component
                if (heldComponent != null)
                {
                    heldComponent.PickUp(holdPoint); // Call pickup logic on the object
                }
                else
                {
                    // If no Holdable script, cancel pickup
                    heldObject = null;
                }
            }
        }
    }

    void DropObject()
    {
        // If currently holding something, tell it to drop
        if (heldComponent != null)
        {
            heldComponent.Drop();
            heldObject = null;
            heldComponent = null;
        }
    }
}
