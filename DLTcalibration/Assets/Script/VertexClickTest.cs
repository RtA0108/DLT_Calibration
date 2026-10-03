using UnityEngine;
using System;
using System.Collections.Generic;

// 대응점 선택을 슬롯 단위로 관리한다. 슬롯 i 하나에 아래 네 가지가 항상 함께 묶인다.
//   clickedObjects[i]  : 선택된 버텍스 구
//   verticesStruct[i]  : 3D 좌표 + 프로젝터 2D 좌표(screenCoordinate) + 클릭 당시 투영 위치(screenCoordinateGT)
//   markers[i]         : 프로젝터 화면의 마커 (드래그하면 verticesStruct[i].screenCoordinate가 갱신됨)
//   구 색상             : 빨강 = 선택됨
// 2D 좌표는 모두 projectCam(프로젝터)의 스크린 픽셀 좌표(좌하단 원점)다.
public class VertexClickTest : MonoBehaviour
{
    public const int MaxPoints = 20; // 대응점 슬롯 수 (= 추천점 개수 상한)
    private const float MinMarkerSpreadPixels = 20f; // 추천점이 이보다 좁게 몰려 있으면 경고

    [Header("Data")]
    public GameObject[] clickedObjects; // 선택된 Vertex 오브젝트들
    public int arrayIndex = 0; // 현재 선택된 개수

    public VertexStruct[] verticesStruct; // 데이터 저장용 구조체 배열

    [Header("References")]
    public Camera projectCam;            // 프로젝터 카메라 (2D 좌표의 기준)
    public MarkerManager markerManager;  // 마커를 띄울 CanvasUI의 MarkerManager

    [Header("Patch Marker")]
    public int patchSize = 64;                                    // 패치 한 변 (프로젝터 픽셀). 0이면 패치 없음
    public PatchSnapshot.Mode patchMode = PatchSnapshot.Mode.Lines; // T 키로 전환

    [Header("Live Calibration")]
    public bool liveSolve = true;          // L 키: 마커를 놓을 때마다 자동으로 DLT를 다시 풂 (점 6개 이상)
    public DLT_solve dltSolver;

    private Marker[] markers;
    private readonly PatchSnapshot patchSnapshot = new PatchSnapshot();
    private bool alignmentView;          // V 키: 프로젝터에 모델 없이 마커/패치만 표시
    private int savedCullingMask;

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
        clickedObjects = new GameObject[MaxPoints];
        verticesStruct = new VertexStruct[MaxPoints];
        markers = new Marker[MaxPoints];
        arrayIndex = 0;

        if (markerManager == null)
        {
            GameObject canvasUI = GameObject.Find("CanvasUI");
            if (canvasUI != null) markerManager = canvasUI.GetComponent<MarkerManager>();
        }
        if (dltSolver == null) dltSolver = GetComponent<DLT_solve>();
        if (projectCam == null) Debug.LogError("[VertexClickTest] projectCam이 지정되지 않았습니다.");
        if (markerManager == null) Debug.LogError("[VertexClickTest] MarkerManager를 찾을 수 없습니다.");
    }

    private void Update()
    {
        ReleaseDestroyedSlots();

        if (Input.GetMouseButtonDown(0) && Display.activeEditorGameViewTarget == 0)
        {
            HandleClick();
        }

        // 'R' 키: 추천점(빨간 구)을 대응점으로 바로 선택
        if (Input.GetKeyDown(KeyCode.R))
        {
            SelectRecommendedVertices();
        }

        // 'T' 키: 패치를 선(외곽선/모서리) <-> 텍스처로 전환
        if (Input.GetKeyDown(KeyCode.T))
        {
            TogglePatchMode();
        }

        // 'V' 키: 정렬 보기 (프로젝터에 모델 없이 마커/패치만)
        if (Input.GetKeyDown(KeyCode.V))
        {
            ToggleAlignmentView();
        }

        // 'L' 키: 실시간 재계산 켜기/끄기
        if (Input.GetKeyDown(KeyCode.L))
        {
            liveSolve = !liveSolve;
            Debug.Log($"[Live] 실시간 재계산: {(liveSolve ? "켜짐" : "꺼짐 (F 키로 직접 계산)")}");
        }
    }

    // 마커 드래그가 끝날 때 Marker가 호출한다.
    public void OnMarkerDragEnd(int slot)
    {
        if (liveSolve && dltSolver != null && SelectedCount() >= 6) dltSolver.PerformDLT(false);
    }

    // DLT 결과가 projectCam에 적용된 뒤 호출된다 (F 키, 실시간 재계산 모두).
    // 패치를 새 카메라 기준으로 다시 잘라, 그 버텍스 주변의 더 정확한 모양으로 바꾼다.
    public void OnCameraSolved()
    {
        GameObject target = MainController.Instance != null ? MainController.Instance.targetMesh : null;
        if (target == null || patchSize <= 0) return;
        patchSnapshot.EnsureCaptured(projectCam, target);

        for (int i = 0; i < clickedObjects.Length; i++)
        {
            if (clickedObjects[i] == null || markers[i] == null) continue;

            Vector3 projected = projectCam.WorldToScreenPoint(clickedObjects[i].transform.position);
            Vector2 predicted = new Vector2(projected.x, projected.y);
            markers[i].SetPatches(
                patchSnapshot.Crop(PatchSnapshot.Mode.Lines, predicted, patchSize),
                patchSnapshot.Crop(PatchSnapshot.Mode.Texture, predicted, patchSize),
                patchMode, patchSize);
        }
    }

    private int SelectedCount()
    {
        int count = 0;
        foreach (GameObject o in clickedObjects) if (o != null) count++;
        return count;
    }

    private void OnDestroy()
    {
        patchSnapshot.Release();
    }

    public void TogglePatchMode()
    {
        patchMode = patchMode == PatchSnapshot.Mode.Lines ? PatchSnapshot.Mode.Texture : PatchSnapshot.Mode.Lines;
        foreach (Marker m in markers) if (m != null) m.SetPatchMode(patchMode);
        Debug.Log($"[Patch] 패치 모드: {patchMode}");
    }

    public void ToggleAlignmentView()
    {
        alignmentView = !alignmentView;
        if (alignmentView)
        {
            savedCullingMask = projectCam.cullingMask;
            projectCam.cullingMask = 1 << LayerMask.NameToLayer("UI");
        }
        else
        {
            projectCam.cullingMask = savedCullingMask;
        }
        Debug.Log($"[Patch] 정렬 보기: {(alignmentView ? "켜짐 (마커/패치만 투사)" : "꺼짐")}");
    }

    // 마커에 주변 모양 패치를 붙인다. 패치 중심 = 현재 프로젝터 카메라로 본 버텍스 위치.
    private void AttachPatch(Marker marker, Vector2 screen)
    {
        GameObject target = MainController.Instance != null ? MainController.Instance.targetMesh : null;
        if (marker == null || target == null || patchSize <= 0) return;

        patchSnapshot.EnsureCaptured(projectCam, target);
        marker.SetPatches(
            patchSnapshot.Crop(PatchSnapshot.Mode.Lines, screen, patchSize),
            patchSnapshot.Crop(PatchSnapshot.Mode.Texture, screen, patchSize),
            patchMode, patchSize);
    }

    // 표시 중인 추천점들을 대응점으로 선택한다. 기존 선택(마커 포함)은 비우고,
    // 추천 순서(가장 salient한 점이 먼저)대로 슬롯 0번부터 채운다.
    public void SelectRecommendedVertices()
    {
        MainController main = MainController.Instance;
        if (main == null) return;

        if (!main.IsCalibrationActive)
        {
            Debug.LogWarning("[Recommend] Calibration 모드(키 1)를 먼저 켜세요.");
            return;
        }

        ProjectionMappingCalibrator calibrator = main.currentCalibrator;
        List<Vector3> recommended = calibrator != null ? calibrator.GetRecommendedPositions() : new List<Vector3>();
        if (recommended.Count == 0)
        {
            Debug.LogWarning("[Recommend] 표시된 추천점이 없습니다. 키 3으로 추천점을 먼저 표시하세요.");
            return;
        }

        if (main.sphereGenerator == null)
        {
            Debug.LogError("[Recommend] CreateSphereAtVertex를 찾을 수 없습니다.");
            return;
        }

        // 추천점과 버텍스 구는 같은 메쉬 버텍스에서 나온 좌표라 거의 일치한다. 허용 오차는 메쉬 크기의 0.1%.
        float tolerance = 1e-3f;
        if (calibrator.meshFilter != null && calibrator.meshFilter.TryGetComponent(out Renderer meshRenderer))
            tolerance = Mathf.Max(tolerance, meshRenderer.bounds.size.magnitude * 1e-3f);

        ClearSelection();

        int selected = 0;
        foreach (Vector3 position in recommended)
        {
            GameObject sphere = main.sphereGenerator.FindSphereAt(position, tolerance);
            if (sphere == null)
            {
                Debug.LogWarning($"[Recommend] {position} 위치의 버텍스 구를 찾지 못했습니다.");
                continue;
            }
            if (ArrayContains(clickedObjects, sphere)) continue;

            AddObject(sphere);
            selected++;
        }

        Debug.Log($"[Recommend] 추천점 {recommended.Count}개 중 {selected}개를 대응점으로 선택했습니다.");

        // 모델이 프로젝터 화면에서 너무 작으면 마커가 겹쳐서 맞출 수 없다.
        Rect spread = Rect.MinMaxRect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
        for (int i = 0; i < clickedObjects.Length; i++)
        {
            if (clickedObjects[i] == null) continue;
            Vector2 p = verticesStruct[i].screenCoordinateGT;
            spread.xMin = Mathf.Min(spread.xMin, p.x); spread.yMin = Mathf.Min(spread.yMin, p.y);
            spread.xMax = Mathf.Max(spread.xMax, p.x); spread.yMax = Mathf.Max(spread.yMax, p.y);
        }
        if (selected > 0 && Mathf.Max(spread.width, spread.height) < MinMarkerSpreadPixels)
            Debug.LogWarning($"[Recommend] 추천점들이 프로젝터 화면에서 {spread.width:F1}x{spread.height:F1}px 안에 몰려 있습니다. 스케일 슬라이더로 모델을 키우세요.");
    }

    // 모든 선택을 해제하고 마커도 지운다.
    public void ClearSelection()
    {
        for (int i = 0; i < clickedObjects.Length; i++)
        {
            if (ReferenceEquals(clickedObjects[i], null)) continue;
            if (clickedObjects[i] != null) SetSphereSelected(clickedObjects[i], false);
            ReleaseSlot(i);
        }
    }

    private void HandleClick()
    {
        // Calibration 모드가 꺼져 있으면 선택하지 않는다.
        if (MainController.Instance != null && !MainController.Instance.IsCalibrationActive) return;

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
        int slot = Array.IndexOf(clickedObjects, null);

        if (slot == -1)
        {
            Debug.LogWarning("더 이상 선택할 수 없습니다 (배열 가득 참).");
            return;
        }

        Vector3 world = target.transform.position;

        // 마커 시작 위치 = 현재 프로젝터 카메라로 이 버텍스를 투영한 위치.
        // (예전에는 Main Camera 픽셀 좌표를 써서, 해상도가 다른 프로젝터 화면과 좌표계가 섞였음)
        Vector3 projected = projectCam.WorldToScreenPoint(world);
        Vector2 screen = new Vector2(projected.x, projected.y);
        if (projected.z <= 0f || screen.x < 0f || screen.y < 0f || screen.x > projectCam.pixelWidth || screen.y > projectCam.pixelHeight)
            Debug.LogWarning($"[Select] {target.name}이(가) 프로젝터 화면 밖에 투영됩니다: {projected}");

        clickedObjects[slot] = target;
        verticesStruct[slot] = new VertexStruct
        {
            uniqIndex = slot,
            worldCoordinate = world,
            screenCoordinate = screen,
            screenCoordinateGT = screen
        };
        if (markerManager != null) markers[slot] = markerManager.CreateMarker(slot, screen, projectCam, this);
        AttachPatch(markers[slot], screen);
        SetSphereSelected(target, true);

        arrayIndex++;
        Debug.Log($"[Select] 추가됨 ({arrayIndex}개, 마커 {slot + 1}번): {target.name}");
    }

    // 오브젝트 제거 함수
    private void RemoveObject(GameObject target)
    {
        for (int i = 0; i < clickedObjects.Length; i++)
        {
            if (clickedObjects[i] == target)
            {
                SetSphereSelected(target, false);
                ReleaseSlot(i);
                Debug.Log($"[Deselect] 해제됨 ({arrayIndex}개 남음): {target.name}");
                return;
            }
        }
    }

    // 슬롯을 비우고 그 슬롯의 마커도 지운다.
    private void ReleaseSlot(int slot)
    {
        if (markers[slot] != null) Destroy(markers[slot].gameObject);
        markers[slot] = null;
        clickedObjects[slot] = null;
        verticesStruct[slot] = new VertexStruct();
        arrayIndex--;
    }

    // 메쉬를 바꾸면 구들이 새로 만들어지면서 선택돼 있던 구가 파괴된다. 그런 슬롯은 마커와 함께 정리한다.
    private void ReleaseDestroyedSlots()
    {
        for (int i = 0; i < clickedObjects.Length; i++)
        {
            // 참조는 남아 있는데 Unity 오브젝트는 파괴된 상태
            if (!ReferenceEquals(clickedObjects[i], null) && clickedObjects[i] == null)
                ReleaseSlot(i);
        }
    }

    private static void SetSphereSelected(GameObject sphere, bool selected)
    {
        if (sphere.TryGetComponent(out VertexInteraction interaction))
            interaction.SetSelected(selected);
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