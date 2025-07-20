using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInteractions : MonoBehaviour
{
    [Header("InteractableInfo")]
    public float sphereCastRaidus = 0.5f;
    public int interactableLayerIndex;
    private Vector3 raycastPos;
    public GameObject lookObject;
    private PhysicsObject physicsObject;
    private Camera mainCamera;

    [Header("Pickup")]
    [SerializeField] private Transform pickupParent;
    public GameObject currentlyPickedUpObject;
    private Rigidbody pickupRB;

    [Header("ObjectFollow")]
    [SerializeField] private float minSpeed = 0;
    [SerializeField] private float maxSpeed = 300f;
    [SerializeField] private float maxDistance = 10f;
    private float currentSpeed = 0f;
    private float currentDist = 0f;

    [Header("Rotation")]
    public float rotationSpeed = 100f;
    Quaternion lookRot;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    // A simple visualization of the point were following in the scene view
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(pickupParent.position, 0.5f);
    }

    // Interactable object detections and distance check
    void Update()
    {
        //Here we check if were currently looking at an interactable object
        raycastPos = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        RaycastHit hit;
        if (Physics.SphereCast(raycastPos, sphereCastRaidus, mainCamera.transform.forward, out hit, maxDistance, 1 << interactableLayerIndex))
        {
            lookObject = hit.collider.transform.root.gameObject;
        }

        else
        {
            lookObject = null;
        }

        // if we press the button of choice
        if (Input.GetButtonDown("Fire2"))
        {
            if (currentlyPickedUpObject == null)
            {
                if (lookObject != null)
                {
                    PickUpObject();
                }
            }

            // if we press the pickup button and have something, we drop it
            else
            {
                BreakConnection();
            }
        }

        if (currentlyPickedUpObject != null && currentDist > maxDistance) BreakConnection();
    }

    // Velocity movement toward pickup parent and rotation
    private void FixedUpdate()
    {
        if (currentlyPickedUpObject != null)
        {
            currentDist = Vector3.Distance(pickupParent.position, pickupRB.position);
            currentSpeed = Mathf.SmoothStep(minSpeed, maxSpeed, currentDist / maxDistance);
            currentSpeed *= Time.fixedDeltaTime;
            Vector3 direction = pickupParent.position - pickupRB.position;
            pickupRB.velocity = direction.normalized * currentSpeed;
            // Rotation
            lookRot = Quaternion.LookRotation(mainCamera.transform.position - currentlyPickedUpObject.transform.position);
            lookRot = Quaternion.Slerp(mainCamera.transform.rotation, lookRot, rotationSpeed * Time.fixedDeltaTime);
            pickupRB.MoveRotation(lookRot);
        }
    }

    // Release the Object
    public void BreakConnection()
    {
        pickupRB.constraints = RigidbodyConstraints.None;
        currentlyPickedUpObject = null;
        physicsObject.pickedUp = false;
        currentDist = 0;
    }

    public void PickUpObject()
    {
        physicsObject = lookObject.GetComponentInChildren<PhysicsObject>();
        currentlyPickedUpObject = lookObject;
        pickupRB = currentlyPickedUpObject.GetComponent<Rigidbody>();
        pickupRB.constraints = RigidbodyConstraints.FreezeRotation;
        physicsObject.playerInteractions = this;
        StartCoroutine(physicsObject.PickUp());
    }
}
