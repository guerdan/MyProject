
using Script.Model.Auto;
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
            if (_data is MapCaptureNode cN)
            {
                Title.text = "地图识别";
            }
            else if (_data is MapPathFindingNode pN)
            {
                Title.text = "地图寻路";
            }

        }
    }
}