using UnityEditor;
using UnityEngine;

public class MeshInitializer : MonoBehaviour
{
    // 유니티 상단 메뉴 [Tools] -> [Setup Selected Meshes...] 클릭 시 실행됨
    [MenuItem("Tools/Setup Selected Meshes for Saliency Test")]
    static void SetupSelectedMeshes()
    {
        // 1. 씬에서 'Project Camera' 미리 찾아놓기
        Camera projectCam = null;
        GameObject projectCamObj = GameObject.FindWithTag("Project Camera");
        if (projectCamObj != null)
        {
            projectCam = projectCamObj.GetComponent<Camera>();
        }
        else
        {
            Debug.LogWarning("[MeshInitializer] 씬에서 'Project Camera' 태그를 찾을 수 없습니다! 카메라는 수동으로 연결해야 합니다.");
        }

        foreach (GameObject go in Selection.gameObjects)
        {
            if (go == null) continue;

            // 1. MeshFilter
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null) mf = go.AddComponent<MeshFilter>();

            // 2. MeshRenderer (없을 때만 추가)
            if (go.GetComponent<MeshRenderer>() == null) go.AddComponent<MeshRenderer>();

            // 3. MeshCollider & SharedMesh 연결
            MeshCollider collider = go.GetComponent<MeshCollider>();
            if (collider == null) collider = go.AddComponent<MeshCollider>();
            if (mf.sharedMesh != null) collider.sharedMesh = mf.sharedMesh;

            // 4. ProjectionMappingCalibrator (최신 로직 반영)
            var calibrator = go.GetComponent<ProjectionMappingCalibrator>();
            if (calibrator == null) calibrator = go.AddComponent<ProjectionMappingCalibrator>();

            // ★ [수정] 변수명 변경 (mainCamera -> targetCamera)
            // ★ [수정] 들어갈 값 변경 (Camera.main -> projectCam)
            calibrator.targetCamera = projectCam;
            calibrator.meshFilter = mf;
            Debug.Log($"[MeshInitializer] Calibrator 설정 완료 (Target: {(projectCam ? projectCam.name : "없음")})");

            // 5. SaliencyMapVisualizer
            var visualizer = go.GetComponent<SaliencyMapVisualizer>();
            if (visualizer == null) visualizer = go.AddComponent<SaliencyMapVisualizer>();

            // 시각화(Heatmap)는 사용자가 보는 것이므로 MainCamera가 맞을 수도 있고, 
            // 프로젝터 기준이라면 projectCam일 수도 있습니다. 
            // 일단 기존 코드 존중하여 MainCamera 유지하되, 필요하면 projectCam으로 바꾸세요.
            visualizer.mainCamera = Camera.main;
            visualizer.meshFilter = mf;

            int mask = LayerMask.GetMask("Vertex In 3D"); // 혹은 사용중인 레이어 이름
            if (mask != 0) visualizer.visibilityLayerMask = mask;
        }

        Debug.Log("[MeshInitializer] 선택된 모든 Mesh 세팅 완료!");
    }
}
//using UnityEditor;
//using UnityEngine;

//public class MeshInitializer : MonoBehaviour
//{
//    [MenuItem("Tools/Setup Selected Meshes for Saliency Test")]
//    static void SetupSelectedMeshes()
//    {
//        foreach (GameObject go in Selection.gameObjects)
//        {
//            if (go == null) continue;

//            // Add MeshFilter if missing
//            MeshFilter mf = go.GetComponent<MeshFilter>();
//            if (mf == null)
//            {
//                mf = go.AddComponent<MeshFilter>();
//                Debug.Log($"[MeshInitializer] MeshFilter 추가됨: {go.name}");
//            }
//            else
//            {
//                mf = go.GetComponent<MeshFilter>();
//            }

//            // Add MeshRenderer if missing (사용자가 수동 설정하길 원함 → 자동 추가는 유지하되 설정은 안 건드림)
//            if (go.GetComponent<MeshRenderer>() == null)
//            {
//                go.AddComponent<MeshRenderer>();
//                Debug.Log($"[MeshInitializer] MeshRenderer 추가됨: {go.name}");
//            }

//            // Add MeshCollider if missing and set sharedMesh
//            MeshCollider collider = go.GetComponent<MeshCollider>();
//            if (collider == null)
//            {
//                collider = go.AddComponent<MeshCollider>();
//                Debug.Log($"[MeshInitializer] MeshCollider 추가됨: {go.name}");
//            }
//            collider.sharedMesh = mf.sharedMesh;

//            // Add ProjectionMappingCalibrator if missing
//            if (go.GetComponent<ProjectionMappingCalibrator>() == null)
//            {
//                var calibrator = go.AddComponent<ProjectionMappingCalibrator>();
//                calibrator.mainCamera = Camera.main;
//                calibrator.meshFilter = mf;
//                Debug.Log($"[MeshInitializer] ProjectionMappingCalibrator 추가 + 파라미터 설정됨: {go.name}");
//            }

//            // Add SaliencyMapVisualizer if missing
//            if (go.GetComponent<SaliencyMapVisualizer>() == null)
//            {
//                var visualizer = go.AddComponent<SaliencyMapVisualizer>();
//                visualizer.mainCamera = Camera.main;
//                visualizer.meshFilter = mf;

//                int mask = LayerMask.GetMask("Vertex In 3D");
//                if (mask == 0)
//                {
//                    Debug.LogWarning("[MeshInitializer] Layer 'Vertex In 3D'가 정의되어 있지 않습니다.");
//                }
//                else
//                {
//                    visualizer.visibilityLayerMask = mask;
//                }

//                Debug.Log($"[MeshInitializer] SaliencyMapVisualizer 추가 + 파라미터 설정됨: {go.name}");
//            }
//        }

//        Debug.Log("[MeshInitializer] 선택된 모든 mesh에 대한 구성 완료");
//    }
//}
