using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class EdgeMaskRenderer : MonoBehaviour
{
    public Shader depthEdgeShader;
    public RenderTexture edgeMask;
    private Material edgeMaterial;
    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();

        if (depthEdgeShader == null)
        {
            Debug.LogError("[EdgeMaskRenderer] DepthEdgeShader가 연결되지 않았습니다.");
            return;
        }

        edgeMaterial = new Material(depthEdgeShader);

        // DepthTexture 활성화 필수
        cam.depthTextureMode |= DepthTextureMode.Depth;
        Debug.Log($"[EdgeMaskRenderer] depthTextureMode = {cam.depthTextureMode}");
        edgeMask = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
        edgeMask.name = "DepthEdgeMaskRT";

    }

    public void RenderDepthEdgeMask()
    {
        if (edgeMaterial == null || cam == null || edgeMask == null)
        {
            Debug.LogError("[EdgeMaskRenderer] 필수 구성요소가 null입니다.");
            return;
        }

        RenderTexture temp = RenderTexture.GetTemporary(Screen.width, Screen.height);
        cam.targetTexture = temp;
        cam.Render();
        cam.targetTexture = null;

        Graphics.Blit(temp, edgeMask, edgeMaterial);
        RenderTexture.ReleaseTemporary(temp);

        Texture2D tex = new Texture2D(edgeMask.width, edgeMask.height, TextureFormat.RGB24, false);
        RenderTexture.active = edgeMask;
        tex.ReadPixels(new Rect(0, 0, edgeMask.width, edgeMask.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        byte[] bytes = tex.EncodeToPNG();
        string path = Application.dataPath + "/DepthEdgeResult.png";
        System.IO.File.WriteAllBytes(path, bytes);
        Debug.Log("[EdgeMaskRenderer] edgeMask 저장 완료: " + path);
    }
    //public Camera sourceCamera;
    //public Shader edgeShader;
    //public RenderTexture edgeMask;

    //private Material edgeMaterial;

    //void Start()
    //{

    //    if (sourceCamera == null)
    //    {
    //        Debug.LogError("sourceCamera is null!");
    //    }

    //    if (edgeShader == null)
    //    {
    //        Debug.LogError("edgeShader is null!");
    //    }

    //    edgeMaterial = new Material(edgeShader);

    //    edgeMask = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
    //    edgeMask.name = "EdgeMaskRT";
    //    Debug.Log($"edgeMask 생성됨: {edgeMask.width}x{edgeMask.height}");
    //}

    //public void RenderEdgeMask()
    //{

    //    if (sourceCamera == null || edgeMaterial == null)
    //    {
    //        Debug.LogError("[EdgeMaskRenderer] sourceCamera 또는 edgeMaterial이 null입니다.");
    //        return;
    //    }

    //    // 1. 임시 텍스처 생성
    //    RenderTexture temp = RenderTexture.GetTemporary(Screen.width, Screen.height);

    //    // 2. 카메라가 temp에 그리기
    //    sourceCamera.targetTexture = temp;
    //    sourceCamera.Render();
    //    sourceCamera.targetTexture = null;

    //    // 3. edgeMask를 Blit 대상으로 지정하고 그리기
    //    RenderTexture.active = edgeMask;

    //    //  (선택) 확인용 Clear 색상  화면에서 보일지 체크
    //    GL.Clear(true, true, Color.magenta);

    //    // 4. 실제 Sobel 필터 적용해서 edgeMask에 출력
    //    Graphics.Blit(temp, edgeMask, edgeMaterial);

    //    // 5. 원래 렌더 타겟 복구
    //    RenderTexture.active = null;
    //    RenderTexture.ReleaseTemporary(temp);
    //}

}