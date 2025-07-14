using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    public Transform holdPoint;
    public float pickupRange = 3f;
    
    public Transform holdPointRoot;
    public float holdDistance = 2f;
    public float minHoldDistance = 1f;
    public float maxHoldDistance = 4f;
    public float breakDistance = 5f;

    public LayerMask interactableLayer;

    private Rigidbody heldObject;
    private Holdable heldComponent;

    void Update()
    {
        // Scroll input
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            holdDistance = Mathf.Clamp(holdDistance + scroll * 1.5f, minHoldDistance, maxHoldDistance);
        }

        if (heldObject != null)
        {
            holdPoint.position = holdPointRoot.position + holdPointRoot.forward * holdDistance;

            // Check if object is too far
            if (Vector3.Distance(heldObject.position, holdPoint.position) > breakDistance)
            {
                DropObject();
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            TryPickup();
        }

        if (Input.GetMouseButtonUp(0))
        {
            DropObject();
        }
    }

    void TryPickup()
    {
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));

        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, interactableLayer))
        {
            float distanceToHit = Vector3.Distance(holdPointRoot.position, hit.point);
            holdDistance = Mathf.Clamp(distanceToHit, minHoldDistance, maxHoldDistance); // Add this line

            if (hit.rigidbody != null)
            {
                heldObject = hit.rigidbody;
                heldComponent = heldObject.GetComponent<Holdable>();

                if (heldComponent != null)
                {
                    heldComponent.PickUp(holdPoint);
                }
                else
                {
                    heldObject = null;
                }
            }
        }
    }


    void DropObject()
    {
        if (heldComponent != null)
        {
            heldComponent.Drop();
            heldObject = null;
            heldComponent = null;
        }
    }
}
