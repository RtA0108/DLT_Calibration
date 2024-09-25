using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
//using static UnityEditor.Searcher.SearcherWindow.Alignment;

public class DLT_solve : MonoBehaviour
{
    [DllImport("DLT_Rezero.dll", EntryPoint = "DLT")]
    // Start is called before the first frame update
    private static extern void DLT(double[] worldPoints, double[] imagePoints, int numPoints, double[] projectionMatrix);
    [DllImport("DLT_Rezero.dll", EntryPoint = "projectPoints")]
    private static extern void projectPoints(double[] worldPoints, double[] projectionMatrix, double[] rtMatrix, double[] resultPoints, float camPos);

    private GameObject LVManger;

    public VertexClickTest vertexClickTest;
    public CreateSphereAtVertex createSphereAtVertex;
    private int vertexCountDLT;
    private GameObject createdBox;
    
    private Camera projCam;
    
    //private GameObject somethingMesh;
    //private MeshFilter meshFilter;
    //private Mesh mesh;

    void Awake()
    {

        vertexCountDLT = createSphereAtVertex.vertexCount; //생성된 vertex 개수
        Debug.Log(vertexCountDLT);
        LVManger = GameObject.Find("LevelManager");
        projCam = GameObject.FindGameObjectWithTag("Project Camera").gameObject.GetComponent<Camera>();
        //somethingMesh = GameObject.FindGameObjectWithTag("Project Mesh"); //프로젝션 타겟 메시?
        //meshFilter = somethingMesh.GetComponent<MeshFilter>(); //왜 something 이라고 해놓고 project mesh를 사용했지?
        //mesh = meshFilter.mesh;
        
        //if (somethingMesh == null)
        //{
        //    Debug.LogError("MeshFilter not found on the GameObject");
        //}
        //Debug.Log("vertices" + string.Join(", ", mesh.vertices));
    }


    void Update()
    {
        int index = System.Array.IndexOf(vertexClickTest.clickedObjects, null);
        index = Math.Min(index, 6); //최대 6개를 유지하기 위함 (6개 이상인 경우 뭐가 우선으로 들어가는지 파악할 필요 O -> 어차피 수정하여 6개 초과해도 가능하게 만들 예정)
        float camPosY = projCam.pixelHeight;
        if (Input.GetKeyDown(KeyCode.F))
        {

            
            if (index > 5)
            {
                
                double[] worldPoints = new double[18];
                double[] imagePoints = new double[12];
                for (int i = 0; i < index; i++)
                {
                    //y를 -로 둔채로 계산하면 최종 계산되는 position에서 y가 -로 나옴 -> DLT에서 계산한 뒤로 unity로 넘겨줄때 y와 관련된 부분에 -를 해야할듯
                    worldPoints[i * 3] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.x;
                    worldPoints[i * 3 + 1] = -(double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.y;
                    worldPoints[i * 3 + 2] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.z;


                    imagePoints[i * 2] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].screenCoordinate.x;
                    imagePoints[i * 2 + 1] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].screenCoordinate.y;
                }
                Debug.Log("3D Matrix: " + string.Join(", ", worldPoints));
                Debug.Log("2D Matrix: " + string.Join(", ", imagePoints));

                //double[] imagePoints = vertexClickTest.imagePoints;
                int numPoints = index;
                //worldPoints.Length / 3; // Assuming each 3D point has X, Y, Z coordinates

                // Allocate memory for the projection matrix
                double[] projectionMatrix = new double[11];

                DLT(worldPoints, imagePoints, numPoints, projectionMatrix);

                Debug.Log("Projection Matrix: " + string.Join(", ", projectionMatrix));

                Matrix4x4 PMat = new Matrix4x4();
                PMat.SetRow(0, new Vector4((float)projectionMatrix[0], (float)projectionMatrix[1], (float)projectionMatrix[2], (float)projectionMatrix[3])); // Adjusted for Unity
                PMat.SetRow(1, new Vector4((float)projectionMatrix[4], (float)projectionMatrix[5], (float)projectionMatrix[6], (float)projectionMatrix[7]));
                PMat.SetRow(2, new Vector4((float)projectionMatrix[8], (float)projectionMatrix[9], (float)projectionMatrix[10], 1));
                PMat.SetRow(3, new Vector4(0, 0, 0, 1));

                MSE(PMat, worldPoints, imagePoints);
                cameraCalibrationWithDLT(projectionMatrix, projCam);

            }
        }
        //for (int i = 0; i < index; i++)
        //{
        //    Vector3 worldPos = vertexClickTest.clickedObjects[i].transform.position;
        //    Vector2 screenPos = projCam.WorldToScreenPoint(worldPos);
        //    screenPos.y = projCam.pixelHeight - screenPos.y;
        //    // Update the struct with the new screen position
        //    LVManger.GetComponent<VertexClickTest>().verticesStruct[i].screenCoordinate = screenPos;
        //}


    }
    
    private void MSE(Matrix4x4 P, double[] wolrdCoord, double[] imageCoord)
    {
        //double[] mseResult = new double[12];

        for(int i =0; i<6; i++)
        {
            Vector4 vertexPosition = new Vector4((float)wolrdCoord[3 * i], (float)wolrdCoord[3 * i + 1], (float)wolrdCoord[3 * i + 2], 1);
            Vector4 result = P * vertexPosition;
            //Debug.Log(result);

            double projectedX = result.x / result.z;
            double projectedY = result.y / result.z;

            double originalX = imageCoord[2*i];
            double originalY = imageCoord[2*i+1];
            
            double errorx = Math.Sqrt(Math.Pow(projectedX - originalX, 2));
            double errory = Math.Sqrt(Math.Pow(projectedY - originalY, 2));
            Debug.Log("vx"+i + " " + errorx + "vy" + i + " " + errory);
        }
    }

    private void cameraCalibrationWithDLT(double[] cameraParameter, Camera projCam)
    {
        //a = a1, a2, a3 | b = b1, b2, b3 | c = c1, c2, c3 | a4, b4는 이 계산에서 제외
        Vector3 a = new Vector3((float)cameraParameter[0], (float)cameraParameter[1], (float)cameraParameter[2]);
        Vector3 b = new Vector3((float)cameraParameter[4], (float)cameraParameter[5], (float)cameraParameter[6]);
        Vector3 c = new Vector3((float)cameraParameter[8], (float)cameraParameter[9], (float)cameraParameter[10]);

        double aTa = Vector3.Dot(a, a);
        double aTb = Vector3.Dot(a, b);
        double aTc = Vector3.Dot(a, c);
        double bTc = Vector3.Dot(b, c);
        double cTc = Vector3.Dot(c, c);

        //eq 6
        double x0 = aTc / cTc;
        double y0 = bTc / cTc;
        double c2 = (aTa / cTc) - Math.Pow((aTc / cTc), 2);
        double p = Math.Sqrt(cTc);
        

        //eq 7
        double d = ((aTb * cTc) - (aTc * bTc)) / ((aTa * cTc) - Math.Pow(aTc, 2));
        //스칼라 삼중곱...?
        double m = -Vector3.Dot(a, Vector3.Cross(b,c))/(Math.Pow(p,3)*c2);

        //eq 8
        double c1 = Math.Sqrt(c2);
        Matrix4x4 Rot = new Matrix4x4();
        Rot.SetRow(0, new Vector4((float)m, 0, -(float)(m * x0), 0));
        Rot.SetRow(1, new Vector4(-(float)d, 1, (float)((x0 * d) - y0), 0));
        Rot.SetRow(2, new Vector4(0, 0, -(float)(m * c1), 0));
        Rot.SetRow(3, new Vector4(0, 0, 0, 1));

        Matrix4x4 abc = new Matrix4x4();
        abc.SetColumn(0, new Vector4(a.x, a.y, a.z, 0));  
        abc.SetColumn(1, new Vector4(b.x, b.y, b.z, 0));  
        abc.SetColumn(2, new Vector4(c.x, c.y, c.z, 0));  
        abc.SetColumn(3, new Vector4(0, 0, 0, 1));
        

        Matrix4x4 R = new Matrix4x4();
        R =  Rot * abc.transpose;
        R = ScaleMatrix(R, (float)(1.0f / (p * m * c1)));
        
        R.m33 = 1.0f;
        //eq 9
        Vector4 spatialPosition = new Vector4();
        Vector4 ab1 = new Vector4(-(float)cameraParameter[3], -(float)cameraParameter[7], -1, 1);
        spatialPosition = abc.inverse.transpose * ab1;
        
        Vector3 translation = new Vector3(spatialPosition.x, spatialPosition.y, spatialPosition.z);
        Debug.Log(translation);
        projCam.fieldOfView = CalculateFieldOfView(c1, projCam.pixelHeight);
        projCam.lensShift = new Vector2((float)x0 / projCam.pixelWidth, (float)y0 / projCam.pixelHeight);

        Debug.Log("abcT" + abc.transpose);
        Debug.Log("rotation"+R);
        ApplyCameraExtrinsics(projCam, R, translation);



    }


    Matrix4x4 ScaleMatrix(Matrix4x4 matrix, float scalar)
    {
        Matrix4x4 result = new Matrix4x4();
        for (int i = 0; i < 4; i++)  // Row index
        {
            for (int j = 0; j < 4; j++)  // Column index
            {
                result[i, j] = matrix[i, j] * scalar;
            }
        }
        return result;
    }

    float CalculateFieldOfView(double f, float imageHeight)
    {
        float fov = 2.0f * Mathf.Atan(imageHeight / (float)(2.0f * f)) * Mathf.Rad2Deg;
        

        return fov;
    }



    void ApplyCameraExtrinsics(Camera camera, Matrix4x4 R, Vector3 T)
    {

        Vector3 position = ConvertTranslationOpenCVToUnity(T);
        position.z += 1000;
        camera.transform.position = position;
        //Quaternion rotation = R.rotation;
        Quaternion rotation = ConvertOpenCVToUnityQuaternion(R);
        camera.transform.rotation = rotation;

    }

    Vector3 ConvertTranslationOpenCVToUnity(Vector3 translation)
    {
        
        return new Vector3(translation.x, -translation.y, translation.z); // Flip Y and Z axes
    }
    Quaternion ConvertOpenCVToUnityQuaternion(Matrix4x4 R)
    {
        // OpenCV -> Unity 좌표계 변환 (y축과 z축 반전)
        Vector3 right = new Vector3(R.m00, -R.m10, R.m20);
        Vector3 up = new Vector3(R.m01, -R.m11, R.m21);
        Vector3 forward = new Vector3(R.m02, -R.m12, -R.m22); // z축 반전 -> 없으면 y -180도 회전

        // Unity의 좌표계에 맞춰 회전 행렬 재구성
        Matrix4x4 unityMatrix = new Matrix4x4();
        unityMatrix.SetColumn(0, new Vector4(right.x, right.y, right.z, 0));
        unityMatrix.SetColumn(1, new Vector4(up.x, up.y, up.z, 0));
        unityMatrix.SetColumn(2, new Vector4(forward.x, forward.y, forward.z, 0));
        unityMatrix.SetColumn(3, new Vector4(0, 0, 0, 1));

        // Unity의 Quaternion으로 변환
        return Quaternion.LookRotation(forward, up);
    }
}
