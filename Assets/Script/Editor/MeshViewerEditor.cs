
using System.Collections.Generic;
using System.Text;
using Script.Test;
using UnityEditor;
using UnityEngine;

namespace Script.Editor
{
   [CustomEditor(typeof(TestMeshViewer))]
    public class MeshViewerEditor : UnityEditor.Editor
    {
        private bool showInfo = true;
        private bool showUV = true;
        private bool showVertice = true;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            showInfo = GUILayout.Toggle(showInfo, "Show Info");
            showVertice = GUILayout.Toggle(showVertice, "Show Vertice");
            showUV = GUILayout.Toggle(showUV, "Show UV");

        }
        private void OnSceneGUI()
        {
            if (!showInfo)
            {
                return;
            }


            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.red;
            TestMeshViewer viewer = target as TestMeshViewer;
            Mesh mesh = viewer.GetComponent<MeshFilter>().sharedMesh;
            Dictionary<Vector3, StringBuilder> posList = new Dictionary<Vector3, StringBuilder>();

            for (int i = 0, imax = mesh.vertices.Length; i < imax; ++i)
            {
                Vector3 vPos = viewer.transform.TransformPoint(mesh.vertices[i]);
                StringBuilder sb= new StringBuilder();
                if (!posList.TryGetValue(vPos, out _))
                {
                    sb.Clear();
                    sb.Append("index:" + i);
                    sb.Append("\n vertice:" + mesh.vertices[i]);
                    if (i < mesh.normals.Length) sb.Append("\n normal:" + mesh.normals[i]);
                    if (i < mesh.tangents.Length) sb.Append("\n tangents:" + mesh.tangents[i]);
                    if (i < mesh.colors.Length) sb.Append("\n color:" + mesh.colors[i]);
                    if (i < mesh.colors32.Length) sb.Append("\n color32:" + mesh.colors32[i]);
                    if (i < mesh.uv.Length) sb.Append("\n uv:" + mesh.uv[i]);
                    //通常用于光照贴图
                    if (i < mesh.uv2.Length) sb.Append("\n uv2:" + mesh.uv2[i]);
                    if (i < mesh.uv3.Length) sb.Append("\n uv3:" + mesh.uv3[i]);
                    posList.Add(vPos, sb);
                }

                // if (i == 0)
                    Handles.Label(vPos, sb.ToString(), style);
            }
        }

        private void AddVerticeStr(StringBuilder sb, Vector3 vert)
        {
            if (!showVertice)
            {
                return;
            }
            sb.Append(",vertice:" + vert);
        }
        private void AddUVStr(StringBuilder sb, Vector2[] uvList, int index)
        {
            if (!showUV)
            {
                return;
            }


        }
    }
}