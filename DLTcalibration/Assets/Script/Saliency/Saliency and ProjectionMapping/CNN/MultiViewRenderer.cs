using UnityEngine;
using System.IO;

public class MultiViewRenderer : MonoBehaviour
{
    public Camera renderCamera;
    public GameObject targetObject;
    public int resolution = 224;
    public string saveFolder = "Assets/CapturedViews";

    private readonly float[] azimuths = { 0, 30, 60, 90, 120, 150, 180, 210, 240, 270, 300, 330 };
    private readonly float[] elevations = { -30, 30 };

    void Start()
    {
        RenderAllViews();
    }

    void RenderAllViews()
    {
        Vector3 center = targetObject.GetComponent<Renderer>().bounds.center;
        float distance = 447f;

        int viewIdx = 0;
        foreach (float elev in elevations)
        {
            foreach (float azim in azimuths)
            {
                // 방향 계산
                Quaternion rot = Quaternion.Euler(elev, azim, 0);
                Vector3 camPos = center + rot * new Vector3(0, 0, -distance);

                renderCamera.transform.position = camPos;
                renderCamera.transform.LookAt(center);
                CaptureImage($"view_{viewIdx:D2}.png");
                viewIdx++;
            }
        }
    }

    void CaptureImage(string fileName)
    {
        RenderTexture rt = new RenderTexture(resolution, resolution, 24);
        renderCamera.targetTexture = rt;

        Texture2D image = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
        renderCamera.Render();
        RenderTexture.active = rt;
        image.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
        image.Apply();

        byte[] bytes = image.EncodeToPNG();
        Directory.CreateDirectory(saveFolder);
        File.WriteAllBytes(Path.Combine(saveFolder, fileName), bytes);

        renderCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);
        Destroy(image);
    }
}