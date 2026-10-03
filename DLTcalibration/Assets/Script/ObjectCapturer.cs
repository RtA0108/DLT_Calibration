using UnityEngine;
using System.IO;

public class ObjectCapturer : MonoBehaviour
{
    public Camera captureCamera;
    public RenderTexture renderTexture;
    public GameObject targetObjectToCapture; // 캡처할 오브젝트를 Inspector에서 지정

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            CaptureObject();
        }
    }

    public void CaptureObject()
    {
        if (captureCamera == null || renderTexture == null || targetObjectToCapture == null)
        {
            Debug.LogError("필요한 컴포넌트(카메라, 렌더 텍스처, 타겟 오브젝트)가 할당되지 않았습니다.");
            return;
        }

        // *** 추가된 부분: 캡처 직전에 카메라를 오브젝트에 맞춥니다. ***
        CameraFitter.FitObjectToCamera(targetObjectToCapture, captureCamera, 0.1f); // 10%의 여백

        // (이하 기존 캡처 코드와 동일)
        captureCamera.Render();
        RenderTexture.active = renderTexture;

        Texture2D texture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
        texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        texture2D.Apply();

        RenderTexture.active = null;

        byte[] bytes = texture2D.EncodeToPNG();

        string filename = $"{targetObjectToCapture.name}_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
        File.WriteAllBytes(Path.Combine(Application.dataPath, "..", filename), bytes);

        Debug.Log($"오브젝트 캡처 완료: {filename}");
    }
}