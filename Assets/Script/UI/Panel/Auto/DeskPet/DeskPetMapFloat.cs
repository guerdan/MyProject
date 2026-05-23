
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Script.Framework.Else;
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
        public GlobalKeyboardManager KeyManager => GlobalKeyboardManager.Inst;

        [SerializeField] private Button CloseBtn;
        [SerializeField] private Button MemoryBtn;
        [SerializeField] private ImageDetailComp ImageComp;

        List<(Options, string)> MapOptions = new List<(Options, string)>()
            {
                (Options.Map,"map"),
                (Options.Grid,"grid"),
                (Options.Auto,"Auto"),
                (Options.FindNearestFog,"NearFog"),
                (Options.FindNearestFogFollowing,"NearFog (Follow)"),
                (Options.FollowTarget,"FollowTarget"),
                (Options.SmallMap,"small_map"),
                (Options.LightMap,"light_map"),
                (Options.JudgeMap,"judge_map"),
                (Options.FogMap,"fog_map"),
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

        void OnEnable()
        {
        }
        void OnDisable()
        {
        }

        public void SetData(DeskPetMain parent)
        {
            this.parent = parent;
            tipsComp = parent.TipsCompShared;

            // 换脚本，就重置
            //
            if (_scriptId != parent.ScriptId)
                _optionSelectStatus = -1;

            _scriptId = parent.ScriptId;

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

            var strList = MapOptions.Select(t => t.Item2).ToList();
            tipsComp.SetData(strList, OnSelectTipsComp, 140, 7);
            tipsComp.SetCurIndex(_optionSelectStatus);

            var tipsCompRectT = tipsComp.GetComponent<RectTransform>();
            var pos = Utils.GetPos(tipsCompRectT, MemoryBtn.GetComponent<RectTransform>()
                , new Vector2(19, 8), true);
            tipsCompRectT.anchoredPosition = pos;
        }

        MapData GetMapData(string scriptId, out string mapId)
        {
            mapId = null;
            if (scriptId == null) return null;
            var script = AutoScriptManager.Inst.GetScriptData(scriptId);
            var node = script.GetNodeByType(NodeType.MapCapture);
            if (node == null) return null;

            var mapNode = node as MapCaptureNode;
            mapId = mapNode.MapId;
            var mapData = MapDataManager.Inst.Get(mapId);

            return mapData;
        }


        void OnSelectTipsComp(int option_int)
        {
            _optionSelectStatus = option_int;
            _option = MapOptions[option_int].Item1;

            _mapData = GetMapData(parent.ScriptId, out string mapId);
            if (_mapData == null) { ClearComp(); return; }

            Sprite mapSprite = null;
            Vector2Int line_offset = Vector2Int.zero;
            bool reset = _optionSelectStatus != option_int;


            // DU.RunWithTimer(() =>
            // {
            ImageCompareTestPanel.GetSpriteOfMapOption(_scriptId, mapId, _option, ""
                            , out mapSprite, out line_offset, out var anchor, out string debug_str, ImageComp);
            // }, $"GetSpriteOfMapOption");


            // 如果大小发生变化，想要视野位置不变，就要传底下的偏移
            // Vector2Int anchor = Vector2Int.zero;
            // Vector2Int offset = _old_anchor - anchor;
            // _old_anchor = anchor;
            // ImageComp.SetData(mapSprite, options[option_int], false, offset);


            if (mapSprite == null) { ClearComp(); return; }

            _mapData.GetContentAttr(out Vector2Int xRange, out Vector2Int yRange
            , out var w, out var h);
            string title = $"{debug_str}";

            // if (_option == Options.FindNearestFogFollowing)
            // {
            //     var result = _mapData.FindFogStatus;
            //     string title1 = $"全图 {w} * {h}";
            //     string debug_str = _mapData.GetPathFindingDebugStr(result);
            //     title = result == PathFindingResult.Success ? title1 : debug_str;
            // }
            // else
            // {
            //     title = MapOptions[option_int].Item2;
            // }


            if (reset)
                ImageComp.SetData(mapSprite, title);
            else
                ImageComp.SetData(mapSprite, title, false);

            ImageComp.SetLineOffset(line_offset);

        }

        #region Update
        float _countDown = 0.5f;    // 1s间隔实例 
        int _map_frame;             // 地图帧序


        int _index;
        int _max_index;
        string _debug_dir;

        void Update()
        {
            float delta = Time.deltaTime;

            //2s的更新间隔
            _countDown -= delta;
            if (_countDown <= 0)
            {
                _countDown = 0.5f;

                _scriptId = parent.ScriptId;
                _mapData = GetMapData(_scriptId, out _);
                var option_condition = _option == Options.FindNearestFogFollowing
                    || _option == Options.FollowTarget || _option == Options.Auto;

                if (_mapData != null && _optionSelectStatus > -1 && option_condition)
                {
                    var map_frame = _mapData.FrameCount;
                    if (map_frame != _map_frame)
                    {
                        _map_frame = map_frame;
                        // DU.RunWithTimer(() =>
                        // {
                        OnSelectTipsComp(_optionSelectStatus);
                        // }, "Update");
                    }
                }
                if (_mapData == null)
                {
                    ClearComp();
                }
            }

            // Debug
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Space) && _mapData != null && _index <= _max_index)
            {
                var file_path = _debug_dir + $"/{_index++}.png";
                var colors = IU.BitmapToColor32(new Bitmap(file_path));
                _mapData.Capture(colors);
                _mapData.PrintResult();
            }

            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Return))
            {
                MapDataManager.Inst.Remove("Map-22");
                MapDataManager.Inst.Create("Map-22", new CVRect(0, 0, 200, 200));
                _mapData = MapDataManager.Inst.Get("Map-22");
                _index = 15; _max_index = 26; _debug_dir = @"D:\unityProject\MyProject_Resource\P3P4";
                DU.LogWarning("【重置地图数据】成功");
            }
        }

        #endregion




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