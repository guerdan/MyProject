using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Component
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

            if (vertices.Count <= 0) return;
            UIVertex min_vertex = vertices[0];
            UIVertex max_vertex = vertices[0];

            for (int i = 0; i < vertices.Count; i++)
            {
                min_vertex = UVAddXY(vertices[i]) <= UVAddXY(min_vertex) ? vertices[i] : min_vertex;
                max_vertex = UVAddXY(vertices[i]) >= UVAddXY(max_vertex) ? vertices[i] : max_vertex;
            }


            float width = max_vertex.position.x - min_vertex.position.x;
            float height = max_vertex.position.y - min_vertex.position.y;

            for (int i = 0; i < vertices.Count; i++)
            {
                UIVertex vertex = vertices[i];
                float xRatio = (vertex.position.x - min_vertex.position.x) / width;
                float yRatio = (vertex.position.y - min_vertex.position.y) / height;
                vertex.uv1 = new Vector2(xRatio, yRatio);
                vertices[i] = vertex;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(vertices);

            #region 打印

            // string print = "";
            // for (int i = 0; i < vertices.Count; i++)
            // {
            //     UIVertex vertex = vertices[i];
            //     print += String.Format(
            //         $"Vertex {i}: UV0 = {vertex.uv0} position = {vertex.position}\n");
            // }
            //
            // Debug.LogWarning(print);

            #endregion
        }

        public float UVAddXY(UIVertex vertex0)
        {
            return vertex0.uv0.x + vertex0.uv0.y;
        }
    }
}