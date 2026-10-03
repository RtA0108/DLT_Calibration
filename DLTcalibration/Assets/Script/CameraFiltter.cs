using UnityEngine;

public static class CameraFitter
{
    public static void FitObjectToCamera(GameObject targetObject, Camera camera, float padding = 0.1f)
    {
        if (targetObject == null || camera == null)
        {
            Debug.LogError("타겟 오브젝트 또는 카메라가 없습니다.");
            return;
        }

        Bounds bounds = GetBounds(targetObject);

        // --- 원근(Perspective) 카메라 로직 추가 ---
        if (!camera.orthographic)
        {
            // 화면의 세로와 가로 중 어느 쪽을 기준으로 채울지 결정
            float screenRatio = (float)Screen.width / (float)Screen.height;
            float targetRatio = bounds.size.x / bounds.size.y;

            float distance;
            if (screenRatio > targetRatio)
            {
                // 화면이 오브젝트보다 가로로 넓은 경우 (세로 기준)
                distance = bounds.size.y / 2 / Mathf.Tan(camera.fieldOfView / 2 * Mathf.Deg2Rad);
            }
            else
            {
                // 화면이 오브젝트보다 세로로 길거나 같은 경우 (가로 기준)
                float fovRad = camera.fieldOfView / 2 * Mathf.Deg2Rad;
                distance = bounds.size.x / 2 / Mathf.Tan(fovRad) / camera.aspect;
            }

            // 계산된 거리와 패딩을 적용하여 카메라 위치 설정
            camera.transform.position = bounds.center - camera.transform.forward * (distance * (1 + padding));
        }
        // --- 직교(Orthographic) 카메라 로직은 기존과 동일 ---
        else
        {
            float objectSize = Mathf.Max(bounds.size.x, bounds.size.y);
            float cameraSize = objectSize / 2f;

            // 화면 비율을 고려
            if (camera.aspect < 1f) // 세로가 더 긴 화면
            {
                camera.orthographicSize = cameraSize / camera.aspect * (1f + padding);
            }
            else // 가로가 더 길거나 같은 화면
            {
                camera.orthographicSize = cameraSize * (1f + padding);
            }

            // 카메라 위치를 오브젝트의 중심으로 이동
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, camera.transform.position.z);
        }
    }

    private static Bounds GetBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(target.transform.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }
}