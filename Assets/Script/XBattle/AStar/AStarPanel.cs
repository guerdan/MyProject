using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Script.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.XBattle.AStar
{
    public enum AStarPanelOper
    {
        SetStart = 0,
        SetDestin = 1,
        SetBlock = 2,
    }

    public class AStarPanel : MonoBehaviour, IPointerClickHandler
    {
        public Button setStartBtn;
        public Button setDestinBtn;
        public Button setBlockBtn;
        public Button clearAllBtn;
        public Button stepBtn;

        public GameObject UIPrefab;
        public GameObject UIParent;

        private List<GameObject> nodes = new List<GameObject>();
        private AStarPanelOper operate;
        int row = 7;
        int column = 10;
        int height = 500;
        int width = 700;

        // Start is called before the first frame update
        void Start()
        {
            setStartBtn.onClick.AddListener(onSetStartBtnClick);
            setDestinBtn.onClick.AddListener(onSetDestinBtnClick);
            setBlockBtn.onClick.AddListener(onSetBlockBtnClick);
            clearAllBtn.onClick.AddListener(onClearAllBtnClick);
            stepBtn.onClick.AddListener(onStepBtnClick);

            this.init();
        }


        /// <summary>
        /// 从上到下，从左到右排列格子
        /// </summary>
        void init()
        {
            Vector2Int start = new Vector2Int(0, 0);
            Vector2Int end = new Vector2Int(10, 10);


            #region logic

            AStarLogicManager.inst.init(row, column);

            #endregion

            #region ui

            foreach (var go in nodes)
            {
                Destroy(go);
            }

            nodes.Clear();

            float cell_width = width / column;
            float cell_height = height / row;
            for (int i = 0; i < row; i++)
            for (int j = 0; j < column; j++)
            {
                GameObject node = Instantiate(UIPrefab);
                node.transform.SetParent(UIParent.transform, false);
                nodes.Add(node);

                RectTransform rect = node.GetComponent<RectTransform>();
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cell_width - 2);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cell_height - 2);
                rect.anchoredPosition =
                    new Vector2(cell_width * j + cell_width / 2, -cell_height * i - cell_height / 2);
            }

            refreshUi();

            #endregion
        }

        void onSetStartBtnClick()
        {
            operate = AStarPanelOper.SetStart;
        }

        void onSetDestinBtnClick()
        {
            operate = AStarPanelOper.SetDestin;
        }

        void onSetBlockBtnClick()
        {
            operate = AStarPanelOper.SetBlock;
        }

        void onClearAllBtnClick()
        {
            init();
        }

        void onStepBtnClick()
        {
            AStarLogicManager.inst.FindPathStep();
            refreshUi();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var pos = eventData.position;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                UIParent.GetComponent<RectTransform>(),
                pos,
                Root.Inst.Camera,
                out Vector2 uiPos);

            Debug.LogWarning(uiPos);
            float cell_width = width / column;
            float cell_height = height / row;
            int x_index = (int)Math.Floor(uiPos.x / cell_width);
            int y_index = (int)Math.Floor(-uiPos.y / cell_height);


            if (operate == AStarPanelOper.SetStart)
                AStarLogicManager.inst.SetStart(new Vector2Int(x_index, y_index));
            if (operate == AStarPanelOper.SetBlock)
                AStarLogicManager.inst.SetBlock(new Vector2Int(x_index, y_index));
            if (operate == AStarPanelOper.SetDestin)
                AStarLogicManager.inst.SetTarget(new Vector2Int(x_index, y_index));
            refreshUi();
        }

        public void refreshUi()
        {
            for (int i = 0; i < row; i++)
            for (int j = 0; j < column; j++)
            {
                int index = i * column + j;
                nodes[index].GetComponent<AStarCell>().SetData(AStarLogicManager.inst.grid[i, j]);
            }
        }


    }
}