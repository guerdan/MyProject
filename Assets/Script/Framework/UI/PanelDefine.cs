using System.Collections.Generic;

namespace Script.Framework.UI
{

    public enum UITypeEnum
    {
        Full,        // 全屏界面，层级0
        PopUp,       // 弹窗界面，层级0
        TopPopUp,    // 顶层弹窗，层级1，如：战力提升弹窗
        TopSystem,   // 顶层系统通知界面，层级2，如：断线重连弹窗、转菊花、轮播通知栏
    }

    //当关闭Layer=0的界面栈的最后个界面时，调用AssetManager.ReleaseUnuseAsset真正回收资源
    public enum UIRecycleTypeEnum
    {
        Once,          //关闭界面后，立即销毁实例。
        Normal,        //关闭界面10秒后，销毁实例。
        Frequent,      //关闭界面60秒后，销毁实例。
    }


    /// <summary>
    /// 定义界面名称
    /// </summary>
    public enum PanelEnum
    {
        HeroDetailPanel,
        HeroMainPanel,
        BattleUiView,
        StageDefeatPageView,
        NewStageWinPageView,
        BattleRulePopUpView,
        BattleRuleElementPopUpView,
    }


    /// <summary>
    /// 面向UIManager
    /// </summary>
    public class PanelDefine
    {
        public PanelEnum Key;       //访问key
        public string Name;         //名字
        public string Path;         //预制体路径
        public UITypeEnum Type;     //界面类型
        public UIRecycleTypeEnum RecycleType;   //回收类型

        public bool ShowGrayBg = true;          //是否显示灰色背景。Full界面无此功能，其他界面有。默认为true

        // 是否因遮挡关系而隐藏上个界面。默认为true
        // 打开一个Full界面，栈中的界面全隐藏
        // 打开一个PopUp界面，栈中的界面除了最上方的Full界面其他都隐藏。
        public bool HideLastPanel = true;

        // public bool TopSingle = true;   // 最上层只能弹出一份弹窗，防连点。默认为true。如果加载时屏蔽，实例出来后也屏蔽，那就无懈可击

        public int Layer { get { return PanelUtil.GetLayer(Type); } }
        public bool GetShowGrayBg { get { return Type != UITypeEnum.Full && ShowGrayBg; } }

    }

    /// <summary>
    /// 面向UIManager
    /// </summary>
    public static class PanelUtil
    {

        public static int RecycleTimeNormal = 10;
        public static int RecycleTimeFrequent = 60;

        static Dictionary<PanelEnum, PanelDefine> _panelDefineDic;

        public static Dictionary<PanelEnum, PanelDefine> PanelDefineDic
        {
            get
            {
                if (_panelDefineDic == null)
                {
                    _panelDefineDic = new Dictionary<PanelEnum, PanelDefine>();
                    foreach (var item in PanelDefineList)
                    {
                        _panelDefineDic.Add(item.Key, item);
                    }
                }
                return _panelDefineDic;
            }
        }

        static List<PanelDefine> PanelDefineList = new List<PanelDefine>(){
            new PanelDefine(){
                Key = PanelEnum.HeroDetailPanel,
                Name = "HeroDetailPanel",
                Path = "Hero/Prefabs/HeroDetailPanel",
                Type = UITypeEnum.PopUp,
                RecycleType = UIRecycleTypeEnum.Normal,
            },

             new PanelDefine{
                Key = PanelEnum.HeroMainPanel,
                Name = "HeroMainPanel",
                Path = "Hero/Prefabs/HeroMainPanel",
                Type = UITypeEnum.Full,
                RecycleType = UIRecycleTypeEnum.Normal,
            },
        };

        public static int GetLayer(UITypeEnum layer)
        {
            switch (layer)
            {
                case UITypeEnum.Full:
                    return 0;
                case UITypeEnum.PopUp:
                    return 0;
                case UITypeEnum.TopPopUp:
                    return 1;
                case UITypeEnum.TopSystem:
                    return 2;
                default:
                    return 0;
            }
        }
        
    }
}