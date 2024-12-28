using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace Script.UI.Component
{
    public class OutlineCustome : UnityEngine.UI.Shadow
    {
        protected OutlineCustome()
        {
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;

            var verts = ListPool<UIVertex>.Get();
            vh.GetUIVertexStream(verts);
            var length = verts.Count;

            var neededCpacity = verts.Count * 5;
            if (verts.Capacity < neededCpacity)
                verts.Capacity = neededCpacity;

            var start = 0;
            var end = verts.Count;
            ApplyShadowZeroAlloc(verts, new Color32(255, 0, 0, 255), start, verts.Count, effectDistance.x,
                effectDistance.y);

            start = end;
            end = verts.Count;
            ApplyShadowZeroAlloc(verts, new Color32(0, 255, 0, 255), start, verts.Count, effectDistance.x,
                -effectDistance.y);

            start = end;
            end = verts.Count;
            ApplyShadowZeroAlloc(verts, new Color32(0, 0, 255, 255), start, verts.Count, -effectDistance.x,
                effectDistance.y);

            start = end;
            end = verts.Count;
            ApplyShadowZeroAlloc(verts, new Color32(255, 255, 0, 255), start, verts.Count, -effectDistance.x,
                -effectDistance.y);

            //处理一下，验证一些东西:这样原图就被遮住了。
            ChangeVert(verts, length);

            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
            ListPool<UIVertex>.Release(verts);
        }

        protected void ChangeVert(List<UIVertex> verts, int length)
        {
            UIVertex vt;
            List<UIVertex> list = new List<UIVertex>();


            //暂存最末的原顶点
            for (int i = verts.Count - length; i < verts.Count; ++i)
            {
                vt = verts[i];
                list.Add(vt);
            }

            //将前面的顶点后移
            for (int i = verts.Count - length - 1; i >= 0; --i)
            {
                verts[i + length] = verts[i];
            }

            //将原顶点数据赋值到最前面
            for (int i = 0; i < length; ++i)
            {
                verts[i] = list[i];
            }
        }
    }
}