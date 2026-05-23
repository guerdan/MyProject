
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using OfficeOpenXml;
using Script.Util;
using UnityEngine;

namespace Script.Model.Auto
{
    public class GameItemCfgManager
    {
        private static GameItemCfgManager _inst;
        public static GameItemCfgManager Inst
        { get { if (_inst == null) _inst = new GameItemCfgManager(); return _inst; } }
        public readonly int BlurScale = 4;
        public readonly Vector2Int CellSize = new Vector2Int(41, 41);       // 一般格子的大小
        public readonly Vector2Int SNumStart = new Vector2Int(5, 27);       // 一般格子的数字起点
        public readonly Vector2Int BNumStart = new Vector2Int(5, 21);       // 一般格子的数字起点
        public readonly Vector2Int NumExcept = new Vector2Int(22, 21);      // 一般格子，排除的左上角

        public readonly Vector2Int CurrCellSize = new Vector2Int(43, 43);   // 通货页格子的大小
        public readonly Vector2Int CurrNumStart = new Vector2Int(6, 28);    // 通货页格子的数字起点

        public readonly Vector2Int CurrCusCellStart = new Vector2Int(495, 219); // 通货页自定义格子的起点
        public readonly Vector2Int CurrCusCellOff = new Vector2Int(45, 45);     // 通货页自定义格子的偏移


        public readonly Vector2Int NormSFrame = new Vector2Int(1,1);            // 一般格子，选中框偏移
        public readonly Vector2Int CurrSFrame = new Vector2Int(2,2);            // 通货页格子，选中框偏移
        public readonly Vector2Int NormTFrame = new Vector2Int(1,1);            // 一般格子，目标框偏移


        // 采样坐标列表          定义图片模糊化采样的点   61个
        public List<Vector2Int> BlurPoints;     

                
        // Dic<id, 数据>        存储图片转化后的信息
        public GameItemCfg[] MatchList;
        public Dictionary<int, GameItemCfg> IdCfg;

        /// <summary>
        /// (0,0,0)黑色是特征。
        /// </summary>
        public Color32Image[] SNumTmpls;     // 小号数字，特征图像
        public Color32Image[] BNumTmpls;     // 大号数字，特征图像

        private bool init = false;
        public void Init()
        {
            if (init)
                return;
            init = true;

            var dirPath = @"D:\unityProject\MyProject_Resource\物品格";
            var excelPath = $"{dirPath}/物品.xlsx";
            if (!File.Exists(excelPath))
                throw new Exception($"word文件不存在");


            IdCfg = new Dictionary<int, GameItemCfg>();

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage(new FileInfo(excelPath)))
            {
                // 获取第一个工作表
                var worksheet = package.Workbook.Worksheets[0];

                // 获取行数和列数
                int rowCount = worksheet.Dimension.Rows;
                int colCount = worksheet.Dimension.Columns;

                for (int row = 2; row <= rowCount; row++)
                {
                    var first = worksheet.Cells[row, 1].Text;
                    if (first == "")
                        continue;
                    var id = int.Parse(first);
                    var name = worksheet.Cells[row, 2].Text;
                    var path = worksheet.Cells[row, 3].Text;
                    var pos_str = worksheet.Cells[row, 4].Text;

                    Vector2Int pos = default;
                    if (pos_str.Length > 0)
                    {
                        pos_str = pos_str.Replace(" ", "");
                        pos_str = pos_str.Substring(1, pos_str.Length - 2);
                        var pos_l = pos_str.Split(",");
                        pos = new Vector2Int(int.Parse(pos_l[0]), int.Parse(pos_l[1]));
                    }

                    var cfg = new GameItemCfg(id, name, path, pos);
                    IdCfg.Add(id, cfg);
                }
            }

            BlurPoints = GetGameItemBlurPoints();

            var List = new List<GameItemCfg>();
            foreach (var item in IdCfg.Values)
            {
                var path = $"{dirPath}/{item.Path}.png";
                if (!File.Exists(path))
                    continue;

                var img = RecogUtil.GetImg(path);
                var sample = MakeImageUtil.GetBlurValue(img, BlurScale, BlurPoints);
                item.sample = sample;
                List.Add(item);
            }
            MatchList = List.ToArray();


            // 数字
            //
            SNumTmpls = new Color32Image[10];
            BNumTmpls = new Color32Image[10];

            for (int i = 0; i < 10; i++)
            {
                var path = $"{dirPath}/Num/s_{i}.png";

                var img = RecogUtil.GetImg(path);
                img = RecogUtil.FilterImg(img, RecogUtil.color_black, default, true);
                SNumTmpls[i] = img;
            }

            for (int i = 0; i < 10; i++)
            {
                var path = $"{dirPath}/Num/b_{i}.png";

                var img = RecogUtil.GetImg(path);
                img = RecogUtil.FilterImg(img, RecogUtil.color_black, default, true);
                BNumTmpls[i] = img;
            }

            // @debug
            // DebugImageTmplsQuality();


            // DU.RunWithTimer(() =>
            // {
            // 测试性能
            // foreach (var pair in ImageTmpls)
            // {
            //     var list = pair.Value;
            //     for (int i = 0; i < 990; i++)
            //     {
            //         list.Add(("100", new Color32[100]));
            //     }
            // }
            // }, "InitGameItemTemplate");


        }

        public GameItemCfg GetItem(int id)
        {
            IdCfg.TryGetValue(id, out var result);
            return result;
        }


        public void DebugImageTmplsQuality()
        {
            var tmpls = IdCfg.ToList();
            List<ImageTmplDebugData> debugs = new List<ImageTmplDebugData>();

            for (int i = 0; i < tmpls.Count; i++)
            {
                var left = tmpls[i];
                if (left.Value.sample == null) continue;

                var debug = new ImageTmplDebugData();
                debugs.Add(debug);
                debug.LeftId = left.Key;

                float min_diff = float.MaxValue;
                for (int j = 0; j < tmpls.Count; j++)
                {
                    if (j == i) continue;
                    var right = tmpls[j];
                    if (right.Value.sample == null) continue;

                    MakeImageUtil.CompareTwoImage(left.Value.sample, right.Value.sample, out var a_diff, out var a_square_diff);
                    if (a_square_diff < min_diff)
                    {
                        min_diff = a_square_diff;
                        debug.RightId = right.Key;
                        debug.AverageDiff = a_diff;
                        debug.AverageSquareDiff = a_square_diff;
                    }
                }
            }

            string str = "";
            foreach (var data in debugs)
            {
                str += $"{data.LeftId}与{data.RightId}最像，均差{DU.FloatFormat(data.AverageDiff, 2)}，均方差{DU.FloatFormat(data.AverageSquareDiff, 2)}\n";
            }

            DU.LogWarning(str);
        }


        /// <summary>
        /// 采样点
        /// </summary>
        public List<Vector2Int> GetGameItemBlurPoints()
        {
            int w = CellSize.x;
            int h = CellSize.y;
            int s = BlurScale;

            var spos = NumExcept + new Vector2Int(1, 0);

            List<Vector2Int> result = new List<Vector2Int>();

            int xs = 1;         // 边框厚度为1px
            int ys = 1;
            var xe = w - s - 1;
            var ye = h - s - 1;
            // 分成3块
            for (int y = spos.y; y <= ye; y += s)
                for (int x = spos.x; x <= xe; x += s)
                {
                    result.Add(new Vector2Int(x, y));
                }

            for (int y = spos.y - s; y >= ys; y -= s)
                for (int x = spos.x; x <= xe; x += s)
                {
                    result.Add(new Vector2Int(x, y));
                }
            for (int y = spos.y - s; y >= ys; y -= s)
                for (int x = spos.x - s; x >= xs; x -= s)
                {
                    result.Add(new Vector2Int(x, y));
                }

            return result;
        }


    }

    public class GameItemCfg
    {
        // 序列化的 ——————————
        public int Id;
        public string Name;
        public string Path;
        public Vector2Int Pos;      // >0 代表在通货页有固定位置，能够无限堆叠。

        // ——————————————————

        public Color32[] sample;    // 样本的采样结果

        public GameItemCfg(int id, string name, string path, Vector2Int pos)
        {
            Id = id;
            Name = name;
            Path = path;
            Pos = pos;
        }
    }


    public class ImageTmplDebugData
    {
        public int LeftId;
        public int RightId;
        public float AverageDiff;           // 最小平均差
        public float AverageSquareDiff;     // 最小平均平方差
    }
}