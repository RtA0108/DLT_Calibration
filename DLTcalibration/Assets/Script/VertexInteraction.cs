using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class VertexInteraction : MonoBehaviour
{
    public GameObject newMeshPrefab;
    //새로 생성된 Vertex의 screenCoord를 지속적으로 저장 (마우스 위치가 아니라 sphere의 위치로 저장해야 함)
    public Dictionary<int, Vector2> screenCoord = new Dictionary<int, Vector2>();

    private GameObject createdMesh;
    private GameObject LVManger;
    private Color originalColor;
    private new Renderer renderer;
    private static int meshCounter = 0;
    private int meshIndex = 0;
    private bool copied = false;
    public Camera mainCam;
    private GameObject markerManager;
    void Start()
    {
        Camera cam = GameObject.FindGameObjectWithTag("MainCamera").gameObject.GetComponent<Camera>();
        mainCam = cam;
        // Get the renderer component to access the material color
        renderer = GetComponent<Renderer>();
        // markerManager = GameObject.Find("MarkerManager");
        markerManager = GameObject.Find("CanvasUI");
        if (markerManager == null)
        {
            Debug.LogError("MarkerManager를 찾을 수 없습니다. 씬에 MarkerManager가 존재하는지 확인하세요.");
        }
        // Store the original color
        originalColor = renderer.material.color;
        LVManger = GameObject.Find("LevelManager");
    }
    private void OnMouseDown()
    {
        if (markerManager == null)
        {
            Debug.LogError("MarkerManager is not assigned.");
            return;
        }

        renderer.material.color = renderer.material.color == originalColor ? Color.red : originalColor;
        Debug.Log(this.transform.position);
        if (!copied){
            createdMesh = Instantiate(newMeshPrefab, this.transform.position, Quaternion.identity);
            Vector3 newPos = this.transform.position;
            meshIndex = LVManger.GetComponent<VertexClickTest>().arrayIndex;
            LVManger.GetComponent<VertexClickTest>().verticesStruct[meshIndex].worldCoordinate = newPos;
            newPos.z += 1000f; // Change this value as needed
            createdMesh.transform.position = newPos;
            // GameObject copy = Instantiate(gameObject);
            // copy.transform.Translate(0f,0f,-10f);
            // copy.transform.Position()
            Vector2 screenCoordMarker = new Vector2(mainCam.WorldToScreenPoint(newPos).x, mainCam.WorldToScreenPoint(newPos).y);
            Debug.Log("interaction"+screenCoordMarker);
            markerManager.GetComponent<MarkerManager>().CreateMarker(screenCoordMarker);
            //마커 2D 추가
            LVManger.GetComponent<VertexClickTest>().verticesStruct[meshIndex].screenCoordinate = screenCoordMarker;

            createdMesh.name = "2D_Vertex_" + meshCounter.ToString();
            meshCounter++;
            copied = true;
        }
        
    }
    void Update()
    {
 
    }

   
}