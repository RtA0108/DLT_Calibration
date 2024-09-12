using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// public class RotateOnKeyPress : MonoBehaviour
// {
//     private bool isRotationMode = false;
//     private Quaternion originalRotation;
//     //GameObject[] objects = GameObject.FindGameObjectsWithTag("something");

//     void Start()
//     {
//         // Store the original rotation when the script starts
//         originalRotation = transform.rotation;
//     }

//     void Update()
//     {
//         // Check for the 'R' key press
//         if (Input.GetKeyDown(KeyCode.R))
//         {
//             // Toggle between rotation mode and restoring original state
//             isRotationMode = !isRotationMode;

//             if (isRotationMode)
//             {
//                 // Enter rotation mode
//                 StartRotationMode();
//             }
//             else
//             {
//                 // Restore original state
//                 RestoreOriginalState();
//             }
//         }

//         // Rotate the mesh in rotation mode
//         if (isRotationMode)
//         {
//             RotateMesh();
//         }
//     }

//     void StartRotationMode()
//     {
//         // Check if the GameObject has the specified tag
//         if (gameObject.CompareTag("something"))
//         {
//             // Perform any setup needed before rotation mode
//             // (e.g., disable other scripts, change materials, etc.)
//         }
//     }

//     void RestoreOriginalState()
//     {
//         // Reset the rotation to the original state
//         transform.rotation = originalRotation;

//         // Check if the GameObject has the specified tag
//         if (gameObject.CompareTag("something"))
//         {
//             // Perform any cleanup needed after exiting rotation mode
//             // (e.g., enable other scripts, revert materials, etc.)
//         }
//     }

//     void RotateMesh()
//     {
//         // Rotate the mesh while in rotation mode
//         // You can customize the rotation behavior based on your requirements
//         transform.Rotate(Vector3.up * Time.deltaTime * 30f); // Rotating around the Y-axis for demonstration
//     }
// }


// public class RotateOnKeyPress : MonoBehaviour
// {
//     private bool isRotationMode = false;

//     private bool isDragging = false;
//     private Vector3 originalMousePosition;

//     void Start()
//     {
        
//     }

//     void Update()
//     {
//         // Check for the 'R' key press
//         if (Input.GetKeyDown(KeyCode.R))
//         {
//             // Toggle between rotation mode and restoring original state
//             isRotationMode = !isRotationMode;
            
//         }

//         // Rotate the mesh in rotation mode
//         if (isRotationMode)
//         {
//             // Check for mouse button down to start dragging
//              if (Input.GetMouseButtonDown(0))
//             {
//                 StartDrag();
//             }

//             // Check for mouse button release to stop dragging
//             if (Input.GetMouseButtonUp(0))
//             {
//                 StopDrag();
//             }
//             // Rotate the mesh while dragging
//             if (isDragging)
//             {
//                 RotateOnDrag();
//             }
//         }


        
//     }

//     void StartDrag()
//     {
//         // Store the original mouse position when dragging starts
//         isDragging = true;
//         originalMousePosition = Input.mousePosition;
//     }

//     void StopDrag()
//     {
//         // Stop dragging when the mouse button is released
//         isDragging = false;
//     }

//     void RotateOnDrag()
//     {
//         // Calculate the mouse movement since the last frame
//         Vector3 deltaMouse = Input.mousePosition - originalMousePosition;

//         // Calculate rotation angles based on mouse movement
//         float rotateX = deltaMouse.y;
//         float rotateY = -deltaMouse.x;

//         // Rotate the mesh based on the calculated angles
//         transform.Rotate(Vector3.up, rotateY, Space.World);
//         transform.Rotate(Vector3.right, rotateX, Space.World);

//         // Update the original mouse position for the next frame
//         originalMousePosition = Input.mousePosition;
//     }
// }