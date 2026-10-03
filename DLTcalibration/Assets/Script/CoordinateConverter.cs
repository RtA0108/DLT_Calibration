using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CoordinateConverter
{
    // 예: Z-up (Blender) → Y-up (Unity)
    public static Vector3 ZUpToYUp(Vector3 v)
    {
        return new Vector3(v.x, v.z, -v.y);
    }

    // 예: OpenCV 좌표계 → Unity
    public static Vector3 OpenCVToUnity(Vector3 v)
    {
        return new Vector3(v.x, v.z, -v.y);  // 보통 OpenCV는 Z가 up인 경우 많음
    }

    // Unity → Blender (Z-up)
    public static Vector3 UnityToZUp(Vector3 v)
    {
        return new Vector3(v.x, -v.z, v.y);
    }

    // 필요한 만큼 추가 가능
}