using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OpenCvSharp;
using Script.Framework.UI;
using Script.UI.Component;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    public class ImageCompareTestPanel : BasePanel
    {
        [SerializeField] private Button LeftPathBtn;
        [SerializeField] private Button RightPathBtn;
        [SerializeField] private CheckBox SyncCB;
        [SerializeField] private ImageDetailComp LeftImage;
        [SerializeField] private ImageDetailComp RightImage;
        [SerializeField] private Button FirstBtn;

        string left_path;
        string right_path;


        void Awake()
        {
            _useScaleAnim = false;
            LeftPathBtn.onClick.AddListener(OnLeftPathBtnClick);
            RightPathBtn.onClick.AddListener(OnRightPathBtnClick);

            FirstBtn.onClick.AddListener(OnFirstBtnClick);
        }
        public override void SetData(object data)
        {

            SyncCB.SetData(true, OnSyncCB);
        }

        void Refresh()
        {

        }



        void OnLeftPathBtnClick()
        {
            string path = WU.OpenFileDialog("选择图片", Application.streamingAssetsPath, "图片 *.png *.jpg)|*.png;*.jpg");
            if (string.IsNullOrEmpty(path)) return;

            left_path = path;
            LeftImage.SetData(left_path);

            OnSyncCB(SyncCB.GetStatus());
        }
        void OnRightPathBtnClick()
        {
            string path = WU.OpenFileDialog("选择图片", Application.streamingAssetsPath, "图片 *.png *.jpg)|*.png;*.jpg");
            if (string.IsNullOrEmpty(path)) return;

            right_path = path;
            RightImage.SetData(right_path);

            OnSyncCB(SyncCB.GetStatus());
        }

        public override void Close()
        {
            base.Close();
            UIManager.Inst.ShowPanel(PanelEnum.TemplateMatchDrawResultPanel, new List<object> { null, 0 });
        }


        void OnFirstBtnClick()
        {
            CutOutImage();
        }

        // 截取
        // 整窗大小为 Size(1902, 839) 
        // 小地图为 P(1695, 43), Size(200, 200)
        void CutOutImage()
        {
            string path = WU.OpenFileDialog("选择图片", Application.streamingAssetsPath, "图片 *.png *.jpg)|*.png;*.jpg");
            if (string.IsNullOrEmpty(path)) return;

            var source = new Bitmap(path);
            if (source.Width != 1902 && source.Height != 839)
            {
                DU.LogWarning("[CutOutImage] 原图片尺寸不对");
                source.Dispose();
                return;
            }
            var target = IU.CutOutImage(source, new CVRect(1695, 43, 200, 200));
            var name = Path.GetFileNameWithoutExtension(path);
            IU.SaveBitmap(target, "SmallMap", $"{name}_cutout");
            DU.LogWarning("[CutOutImage] 成功");

            source.Dispose();
            target.Dispose();
        }

        void OnSyncCB(bool isOn)
        {
            if (isOn)
            {
                LeftImage.Change(
                    f => RightImage.ScaleTo(f),
                    v2 => RightImage.ScrollTo(v2),
                    v2 => RightImage.SelectPixel(v2)
                );
                RightImage.Change(
                    f => LeftImage.ScaleTo(f),
                    v2 => LeftImage.ScrollTo(v2),
                    v2 => LeftImage.SelectPixel(v2)
                );
            }
            else
            {
                LeftImage.Change(null, null, null);
                RightImage.Change(null, null, null);
            }
        }
    }
}