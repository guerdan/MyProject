
using System.Collections.Generic;
using Script.Util;
using UnityEngine;

namespace Script.Model.Auto
{
    public class GameItemCfg
    {
        private static GameItemCfg _inst;
        public static GameItemCfg Inst
        { get { if (_inst == null) _inst = new GameItemCfg(); return _inst; } }

        public Dictionary<string, GameItem> IdCfg;
        public Dictionary<GameItemEnum, GameItem> EnumCgf;

        public readonly int BlurScale = 4;

        // Dic<size, 采样坐标列表>          定义图片模糊化采样的点
        public Dictionary<Vector2Int, List<Vector2Int>> BlurPoints;
        // Dic<size,  Dic<id, 数据>>        存储图片转化后的信息
        public Dictionary<Vector2Int, Dictionary<string, Color32[]>> ImageTmpls;
        // Dic<size,  List<(id, 数据)>>        ImageTmpls的列表形式
        public Dictionary<Vector2Int, List<(string, Color32[])>> ImageTmplsList;

        public void Init()
        {
            var List = new List<GameItem>()
            {
                new GameItem("1",GameItemEnum.HolyStone,"神圣石",1),
                new GameItem("2",GameItemEnum.HolyStoneII,"神圣石II",2),
                new GameItem("3",GameItemEnum.HolyStoneIII,"神圣石III",3),
                new GameItem("4",GameItemEnum.ChaosStone,"混沌石",1),
                new GameItem("5",GameItemEnum.ChaosStoneII,"混沌石II",2),
                new GameItem("6",GameItemEnum.ChaosStoneIII,"混沌石III",3),
                new GameItem("7",GameItemEnum.NobleStone,"崇高石",1),
                new GameItem("8",GameItemEnum.NobleStoneII,"崇高石II",2),
                new GameItem("9",GameItemEnum.NobleStoneIII,"崇高石III",3),
                new GameItem("10",GameItemEnum.InvalidStone,"无效石",1),
                new GameItem("11",GameItemEnum.ValStone,"瓦尔石",1),
                new GameItem("12",GameItemEnum.RichStone,"富豪石",1),
                new GameItem("13",GameItemEnum.PromoteStone,"增幅石",1),
                new GameItem("14",GameItemEnum.TransformStone,"蜕变石",1),
                new GameItem("15",GameItemEnum.MapStoneXIV,"地图14",14),
                new GameItem("16",GameItemEnum.MapStoneXV,"地图15",15),
                new GameItem("100",GameItemEnum.ItemX,"未定义",1),
            };

            IdCfg = new Dictionary<string, GameItem>();
            EnumCgf = new Dictionary<GameItemEnum, GameItem>();

            foreach (var item in List)
            {
                IdCfg[item.Id] = item;
                EnumCgf[item.Enum] = item;
            }

            DU.RunWithTimer(() =>
            {
                BlurPoints = MakeImageUtil.GetGameItemBlurPoints();
                ImageTmpls = MakeImageUtil.DealGameItem();
                ImageTmplsList = new Dictionary<Vector2Int, List<(string, Color32[])>>();
                foreach (var pair in ImageTmpls)
                {
                    var list = new List<(string, Color32[])>();
                    ImageTmplsList[pair.Key] = list;
                    foreach (var pair1 in pair.Value)
                    {
                        list.Add((pair1.Key, pair1.Value));
                    }


                    // 测试性能
                    // for (int i =0;i < 990; i++)
                    // {
                    //     list.Add(("100",new Color32[100]));
                    // }
                }
            }, "InitGameItemTemplate");

        }

        public GameItem GetItem(string id)
        {
            return IdCfg[id];
        }
        public GameItem GetItem(GameItemEnum Enum)
        {
            return EnumCgf[Enum];
        }

        public void DebugImageTmplsQuality()
        {
            var tmpls = ImageTmplsList[new Vector2Int(38, 38)];
            ImageTmplDebugData[] debugs = new ImageTmplDebugData[tmpls.Count];

            for (int i = 0; i < tmpls.Count; i++)
            {
                var left = tmpls[i];
                var debug = new ImageTmplDebugData();
                debugs[i] = debug;
                debug.LeftId = left.Item1;

                float min_diff = float.MaxValue;
                for (int j = 0; j < tmpls.Count; j++)
                {
                    if (j == i) continue;
                    var right = tmpls[j];
                    MakeImageUtil.CompareTwoImage(left.Item2, right.Item2, out var a_diff, out var a_square_diff);
                    if (a_square_diff < min_diff)
                    {
                        min_diff = a_square_diff;
                        debug.RightId = right.Item1;
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

    }

    public class GameItem
    {
        public string Id;
        public GameItemEnum Enum;
        public string Name;
        public int Rank;

        public GameItem(string id, GameItemEnum Enum, string name, int rank)
        {
            Id = id;
            this.Enum = Enum;
            Name = name;
            Rank = rank;
        }
    }

    public enum GameItemEnum
    {
        HolyStone,          // 圣神石
        HolyStoneII,
        HolyStoneIII,
        ChaosStone,         // 混沌石
        ChaosStoneII,
        ChaosStoneIII,
        NobleStone,         // 崇高石

        NobleStoneII,

        NobleStoneIII,

        InvalidStone,       // 无效石
        ValStone,           // 瓦尔石
        RichStone,          // 富豪石
        PromoteStone,       // 增幅石
        TransformStone,     // 蜕变石
        MapStoneXIV,        // 引路石 14阶
        MapStoneXV,         // 引路石 15阶
        ItemX,         // 引路石 15阶

    }


    public class ImageTmplDebugData
    {
        public string LeftId;
        public string RightId;
        public float AverageDiff;           // 最小平均差
        public float AverageSquareDiff;     // 最小平均平方差
    }
}