

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Script.Util;
using Spine;
using Spine.Unity;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Script.Render.GPUSkinning
{

    // 动态生成的模型的顶点顺序，就是先按附件排序SpineMesh.skeletonDrawOrderItems
    // 再把每个附件内的顶点顺序拼接。所以我们有初始姿势模型，顶点绑定的骨骼数据，
    // 然后采样动画的第一帧，每个骨骼的矩阵倒推就获得了初始姿势的逆矩阵
    // 对每帧动画采样生成动画纹理，记录每帧每骨骼的最终变换矩阵。
    public class GPUSkinning2DSample : MonoBehaviour
    {
        [Header("导出路径")] public string OutPutDir = "Assets/RoleAssets/BoneClip";
        [Header("spine动画器")] public SkeletonAnimation anim;
        [Header("使用的Shader")] public Shader shader;   //用来生成材质

        [EditDisabledAttribute]
        [Header("骨骼总数")]
        public int bonesCount;

        [EditDisabledAttribute]
        [Header("顶点绑定骨骼解析信息")]
        public string[] vertexBindBoneInfo;

        private SkeletonDataAsset asset;
        private MeshRenderer re;
        private Image im;

        private List<Vector2[]> vertexBoneWeights = new List<Vector2[]>();

        public void Start()
        {
            asset = anim.SkeletonDataAsset;
            if(!File.Exists(OutPutDir))
            {
                Directory.CreateDirectory(OutPutDir);
            }
            Deal();

        }

        void Deal()
        {
            // 先初始化动画
            string animName = "idle";
            string spineName = asset.skeletonJSON.name;
            anim.AnimationState.SetAnimation(0, animName, true);
            anim.timeScale = 1;

            // 每秒30帧的动画
            Skeleton skeleton = anim.Skeleton;  //spine实例
            var drawSlotOrder = skeleton.drawOrder.Items;
            var skeletonBones = skeleton.bones.Items;

            // 开始采样 从0帧开始
            asset = anim.SkeletonDataAsset;
            SkeletonData skeletonData = asset.GetSkeletonData(true);
            Spine.Animation animation = skeletonData.FindAnimation(animName);
            float duration = animation.Duration;
            float interval = 0.03333f;
            int animFrameCount = Mathf.FloorToInt(duration / interval);
            bonesCount = skeletonBones.Length;
            Attachment[] attachL = new Attachment[drawSlotOrder.Length];
            //采样每个骨骼的矩阵：顶点从骨骼空间变换到模型空间的矩阵。
            Matrix4x4[][] BoneToModelMat = new Matrix4x4[animFrameCount][];
            var nodeDic = new Dictionary<string, Transform>();
            var boneUsedIndex = new HashSet<int>();

            float lastTime = 0;
            float time = 0.0001f;
            for (int frame = 0; frame < animFrameCount; frame++)
            {
                // 采样
                anim.Update(time - lastTime);
                lastTime = time;
                time += interval;
                // Debug.Log($"采样第{frame}帧有 {anim.meshGenerator.VertexCount}");

                for (int slotIndex = 0; slotIndex < drawSlotOrder.Length; slotIndex++)
                {
                    Slot slot = drawSlotOrder[slotIndex];
                    Attachment attachment = slot.Attachment;  //附件

                    if (attachment == null)
                    {
                        var a = 0;
                    }

                    if (attachment is RegionAttachment || attachment is MeshAttachment)
                    {
                        if (attachL[slotIndex] == null)
                            attachL[slotIndex] = attachment;
                        else if (attachL[slotIndex] != attachment)
                        {
                            GPUSkinUtil.Msg($"插槽同时存在多个附件！ 插槽位置：{slotIndex}", true);
                            return;
                        }
                    }
                }

                //采样每个骨骼的矩阵：顶点从骨骼空间变换到模型空间的矩阵。
                BoneToModelMat[frame] = new Matrix4x4[bonesCount];
                for (int boneIndex = 0; boneIndex < bonesCount; boneIndex++)
                {
                    Bone bone = skeletonBones[boneIndex];
                    string nodeName = bone.Data.Name;

                    if (!nodeDic.TryGetValue(nodeName, out Transform trans))
                    {
                        var finds = new List<Transform>();
                        FindChildNodesRecursive(anim.transform, nodeName, finds);
                        if (finds.Count == 0)
                        {
                            GPUSkinUtil.Msg($"骨骼{bone.Data.Name}不存在！", true);
                            return;
                        }
                        trans = finds[0];
                        nodeDic[nodeName] = trans;
                    }

                    BoneToModelMat[frame][boneIndex] = anim.transform.worldToLocalMatrix * trans.localToWorldMatrix;
                }
            }



            // 顶点绑定的骨骼数据
            for (int slotIndex = 0; slotIndex < drawSlotOrder.Length; slotIndex++)
            {
                Slot slot = drawSlotOrder[slotIndex];
                Attachment attachment = attachL[slotIndex];

                if (attachment is RegionAttachment)
                {
                    // 4个顶点绑定一个骨骼
                    var boneWeight = new Vector2[1] { new Vector2(slot.Bone.Data.Index, 1) };
                    for (int i = 0; i < 4; i++)
                    {
                        vertexBoneWeights.Add(boneWeight);
                    }
                    boneUsedIndex.Add(slot.Bone.Data.Index);
                }
                else if (attachment is MeshAttachment)
                {
                    // 多个顶点 绑定多个骨骼
                    var meshAttachment = attachment as MeshAttachment;
                    var bones = meshAttachment.Bones;
                    var vertices = meshAttachment.Vertices;

                    // 因为WorldVertices float数组存的是每个顶点的x和y，所以长度是顶点数2倍。
                    int count = meshAttachment.WorldVerticesLength >> 1;

                    if (bones == null)
                    {
                        // 多个顶点绑定一个骨骼
                        var boneWeight = new Vector2[1] { new Vector2(slot.Bone.Data.Index, 1) };
                        for (int i = 0; i < count; i++)
                        {
                            vertexBoneWeights.Add(boneWeight);
                        }
                        boneUsedIndex.Add(slot.Bone.Data.Index);
                    }
                    else
                    {
                        // 多个顶点 绑定多个骨骼  参照写法VertexAttachment.ComputeWorldVertices()
                        int v = 0;
                        int b = 0;


                        for (int i = 0; i < count; i++)
                        {
                            // bones 存储每个顶点绑定的骨骼序号，格式 {count} - 多个{index}
                            // (count + 1)个int为一组。
                            int n = bones[v++];  // 顶点绑定的骨骼总数
                            int start = v;
                            int end = v + n;
                            var boneWeight = new Vector2[n];
                            vertexBoneWeights.Add(boneWeight);
                            for (int k = start; k < end; k++)
                            {
                                int boneIndex = bones[v];
                                // vertices 存储每个顶点的骨骼空间坐标和骨骼权重，格式 {x},{y},{weight}
                                // 3个int为一组。
                                float weight = vertices[b + 2];
                                boneWeight[k - start] = new Vector2(boneIndex, weight);
                                b += 3;
                                v++;
                                boneUsedIndex.Add(boneIndex);
                            }
                        }
                    }
                }
            }

            //保存网格
            GenerateMeshByMappingBoneWeightToMeshUV();
            //输出信息
            var vertexBindBoneInfoDic = new Dictionary<int, int>();
            for (int i = 0; i < vertexBoneWeights.Count; i++)
            {
                var boneCount = vertexBoneWeights[i].Length;
                if (!vertexBindBoneInfoDic.ContainsKey(boneCount))
                    vertexBindBoneInfoDic[boneCount] = 0;
                vertexBindBoneInfoDic[boneCount]++;
            }
            var keyList = vertexBindBoneInfoDic.Keys.ToList();
            keyList.Sort();
            vertexBindBoneInfo = new string[keyList.Count];
            for (int i = 0; i < keyList.Count; i++)
            {
                int key = keyList[i];
                vertexBindBoneInfo[i] = $"绑定{key}个骨骼的顶点数为{vertexBindBoneInfoDic[key]}";
            }



            //得到初始姿势逆矩阵
            Matrix4x4[] inverseMat = new Matrix4x4[bonesCount];
            for (int i = 0; i < bonesCount; i++)
            {
                inverseMat[i] = BoneToModelMat[0][i].inverse;
            }
            //因为没有z轴变化，所以一个4X4矩阵只用存6个数就行。分别为m00,m01,m03,m10,m11,m13
            var totalFloats = (float)bonesCount * animFrameCount * 6;
            int totalFloatsSqrt = Mathf.CeilToInt(Mathf.Sqrt(totalFloats));
            int width = 32;
            for (; width <= 1024; width *= 2)
            {
                if (width >= totalFloatsSqrt)
                    break;
            }
            int lines = Mathf.CeilToInt(totalFloats / width);
            Texture2D texture = new Texture2D(width, lines, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] colors = new Color[width * lines];

            // 逐帧写入矩阵
            for (int i = 0; i < animFrameCount; i++)
            {
                // 写入变换后的矩阵
                for (int j = 0; j < bonesCount; j++)
                {
                    //绑定姿势的逆矩阵 左乘 骨骼的本地到世界变换矩阵 左乘 物体的世界到本地变换逆矩阵 = 最终变换矩阵
                    //推理: 骨骼的世界变换矩阵 左乘 物体的世界变换逆矩阵 = 每个骨骼的变换矩阵叠乘
                    Matrix4x4 matrix = BoneToModelMat[i][j] * inverseMat[j];
                    colors[(i * bonesCount + j) * 6 + 0] = GPUSkinUtil.EncodeFloatRGBA(matrix.m00);
                    colors[(i * bonesCount + j) * 6 + 1] = GPUSkinUtil.EncodeFloatRGBA(matrix.m01);
                    colors[(i * bonesCount + j) * 6 + 2] = GPUSkinUtil.EncodeFloatRGBA(matrix.m03);
                    colors[(i * bonesCount + j) * 6 + 3] = GPUSkinUtil.EncodeFloatRGBA(matrix.m10);
                    colors[(i * bonesCount + j) * 6 + 4] = GPUSkinUtil.EncodeFloatRGBA(matrix.m11);
                    colors[(i * bonesCount + j) * 6 + 5] = GPUSkinUtil.EncodeFloatRGBA(matrix.m13);
                }
            }
            texture.SetPixels(colors);
            texture.Apply();

            var path = string.Format("{0}/{1}_{2}_bone.png", OutPutDir, spineName, animName);
            GenerateTexture(texture, path);


            // 生成材质
            var mat = new Material(shader);
            mat.SetTexture("_MainTex", asset.atlasAssets[0].PrimaryMaterial.mainTexture);
            Texture texRes = AssetDatabase.LoadAssetAtPath<Texture>(path);
            mat.SetTexture("_AnimTex", texRes);
            mat.SetInt("_BoneCount", bonesCount);
            mat.SetInt("_FrameCount", animFrameCount);
            mat.SetFloat("_Interval", interval);
            mat.enableInstancing = true;


            path = string.Format("{0}/{1}.mat", OutPutDir, spineName);
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();


            GPUSkinUtil.Msg($"Material saved to {path}");
        }

        void FindChildNodesRecursive(Transform parent, string name, List<Transform> foundNodes)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    foundNodes.Add(child);
                }
                // 递归查找子节点
                FindChildNodesRecursive(child, name, foundNodes);
            }
        }

        // 生成模型网格
        private void GenerateMeshByMappingBoneWeightToMeshUV()
        {
            var mesh = anim.GetComponent<MeshFilter>().mesh;
            int length = vertexBoneWeights.Count;

            Vector2[] UV1 = new Vector2[length];
            Vector2[] UV2 = new Vector2[length];
            Vector2[] UV3 = new Vector2[length];
            Vector2[] UV4 = new Vector2[length];
            // Vector2[] UV5 = new Vector2[length];
            // Vector2[] UV6 = new Vector2[length];
            // Vector2[] UV7 = new Vector2[length];
            // Vector2[] UV8 = new Vector2[length];
            // Vector2[] UV9 = new Vector2[length];
            for (int i = 0; i < length; i++)
            {
                var b = vertexBoneWeights[i];
                UV1[i] = b[0];
                UV2[i] = b.Length > 1 ? b[1] : Vector2.zero;
                UV3[i] = b.Length > 2 ? b[2] : Vector2.zero;
                UV4[i] = b.Length > 3 ? b[3] : Vector2.zero;
                // UV5[i] = b.Length > 4 ? b[4] : Vector2.zero;
                // UV6[i] = b.Length > 5 ? b[5] : Vector2.zero;
                // UV7[i] = b.Length > 6 ? b[6] : Vector2.zero;
                // UV8[i] = b.Length > 7 ? b[7] : Vector2.zero;
                // UV9[i] = b.Length > 8 ? b[8] : Vector2.zero;
            }
            mesh.SetUVs(1, UV1);
            mesh.SetUVs(2, UV2);
            mesh.SetUVs(3, UV3);
            mesh.SetUVs(4, UV4);
            // mesh.SetUVs(5, UV5);
            // mesh.SetUVs(6, UV6);
            // mesh.SetUVs(7, UV7);
            // mesh.SetUVs(8, UV8);
            // mesh.SetUVs(9, UV9);

            //保存网格
            var copy = Instantiate(mesh);
            var path = string.Format("{0}/{1}.asset", OutPutDir, mesh.name);
            AssetDatabase.CreateAsset(copy, path);
            AssetDatabase.SaveAssets();
            
            GPUSkinUtil.Msg($"Mesh saved to {OutPutDir}");
        }

        // 生成动画纹理
        private void GenerateTexture(Texture2D tex, string path)
        {
            var bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            GPUSkinUtil.Msg(string.Format("Texture saved to {0}", path));
            AssetDatabase.Refresh();
            // 修改下导入设置。找到meta修改meta
            TextureImporter timporter = TextureImporter.GetAtPath(path) as TextureImporter;
            if (timporter)
            {
                TextureImporterSettings tis = new TextureImporterSettings();
                timporter.ReadTextureSettings(tis);
                tis.filterMode = FilterMode.Point;
                tis.npotScale = TextureImporterNPOTScale.None;
                tis.mipmapEnabled = false;
                timporter.SetTextureSettings(tis);
                //重新导入资产，以确保修改生效
                AssetDatabase.ImportAsset(path);
            }
        }


    }
}

#endif