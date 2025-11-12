using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenCvSharp;
using Script.Framework.AssetLoader;
using Script.Framework.UI;
using Script.Model.Auto;
using Script.Test;
using Script.UI.Component;
using Script.Util;
using TMPro;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using static Script.Model.Auto.MapData;
using Color = UnityEngine.Color;

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
            OnSelectMapOption(0);
        }

        void OnClickMemoryBtn(bool isLeft)
        {
            _clickLeft = isLeft;
            Utils.SetActive(TipsComp, true);

            var comp = _clickLeft ? LeftMemoryBtn : RightMemoryBtn;

            List<string> options = new List<string>()
            {
                "_map",         // 像素粒度
                "_grid",        // 5X5大格子粒度
                "A*寻路",       // 5X5大格子粒度
                "_small_map",        // 像素粒度
                "_judge_map",        // 像素粒度
                "保存全部地图",       // 保存至本地
            };
            TipsComp.SetData(options, OnSelectMapOption, 140, 7);
            TipsComp.SetCurIndex(_optionSelectStatus[_clickLeft ? 0 : 1]);

            var tipsCompRectT = TipsComp.GetComponent<RectTransform>();
            var pos = Utils.GetPos(tipsCompRectT, comp.GetComponent<RectTransform>()
                , new Vector2(25, 8), true);
            tipsCompRectT.anchoredPosition = pos;
        }

        /// <summary>
        /// 0:_map 
        /// 1:_map1 
        /// 2:大格子寻路
        /// </summary>
        void OnSelectMapOption(int option)
        {
            _onSelectPixel = null;

            if (option < 0)
                return;
            MapData mapData = MapDataManager.Inst.Get(_mapId);
            if (mapData == null)
                return;

            if (option == 5)
            {
                //保存
                var script = AutoScriptManager.Inst.GetScriptData(_scriptId);
                string dir = $"{script.GetCapturePath()}/拍摄地图节点";
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                mapData.Save($"{dir}/map.png");
                return;
            }

            ImageDetailComp comp = _clickLeft ? LeftImage : RightImage;
            _optionSelectStatus[_clickLeft ? 0 : 1] = option;

            mapData.GetContentAttr(out Vector2Int xRange, out Vector2Int yRange
                , out var w, out var h);

            // 使用 rgba 格式
            // Texture2D 是以左下角为像素坐标系原点，而 Mat、Bitmap是左上角
            Color32[] pixels = null;
            Vector2Int size = new Vector2Int(w, h);

            if (option == 0)
            {
                pixels = mapData.GetImageMap0();
            }
            else if (option == 1)
            {
                pixels = mapData.GetImageGrid();
            }
            else if (option == 2)
            {
                var str = InputText.text;
                str.Replace(" ", "");
                str = str.Substring(1, str.Length - 2);
                var arr = str.Split(',');
                try
                {
                    var start = new Vector2Int(int.Parse(arr[0]), int.Parse(arr[1]));
                    var target = new Vector2Int(int.Parse(arr[2]), int.Parse(arr[3]));
                    DU.RunWithTimer(() => _mapData.StartAStarBigGrid(start, target)
                    , "StartAStarBigGrid");
                    pixels = mapData.GetImageGridAStart();
                }
                catch
                {
                    DU.LogError("[A*寻路 打印图像] 输入文本格式不对");
                    return;
                }

            }
            else if (option == 3)
            {
                size = new Vector2Int(200, 200);
                pixels = mapData.GetImageSmallMap();
            }
            else if (option == 4)
            {
                pixels = mapData.GetImageJudgeMap();
            }


            var mapTexture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);

            if (pixels.Length == 0)
                return;
            pixels = IU.Color32ReverseVertical(pixels, size.x);

            // 应用像素数据到纹理
            mapTexture.SetPixels32(pixels);
            mapTexture.Apply();

            var line_offset = new Vector2Int(-(xRange.x % 5), -(yRange.x % 5));
            var mapSprite = Sprite.Create(mapTexture, new UnityEngine.Rect(0, 0, size.x, size.y), new Vector2(0.5f, 0.5f));
            comp.SetData(mapSprite);
            comp.SetLineOffset(line_offset);

            SaveMapImage(_clickLeft, mapTexture, mapSprite);


            // 加个选择像素回调，为了方便查看内存
            _onSelectPixel = v2 =>
            {
                if (v2.x < 0 || v2.x >= w || v2.y < 0 || v2.y >= h)
                    return;

                var px = v2.x + xRange.x;
                var py = v2.y + yRange.x;
                var grid_px = px / 5;
                var grid_py = py / 5;

                BigCell cell = mapData._gridData._grid[grid_px, grid_py];
                if (cell == null)
                    return;
                CellType[,] map = _mapData._map;

                int x_start = cell.x * 5;
                int y_start = cell.y * 5;
                var s_map = new CellType[5, 5];
                for (int m = y_start; m < y_start + 5; m++)
                    for (int n = x_start; n < x_start + 5; n++)
                    {
                        s_map[n - x_start, m - y_start] = map[n, m];
                    }

                var judge_pos = _mapData._judgePos;
                JudgeCell j = default;
                if (px - judge_pos.x >= 0 && px - judge_pos.x < 300
                    && py - judge_pos.y >= 0 && py - judge_pos.y < 300)
                    j = _mapData._judge_map[px - judge_pos.x, py - judge_pos.y];

            };

        }

        void SaveMapImage(bool isLeft, Texture2D texture, Sprite sprite)
        {
            ClearMapImage(isLeft);
            if (isLeft)
            {
                _mapLTexture = texture;
                _mapLSprite = sprite;
            }
            else
            {
                _mapRTexture = texture;
                _mapRSprite = sprite;
            }
        }


        void ClearMapImage(bool isLeft)
        {
            if (isLeft && _mapLTexture != null)
            {
                Destroy(_mapLTexture);
                _mapLTexture = null;
                Destroy(_mapLSprite);
                _mapLSprite = null;
            }
            else if (!isLeft && _mapRTexture != null)
            {
                Destroy(_mapRTexture);
                _mapRTexture = null;
                Destroy(_mapRSprite);
                _mapRSprite = null;
            }

        }


        #endregion

        void OnClickLeftPathBtn()
        {
            // string init_path = Application.streamingAssetsPath;
            string init_path = @"C:\Users\hp\Desktop\path\图\0.2间隔";
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
                OnSelectMapOption(_optionSelectStatus[_clickLeft ? 0 : 1]);
                _clickLeft = true;
                OnSelectMapOption(_optionSelectStatus[_clickLeft ? 0 : 1]);
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
        int max_r = 0;

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

            // 第1版  颜色偏白，微蓝   
            //
            // var min_1th = new Color32(117, 123, 128, 255);
            // var max_1th = new Color32(173, 165, 180, 255);
            // var min_2th = new Color32(86, 88, 90, 255);
            // var max_2th = max_1th;
            // var min_3th = new Color32(65, 120, 135, 255);
            // var max_3th = new Color32(95, 140, 170, 255);
            // var B_to_G = new Vector2Int(5, 30);

            // 第2版  颜色偏蓝   
            //
            // 1类边界
            var min_1th = new Color32(110, 110, 150, 255);
            var max_1th = new Color32(145, 145, 195, 255);
            var B_to_G_1th = new Vector2Int(30, 60);
            // 2类边界
            var min_2th = new Color32(86, 88, 90, 255);
            var max_2th = max_1th;
            // 1类迷雾
            var min_3th = new Color32(0, 130, 170, 255);
            var max_3th = new Color32(90, 142, 192, 255);
            var B_to_G_3th = new Vector2Int(38, 55);


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

            #region 筛-初始像素

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

                            if (r == 0 && g == 0 && b == 0) // 文字描边
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
                            if (colorData[j, i] == 1 || colorData[j, i] > 9)
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
                    if (data == 1)
                        pixels[index] = new Color32(0, 0, 0, 255);
                    else if (data == 2)      // 边界     
                        pixels[index] = new Color32(255, 255, 255, 255);
                    else if (data == 5)      // 二阶边界,弱一点                 
                        pixels[index] = new Color32(255, 0, 0, 255);
                    else if (data == 10)
                        pixels[index] = new Color32(0, 0, 255, 255);
                    else if (data == 11)
                        pixels[index] = new Color32(128, 128, 200, 255);

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


                foreach (var offset in Utils.SurroundList)
                {
                    int px = offset.x + x;
                    int py = offset.y + y;
                    if (colorData[px, py] == 3)
                    {
                        colorData[px, py] = 5;
                        stack[count++] = new Vector2Int(px, py);
                    }

                }

                // if (colorData[x, y - 1] == 3)
                // {
                //     colorData[x, y - 1] = 5;
                //     stack[count++] = new Vector2Int(x, y - 1);
                // }
                // if (colorData[x - 1, y] == 3)
                // {
                //     colorData[x - 1, y] = 5;
                //     stack[count++] = new Vector2Int(x - 1, y);
                // }
                // if (colorData[x, y + 1] == 3)
                // {
                //     colorData[x, y + 1] = 5;
                //     stack[count++] = new Vector2Int(x, y + 1);
                // }
                // if (colorData[x + 1, y] == 3)
                // {
                //     colorData[x + 1, y] = 5;
                //     stack[count++] = new Vector2Int(x + 1, y);
                // }
                // if (colorData[x - 1, y - 1] == 3)
                // {
                //     colorData[x - 1, y - 1] = 5;
                //     stack[count++] = new Vector2Int(x - 1, y - 1);
                // }
                // if (colorData[x - 1, y + 1] == 3)
                // {
                //     colorData[x - 1, y + 1] = 5;
                //     stack[count++] = new Vector2Int(x - 1, y + 1);
                // }
                // if (colorData[x + 1, y + 1] == 3)
                // {
                //     colorData[x + 1, y + 1] = 5;
                //     stack[count++] = new Vector2Int(x + 1, y + 1);
                // }
                // if (colorData[x + 1, y - 1] == 3)
                // {
                //     colorData[x + 1, y - 1] = 5;
                //     stack[count++] = new Vector2Int(x + 1, y - 1);
                // }
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

        #region 处理小地图

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
        void OnClickBtn2th()
        {
            // FilterPixel();

            //  比较下谁执快
            // TestExecutionTime.Inst.Test1();
            // TestExecutionTime.Inst.Test2();
            TestExecutionTime.Inst.Test3();
        }

        int _index;
        int _max_index;
        string _debug_dir;

        void OnClickBtn3th()
        {
            // if (_mapData == null)
            // {
            MapDataManager.Inst.Remove("Map-22");
            MapDataManager.Inst.Create("Map-22", new CVRect(0, 0, 200, 200));
            _mapData = MapDataManager.Inst.Get("Map-22");
            // }

            // string path = WU.OpenFileDialog("选择图片", Application.streamingAssetsPath, "图片 *.png *.jpg)|*.png;*.jpg");
            // if (string.IsNullOrEmpty(path)) return;
            // _mapData.Capture(new Bitmap(path));


            // var dir = @"D:\unityProject\MyProject\Assets\StreamingAssets\SmallMap\小地图_22";

            _index = 31;
            _max_index = 111;
            _debug_dir = @"C:\Users\hp\Desktop\path\图\0.2间隔";

            // count++;
            // for (int i = 31; i <= 111; i++)
            // {
            //     var file_path = dir + $"/{i}.png";
            //     _mapData.Capture(new Bitmap(file_path));
            // }


            // var save_path = Application.streamingAssetsPath + $"/SmallMap/big_map.png";
            // _mapData.Save(save_path);

        }

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



        Sprite _show_sprite;
        void OnDestroy()
        {
            if (_show_sprite != null)
            {
                Destroy(_show_sprite);
                Destroy(_show_sprite.texture);
                _show_sprite = null;

                ClearMapImage(true);
                ClearMapImage(false);
            }
        }


        #endregion
        public override void Close()
        {
            base.Close();
            UIManager.Inst.ShowPanel(PanelEnum.TemplateMatchDrawResultPanel, new List<object> { null, 0 });
        }
    }


}