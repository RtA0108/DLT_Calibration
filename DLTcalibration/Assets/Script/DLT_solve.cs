using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Transactions;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
//using static UnityEditor.Searcher.SearcherWindow.Alignment;

public class DLT_solve : MonoBehaviour
{

    #region --- DLL Imports ---
    [DllImport("DLT_Rezero.dll", EntryPoint = "DLT")]
    private static extern void DLT(double[] worldPoints, double[] imagePoints, int numPoints, double[] projectionMatrix);

    [DllImport("DLT_Rezero.dll", EntryPoint = "projectPoints")]
    private static extern void projectPoints(double[] worldPoints, double[] projectionMatrix, double[] rtMatrix, double[] resultPoints, float camPos);
    #endregion
    [Header("References")]
    public VertexClickTest vertexClickTest;      // 클릭 데이터
    public CreateSphereAtVertex createSphereAtVertex; // 전체 버텍스 정보 (focal length 보정에 필요하다면 사용)
    public Camera projCam;                       // 프로젝션 맵핑에 사용할 카메라

    private void Awake()
    {
        // 안전장치: 카메라가 연결 안 되어 있으면 태그로 찾기
        if (projCam == null)
        {
            GameObject camObj = GameObject.FindGameObjectWithTag("Project Camera");
            if (camObj != null) projCam = camObj.GetComponent<Camera>();
        }
    }

    void Update()
    {
        // 'F' 키를 누르면 DLT 계산 시작
        if (Input.GetKeyDown(KeyCode.F))
        {
            PerformDLT();
        }
    }

    /// <summary>
    /// 유효한 점들을 수집하여 DLT 계산을 수행하는 메인 함수
    /// </summary>
    private void PerformDLT()
    {
        // 1. 유효한(Null이 아닌) 점만 골라내기
        List<VertexClickTest.VertexStruct> validPoints = new List<VertexClickTest.VertexStruct>();

        for (int i = 0; i < vertexClickTest.clickedObjects.Length; i++)
        {
            // 오브젝트가 존재하고(null이 아니고), 구조체 데이터도 유효하다면 리스트에 추가
            if (vertexClickTest.clickedObjects[i] != null)
            {
                validPoints.Add(vertexClickTest.verticesStruct[i]);
            }
        }

        int pointCount = validPoints.Count;

        // DLT는 최소 6개의 점이 필요함
        if (pointCount < 6)
        {
            Debug.LogError($"[DLT Error] 점이 부족합니다. (현재: {pointCount}개 / 최소: 6개)");
            return;
        }

        // 최대 6개까지만 사용 (기존 로직 유지)
        // 만약 6개 이상도 허용하려면 이 줄을 지우거나 pointCount 상한을 늘리세요.
        int useCount = Math.Min(pointCount, 6);

        Debug.Log($"[DLT Start] 유효한 점 {useCount}개를 사용하여 계산을 시작합니다...");

        // 2. 데이터 배열 준비 (World -> Image 좌표)
        double[] worldPoints = new double[useCount * 3]; // (x,y,z) * N
        double[] imagePoints = new double[useCount * 2]; // (u,v) * N
        double[] imagePointsGT = new double[useCount * 2]; // Ground Truth

        for (int i = 0; i < useCount; i++)
        {
            var vStruct = validPoints[i];

            // *Note: DLT DLL이 Y축 반전을 요구하여 -를 붙임 (기존 로직 유지)
            worldPoints[i * 3 + 0] = vStruct.worldCoordinate.x;
            worldPoints[i * 3 + 1] = -vStruct.worldCoordinate.y;
            worldPoints[i * 3 + 2] = -vStruct.worldCoordinate.z;

            // Unity Screen 좌표계(좌하단 0,0) -> OpenCV 이미지 좌표계(좌상단 0,0) 변환
            imagePoints[i * 2 + 0] = vStruct.screenCoordinate.x;
            imagePoints[i * 2 + 1] = projCam.pixelHeight - vStruct.screenCoordinate.y;

            imagePointsGT[i * 2 + 0] = vStruct.screenCoordinateGT.x;
            imagePointsGT[i * 2 + 1] = projCam.pixelHeight - vStruct.screenCoordinateGT.y;
        }

        // 3. DLT 계산 수행 (DLL 호출)
        double[] projectionMatrix = new double[11];
        double[] projectionMatrixGT = new double[11];

        DLT(worldPoints, imagePoints, useCount, projectionMatrix);
        DLT(worldPoints, imagePointsGT, useCount, projectionMatrixGT);

        // 4. 파라미터 분해 및 적용
        // 내부/외부 파라미터를 계산하여 projCam에 적용합니다.
        CalculateAndApplyParameters(projectionMatrix, projCam);

        // 5. 결과 분석 (Residual & RMSE)
        Debug.Log("--- [Result Analysis] ---");
        CalculateResidual(imagePointsGT, imagePoints);
        CalculateRMSE(projectionMatrixGT, projectionMatrix);
        Debug.Log("-------------------------");
    }

    #region --- Math & Calibration Logic ---

    // DLT 매트릭스를 분해하여 Unity 카메라에 적용 (핵심 수학 로직)
    private void CalculateAndApplyParameters(double[] dltMatrix, Camera cam)
    {
        // Step 1: P 행렬의 각 행 벡터 추출
        Vector3 a = new Vector3((float)dltMatrix[0], (float)dltMatrix[1], (float)dltMatrix[2]); // 1행
        Vector3 b = new Vector3((float)dltMatrix[4], (float)dltMatrix[5], (float)dltMatrix[6]); // 2행
        Vector3 c = new Vector3((float)dltMatrix[8], (float)dltMatrix[9], (float)dltMatrix[10]); // 3행

        float cTc = Vector3.Dot(c, c);

        // Step 2: 주점(Principal Point) 계산
        double x0 = Vector3.Dot(a, c) / cTc;
        double y0 = Vector3.Dot(b, c) / cTc;

        // Step 3: 초점 거리(Focal Length) 계산
        // c^2 = (a^T a)/(c^T c) - (a^T c / c^T c)^2
        double cSquared = (Vector3.Dot(a, a) / cTc) - Math.Pow(Vector3.Dot(a, c) / cTc, 2);
        double focalLength = Math.Sqrt(cSquared); // Pixel 단위 Focal Length

        // Skew (d) 계산
        double num_d = (Vector3.Dot(a, b) * cTc) - (Vector3.Dot(a, c) * Vector3.Dot(b, c));
        double den_d = (Vector3.Dot(a, a) * cTc) - Math.Pow(Vector3.Dot(a, c), 2);
        double d = num_d / den_d;

        // m 계산 (Scale Factor)
        float p = Mathf.Sqrt(cTc);
        double det_abc = Vector3.Dot(a, Vector3.Cross(b, c)); // 행렬식
        double m = -det_abc / (Math.Pow(p, 3) * cSquared);

        // Step 5: 회전 행렬(Rotation Matrix) R 구성
        Matrix4x4 leftMatrix = new Matrix4x4();
        leftMatrix.m00 = (float)m;
        leftMatrix.m01 = 0;
        leftMatrix.m02 = (float)(-m * x0);

        leftMatrix.m10 = (float)(-d);
        leftMatrix.m11 = 1;
        leftMatrix.m12 = (float)(x0 * d - y0);

        leftMatrix.m20 = 0;
        leftMatrix.m21 = 0;
        leftMatrix.m22 = -(float)(m * focalLength);
        leftMatrix.m33 = 1.0f;

        // (abc)^T Matrix
        Matrix4x4 abc = new Matrix4x4();
        abc.SetColumn(0, new Vector4(a.x, a.y, a.z, 0));
        abc.SetColumn(1, new Vector4(b.x, b.y, b.z, 0));
        abc.SetColumn(2, new Vector4(c.x, c.y, c.z, 0));
        abc.m33 = 1.0f;

        // R = scale * leftMatrix * abc^T
        float scale = 1.0f / (p * (float)m * (float)focalLength);
        Matrix4x4 R = leftMatrix * abc.transpose;

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                R[i, j] *= scale;
            }
        }

        // Translation Vector (T) 계산
        Vector4 translationVector = new Vector4(-(float)dltMatrix[3], -(float)dltMatrix[7], -1, 1);
        Vector4 T = abc.inverse.transpose * translationVector;
        Vector3 translate = new Vector3(T.x, T.y, T.z);

        // R 전치 (Transpose)
        R = R.transpose;

        // --- [로그 출력] ---
        Debug.Log($"[Calibration Info] Focal Length: {focalLength}");
        Debug.Log($"[Calibration Info] Principal Point: ({x0}, {y0})");
        Debug.Log($"[Calibration Info] Translation: {translate}");

        // 최종 적용
        ApplyIntrinsicsAndExtrinsics(focalLength, d, x0, y0, R, translate, cam);
    }

    private void ApplyIntrinsicsAndExtrinsics(double focalLength, double skew, double principalX, double principalY, Matrix4x4 rotationMatrix, Vector3 translation, Camera cam)
    {
        // 1. 내부 파라미터 (Intrinsics) 적용
        cam.focalLength = (float)focalLength * (36.0f / Screen.width); // 35mm 센서 기준 변환 추정

        Vector2 lensShift = new Vector2(
            (float)(principalX / Screen.width) * 2.0f - 1.0f, // X축 정규화 (-1 ~ 1)
            (float)(principalY / Screen.height) * 2.0f - 1.0f // Y축 정규화 (-1 ~ 1)
        );
        cam.lensShift = lensShift;

        // 2. 외부 파라미터 (Extrinsics) 적용
        // OpenCV(우수계) -> Unity(좌수계) 좌표 변환
        Matrix4x4 unityRotation = AdjustRotationForUnity(rotationMatrix);
        Vector3 unityTranslation = AdjustForCoordinateSystem(translation);

        cam.transform.position = unityTranslation;
        cam.transform.rotation = QuaternionFromMatrix(unityRotation);

        Debug.Log("[Camera Update] 카메라 파라미터가 적용되었습니다.");
    }

    private Matrix4x4 AdjustRotationForUnity(Matrix4x4 openCVRotationMatrix)
    {
        Matrix4x4 m = new Matrix4x4();
        // Y축, Z축 반전 (좌수계 변환)
        m.m00 = openCVRotationMatrix.m00;
        m.m01 = -openCVRotationMatrix.m01;
        m.m02 = -openCVRotationMatrix.m02;

        m.m10 = -openCVRotationMatrix.m10;
        m.m11 = openCVRotationMatrix.m11;
        m.m12 = openCVRotationMatrix.m12;

        m.m20 = -openCVRotationMatrix.m20;
        m.m21 = openCVRotationMatrix.m21;
        m.m22 = openCVRotationMatrix.m22;
        m.m33 = 1.0f;
        return m;
    }

    private Vector3 AdjustForCoordinateSystem(Vector3 translation)
    {
        // Y, Z축 반전
        return new Vector3(translation.x, -translation.y, -translation.z);
    }

    private Quaternion QuaternionFromMatrix(Matrix4x4 m)
    {
        // 행렬 -> 쿼터니언 변환
        // (Unity 내부 로직 혹은 Mathf 사용 가능하나, 기존 로직 유지)
        Quaternion q = new Quaternion();
        q.w = Mathf.Sqrt(Mathf.Max(0, 1.0f + m.m00 + m.m11 + m.m22)) / 2.0f;
        float w4 = 4.0f * q.w;
        q.x = (m.m21 - m.m12) / w4;
        q.y = (m.m02 - m.m20) / w4;
        q.z = (m.m10 - m.m01) / w4;
        return q;
    }

    // 결과 검증용 (Residual)
    private void CalculateResidual(double[] GT, double[] current)
    {
        float totalDist = 0;
        for (int i = 0; i < GT.Length; i += 2)
        {
            Vector2 gtPos = new Vector2((float)GT[i], (float)GT[i + 1]);
            Vector2 curPos = new Vector2((float)current[i], (float)current[i + 1]);
            totalDist += Vector2.Distance(gtPos, curPos);
        }
        Debug.Log($"[Residual] Total 2D Error: {totalDist}");
    }

    // 결과 검증용 (RMSE)
    private void CalculateRMSE(double[] GT, double[] DLT)
    {
        double sumSq = 0;
        for (int i = 0; i < GT.Length; i++)
        {
            double diff = GT[i] - DLT[i];
            sumSq += diff * diff;
        }
        double rmse = Math.Sqrt(sumSq / GT.Length);
        Debug.Log($"[RMSE] Error: {rmse}");
    }

    #endregion
}
//    private GameObject LVManger;

//    public VertexClickTest vertexClickTest;
//    public CreateSphereAtVertex createSphereAtVertex;
//    private int vertexCountDLT;
//    private GameObject createdBox;

//    private Camera projCam;

//    void Awake()
//    {

//        vertexCountDLT = createSphereAtVertex.vertexCount; //생성된 vertex 개수
//        Debug.Log(vertexCountDLT);
//        LVManger = GameObject.Find("LevelManager");
//        projCam = GameObject.FindGameObjectWithTag("Project Camera").gameObject.GetComponent<Camera>();

//    }


//    void Update()
//    {
//        int index = System.Array.IndexOf(vertexClickTest.clickedObjects, null);
//        index = Math.Min(index, 6); //최대 6개를 유지하기 위함 (6개 이상인 경우 뭐가 우선으로 들어가는지 파악할 필요 O -> 어차피 수정하여 6개 초과해도 가능하게 만들 예정)
//        //float camPosY = projCam.pixelHeight;
//        if (Input.GetKeyDown(KeyCode.F))
//        {
//            Debug.Log("DO it");
//            if (index > 5)
//            {
//                //3D point, 2D point 저장 및 변환?
//                double[] worldPoints = new double[18];
//                double[] imagePoints = new double[12];

//                double[] imagePointsGT = new double[12];
//                for (int i = 0; i < index; i++)
//                {
//                    //y를 -로 둔채로 계산하면 최종 계산되는 position에서 y가 -로 나옴 -> DLT에서 계산한 뒤로 unity로 넘겨줄때 y와 관련된 부분에 -를 해야할듯
//                    worldPoints[i * 3] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.x;
//                    worldPoints[i * 3 + 1] = -(double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.y;
//                    worldPoints[i * 3 + 2] = -(double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.z;

//                    imagePoints[i * 2] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].screenCoordinate.x;
//                    imagePoints[i * 2 + 1] = projCam.pixelHeight - (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].screenCoordinate.y;

//                    imagePointsGT[i * 2] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].screenCoordinateGT.x;
//                    imagePointsGT[i * 2 + 1] = projCam.pixelHeight - (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].screenCoordinateGT.y;
//                }
//                //확인 절차
//                Debug.Log("3D Matrix: " + string.Join(", ", worldPoints));
//                Debug.Log("2D Matrix: " + string.Join(", ", imagePoints));
//                Debug.Log("2D Matrix GT: " + string.Join(", ", imagePointsGT));

//                //double[] imagePoints = vertexClickTest.imagePoints;
//                int numPoints = index;
//                //worldPoints.Length / 3; // Assuming each 3D point has X, Y, Z coordinates

//                // DLT 식에 사용되는 행렬 (3x4 행렬, (2,3)은 1로 고정? 아니면 12 배열로 만들어서 마지막 값으로 나누는걸로 변경?)
//                double[] projectionMatrix = new double[11];
//                DLT(worldPoints, imagePoints, numPoints, projectionMatrix);
//                double[] projectionMatrixGT = new double[11];
//                DLT(worldPoints, imagePointsGT, numPoints, projectionMatrixGT);
//                Debug.Log("Projection Matrix: " + string.Join(", ", projectionMatrix));
//                Debug.Log("Projection Matrix GT: " + string.Join(", ", projectionMatrixGT));
//                //Matrix4x4 PMat = new Matrix4x4();
//                //PMat.SetRow(0, new Vector4((float)projectionMatrix[0], (float)projectionMatrix[1], (float)projectionMatrix[2], (float)projectionMatrix[3])); // Adjusted for Unity
//                //PMat.SetRow(1, new Vector4((float)projectionMatrix[4], (float)projectionMatrix[5], (float)projectionMatrix[6], (float)projectionMatrix[7]));
//                //PMat.SetRow(2, new Vector4((float)projectionMatrix[8], (float)projectionMatrix[9], (float)projectionMatrix[10], 1));
//                //PMat.SetRow(3, new Vector4(0, 0, 0, 1));


//                //Debug.Log("Projection Matrix: " + string.Join(", ", PMat));
//                //Debug.Log("Projection Matrix Main Camera: " + Camera.main.projectionMatrix);
//                //Debug.Log("Projection Matrix Projection Camera: " + projCam.projectionMatrix);
//                //newCalibrationWithPM(PMat, projCam);

//                CalculateParameters(projectionMatrix, projCam);
//                Residual(imagePointsGT, imagePoints);
//                RMSE(projectionMatrixGT, projectionMatrix);
//                //MSE(PMat, worldPoints, imagePoints);
//                //Debug.Log("Projection Matrix: " + string.Join(", ", PMat));

//                Debug.Log("Projection Matrix Projection Camera: " + projCam.projectionMatrix);
//                Debug.Log("Real Camera Rotation: " + projCam.transform.rotation);


//            }
//            else
//            {
//                Debug.LogError("Not enough index");
//            }
//        }
//        // 지속적인 위치 업데이트를 위한 부분인데... 지금은 미사용 -> 10/16 얘를 다시 사용해야할 수도?
//        //for (int i = 0; i < index; i++)
//        //{
//        //    Vector3 worldPos = vertexClickTest.clickedObjects[i].transform.position;
//        //    Vector2 screenPos = projCam.WorldToScreenPoint(worldPos);
//        //    screenPos.y = projCam.pixelHeight - screenPos.y;
//        //    // Update the struct with the new screen position
//        //    LVManger.GetComponent<VertexClickTest>().verticesStruct[i].screenCoordinate = screenPos;
//        //}


//    }

//    private void Residual(double[] GT, double[] imageCoordinate)
//    {

//        float distanceResult = 0;
//        for (int i = 0; i < GT.Length; i += 2) {
//            Vector2 gt2D = new Vector2((float)GT[i], (float)GT[i + 1]);
//            Vector2 image2D = new Vector2((float)imageCoordinate[i], (float)imageCoordinate[i + 1]);
//            float distance = Vector2.Distance(gt2D, image2D);
//            distanceResult += distance;
//        }
//        Debug.Log("(Total 2D)Res: " + distanceResult);
//    }

//    private void RMSE(double[] GT, double[] DLT)
//    {
//        double rmse = 0;
//        //물론 둘다 DLT, GT는 ???의 DLT 파라미터, DLT는 projector에 적용된 DLT 파라미터
//        for (int i = 0; i < GT.Length; i++) 
//        {
//            double difference = GT[i] - DLT[i];
//            rmse += difference*difference;
//            Debug.Log("DLT Parameter " + (i+1) + " : " + difference);
//        }

//        double rmseResult = Math.Sqrt(rmse / GT.Length);
//        Debug.Log("(DLT)RMSE: " + rmseResult);

//    }


//    //새로 작성
//    private void CalculateParameters(double[] dltMatrix, Camera projCam)
//    {
//        // Step 1: Define vectors a, b, and c as the rows of the P matrix
//        // 기본적으로 벡터는 열벡터인가...? 열이에야 cTc 같은게 성립.
//        Vector4 a = new Vector4((float)dltMatrix[0], (float)dltMatrix[1], (float)dltMatrix[2], (float)dltMatrix[3]);  // First row (a1, a2, a3, a4)
//        Vector4 b = new Vector4((float)dltMatrix[4], (float)dltMatrix[5], (float)dltMatrix[6], (float)dltMatrix[7]);  // Second row (b1, b2, b3, b4)
//        Vector4 c = new Vector4((float)dltMatrix[8], (float)dltMatrix[9], (float)dltMatrix[10], 1);                   // Third row (c1, c2, c3, c4=1)

//        // Step 2: Calculate c^T * c (norm squared of vector c)
//        float cTc = new Vector3(c.x, c.y, c.z).sqrMagnitude;  // c^T * c using first 3 elements of c (c1, c2, c3)

//        // Step 3: Equation 6 - Principal point calculation
//        double x0 = Vector3.Dot(new Vector3(a.x, a.y, a.z), new Vector3(c.x, c.y, c.z)) / cTc;  // x0 = (a^T c) / (c^T c)
//        double y0 = Vector3.Dot(new Vector3(b.x, b.y, b.z), new Vector3(c.x, c.y, c.z)) / cTc;  // y0 = (b^T c) / (c^T c)

//        // Step 4: Equation 7 - Calculate c^2, d (skew), and m
//        // c^2 = (a^T a) / (c^T c) - (a^T c / c^T c)^2
//        double cSquared = (Vector3.Dot(new Vector3(a.x, a.y, a.z), new Vector3(a.x, a.y, a.z)) / cTc) - Mathf.Pow((float)Vector3.Dot(new Vector3(a.x, a.y, a.z), new Vector3(c.x, c.y, c.z)) / cTc, 2);
//        // focalLength c
//        double focalLength = Mathf.Sqrt((float)cSquared); //픽셀 단위일 가능성 농후 (그래서 값이 상당히 큼)
//        Debug.Log("Focal Length: "+focalLength);
//        // d = ((a^T b) * (c^T c) - (a^T c) * (b^T c)) / ((a^T a)(c^T c) - (a^T c)^2)
//        double numerator_d = (Vector3.Dot(new Vector3(a.x, a.y, a.z), new Vector3(b.x, b.y, b.z)) * cTc) - (Vector3.Dot(new Vector3(a.x, a.y, a.z), new Vector3(c.x, c.y, c.z)) * Vector3.Dot(new Vector3(b.x, b.y, b.z), new Vector3(c.x, c.y, c.z)));
//        double denominator_d = (Vector3.Dot(new Vector3(a.x, a.y, a.z), new Vector3(a.x, a.y, a.z)) * cTc) - Mathf.Pow(Vector3.Dot(new Vector3(a.x, a.y, a.z), new Vector3(c.x, c.y, c.z)), 2);
//        double d = numerator_d / denominator_d;

//        // m = -det(abc) / (p^3 * c^2)
//        float p = Mathf.Sqrt(cTc);  // p = sqrt(c^T * c)
//        //double det_abc = Vector3.Dot(new Vector3(a.x, a.y, a.z), Vector3.Cross(new Vector3(b.x, b.y, b.z), new Vector3(c.x, c.y, c.z)));  // Determinant of (abc) -> abc를 행벡터로 구성한 경우
//        double det_abc = Vector3.Dot(new Vector3(a.x, b.x, c.x), Vector3.Cross(new Vector3(a.y, b.y, c.y), new Vector3(a.z, b.z, c.z)));  // Determinant of (abc) -> abc를 열벡터로 구성한 경우
//        double m = -det_abc / (Mathf.Pow(p, 3) * cSquared);

//        // Step 5: Equation 8 - Build the rotation matrix R
//        Matrix4x4 R = new Matrix4x4();

//        // First part of R: the left-side matrix in Equation (8)
//        Matrix4x4 leftMatrix = new Matrix4x4();
//        leftMatrix.m00 = (float)m;               // m
//        leftMatrix.m01 = 0;                      // 0
//        leftMatrix.m02 = (float)(-m * x0);       // -mx0

//        leftMatrix.m10 = (float)(-d);            // -d
//        leftMatrix.m11 = 1;                      // 1
//        leftMatrix.m12 = (float)(x0 * d - y0);   // x0 * d - y0

//        leftMatrix.m20 = 0;                      // 0
//        leftMatrix.m21 = 0;                      // 0
//        leftMatrix.m22 = -(float)(m * focalLength);            // -m
//        leftMatrix.m33 = 1.0f;                   // Homogeneous coordinate

//        // Calculate R as: (1 / (p * m * f)) * (leftMatrix) * (abc)^T
//        float scale = 1.0f / (p * (float)m * (float)focalLength);  // The scale factor

//        // Step 6: Equation 9 - Calculate the translation vector
//        // (abc)^-T * (-a4, -b4, -1)
//        Matrix4x4 abc = new Matrix4x4();

//        // Fill abc with a, b, c row vectors
//        abc.SetColumn(0, new Vector4(a.x, a.y, a.z, 0));  // a1, a2, a3
//        abc.SetColumn(1, new Vector4(b.x, b.y, b.z, 0));  // b1, b2, b3
//        abc.SetColumn(2, new Vector4(c.x, c.y, c.z, 0));  // c1, c2, c3
//        abc.m33 = 1.0f;  // Homogeneous coordinate

//        // Vector (-a4, -b4, -1)
//        Vector4 translationVector = new Vector4(-(float)a.w, -(float)b.w, -1, 1);



//        // Compute the translation as T = (abc)^-T * (-a4, -b4, -1)
//        Vector4 T = abc.inverse.transpose* translationVector;

//        Vector3 translate = new Vector3(T.x, T.y, T.z);
//        //분명 결과값은 제대로 나오는 것으로 보이나, 적용하는 과정에서 이상하게 적용되는 것 같음. T는 분명 기존 이동값과 동일하게 나오는데 projCam의 translate이 결과적으로 달라짐.(회전땜에 발생하는 현상일지도?)
//        R = leftMatrix * abc.transpose;
//        for (int i = 0; i < 3; i++)
//        {
//            for (int j = 0; j < 3; j++)
//            {
//                R[i, j] *= scale;
//            }
//        }
//        //R의 경우 수치가 거의 유사하나 부호가 다름. 이를 변경만 잘 시키면 되지 않을까?
//        Quaternion cameraRotation = Camera.main.transform.rotation;
//        Matrix4x4 rotationMatrix = MatrixFromQuaternion(cameraRotation);
//        R = R.transpose;
//        // Output the calculated intrinsic and extrinsic parameters
//        //Debug.Log($"Principal Point: x0 = {x0}, y0 = {y0}");
//        //Debug.Log($"Skew: d = {d}");
//        //Debug.Log($"Rotation Matrix: \n{R}");
//        //Debug.Log($"Translation Vector: T = {T}");
//        Debug.Log("Translate result: " + T);
//        Debug.Log("Rotation Matrix: " + R);
//        Debug.Log("Rotation Camera Matrix" + rotationMatrix);
//        Debug.Log("rotation" + cameraRotation);
//        ApplyIntrinsicsAndExtrinsics(focalLength, d, x0, y0, R, translate, projCam);

//    }

//    private Matrix4x4 MatrixFromQuaternion(Quaternion q)
//    {
//        Matrix4x4 m = new Matrix4x4();

//        float qx2 = q.x * q.x;
//        float qy2 = q.y * q.y;
//        float qz2 = q.z * q.z;
//        float qw2 = q.w * q.w;

//        float xy = q.x * q.y;
//        float xz = q.x * q.z;
//        float yz = q.y * q.z;
//        float wx = q.w * q.x;
//        float wy = q.w * q.y;
//        float wz = q.w * q.z;

//        m.m00 = 1 - 2 * (qy2 + qz2);
//        m.m01 = 2 * (xy - wz);
//        m.m02 = 2 * (xz + wy);
//        m.m03 = 0;

//        m.m10 = 2 * (xy + wz);
//        m.m11 = 1 - 2 * (qx2 + qz2);
//        m.m12 = 2 * (yz - wx);
//        m.m13 = 0;

//        m.m20 = 2 * (xz - wy);
//        m.m21 = 2 * (yz + wx);
//        m.m22 = 1 - 2 * (qx2 + qy2);
//        m.m23 = 0;

//        m.m30 = 0;
//        m.m31 = 0;
//        m.m32 = 0;
//        m.m33 = 1;

//        return m;
//    }

//    public void ApplyIntrinsicsAndExtrinsics(double focalLength, double skew, double principalX, double principalY, Matrix4x4 rotationMatrix, Vector3 translation, Camera projCam)
//    {
//        // Step 1: Apply intrinsics to the Unity camera's projection matrix
//        //ApplyIntrinsics(focalLength, skew, principalX, principalY, projCam);
//        ApplyIntrinsicsPhysic(focalLength, skew, principalX, principalY, projCam);
//        // Step 2: Apply extrinsics (rotation and translation) to the Unity camera's transform
//        ApplyExtrinsics(rotationMatrix, translation, projCam);
//    }

//    // Apply intrinsic parameters to Camera.projectionMatrix in Unity
//    private void ApplyIntrinsics(double focalLength, double skew, double principalX, double principalY, Camera projCam)
//    {
//        float near = 0.3f;
//        float far = 1000f;
//        // Compute frustum boundaries using the intrinsic parameters
//        float left = (float)((principalX - Screen.width) * near / focalLength);
//        float right = (float)(principalX * near / focalLength);

//        float bottom = (float)((principalY - Screen.height) * near / focalLength);
//        float top = (float)(principalY * near / focalLength);

//        // Create a projection matrix
//        Matrix4x4 projectionMatrix = new Matrix4x4();

//        // First row
//        projectionMatrix.m00 = (2.0f * near) / (right - left);  // 2n / (r-l)
//        projectionMatrix.m01 = (float)(skew * near / focalLength);  // Apply skew (s), usually 0
//        projectionMatrix.m02 = (right + left) / (right - left);  // (r+l) / (r-l)
//        projectionMatrix.m03 = 0.0f;

//        // Second row
//        projectionMatrix.m10 = 0.0f;
//        projectionMatrix.m11 = (2.0f * near) / (top - bottom);  // 2n / (t-b)
//        projectionMatrix.m12 = (top + bottom) / (top - bottom);  // (t+b) / (t-b)
//        projectionMatrix.m13 = 0.0f;

//        // Third row
//        projectionMatrix.m20 = 0.0f;
//        projectionMatrix.m21 = 0.0f;
//        projectionMatrix.m22 = -(far + near) / (far - near);  // -(f+n) / (f-n)
//        projectionMatrix.m23 = -(2.0f * far * near) / (far - near);  // -(2fn) / (f-n)

//        // Fourth row
//        projectionMatrix.m30 = 0.0f;
//        projectionMatrix.m31 = 0.0f;
//        projectionMatrix.m32 = -1.0f;
//        projectionMatrix.m33 = 0.0f;

//        Debug.Log("ProjectionCam intrinsics." + projCam.projectionMatrix.ToString());
//        // Apply the calculated projection matrix to the main camera
//        projCam.projectionMatrix = projectionMatrix;

//        Debug.Log("Applied camera intrinsics." + projCam.projectionMatrix);
//    }

//    private void ApplyIntrinsicsPhysic(double focalLength, double skew, double principalX, double principalY, Camera projCam)
//    {
//        projCam.focalLength = (float)focalLength * (36.0f / Screen.width);
//        Vector2 lensShift = new Vector2(
//            (float)(principalX / Screen.width) * 2.0f - 1.0f, // X축으로 정규화
//            (float)(principalY / Screen.height) * 2.0f - 1.0f  // Y축으로 정규화
//        );
//        projCam.lensShift = lensShift;

//    }
//    // Apply extrinsic parameters (rotation and translation) to Unity's camera
//    private void ApplyExtrinsics(Matrix4x4 rotationMatrix, Vector3 translation, Camera projCam)
//    {

//        Matrix4x4 R = AdjustRotationForUnity(rotationMatrix);

//        // Convert the rotation matrix into a Quaternion for Unity
//        Quaternion rotation = QuaternionFromMatrix(R);

//        // Adjust for the difference between OpenCV's right-handed system and Unity's left-handed system
//        translation = AdjustForCoordinateSystem(translation);

//        // Apply the translation and rotation to the Unity camera's transform
//        projCam.transform.position = translation;
//        projCam.transform.rotation = rotation;

//        Debug.Log("Apply rotation: " + projCam.transform.rotation);
//        Debug.Log("Applied camera extrinsics.");
//    }

//    private Matrix4x4 AdjustRotationForUnity(Matrix4x4 openCVRotationMatrix)
//    {
//        Matrix4x4 adjustedMatrix = new Matrix4x4();

//        // Invert Y and Z axes for the OpenCV-to-Unity conversion
//        adjustedMatrix.m00 = openCVRotationMatrix.m00;
//        adjustedMatrix.m01 = -openCVRotationMatrix.m01; // Invert Y
//        adjustedMatrix.m02 = -openCVRotationMatrix.m02; // Invert Z

//        adjustedMatrix.m10 = -openCVRotationMatrix.m10; // Invert Y
//        adjustedMatrix.m11 = openCVRotationMatrix.m11;
//        adjustedMatrix.m12 = openCVRotationMatrix.m12; // Invert Z

//        adjustedMatrix.m20 = -openCVRotationMatrix.m20; // Invert Y
//        adjustedMatrix.m21 = openCVRotationMatrix.m21; // Invert Z
//        adjustedMatrix.m22 = openCVRotationMatrix.m22;

//        adjustedMatrix.m33 = 1.0f; // Homogeneous coordinate for a 4x4 matrix
//        Debug.Log("New Rotation Matrix: "+ adjustedMatrix);
//        return adjustedMatrix;
//    }

//    // Adjust the translation vector for OpenCV to Unity coordinate system conversion
//    private Vector3 AdjustForCoordinateSystem(Vector3 translation)
//    {
//        // OpenCV is right-handed, Unity is left-handed: invert Y and Z
//        translation.y = -translation.y;
//        translation.z = -translation.z;
//        return translation;

//    }

//    // Helper function to convert a 4x4 rotation matrix into a Quaternion in Unity
//    private Quaternion QuaternionFromMatrix(Matrix4x4 m)
//    {
//        Quaternion q = new Quaternion();
//        q.w = Mathf.Sqrt(1.0f + m.m00 + m.m11 + m.m22) / 2.0f;
//        float w4 = 4.0f * q.w;
//        q.x = (m.m21 - m.m12) / w4;
//        q.y = (m.m02 - m.m20) / w4;
//        q.z = (m.m10 - m.m01) / w4;
//        Debug.Log("New rotation: " + q);
//        return q;
//    }
//}
