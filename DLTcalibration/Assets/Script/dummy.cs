using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
//using static UnityEditor.Searcher.SearcherWindow.Alignment;

public class dummy : MonoBehaviour
{
    //DLT_solve.cs 사본
    [DllImport("DLT_Rezero.dll", EntryPoint = "DLT")]
    // Start is called before the first frame update
    private static extern void DLT(double[] worldPoints, double[] imagePoints, int numPoints, double[] projectionMatrix, double[] rtMatrix, double[] KMatrix);
    [DllImport("DLT_Rezero.dll", EntryPoint = "projectPoints")]
    private static extern void projectPoints(double[] worldPoints, double[] projectionMatrix, double[] rtMatrix, double[] resultPoints, float camPos);

    private GameObject LVManger;

    public VertexClickTest vertexClickTest;
    public CreateSphereAtVertex createSphereAtVertex;
    private int vertexCountDLT;
    private GameObject createdBox;
    private GameObject somethingMesh;
    //public GameObject somethingMesh;
    private Camera projCam;
    //private bool flag = false;
    private MeshFilter meshFilter;
    private Mesh mesh;

    void Awake()
    {

        vertexCountDLT = createSphereAtVertex.vertexCount;
        Debug.Log(vertexCountDLT);
        LVManger = GameObject.Find("LevelManager");
        projCam = GameObject.FindGameObjectWithTag("Project Camera").gameObject.GetComponent<Camera>();
        somethingMesh = GameObject.FindGameObjectWithTag("Project Mesh");
        meshFilter = somethingMesh.GetComponent<MeshFilter>();
        mesh = meshFilter.mesh;

        if (somethingMesh == null)
        {
            Debug.LogError("MeshFilter not found on the GameObject");
        }
        Debug.Log("vertices" + string.Join(", ", mesh.vertices));
    }


    void Update()
    {
        int index = System.Array.IndexOf(vertexClickTest.clickedObjects, null);

        float camPosY = projCam.pixelHeight;
        if (Input.GetKeyDown(KeyCode.F))
        {


            if (index > 5)
            {

                double[] worldPoints = new double[18];
                for (int i = 0; i < index; i++)
                {
                    Vector3 unityWorldPoint = LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate;
                    //Vector3 opencvWorldPoint = new Vector3(unityWorldPoint.x, -unityWorldPoint.y, -unityWorldPoint.z);
                    //y-up to z-up -> (X, Y, Z) to (X, -Z, Y)
                    Vector3 opencvWorldPoint = new Vector3(unityWorldPoint.x, -unityWorldPoint.z, unityWorldPoint.y);
                    worldPoints[i * 3] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.x;
                    worldPoints[i * 3 + 1] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.y;
                    worldPoints[i * 3 + 2] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.z;
                }
                Debug.Log("3D Matrix: " + string.Join(", ", worldPoints));

                double[] imagePoints = new double[12];
                for (int i = 0; i < index; i++)
                {
                    imagePoints[i * 2] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].screenCoordinate.x;
                    imagePoints[i * 2 + 1] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].screenCoordinate.y;
                }

                Debug.Log("2D Matrix: " + string.Join(", ", imagePoints));
                //double[] imagePoints = vertexClickTest.imagePoints;
                int numPoints = index;
                //worldPoints.Length / 3; // Assuming each 3D point has X, Y, Z coordinates

                // Allocate memory for the projection matrix
                double[] projectionMatrix = new double[12];
                double[] projectedPoints = new double[12];
                double[] rtMatrix = new double[12];
                double[] KMatrix = new double[9];
                // Call the DLT function from the DLL
                DLT(worldPoints, imagePoints, numPoints, projectionMatrix, rtMatrix, KMatrix);
                projectPoints(worldPoints, projectionMatrix, rtMatrix, projectedPoints, camPosY);
                Debug.Log("Projection Matrix: " + string.Join(", ", projectionMatrix));
                Debug.Log("Projection Point Matrix: " + string.Join(", ", projectedPoints));
                Matrix4x4 projectionMat = new Matrix4x4();
                projectionMat.SetColumn(0, new Vector4((float)projectionMatrix[0], (float)projectionMatrix[4], (float)projectionMatrix[8], 0)); // Adjusted for Unity
                projectionMat.SetColumn(1, new Vector4((float)projectionMatrix[1], (float)projectionMatrix[5], (float)projectionMatrix[9], 0));
                projectionMat.SetColumn(2, new Vector4((float)projectionMatrix[2], (float)projectionMatrix[6], (float)projectionMatrix[10], -1));
                projectionMat.SetColumn(3, new Vector4((float)projectionMatrix[3], (float)projectionMatrix[7], (float)projectionMatrix[11], 0));

                Matrix4x4 KMat = new Matrix4x4();
                KMat.SetRow(0, new Vector4((float)KMatrix[0], (float)KMatrix[1], (float)KMatrix[2], 0)); // Adjusted for Unity
                KMat.SetRow(1, new Vector4((float)KMatrix[3], (float)KMatrix[4], (float)KMatrix[5], 0));
                KMat.SetRow(2, new Vector4((float)KMatrix[6], (float)KMatrix[7], (float)KMatrix[8], 0));
                KMat.SetRow(3, new Vector4(0, 0, 0, 1));

                Matrix4x4 rtMatrixUnity = new Matrix4x4();
                rtMatrixUnity.SetColumn(0, new Vector4((float)rtMatrix[0], (float)rtMatrix[4], (float)rtMatrix[8], 0));
                rtMatrixUnity.SetColumn(1, new Vector4((float)rtMatrix[1], (float)rtMatrix[5], (float)rtMatrix[9], 0));
                rtMatrixUnity.SetColumn(2, new Vector4((float)rtMatrix[2], (float)rtMatrix[6], (float)rtMatrix[10], 0));
                rtMatrixUnity.SetColumn(3, new Vector4((float)rtMatrix[3], (float)rtMatrix[7], (float)rtMatrix[11], 1));

                projCam.fieldOfView = CalculateFieldOfView(KMat, camPosY);
                projCam.aspect = CalculateAspectRatio(KMat);
                ApplyCameraExtrinsics(projCam, rtMatrixUnity);
            }
        }
        for (int i = 0; i < index; i++)
        {
            Vector3 worldPos = vertexClickTest.clickedObjects[i].transform.position;
            Vector2 screenPos = projCam.WorldToScreenPoint(worldPos);
            screenPos.y = projCam.pixelHeight - screenPos.y;
            // Update the struct with the new screen position
            LVManger.GetComponent<VertexClickTest>().verticesStruct[i].screenCoordinate = screenPos;
        }

        //if (Input.GetKeyDown(KeyCode.F))
        //{

        //    int index = System.Array.IndexOf(vertexClickTest.clickedObjects, null);
        //    if (index > 5)
        //    {

        //        double[] worldPoints = new double[18];
        //        for (int i = 0; i < index; i++)
        //        {
        //            Vector3 unityWorldPoint = LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate;
        //            //Vector3 opencvWorldPoint = new Vector3(unityWorldPoint.x, -unityWorldPoint.y, -unityWorldPoint.z);
        //            //y-up to z-up -> (X, Y, Z) to (X, -Z, Y)
        //            Vector3 opencvWorldPoint = new Vector3(unityWorldPoint.x, -unityWorldPoint.z, unityWorldPoint.y);
        //            worldPoints[i * 3] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.x;
        //            worldPoints[i * 3 + 1] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.y;
        //            worldPoints[i * 3 + 2] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.z;
        //        }
        //        Debug.Log("3D Matrix: " + string.Join(", ", worldPoints));

        //        double[] imagePoints = new double[12];
        //        for (int i = 0; i < index; i++)
        //        {
        //            imagePoints[i * 2] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].screenCoordinate.x;
        //            imagePoints[i * 2 + 1] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].screenCoordinate.y;
        //        }

        //        Debug.Log("2D Matrix: " + string.Join(", ", imagePoints));
        //        //double[] imagePoints = vertexClickTest.imagePoints;
        //        int numPoints = index;
        //        //worldPoints.Length / 3; // Assuming each 3D point has X, Y, Z coordinates

        //        // Allocate memory for the projection matrix
        //        double[] projectionMatrix = new double[12];
        //        double[] projectedPoints = new double[12];
        //        double[] rtMatrix = new double[12];
        //        // Call the DLT function from the DLL
        //        DLT(worldPoints, imagePoints, numPoints, projectionMatrix, rtMatrix);
        //        projectPoints(worldPoints, projectionMatrix, rtMatrix, projectedPoints, camPosY);
        //        Debug.Log("Projection Matrix: " + string.Join(", ", projectionMatrix));
        //        Debug.Log("Projection Point Matrix: " + string.Join(", ", projectedPoints));
        //        Matrix4x4 projectionMat = new Matrix4x4();

        //        projectionMat.SetColumn(0, new Vector4((float)projectionMatrix[0], (float)projectionMatrix[4], (float)projectionMatrix[8], 0)); // Adjusted for Unity
        //        projectionMat.SetColumn(1, new Vector4((float)projectionMatrix[1], (float)projectionMatrix[5], (float)projectionMatrix[9], 0));
        //        projectionMat.SetColumn(2, new Vector4((float)projectionMatrix[2], (float)projectionMatrix[6], (float)projectionMatrix[10], 0));
        //        projectionMat.SetColumn(3, new Vector4((float)projectionMatrix[3], (float)projectionMatrix[7], (float)projectionMatrix[11], 1));
        //        Matrix4x4 adjustedMatrix = ConvertToUnityMatrixFormat(projectionMat);
        //        TransformMesh(mesh, adjustedMatrix);
        //        AdjustMeshAttributes(mesh);

        //        CalculateReprojectionError();

        //    }
        //    else
        //    {

        //        Debug.Log(index);
        //    }
        //}



        //if (!flag)
        //{
        //    if (Input.GetKeyDown(KeyCode.C))
        //    {

        //        int index = System.Array.IndexOf(vertexClickTest.clickedObjects, null);
        //        if (index > 5)
        //        {
        //            flag = true;
        //            Debug.Log(index);
        //            // Example usage with 6 point correspondences
        //            double[] worldPoints = new double[18];
        //            for (int i = 0; i < index; i++)
        //            {
        //                Vector3 unityWorldPoint = LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate;
        //                //Vector3 opencvWorldPoint = new Vector3(unityWorldPoint.x, -unityWorldPoint.y, -unityWorldPoint.z);
        //                //y-up to z-up -> (X, Y, Z) to (X, -Z, Y)
        //                Vector3 opencvWorldPoint = new Vector3(unityWorldPoint.x, -unityWorldPoint.z, unityWorldPoint.y);
        //                worldPoints[i * 3] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.x;
        //                worldPoints[i * 3 + 1] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.y;
        //                worldPoints[i * 3 + 2] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.z;
        //            }
        //            Debug.Log("3D Matrix: " + string.Join(", ", worldPoints));

        //            double[] imagePoints = new double[12];
        //            for (int i = 0; i < index; i++)
        //            {
        //                imagePoints[i * 2] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].screenCoordinate.x;
        //                imagePoints[i * 2 + 1] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].screenCoordinate.y;
        //            }

        //            Debug.Log("2D Matrix: " + string.Join(", ", imagePoints));
        //            //double[] imagePoints = vertexClickTest.imagePoints;
        //            int numPoints = index;
        //            //worldPoints.Length / 3; // Assuming each 3D point has X, Y, Z coordinates

        //            // Allocate memory for the projection matrix
        //            double[] projectionMatrix = new double[12];
        //            double[] projectedPoints = new double[12];
        //            double[] rtMatrix = new double[12];
        //            // Call the DLT function from the DLL
        //            DLT(worldPoints, imagePoints, numPoints, projectionMatrix, rtMatrix);
        //            projectPoints(worldPoints, projectionMatrix, rtMatrix, projectedPoints, camPosY);
        //            // Print the projection matrix
        //            Debug.Log("Projection Matrix: " + string.Join(", ", projectionMatrix));
        //            Debug.Log("Projection Point Matrix: " + string.Join(", ", projectedPoints));

        //            //RT Matrix
        //            // Print the RT matrix
        //            Debug.Log("RT Matrix: " + string.Join(", ", rtMatrix));

        //            // Convert RT matrix from OpenCV back to Unity coordinates
        //            Matrix4x4 rtMatrixUnity = new Matrix4x4();
        //            //rtMatrixUnity.SetColumn(0, new Vector4((float)rtMatrix[0], -(float)rtMatrix[1], -(float)rtMatrix[2], 0));
        //            //rtMatrixUnity.SetColumn(1, new Vector4((float)rtMatrix[4], -(float)rtMatrix[5], -(float)rtMatrix[6], 0));
        //            //rtMatrixUnity.SetColumn(2, new Vector4((float)rtMatrix[8], -(float)rtMatrix[9], -(float)rtMatrix[10], 0));
        //            //rtMatrixUnity.SetColumn(3, new Vector4((float)rtMatrix[3], -(float)rtMatrix[7], -(float)rtMatrix[11], 1));
        //            //z-up to y-up -> (X, Y, Z) to (X, Z, -Y)
        //            rtMatrixUnity.SetColumn(0, new Vector4((float)rtMatrix[0], (float)rtMatrix[8], -(float)rtMatrix[4], 0)); // Adjusted for Unity
        //            rtMatrixUnity.SetColumn(1, new Vector4((float)rtMatrix[1], (float)rtMatrix[9], -(float)rtMatrix[5], 0));
        //            rtMatrixUnity.SetColumn(2, new Vector4((float)rtMatrix[2], (float)rtMatrix[10], -(float)rtMatrix[6], 0)); // �̰� �ƴѵ� -> �̰� �´�
        //            rtMatrixUnity.SetColumn(3, new Vector4(0, 0, 0, 1));

        //            rtMatrixUnity *= Matrix4x4.Rotate(projCam.transform.rotation);
        //            Quaternion rotation = Quaternion.LookRotation(rtMatrixUnity.GetColumn(2), -rtMatrixUnity.GetColumn(1)); // Adjust rotation
        //            Vector3 position = new Vector3((float)rtMatrix[3], (float)rtMatrix[7], (float)rtMatrix[11]);
        //            position.z += 1000.0f;

        //            createdBox = Instantiate(somethingMesh, position, rotation);
        //            //createdBox.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        //        }
        //        else
        //        {

        //            Debug.Log(index);
        //        }
        //    }
        //}
        //else
        //{
        //    int index = System.Array.IndexOf(vertexClickTest.clickedObjects, null);
        //    double[] worldPoints = new double[18];
        //    for (int i = 0; i < index; i++)
        //    {
        //        Vector3 unityWorldPoint = LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate;
        //        //Vector3 opencvWorldPoint = new Vector3(unityWorldPoint.x, -unityWorldPoint.y, -unityWorldPoint.z);
        //        //y-up to z-up -> (X, Y, Z) to (X, -Z, Y)
        //        Vector3 opencvWorldPoint = new Vector3(unityWorldPoint.x, -unityWorldPoint.z, unityWorldPoint.y);
        //        worldPoints[i * 3] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.x;
        //        worldPoints[i * 3 + 1] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.y;
        //        worldPoints[i * 3 + 2] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.z;
        //    }
        //    Debug.Log("3D Matrix: " + string.Join(", ", worldPoints));

        //    double[] imagePoints = new double[12];
        //    for (int i = 0; i < index; i++)
        //    {
        //        imagePoints[i * 2] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].screenCoordinate.x;
        //        imagePoints[i * 2 + 1] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].screenCoordinate.y;
        //    }
        //    // Convert Unity coordinates to OpenCV coordinates
        //    //int screenHeight = Screen.height;
        //    //for (int i = 0; i < index; i++)
        //    //{
        //    //    imagePoints[i * 2 + 1] = screenHeight - imagePoints[i * 2 + 1];
        //    //}

        //    Debug.Log("2D Matrix: " + string.Join(", ", imagePoints));
        //    //double[] imagePoints = vertexClickTest.imagePoints;
        //    int numPoints = index;
        //    //worldPoints.Length / 3; // Assuming each 3D point has X, Y, Z coordinates

        //    // Allocate memory for the projection matrix
        //    double[] projectionMatrix = new double[12];
        //    double[] projectedPoints = new double[12];
        //    double[] rtMatrix = new double[12];
        //    // Call the DLT function from the DLL
        //    DLT(worldPoints, imagePoints, numPoints, projectionMatrix, rtMatrix);
        //    projectPoints(worldPoints, projectionMatrix, rtMatrix, projectedPoints, camPosY);
        //    Debug.Log("Projection Matrix: " + string.Join(", ", projectionMatrix));
        //    Debug.Log("Projection Point Matrix: " + string.Join(", ", projectedPoints));

        //    Debug.Log("RT Matrix: " + string.Join(", ", rtMatrix));
        //    //Matrix4x4 camRotationMatrix = Matrix4x4.Rotate(projCam.transform.rotation);
        //    //Matrix4x4 rtMatrixMat = new Matrix4x4();
        //    //rtMatrixMat.SetColumn(0, new Vector4((float)rtMatrix[0], (float)rtMatrix[1], (float)rtMatrix[2], 0));
        //    //rtMatrixMat.SetColumn(1, new Vector4((float)rtMatrix[4], (float)rtMatrix[5], (float)rtMatrix[6], 0));
        //    //rtMatrixMat.SetColumn(2, new Vector4((float)rtMatrix[8], (float)rtMatrix[9], (float)rtMatrix[10], 0));
        //    //rtMatrixMat.SetColumn(3, new Vector4(0, 0, 0, 1));
        //    ////create new box with RT Matrix
        //    ////Matrix4x4 combinedRotationMatrix = rtMatrixMat * camRotationMatrix;
        //    //Matrix4x4 combinedRotationMatrix = rtMatrixMat * Matrix4x4.Rotate(projCam.transform.rotation);
        //    ////Quaternion rotation = rtMatrixMat.rotation * Quaternion.Euler(0, 180, 0);
        //    ////Quaternion rotationResult = rotation * projCam.transform.rotation;

        //    //Quaternion combinedRotation = combinedRotationMatrix.rotation * Quaternion.Euler(0, 180, 0) * somethingMesh.transform.rotation;
        //    //Vector3 DLTTransformPosition = new Vector3((float)rtMatrix[3], (float)rtMatrix[7], 1000.0f + (float)rtMatrix[11]);
        //    Matrix4x4 rtMatrixUnity = new Matrix4x4();
        //    //rtMatrixUnity.SetColumn(0, new Vector4((float)rtMatrix[0], (float)rtMatrix[1], (float)rtMatrix[2], 0));
        //    //rtMatrixUnity.SetColumn(1, new Vector4((float)rtMatrix[4], (float)rtMatrix[5], (float)rtMatrix[6], 0));
        //    //rtMatrixUnity.SetColumn(2, new Vector4((float)rtMatrix[8], (float)rtMatrix[9], (float)rtMatrix[10], 0));
        //    //rtMatrixUnity.SetColumn(3, new Vector4((float)rtMatrix[3], -(float)rtMatrix[7], -(float)rtMatrix[11], 1));
        //    //z-up to y-up -> (X, Y, Z) to (X, Z, -Y)
        //    //rtMatrixUnity.SetColumn(0, new Vector4((float)rtMatrix[0], (float)rtMatrix[8], -(float)rtMatrix[4], 0)); // Adjusted for Unity
        //    //rtMatrixUnity.SetColumn(1, new Vector4((float)rtMatrix[1], (float)rtMatrix[9], -(float)rtMatrix[5], 0));
        //    //rtMatrixUnity.SetColumn(2, new Vector4((float)rtMatrix[2], (float)rtMatrix[10], -(float)rtMatrix[6], 0)); // �̰� �ƴѵ� -> �̰� �´µ�?
        //    //rtMatrixUnity.SetColumn(0, new Vector4((float)rtMatrix[0], (float)rtMatrix[2], -(float)rtMatrix[1], 0)); // Adjusted for Unity
        //    //rtMatrixUnity.SetColumn(1, new Vector4((float)rtMatrix[4], (float)rtMatrix[6], -(float)rtMatrix[5], 0));
        //    //rtMatrixUnity.SetColumn(2, new Vector4((float)rtMatrix[8], (float)rtMatrix[10], -(float)rtMatrix[9], 0));
        //    rtMatrixUnity.SetColumn(0, new Vector4((float)projectionMatrix[0], (float)projectionMatrix[4], (float)projectionMatrix[8], 0));
        //    rtMatrixUnity.SetColumn(1, new Vector4((float)projectionMatrix[1], (float)projectionMatrix[5], (float)projectionMatrix[9], 0));
        //    rtMatrixUnity.SetColumn(2, new Vector4((float)projectionMatrix[2], (float)projectionMatrix[6], (float)projectionMatrix[10], 0)); // �� P matrix�� ���غ��� ���� -> ��ǥ�� ���� �� ����? �ϴ� ���� ��
        //    rtMatrixUnity.SetColumn(3, new Vector4((float)projectionMatrix[3], (float)projectionMatrix[7], (float)projectionMatrix[11], 1));
        //    //rtMatrixUnity *= Matrix4x4.Rotate(projCam.transform.rotation);
        //    Debug.Log("Projection Camera rotation Matrix: " + string.Join(", ", projCam.transform.rotation));
        //    Quaternion rotation = rtMatrixUnity.rotation;// * projCam.transform.rotation; // Adjust rotation
        //    Debug.Log("rotation quaternion: " + string.Join(", ", rotation));
        //    Vector3 position = new Vector3((float)rtMatrix[3], (float)rtMatrix[7], (float)rtMatrix[11]);
        //    position.z += 1000.0f;

        //    createdBox.transform.rotation = rotation;
        //    createdBox.transform.position = position;
        //    CalculateReprojectionError();
        //}

    }

    float CalculateFieldOfView(Matrix4x4 K, float imageHeight)
    {
        float f_y = K.m11; // Assuming K[1,1] is the focal length in pixels along the y-axis
        float fovRadians = 2 * Mathf.Atan(imageHeight / (2 * f_y));
        return fovRadians * Mathf.Rad2Deg;
    }
    float CalculateAspectRatio(Matrix4x4 K)
    {
        float f_x = K.m00; // Focal length in pixels along the x-axis
        float f_y = K.m11; // Focal length in pixels along the y-axis
        return f_x / f_y;
    }


    Quaternion ConvertRotationOpenCVToUnity(Matrix4x4 R)
    {
        //Vector3 forward;
        //Vector3 up;

        //// Convert OpenCV rotation matrix (R) to Unity rotation
        //// OpenCV Matrix Format:
        //// [ R00 R01 R02 ]
        //// [ R10 R11 R12 ]
        //// [ R20 R21 R22 ]
        //// Unity uses a left-handed coordinate system
        //forward.x = R.m20; // Forward direction in Unity is the negative Z direction in OpenCV
        //forward.y = R.m21;
        //forward.z = -R.m22; // Flip Z-axis

        //up.x = -R.m10; // Up direction in Unity is the negative Y direction in OpenCV
        //up.y = -R.m11;
        //up.z = R.m12; // Flip Z-axis

        //return Quaternion.LookRotation(forward, up);

        Matrix4x4 transposeY = new Matrix4x4();
        transposeY.SetRow(0, new Vector4(1, 0, 0, 0));
        transposeY.SetRow(1, new Vector4(0, 0, 1, 0));
        transposeY.SetRow(2, new Vector4(0, -1, 0, 0));
        transposeY.SetRow(3, new Vector4(0, 0, 0, 1));
        R = transposeY * R * transposeY.transpose;

        Quaternion rotationM = R.rotation;
        return rotationM;
    }
    Vector3 ConvertTranslationOpenCVToUnity(Vector3 translation)
    {
        //return new Vector3(translation.x, translation.z, -translation.y); // Flip Y and Z axes
        return new Vector3(translation.x, -translation.y, translation.z); // Flip Y and Z axes
    }

    void ApplyCameraExtrinsics(Camera camera, Matrix4x4 RT)
    {
        // Extract translation from RT and convert
        Vector3 position = new Vector3(RT.m03, RT.m13, RT.m23);
        position = ConvertTranslationOpenCVToUnity(position);

        // Create rotation matrix from RT
        Matrix4x4 R = RT;
        R.m03 = R.m13 = R.m23 = 0; // Remove translation components for pure rotation extraction
        Quaternion rotation = ConvertRotationOpenCVToUnity(R);

        rotation *= Quaternion.Euler(0, 0, 180);

        Vector3 cameraPosition = -R.inverse.MultiplyPoint(position);
        cameraPosition.z += 996;
        camera.transform.position = cameraPosition;
        camera.transform.rotation = rotation;
    }

    //MSE �Լ�
    private void CalculateReprojectionError()
    {

        float camPosY = projCam.pixelHeight;
        int index = System.Array.IndexOf(vertexClickTest.clickedObjects, null);
        double[] worldPoints = new double[18];
        for (int i = 0; i < index; i++)
        {
            Vector3 unityWorldPoint = LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate;
            //Vector3 opencvWorldPoint = new Vector3(unityWorldPoint.x, -unityWorldPoint.z, unityWorldPoint.y);
            Vector3 opencvWorldPoint = new Vector3(unityWorldPoint.x, -unityWorldPoint.z, unityWorldPoint.y);
            worldPoints[i * 3] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.x;
            worldPoints[i * 3 + 1] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.y;
            worldPoints[i * 3 + 2] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].worldCoordinate.z;
        }

        double[] imagePoints = new double[12];
        for (int i = 0; i < index; i++)
        {
            imagePoints[i * 2] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].screenCoordinate.x;
            imagePoints[i * 2 + 1] = (double)LVManger.GetComponent<VertexClickTest>().verticesStruct[i].screenCoordinate.y;
        }
        int numPoints = index;

        double[] projectionMatrix = new double[12];
        double[] projectedPoints = new double[12];
        double[] rtMatrix = new double[12];
        double[] KMatrix = new double[9];
        DLT(worldPoints, imagePoints, numPoints, projectionMatrix, rtMatrix, KMatrix);

        projectPoints(worldPoints, projectionMatrix, rtMatrix, projectedPoints, camPosY);

        // Calculate the reprojection error
        double errorSum = 0;
        for (int i = 0; i < numPoints; i++)
        {
            double originalX = imagePoints[i * 2];
            double originalY = imagePoints[i * 2 + 1];
            double projectedX = projectedPoints[i * 2];
            double projectedY = projectedPoints[i * 2 + 1];

            double error = Math.Sqrt(Math.Pow(projectedX - originalX, 2) + Math.Pow(projectedY - originalY, 2));
            errorSum += error;
        }

        double averageError = errorSum / numPoints;

        Debug.Log($"Average Reprojection Error: {averageError}");
    }
    //void TransformMesh(Mesh mesh, Matrix4x4 projectionMatrix)
    //{
    //    //Vector3[] vertices = mesh.vertices;
    //    //for (int i = 0; i < vertices.Length; i++)
    //    //{
    //    //    Vector4 homogenous = new Vector4(vertices[i].x, vertices[i].y, vertices[i].z, 1f);
    //    //    Vector4 transformed = projectionMatrix * homogenous;
    //    //    vertices[i] = transformed;
    //    //    vertices[i].z += 1000;
    //    //}
    //    //mesh.vertices = vertices;
    //    //Debug.Log("vertices" + string.Join(", ", mesh.vertices));
    //    //mesh.RecalculateBounds();

    //    Vector3[] vertices = mesh.vertices;
    //    for (int i = 0; i < vertices.Length; i++)
    //    {
    //        // Apply the matrix to each vertex, ensuring we use a 3D transform
    //        vertices[i] = projectionMatrix.MultiplyPoint3x4(vertices[i]);
    //    }
    //    mesh.vertices = vertices;
    //    mesh.RecalculateBounds();
    //    mesh.RecalculateNormals();
    //}
    //void AdjustMeshAttributes(Mesh mesh)
    //{
    //    mesh.RecalculateNormals();
    //    mesh.RecalculateTangents();
    //}

}
