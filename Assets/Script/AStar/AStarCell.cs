using UnityEngine;
using UnityEngine.UI;

namespace Script.AStar
{
    public class AStarCell : MonoBehaviour
    {
        public Text GText;
        public Text HText;
        public Text FText;
        public Image img;

        public void SetData(AStarLogicNode node)
        {
            if (AStarLogicManager.inst.openList.Contains(node) || AStarLogicManager.inst.closedList.Contains(node))
            {
                GText.text = $"G:{node.G}";
                HText.text = $"H:{node.H}";
                FText.text = $"F:{node.F}";
            }
            else
            {
                GText.text = "";
                HText.text = "";
                FText.text = "";
            }


            string colorString = "";
            if (node.Type == AStarLogicNodeType.Block)
                colorString = "#808080";
            else if (node.Type == AStarLogicNodeType.Start)
                colorString = "#00ff01";
            else if (node.Type == AStarLogicNodeType.Target)
                colorString = "#fe0000";
            else if (AStarLogicManager.inst.openList.Contains(node))
                colorString = "#017eff";
            else if (AStarLogicManager.inst.closedList.Contains(node))
                colorString = "#01ffff";
            else
                colorString = "#FFFFFF";

            ColorUtility.TryParseHtmlString(colorString, out Color color);
            img.color = color;
        }
    }
}