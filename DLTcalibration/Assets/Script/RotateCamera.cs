using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateCamera : MonoBehaviour
{
    public GameObject reference;

    private float xRotateMove, yRotateMove;

    public float rotateSpeed = 1000.0f;

    public float scrollSpeed = 2000.0f;
    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            xRotateMove = Input.GetAxis("Mouse X") * Time.deltaTime * rotateSpeed * 3;
            yRotateMove = Input.GetAxis("Mouse Y") * Time.deltaTime * rotateSpeed * 3;

            Vector3 referencePosition = reference.transform.position;

            transform.RotateAround(referencePosition, Vector3.right, -yRotateMove);
            transform.RotateAround(referencePosition, Vector3.up, xRotateMove);
            
            transform.LookAt(referencePosition);
        }
        else
        {
            float scroollWheel = Input.GetAxis("Mouse ScrollWheel");

            Vector3 cameraDirection = this.transform.localRotation * Vector3.forward;

            this.transform.position += cameraDirection * Time.deltaTime * scroollWheel * scrollSpeed;
        }
    }
}
