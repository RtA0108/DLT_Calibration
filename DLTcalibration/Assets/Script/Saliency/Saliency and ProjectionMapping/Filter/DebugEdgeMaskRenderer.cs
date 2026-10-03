using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugEdgeMaskRenderer : MonoBehaviour
{
    public RenderTexture edgeMask;

    void OnGUI()
    {
        if (edgeMask == null)
        {
            GUI.Label(new Rect(10, 10, 300, 30), "edgeMask == null x");
            return;
        }
        GUI.DrawTexture(new Rect(Screen.width - 256, Screen.height - 256, 256, 256), edgeMask);
    }
}