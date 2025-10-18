using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class CharacterMovement : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float crouchedSpeed = 2f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float jumpHeight = 5;
    [SerializeField] private GameObject cameraPivot;


    private float playerSpeed;
    private float cameraRotateX = 0f;
    private bool isCrouched = false;
    private bool isGrounded = false;
    private Rigidbody rigidBody;
    private Animator animator;
    private CapsuleCollider capsuleCollider;
    private float capsuleHalfHeight;


    void Start()
    {
        //--get components--
        animator = GetComponent<Animator>();
        rigidBody = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        capsuleHalfHeight = capsuleCollider.height / 2;


        //--hide the mosue cursor. Press Esc during play to show the cursor. --
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }




    void Update()
    {
        //--get values used for character and camera movement--
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        float mouse_X = Input.GetAxis("Mouse X")*mouseSensitivity;
        float mouse_Y = Input.GetAxis("Mouse Y")*mouseSensitivity * -1;
        //normalize horizontal and vertical input (I am not sure about this one but it seems to work :P)
        float normalizedSpeed = Vector3.Dot(new Vector3(horizontalInput, 0f, verticalInput).normalized, new Vector3(horizontalInput, 0f, verticalInput).normalized);


        //--camera movement and character sideways rotation--
        transform.Rotate(0, mouse_X, 0);
        cameraRotateX += mouse_Y;
        cameraRotateX = Mathf.Clamp(cameraRotateX, -15, 60); //limites the up/down rotation of the camera 
        cameraPivot.transform.localRotation = Quaternion.Euler(cameraRotateX, 0, 0);


        //--check if character is on the ground
        CheckGround();


        //--set the crouched state to a default value of false, unless I am pressing the crouch button 
        isCrouched = false;


        //--sets Speed, "inAir" and "isCrouched" parameters in the Animator--
        animator.SetFloat("Speed", playerSpeed);
        animator.SetBool("inAir", false);
        animator.SetBool("isCrouched", false);


        //--change playerSpeed and Animator Parameters when the "run" or "crouch" buttons are pressed--
        if (Input.GetButton("Run"))
        {
            transform.Translate(new Vector3(horizontalInput, 0, verticalInput) * runSpeed * Time.deltaTime);
            playerSpeed = Mathf.Lerp(playerSpeed, normalizedSpeed * runSpeed, 0.05f);
        }
        else if(Input.GetButton("Crouch")){
            isCrouched = true;
            transform.Translate(new Vector3(horizontalInput, 0, verticalInput) * crouchedSpeed * Time.deltaTime);
            playerSpeed = Mathf.Lerp(playerSpeed, normalizedSpeed * crouchedSpeed, 0.05f);
            animator.SetBool("isCrouched", true);
        }
        else //this is the standard walk behaviour 
        {
            transform.Translate(new Vector3(horizontalInput, 0, verticalInput) * walkSpeed * Time.deltaTime);
            playerSpeed = Mathf.Lerp(playerSpeed, normalizedSpeed * walkSpeed, 1f);
        }




        //--Jump behaviour--
        if (Input.GetButton("Jump") && isGrounded && !isCrouched)
        {
            rigidBody.linearVelocity = new Vector3(0, jumpHeight, 0);
        }
        if (!isGrounded)
        {
            animator.SetBool("inAir", true);
        }


        //--Play the "Special" animation --
        if (Input.GetButtonDown("Special"))
        {
            animator.SetTrigger("Special");
        }
    }


    void CheckGround()
    {
        //--send a ray from the center of the collider to the ground. The player is "grounded" if the ray distance(length) is equal to half of the capsule height--
        Physics.Raycast(capsuleCollider.bounds.center, Vector3.down, out var hit);
        if (hit.distance < (capsuleHalfHeight + 0.1f))
        {
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
        }
    }


}
