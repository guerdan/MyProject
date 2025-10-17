
using System.Collections.Generic;
using Script.Framework.UI;
using Script.UI.Component;
using Script.Util;
using UnityEngine;

namespace Script.UI.Panel.Auto
{
    public class TemplateMatchDrawResultPanel : BasePanel
    {
        static Color Blue;
        static TemplateMatchDrawResultPanel()
        {
            Blue = Utils.ParseHtmlString("#003EFF");
        }

        [SerializeField] private RectTransform Content;
        [SerializeField] private GameObject Prefab;

        List<SquareFrameUI> _itemList = new List<SquareFrameUI>();
        float _countDown = 0;
        void Awake()
        {
            _animEnable = false;
            Prefab.SetActive(false);
        }

        public override void SetData(object data)
        {
            var dataList = data as List<object>;
            var itemdata = dataList[0] as List<CVMatchResult>;
            if (itemdata == null)
            {
                Clear();
                return;
            }
            float duration = (float)dataList[1];
            Utils.RefreshItemListByCount<SquareFrameUI>(_itemList, itemdata.Count, Prefab, Content, (item, index) =>
               {
                   var matchResult = itemdata[index];
                   if (matchResult.UIType == 0)
                       item.SetData(DU.FloatFormat(matchResult.Score, 2), matchResult.Rect, Color.green);
                   else if (matchResult.UIType == 1)  // 是截屏范围
                   {
                       var r = matchResult.Rect;
                       item.SetData($"P({(int)r.x},{(int)r.y}), Size({(int)r.w},{(int)r.h})", r, Color.red, 32);
                   }
                   else if (matchResult.UIType == 2)  // 失败后的，最高分数
                       item.SetData(DU.FloatFormat(matchResult.Score, 2), matchResult.Rect, new Color());
               });

            _countDown = duration;
        }

        void Update()
        {
            if (_countDown > 0)
            {
                _countDown -= Time.deltaTime;
                if (_countDown <= 0)
                {
                    Clear();
                }
            }
        }


        void Clear()
        {
            foreach (var item in _itemList)
                item.gameObject.SetActive(false);
        }
    }
}