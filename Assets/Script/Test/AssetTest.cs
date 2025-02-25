using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Script.Framework;
using Script.Framework.AssetLoader;
using Script.Framework.UI;
using Script.Util;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Random = System.Random;

namespace Script.Test
{
    public class AssetTest : MonoBehaviour
    {
        public Image image0;
        public Image image1;



        List<string> imgPaths0 = new List<string>(){
            "Common/Sprites/bg_pvp_myrank",
            "Common/Sprites/icon_pvp_attack",
            "Common/Sprites/icon_pvp_defence",
            "Common/Sprites/icon_pvp_defence_en",
            "Common/Sprites/icon_pvp_fail",
            "Common/Sprites/icon_pvp_fail_en",
            "Common/Sprites/icon_pvp_rank1",
            "Common/Sprites/icon_pvp_rank2",
            "Common/Sprites/icon_pvp_rank3",
            "Common/Sprites/icon_pvp_score",
            "Common/Sprites/icon_pvp_win",
            "Common/Sprites/icon_pvp_win_en",
            "Common/Sprites/rank_icon_log",
            "Common/Sprites/rank_icon_reward",
        };
        List<string> imgPaths1 = new List<string>(){
            "PreCommon/Sprites/rank_my_bg01",
            "PreCommon/Sprites/rank_no1_bg01",
            "PreCommon/Sprites/rank_no2_bg01",
            "PreCommon/Sprites/rank_no3_bg01",
            "PreCommon/Sprites/rank_no4_bg01",
        };
        Random random = new Random();

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                SetImage0();
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                UIManager.Inst.ClearCache();
            }
            if (Input.GetKeyDown(KeyCode.L))
            {
                LookMemory();
            }
        }
        void SetImage0()
        {
            AssetUtil.SetImage(imgPaths1[random.Next(0, imgPaths1.Count)], image0);
        }
        void SetImage1()
        {
            AssetUtil.SetImage(imgPaths1[random.Next(0, imgPaths1.Count)], image1);
        }
        //测试了 LoadPrefab接口，从内存监视器上看没问题
        void TestLoadPrefab()
        {
            AssetUtil.LoadPrefab("Common/Prefab/Image1", (prefab) =>
            {
                var go = Instantiate(prefab);
                go.transform.SetParent(transform, false);
                image1 = go.GetComponent<Image>();
                return go;
            });
        }
        void Destory1()
        {
            if (image1 == null) return;
            Destroy(image1.gameObject);
            image1 = null;
        }

        void LookMemory()
        {
            Type addressablesType = typeof(Addressables);
            FieldInfo m_AddressablesField = addressablesType.GetField("Instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (m_AddressablesField != null)
            {
                var m_Addressables = m_AddressablesField.GetValue(null);
            }
        }

        private List<Image> images = new List<Image>();
        private Image CreateImage(Vector2 pos)
        {
            var go = new GameObject("Image");
            var image = go.AddComponent<Image>();
            image.transform.SetParent(transform, false);
            var rectran = image.GetComponent<RectTransform>();
            rectran.anchoredPosition = pos;
            rectran.sizeDelta = new Vector2(100, 100);
            return image;
        }

        int stepType1 = 0;
        //测试了 SetImage接口
        void TestReleaseBundleCondition()
        {
            switch (stepType1)
            {
                case 0:
                    {
                        var img = CreateImage(new Vector2(-100, 200));
                        var img1 = CreateImage(new Vector2(100, 200));
                        images.Add(img);
                        images.Add(img1);
                    }
                    break;
                case 1:
                    {
                        var img = images[0];
                        var img1 = images[1];
                        AssetUtil.SetImage(imgPaths1[random.Next(0, imgPaths1.Count)], img);
                        AssetUtil.SetImage(imgPaths1[random.Next(0, imgPaths1.Count)], img1);
                    }
                    break;
                case 2:
                    {
                        var img = images[0];
                        Destroy(img.gameObject);
                        images.RemoveAt(0);
                    }
                    break;
                case 3:
                    {
                        var img = images[0];
                        Destroy(img.gameObject);
                        images.RemoveAt(0);
                    }
                    break;
            }

            if (stepType1 == 3)
                stepType1 = 0;
            else
                stepType1++;
        }


    }
}