using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

// 대응점(3D 버텍스 <-> 프로젝터 2D 픽셀)으로 DLT를 풀어 프로젝터 카메라(projCam)에 적용한다.
//
// 좌표계는 변환 없이 Unity 그대로 쓴다.
//   - 3D : Unity 월드 좌표
//   - 2D : projCam 스크린 픽셀 좌표 (좌하단 원점, y 위쪽) = Camera.WorldToScreenPoint와 같은 좌표
// 이 좌표로 푼 P = λK[R | -RC]를 분해하면 R의 행이 곧 카메라의 right / up / forward이고,
// K(fx, fy, skew, cx, cy)는 projCam.projectionMatrix로 그대로 옮길 수 있다.
public class DLT_solve : MonoBehaviour
{

    #region --- DLL Imports ---
    [DllImport("DLT_Rezero.dll", EntryPoint = "DLT")]
    private static extern void DLT(double[] worldPoints, double[] imagePoints, int numPoints, double[] projectionMatrix);

    [DllImport("DLT_Rezero.dll", EntryPoint = "projectPoints")]
    private static extern void projectPoints(double[] worldPoints, double[] projectionMatrix, double[] rtMatrix, double[] resultPoints, float camPos);
    #endregion
    private const double InconsistencyWarnPixels = 3.0; // 실시간 재계산에서 이보다 크게 어긋나면 경고

    [Header("References")]
    public VertexClickTest vertexClickTest;      // 클릭 데이터
    public CreateSphereAtVertex createSphereAtVertex; // 전체 버텍스 정보 (focal length 보정에 필요하다면 사용)
    public Camera projCam;                       // 프로젝션 맵핑에 사용할 카메라

    // DLT 결과를 분해한 카메라 파라미터 (픽셀 단위 내부 파라미터 + 월드 기준 포즈)
    public struct CameraParams
    {
        public double fx, fy, skew, cx, cy;
        public Vector3 position;                 // 카메라 중심
        public Vector3 right, up, forward;       // R의 각 행

        public Quaternion Rotation => Quaternion.LookRotation(forward, up);
    }

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
        if (Input.GetKeyDown(KeyCode.F) && !HotkeyGuard.Blocked)
        {
            PerformDLT();
        }
    }

    /// <summary>
    /// 유효한 점들을 수집하여 DLT 계산을 수행하는 메인 함수.
    /// verbose = false는 마커를 옮길 때마다 자동으로 다시 푸는 경우로, 로그를 한 줄만 남긴다.
    /// </summary>
    public bool PerformDLT(bool verbose = true)
    {
        // 1. 선택된 점 수집.
        //    3D 좌표는 클릭 시점 값이 아니라 구의 현재 위치를 쓴다 (클릭 후 슬라이더로 메쉬를 움직였을 수 있음).
        var world = new List<Vector3>();
        var image = new List<Vector2>();
        var imageGT = new List<Vector2>();
        for (int i = 0; i < vertexClickTest.clickedObjects.Length; i++)
        {
            if (vertexClickTest.clickedObjects[i] == null) continue;
            world.Add(vertexClickTest.clickedObjects[i].transform.position);
            image.Add(vertexClickTest.verticesStruct[i].screenCoordinate);
            imageGT.Add(vertexClickTest.verticesStruct[i].screenCoordinateGT);
        }

        int pointCount = world.Count;

        // DLT는 최소 6개의 점이 필요함 (6개를 넘으면 전부 써서 최소제곱으로 푼다)
        if (pointCount < 6)
        {
            if (verbose) Debug.LogError($"[DLT Error] 점이 부족합니다. (현재: {pointCount}개 / 최소: 6개)");
            return false;
        }

        // 점들이 한 평면 위에 있으면 DLT 해가 하나로 정해지지 않는다.
        bool nearlyPlanar = PlanarityScore(world) < 1e-3;
        if (nearlyPlanar && verbose)
            Debug.LogWarning("[DLT] 선택한 점들이 거의 한 평면(또는 직선) 위에 있습니다. 결과가 매우 불안정할 수 있습니다.");

        if (verbose) Debug.Log($"[DLT Start] 점 {pointCount}개로 계산을 시작합니다...");

        // 2. DLT 계산 및 분해
        if (!TryDecompose(SolveDLT(world, image), world, out CameraParams cam))
        {
            Debug.LogError("[DLT Error] 투영 행렬을 카메라 파라미터로 분해하지 못했습니다.");
            return false;
        }

        // 3. projCam에 적용
        ApplyToCamera(cam, projCam, verbose);

        // 4. 결과 분석
        if (verbose)
        {
            Debug.Log("--- [Result Analysis] ---");
            ReportResult(cam, world, image, imageGT);
            Debug.Log("-------------------------");
        }
        else
        {
            ReprojectionError(world, image, out double rmse, out double max);
            Debug.Log($"[Live] 점 {pointCount}개로 다시 계산: 재투영 RMSE {rmse:F2}px, 최대 {max:F2}px{(nearlyPlanar ? " (점들이 거의 한 평면!)" : "")}");

            // 점이 7개 이상이면 서로 맞지 않는 마커가 있는지 전체 일관성으로만 알린다.
            // 어느 마커인지는 지목하지 않는다: 미지수가 11개라 점 10개 이하에서는 최소제곱이 오차를
            // 다른 점들에 나눠 떠넘겨서, 잘못 놓인 마커가 아닌 다른 마커가 가장 크게 나오는 경우가 많다.
            if (pointCount > 6 && max > InconsistencyWarnPixels)
                Debug.LogWarning($"[Live] 마커들이 서로 {max:F1}px까지 맞지 않습니다. 투영된 모양을 보고 어긋난 부분의 마커를 다시 맞추세요.");
        }

        // 마커 패치와 어긋남 표시를 새 카메라 기준으로 갱신
        vertexClickTest.OnCameraSolved();
        return true;
    }

    #region --- Math & Calibration Logic ---

    // Hartley 정규화를 거쳐 DLL의 DLT를 호출하고, 원래 좌표계의 3x4 투영 행렬 P를 돌려준다 (row-major 12개).
    // DLL은 P[2,3] = 1로 고정한 11-파라미터 최소제곱이라, 정규화 없이 넣으면 좌표 크기 차이 때문에 수치적으로 불안정하다.
    private static double[] SolveDLT(List<Vector3> world, List<Vector2> image)
    {
        int n = world.Count;

        // 월드 점: 중심을 원점으로 옮기고 원점까지 평균 거리가 sqrt(3)이 되도록
        D3 c3 = default;
        foreach (var p in world) c3 = c3 + new D3(p);
        c3 = c3 / n;
        double d3 = 0;
        foreach (var p in world) d3 += (new D3(p) - c3).Norm;
        double s3 = Math.Sqrt(3) / (d3 / n);

        // 이미지 점: 중심을 원점으로 옮기고 평균 거리가 sqrt(2)가 되도록
        double c2x = 0, c2y = 0;
        foreach (var q in image) { c2x += q.x; c2y += q.y; }
        c2x /= n; c2y /= n;
        double d2 = 0;
        foreach (var q in image) d2 += Math.Sqrt((q.x - c2x) * (q.x - c2x) + (q.y - c2y) * (q.y - c2y));
        double s2 = Math.Sqrt(2) / (d2 / n);

        double[] worldPoints = new double[n * 3];
        double[] imagePoints = new double[n * 2];
        for (int i = 0; i < n; i++)
        {
            D3 w = (new D3(world[i]) - c3) * s3;
            worldPoints[i * 3 + 0] = w.x;
            worldPoints[i * 3 + 1] = w.y;
            worldPoints[i * 3 + 2] = w.z;
            imagePoints[i * 2 + 0] = (image[i].x - c2x) * s2;
            imagePoints[i * 2 + 1] = (image[i].y - c2y) * s2;
        }

        double[] L = new double[11];
        DLT(worldPoints, imagePoints, n, L);
        double[,] Pn =
        {
            { L[0], L[1], L[2],  L[3] },
            { L[4], L[5], L[6],  L[7] },
            { L[8], L[9], L[10], 1.0 }
        };

        // 정규화 해제: P = T2^-1 * Pn * T3
        //   T3 = [s3*I | -s3*c3],  T2^-1 = [[1/s2, 0, c2x], [0, 1/s2, c2y], [0, 0, 1]]
        var A = new double[3, 4];
        for (int r = 0; r < 3; r++)
        {
            for (int j = 0; j < 3; j++) A[r, j] = s3 * Pn[r, j];
            A[r, 3] = Pn[r, 3] - s3 * (Pn[r, 0] * c3.x + Pn[r, 1] * c3.y + Pn[r, 2] * c3.z);
        }

        double[] P = new double[12];
        for (int j = 0; j < 4; j++)
        {
            P[0 + j] = A[0, j] / s2 + c2x * A[2, j];
            P[4 + j] = A[1, j] / s2 + c2y * A[2, j];
            P[8 + j] = A[2, j];
        }
        return P;
    }

    // P = λK[R | -RC]를 분해한다 (RQ 분해를 Gram-Schmidt로 풀어 쓴 것).
    //   K = [[fx, skew, cx], [0, fy, cy], [0, 0, 1]],  R의 행 = right, up, forward,  C = 카메라 중심
    private static bool TryDecompose(double[] P, List<Vector3> world, out CameraParams cam)
    {
        cam = default;

        D3 m1 = new D3(P[0], P[1], P[2]);
        D3 m2 = new D3(P[4], P[5], P[6]);
        D3 m3 = new D3(P[8], P[9], P[10]);
        D3 p4 = new D3(P[3], P[7], P[11]);

        // P의 부호는 임의다. 점들이 카메라 앞(깊이 > 0)에 오도록 부호를 맞춘다.
        double depthSum = 0;
        foreach (var X in world) depthSum += D3.Dot(m3, new D3(X)) + p4.z;
        if (depthSum < 0)
        {
            m1 = m1 * -1; m2 = m2 * -1; m3 = m3 * -1; p4 = p4 * -1;
        }

        double lambda = m3.Norm;
        if (lambda < 1e-12) return false;

        D3 r3 = m3 / lambda;

        double cy = D3.Dot(m2, r3) / lambda;
        D3 v2 = m2 / lambda - r3 * cy;
        double fy = v2.Norm;
        D3 r2 = v2 / fy;

        double cx = D3.Dot(m1, r3) / lambda;
        double skew = D3.Dot(m1, r2) / lambda;
        D3 v1 = m1 / lambda - r2 * skew - r3 * cx;
        double fx = v1.Norm;
        D3 r1 = v1 / fx;

        // 대응이 정상이면 (right, up, forward)는 Unity 카메라와 손잡이 방향이 같아 det(R) = +1이다.
        // -1이면 2D 좌표가 좌우로 뒤집혀 들어온 것. P를 그대로 재현하도록 fx를 음수로 둔다.
        if (D3.Dot(r1, D3.Cross(r2, r3)) < 0)
        {
            Debug.LogWarning("[DLT] 좌우가 뒤집힌 해가 나왔습니다. 마커와 버텍스의 짝이 맞는지 확인하세요.");
            r1 = r1 * -1;
            fx = -fx;
        }

        // 카메라 중심: M C = -p4.  M의 역행렬의 열은 행 벡터끼리의 외적 / det(M)
        double det = D3.Dot(m1, D3.Cross(m2, m3));
        if (Math.Abs(det) < 1e-12) return false;
        D3 C = (D3.Cross(m2, m3) * p4.x + D3.Cross(m3, m1) * p4.y + D3.Cross(m1, m2) * p4.z) * (-1.0 / det);

        cam = new CameraParams
        {
            fx = fx, fy = fy, skew = skew, cx = cx, cy = cy,
            position = C.ToVector3(),
            right = r1.ToVector3(),
            up = r2.ToVector3(),
            forward = r3.ToVector3()
        };
        return true;
    }

    private static void ApplyToCamera(CameraParams p, Camera cam, bool verbose)
    {
        cam.transform.SetPositionAndRotation(p.position, p.Rotation);

        // 내부 파라미터는 투영 행렬로 직접 넣는다.
        // (focalLength/lensShift로는 fy != fx와 skew를 표현할 수 없고, 예전 환산식은 Screen.width 기준이라
        //  프로젝터 해상도와도 맞지 않았음. 되돌리려면 cam.ResetProjectionMatrix())
        cam.projectionMatrix = BuildProjectionMatrix(p, cam.pixelWidth, cam.pixelHeight, cam.nearClipPlane, cam.farClipPlane);

        if (verbose) Debug.Log("[Camera Update] 카메라 파라미터가 적용되었습니다.");
    }

    // 픽셀 단위 K를 Unity(OpenGL 규약) 투영 행렬로 옮긴다. 뷰 공간에서 카메라는 -z를 바라본다.
    //   u = fx * x/z + skew * y/z + cx  (x, y, z = right, up, forward 성분)  ->  NDC x = 2u/W - 1
    public static Matrix4x4 BuildProjectionMatrix(CameraParams p, float width, float height, float near, float far)
    {
        Matrix4x4 m = Matrix4x4.zero;
        m.m00 = (float)(2.0 * p.fx / width);
        m.m01 = (float)(2.0 * p.skew / width);
        m.m02 = (float)(1.0 - 2.0 * p.cx / width);
        m.m11 = (float)(2.0 * p.fy / height);
        m.m12 = (float)(1.0 - 2.0 * p.cy / height);
        m.m22 = -(far + near) / (far - near);
        m.m23 = -2f * far * near / (far - near);
        m.m32 = -1f;
        return m;
    }

    private void ReportResult(CameraParams cam, List<Vector3> world, List<Vector2> image, List<Vector2> imageGT)
    {
        int n = world.Count;

        Debug.Log($"[Calibration Info] fx {cam.fx:F2}, fy {cam.fy:F2}, skew {cam.skew:F3}, 주점 ({cam.cx:F2}, {cam.cy:F2}) px  (화면 {projCam.pixelWidth}x{projCam.pixelHeight})");
        Debug.Log($"[Calibration Info] 위치 {cam.position}, 회전 {cam.Rotation.eulerAngles}");

        ReprojectionError(world, image, out double rmse, out double max);
        Debug.Log($"[Reprojection] RMSE {rmse:F3}px, 최대 {max:F3}px");

        // 마커 이동량: 클릭 당시 투영 위치(GT)에서 마커를 얼마나 옮겼는지
        double moved = 0;
        for (int i = 0; i < n; i++) moved += Vector2.Distance(image[i], imageGT[i]);
        Debug.Log($"[Residual] 마커 평균 이동량 {moved / n:F2}px");

        // 시뮬레이션 평가: 마커를 옮기지 않은 GT 2D 점으로 푼 카메라와 비교
        if (TryDecompose(SolveDLT(world, imageGT), world, out CameraParams gt))
        {
            float posErr = Vector3.Distance(cam.position, gt.position);
            float rotErr = Quaternion.Angle(cam.Rotation, gt.Rotation);
            Debug.Log($"[Sim] GT 대비 카메라 위치 오차 {posErr:F3}, 회전 오차 {rotErr:F3}°, fx 차이 {cam.fx - gt.fx:F2}px");
        }
    }

    // 재투영 오차: 적용된 projCam으로 3D 점을 다시 투영해 마커 위치와 비교한다.
    // (점이 6개면 식 12개에 미지수 11개라 거의 0으로 나온다. 점이 많을수록 의미 있는 값이 된다)
    private void ReprojectionError(List<Vector3> world, List<Vector2> image, out double rmse, out double max)
    {
        double sumSq = 0;
        max = 0;
        for (int i = 0; i < world.Count; i++)
        {
            Vector3 s = projCam.WorldToScreenPoint(world[i]);
            double d = Vector2.Distance(new Vector2(s.x, s.y), image[i]);
            sumSq += d * d;
            max = Math.Max(max, d);
        }
        rmse = Math.Sqrt(sumSq / world.Count);
    }

    // 점 분포의 공분산으로 평면성을 잰다. 0이면 완전히 한 평면(또는 직선), 고르게 퍼져 있으면 1에 가깝다.
    private static double PlanarityScore(List<Vector3> pts)
    {
        D3 c = default;
        foreach (var p in pts) c = c + new D3(p);
        c = c / pts.Count;

        double xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
        foreach (var p in pts)
        {
            D3 d = new D3(p) - c;
            xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
            yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
        }

        double det = xx * (yy * zz - yz * yz) - xy * (xy * zz - yz * xz) + xz * (xy * yz - yy * xz);
        double meanVar = (xx + yy + zz) / 3;
        if (meanVar <= 0) return 0;
        return det / (meanVar * meanVar * meanVar);
    }

    // double 정밀도 3D 벡터 (Vector3는 float라 분해 과정에서 정밀도가 부족함)
    private struct D3
    {
        public double x, y, z;

        public D3(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
        public D3(Vector3 v) : this(v.x, v.y, v.z) { }

        public static D3 operator +(D3 a, D3 b) => new D3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static D3 operator -(D3 a, D3 b) => new D3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static D3 operator *(D3 a, double s) => new D3(a.x * s, a.y * s, a.z * s);
        public static D3 operator /(D3 a, double s) => new D3(a.x / s, a.y / s, a.z / s);

        public static double Dot(D3 a, D3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static D3 Cross(D3 a, D3 b) => new D3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);

        public double Norm => Math.Sqrt(Dot(this, this));
        public Vector3 ToVector3() => new Vector3((float)x, (float)y, (float)z);
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
