using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using OpenCvSharp;
using Script.Framework.UI;
using Script.Model.Auto;
using Script.Test;
using Script.UI.Components;
using Script.Util;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    /// <summary>
    /// 参考坐标系是 左下角为原点，X轴向右，Y轴向上
    /// </summary>
    public class ImageCompareTestPanel : BasePanel
    {

        [SerializeField] private Button LeftPathBtn;
        [SerializeField] private Button LeftMemoryBtn;
        [SerializeField] private Button RightPathBtn;
        [SerializeField] private Button RightMemoryBtn;
        [SerializeField] private CheckBox SyncCB;
        [SerializeField] private ImageDetailComp LeftImage;
        [SerializeField] private ImageDetailComp RightImage;
        [SerializeField] private Button Btn1th;
        [SerializeField] private Button Btn2th;
        [SerializeField] private Button Btn3th;
        [SerializeField] private Button Btn4th;
        [SerializeField] private InputField InputText;
        [SerializeField] private KeywordTipsComp TipsComp;

        string left_path;
        string right_path;
        Texture2D _sourceTex;
        Mat _sourceMat;
        // 在托管与非托管之间共享的内存。没有维护开销
        NativeArray<Color32> _pixels;
        int _imgW;
        int _imgH;
        Sprite _targetSpr;

        // 以下为业务模块
        Vector2Int _playerPos = Vector2Int.left;        //参考坐标系的原点在地图左下角


        void Awake()
        {
            _useScaleAnim = false;
            LeftPathBtn.onClick.AddListener(OnClickLeftPathBtn);
            LeftMemoryBtn.onClick.AddListener(() => OnClickMemoryBtn(true));
            RightPathBtn.onClick.AddListener(OnClickRightPathBtn);
            RightMemoryBtn.onClick.AddListener(() => OnClickMemoryBtn(false));

            Btn1th.onClick.AddListener(OnClickBtn1th);
            Btn2th.onClick.AddListener(OnClickBtn2th);
            Btn3th.onClick.AddListener(OnClickBtn3th);
            Btn4th.onClick.AddListener(OnClickBtn4th);

            Utils.SetActive(TipsComp, false);
        }
        public override void SetData(object data)
        {

            Utils.SetActive(LeftMemoryBtn, false);
            Utils.SetActive(RightMemoryBtn, false);

            if (data is string[] list)
            {
                RightImage.ClearData();
                LeftImage.ClearData();
                _mapId = list[0];
                _scriptId = list[1];
                RefreshMapPanel();
            }
            else
            {
                var path = @"C:\Users\hp\Desktop\path\图\0.2间隔\31.png";
                LoadLeftImage(path);
            }

            InputText.text = "(95,120,279,65)";
            SyncCB.SetData(false, OnSyncCB);
            OnSyncCB(SyncCB.GetStatus());
        }

        #region 拍摄地图

        string _mapId;
        string _scriptId;
        Texture2D _mapLTexture;
        Sprite _mapLSprite;
        Texture2D _mapRTexture;
        Sprite _mapRSprite;

        bool _clickLeft;
        int[] _optionSelectStatus = new int[] { -1, -1 };
        Action<Vector2Int> _onSelectPixel;
        void RefreshMapPanel()
        {
            Utils.SetActive(LeftMemoryBtn, true);
            Utils.SetActive(RightMemoryBtn, true);

            _clickLeft = true;
            OnSelectTipsComp(0);
        }

        public static List<string> MapOptions = new List<string>()
            {
                "_map",             // 像素粒度
                "_grid",            // 5X5大格子粒度
                "A*寻路",           // 5X5大格子粒度
                "指定目标寻路",      // 5X5大格子粒度
                "最近迷雾",
                "最近迷雾 (跟踪)",
                "_light_map",
                "_small_map",
                "_judge_map",
                "_fog_map",
                "保存全部地图",       // 保存至本地
            };

        void OnClickMemoryBtn(bool isLeft)
        {
            _clickLeft = isLeft;
            Utils.SetActive(TipsComp, true);

            var comp = _clickLeft ? LeftMemoryBtn : RightMemoryBtn;


            TipsComp.SetData(MapOptions, OnSelectTipsComp, 140, 7);
            TipsComp.SetCurIndex(_optionSelectStatus[_clickLeft ? 0 : 1]);

            var tipsCompRectT = TipsComp.GetComponent<RectTransform>();
            var pos = Utils.GetPos(tipsCompRectT, comp.GetComponent<RectTransform>()
                , new Vector2(25, 8), true);
            tipsCompRectT.anchoredPosition = pos;
        }
        void OnSelectTipsComp(int option_int)
        {
            if (option_int < 0)
                return;


            ImageDetailComp comp = _clickLeft ? LeftImage : RightImage;
            GetSpriteOfMapOption(_scriptId, _mapId, (Options)option_int, InputText.text
                                , out var mapSprite, out var line_offset, out _, comp);

            if (mapSprite == null)
                return;

            _optionSelectStatus[_clickLeft ? 0 : 1] = option_int;
            comp.SetData(mapSprite);
            comp.SetLineOffset(line_offset);

        }

        #region MapOption

        /// <summary>
        /// 调用者实例 与 需求资源 绑定关系缓存。调用者实例销毁时，需要通知这里清理缓存
        /// </summary>
        static Dictionary<Component, (Texture2D, Sprite)> _mapOptionCache = new Dictionary<Component, (Texture2D, Sprite)>();
        public static void GetSpriteOfMapOption(string scriptId, string mapId, Options option, string command,
                         out Sprite sprite, out Vector2Int line_offset, out Vector2Int anchor,
                        Component cacheKey = null)
        {
            sprite = null;
            line_offset = default;
            anchor = default;

            MapData mapData = MapDataManager.Inst.Get(mapId);
            if (mapData == null) return;

            mapData.GetContentAttr(out Vector2Int xRange, out Vector2Int yRange
                , out var w, out var h);
            if (w == 0) return;


            anchor = new Vector2Int(xRange.x, yRange.x);

            // 使用 rgba 格式
            // Texture2D 是以左下角为像素坐标系原点，而 Mat、Bitmap是左上角
            Color32[] pixels = null;
            Vector2Int size = new Vector2Int(w, h);
            // DU.RunWithTimer(() =>
            // {
            if (option == Options.Map)
            {
                pixels = mapData.GetImageMap0();
            }
            else if (option == Options.Grid)
            {
                pixels = mapData.GetImageGrid();
            }
            else if (option == Options.AStartFunc)
            {
                var str = command;
                str.Replace(" ", "");
                str = str.Substring(1, str.Length - 2);
                var arr = str.Split(',');
                Vector2Int start;
                Vector2Int target;
                try
                {
                    start = new Vector2Int(int.Parse(arr[0]), int.Parse(arr[1]));
                    target = new Vector2Int(int.Parse(arr[2]), int.Parse(arr[3]));
                }
                catch
                {
                    DU.LogError("[A*寻路 打印图像] 输入文本格式不对");
                    return;
                }

                DU.RunWithTimer(() =>
                    mapData.StartAStarBigGrid(start, target)
                    , "StartAStarBigGrid");
                pixels = mapData.GetImageGridAStar();

            }
            else if (option == Options.SearchTargetFunc)
            {
                var str = command;
                int target_index = 0;
                try
                {
                    target_index = int.Parse(str);
                }
                catch
                {
                    DU.LogError("[指定目标寻路 打印图像] 输入文本格式不对");
                    return;
                }
                DU.RunWithTimer(() =>
                    mapData.StartAStarByIndex(target_index)
                , "StartAStarBigGrid");

                // 可能会变
                //
                mapData.GetContentAttr(out xRange, out yRange
               , out w, out h);
                size = new Vector2Int(w, h);

                pixels = mapData.GetImageGridAStar();
            }
            else if (option == Options.FindNearestFog)
            {
                mapData.FindNearestFog();
                pixels = mapData.GetImageToNearestFog();
            }
            else if (option == Options.FindNearestFogFollowing)
            {
                size = new Vector2Int(200, 200);
                mapData.FindNearestFog();
                pixels = mapData.GetImageToNearestFogFollowing(size.x, size.y);
            }
            else if (option == Options.LightMap)
            {
                pixels = mapData.GetImageLightMap();
            }
            else if (option == Options.SmallMap)
            {
                size = new Vector2Int(200, 200);
                pixels = mapData.GetImageSmallMap();
            }
            else if (option == Options.JudgeMap)
            {
                pixels = mapData.GetImageJudgeMap();
            }
            else if (option == Options.FogMap)
            {
                pixels = mapData.GetImageFogMap();
            }
            else if (option == Options.SaveAllMap)
            {
                //保存
                var script = AutoScriptManager.Inst.GetScriptData(scriptId);
                string dir = $"{script.GetCapturePath()}/拍摄地图节点";
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                mapData.Save($"{dir}/map.png");
                return;
            }
            // }, "MapData");

            if (pixels.Length == 0)
                return;

            Texture2D mapTexture = null;
            if (cacheKey != null && _mapOptionCache.TryGetValue(cacheKey, out var val)
                && val.Item1.width == size.x && val.Item1.height == size.y)
            {
                // 有缓存。并且长宽都一致，就复用。
                mapTexture = val.Item1;
                sprite = val.Item2;
            }
            else
            {
                // 否则。新建并清理缓存
                mapTexture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
                sprite = Sprite.Create(mapTexture, new UnityEngine.Rect(0, 0, size.x, size.y), new Vector2(0.5f, 0.5f));

                if (cacheKey != null)
                {
                    ClearMapOptionCache(cacheKey);
                    _mapOptionCache[cacheKey] = (mapTexture, sprite);
                }
            }

            pixels = IU.Color32ReverseVertical(pixels, size.x);

            // 应用像素数据到纹理
            mapTexture.SetPixels32(pixels);
            mapTexture.Apply();

            line_offset = new Vector2Int(-(xRange.x % 5), -(yRange.x % 5));
        }

        public static void ClearMapOptionCache(Component cacheKey)
        {
            if (_mapOptionCache.TryGetValue(cacheKey, out var val))
            {
                Destroy(val.Item1);
                Destroy(val.Item2);
                _mapOptionCache.Remove(cacheKey);
            }
        }

        #endregion
        

        #endregion

        void OnClickLeftPathBtn()
        {
            // string init_path = Application.streamingAssetsPath;
            string init_path = @"D:\unityProject\MyProject\TestResource\图";
            string path = WU.OpenFileDialog("选择图片", init_path, "图片 *.png *.jpg)|*.png;*.jpg");
            LoadLeftImage(path);
        }

        void LoadLeftImage(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            ClearMat();

            _sourceMat = IU.GetMat(path);

            left_path = path;
            LeftImage.SetData(left_path, Path.GetFileName(path));

            _sourceTex = LeftImage.Image.sprite.texture;
            // 是获取托管像素数组，修改的是托管内存
            //
            _pixels = _sourceTex.GetPixelData<Color32>(0);
            // 是从托管像素数组拷贝一份数组
            // tex.GetPixels32(_pixels, 0);
            _imgW = _sourceTex.width;
            _imgH = _sourceTex.height;
        }

        void OnClickRightPathBtn()
        {
            string path = WU.OpenFileDialog("选择图片", Application.streamingAssetsPath, "图片 *.png *.jpg)|*.png;*.jpg");
            if (string.IsNullOrEmpty(path)) return;

            right_path = path;
            RightImage.SetData(right_path);
        }

        #region Update
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space) && _mapData != null && _index <= _max_index)
            {
                var file_path = _debug_dir + $"/{_index++}.png";
                _mapData.Capture(new Bitmap(file_path));

                _clickLeft = false;
                OnSelectTipsComp(_optionSelectStatus[_clickLeft ? 0 : 1]);

                _clickLeft = true;
                OnSelectTipsComp(_optionSelectStatus[_clickLeft ? 0 : 1]);

                _mapData.PrintResult();
            }
        }

        #endregion
        void OnSyncCB(bool isOn)
        {
            if (isOn)
            {
                LeftImage.SyncOnScaleChange(f => RightImage.ScaleTo(f));
                LeftImage.SyncOnScroll(v2 => RightImage.ScrollTo(v2));
                LeftImage.SyncOnSelectPixel(v2 =>
                {
                    RightImage.SelectPixel(v2);
                    if (_onSelectPixel != null) _onSelectPixel(v2);
                });

                RightImage.SyncOnScaleChange(f => LeftImage.ScaleTo(f));
                RightImage.SyncOnScroll(v2 => LeftImage.ScrollTo(v2));
                RightImage.SyncOnSelectPixel(v2 => LeftImage.SelectPixel(v2));

            }
            else
            {
                LeftImage.SyncOnScaleChange(null);
                LeftImage.SyncOnScroll(null);
                LeftImage.SyncOnSelectPixel(null);

                RightImage.SyncOnScaleChange(null);
                RightImage.SyncOnScroll(null);
                RightImage.SyncOnSelectPixel(null);
            }
        }


        #region 找角色坐标

        // 模板匹配找角色位置
        void FindRole()
        {
            // 还是来一遍匹配，因为有多人情况
            string templatePath = Application.streamingAssetsPath + "/MatchTemplate/role_1P.png";

            using (var template = IU.GetMat(templatePath, true))
            {
                int t_width = template.Width;
                int t_height = template.Height;

                using (var resultMat = IU.MatchTemplate1(_sourceMat, template))
                {
                    var result = IU.FindResult(resultMat, t_width, t_height, 0.9f, out var _);
                    if (result.Count > 0)
                    {
                        var center = result[0].Rect.GetCenterPixel();
                        var playerPos = new Vector2(center.x, _imgH - 1 - center.y);
                        _playerPos = new Vector2Int((int)Mathf.Round(playerPos.x), (int)Mathf.Round(playerPos.y));
                        DU.LogWarning($"[FindRole] 匹配度：{result[0].Score} 角色坐标：{_playerPos}");
                    }
                }
            }
        }
        #endregion

        // 
        void ChangeImage()
        {
            if (_sourceTex == null) return;

            for (int i = 0; i < _imgH; i++)
            {
                for (int j = 0; j < _imgW; j++)
                {
                    int index = i * _imgW + j;
                    var color = _pixels[index];
                    color.a = 255;
                    _pixels[index] = color;
                }
            }

            _sourceTex.Apply();
        }
        #region 筛选






        #region FilterPixel
        void FilterPixel()
        {
            if (_sourceTex == null) return;

            // var str = Input.text;
            // str = str.Replace(" ", "");
            // if (str == "") return;
            // str = str.Substring(1, str.Length - 2);
            // var arr = str.Split(',');
            // if (arr.Length != 3) return;
            // if (!float.TryParse(arr[0], out float r)) return;
            // if (!float.TryParse(arr[1], out float g)) return;
            // if (!float.TryParse(arr[2], out float b)) return;
            // var target = new Color32((byte)r, (byte)g, (byte)b, 255);

            var color_set = 1;

            Color32 min_1th = default;
            Color32 max_1th = default;
            Vector2Int B_to_G_1th = default;
            Color32 min_2th = default;
            Color32 max_2th = default;
            Color32 min_3th = default;
            Color32 max_3th = default;
            Vector2Int B_to_G_3th = default;

            if (color_set == 1)
            {
                // 第1版  颜色偏蓝   
                //
                // 1类边界
                min_1th = new Color32(110, 110, 150, 255);
                max_1th = new Color32(145, 145, 195, 255);
                B_to_G_1th = new Vector2Int(30, 60);
                // 2类边界
                min_2th = new Color32(86, 88, 90, 255);
                max_2th = max_1th;
                // 1类迷雾
                min_3th = new Color32(0, 130, 170, 255);
                max_3th = new Color32(90, 142, 192, 255);
                B_to_G_3th = new Vector2Int(38, 55);
            }
            else
            {
                // 第2版 偏白
                //
                min_1th = new Color32(117, 123, 128, 255);
                max_1th = new Color32(173, 165, 180, 255);
                B_to_G_1th = new Vector2Int(30, 60);
                min_2th = new Color32(86, 88, 90, 255);
                max_2th = max_1th;
                min_3th = new Color32(65, 120, 135, 255);
                max_3th = new Color32(100, 140, 170, 255);
                B_to_G_3th = new Vector2Int(5, 30);
            }



            var x_start = 2;
            var x_end = _imgW + 2;
            var y_start = 2;
            var y_end = _imgH + 2;
            //colorData中，0-未定义,1-空地,2和5-边界，10和11-迷雾
            int[,] colorData = new int[_imgW + 4, _imgH + 4];

            // var fit_one_list = new List<Vector2Int>();
            var first_list = new List<Vector2Int>();

            var fog_constant = new Vector2Int[]{
                new Vector2Int(-2,0),new Vector2Int(-1,0),new Vector2Int(1,0),new Vector2Int(2,0),
                new Vector2Int(-1,1),new Vector2Int(0,1),new Vector2Int(1,1),
                new Vector2Int(-1,-1),new Vector2Int(0,-1),new Vector2Int(1,-1),
                new Vector2Int(0,2),new Vector2Int(0,-2),
            };


            DU.RunWithTimer(() =>
            {
                DU.RunWithTimer(() =>
                {
                    // 先处理迷雾
                    for (int i = y_start; i < y_end; i++)
                        for (int j = x_start; j < x_end; j++)
                        {
                            int index = (i - 2) * _imgW + j - 2;
                            var color = _pixels[index];
                            byte r = color.r;
                            byte g = color.g;
                            byte b = color.b;

                            if (r <= 3 && g <= 3 && b <= 3) // 文字描边
                                colorData[j, i] = 0;

                            else if (r < 50 && g < 50 && b < 50) // 空地
                                colorData[j, i] = 1;

                            else if (Between(color, min_3th, max_3th)
                                && b - g >= B_to_G_3th.x && b - g <= B_to_G_3th.y)
                            {
                                // fog_first_list.Add(new Vector2Int(j, i));
                                colorData[j, i] = 10;
                                // max_r = color.r > max_r ? color.r : max_r;

                                foreach (var offset in fog_constant)
                                {
                                    int px = j + offset.x;
                                    int py = i + offset.y;
                                    var data = colorData[px, py];
                                    if (data != 10)
                                        colorData[px, py] = 11;
                                }
                            }
                        }


                    // 再处理边界
                    for (int i = y_start; i < y_end; i++)
                        for (int j = x_start; j < x_end; j++)
                        {
                            int index = (i - 2) * _imgW + j - 2;
                            var color = _pixels[index];
                            byte r = color.r;
                            byte g = color.g;
                            byte b = color.b;
                            if (colorData[j, i] != 0)
                                continue;

                            if (Between(color, min_1th, max_1th)
                                && b - g >= B_to_G_1th.x && b - g <= B_to_G_1th.y)
                            {
                                first_list.Add(new Vector2Int(j, i));
                                colorData[j, i] = 2;
                            }
                            else if (Between(color, min_2th, max_2th))
                            {
                                colorData[j, i] = 3;
                            }
                        }



                }, "Condition check");

                Traversal(colorData, first_list);


            }, "GenerateMap");

        #endregion

            var temp = new int[_imgW, _imgH];

            for (int i = y_start; i < y_end; i++)
                for (int j = x_start; j < x_end; j++)
                    if (colorData[j, i] == 3)
                        temp[j - 2, i - 2] = 0;
                    else
                        temp[j - 2, i - 2] = colorData[j, i];

            colorData = temp;






            Texture2D texture = new Texture2D(_imgW, _imgH, TextureFormat.RGBA32, false);

            // 填充纹理数据
            Color32[] pixels = new Color32[_imgW * _imgH];
            for (int i = 0; i < _imgH; i++)
                for (int j = 0; j < _imgW; j++)
                {
                    int index = i * _imgW + j;
                    var data = colorData[j, i];
                    if (data == 0)
                        pixels[index] = new Color32(128, 128, 128, 255);
                    if (data == 1)          // 空地
                        pixels[index] = new Color32(0, 0, 0, 255);
                    else if (data == 2)     // 边界     
                        pixels[index] = new Color32(255, 255, 255, 255);
                    else if (data == 5)     // 若边界                
                        pixels[index] = new Color32(255, 0, 0, 255);
                    else if (data == 10)    // 迷雾
                        pixels[index] = new Color32(0, 0, 255, 255);
                    else if (data == 11)    // 弱迷雾
                        pixels[index] = new Color32(0, 0, 180, 255);

                }
        #endregion


            // 应用像素数据到纹理
            texture.SetPixels32(pixels);
            texture.Apply();

            // 清理
            //
            if (_targetSpr != null)
            {
                Destroy(_targetSpr);
                Destroy(_targetSpr.texture);
                _targetSpr = null;
            }
            _targetSpr = Sprite.Create(texture, new UnityEngine.Rect(0, 0, _imgW, _imgH), new Vector2(0.5f, 0.5f));
            RightImage.SetData(_targetSpr);


            // DU.LogWarning($"max_r: {max_r}");

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // 告诉编译器要内联
        bool Between(Color32 val, Color32 min, Color32 max)
        {
            return val.r >= min.r && val.g >= min.g && val.b >= min.b
                && val.r <= max.r && val.g <= max.g && val.b <= max.b;
        }

        /// <summary>
        /// 遍历
        /// 递归,发散型递归。感觉用栈可以实现非递归
        /// 猜想优化点：将colorData扩展为[w+1,h+1]，边缘设置为0，这样就不用每次都判断边界了
        /// </summary>
        void Traversal(int[,] colorData, List<Vector2Int> first_list)
        {
            int y_end = _imgH - 1;
            int x_end = _imgW - 1;

            Vector2Int[] stack = new Vector2Int[first_list.Count * 4];
            // var count_debug = 0;
            var count = 0;
            foreach (var pos in first_list)
            {
                stack[count++] = pos;
            }

            while (count > 0)
            {
                var pop = stack[--count];
                int x = pop.x;
                int y = pop.y;


                foreach (var offset in Utils.EightDirList)
                {
                    int px = offset.x + x;
                    int py = offset.y + y;
                    if (colorData[px, py] == 3)
                    {
                        colorData[px, py] = 5;
                        stack[count++] = new Vector2Int(px, py);
                    }

                }


            }
        }


        /// <summary>
        /// 生成地图。colorData中，0-为不可通行空地，1-为可通行区域，2-边界
        /// </summary>
        void GenerateMap(int[,] colorData)
        {
            int y_end = _imgH - 1;
            int x_end = _imgW - 1;

            // Stack<Vector2Int> stack = new Stack<Vector2Int>();
            // stack.Push(_playerPos);

            Vector2Int[] stack = new Vector2Int[_imgW * _imgH];
            var count = 0;
            stack[count++] = _playerPos;
            while (count > 0)
            {
                // var pop = stack.Pop();
                var pop = stack[--count];
                int x = pop.x;
                int y = pop.y;

                if (colorData[x, y] == 0)
                {
                    colorData[x, y] = 1; // 标记为"可通行空地"
                }

                if (y > 0 && colorData[x, y - 1] == 0)
                    stack[count++] = new Vector2Int(x, y - 1);
                if (x > 0 && colorData[x - 1, y] == 0)
                    stack[count++] = new Vector2Int(x - 1, y);
                if (y < y_end && colorData[x, y + 1] == 0)
                    stack[count++] = new Vector2Int(x, y + 1);
                if (x < x_end && colorData[x + 1, y] == 0)
                    stack[count++] = new Vector2Int(x + 1, y);

            }
        }


        void ClearMat()
        {
            if (_sourceMat != null)
            {
                _sourceMat.Dispose();
                _sourceMat = null;
            }
            if (_sourceTex != null)
            {
                // Destroy(_sourceTex);  //有人回收
                _sourceTex = null;
            }

            _imgW = 0;
            _imgH = 0;
        }

        #region Btn 1

        MapData _mapData;

        void OnClickBtn1th()
        {
            // FindRole();
            // LeftImage.SelectPixel(_playerPos);

            // var gridData = new GridData(null);
            // var cell = new BigCell(0, 0);
            // var map = new CellType[7, 7];
            // var map_temp = new int[7, 7]
            // {
            //     { 1, 1, 1, 1, 1, 1, 1 },
            //     { 1, 2, 1, 1, 1, 1, 1 },
            //     { 1, 1, 2, 2, 2, 1, 1 },
            //     { 1, 1, 2, 2, 2, 1, 1 },
            //     { 1, 1, 2, 2, 2, 1, 1 },
            //     { 1, 1, 1, 1, 1, 1, 1 },
            //     { 1, 1, 1, 1, 1, 1, 1 },
            // };

            // var len = map_temp.GetLength(0);
            // for (int i = 0; i < len; i++)
            //     for (int j = 0; j < len; j++)
            //     {
            //         map[j, len - 1 - i] = (CellType)map_temp[j, i];
            //     }

            // gridData.RefreshCell(cell, map, 1, 1,true);
            // DU.LogWarning($"方向 {Convert.ToString((int)cell.Direction, 2).PadLeft(8, '0')}");


            var map = new bool[7, 7];
            var map_temp = new int[7, 7]
            {
                { 1, 1, 1, 1, 1, 1, 1 },
                { 1, 2, 1, 1, 1, 1, 1 },
                { 1, 1, 2, 2, 2, 1, 1 },
                { 1, 1, 2, 2, 2, 1, 1 },
                { 1, 1, 2, 2, 2, 1, 1 },
                { 1, 1, 1, 1, 1, 1, 1 },
                { 1, 1, 1, 1, 1, 1, 1 },
            };

            var len = map_temp.GetLength(0);
            for (int i = 0; i < len; i++)
                for (int j = 0; j < len; j++)
                {
                    map[j, len - 1 - i] = map_temp[j, i] == 1;
                }

            SmallCellFinder finder = new SmallCellFinder();
            var result = finder.BeginAStar(map, new Vector2Int(2, 1), new Vector2Int(6, 6));

        }

        #endregion
        #region Btn 2

        void OnClickBtn2th()
        {
            FilterPixel();

            //  比较下谁执快
            // TestExecutionTime.Inst.Test1();
            // TestExecutionTime.Inst.Test2();
            // TestExecutionTime.Inst.Test3();
            // TestExecutionTime.Inst.Test4();
            // TestExecutionTime.Inst.Test5();
        }
        #endregion
        #region Btn 3 
        int _index;
        int _max_index;
        string _debug_dir;

        void OnClickBtn3th()
        {
            // if (_mapData == null)
            // {
            MapDataManager.Inst.Remove("Map-22");
            MapDataManager.Inst.Create("Map-22", new CVRect(0, 0, 200, 200), 0);
            _mapData = MapDataManager.Inst.Get("Map-22");
            // }

            // string path = WU.OpenFileDialog("选择图片", Application.streamingAssetsPath, "图片 *.png *.jpg)|*.png;*.jpg");
            // if (string.IsNullOrEmpty(path)) return;
            // _mapData.Capture(new Bitmap(path));


            // var dir = @"D:\unityProject\MyProject\Assets\StreamingAssets\SmallMap\小地图_22";

            _index = 1;
            _max_index = 40;
            // _debug_dir = @"D:\unityProject\MyProject\TestResource\图\0.2间隔";
            _debug_dir = @"Assets\StreamingAssets\SmallMap\Map-22";

            // for (_index = 31; _index <= 111; _index++)
            // {
            //     var file_path = _debug_dir + $"/{_index}.png";
            //     _mapData.Capture(new Bitmap(file_path));
            // }


            // var save_path = Application.streamingAssetsPath + $"/SmallMap/big_map.png";
            // _mapData.Save(save_path);

        }
        #endregion
        #region Btn 4
        void OnClickBtn4th()
        {
            if (_mapData == null)
            {
                OnClickBtn3th();
            }

            var str = InputText.text;
            str.Replace(" ", "");
            str = str.Substring(1, str.Length - 2);
            var arr = str.Split(',');
            var start = new Vector2Int(int.Parse(arr[0]), int.Parse(arr[1]));
            var target = new Vector2Int(int.Parse(arr[2]), int.Parse(arr[3]));


            DU.RunWithTimer(() =>
            {
                // for (int i =0;i<10;i++)
                _mapData.StartAStarBigGrid(start, target);
            }, "StartAStarBigGrid");

            var save_path = Application.streamingAssetsPath + $"/SmallMap/big_map.png";
            _mapData.SaveAStar(save_path);

        }


        #endregion
        Sprite _show_sprite;
        void OnDestroy()
        {
            if (_show_sprite != null)
            {
                Destroy(_show_sprite);
                Destroy(_show_sprite.texture);
                _show_sprite = null;

                ClearMapOptionCache(LeftImage);
                ClearMapOptionCache(RightImage);
            }
        }


        public override void Close()
        {
            base.Close();
            UIManager.Inst.ShowPanel(PanelEnum.TemplateMatchDrawResultPanel, new List<object> { null, 0 });
        }

        #region Options
        public enum Options
        {
            Map,
            Grid,
            AStartFunc,
            SearchTargetFunc,
            FindNearestFog,
            FindNearestFogFollowing,
            LightMap,
            SmallMap,
            JudgeMap,
            FogMap,
            SaveAllMap,
        }

        #endregion
    }


}