
using System.Collections.Generic;
using Script.Framework.UI;
using Script.UI.Component;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    public class LogPrintPanel : BasePanel
    {
        [SerializeField] private VirtualListComp listComp;
        [SerializeField] private GameObject template;
        [SerializeField] private Text tempText;
        List<string> data;
        List<float> textsHieght;
        void Awake()
        {
            template.SetActive(false);
            listComp.OnGetItemSize = GetItemSize;
            listComp.OnGetItemTemplate = GetItemTemplate;
            listComp.OnUpdateItem = UpdateItem;
        }

        private GameObject GetItemTemplate(int index)
        {
            return template;
        }

        private Vector2 GetItemSize(int index)
        {
            var width = template.GetComponent<RectTransform>().rect.size.x;
            return new Vector2(width, textsHieght[index]);
        }

        private void UpdateItem(GameObject item, int index)
        {
            var ui = item.GetComponent<LogPrintItem>();
            ui.SetData(data[index], textsHieght[index]);
        }

        public override void SetData(object data)
        {
            if (data is List<string> list)
            {
                this.data = list;
                ShowList();
            }
            else
            {
                DU.MessageBox($"{PanelDefine.Name} SetData类型错误");
            }
        }

        void ShowList()
        {
            // 计算文本高度
            textsHieght = new List<float>();
            for (int i = 0; i < data.Count; i++)
            {
                tempText.text = data[i];
                textsHieght.Add(tempText.preferredHeight + 16);
            }

            // 设置数据源
            listComp.ReloadData(data.Count);
            listComp.ScrollToItemVertical(data.Count - 1);
        }
    }


}