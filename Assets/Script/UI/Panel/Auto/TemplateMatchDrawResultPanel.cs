
using System.Collections.Generic;
using Script.Framework.UI;
using Script.UI.Component;
using Script.Util;
using UnityEngine;

namespace Script.UI.Panel.Auto
{
    public class TemplateMatchDrawResultPanel : BasePanel
    {
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
            float duration = (float)dataList[1];
            Utils.RefreshItemListByCount<SquareFrameUI>(_itemList, itemdata.Count, Prefab, Content, (item, index) =>
               {
                   var matchResult = itemdata[index];
                   if (matchResult.Score <= 1)
                       item.SetData(matchResult.Score, matchResult.Rect, Color.red, 2, true);
                   else if (matchResult.Score == 100)  // 是截屏范围
                       item.SetData(0, matchResult.Rect, Color.green, 2, false);
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
                    foreach (var item in _itemList)
                        item.gameObject.SetActive(false);
                }
            }
        }

    }
}