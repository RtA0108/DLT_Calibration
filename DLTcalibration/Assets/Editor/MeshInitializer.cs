using UnityEditor;
using UnityEngine;

public class MeshInitializer : MonoBehaviour
{
    [MenuItem("Tools/Setup Selected Meshes for Saliency Test")]
    static void SetupSelectedMeshes()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go == null) continue;

            // Add MeshFilter if missing
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null)
            {
                mf = go.AddComponent<MeshFilter>();
                Debug.Log($"[MeshInitializer] MeshFilter 추가됨: {go.name}");
            }
            else
            {
                mf = go.GetComponent<MeshFilter>();
            }

            // Add MeshRenderer if missing (사용자가 수동 설정하길 원함 → 자동 추가는 유지하되 설정은 안 건드림)
            if (go.GetComponent<MeshRenderer>() == null)
            {
                go.AddComponent<MeshRenderer>();
                Debug.Log($"[MeshInitializer] MeshRenderer 추가됨: {go.name}");
            }

            // Add MeshCollider if missing and set sharedMesh
            MeshCollider collider = go.GetComponent<MeshCollider>();
            if (collider == null)
            {
                collider = go.AddComponent<MeshCollider>();
                Debug.Log($"[MeshInitializer] MeshCollider 추가됨: {go.name}");
            }
            collider.sharedMesh = mf.sharedMesh;

            // Add ProjectionMappingCalibrator if missing
            if (go.GetComponent<ProjectionMappingCalibrator>() == null)
            {
                var calibrator = go.AddComponent<ProjectionMappingCalibrator>();
                calibrator.mainCamera = Camera.main;
                calibrator.meshFilter = mf;
                Debug.Log($"[MeshInitializer] ProjectionMappingCalibrator 추가 + 파라미터 설정됨: {go.name}");
            }

            // Add SaliencyMapVisualizer if missing
            if (go.GetComponent<SaliencyMapVisualizer>() == null)
            {
                var visualizer = go.AddComponent<SaliencyMapVisualizer>();
                visualizer.mainCamera = Camera.main;
                visualizer.meshFilter = mf;

                int mask = LayerMask.GetMask("Vertex In 3D");
                if (mask == 0)
                {
                    Debug.LogWarning("[MeshInitializer] Layer 'Vertex In 3D'가 정의되어 있지 않습니다.");
                }
                else
                {
                    visualizer.visibilityLayerMask = mask;
                }

                Debug.Log($"[MeshInitializer] SaliencyMapVisualizer 추가 + 파라미터 설정됨: {go.name}");
            }
        }

        Debug.Log("[MeshInitializer] 선택된 모든 mesh에 대한 구성 완료");
    }
}
