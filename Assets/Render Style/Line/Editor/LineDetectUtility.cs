using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class LineDetectUtility : EditorWindow
{
    Mesh mesh;
    Shader UVLayoutShader;
    Shader UVDetectShader;
    Shader LineBlurShader;
    int lineWidth = 1;
    int blurRadius = 1;
    Vector2Int resolution = new Vector2Int(1024, 1024);
    string filePath = "Assets/Line.png";

    bool hasMesh;
    bool hasShader;
    bool hasResolution;
    bool hasFilePath;

    [MenuItem("Tools/Bake Line By UV Detect")]
    static void OpenWindow()
    {
        LineDetectUtility window = EditorWindow.GetWindow<LineDetectUtility>();
        window.Show();
        window.CheckInput();
    }

    void OnGUI()
    {
        using (var check = new EditorGUI.ChangeCheckScope())
        {
            mesh = (Mesh)EditorGUILayout.ObjectField("Mesh", mesh, typeof(Mesh), false);
            UVLayoutShader = (Shader)EditorGUILayout.ObjectField("UVLayout", UVLayoutShader, typeof(Shader), false);
            UVDetectShader = (Shader)EditorGUILayout.ObjectField("UVDetect", UVDetectShader, typeof(Shader), false);
            LineBlurShader = (Shader)EditorGUILayout.ObjectField("LineBlur", LineBlurShader, typeof(Shader), false);
            lineWidth = EditorGUILayout.IntSlider("Line Width", lineWidth, 1, 10);
            blurRadius = EditorGUILayout.IntSlider("Blur Radius", blurRadius, 1, 10);
            resolution = EditorGUILayout.Vector2IntField("Texture Resolution", resolution);
            filePath = FileField(filePath);

            if (check.changed)
            {
                CheckInput();
            }
        }

        GUI.enabled = hasShader && hasResolution && hasFilePath;
        if (GUILayout.Button("Bake"))
        {
            BakeTexture();
        }
        GUI.enabled = true;

        //tell the user what inputs are missing
        if (!mesh)
            EditorGUILayout.HelpBox("You're still missing a mesh to bake.", MessageType.Warning);
        if (!hasShader)
            EditorGUILayout.HelpBox("You're still missing a shader to bake.", MessageType.Warning);
        if (!hasResolution)
            EditorGUILayout.HelpBox("Please set a size bigger than zero.", MessageType.Warning);
        if (!hasFilePath)
            EditorGUILayout.HelpBox("No file to save the image to given.", MessageType.Warning);
    }

    void CheckInput()
    {
        //check which values are entered already
        hasMesh = mesh != null;
        hasShader = UVLayoutShader != null && UVDetectShader != null && LineBlurShader != null;
        hasResolution = resolution.x > 0 && resolution.y > 0;
        hasFilePath = false;
        try
        {
            string ext = Path.GetExtension(filePath);
            hasFilePath = ext.Equals(".png");
        }
        catch (ArgumentException) { }
    }

    string FileField(string path)
    {
        //allow the user to enter output file both as text or via file browser
        EditorGUILayout.LabelField("File Path");
        using (new GUILayout.HorizontalScope())
        {
            path = EditorGUILayout.TextField(path);
            if (GUILayout.Button("Choose"))
            {
                //set default values for directory, then try to override them with values of existing path
                string directory = "Assets";
                string fileName = "Line.png";
                try
                {
                    directory = Path.GetDirectoryName(path);
                    fileName = Path.GetFileName(path);
                }
                catch (ArgumentException) { }
                string chosenFile = EditorUtility.SaveFilePanelInProject(
                    "Choose File", 
                    fileName,
                    "png", 
                    "Please enter a file name to save the texture to", 
                    directory);
                if (!string.IsNullOrEmpty(chosenFile))
                    path = chosenFile;
                //repaint editor because the file changed and we can't set it in the textfield retroactively
                Repaint();
            }
        }
        return path;
    }

    void BakeTexture()
    {
        Material uvLayout = new Material(UVLayoutShader);
        Material uvDetect = new Material(UVDetectShader);
        Material lineBlur = new Material(LineBlurShader);

        RenderTexture lineTex = RenderTexture.GetTemporary(
            resolution.x, resolution.y, 0, RenderTextureFormat.RHalf);
        CommandBuffer cb = new CommandBuffer();
        cb.SetRenderTarget(lineTex);
        cb.DrawMesh(mesh, Matrix4x4.identity, uvLayout, 0, 0);
        int temp = Shader.PropertyToID("_Temp");
        cb.GetTemporaryRT(temp, lineTex.descriptor);
        uvDetect.SetFloat("_LineWidth", lineWidth);
        cb.Blit(lineTex, temp, uvDetect);
        lineBlur.SetFloat("_BlurRadius", blurRadius);
        cb.Blit(temp, lineTex, lineBlur);
        cb.ReleaseTemporaryRT(temp);
        Graphics.ExecuteCommandBuffer(cb);

        //transfer image from rendertexture to texture
        Texture2D texture = new Texture2D(resolution.x, resolution.y);
        RenderTexture.active = lineTex;
        texture.ReadPixels(new Rect(Vector2.zero, resolution), 0, 0);

        //save texture to file
        byte[] png = texture.EncodeToPNG();
        File.WriteAllBytes(filePath, png);
        AssetDatabase.Refresh();

        //clean up variables
        cb.Release();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(lineTex);
        DestroyImmediate(texture);
        DestroyImmediate(uvLayout);
        DestroyImmediate(uvDetect);
    }
}
