using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class EdgeVertexFilter
{
    public static List<Vector3> FilterByEdgeMask(List<Vector3> visibleVertices, Camera cam, RenderTexture edgeMask, float pixelThreshold = 0.01f)
    {
        Texture2D edgeTex = new Texture2D(edgeMask.width, edgeMask.height, TextureFormat.RGB24, false);
        RenderTexture.active = edgeMask;
        edgeTex.ReadPixels(new Rect(0, 0, edgeMask.width, edgeMask.height), 0, 0);
        edgeTex.Apply();

        List<Vector3> result = new();
        foreach (var v in visibleVertices)
        {
            Vector3 screenPos = cam.WorldToScreenPoint(v);
            if (screenPos.z < 0) continue; // 뒤에 있는 것 제외

            int x = Mathf.RoundToInt(screenPos.x);
            int y = Mathf.RoundToInt(screenPos.y);

            if (x < 0 || y < 0 || x >= edgeTex.width || y >= edgeTex.height)
                continue;

            Color pixel = edgeTex.GetPixel(x, y);
            if (pixel.r < pixelThreshold)
                result.Add(v); // edge가 아니면 유지
        }

        return result;
    }
}