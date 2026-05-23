
using System;
using Script.Framework;
using Script.Model.Auto;
using Script.Util;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Script.UI.Components
{
    public class InputTextSVItem : MonoBehaviour
    {

        [SerializeField] private Image SelectedBg;
        [SerializeField] private Text Title;
        [SerializeField] private InputTextProComp InputText;


        InputTextSVItemData _data;
        int _index;
        InputTextSV _parent;


        public void SetData(InputTextSVItemData data, int index, float width, InputTextSV parent)
        {
            _data = data;
            _index = index;
            _parent = parent;

            var rect = GetComponent<RectTransform>();
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

            Title.text = _data.Title;
            float start_pos = Utils.GetTextPreferWidth(Title.fontSize, Title.text);


            Action<string> save_func = (str) =>
            {
                str = str.Replace(" ", "");
                data.Content = str;
                // string format = AutoDataUIConfig.FormulaFormat(data.RegionExpression);  //格式化
                InputText.SetText(data.Content);
            };

            // string format = AutoDataUIConfig.FormulaFormat(data.RegionExpression);  //格式化
            InputText.SetData(data.Content, save_func);

            var input_rect = InputText.GetComponent<RectTransform>();
            input_rect.offsetMin = new Vector2(start_pos / 2 + 20, -14);

            Refresh();
        }

        public void Refresh()
        {

        }
    }
}