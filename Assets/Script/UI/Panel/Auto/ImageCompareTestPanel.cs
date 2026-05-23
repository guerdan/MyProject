using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using OpenCvSharp;
using Script.Framework;
using Script.Framework.UI;
using Script.Model.Auto;
using Script.Test;
using Script.UI.Components;
using Script.Util;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Color = UnityEngine.Color;
using Image = UnityEngine.UI.Image;

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
        [SerializeField] private Image MaskImage;
        [SerializeField] private Button Btn1th;
        [SerializeField] private Button Btn2th;
        [SerializeField] private Button Btn3th;
        [SerializeField] private Button Btn4th;
        [SerializeField] private InputField InputText;
        [SerializeField] private KeywordTipsComp TipsComp;

        Texture2D _leftTex;
        Mat _leftMat;
        int _imgW;
        int _imgH;
        Sprite _resultSpr;          // 生成筛选结果

        Color32 color_red = new Color32(255, 0, 0, 255);

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
            SU.Print();
        }
        public override void SetData(object data)
        {
            // Config.DebugImageTmplsQuality();

            Utils.SetActive(LeftMemoryBtn, false);
            Utils.SetActive(RightMemoryBtn, false);

            if (data is string[] list)
            {
                RightImage.ClearData();
                LeftImage.ClearData();
                _mapId = list[0];
                _mapData = MapDataManager.Inst.Get("Map-22");
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
        AutoScriptData _script;

        bool _clickLeft;
        int[] _optionSelectStatus = new int[] { -1, -1 };
        Action<Vector2Int> _onSelectPixel;

        Vector2Int _playerPos = Vector2Int.left;        //参考坐标系的原点在地图左下角


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

            var strList = MapOptions.Select(t => t.Item2).ToList();
            TipsComp.SetData(strList, OnSelectMapOption, 140, 7);
            TipsComp.SetCurIndex(_optionSelectStatus[_clickLeft ? 0 : 1]);

            var tipsCompRectT = TipsComp.GetComponent<RectTransform>();
            var pos = Utils.GetPos(tipsCompRectT, comp.GetComponent<RectTransform>()
                , new Vector2(25, 8), true);
            tipsCompRectT.anchoredPosition = pos;
        }
        void OnSelectMapOption(int option_int)
        {
            if (option_int < 0)
                return;

            Options option = MapOptions[option_int].Item1;
            ImageDetailComp comp = _clickLeft ? LeftImage : RightImage;
            GetSpriteOfMapOption(_scriptId, _mapId, option, InputText.text
                                , out var mapSprite, out var line_offset, out _, out var debug_str, comp);

            if (mapSprite == null)
                return;

            _optionSelectStatus[_clickLeft ? 0 : 1] = option_int;
            comp.SetData(mapSprite, debug_str);
            comp.SetLineOffset(line_offset);

        }

        #region MapOption

        public enum Options
        {
            Map,                        // 像素粒度
            Grid,                       // 5X5大格子粒度
            Auto,                       // 根据脚本变量，自动切换模式
            FindNearestFog,
            FindNearestFogFollowing,
            FollowTarget,
            LightMap,
            SmallMap,
            RawSmallMap,
            ErrorSmallMap,
            SmallMapOrigin,
            JudgeMap,
            FogMap,
            AStartFunc,                 // 目标寻路
            SaveAllMap,                 // 保存至本地
        }

        static List<(Options, string)> MapOptions = new List<(Options, string)>()
            {
                (Options.Map,"map"),
                (Options.Grid,"grid"),

                (Options.Auto,"Auto"),
                (Options.FindNearestFog,"NearFog"),
                (Options.FindNearestFogFollowing,"NearFog (Follow)"),
                (Options.FollowTarget,"FollowTarget"),

                (Options.SmallMapOrigin,"原图_small_map"),
                (Options.SmallMap,"small_map"),
                (Options.RawSmallMap,"raw_small_map"),
                (Options.ErrorSmallMap,"error_small_map"),

                (Options.LightMap,"light_map"),
                (Options.JudgeMap,"judge_map"),
                (Options.FogMap,"fog_map"),

                (Options.AStartFunc,"A*寻路:位置"),
                (Options.SaveAllMap,"保存全部地图"),
            };

        /// <summary>
        /// 调用者实例 与 需求资源 绑定关系缓存。调用者实例销毁时，需要通知这里清理缓存
        /// </summary>
        static Dictionary<Component, (Texture2D, Sprite)> _mapOptionCache = new Dictionary<Component, (Texture2D, Sprite)>();
        public static void GetSpriteOfMapOption(string scriptId, string mapId, Options option, string command,
                         out Sprite sprite, out Vector2Int line_offset, out Vector2Int anchor, out string debug_str,
                        Component cacheKey = null)
        {
            debug_str = "";
            sprite = null;
            line_offset = default;
            anchor = default;
            var script = AutoScriptManager.Inst.GetScriptData(scriptId);

            MapData mapData = MapDataManager.Inst.Get(mapId);
            if (mapData == null) return;

            mapData.GetContentAttr(out Vector2Int xRange, out Vector2Int yRange
                , out var w, out var h);
            line_offset = new Vector2Int(-(xRange.x % 5), -(yRange.x % 5));

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
                // mapData.RestoreMapInfo();
                pixels = mapData.GetImageMap0();
            }
            else if (option == Options.Grid)
            {
                pixels = mapData.GetDebugImage_Grid();
            }
            else if (option == Options.Auto)
            {
                var status = script.FormulaGetResult("map_finding_status");
                size = new Vector2Int(200, 200);
                var debug1 = "";
                if (status == 0)
                {
                    pixels = mapData.GetDebugImage_DetailFollowing(size, out _);
                    debug1 += "S=0";
                }
                else if (status == 1)
                {
                    ExploreFog(script, mapData, out var info);
                    pixels = mapData.GetDebugImage_NearestFogFollowing(size, out _);
                    debug1 += $"S=1 {info}";
                }
                else if (status == 2)
                {
                    ReachPos(script, mapData, out pixels, out var info);
                    debug1 += $"S=2 {info}";
                }

                debug_str += debug1;
                var start_pos = mapData.SmallMapPos;
                line_offset = new Vector2Int(-(start_pos.x % 5), -(start_pos.y % 5));

            }
            else if (option == Options.FindNearestFog || option == Options.FindNearestFogFollowing)
            {
                ExploreFog(script, mapData, out var info);
                debug_str += info;

                if (option == Options.FindNearestFog)
                    pixels = mapData.GetDebugImage_NearestFog();
                else
                {
                    size = new Vector2Int(200, 200);
                    pixels = mapData.GetDebugImage_NearestFogFollowing(size, out var start_pos);
                    line_offset = new Vector2Int(-(start_pos.x % 5), -(start_pos.y % 5));
                }
            }
            else if (option == Options.FollowTarget)
            {
                size = new Vector2Int(200, 200);

                MapIconType executor1 = default;
                MapIconType executor2 = default;
                MapIconType follow_target = default;
                bool exe1_recog = false;
                bool exe2_recog = false;
                bool target_recog = false;
                float distance = 7;
                float avoid_factor = 2;

                var node = script.GetMapPathFindingNode(PathFindingType.FollowPlayer);
                if (node == null)
                {
                    follow_target = MapIconType.P1;
                    executor1 = MapIconType.P2;
                }
                else
                {
                    var input_param = node._input_param;
                    follow_target = MapPathFindingNode.StringToPlayer(input_param[1]);
                    executor1 = MapPathFindingNode.StringToPlayer(input_param[2]);
                    executor2 = MapPathFindingNode.StringToPlayer(input_param[3]);
                    distance = float.Parse(input_param[4]);
                    avoid_factor = float.Parse(input_param[5]);
                }


                var small_map_pos = mapData.SmallMapPos;
                var dir = mapData.GetDir_FollowTarget(executor1, follow_target, distance, avoid_factor
                                                    , out exe1_recog, out target_recog, out var path1);

                mapData.GetDir_FollowTarget(executor2, follow_target, distance, avoid_factor
                                            , out exe2_recog, out target_recog, out var path2);

                pixels = mapData.GetDebugImage_PlayerAStar(path1, path2, size, out var start_pos);


                line_offset = new Vector2Int(-(start_pos.x % 5), -(start_pos.y % 5));

                if (!exe1_recog || !target_recog || (executor2 != MapIconType.Undefined && !exe2_recog))
                {
                    debug_str = $"无法识别: ";
                    if (!exe1_recog) debug_str += $"E1 |";
                    if (executor2 != MapIconType.Undefined && !exe2_recog) debug_str += $"E2 |";
                    if (!target_recog) debug_str += $"T |";
                    debug_str = debug_str.TrimEnd('|');
                }

                DU.LogWarning($"方向 {dir}");
            }
            else if (option == Options.LightMap)
            {
                pixels = mapData.GetImageLightMap();
            }
            else if (option == Options.SmallMapOrigin)
            {
                size = new Vector2Int(200, 200);
                pixels = mapData.GetDebugImage_SmallMapOrigin();
            }
            else if (option == Options.SmallMap)
            {
                var match_score = mapData.GetCurAccuracyScore();
                var average_score = mapData.GetAverageAccuracyScore();
                debug_str += $"{DU.FloatFormat(match_score)}/{DU.FloatFormat(average_score)}";
                size = new Vector2Int(200, 200);
                pixels = mapData.GetDebugImage_SmallMap(MapData.SmallMapDebugType.Normal);
            }
            else if (option == Options.RawSmallMap)
            {
                size = new Vector2Int(200, 200);
                pixels = mapData.GetDebugImage_SmallMap(MapData.SmallMapDebugType.Raw);
            }
            else if (option == Options.ErrorSmallMap)
            {
                size = new Vector2Int(200, 200);
                pixels = mapData.GetDebugImage_SmallMap(MapData.SmallMapDebugType.Error);
            }

            else if (option == Options.JudgeMap)
            {
                pixels = mapData.GetDebugImage_JudgeMap();
            }
            else if (option == Options.FogMap)
            {
                pixels = mapData.GetDebugImage_FogMap();
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
                    DU.LogError("[AStartFunc] 输入文本格式不对");
                    return;
                }

                BigCellPathResult pathObj = null;
                DU.RunWithTimer(() =>
                  pathObj = mapData.StartAStarBigGrid(start, target)
                    , "StartAStarBigGrid");
                pixels = mapData.GetDebugImage_GridAStar(pathObj);

            }
            else if (option == Options.SaveAllMap)
            {
                //保存
                string dir = $"{script.GetCapturePath()}/拍摄地图节点";
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                mapData.Save($"{dir}/map.png");
                return;
            }
            // }, "MapData");

            if (pixels.Length == 0)
                return;



            var errors = mapData.ErrorList;
            if (errors[0] > 0)
                debug_str += $" 匹配E{errors[0]}";
            if (errors[1] > 0)
                debug_str += $" 漏缝E{errors[1]}";
            if (errors[2] > 0)
                debug_str += $" 空地E{errors[2]}";

            if (option != Options.SmallMap)
            {
                var match_score = mapData.GetCurAccuracyScore();
                var average_score = mapData.GetAverageAccuracyScore();
                debug_str += $" {DU.FloatFormat(match_score)}";
            }

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

            // if (needReverseY) pixels = IU.Color32ReverseYAxis(pixels, size.x);

            // 应用像素数据到纹理
            mapTexture.SetPixels32(pixels);
            mapTexture.Apply();

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

        static void ExploreFog(AutoScriptData script, MapData mapData, out string debug_str)
        {
            debug_str = "";
            MapIconType executor = MapIconType.P1;
            var node = script.GetMapPathFindingNode(PathFindingType.ExploreFog);
            float avoid_factor = 1.5f;
            float team_max_distance = 0;
            if (node != null)
            {
                var input_param = node._input_param;
                executor = MapPathFindingNode.StringToPlayer(input_param[1]);
                avoid_factor = float.Parse(input_param[2]);
                team_max_distance = float.Parse(input_param[3]);
            }

            bool distance_fit = mapData.CheckPlayerDistance(executor, team_max_distance);
            if (distance_fit)
                mapData.FindNearestFogAStar(executor, false, avoid_factor);
            else
                debug_str += "友方过远";
        }


        static void ReachPos(AutoScriptData script, MapData mapData, out Color32[] pixels, out string debug_str)
        {
            var size = new Vector2Int(200, 200);
            debug_str = "";
            var node = script.GetMapPathFindingNode(PathFindingType.ReachPos);
            if (node == null)
            {
                debug_str = "无脚本节点";
                pixels = mapData.GetDebugImage_DetailFollowing(size, out _);
                return;
            }

            var input_param = node._input_param;
            MapIconType executor = MapPathFindingNode.StringToPlayer(input_param[1]);
            var temp = script.FormulaGetResultV2(input_param[2]);
            var target_pos = mapData.ConvertToData(temp);
            float stop_distance = script.FormulaGetResult(input_param[3]);
            float avoid_factor = float.Parse(input_param[4]);
            float team_max_distance = float.Parse(input_param[5]);


            var executor_pos = mapData.FindPlayerPosAndHistory(executor, out _);

            bool stop_distance_fit = (executor_pos - target_pos).magnitude > stop_distance;
            bool team_distance_fit = mapData.CheckPlayerDistance(executor, team_max_distance);
            if (team_distance_fit && stop_distance_fit)
            {
                var findPath = mapData.PlayerToTileAStar(executor_pos, target_pos, avoid_factor);
                pixels = mapData.GetDebugImage_DetailFollowing(size, out _, findPath);
                if (findPath.Status != PathFindingResult.Success)
                {
                    debug_str = "寻路失败";
                }
            }
            else
            {
                if (!team_distance_fit)
                    debug_str = "友方过远";
                else if (!stop_distance_fit)
                    debug_str = "已抵达";
                pixels = mapData.GetDebugImage_DetailFollowing(size, out _);
            }

        }

        #endregion


        #endregion

        string _init_path = @"D:\unityProject\MyProject_Resource";

        void OnClickLeftPathBtn()
        {
            // string init_path = Application.streamingAssetsPath;
            string path = WU.OpenFileDialog("选择图片", _init_path, "图片 *.png *.jpg)|*.png;*.jpg");
            LoadLeftImage(path);
        }

        void LoadLeftImage(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            ClearLeftMat();

            _leftMat = IU.GetMat(path);
            LeftImage.SetData(path, Path.GetFileName(path));

            _leftTex = LeftImage.Image.sprite.texture;
            // 是获取托管像素数组，修改的是托管内存
            //
            // 是从托管像素数组拷贝一份数组
            // tex.GetPixels32(_pixels, 0);
            _imgW = _leftTex.width;
            _imgH = _leftTex.height;
        }

        void OnClickRightPathBtn()
        {
            string path = WU.OpenFileDialog("选择图片", _init_path, "图片 *.png *.jpg)|*.png;*.jpg");
            if (string.IsNullOrEmpty(path)) return;

            RightImage.SetData(path, Path.GetFileName(path));
        }

        #region Update

        List<string> _strList = new List<string>();
        List<Vector3Int> _colorList1 = new List<Vector3Int>();
        List<Vector3Int> _colorList2 = new List<Vector3Int>();
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space) && _mapData != null && _index <= _max_index)
            {
                // GameTimer.Inst.SetTimeOnce(this, () =>      // 防止断点时，对代码输出了空格
                // {

                var file_path = _debug_dir + $"/{_index++}.png";
                var bit = new Bitmap(file_path);
                var colors = IU.BitmapToColor32(bit);
                _mapData.Capture(colors);

                _clickLeft = false;
                OnSelectMapOption(_optionSelectStatus[_clickLeft ? 0 : 1]);

                _clickLeft = true;
                OnSelectMapOption(_optionSelectStatus[_clickLeft ? 0 : 1]);

                // _mapData.PrintResult();

                // }, 0);
            }

            if (Input.GetKeyDown(KeyCode.Return))
            {
                _strList.Add(InputText.text);
                LeftImage.GetSelectPixelInfo(out var center, out var convolution);
                _colorList1.Add(center);
                _colorList2.Add(convolution);
                InputText.text = "";
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

                using (var resultMat = IU.MatchTemplate1(_leftMat, template))
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


        #region 筛选






        #region FilterPixel
        void FilterPixel()
        {
            if (_leftTex == null) return;

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


            // 数据源
            var pixs = LeftImage.GetColor32(out _, out _);
            PixType[,] colorData = null;
            List<Vector2Int>[] icon_result = null;
            DU.RunWithTimer(() =>
            {
                MapData.ColorToData(pixs, out colorData, out _, out _);
            }, "ColorToData");


            Texture2D texture = new Texture2D(_imgW, _imgH, TextureFormat.RGBA32, false);
            // 填充纹理数据
            Color32[] pixels = new Color32[_imgW * _imgH];
            Color32Image image = new Color32Image(pixels, _imgW, _imgH);
            for (int i = 0; i < _imgH; i++)
                for (int j = 0; j < _imgW; j++)
                {
                    int index = i * _imgW + j;
                    var data = colorData[j, i];
                    if (data == PixType.Undefined)                      // 未定义
                        pixels[index] = new Color32(128, 128, 128, 255);
                    if (data == PixType.Empty)                          // 空地
                        pixels[index] = new Color32(0, 0, 0, 255);
                    else if (data == PixType.ObstacleEdge)              // 边界     
                        pixels[index] = new Color32(255, 255, 255, 255);
                    else if (data == PixType.ObstacleEdgeTemp)          // 候选边界                
                        pixels[index] = new Color32(255, 0, 0, 255);
                    else if (data == PixType.Fog)                       // 迷雾
                        pixels[index] = new Color32(0, 0, 255, 255);
                    else if (data == PixType.FogArea)                   // 候选迷雾
                        pixels[index] = new Color32(0, 0, 180, 255);

                }
            // MapData.DrawIcon(image, icon_result, default);

            // 应用像素数据到纹理
            texture.SetPixels32(pixels);
            texture.Apply();

            // 清理
            ClearResult();

            _resultSpr = Sprite.Create(texture, new UnityEngine.Rect(0, 0, _imgW, _imgH), new Vector2(0.5f, 0.5f));
            RightImage.SetData(_resultSpr);


            // DU.LogWarning($"max_r: {max_r}");

        }

        #endregion
        #endregion

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


        void ClearLeftMat()
        {
            if (_leftMat != null)
            {
                _leftMat.Dispose();
                _leftMat = null;
            }
            if (_leftTex != null)
            {
                // Destroy(_sourceTex);  //有人回收
                _leftTex = null;
            }

            _imgW = 0;
            _imgH = 0;
        }
        void ClearResult()
        {
            if (_resultSpr != null)
            {
                Destroy(_resultSpr);
                Destroy(_resultSpr.texture);
                _resultSpr = null;
            }
        }


        #region Other与Btn



        MapData _mapData;

        void OnClickBtn1th()
        {
            Utils.SetActive(TipsComp, true);
            TipsComp.SetData(ToolOptions, OnSelectToolOption, 140, 7);
            TipsComp.SetCurIndex(-1);

            var tipsCompRectT = TipsComp.GetComponent<RectTransform>();
            var pos = Utils.GetPos(tipsCompRectT, Btn1th.GetComponent<RectTransform>()
                , new Vector2(0, -25), true);
            tipsCompRectT.anchoredPosition = pos;
        }


        void OnClickBtn2th()
        {
            // var img = RecogUtil.GetImg(LeftImage._path);
            // RecogUtil.RecogAffix(img);

            // FilterPixel();
            // SplitBagCell();

            //  比较下谁执快
            TestExecutionTime.Inst.Test();
            // TestExecutionTime.Inst.Test10();
            // TestExecutionTime.Inst.Test20();

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

            // _index = 1; _max_index = 40; _debug_dir = @"Assets\StreamingAssets\SmallMap\Map-22";
            // _index = 31; _max_index = 111; _debug_dir = @"D:\unityProject\MyProject_Resource\0.2间隔";
            // _index = 1; _max_index = 48; _debug_dir = @"D:\unityProject\MyProject\TestResource\Map-23";

            // _index = 0; _max_index = 51; _debug_dir = @"D:\unityProject\MyProject_Resource\for_init";

            // _index = 15; _max_index = 25; _debug_dir = @"D:\unityProject\MyProject_Resource\案例\P3P4";

            // _index = 1; _max_index = 31; _debug_dir = @"D:\unityProject\MyProject_Resource\案例\Map-102";
            // _index = 1; _max_index = 11; _debug_dir = @"D:\unityProject\MyProject_Resource\案例\物品";
            // _index = 1; _max_index = 37; _debug_dir = @"D:\unityProject\MyProject_Resource\案例\bug1";
            // _index = 1; _max_index = 22; _debug_dir = @"D:\unityProject\MyProject_Resource\案例\bug初始空地";
            // _index = 1; _max_index = 20; _debug_dir = @"D:\unityProject\MyProject_Resource\案例\开头堵死2";
            _index = 340; _max_index = 380; _debug_dir = @"D:\unityProject\MyProject_Resource\案例\事故1";

            // _debug_dir = @"D:\unityProject\MyProject\TestResource";
            // var file_path = _debug_dir + $"/{1}.png";

            // for (_index = 31; _index <= 111; _index++)
            // {
            // var file_path = _debug_dir + $"/{_index}.png";
            // var colors = IU.BitmapToColor32(new Bitmap(file_path));
            // _mapData.Capture(colors);
            // }


            // var save_path = Application.streamingAssetsPath + $"/SmallMap/big_map.png";
            // _mapData.Save(save_path);

        }

        void OnClickBtn4th()
        {
            // if (_mapData == null)
            // {
            //     OnClickBtn3th();
            // }

            // var str = InputText.text;
            // str.Replace(" ", "");
            // str = str.Substring(1, str.Length - 2);
            // var arr = str.Split(',');
            // var start = new Vector2Int(int.Parse(arr[0]), int.Parse(arr[1]));
            // var target = new Vector2Int(int.Parse(arr[2]), int.Parse(arr[3]));


            // DU.RunWithTimer(() =>
            // {
            //     // for (int i =0;i<10;i++)
            //     _mapData.StartAStarBigGrid(start, target);
            // }, "StartAStarBigGrid");

            // var save_path = Application.streamingAssetsPath + $"/SmallMap/big_map.png";
            // _mapData.SaveAStar(save_path);

        }

        #region ToolOption

        static List<string> ToolOptions = new List<string>()
            {
                "处理图片",         //个性化需求
                "处理图片2",         //个性化需求
                "裁剪区域",         //以左上角为起点，截取指定宽高的区域
                "喂单张地图",
                "对比颜色",         //颜色不同的像素，会有白色遮罩
                "对比图片",
                "打印数据",
                "处理文本",
            };

        void OnSelectToolOption(int opt_index)
        {
            string option = ToolOptions[opt_index];
            var input_str = InputText.text;
            string output_path = @"D:\unityProject\MyProject_Resource\输出";

            if (option == "裁剪区域")
            {
                input_str.Replace(" ", "");
                input_str = input_str.Substring(1, input_str.Length - 2);
                var arr = input_str.Split(',');
                int num0 = int.Parse(arr[0]), num1 = int.Parse(arr[1])
                , num2 = int.Parse(arr[2]), num3 = int.Parse(arr[3]);

                var input_path = LeftImage._path;
                var name = Path.GetFileNameWithoutExtension(input_path);
                using (Bitmap bitmap = new Bitmap(input_path))
                {
                    int h = bitmap.Height;
                    using (Bitmap cut = IU.CutOutImage(bitmap
                        , new CVRect(num0, h - 1 - (num1 + num3 - 1), num2, num3)))
                    {
                        IU.SaveBitmap(cut, $"{output_path}/{name}_cut.png");
                    }
                }

            }
            else if (option == "喂单张地图")
            {
                MapDataManager.Inst.Remove("Map-22");
                MapDataManager.Inst.Create("Map-22", new CVRect(0, 0, 200, 200));
                _mapData = MapDataManager.Inst.Get("Map-22");
                var colors = IU.BitmapToColor32(new Bitmap(LeftImage._path));
                _mapData.Capture(colors);
            }

            else if (option == "处理图片")
            {
                string path = LeftImage._path;

                List<Color32Image> list = null;
                using (Bitmap bitmap = new Bitmap(path))
                using (Bitmap bitmap1 = bitmap.Clone(new Rectangle(0, 0, bitmap.Width, bitmap.Height), PixelFormat.Format32bppArgb))
                {
                    IU.BitmapToMat(bitmap1, out Mat source, out Vec4b[] data);
                    var img = new Vec4bImage(data, bitmap1.Width, bitmap1.Height);
                    list = RecogUtil.ItemFloatParse(img);
                    source.Dispose();
                }

                string dir_path = @"D:\unityProject\MyProject_Resource\ItemAttr\template";
                string name = Path.GetFileNameWithoutExtension(path);
                string[] sub_names = name.Split(",");


                for (var i = 0; i < list.Count; i++)
                {
                    if (i >= sub_names.Length) continue;
                    string sub = sub_names[i];
                    if (string.IsNullOrEmpty(sub)) continue;
                    RecogUtil.Save(list[i], $"{dir_path}/{sub}.png");
                }

            }
            else if (option == "处理图片2")
            {
                var img = RecogUtil.GetImg(LeftImage._path);

                // string dir_path = @"D:\unityProject\MyProject_Resource\ItemAttr\template\输出";
                // string path = $"{dir_path}/word";
                // RecogUtil.SplitTextImg(img, path);


                // RecogUtil.IdentifyNumber(img, false, out int number);
                // DU.LogWarning($"数字: {number}");

                RecogUtil.IdentifyFrame(img, false, out var en);
                DU.LogWarning($"框: {en}");

            }

            else if (option == "对比颜色")
            {

                var pixs_left = LeftImage.GetColor32(out int w, out int h);
                var pixs_right = RightImage.GetColor32(out _, out _);
                if (pixs_left.Length != pixs_right.Length)
                {
                    DU.LogWarning("大小不相等");
                    RightImage.SetMask(null);
                    return;
                }

                Color32[] colors = new Color32[pixs_left.Length];
                Color32 white_color = new Color32(255, 255, 255, 255);
                Color32 no_color = new Color32(255, 255, 255, 0);

                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int index = y * w + x;
                        bool same = IU.Color32Equal(pixs_left[index], pixs_right[index]);
                        colors[index] = same ? white_color : no_color;
                    }

                RightImage.SetMask(colors);

            }
            else if (option == "对比图片")
            {

                // 算平均方差, 平均差
                var pixs_left = LeftImage.GetColor32(out int w, out int h);
                var pixs_right = RightImage.GetColor32(out _, out _);
                if (pixs_left.Length != pixs_right.Length)
                {
                    DU.LogWarning("大小不相等");
                    return;
                }

                int count = 0;
                int diff = 0;
                int square_diff = 0;

                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int index = y * w + x;
                        var left = pixs_left[index];
                        var right = pixs_right[index];
                        if (left.a == 0 || right.a == 0)
                            continue;
                        count += 3;
                        int dr = left.r - right.r, dg = left.g - right.g, db = left.b - right.b;
                        diff = diff + Math.Abs(dr) + Math.Abs(dg) + Math.Abs(db);
                        square_diff = square_diff + dr * dr + dg * dg + db * db;
                    }

                float average_diff = (float)diff / count;
                float average_square_diff = (float)square_diff / count;
                DU.LogWarning($"两图像的平均差：{DU.FloatFormat(average_diff, 2)}；平均方差：{DU.FloatFormat(average_square_diff, 2)} ");
            }
            else if (option == "打印数据")
            {
                string str1 = "";
                string str2 = "";
                string str3 = "";
                for (int i = 0; i < _strList.Count; i++)
                {
                    var str = _strList[i];
                    var color1 = _colorList1[i];
                    var color2 = _colorList2[i];
                    str1 += $"#{i + 1}.{str}/";
                    str2 += $"({color1.x},{color1.y},{color1.z})/";
                    str3 += $"({color2.x},{color2.y},{color2.z})/";
                }

                DU.LogWarning($"{str1}\n{str2}\n{str3}");

                _strList.Clear();
                _colorList1.Clear();
                _colorList2.Clear();
            }
            else if (option == "处理文本")
            {
                var str = InputText.text;
                var itemList = str.Split("/");
                Vector3Int sum = default;
                Vector3Int max = default;
                Vector3Int min = default;

                float g2r_min = 0;
                float g2r_max = 0;
                float b2g_min = 0;
                float b2g_max = 0;
                float b2r_min = 0;
                float b2r_max = 0;

                foreach (var item in itemList)
                {
                    var nums = item.Substring(1, item.Length - 2).Split(",");
                    var x = int.Parse(nums[0]);
                    var y = int.Parse(nums[1]);
                    var z = int.Parse(nums[2]);
                    var g2r = (float)y / x;
                    var b2g = (float)z / y;
                    var b2r = (float)z / x;
                    sum.x += x;
                    sum.y += y;
                    sum.z += z;

                    if (max == default)
                        max = new Vector3Int(x, y, z);
                    if (min == default)
                        min = new Vector3Int(x, y, z);
                    if (g2r_min == 0)
                        g2r_min = g2r;
                    if (g2r_max == 0)
                        g2r_max = g2r;
                    if (b2g_min == 0)
                        b2g_min = b2g;
                    if (b2g_max == 0)
                        b2g_max = b2g;
                    if (b2r_min == 0)
                        b2r_min = b2r;
                    if (b2r_max == 0)
                        b2r_max = b2r;

                    max.x = x > max.x ? x : max.x;
                    max.y = y > max.y ? y : max.y;
                    max.z = z > max.z ? z : max.z;
                    min.x = x < min.x ? x : min.x;
                    min.y = y < min.y ? y : min.y;
                    min.z = z < min.z ? z : min.z;
                    if (g2r < g2r_min) g2r_min = g2r;
                    if (g2r > g2r_min) g2r_max = g2r;
                    if (b2g < b2g_min) b2g_min = b2g;
                    if (b2g > b2g_min) b2g_max = b2g;
                    if (b2r < b2r_min) b2r_min = b2r;
                    if (b2r > b2r_min) b2r_max = b2r;
                }
                int len = itemList.Length;
                Vector3Int average = new Vector3Int(sum.x / len, sum.y / len, sum.z / len);
                DU.LogWarning($"最小值({min.x},{min.y},{min.z}) 最大值({max.x},{max.y},{max.z}) 平均({average.x},{average.y},{average.z})\n"
                + $"g2r最小值{g2r_min}最大值{g2r_max}\n" + $"b2g最小值{b2g_min}最大值{b2g_max}\n"
                + $"b2r最小值{b2r_min}最大值{b2r_max}\n");
                InputText.text = "";
            }



            DU.LogWarning("成功");
        }

        #endregion
        void OnDestroy()
        {
            ClearMapOptionCache(LeftImage);         // 清理LeftImage、RightImage绑定的缓存
            ClearMapOptionCache(RightImage);

            ClearLeftMat();
            ClearResult();
        }


        public override void Close()
        {
            base.Close();
            UIManager.Inst.ShowPanel(PanelEnum.TemplateMatchDrawResultPanel, new List<object> { null, 0 });
        }

        #endregion
        #region SplitBagCell

        public GameItemCfgManager Config => GameItemCfgManager.Inst;

        void SplitBagCell()
        {

            if (_leftTex == null) return;

            var pixs = _leftTex.GetPixelData<Color32>(0);
            Texture2D texture = new Texture2D(_imgW, _imgH, TextureFormat.RGBA32, false);

            // 填充纹理数据
            Color32[] tar = new Color32[_imgW * _imgH];
            Color32Image img = new Color32Image(tar, _imgW, _imgH);
            int w = img.W, h = img.H;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var index = y * w + x;
                    tar[index] = pixs[index];
                }

            var bagCells = new List<BagCell>(60);
            for (int i = 0; i < 60; i++)
            {
                bagCells.Add(new BagCell());
            }

            // 处理输入信息
            // 第一排背包格的高是39px，其它排是40px；第5列背包格的宽是39px，其它列是40px
            //
            for (int i = 0; i < 5; i++)
            {
                // var y = 18 + 39 * i;
                var y = i < 4 ? 153 + 39 * i : 152 + 39 * i;

                for (int j = 0; j < 12; j++)
                {
                    var cell = bagCells[i * 12 + j];
                    var x = j < 5 ? 1099 + 39 * j : 1098 + 39 * j;
                    cell.Row = 5 - i;
                    cell.Column = j + 1;
                    cell.LeftUp = new Vector2Int(x, y);
                    cell.Size = new Vector2Int(j != 4 ? 40 : 39, i != 4 ? 40 : 39);
                }

            }

            var debug_texts = new List<(string, Vector2Int)>();

            DU.RunWithTimer(() =>
            {
                // 识别输入的背包格 
                //
                foreach (BagCell cell in bagCells)
                {
                    var startP = cell.StartP;
                    var size = cell.Size;
                    // debug
                    // if (cell.Row == 4 && cell.Column == 5)
                    // {
                    //     var k = 1;
                    // }

                    string item_debug = "";
                    string number_debug = "";
                    string roman_debug = "";

                    RecogUtil.IdentifyItem(img, out int item_id, out float item_score, out var a_diff);
                    RecogUtil.IdentifyFrame(img, false, out ItemGridFrame frame_id);

                    var frame_debug = "";
                    if (frame_id != ItemGridFrame.Empty) frame_debug = $"框{(int)frame_id}/\n";

                    if (item_id > 0)
                    {
                        var item = Config.GetItem(item_id);
                        // 以左下角不行就以左上角。 5/23
                        var numberPos = new Vector2Int(startP.x + 5, cell.LeftUp.y - 16);

                        RecogUtil.IdentifyNumber(img, false, out int number);

                        item_debug = $"{SU.GetString(item.Name)}/ {(item_score == 0 ? "0" : GetDebugStr(item_score) + "/ " + GetDebugStr(a_diff))}\n";
                        number_debug = $"数{number}/ \n";
                        // roman_debug = romanNumber > 1 ?
                        // $"阶{romanNumber}/ {(roman_score == 1 ? "1" : GetDebugStr(roman_score))}\n"
                        //  : "";
                    }
                    var debug_str = item_debug + number_debug + roman_debug + frame_debug;

                    if (debug_str.EndsWith("\n")) debug_str = debug_str.Substring(0, debug_str.Length - 1);
                    debug_texts.Add((debug_str, startP + cell.Size / 2));
                }

            }, "IdentifyBagCell");


            // 画分割线
            //
            foreach (BagCell cell in bagCells)
            {
                var startP = cell.StartP;
                var x_start = startP.x;
                var y_start = startP.y;
                var x_end = startP.x + cell.Size.x;
                var y_end = startP.y + cell.Size.y;
                for (int y = startP.y; y < y_end; y++)
                {
                    tar[y * w + x_start] = color_red;
                    tar[y * w + x_end - 1] = color_red;
                }

                for (int x = startP.x; x < x_end; x++)
                {
                    tar[y_start * w + x] = color_red;
                    tar[(y_end - 1) * w + x] = color_red;
                }
            }


            // 应用像素数据到纹理
            texture.SetPixels32(tar);
            texture.Apply();

            // 清理
            ClearResult();

            _resultSpr = Sprite.Create(texture, new UnityEngine.Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            RightImage.SetData(_resultSpr);


            RightImage.SetTextUI(debug_texts, 20, 3, Color.yellow);

        }


        #endregion

        string GetDebugStr(float num)
        {
            return $"<color=#FF0000>{DU.FloatFormat(num, 2)}</color>";
        }

        /// <summary>
        /// 罗马数字三个符号。Item1是数字值，Item2是图像数据
        /// </summary>
        RomanNumberTmpl[] romanNumberTmpls;
        Color32 black_color = new Color32(0, 0, 0, 255);

        /// <summary>
        /// 固定位置的识别， 一个通道就行
        /// 输出完整的数，和匹配度最低的分数，方便断点审查
        /// </summary>
        void IdentifyNumber(Color32Image image, Vector2Int offset, out int number, out float score)
        {
            var Config = GameItemCfgManager.Inst;
            number = 1;
            score = 1;
            var colors = image.Colors;
            int w = image.W, h = image.H;

            List<(int, float)> result = new List<(int, float)>();
            // tar[offset.y * w + offset.x] = color_red;   // debug

            // while (true)
            // {
            //     int black_count = 0;
            //     for (int i = 0; i < 12; i++)
            //         for (int j = 0; j < 7; j++)
            //         {
            //             var x = offset.x + j;
            //             var y = offset.y + i;
            //             var index = y * w + x;
            //             Color32 a = colors[index];
            //             Color32 b = black_color;
            //             if (a.r == b.r && a.g == b.g && a.b == b.b)
            //                 black_count++;
            //         }
            //     if (black_count < 2)
            //         break;

            //     var fit_list = new float[10];
            //     int max_fit_num = 0;
            //     float max_fit_score = 0;
            //     for (int image_i = 0; image_i < 10; image_i++)
            //     // for (int image_i = 1; image_i < 2; image_i++)
            //     {
            //         var template = Config.numberTmpls[image_i];
            //         var list = template.Item1;
            //         var fit_count = 0;
            //         foreach (var pos in list)
            //         {
            //             var index = (pos.y + offset.y) * w + pos.x + offset.x;
            //             Color32 a = colors[index];
            //             Color32 b = black_color;
            //             // input[index] = new Color32(255, 0, 0, 255);
            //             if (a.r == b.r && a.g == b.g && a.b == b.b)
            //                 fit_count++;
            //         }

            //         float fit_score = (float)fit_count / list.Count;
            //         fit_list[image_i] = fit_score;
            //         if (fit_score > max_fit_score)
            //         {
            //             max_fit_score = fit_score;
            //             max_fit_num = image_i;
            //         }
            //     }


            //     result.Add((max_fit_num, max_fit_score));

            //     // 至少要一半的黑像素符合。不达及格线就终止
            //     if (max_fit_score < 0.5f)
            //         break;


            //     offset = offset + new Vector2Int(7, 0);
            // }

            if (result.Count == 0)
                return;

            // 拼接字符串，再转为数字
            string s = "";
            float min = 1;
            foreach (var pair in result)
            {
                s += pair.Item1;
                min = pair.Item2 < min ? pair.Item2 : min;
            }
            number = int.Parse(s);
            score = min;
        }





        void IdentifyRomanNumber(Color32Image image, Vector2Int offset, out int number, out float score)
        {
            if (romanNumberTmpls == null)
                InitRomanNumberTemplate();

            number = 1;
            score = 1;
            var colors = image.Colors;
            int w = image.W, h = image.H;

            {
                int black_count = 0;
                for (int i = 0; i < 11; i++)
                {
                    var y = offset.y + i;
                    var index = y * w + offset.x;
                    if (IU.Color32Equal(colors[index], black_color))
                    {
                        black_count++;
                    }
                }

                if (black_count == 0)
                    offset += new Vector2Int(-1, 0);
            }

            colors[offset.y * w + offset.x] = color_red;   // debug

            List<(int, float)> result = new List<(int, float)>();
            while (true)
            {
                int black_count = 0;
                for (int i = 0; i < 11; i++)
                {
                    var y = offset.y + i;
                    var index = y * w + offset.x;
                    if (IU.Color32Equal(colors[index], black_color))
                    {
                        black_count++;
                    }
                }

                if (black_count == 0)
                    break;

                var roman_count = romanNumberTmpls.Length;
                var fit_list = new float[roman_count];
                RomanNumberTmpl max_fit = null;
                float max_fit_score = 0;

                for (int image_i = 0; image_i < roman_count; image_i++)
                {
                    // var image_i = 0;
                    var tmpl = romanNumberTmpls[image_i];
                    var img = tmpl.Image;
                    var total_count = 0;
                    var fit_count = 0;
                    var x_start = offset.x - tmpl.W + 1;
                    for (int i = 0; i < tmpl.H; i++)
                        for (int j = 0; j < tmpl.W; j++)
                        {
                            var x = x_start + j;
                            var y = offset.y + i;
                            var color_i = colors[y * w + x];
                            var color_t = img[i * tmpl.W + j];
                            if (color_t.r > 0)
                            {
                                // input[y * w + x] = new Color32(255, 0, 0, 255);
                                total_count++;
                                if (color_i.r - color_t.r >= -1 && color_i.r - color_t.r <= 1)
                                {
                                    fit_count++;
                                }
                            }

                        }

                    float fit_score = (float)fit_count / total_count;
                    fit_list[image_i] = fit_score;
                    if (fit_score > max_fit_score)
                    {
                        max_fit_score = fit_score;
                        max_fit = tmpl;
                    }
                }

                if (max_fit == null)
                    break;
                result.Add((max_fit.Num, max_fit_score));


                // 至少要一半的黑像素符合。不达及格线就终止
                if (max_fit_score < 0.5f)
                    break;


                offset = offset - new Vector2Int(max_fit.Holder, 0);
            }

            if (result.Count == 0)
                return;

            // 如XVIII，只有右侧连续的I是+1其他是-1
            float min = 1;
            number = 0;

            foreach (var pair in result)
            {
                number += pair.Item1;
                min = pair.Item2 < min ? pair.Item2 : min;
            }

            score = min;
        }

        void InitRomanNumberTemplate()
        {
            romanNumberTmpls = new RomanNumberTmpl[5];
            var pre_path = $"{Application.streamingAssetsPath}/GameItem/RomanNum";

            romanNumberTmpls[0] = new RomanNumberTmpl { Num = 9, W = 10, H = 11, Holder = 10 };
            romanNumberTmpls[1] = new RomanNumberTmpl { Num = 4, W = 11, H = 11, Holder = 11 };
            romanNumberTmpls[2] = new RomanNumberTmpl { Num = 10, W = 7, H = 11, Holder = 7 };
            romanNumberTmpls[3] = new RomanNumberTmpl { Num = 5, W = 8, H = 11, Holder = 8 };
            romanNumberTmpls[4] = new RomanNumberTmpl { Num = 1, W = 4, H = 11, Holder = 3 };


            for (int image_i = 0; image_i < romanNumberTmpls.Length; image_i++)
            {
                var tmpl = romanNumberTmpls[image_i];
                var path = $"{pre_path}/{tmpl.Num}.png";
                using (var bitmap = new Bitmap(path))
                {
                    var colors = IU.BitmapToColor32(bitmap);
                    IU.Color32ReverseYAxis(colors, tmpl.W);
                    tmpl.Image = colors;
                }
            }
        }


        // status:0-无, 1-选择框, 2-待选框
        void IdentifyFrame(Color32Image image, Vector2Int offset, out string status, out float score)
        {
            status = "0";
            score = 0;

            var colors = image.Colors;
            int w = image.W, h = image.H;

            var yellow_color = new Color32(231, 206, 108, 255);

            // 是否为选择框
            // (1,1)至(1,4) 四个点呈黄色
            int fit_count = 0;
            for (int y = 1; y <= 4; y++)
            {
                var index = (y + offset.y) * w + 1 + offset.x;
                var col = colors[index];
                if (col.r - yellow_color.r >= -10 && col.r - yellow_color.r <= 10
                    && col.g - yellow_color.g >= -10 && col.g - yellow_color.g <= 10
                    && col.b - yellow_color.b >= -10 && col.b - yellow_color.b <= 10)
                    fit_count++;
            }

            if (fit_count > 3)
            {
                status = "1";
                score = (float)fit_count / 4;
                return;
            }

            // 是否为待选框
            // (1,0)至(10,0) 10个点呈-高于(70,60,50)
            var t_color = new Color32(70, 60, 50, 255);
            fit_count = 0;
            for (int x = 1; x <= 10; x++)
            {
                var index = (0 + offset.y) * w + x + offset.x;
                var col = colors[index];
                if (col.r >= t_color.r && col.g >= t_color.g && col.b >= t_color.b)
                    fit_count++;
            }

            if (fit_count > 7)
            {
                status = "2";
                score = (float)fit_count / 10;
                return;
            }

        }


        #region IdentItem

        // void IdentifyItem(Color32Image image, Vector2Int startP, Vector2Int cell_size,
        //                 out string id, out float score, out float a_diff)
        // {
        //     GameItemCfgManager Config = GameItemCfgManager.Inst;
        //     id = null;
        //     score = 0;
        //     a_diff = 0;

        //     var colors = image.Colors;
        //     var w = image.W;

        //     Vector2Int size = cell_size - new Vector2Int(2, 2);
        //     List<Vector2Int> sample_points = Config.BlurPoints;
        //     List<(string, Color32[])> tmpls = Config.ImageTmplsList;
        //     var sample_count = sample_points.Count;
        //     var tmpl_count = tmpls.Count;

        //     // 中间10X10是黑的就表明为空背包格
        //     Vector2Int m_off = startP + new Vector2Int(15, 15);
        //     int black_sum = 0;
        //     for (int dy = 0; dy < 10; dy++)
        //         for (int dx = 0; dx < 10; dx++)
        //         {
        //             var col = colors[(dy + m_off.y) * w + dx + m_off.x];
        //             black_sum = black_sum + col.r + col.g + col.b;
        //         }
        //     int average_black = black_sum / 300;
        //     if (average_black < 10)
        //         return;

        //     Vector2Int off = startP + new Vector2Int(1, 1);
        //     var sampl_result = new Color32[sample_count];

        //     int s = Config.BlurScale;
        //     int blur_size = s * s;
        //     for (int i = 0; i < sample_count; i++)
        //     {
        //         var p = sample_points[i];
        //         int r = 0, g = 0, b = 0;
        //         for (int dy = 0; dy < s; dy++)
        //             for (int dx = 0; dx < s; dx++)
        //             {
        //                 var col = colors[(p.y + dy + off.y) * w + p.x + dx + off.x];
        //                 r += col.r;
        //                 g += col.g;
        //                 b += col.b;
        //             }
        //         var blur_col = new Color32((byte)(r / blur_size), (byte)(g / blur_size), (byte)(b / blur_size), 255);
        //         sampl_result[i] = blur_col;
        //     }

        //     // 全匹配
        //     // score = float.MaxValue;
        //     // foreach (var pair in tmpls)
        //     // {
        //     //     var c2 = pair.Item2;
        //     //     CompareTwoImage(sampl_result, c2, out var average_diff, out var diff);
        //     //     if (diff < score)
        //     //     {
        //     //         id = pair.Item1;
        //     //         score = diff;
        //     //         a_diff = average_diff;
        //     //     }
        //     // }


        //     // 如同淘汰制比赛一样，冠军最后胜出。
        //     // threshold 淘汰阈值，当累计差达到阈值时淘汰此匹配项，但至少保留一项
        //     //
        //     float thr = 20 * 3 * sample_count;
        //     DiffInfo[] diffs = new DiffInfo[tmpl_count];    // DiffInfo = (模版序,累计差)
        //     for (int i = 0; i < tmpl_count; i++)
        //     {
        //         diffs[i] = new DiffInfo() { Id = i, Sum = 0 };
        //     }

        //     int remain = tmpl_count;
        //     for (int i = 0; i < sample_count; i++)
        //     {
        //         var index = 0;
        //         var left = sampl_result[i];
        //         for (int j = 0; j < remain; j++)
        //         {
        //             DiffInfo t_diff = diffs[j];
        //             var pair = tmpls[t_diff.Id];

        //             Color32[] t_colors = pair.Item2;
        //             Color32 right = t_colors[i];
        //             int dr = left.r - right.r, dg = left.g - right.g, db = left.b - right.b;
        //             t_diff.Sum = t_diff.Sum + dr * dr + dg * dg + db * db;
        //             diffs[j] = t_diff;

        //             if (t_diff.Sum < thr)
        //             {
        //                 diffs[index++] = t_diff;
        //             }
        //         }

        //         if (index <= 1)
        //         {
        //             if (index == 0)     // 保留方差最小的
        //                 for (int j = 1; j < remain; j++)
        //                 {
        //                     if (diffs[j].Sum < diffs[0].Sum)
        //                         diffs[0] = diffs[j];
        //                 }
        //             break;
        //         }

        //         remain = index;
        //     }
        //     int result = diffs[0].Id;


        //     var tmpl = tmpls[result];
        //     MakeImageUtil.CompareTwoImage(sampl_result, tmpl.Item2, out var average_diff, out var diff);
        //     id = tmpl.Item1;
        //     score = diff;
        //     a_diff = average_diff;

        //     // var startP = cell_startP;
        //     // var RomanPos = startP + new Vector2Int(37, 5);
        //     // var RomanPos = new Vector2Int(startP.x + 37, startP.y + size.y - 35);
        //     // IdentifyRomanNumber(image, RomanPos, out int romanNumber, out float roman_score);
        // }




        #endregion

    }





    public class BagCell
    {
        public Vector2Int LeftUp;
        public Vector2Int StartP => new Vector2Int(LeftUp.x, LeftUp.y - Size.y + 1);
        public Vector2Int Size;
        public int Row;             // 背包中的第几行
        public int Column;          // 背包中的第几列

        public string ItemId;
        public string Num;

        public float ItemIdentScore;
        public float NumIdentScore;
    }

    public class RomanNumberTmpl
    {
        public int Num;                 // 代表数字几
        public Color32[] Image;         // 图像数据
        public int W;                   // 宽
        public int H;                   // 高
        public int Holder;              // 占位

    }



}