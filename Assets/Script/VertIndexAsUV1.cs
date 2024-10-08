using UnityEngine;
using UnityEngine.UI;

namespace Script
{
    public class VertIndexAsUV1 : BaseMeshEffect
    {
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;

            UIVertex vert = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                vert.uv1.x = 0.5f;
                vert.uv1.y = 0.5f;
                vh.SetUIVertex(vert, i);
            }
        }
    }
}