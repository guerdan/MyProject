using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Script
{
    [RequireComponent(typeof(Graphic))]
    public class VertIndexAsUV1 : BaseMeshEffect
    {
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;

            // UIVertex vert = new UIVertex();
            // for (int i = 0; i < vh.currentVertCount; i++)
            // {
            //     vh.PopulateUIVertex(ref vert, i);
            //     vert.uv1.x = 0.5f;
            //     vert.uv1.y = 0.5f;
            //     vh.SetUIVertex(vert, i);
            // }
            
            List<UIVertex> vertices = new List<UIVertex>();
            vh.GetUIVertexStream(vertices);

            for (int i = 0; i < vertices.Count; i++)
            {
                UIVertex vertex = vertices[i];
                // 设置第二组 UV 坐标，这里简单地将第一组 UV 坐标复制到第二组
                vertex.uv1 = new Vector2(vertex.uv0.x + 0.5f, vertex.uv0.y);
                vertices[i] = vertex;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(vertices);

            string print = "";
            for (int i = 0; i < vertices.Count; i++)
            {
                UIVertex vertex = vertices[i];
                print += String.Format(
                    $"Vertex {i}: UV0 = {vertex.uv0} \n");
            }
            Debug.LogWarning(print);
        }
        

    }
}