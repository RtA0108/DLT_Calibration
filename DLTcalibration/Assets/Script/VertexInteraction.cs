using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // UI 관련 기능을 위해 필요

public class VertexInteraction : MonoBehaviour
{
    // [설정] 새로 생성할 Mesh Prefab (필요 시)
    public GameObject newMeshPrefab;

    // 내부 변수들
    private Color originalColor;
    private new Renderer renderer;
    private bool copied = false; // 중복 실행 방지용 플래그
    private GameObject markerManager;
    private GameObject LVManger;
    private Camera mainCam;

    void Start()
    {
        // 1. 렌더러 및 색상 초기화
        renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            originalColor = renderer.material.color;
        }

        // 2. 카메라 찾기 (안전장치 추가)
        if (Camera.main != null) mainCam = Camera.main;
        else
        {
            GameObject camObj = GameObject.FindGameObjectWithTag("MainCamera");
            if (camObj != null) mainCam = camObj.GetComponent<Camera>();
        }

        // 3. 매니저들 찾기
        markerManager = GameObject.Find("CanvasUI"); // 혹은 "MarkerManager"
        if (markerManager == null) markerManager = GameObject.Find("MarkerManager");

        LVManger = GameObject.Find("LevelManager");
    }

    private void OnMouseDown()
    {
        // ▼▼▼ [핵심] 1. Calibration 모드가 꺼져있으면 클릭 무시 ▼▼▼
        if (MainController.Instance != null && !MainController.Instance.IsCalibrationActive)
        {
            return; // 아무것도 안 하고 함수 종료
        }

        // ▼▼▼ [안전장치] 필수 요소가 없으면 에러 방지 ▼▼▼
        if (renderer == null || markerManager == null || LVManger == null || mainCam == null)
        {
            Debug.LogWarning("[VertexInteraction] 필요한 매니저나 컴포넌트를 찾을 수 없습니다.");
            return;
        }

        // 2. 색상 변경 (하양 <-> 빨강)
        // (VertexClickTest에서 선택 해제 로직이 있으므로, 여기서는 시각적 피드백만 줍니다)
        renderer.material.color = (renderer.material.color == originalColor) ? Color.red : originalColor;

        Debug.Log($"Vertex Clicked: {this.name} at {transform.position}");

        // 3. 마커 생성 및 데이터 저장 (기존 로직 유지)
        if (!copied)
        {
            // VertexClickTest 스크립트 가져오기
            var clickTest = LVManger.GetComponent<VertexClickTest>();

            if (clickTest != null)
            {
                // 현재 인덱스 가져오기 (주의: VertexClickTest의 arrayIndex와 동기화가 중요함)
                int meshIndex = clickTest.arrayIndex;

                // 화면 좌표 변환
                Vector2 screenPos = mainCam.WorldToScreenPoint(this.transform.position);

                // UI 마커 생성
                var markerMgrScript = markerManager.GetComponent<MarkerManager>();
                if (markerMgrScript != null)
                {
                    markerMgrScript.CreateMarker(screenPos);
                }

                // 데이터 주입 (범위 체크 추가)
                if (meshIndex < clickTest.verticesStruct.Length)
                {
                    clickTest.verticesStruct[meshIndex].screenCoordinate = screenPos;
                    clickTest.verticesStruct[meshIndex].screenCoordinateGT = screenPos;
                }
            }

            // copied = true; // [참고] 만약 선택/해제를 반복해야 한다면 이 줄을 지워야 할 수도 있습니다.
            // 일단 기존 기능 유지를 위해 놔둡니다.
            copied = true;
        }
    }
}

//using System;
//using System.Collections;
//using System.Collections.Generic;
//using Unity.VisualScripting;
//using UnityEngine;
//using UnityEngine.UI;

//public class VertexInteraction : MonoBehaviour
//{
//    public GameObject newMeshPrefab;
//    //새로 생성된 Vertex의 screenCoord를 지속적으로 저장 (마우스 위치가 아니라 sphere의 위치로 저장해야 함)
//    public Dictionary<int, Vector2> screenCoord = new Dictionary<int, Vector2>();

//    private GameObject createdMesh;
//    private GameObject LVManger;
//    private Color originalColor;
//    private new Renderer renderer;
//    private static int meshCounter = 0;
//    private int meshIndex = 0;
//    private bool copied = false;
//    public Camera mainCam;
//    private GameObject markerManager;
//    void Start()
//    {
//        Camera cam = GameObject.FindGameObjectWithTag("MainCamera").gameObject.GetComponent<Camera>();
//        mainCam = cam;
//        // Get the renderer component to access the material color
//        renderer = GetComponent<Renderer>();
//        // markerManager = GameObject.Find("MarkerManager");
//        markerManager = GameObject.Find("CanvasUI");
//        if (markerManager == null)
//        {
//            Debug.LogError("MarkerManager를 찾을 수 없습니다. 씬에 MarkerManager가 존재하는지 확인하세요.");
//        }
//        // Store the original color
//        originalColor = renderer.material.color;
//        LVManger = GameObject.Find("LevelManager");
//    }
//    private void OnMouseDown()
//    {
//        if (markerManager == null)
//        {
//            Debug.LogError("MarkerManager is not assigned.");
//            return;
//        }

//        renderer.material.color = renderer.material.color == originalColor ? Color.red : originalColor;
//        Debug.Log(this.transform.position);
//        if (!copied){

//            meshIndex = LVManger.GetComponent<VertexClickTest>().arrayIndex;
//            Vector2 screenCoordMarker = new Vector2(mainCam.WorldToScreenPoint(this.transform.position).x, mainCam.WorldToScreenPoint(this.transform.position).y);
//            Debug.Log("interaction"+screenCoordMarker);
//            markerManager.GetComponent<MarkerManager>().CreateMarker(screenCoordMarker);
//            //마커 2D 추가
//            LVManger.GetComponent<VertexClickTest>().verticesStruct[meshIndex].screenCoordinate = screenCoordMarker;
//            LVManger.GetComponent<VertexClickTest>().verticesStruct[meshIndex].screenCoordinateGT = screenCoordMarker;
//            meshCounter++;
//            copied = true;
//        }

//    }


//}