using System.Collections.Generic;
using UnityEngine;

namespace Script.Framework.UI
{

    public enum UITypeEnum
    {
        WindowsPopUp,   // Windows式的弹窗界面，层级0；本层级的界面，获得焦点时会置于本层级的顶层
        Full,           // 手游式的全屏界面，层级1
        PopUp,          // 手游式的弹窗界面，层级1
        TopPopUp,       // 顶层弹窗，层级2，如：战力提升弹窗
        TopSystem,      // 顶层系统通知界面，层级3，如：断线重连弹窗、转菊花、轮播通知栏
    }

    //当关闭Layer=0的界面栈的最后个界面时，调用AssetManager.ReleaseUnuseAsset真正回收资源
    public enum UIRecycleTypeEnum
    {
        Once,          //关闭界面后，立即销毁实例。
        Normal,        //关闭界面60秒后，销毁实例。
        Frequent,      //一直缓存实例。
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
        LogPrintPanel,
        PicMatchFloat,
        DrawProcessPanel,
        ProcessNodeInfoPanel,
        EditNamePanel,
        ImageInfoPanel,
        ImageSourcePanel,
        ScriptManagerPanel,
        TemplateMatchDrawResultPanel,
        ImageMatchTestPanel,
        ImageCompareTestPanel,
        ProcessDebugPanel,
        DeskPetMain,
    }


    /// <summary>
    /// 最开始是面向手游UI习惯。
    /// 
    /// 拖拽功能:通过向界面节点加DragPanel组件
    /// </summary>
    public class PanelDefine
    {
        public PanelEnum Key;       //访问key
        public string Name;         //名字
        public string Path;         //预制体路径
        public UITypeEnum Type;     //界面类型
        public UIRecycleTypeEnum RecycleType;               //回收类型
        public Vector2 InitPos = new Vector2(0, 0);         //窗口的初始坐标，界面节点位置 => 依赖InitPos  
        public bool IsSingle = true;                        //单个实例，目前默认都是单例。Windows窗口都单例

        public bool ClickOutWinClose = false;               //点击窗外就关闭的功能。默认为false

        // 是否因遮挡关系而隐藏上个界面。默认为true
        // 打开一个Full界面，栈中的界面全隐藏
        // 打开一个PopUp界面，栈中的界面除了最上方的Full界面其他都隐藏。
        public bool HideLastPanel = true;

        // public bool TopSingle = true;   // 最上层只能弹出一份弹窗，防连点。默认为true。如果加载时屏蔽，实例出来后也屏蔽，那就无懈可击

        public int Layer { get { return PanelUtil.GetLayer(Type); } }
    }

    /// <summary>
    /// 面向UIManager
    /// </summary>
    public static class PanelUtil
    {

        public static int RecycleTimeNormal = 10;
        public static int RecycleTimeFrequent = 60;

        public static int GetLayer(UITypeEnum layer)
        {
            switch (layer)
            {
                case UITypeEnum.WindowsPopUp:
                    return 0;
                case UITypeEnum.Full:
                    return 1;
                case UITypeEnum.PopUp:
                    return 1;
                case UITypeEnum.TopPopUp:
                    return 2;
                case UITypeEnum.TopSystem:
                    return 3;
                default:
                    return 0;
            }
        }

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
                ClickOutWinClose = true,
            },

            new PanelDefine{
                Key = PanelEnum.HeroMainPanel,
                Name = "HeroMainPanel",
                Path = "Hero/Prefabs/HeroMainPanel",
                Type = UITypeEnum.Full,
                RecycleType = UIRecycleTypeEnum.Normal,
            },

            new PanelDefine{
                Key = PanelEnum.LogPrintPanel,
                Name = "LogPrintPanel",
                Path = "Auto/Prefabs/LogPrintPanel",
                Type = UITypeEnum.WindowsPopUp,
                RecycleType = UIRecycleTypeEnum.Normal,
                InitPos = new Vector2(200, 0),
                HideLastPanel = false,
            },

            new PanelDefine{
                Key = PanelEnum.PicMatchFloat,
                Name = "PicMatchFloat",
                Path = "Auto/Prefabs/PicMatchFloat",
                Type = UITypeEnum.WindowsPopUp,
                RecycleType = UIRecycleTypeEnum.Normal,
                InitPos = new Vector2(0, 0),
                HideLastPanel = false,
            },

            new PanelDefine{
                Key = PanelEnum.DrawProcessPanel,
                Name = "DrawProcessPanel",
                Path = "Auto/Prefabs/DrawProcessPanel",
                Type = UITypeEnum.WindowsPopUp,
                RecycleType = UIRecycleTypeEnum.Frequent,
                InitPos = new Vector2(0, 0),
                HideLastPanel = false,
            },

            new PanelDefine{
                Key = PanelEnum.ProcessNodeInfoPanel,
                Name = "ProcessNodeInfoPanel",
                Path = "Auto/Prefabs/ProcessNodeInfoPanel",
                Type = UITypeEnum.PopUp,
                RecycleType = UIRecycleTypeEnum.Frequent,
                InitPos = new Vector2(0, 0),
                HideLastPanel = false,
            },

            new PanelDefine{
                Key = PanelEnum.EditNamePanel,
                Name = "EditNamePanel",
                Path = "Common/Prefabs/Panel/EditNamePanel",
                Type = UITypeEnum.PopUp,
                RecycleType = UIRecycleTypeEnum.Normal,
                InitPos = new Vector2(0, 0),
                HideLastPanel = false,
                ClickOutWinClose = true,
            },

            new PanelDefine{
                Key = PanelEnum.ImageInfoPanel,
                Name = "ImageInfoPanel",
                Path = "Common/Prefabs/Panel/ImageInfoPanel",
                Type = UITypeEnum.PopUp,
                RecycleType = UIRecycleTypeEnum.Normal,
                InitPos = new Vector2(0, 0),
                HideLastPanel = false,
                ClickOutWinClose = true,
            },

            new PanelDefine{
                Key = PanelEnum.ImageSourcePanel,
                Name = "ImageSourcePanel",
                Path = "Common/Prefabs/Panel/ImageSourcePanel",
                Type = UITypeEnum.PopUp,
                RecycleType = UIRecycleTypeEnum.Normal,
                InitPos = new Vector2(0, 0),
                HideLastPanel = false,
            },

            new PanelDefine{
                Key = PanelEnum.ScriptManagerPanel,
                Name = "ScriptManagerPanel",
                Path = "Auto/Prefabs/ScriptManagerPanel",
                Type = UITypeEnum.WindowsPopUp,
                RecycleType = UIRecycleTypeEnum.Frequent,
                InitPos = new Vector2(0, 0),
                HideLastPanel = false,
            },

            new PanelDefine{
                Key = PanelEnum.TemplateMatchDrawResultPanel,
                Name = "TemplateMatchDrawResultPanel",
                Path = "Auto/Prefabs/TemplateMatchDrawResultPanel",
                Type = UITypeEnum.TopPopUp,
                RecycleType = UIRecycleTypeEnum.Frequent,
                InitPos = new Vector2(0, 0),
                HideLastPanel = false,
            },

            new PanelDefine{
                Key = PanelEnum.ImageMatchTestPanel,
                Name = "ImageMatchTestPanel",
                Path = "Common/Prefabs/Panel/ImageMatchTestPanel",
                Type = UITypeEnum.WindowsPopUp,
                RecycleType = UIRecycleTypeEnum.Normal,
                InitPos = new Vector2(0, 0),
                HideLastPanel = false,
                ClickOutWinClose = true,
            },
            new PanelDefine{
                Key = PanelEnum.ImageCompareTestPanel,
                Name = "ImageCompareTestPanel",
                Path = "Common/Prefabs/Panel/ImageCompareTestPanel",
                Type = UITypeEnum.PopUp,
                RecycleType = UIRecycleTypeEnum.Normal,
                InitPos = new Vector2(0, 0),
                HideLastPanel = false,
                ClickOutWinClose = true,
            },

            new PanelDefine{
                Key = PanelEnum.ProcessDebugPanel,
                Name = "ProcessDebugPanel",
                Path = "Auto/Prefabs/ProcessDebugPanel",
                Type = UITypeEnum.PopUp,
                RecycleType = UIRecycleTypeEnum.Normal,
                InitPos = new Vector2(0, 0),
                HideLastPanel = false,
            },

            new PanelDefine{
                Key = PanelEnum.DeskPetMain,
                Name = "DeskPetMain",
                Path = "Auto/Prefabs/DeskPetMain",
                Type = UITypeEnum.TopSystem,
                RecycleType = UIRecycleTypeEnum.Frequent,
                InitPos = new Vector2(0, 0),
                HideLastPanel = false,
            },
            
        };



    }
}