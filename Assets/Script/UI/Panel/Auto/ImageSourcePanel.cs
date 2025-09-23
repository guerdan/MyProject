
using System;
using System.Collections.Generic;
using System.IO;
using Script.Framework.AssetLoader;
using Script.Framework.UI;
using Script.UI.Component;
using Script.Util;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;



namespace Script.UI.Panel.Auto
{

    public class ImageSourcePanel : BasePanel
    {
        [SerializeField] private Button CaptureBtn;
        [SerializeField] private Button FindPathBtn;
        [SerializeField] private Button UserBtn;
        [SerializeField] private ImageLoadComp TemplateImage;
        [SerializeField] private Text SavePathText;
        [SerializeField] private Button SaveBtn;
        string _configPath;    //配置路径
        bool _configPathExist;
        string _externalPath;
        Action<string> _onSave;



        void Awake()
        {
            CaptureBtn.onClick.AddListener(OnCaptureBtnClick);
            FindPathBtn.onClick.AddListener(OnFindPathBtnClick);
            UserBtn.onClick.AddListener(OnUserBtnClick);
            SaveBtn.onClick.AddListener(OnSaveBtnClick);
        }

        public override void SetData(object list)
        {
            _useScaleAnim = false;

            // 路径，赋值Action
            var dataList = list as List<object>;
            _configPath = dataList[0] as string;
            _onSave = dataList[1] as Action<string>;

            Refresh();
        }

        void Refresh(bool save = false)
        {
            _configPathExist = File.Exists(ImageManager.GetFullPath(_configPath));
            if (_configPathExist)
            {
                TemplateImage.SetData(ImageManager.GetFullPath(_configPath), new Vector2(180, 180));
                SavePathText.text = _configPath;
                SaveBtn.GetComponentInChildren<Text>().text = "更改";
                if (save)
                    _onSave?.Invoke(_configPath);
            }
            else
            {
                TemplateImage.SetData("", new Vector2(180, 180));
                SavePathText.text = "";
                SaveBtn.GetComponentInChildren<Text>().text = "保存";
            }
        }

        void OnCaptureBtnClick()
        {

        }
        //
        void OnFindPathBtnClick()
        {
            // 打开资源管理器 => 选择一个图片
            string path = WU.OpenFileDialog("选择图片", "", "图片 *.png *.jpg)|*.png;*.jpg");
            if (string.IsNullOrEmpty(path)) return;

            bool InStreaming = path.StartsWith(Application.streamingAssetsPath);

            if (InStreaming)
            {
                _configPath = GetConfigPath(path);
                Refresh(true);
            }
            else
            {
                TemplateImage.SetData(path, new Vector2(180, 180));
                _externalPath = path;
            }

        }
        // 用户已有资源
        void OnUserBtnClick()
        {
            string path = WU.OpenFileDialog("选择图片", Application.streamingAssetsPath,"图片 *.png *.jpg)|*.png;*.jpg");
            if (string.IsNullOrEmpty(path)) return;
            bool InStreaming = path.StartsWith(Application.streamingAssetsPath);
            if (!InStreaming) return;

            _configPath = GetConfigPath(path);
            Refresh(true);
        }

        // 复制到StreamingAssets下
        void OnSaveBtnClick()
        {
            string fileName = ".png";
            string title = "保存图片";
            if (!_configPathExist)  // 更改路径
            {
                fileName = Path.GetFileName(_externalPath);
                title = "更改路径";
            }

            string savePath = WU.SaveFileDialog(title
            , Application.streamingAssetsPath + $"/{fileName}", "图片 *.png *.jpg)|*.png;*.jpg");

            // 需选择正确的项目内路径
            if (string.IsNullOrEmpty(savePath) || !savePath.StartsWith(Application.streamingAssetsPath))
                return;


            if (_configPathExist)  // 更改路径
            {
                var path = GetConfigPath(savePath);
                if (path == _configPath)
                    return;

                File.Copy(ImageManager.GetFullPath(_configPath), savePath, true);
                // 删除旧文件
                //
                File.Delete(ImageManager.GetFullPath(_configPath));
            }
            else
            {
                File.Copy(_externalPath, savePath, true);
            }
            Debug.Log($"图片已保存到: {_configPath}");

            _configPath = GetConfigPath(savePath);
            Refresh(true);
        }

        // source:绝对路径
        string GetConfigPath(string source)
        {
            int stream_len = Application.streamingAssetsPath.Length + 1;
            var path = source.Substring(stream_len, source.Length - stream_len - 4); // 去掉.png
            return path;
        }
    }
}