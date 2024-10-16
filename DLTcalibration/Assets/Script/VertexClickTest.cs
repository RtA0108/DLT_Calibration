using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
public class VertexClickTest : MonoBehaviour
{
    public GameObject[] clickedObjects; // Array to store clicked objects
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
                   
                    if (index != -1 && clickedObject.tag == "SphereMainCam")
                    {
                        //임시로 "SphereIn2D" 태그에서 현재태그로 변경. -> 이거 아냐...이렇게 하면 안돼... VertexInteraction에서 array에 추가하는 코드로 변경해야 함
                        //여기 확인 필요
                        clickedObjects[index] = clickedObject;
                        Debug.Log("Object already clicked vertex MainCam: " + clickedObject.name);
                        verticesStruct[index].uniqIndex = index;
                        verticesStruct[index].screenCoordinate = new Vector2(projectCam.WorldToScreenPoint(clickedObject.transform.position).x, projectCam.pixelHeight - projectCam.WorldToScreenPoint(clickedObject.transform.position).y);
                        arrayIndex++;

                        Debug.Log(verticesStruct[index].uniqIndex);
                        Debug.Log(verticesStruct[index].worldCoordinate);
                        Debug.Log(verticesStruct[index].screenCoordinate);
                    }
                    else
                    {
                        Debug.LogWarning("Clicked objects array is full. Increase array size if needed.");
                    }
                    
                }
                else
                {
                    Debug.Log("Object already clicked: " + clickedObject.name);
                }
            }
        }
    }

    
    private bool ArrayContains(GameObject[] array, GameObject obj)
    {
        foreach (GameObject item in array)
        {
            if (item == obj)
                return true;
        }
        return false;
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

}