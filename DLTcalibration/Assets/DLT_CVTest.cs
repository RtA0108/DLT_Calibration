using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

public class DLT_CVTest : MonoBehaviour
{

    [DllImport("opencv_world480d")]

   private static extern void cvSVDecomp(System.IntPtr A, System.IntPtr W, System.IntPtr U, System.IntPtr Vt, int flags);

    void Start()
    {
        // 예제로 사용할 6개 이상의 3D-2D 포인트 대응 데이터 (필요에 따라 조정)
        float[] objectPoints = new float[] {
            0f, -141.421f, 100f,
            141.421f, 0f, 100f,
            0f, 141.421f, 100f,
            -141.421f, 0, 100f,
            0f, -141.421f, -100f,
            -141.421f, 0f, -100f
        };

        float[] imagePoints = new float[] {
            428.493f, 206.005f,
            603f, 157.877f,
            777.507f, 206.005f,
            603f, 293.569f,
            461.961f,395.33f,
            603f, 519.068f
        };
 
        // 행렬 A, B, X의 크기 지정
        int rowsA = objectPoints.Length/2;
        int colsA = 11;
        int rowsW = 1;
        int colsW = colsA;
        int rowsU = rowsA;
        int colsU = rowsA;
        int rowsVt = colsA;
        int colsVt = colsA;

        // 행렬 A, B, X를 System.IntPtr로 변환
        System.IntPtr A = Marshal.AllocHGlobal(rowsA * colsA * sizeof(double));
        System.IntPtr W = Marshal.AllocHGlobal(rowsW * colsW * sizeof(double));
        System.IntPtr U = Marshal.AllocHGlobal(rowsU * colsU * sizeof(double));
        System.IntPtr Vt = Marshal.AllocHGlobal(rowsVt * colsVt * sizeof(double));

        // // 데이터 복사
        Marshal.Copy(objectPoints, 0, A, objectPoints.Length);

        // cvSVDecomp 함수 호출
        cvSVDecomp(A, W, U, Vt, 0);

        // 결과 확인
        double[] singularValues = new double[colsW];
        double[] uMatrix = new double[rowsU * colsU];
        double[] vtMatrix = new double[rowsVt * colsVt];

        Marshal.Copy(W, singularValues, 0, colsW);
        Marshal.Copy(U, uMatrix, 0, rowsU * colsU);
        Marshal.Copy(Vt, vtMatrix, 0, rowsVt * colsVt);

        // 결과 출력
        Debug.Log("Singular Values: " + string.Join(", ", singularValues));
        Debug.Log("U Matrix: " + string.Join(", ", uMatrix));
        Debug.Log("Vt Matrix: " + string.Join(", ", vtMatrix));
 
        // 메모리 해제
        Marshal.FreeHGlobal(A);
        Marshal.FreeHGlobal(W);
        Marshal.FreeHGlobal(U);
        Marshal.FreeHGlobal(Vt);
    }
}
