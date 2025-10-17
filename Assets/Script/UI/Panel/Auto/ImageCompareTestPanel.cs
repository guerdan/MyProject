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
using Color = UnityEngine.Color;

namespace Script.UI.Panel.Auto
{
    /// <summary>
    /// 参考坐标系是 左下角为原点，X轴向右，Y轴向上
    /// </summary>
    public class ImageCompareTestPanel : BasePanel
    {
        [SerializeField] private Button LeftPathBtn;
        [SerializeField] private Button RightPathBtn;
        [SerializeField] private CheckBox SyncCB;
        [SerializeField] private ImageDetailComp LeftImage;
        [SerializeField] private ImageDetailComp RightImage;
        [SerializeField] private Button Btn1th;
        [SerializeField] private Button Btn2th;
        [SerializeField] private Button Btn3th;
        [SerializeField] private Button Btn4th;
        [SerializeField] private InputField InputText;

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
            LeftPathBtn.onClick.AddListener(OnLeftPathBtnClick);
            RightPathBtn.onClick.AddListener(OnRightPathBtnClick);

            Btn1th.onClick.AddListener(OnBtn1thClick);
            Btn2th.onClick.AddListener(OnBtn2thClick);
            Btn3th.onClick.AddListener(OnBtn3thClick);
            Btn4th.onClick.AddListener(OnBtn4thClick);
        }
        public override void SetData(object data)
        {
            if (data is string mapId)
            {
                _mapId = mapId;
                SetMap();
            }

            InputText.text = "(60,30,148,207)";
            SyncCB.SetData(false, OnSyncCB);
            OnSyncCB(SyncCB.GetStatus());
        }

        #region 拍摄地图
        string _mapId;
        Texture2D _mapTexture;
        Sprite _mapSprite;

        void SetMap()
        {
            MapData mapData = MapDataManager.Inst.Get(_mapId);
            if (mapData == null)
                return;

            if (_mapTexture != null)
            {
                Destroy(_mapTexture);
                _mapTexture = null;
                Destroy(_mapSprite);
                _mapSprite = null;
            }

            mapData.GetContentAttr(out Vector2Int xRange, out Vector2Int yRange
                , out var w, out var h);

            // 使用 rgba 格式（单通道红色）
            _mapTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);

            // 填充纹理数据
            Color32[] pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int index = y * w + x;
                    var data = mapData._map[x + xRange.x, y + yRange.x];
                    if (data == CellType.Empty)
                        pixels[index] = new Color32(0, 0, 0, 255);
                    else if (data == CellType.ObstacleEdge)
                        pixels[index] = new Color32(255, 255, 255, 255);
                    else if (data == CellType.Fog)
                        pixels[index] = new Color32(0, 0, 255, 255);

                }


            // 应用像素数据到纹理
            _mapTexture.SetPixels32(pixels);
            _mapTexture.Apply();

            _mapSprite = Sprite.Create(_mapTexture, new UnityEngine.Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            LeftImage.SetData(_mapSprite);
        }


        #endregion

        void OnLeftPathBtnClick()
        {
            string path = WU.OpenFileDialog("选择图片", Application.streamingAssetsPath, "图片 *.png *.jpg)|*.png;*.jpg");
            if (string.IsNullOrEmpty(path)) return;

            ClearMat();

            _sourceMat = IU.GetMat(path);

            left_path = path;
            LeftImage.SetData(left_path);

            _sourceTex = LeftImage.Image.sprite.texture;
            // 是获取托管像素数组，修改的是托管内存
            //
            _pixels = _sourceTex.GetPixelData<Color32>(0);
            // 是从托管像素数组拷贝一份数组
            // tex.GetPixels32(_pixels, 0);
            _imgW = _sourceTex.width;
            _imgH = _sourceTex.height;
        }
        void OnRightPathBtnClick()
        {
            string path = WU.OpenFileDialog("选择图片", Application.streamingAssetsPath, "图片 *.png *.jpg)|*.png;*.jpg");
            if (string.IsNullOrEmpty(path)) return;

            right_path = path;
            RightImage.SetData(right_path);
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


        public override void Close()
        {
            base.Close();
            UIManager.Inst.ShowPanel(PanelEnum.TemplateMatchDrawResultPanel, new List<object> { null, 0 });
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

            var min_1th = new Color32(120, 120, 160, 255);
            var max_1th = new Color32(145, 145, 190, 255);
            var min_2th = new Color32(80, 80, 110, 255);
            var max_2th = max_1th;
            var min_3th = new Color32(0, 125, 165, 255);
            var max_3th = new Color32(100, 145, 190, 255);

            //colorData中，0-为不可通行空地，1-为可通行区域，2-边界
            int[,] colorData = new int[_imgW, _imgH];
            // var fit_one_list = new List<Vector2Int>();
            var first_list = new List<Vector2Int>();
            var second_list = new List<Vector2Int>();
            FindRole();   //标记角色位置

            DU.RunWithTimer(() =>
            {
                DU.RunWithTimer(() =>
                {
                    for (int i = 0; i < _imgH; i++)
                    {
                        for (int j = 0; j < _imgW; j++)
                        {
                            int index = i * _imgW + j;
                            var color = _pixels[index];

                            if (Between(color, min_1th, max_1th)
                                && color.b - color.g >= 40 && color.b - color.g <= 60)
                            {
                                first_list.Add(new Vector2Int(j, i));
                                colorData[j, i] = 2;
                            }
                            else if (Between(color, min_3th, max_3th))
                            {
                                colorData[j, i] = 4;
                            }

                            else if (Between(color, min_2th, max_2th)
                                && color.b - color.g >= 28 && color.b - color.g <= 60)
                            {
                                colorData[j, i] = 3;
                            }

                        }
                    }
                }, "Condition check");

                // < 1ms
                // DU.RunWithTimer(() =>
                // {
                second_list = Traversal(colorData, first_list);
                // }, "Traversal");

                for (int i = 0; i < _imgH; i++)
                    for (int j = 0; j < _imgW; j++)
                        if (colorData[j, i] == 3)
                            colorData[j, i] = 0;


                // ≈ 4ms   改成生数组， 优化为≈ 2ms
                // DU.RunWithTimer(() =>
                // {
                GenerateMap(colorData);
            }, "GenerateMap");




            var full_show_color = Color.green;
            // var one_show_color = new Color(77 / 255f, 77 / 255f, 254 / 255f);
            var one_show_color = Color.red;
            // var full_show_color = new Color(128/255f, 195/255f, 66/255f);
            LeftImage.ClearPixelColor(full_show_color);
            LeftImage.ClearPixelColor(one_show_color);
            LeftImage.SetPixelColor(first_list, full_show_color, 1);
            LeftImage.SetPixelColor(second_list, one_show_color, 0, 0.5f);


            // 使用 R8 格式（单通道红色）
            Texture2D texture = new Texture2D(_imgW, _imgH, TextureFormat.R8, false);

            // 填充纹理数据
            Color32[] pixels = new Color32[_imgW * _imgH];
            for (int i = 0; i < _imgH; i++)
                for (int j = 0; j < _imgW; j++)
                {
                    int index = i * _imgW + j;
                    if (colorData[j, i] == 0)           //能走的空地
                        pixels[index] = new Color32(0, 0, 0, 255);
                    else if (colorData[j, i] == 1)       //不能走的空地
                        pixels[index] = new Color32(255, 255, 255, 255);
                    else                                //边界
                        pixels[index] = new Color32(128, 0, 0, 255);
                }


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
        List<Vector2Int> Traversal(int[,] colorData, List<Vector2Int> first_list)
        {
            var result = new List<Vector2Int>();
            int y_end = _imgH - 1;
            int x_end = _imgW - 1;

            Vector2Int[] stack = new Vector2Int[first_list.Count * 4];
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
                if (colorData[x, y] == 3)
                {
                    result.Add(new Vector2Int(x, y));
                    colorData[x, y] = 2; // 标记为"边界"
                }

                if (y > 0 && colorData[x, y - 1] == 3)
                    stack[count++] = new Vector2Int(x, y - 1);
                if (x > 0 && colorData[x - 1, y] == 3)
                    stack[count++] = new Vector2Int(x - 1, y);
                if (y < y_end && colorData[x, y + 1] == 3)
                    stack[count++] = new Vector2Int(x, y + 1);
                if (x < x_end && colorData[x + 1, y] == 3)
                    stack[count++] = new Vector2Int(x + 1, y);
                if (y > 0 && x > 0 && colorData[x - 1, y - 1] == 3)
                    stack[count++] = new Vector2Int(x - 1, y - 1);
                if (y < y_end && x > 0 && colorData[x - 1, y + 1] == 3)
                    stack[count++] = new Vector2Int(x - 1, y + 1);
                if (y < y_end && x < x_end && colorData[x + 1, y + 1] == 3)
                    stack[count++] = new Vector2Int(x + 1, y + 1);
                if (y > 0 && x < x_end && colorData[x + 1, y - 1] == 3)
                    stack[count++] = new Vector2Int(x + 1, y - 1);
            }

            return result;
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

        #endregion

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

        void OnBtn1thClick()
        {
            // FindRole();
            // LeftImage.SelectPixel(_playerPos);

            var gridData = new GridData(null);
            var cell = new BigCell(0, 0);
            var map = new CellType[7, 7];
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
                    map[j, len - 1 - i] = (CellType)map_temp[j, i];
                }

            gridData.RefreshCell(cell, map, 1, 1);
            DU.LogWarning($"方向 {Convert.ToString((int)cell.Direction, 2).PadLeft(8, '0')}");

        }
        void OnBtn2thClick()
        {
            // FilterPixel();

            //  比较下谁执快
            TestExecutionTime.Inst.Test();
        }
        void OnBtn3thClick()
        {
            // if (_mapData == null)
            // {
            _mapData = new MapData(new CVRect(0, 0, 200, 200));
            // }

            // string path = WU.OpenFileDialog("选择图片", Application.streamingAssetsPath, "图片 *.png *.jpg)|*.png;*.jpg");
            // if (string.IsNullOrEmpty(path)) return;
            // _mapData.Capture(new Bitmap(path));


            var dir = @"D:\unityProject\MyProject\Assets\StreamingAssets\SmallMap\小地图_22";
            for (int i = 14; i <= 22; i++)
            {
                var file_path = dir + $"/{i}.png";
                _mapData.Capture(new Bitmap(file_path));
            }

            var save_path = Application.streamingAssetsPath + $"/SmallMap/big_map.png";
            _mapData.Save(save_path);

        }

        void OnBtn4thClick()
        {
            var str = InputText.text;
            str.Replace(" ", "");
            str = str.Substring(1, str.Length - 2);
            var arr = str.Split(',');
            var start = new Vector2Int(int.Parse(arr[0]), int.Parse(arr[1]));
            var target = new Vector2Int(int.Parse(arr[2]), int.Parse(arr[3]));



            // if (_mapData == null)
            // {
            MapDataManager.Inst.Remove("Map-22");
            MapDataManager.Inst.Create("Map-22",new CVRect(0, 0, 200, 200));
            _mapData = MapDataManager.Inst.Get("Map-22");
            var dir = @"D:\unityProject\MyProject\Assets\StreamingAssets\SmallMap\小地图_22";
            for (int i = 14; i <= 22; i++)
            {
                var file_path = dir + $"/{i}.png";
                _mapData.Capture(new Bitmap(file_path));
            }
            // }

            // string path = WU.OpenFileDialog("选择图片", Application.streamingAssetsPath, "图片 *.png *.jpg)|*.png;*.jpg");
            // if (string.IsNullOrEmpty(path)) return;
            // _mapData.Capture(new Bitmap(path));



            // _mapData.InitGridOriginal();

            // DU.RunWithTimer(() =>
            // {
            //     _mapData.StartAStarOri(start, target);
            // }, "AStarOri");

            // _mapData.InitGrid();

            // DU.RunWithTimer(() =>
            // {
            //     _mapData.StartAStar(start, target);
            // }, "AStar");

            // _mapData.InitGrid();
            // DU.RunWithTimer(() =>
            // {
            //     _mapData.StartAStarAVL(start, target);
            // }, "AStarAVLTree");


            _mapData.InitGridOriginal();
            DU.RunWithTimer(() =>
            {
                _mapData.StartAStarRedBlack(start, target);
            }, "AStarRedBlackTree");


            DU.RunWithTimer(() =>
            {
                _mapData.StartAStarBigGrid(start, target);
            }, "StartAStarBigGrid");


            // _mapData.Print();

            var save_path = Application.streamingAssetsPath + $"/SmallMap/astar.png";
            _mapData.SaveAStar(save_path);

        }

        Sprite _show_sprite;
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                for (int i = 0; i < 5; i++)
                    _mapData.Step();
                _mapData.Print();
                var save_path = Application.streamingAssetsPath + $"/SmallMap/astar.png";
                _mapData.SaveAStar(save_path);

                _show_sprite = ImageManager.Inst.LoadSpriteInStreaming(save_path);
                RightImage.SetData(_show_sprite);
            }
        }

        void OnDestroy()
        {
            if (_show_sprite != null)
            {
                Destroy(_show_sprite);
                Destroy(_show_sprite.texture);
                _show_sprite = null;
            }
        }


        #endregion

    }


}