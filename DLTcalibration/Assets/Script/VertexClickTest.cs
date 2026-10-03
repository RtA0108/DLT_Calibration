using UnityEngine;
using System;

public class VertexClickTest : MonoBehaviour
{
    [Header("Data")]
    public GameObject[] clickedObjects; // 선택된 Vertex 오브젝트들
    public int arrayIndex = 0; // 현재 선택된 개수

    public VertexStruct[] verticesStruct; // 데이터 저장용 구조체 배열

    [Header("References")]
    public Camera projectCam;

    [Serializable]
    public struct VertexStruct
    {
        public int uniqIndex;
        public Vector3 worldCoordinate;
        public Vector2 screenCoordinate;
        public Vector2 screenCoordinateGT;
    }

    private void Start()
    {
        clickedObjects = new GameObject[10];
        verticesStruct = new VertexStruct[12];
        arrayIndex = 0;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) && Display.activeEditorGameViewTarget == 0)
        {
            HandleClick();
        }
    }

    private void HandleClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            GameObject target = hit.collider.gameObject;

            // 태그 체크 (원하는 태그가 아니면 무시)
            if (!target.CompareTag("SphereMainCam")) return;

            // ▼▼▼ [수정됨] 토글 로직 구현 ▼▼▼

            // 1. 이미 선택된 오브젝트인가? -> 선택 해제 (삭제)
            if (ArrayContains(clickedObjects, target))
            {
                RemoveObject(target);
            }
            // 2. 새로운 오브젝트인가? -> 선택 (추가)
            else
            {
                AddObject(target);
            }
        }
    }

    // 오브젝트 추가 함수
    private void AddObject(GameObject target)
    {
        int emptyIndex = Array.IndexOf(clickedObjects, null);

        if (emptyIndex != -1)
        {
            clickedObjects[emptyIndex] = target;

            verticesStruct[emptyIndex].uniqIndex = emptyIndex;
            verticesStruct[emptyIndex].worldCoordinate = target.transform.position;

            arrayIndex++;
            Debug.Log($"[Select] 추가됨 ({arrayIndex}개): {target.name}");

            // (옵션) 선택되었음을 알리는 색상 변경 코드를 여기에 넣으세요
            // target.GetComponent<Renderer>().material.color = Color.red; 
        }
        else
        {
            Debug.LogWarning("더 이상 선택할 수 없습니다 (배열 가득 참).");
        }
    }

    // 오브젝트 제거 함수
    private void RemoveObject(GameObject target)
    {
        for (int i = 0; i < clickedObjects.Length; i++)
        {
            if (clickedObjects[i] == target)
            {
                clickedObjects[i] = null;
                // 구조체 데이터도 초기화 (필수는 아니지만 깔끔하게)
                verticesStruct[i] = new VertexStruct();

                arrayIndex--;
                Debug.Log($"[Deselect] 해제됨 ({arrayIndex}개 남음): {target.name}");

                // (옵션) 색상 복구 코드를 여기에 넣으세요
                // target.GetComponent<Renderer>().material.color = Color.white; 

                return;
            }
        }
    }

    private bool ArrayContains(GameObject[] array, GameObject obj)
    {
        foreach (var item in array)
        {
            if (item == obj) return true;
        }
        return false;
    }
}

//using System.Collections;
//using System.Collections.Generic;
//using Unity.VisualScripting;
//using UnityEngine;
//using UnityEngine.UI;
//public class VertexClickTest : MonoBehaviour
//{
//    public GameObject[] clickedObjects; // Array to store clicked objects
//    public int arrayIndex;
//    public Camera projectCam;

//    public struct VertexStruct
//    {
//        public int uniqIndex;
//        public Vector3 worldCoordinate;
//        public Vector2 screenCoordinate;
//        public Vector2 screenCoordinateGT;
//        public VertexStruct(int vertexIndex, Vector3 worldCoord, Vector2 screenCoord, Vector2 screenCoordGT)
//        {
//            this.uniqIndex = vertexIndex;
//            this.worldCoordinate = worldCoord;
//            this.screenCoordinate = screenCoord;
//            this.screenCoordinateGT = screenCoordGT;
//            //원래는 screenCoordinate에 this가 붙어있지 않았는데 이게 원인이었을까?
//        }
//    }
//    public VertexStruct[] verticesStruct;



//    private void Start()
//    {

//        clickedObjects = new GameObject[10]; // Initializing arrays with size 10
//        verticesStruct = new VertexStruct[12];
//        arrayIndex = 0;
//    }

//    private void Update()
//    {
//        // Check if the left mouse button is clicked
//        if (Input.GetMouseButtonDown(0) && Display.activeEditorGameViewTarget == 0)
//        {
//            // Shoot a ray from the camera to the mouse position
//            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
//            RaycastHit hit;

//            // Check if the ray hits an object
//            if (Physics.Raycast(ray, out hit))
//            {
//                // Store clicked object
//                GameObject clickedObject = hit.collider.gameObject;

//                // Check if the clicked object is not already in the array
//                if (!ArrayContains(clickedObjects, clickedObject))
//                {
//                    // Find first empty slot
//                    int index = System.Array.IndexOf(clickedObjects, null);
//                    Debug.Log("index: " + index);

//                    if (index != -1 && clickedObject.tag == "SphereMainCam")
//                    {
//                        //임시로 "SphereIn2D" 태그에서 현재태그로 변경. -> VertexInteraction에서 array에 추가하는 코드로 변경해야 함
//                        //여기 확인 필요
//                        clickedObjects[index] = clickedObject;
//                        Debug.Log("Object already clicked vertex MainCam: " + clickedObject.name);
//                        verticesStruct[index].uniqIndex = index;
//                        verticesStruct[index].worldCoordinate = clickedObject.transform.position;
//                        //verticesStruct[index].screenCoordinate = new Vector2(projectCam.WorldToScreenPoint(clickedObject.transform.position).x, projectCam.pixelHeight - projectCam.WorldToScreenPoint(clickedObject.transform.position).y);
//                        arrayIndex++;
//                        Debug.Log("Working");
//                    }
//                    else
//                    {
//                        Debug.Log("Object already clicked vertex MainCam: " + clickedObject.name);
//                        Debug.LogWarning("Clicked objects array is full. Increase array size if needed.");
//                    }

//                }
//                else
//                {
//                    Debug.Log("Object already clicked: " + clickedObject.name);
//                }
//            }
//        }
//    }


//    private bool ArrayContains(GameObject[] array, GameObject obj)
//    {
//        foreach (GameObject item in array)
//        {
//            if (item == obj)
//                return true;
//        }
//        return false;
//    }
//    // private void OnMouseDown()
//    // {
//    //     renderer.material.color = renderer.material.color == originalColor ? Color.red : originalColor;
//    //     //Debug.Log(this.transform.position);
//    //     if (!copied){
//    //         GameObject copy = Instantiate(gameObject);
//    //         copy.transform.Translate(0f,0f,-10f);
//    //         copied = true;
//    //     }

//    // }
//    // Function to check if an array contains a specific object

//}