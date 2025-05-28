using UnityEngine;

[RequireComponent(typeof(Camera))]
public class SilhouetteEdgeMaskRenderer : MonoBehaviour
{
    public Shader solidColorShader; // SilhouetteSolidColor.shader
    public Shader edgeDetectShader; // SilhouetteEdgeShader

    [HideInInspector] public RenderTexture silhouetteMask;
    [HideInInspector] public RenderTexture edgeMask;

    [Range(0.01f, 1.0f)] public float edgeThreshold = 0.1f;

    private Camera cam;
    private Material edgeMat;
    private Material solidMat;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.depthTextureMode = DepthTextureMode.None;

        // 준비
        if (solidColorShader == null)
            solidColorShader = Shader.Find("Hidden/SilhouetteSolidColor");
        if (edgeDetectShader == null)
            edgeDetectShader = Shader.Find("Hidden/SilhouetteEdgeShader");

        solidMat = new Material(solidColorShader);
        edgeMat = new Material(edgeDetectShader);

        edgeMat.SetFloat("_EdgeThreshold", edgeThreshold);
    }

    public void Render()
    {
        int w = Screen.width;
        int h = Screen.height;

        if (silhouetteMask == null || silhouetteMask.width != w || silhouetteMask.height != h)
        {
            silhouetteMask = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
            silhouetteMask.name = "SilhouetteMask";
        }

        if (edgeMask == null || edgeMask.width != w || edgeMask.height != h)
        {
            edgeMask = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
            edgeMask.name = "EdgeMask";
        }

        // 1. 실루엣 마스크 렌더링
        cam.targetTexture = silhouetteMask;
        cam.RenderWithShader(solidColorShader, "RenderType");
        cam.targetTexture = null;

        // 2. 에지 필터 적용
        Graphics.Blit(silhouetteMask, edgeMask, edgeMat);
    }

    public RenderTexture GetEdgeMask()
    {
        return edgeMask;
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
    public void SaveSilhouetteMaskToPNG(string filename = "SilhouetteMaskSnapshot.png")
    {
        if (silhouetteMask == null)
        {
            Debug.LogError("[SaveSilhouetteMaskToPNG] silhouetteMask가 비어 있습니다.");
            return;
        }

        // silhouetteMask 내용을 Texture2D로 복사
        RenderTexture.active = silhouetteMask;
        Texture2D tex = new Texture2D(silhouetteMask.width, silhouetteMask.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, silhouetteMask.width, silhouetteMask.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        // 바이트로 인코딩해서 저장
        byte[] bytes = tex.EncodeToPNG();
        string path = System.IO.Path.Combine(Application.dataPath, filename);
        System.IO.File.WriteAllBytes(path, bytes);
        Debug.Log($"[SaveSilhouetteMaskToPNG] silhouetteMask 저장됨: {path}");
    }
}
