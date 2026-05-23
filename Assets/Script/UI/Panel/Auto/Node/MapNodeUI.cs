
using Script.Model.Auto;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto.Node
{
    public class MapNodeUI : ProcessNodeUI
    {
        [Header("扩展内容")]
        [SerializeField] private Text Title;        // 标题

        public override void RefreshContent()
        {
            base.RefreshContent();
            if (_data.NodeType == NodeType.MapCapture)
            {
                Title.text = SU.GetString(SU.DiTuShiBie);
            }
            else if (_data.NodeType == NodeType.MapPathFinding)
            {
                Title.text = SU.GetString(SU.DiTuXunLu);
            }
            else if (_data.NodeType == NodeType.ItemGridRecog)
            {
                Title.text = SU.GetString(SU.WuPingGeShiBie);
            }

        }
    }
}