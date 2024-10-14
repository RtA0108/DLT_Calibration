using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
public class VertexClickTest : MonoBehaviour
{
    // private Color originalColor;
    // private Renderer renderer;
    // private bool copied = false;

    public GameObject[] clickedObjects; // Array to store clicked objects
    //public Vector3[] worldCoordinates; // Array to store world coordinates
    //public Vector2[] screenCoordinates; // Array to store screen coordinates
    //public double[] worldPoints;
    //public double[] imagePoints;
    public int arrayIndex;
    public Camera projectCam;

    public struct VertexStruct
    {
        public int uniqIndex;
        public Vector3 worldCoordinate;
        public Vector2 screenCoordinate;
        public VertexStruct(int vertexIndex, Vector3 worldCoord, Vector2 screenCoord)
        {
            this.uniqIndex = vertexIndex;
            this.worldCoordinate = worldCoord;
            screenCoordinate = screenCoord;
        }
    }
    public VertexStruct[] verticesStruct;

  

    private void Start()
    {
        // // Get the renderer component to access the material color
        // renderer = GetComponent<Renderer>();

        // // Store the original color
        // originalColor = renderer.material.color;
        //Debug.Log(projectCam.pixelHeight);

        //worldCoordinates = new Vector3[10];
        //screenCoordinates = new Vector2[10];
        //worldPoints = new double[18];
        //imagePoints = new double[12];

        clickedObjects = new GameObject[10]; // Initializing arrays with size 10
        verticesStruct = new VertexStruct[12];
        arrayIndex = 0;
    }

    private void Update()
    {
        // Check if the left mouse button is clicked
        if (Input.GetMouseButtonDown(0))
        {
            // Shoot a ray from the camera to the mouse position
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Check if the ray hits an object
            if (Physics.Raycast(ray, out hit))
            {
                // Store clicked object
                GameObject clickedObject = hit.collider.gameObject;
                
                // Check if the clicked object is not already in the array
                if (!ArrayContains(clickedObjects, clickedObject))
                {
                    // Find first empty slot
                    int index = System.Array.IndexOf(clickedObjects, null);
                    Debug.Log("index: "+ index);
                   
                    if (index != -1 && clickedObject.tag == "SphereIn2D")
                    {
                       //여기 확인 필요
                        clickedObjects[index] = clickedObject;

                        verticesStruct[index].uniqIndex = index;
                        verticesStruct[index].screenCoordinate = new Vector2(projectCam.WorldToScreenPoint(clickedObject.transform.position).x, projectCam.pixelHeight - projectCam.WorldToScreenPoint(clickedObject.transform.position).y);
                        arrayIndex++;

                        Debug.Log(verticesStruct[index].uniqIndex);
                        Debug.Log(verticesStruct[index].worldCoordinate);
                        Debug.Log(verticesStruct[index].screenCoordinate);
                    }
                    else if (clickedObject.tag == "SphereIn2D")
                    {
                        Debug.LogWarning("Clicked objects array is full. Increase array size if needed.");
                    }
                    else
                    {
                        //분석 필요 (어떤 기준으로 추가가 되는지 안되는지를 모르겠음)
                        Debug.LogWarning("WTF");
                    }

                    // You can do something with the clicked object here
                    //Debug.Log("Clicked object: " + clickedObject.name);
                    //Debug.Log("World Position: " + clickedObject.transform.position);
                    //Debug.Log("Screen Position: " + screenCoordinates[index]);
                }
                else
                {
                    Debug.Log("Object already clicked: " + clickedObject.name);
                }
            }
        }
    }
    // private void OnMouseDown()
    // {
    //     renderer.material.color = renderer.material.color == originalColor ? Color.red : originalColor;
    //     //Debug.Log(this.transform.position);
    //     if (!copied){
    //         GameObject copy = Instantiate(gameObject);
    //         copy.transform.Translate(0f,0f,-10f);
    //         copied = true;
    //     }

    // }
    // Function to check if an array contains a specific object
    private bool ArrayContains(GameObject[] array, GameObject obj)
    {
        foreach (GameObject item in array)
        {
            if (item == obj)
                return true;
        }
        return false;
    }
}