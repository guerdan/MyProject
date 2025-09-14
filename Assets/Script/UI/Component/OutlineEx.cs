
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Component
{
    /// <summary>
    /// OutlineExtension, 实现描边效果。感谢https://www.cnblogs.com/lovewaits/p/15588134.html提供方案
    /// 回想下怎么合批。
    /// 解决描边透明度不改变问题。 Image.Color 传到了 顶点的.color里了
    /// </summary>
    public class OutlineEx : BaseMeshEffect
    {
        [Header("共享现有实例, 下方为只读材质属性")]
        [SerializeField] public Material ShareMat;                         // 使用已有资源

        [Header("单独生成实例, 关联材质属性")]
        [SerializeField] public Color OutlineColor = Color.white;     // 单独生成材质实例
        [SerializeField][Range(0, 6)] public float OutlineWidth = 0;  // 单独生成材质实例

        private bool useNewInstance => ShareMat == null;
        private static List<UIVertex> m_VetexList = new List<UIVertex>();

        protected override void Start()
        {
            base.Start();

            Init();
        }

        void Init()
        {

            if (!useNewInstance)
                base.graphic.material = ShareMat;
            else
            {
                var shader = Shader.Find("Custom/UI/OutlineEx");
                base.graphic.material = new Material(shader);
            }

            if (base.graphic.canvas)
            {
                var v1 = base.graphic.canvas.additionalShaderChannels;
                var v2 = AdditionalCanvasShaderChannels.TexCoord1;
                if ((v1 & v2) != v2)
                {
                    base.graphic.canvas.additionalShaderChannels |= v2;
                }
                v2 = AdditionalCanvasShaderChannels.TexCoord2;
                if ((v1 & v2) != v2)
                {
                    base.graphic.canvas.additionalShaderChannels |= v2;
                }
            }

            this._Refresh();
        }


#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            Init();

        }
#endif


        private void _Refresh()
        {
            if (useNewInstance)
            {
                base.graphic.material.SetColor("_OutlineColor", this.OutlineColor);
                base.graphic.material.SetFloat("_OutlineWidth", this.OutlineWidth);
                base.graphic.SetVerticesDirty();
            }
            else
            {
                this.OutlineColor = base.graphic.material.GetColor("_OutlineColor");
                this.OutlineWidth = base.graphic.material.GetFloat("_OutlineWidth");
            }
        }


        public override void ModifyMesh(VertexHelper vh)
        {
            vh.GetUIVertexStream(m_VetexList);

            this._ProcessVertices();

            vh.Clear();
            vh.AddUIVertexTriangleStream(m_VetexList);
        }


        private void _ProcessVertices()
        {
            for (int i = 0, count = m_VetexList.Count - 3; i <= count; i += 3)
            {
                var v1 = m_VetexList[i];
                var v2 = m_VetexList[i + 1];
                var v3 = m_VetexList[i + 2];
                Vector2 pos1 = v1.position;
                Vector2 pos2 = v2.position;
                Vector2 pos3 = v3.position;

                // 计算原顶点坐标中心点
                //
                var minX = _Min(pos1.x, pos2.x, pos3.x);
                var minY = _Min(pos1.y, pos2.y, pos3.y);
                var maxX = _Max(pos1.x, pos2.x, pos3.x);
                var maxY = _Max(pos1.y, pos2.y, pos3.y);
                var posCenter = new Vector2(minX + maxX, minY + maxY) * 0.5f;
                // 计算原始顶点坐标和UV的方向
                //
                Vector2 triX, triY, uvX, uvY;
                if (Mathf.Abs(Vector2.Dot((pos2 - pos1).normalized, Vector2.right))
                    > Mathf.Abs(Vector2.Dot((pos3 - pos2).normalized, Vector2.right)))
                {
                    triX = pos2 - pos1;
                    triY = pos3 - pos2;
                    uvX = v2.uv0 - v1.uv0;
                    uvY = v3.uv0 - v2.uv0;
                }
                else
                {
                    triX = pos3 - pos2;
                    triY = pos2 - pos1;
                    uvX = v3.uv0 - v2.uv0;
                    uvY = v2.uv0 - v1.uv0;
                }
                // 计算原始UV框
                //
                var uvMin = _Min(v1.uv0, v2.uv0, v3.uv0);
                var uvMax = _Max(v1.uv0, v2.uv0, v3.uv0);
                var uvOrigin = new Vector4(uvMin.x, uvMin.y, uvMax.x, uvMax.y);
                // 为每个顶点设置新的Position和UV，并传入原始UV框
                //
                v1 = _SetNewPosAndUV(v1, this.OutlineWidth, posCenter, triX, triY, uvX, uvY, uvOrigin);
                v2 = _SetNewPosAndUV(v2, this.OutlineWidth, posCenter, triX, triY, uvX, uvY, uvOrigin);
                v3 = _SetNewPosAndUV(v3, this.OutlineWidth, posCenter, triX, triY, uvX, uvY, uvOrigin);
                // 应用设置后的UIVertex
                //
                m_VetexList[i] = v1;
                m_VetexList[i + 1] = v2;
                m_VetexList[i + 2] = v3;
            }
        }


        private static UIVertex _SetNewPosAndUV(UIVertex pVertex, float pOutLineWidth,
            Vector2 pPosCenter,
            Vector2 pTriangleX, Vector2 pTriangleY,
            Vector2 pUVX, Vector2 pUVY,
            Vector4 pUVOrigin)
        {
            // Position
            var pos = pVertex.position;
            var posXOffset = pos.x > pPosCenter.x ? pOutLineWidth : -pOutLineWidth;
            var posYOffset = pos.y > pPosCenter.y ? pOutLineWidth : -pOutLineWidth;
            pos.x += posXOffset;
            pos.y += posYOffset;
            pVertex.position = pos;
            // UV
            var uv = pVertex.uv0;
            uv += ToVector4(pUVX / pTriangleX.magnitude * posXOffset * (Vector2.Dot(pTriangleX, Vector2.right) > 0 ? 1 : -1));
            uv += ToVector4(pUVY / pTriangleY.magnitude * posYOffset * (Vector2.Dot(pTriangleY, Vector2.up) > 0 ? 1 : -1));
            pVertex.uv0 = uv;
            // 原始UV框
            pVertex.uv1 = new Vector2(pUVOrigin.x, pUVOrigin.y);
            pVertex.uv2 = new Vector2(pUVOrigin.z, pUVOrigin.w);

            return pVertex;
        }

        private static Vector4 ToVector4(Vector2 v)
        {
            return new Vector4(v.x, v.y, 0, 0);
        }


        private static float _Min(float pA, float pB, float pC)
        {
            return Mathf.Min(Mathf.Min(pA, pB), pC);
        }


        private static float _Max(float pA, float pB, float pC)
        {
            return Mathf.Max(Mathf.Max(pA, pB), pC);
        }


        private static Vector2 _Min(Vector2 pA, Vector2 pB, Vector2 pC)
        {
            return new Vector2(_Min(pA.x, pB.x, pC.x), _Min(pA.y, pB.y, pC.y));
        }


        private static Vector2 _Max(Vector2 pA, Vector2 pB, Vector2 pC)
        {
            return new Vector2(_Max(pA.x, pB.x, pC.x), _Max(pA.y, pB.y, pC.y));
        }
    }
}