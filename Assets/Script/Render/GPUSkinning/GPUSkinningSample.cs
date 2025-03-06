
#if UNITY_EDITOR


using System.Collections.Generic;
using System.IO;
using Script.Util;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Script.Render.GPUSkinning
{
    public class GPUSkinningSample : MonoBehaviour
    {
        [Header("导出路径")] public string OutPutDir = "Assets/RoleAssets/BoneClip";
        [Header("骨骼")] public SkinnedMeshRenderer skinnedRenderer;
        [Header("网格")] public Mesh mesh;
        [Header("动画器")] public Animation animation;
        [Header("动画片段资源")] public AnimationClip clip;
        [Range(2, 4)]
        [Header("顶点关联骨骼数")] public int weightCount;
        [Header("Shader")] public Shader shader;
        [Header("预览")][EditDisabledAttribute] public Texture2D animTex;


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

                var total = Mathf.CeilToInt(Mathf.Sqrt(boneCount * animFrameCount * 12));
                for (width = 32; width <= 1024; width *= 2)
                {
                    if (total < width)
                        break;
                }
            }
        }

        private void ModifyMesh()
        {
            if (mesh == null || skinnedRenderer == null) return;
            if (vertexCount == 0 || animFrameCount == 0)
            {
                GPUSkinUtil.Msg("网格及动画读取失败！");
                return;
            }
            if (mesh.boneWeights == null || mesh.boneWeights.Length == 0)
            {
                GPUSkinUtil.Msg("网格中没有骨骼权重信息！");
                return;
            }
            if (boneCount > 255)
            {
                GPUSkinUtil.Msg("骨骼数超过255！");
                return;
            }

            MappingBoneWeightToMeshUV(mesh, weightCount);

            if (!CreateAnimTex(animation, skinnedRenderer, clip, mesh, width, animFrameCount, out animTex))
            {
                return;
            }
            var bytes = animTex.EncodeToPNG();
            var name = string.Format("{0}/{1}_{2}_bone.png", OutPutDir, animation.gameObject.name, "Animation1");
            File.WriteAllBytes(name, bytes);
            GPUSkinUtil.Msg(string.Format("已保存到{0}", name));
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


        private bool CreateAnimTex(Animation animation, SkinnedMeshRenderer skinnedMeshRenderer, AnimationClip clip, Mesh mesh,
        int width, int animFrameCount, out Texture2D animTex)
        {
            animTex = null;
            Matrix4x4[] bindPoses = mesh.bindposes;
            Transform[] bones = skinnedMeshRenderer.bones;
            int bonesCount = bones.Length;
            if (bindPoses.Length != bones.Length)
            {
                GPUSkinUtil.Msg("骨骼与绑定姿势不匹配！", true);
                return false;
            }
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
                    colors[(i * bonesCount + j) * 12 + 0] = GPUSkinUtil.EncodeFloatRGBA(matrix.m00);
                    colors[(i * bonesCount + j) * 12 + 1] = GPUSkinUtil.EncodeFloatRGBA(matrix.m01);
                    colors[(i * bonesCount + j) * 12 + 2] = GPUSkinUtil.EncodeFloatRGBA(matrix.m02);
                    colors[(i * bonesCount + j) * 12 + 3] = GPUSkinUtil.EncodeFloatRGBA(matrix.m03);
                    colors[(i * bonesCount + j) * 12 + 4] = GPUSkinUtil.EncodeFloatRGBA(matrix.m10);
                    colors[(i * bonesCount + j) * 12 + 5] = GPUSkinUtil.EncodeFloatRGBA(matrix.m11);
                    colors[(i * bonesCount + j) * 12 + 6] = GPUSkinUtil.EncodeFloatRGBA(matrix.m12);
                    colors[(i * bonesCount + j) * 12 + 7] = GPUSkinUtil.EncodeFloatRGBA(matrix.m13);
                    colors[(i * bonesCount + j) * 12 + 8] = GPUSkinUtil.EncodeFloatRGBA(matrix.m20);
                    colors[(i * bonesCount + j) * 12 + 9] = GPUSkinUtil.EncodeFloatRGBA(matrix.m21);
                    colors[(i * bonesCount + j) * 12 + 10] = GPUSkinUtil.EncodeFloatRGBA(matrix.m22);
                    colors[(i * bonesCount + j) * 12 + 11] = GPUSkinUtil.EncodeFloatRGBA(matrix.m23);
                }
            }
            result.SetPixels(colors);
            result.Apply();
            animTex = result;


            if (shader == null){
                GPUSkinUtil.Msg("Shader is not assigned!");
                return false;
            }

            Material material = new Material(shader);
            material.SetTexture("_MainTex", animTex);

            // 保存材质
            var path = string.Format("{0}/{1}.mat", OutPutDir, mesh.name);
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            GPUSkinUtil.Msg($"Material saved to {path}");


            return true;
        }


       

        private void MappingBoneWeightToMeshUV(Mesh mesh, int weightCount)
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

            //保存网格
            var copy = Instantiate(mesh);
            var path = string.Format("{0}/{1}.asset", OutPutDir, mesh.name);
            AssetDatabase.CreateAsset(copy, path);
            AssetDatabase.SaveAssets();
            
            GPUSkinUtil.Msg($"Mesh saved to {OutPutDir}");
        }


    }
}

#endif