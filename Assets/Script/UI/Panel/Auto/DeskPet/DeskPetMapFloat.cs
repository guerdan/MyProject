
using System.Collections.Generic;
using Script.Model.Auto;
using Script.UI.Components;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;
using Options = Script.UI.Panel.Auto.ImageCompareTestPanel.Options;

namespace Script.UI.Panel.Auto.DeskPet
{
    public class DeskPetMapFloat : MonoBehaviour
    {

        [SerializeField] private Button CloseBtn;
        [SerializeField] private Button MemoryBtn;
        [SerializeField] private ImageDetailComp ImageComp;

        List<string> options = new List<string>()
            {
                "_map",             // 像素粒度
                "_grid",            // 5X5大格子粒度
                "最近迷雾",
                "最近迷雾 (跟踪)",
                "_light_map",
                "_small_map",
                "_judge_map",
                "_fog_map",
                "保存全部地图",       // 保存至本地
            };
        DeskPetMain parent;
        KeywordTipsComp tipsComp;

        int _optionSelectStatus = -1;
        Options _option;
        string _scriptId;
        MapData _mapData;

        void Awake()
        {
            CloseBtn.onClick.AddListener(OnClickCloseBtn);
            MemoryBtn.onClick.AddListener(OnClickMemoryBtn);
        }

        public void SetData(DeskPetMain parent)
        {
            this.parent = parent;
            tipsComp = parent.TipsCompShared;

            // 换脚本，就重置
            //
            if (_scriptId != parent.ScriptId)
                _optionSelectStatus = -1;

            if (_optionSelectStatus == -1)
                OnSelectTipsComp(0);
        }

        void OnClickCloseBtn()
        {
            Utils.SetActive(this, false);

        }
        void OnClickMemoryBtn()
        {
            Utils.SetActive(tipsComp, true);
            tipsComp.SetData(options, OnSelectTipsComp, 140, 7);
            tipsComp.SetCurIndex(_optionSelectStatus);

            var tipsCompRectT = tipsComp.GetComponent<RectTransform>();
            var pos = Utils.GetPos(tipsCompRectT, MemoryBtn.GetComponent<RectTransform>()
                , new Vector2(19, 8), true);
            tipsCompRectT.anchoredPosition = pos;
        }


        void OnSelectTipsComp(int option_int)
        {
            var scriptId = parent.ScriptId;
            if (scriptId == null) { ClearComp(); return; }

            var script = AutoScriptManager.Inst.GetScriptData(scriptId);
            var node = script.GetNodeByType(NodeType.MapCapture);
            if (node == null) { ClearComp(); return; }

            var mapNode = node as MapCaptureNode;
            _mapData = MapDataManager.Inst.Get(mapNode.MapId);
            if (_mapData == null) { ClearComp(); return; }

            int true_option_int = -1;
            // 映射
            switch (option_int)
            {
                case 2:
                case 3:
                case 4:
                case 5:
                    true_option_int = option_int + 2;
                    break;
                default:
                    true_option_int = option_int;
                    break;
            }
            _option = (Options)true_option_int;

            Sprite mapSprite = null;
            Vector2Int line_offset = Vector2Int.zero;
            bool reset = _scriptId != parent.ScriptId || _optionSelectStatus != option_int;

            _scriptId = scriptId;
            _optionSelectStatus = option_int;



            // DU.RunWithTimer(() =>
            // {
            ImageCompareTestPanel.GetSpriteOfMapOption(scriptId, mapNode.MapId, _option, ""
                            , out mapSprite, out line_offset, out var anchor, ImageComp);
            // }, $"GetSpriteOfMapOption");


            // 如果大小发生变化，想要视野位置不变，就要传底下的偏移
            // Vector2Int anchor = Vector2Int.zero;
            // Vector2Int offset = _old_anchor - anchor;
            // _old_anchor = anchor;
            // ImageComp.SetData(mapSprite, options[option_int], false, offset);


            if (mapSprite == null) { ClearComp(); return; }

            _mapData.GetContentAttr(out Vector2Int xRange, out Vector2Int yRange
            , out var w, out var h);
            string title = "";

            if (_option == Options.FindNearestFogFollowing)
            {
                var result = _mapData.ResultOfFind;
                string title1 = $"全图 {w} * {h}";
                string debug_str = _mapData.GetPathFindingDebugStr(result);
                title = result == PathFindingResult.Success ? title1 : debug_str;
            }
            else
            {
                title = options[option_int];
            }


            if (reset)
                ImageComp.SetData(mapSprite, title);
            else
                ImageComp.SetData(mapSprite, title, false);

            ImageComp.SetLineOffset(line_offset);

        }
        float _countDown = 0.5f;   // 1s间隔实例 
        Vector2Int _playerPos;
        void Update()
        {
            float delta = Time.deltaTime;

            //2s的更新间隔
            _countDown -= delta;
            if (_countDown <= 0)
            {
                _countDown = 0.5f;
                if (_mapData != null && _optionSelectStatus > -1
                    && _option == Options.FindNearestFogFollowing)
                {

                    var playerPos = _mapData.GetPlayerPos();
                    if (playerPos != _playerPos)
                    {
                        _playerPos = playerPos;
                        DU.RunWithTimer(() =>
                        {
                            OnSelectTipsComp(_optionSelectStatus);
                        }, "图像自动更新");
                    }
                }
            }


            // if (Input.GetKeyDown(KeyCode.Space) && _mapData != null && _index <= _max_index)
            // {
            //     var file_path = _debug_dir + $"/{_index++}.png";
            //     _mapData.Capture(new Bitmap(file_path));
            //     _mapData.PrintResult();
            // }
        }

        int _index = 31;
        int _max_index = 111;
        string _debug_dir = @"D:\unityProject\MyProject\TestResource\图\0.2间隔";

        void ClearComp()
        {
            ImageComp.ClearData();
            ImageCompareTestPanel.ClearMapOptionCache(this);
        }


        void OnDestroy()
        {
            ImageCompareTestPanel.ClearMapOptionCache(this);
        }
    }
}