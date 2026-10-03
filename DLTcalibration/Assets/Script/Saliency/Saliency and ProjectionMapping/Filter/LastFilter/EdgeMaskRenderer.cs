using UnityEngine;

[RequireComponent(typeof(Camera))]
public class EdgeMaskRenderer : MonoBehaviour
{
    public Shader solidColorShader;      // 단색 메시용 셰이더
    public Shader edgeDetectShader;      // 소벨 필터 셰이더
    public RenderTexture edgeMask;       // 최종 마스크 결과 저장용

    private Camera cam;
    private Material edgeMaterial;
    private Camera renderCam;
    private RenderTexture colorRT;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.enabled = false; // 수동 렌더링 전용
    }

    public void RenderEdgeMask()
    {
        if (solidColorShader == null || edgeDetectShader == null)
        {
            Debug.LogError("[EdgeMaskRenderer] Shader가 지정되지 않았습니다.");
            return;
        }

        if (edgeMaterial == null)
            edgeMaterial = new Material(edgeDetectShader);

        // 1. 단색 메시 렌더링용 카메라 설정
        if (renderCam == null)
        {
            renderCam = new GameObject("RT_Camera").AddComponent<Camera>();
            renderCam.enabled = false;
            renderCam.CopyFrom(cam);
            renderCam.clearFlags = CameraClearFlags.SolidColor;
            renderCam.backgroundColor = Color.black;
        }

        // 2. RT 초기화
        if (edgeMask == null || edgeMask.width != Screen.width || edgeMask.height != Screen.height)
        {
            edgeMask?.Release();
            edgeMask = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
        }

        if (colorRT == null || colorRT.width != Screen.width || colorRT.height != Screen.height)
        {
            colorRT?.Release();
            colorRT = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
        }

        // 3. Render (Mesh만 단색으로)
        renderCam.targetTexture = colorRT;
        renderCam.RenderWithShader(solidColorShader, "RenderType");
        renderCam.targetTexture = null;

        // 4. Edge 추출
        Graphics.Blit(colorRT, edgeMask, edgeMaterial);
        Debug.Log("[EdgeMaskRenderer] Edge Mask 생성 완료");
    }
    public void SaveEdgeMaskToPNG(string filename = "EdgeMaskSnapshot.png")
    {
        if (edgeMask == null)
        {
            Debug.LogError("[SaveEdgeMaskToPNG] edgeMask가 비어 있습니다.");
            return;
        }

        // edgeMask 내용을 Texture2D로 복사
        RenderTexture.active = edgeMask;
        Texture2D tex = new Texture2D(edgeMask.width, edgeMask.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, edgeMask.width, edgeMask.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        // 바이트로 인코딩해서 저장
        byte[] bytes = tex.EncodeToPNG();
        string path = System.IO.Path.Combine(Application.dataPath, filename);
        System.IO.File.WriteAllBytes(path, bytes);
        Debug.Log($"[SaveEdgeMaskToPNG] edgeMask 저장됨: {path}");
    }
}