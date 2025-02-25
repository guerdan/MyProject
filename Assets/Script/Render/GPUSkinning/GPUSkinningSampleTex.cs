
using System.Collections.Generic;
using System.IO;
using Script.Util;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Script.Render.GPUSkinning
{
    public class GPUSkinningSampleTex : MonoBehaviour
    {
        public static bool UseMsg = true;

        private static void Msg(string info, bool warning = false)
        {
            if (!UseMsg) return;
            if (warning)
                Debug.LogWarning(string.Format("{0}--{1}", "烘焙动画纹理", info));
            else
                Debug.Log(string.Format("{0}--{1}", "烘焙动画纹理", info));
        }

        [Header("导出路径")] public string OutPutDir = "Assets/RoleAssets/BoneClip";
        [Header("骨骼")] public SkinnedMeshRenderer skinnedRenderer;
        [Header("网格")] public Mesh mesh;
        [Header("动画器")] public Animation animation;
        [Header("动画片段资源")] public AnimationClip clip;
        [Range(2, 4)]
        [Header("顶点关联骨骼数")] public int weightCount;
        [Header("预览")] public Texture2D animTex;


        // [Header("信息")][ShowInInspector] private int VertexCount => vertexCount;
        // [Header("网格")] private int AnimFrameCount => animFrameCount;
        // [Header("网格")] private int BoneCount => boneCount;
        // [Header("网格")] private int Width => width;
        [EditDisabledAttribute] public int vertexCount;
        [EditDisabledAttribute] public int animFrameCount;
        [EditDisabledAttribute] public int boneCount;
        [EditDisabledAttribute] public int width;

        private void Start()
        {
            Init();
            ModifyMesh();
        }

        private void Init()
        {
            if (mesh != null && clip != null)
            {
                boneCount = mesh.bindposes.Length;
                vertexCount = mesh.vertexCount;
                animFrameCount = (int)(clip.length * clip.frameRate);

                width = Mathf.CeilToInt(Mathf.Sqrt(boneCount * animFrameCount * 12));
                width = width > 32 ? 64 : 32;
                width = width > 64 ? 128 : 64;
                width = width > 128 ? 256 : 128;
                width = width > 256 ? 512 : 256;
            }
        }

        private void ModifyMesh()
        {
            if (mesh == null || skinnedRenderer == null) return;
            if (vertexCount == 0 || animFrameCount == 0)
            {
                Msg("网格及动画读取失败！");
                return;
            }
            if (mesh.boneWeights == null || mesh.boneWeights.Length == 0)
            {
                Msg("网格中没有骨骼权重信息！");
                return;
            }
            if (boneCount > 255)
            {
                Msg("骨骼数超过255！");
                return;
            }

            MappingBoneWeightToMeshUV(mesh, weightCount);

            if (!CreateAnimTex(animation, skinnedRenderer, clip, mesh, width, animFrameCount, out animTex))
            {
                Msg("骨骼与绑定姿势不匹配！");
                return;
            }
            var bytes = animTex.EncodeToPNG();
            var name = string.Format("{0}/{1}_{2}_bone.png", OutPutDir, animation.gameObject.name, "Animation1");
            File.WriteAllBytes(name, bytes);
            Msg(string.Format("已保存到{0}", name));
            AssetDatabase.Refresh();
            // 修改下导入设置。找到meta修改meta
            TextureImporter timporter = TextureImporter.GetAtPath(name) as TextureImporter;
            if (timporter)
            {
                TextureImporterSettings tis = new TextureImporterSettings();
                timporter.ReadTextureSettings(tis);
                tis.filterMode = FilterMode.Point;
                tis.npotScale = TextureImporterNPOTScale.None;
                tis.mipmapEnabled = false;
                timporter.SetTextureSettings(tis);
                //重新导入资产，以确保修改生效
                AssetDatabase.ImportAsset(name);
            }
        }


        private static bool CreateAnimTex(Animation animation, SkinnedMeshRenderer skinnedMeshRenderer, AnimationClip clip, Mesh mesh,
        int width, int animFrameCount, out Texture2D animTex)
        {
            animTex = null;
            Matrix4x4[] bindPoses = mesh.bindposes;
            Transform[] bones = skinnedMeshRenderer.bones;
            int bonesCount = bones.Length;
            if (bindPoses.Length != bones.Length)
                return false;
            if (animation.GetClip(clip.name) == null)
                animation.AddClip(clip, clip.name);
            animation.Play(clip.name);

            // 开始采样
            int lines = Mathf.CeilToInt((float)bones.Length * animFrameCount * 12 / width);
            Texture2D result = new Texture2D(width, lines, TextureFormat.RGBA32, false);
            result.filterMode = FilterMode.Point;
            result.wrapMode = TextureWrapMode.Clamp;
            Color[] colors = new Color[width * lines];
            // 逐帧写入矩阵
            for (int i = 0; i < animFrameCount; i++)
            {
                float time = (float)i / (animFrameCount - 1);
                animation[clip.name].normalizedTime = time;
                animation.Sample();
                // 写入变换后的矩阵
                for (int j = 0; j < bonesCount; j++)
                {
                    //绑定姿势的逆矩阵 左乘 骨骼的本地到世界变换矩阵 左乘 物体的世界到本地变换逆矩阵 = 最终变换矩阵
                    //推理: 骨骼的世界变换矩阵 左乘 物体的世界变换逆矩阵 = 每个骨骼的变换矩阵叠乘
                    Matrix4x4 matrix = skinnedMeshRenderer.transform.worldToLocalMatrix * bones[j].localToWorldMatrix * bindPoses[j];
                    colors[(i * bonesCount + j) * 12 + 0] = EncodeFloatRGBA(matrix.m00);
                    colors[(i * bonesCount + j) * 12 + 1] = EncodeFloatRGBA(matrix.m01);
                    colors[(i * bonesCount + j) * 12 + 2] = EncodeFloatRGBA(matrix.m02);
                    colors[(i * bonesCount + j) * 12 + 3] = EncodeFloatRGBA(matrix.m03);
                    colors[(i * bonesCount + j) * 12 + 4] = EncodeFloatRGBA(matrix.m10);
                    colors[(i * bonesCount + j) * 12 + 5] = EncodeFloatRGBA(matrix.m11);
                    colors[(i * bonesCount + j) * 12 + 6] = EncodeFloatRGBA(matrix.m12);
                    colors[(i * bonesCount + j) * 12 + 7] = EncodeFloatRGBA(matrix.m13);
                    colors[(i * bonesCount + j) * 12 + 8] = EncodeFloatRGBA(matrix.m20);
                    colors[(i * bonesCount + j) * 12 + 9] = EncodeFloatRGBA(matrix.m21);
                    colors[(i * bonesCount + j) * 12 + 10] = EncodeFloatRGBA(matrix.m22);
                    colors[(i * bonesCount + j) * 12 + 11] = EncodeFloatRGBA(matrix.m23);
                }
            }
            result.SetPixels(colors);
            result.Apply();
            animTex = result;
            return true;
        }

        // 前提：v是0-1之间的浮点数
        // https://blog.csdn.net/zengjunjie59/article/details/111405894
        // 单单看代码实在让人摸不着头脑，展开后可得EncodeFloatRGBA的最终结果为：
        // x是v  
        // y是(v mod 1/255）* 255  
        // z是(v mod 1/255^2）* 255^2  
        // w是(v mod 1/255^3）* 255^3
        // 举个例子，比如现在有个float类型的值h=1.23456…，
        // 我如果把h存在一个int整形的变量里面，那么小数后面的精度肯定丢失了。
        // 如果我想保留，那么可以用四个整形变量a, r, g, b来保存，a = 1, r = 2, g = 3, b = 4。
        // 这样g可以表示为:h = a+0.1r+0.01g+0.001*b。这个原理其实和上面的Encode和Decode是一样的，
        // 只不过这个例子用的是10倍，而U3D中是255倍。
        private static Vector4 EncodeFloatRGBA(float v)
        {
            v = v * 0.01f + 0.5f;
            if (v > 1) Msg("精度丢失！");
            Vector4 kEncodeMul = new Vector4(1.0f, 255.0f, 65025.0f, 160581375.0f);
            float kEncodeBit = 1.0f / 255.0f;
            Vector4 enc = kEncodeMul * v;
            for (int i = 0; i < 4; i++)
                enc[i] = enc[i] - Mathf.Floor(enc[i]); //取余
            enc = enc - new Vector4(enc.y, enc.z, enc.w, enc.w) * kEncodeBit;
            return enc;
        }

        private static void MappingBoneWeightToMeshUV(Mesh mesh, int weightCount)
        {
            var boneWeights = mesh.boneWeights;

            List<Vector2> UV1 = new List<Vector2>();
            List<Vector2> UV2 = new List<Vector2>();
            List<Vector2> UV3 = new List<Vector2>();
            List<Vector2> UV4 = new List<Vector2>();
            for (int i = 0; i < boneWeights.Length; i++)
            {
                var bw = boneWeights[i];
                UV1.Add(new Vector2(bw.boneIndex0, bw.weight0));
                UV2.Add(new Vector2(bw.boneIndex1, bw.weight1));
                if (weightCount >= 3) UV3.Add(new Vector2(bw.boneIndex2, bw.weight2));
                if (weightCount >= 4) UV4.Add(new Vector2(bw.boneIndex3, bw.weight3));
            }
            mesh.SetUVs(1, UV1);
            mesh.SetUVs(2, UV2);
            if (weightCount >= 3) mesh.SetUVs(3, UV3);
            if (weightCount >= 4) mesh.SetUVs(4, UV3);

        }


    }
}