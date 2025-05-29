using UnityEngine;
using System.IO;

[RequireComponent(typeof(Camera))]
public class SilhouetteEdgeMaskRenderer : MonoBehaviour
{
    public Shader solidColorShader; // SilhouetteSolidColor.shader
    public Shader edgeDetectShader; // SilhouetteEdgeShader
    public MeshFilter meshFilter;   // 타겟 메쉬

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

        // Shader가 비어있다면 기본 할당
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

        // RenderTexture 생성 및 크기 체크
        if (silhouetteMask == null || silhouetteMask.width != w || silhouetteMask.height != h)
        {
            if (silhouetteMask != null) silhouetteMask.Release();
            silhouetteMask = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "SilhouetteMask" };
        }

        if (edgeMask == null || edgeMask.width != w || edgeMask.height != h)
        {
            if (edgeMask != null) edgeMask.Release();
            edgeMask = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "EdgeMask" };
        }

        // ---------- DrawMesh 방식으로 직접 렌더링 ----------
        var oldRT = RenderTexture.active;
        RenderTexture.active = silhouetteMask;
        GL.Clear(true, true, Color.black);

        GL.PushMatrix();
        Matrix4x4 vp = cam.projectionMatrix * cam.worldToCameraMatrix;
        GL.LoadProjectionMatrix(vp);
        //GL.LoadProjectionMatrix(cam.projectionMatrix);
        solidMat.SetPass(0);
        Graphics.DrawMeshNow(meshFilter.sharedMesh, meshFilter.transform.localToWorldMatrix);
        GL.PopMatrix();

        RenderTexture.active = oldRT;

        // ---------- Edge Shader 적용 ----------
        Graphics.Blit(silhouetteMask, edgeMask, edgeMat);
    }

    public RenderTexture GetEdgeMask() => edgeMask;
    public RenderTexture GetSilhouetteMask() => silhouetteMask;

    public void SaveRenderTextureToPNG(RenderTexture rt, string filename)
    {
        if (rt == null)
        {
            Debug.LogError($"[SaveRenderTextureToPNG] {filename} 저장 실패: RenderTexture가 null입니다.");
            return;
        }

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        byte[] bytes = tex.EncodeToPNG();
        string path = Path.Combine(Application.dataPath, filename);
        File.WriteAllBytes(path, bytes);
        Debug.Log($"[SaveRenderTextureToPNG] 저장 완료: {path}");
    }

    public void SaveEdgeMaskToPNG(string filename = "EdgeMaskSnapshot.png")
    {
        SaveRenderTextureToPNG(edgeMask, filename);
    }

    public void SaveSilhouetteMaskToPNG(string filename = "SilhouetteMaskSnapshot.png")
    {
        SaveRenderTextureToPNG(silhouetteMask, filename);
    }
}
