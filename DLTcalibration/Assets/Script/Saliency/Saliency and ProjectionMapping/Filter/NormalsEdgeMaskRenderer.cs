using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class NormalsEdgeMaskRenderer : MonoBehaviour
{
    [Header("연결된 카메라 (예: ProjectCamera)")]
    public Camera sourceCamera; // 메시를 렌더링하는 카메라

    [Header("실루엣 마스크 결과")]
    public RenderTexture edgeMask;

    [Header("사용할 Shader (Hidden/NormalsEdgeMask)")]
    public Shader edgeShader;

    private Material edgeMaterial;

    void Start()
    {
        if (sourceCamera == null)
        {
            sourceCamera = GetComponent<Camera>();
            Debug.Log("[NormalsEdgeMaskRenderer] sourceCamera가 설정되지 않아 본인 카메라로 대체합니다.");
        }

        if (edgeShader == null)
        {
            edgeShader = Shader.Find("Hidden/NormalsEdgeMask");
        }

        if (edgeShader == null)
        {
            Debug.LogError("[NormalsEdgeMaskRenderer] Shader를 찾을 수 없습니다. 경로 확인 필요.");
            return;
        }

        edgeMaterial = new Material(edgeShader);

        edgeMask = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
        edgeMask.name = "NormalsEdgeMask";
        edgeMask.Create();
    }

    public void RenderEdgeMask()
    {
        if (edgeMaterial == null || sourceCamera == null || edgeMask == null)
        {
            Debug.LogError("[NormalsEdgeMaskRenderer] 필수 구성 요소가 누락되었습니다.");
            return;
        }

        RenderTexture temp = RenderTexture.GetTemporary(Screen.width, Screen.height);
        sourceCamera.targetTexture = temp;
        sourceCamera.Render();
        sourceCamera.targetTexture = null;

        Graphics.Blit(temp, edgeMask, edgeMaterial);

        RenderTexture.ReleaseTemporary(temp);
        Debug.Log("[NormalsEdgeMaskRenderer] edgeMask 렌더링 완료됨");
    }

    public Texture2D CaptureEdgeMaskAsTexture2D()
    {
        Texture2D tex = new Texture2D(edgeMask.width, edgeMask.height, TextureFormat.RGB24, false);
        RenderTexture.active = edgeMask;
        tex.ReadPixels(new Rect(0, 0, edgeMask.width, edgeMask.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        return tex;
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
