
using System;
using System.Collections.Generic;
using Script.Framework.UI;
using Script.Model.Auto;
using Script.UI.Components;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    /// <summary>
    /// todo
    /// 右键菜单（改名,取消收藏，删除）
    /// </summary>
    public class ScriptManagerPanel : BasePanel
    {
        public AutoScriptManager manager => AutoScriptManager.Inst;
        public static Action<string> OnRefresh;

        [SerializeField] private VirtualListComp CollectionList;
        [SerializeField] private VirtualListComp SearchList;
        [SerializeField] private GameObject ScriptItemPrefab;
        [SerializeField] private GameObject FolderItemPrefab;

        [SerializeField] private Text DirPath;
        [SerializeField] private Button ReturnDirPathBtn;
        [SerializeField] private InputTextComp SearchInput;
        [SerializeField] private Button SearchBtn;
        [SerializeField] private Button ClearBtn;

        [SerializeField] private Button CreateBtn;
        [SerializeField] private Button SettingsBtn;


        List<string> _collectionIds;
        List<string> _searchIds;

        string _directoryPath = "Script";

        void Awake()
        {
            // SearchBtn.onClick.AddListener(OnSearchBtnClick);  //没啥用
            ClearBtn.onClick.AddListener(OnClearBtnClick);
            ReturnDirPathBtn.onClick.AddListener(OnReturnDirPathBtnClick);
            CreateBtn.onClick.AddListener(OnCreateBtnClick);
            SettingsBtn.onClick.AddListener(OnClickSettingsBtn);


            ScriptItemPrefab.SetActive(false);
            FolderItemPrefab.SetActive(false);
            CollectionList.OnGetItemTemplate = (int index) => ScriptItemPrefab;
            CollectionList.OnGetItemSize = (int index) => ScriptItemPrefab.GetComponent<RectTransform>().rect.size;
            CollectionList.OnUpdateItem = OnUpdateItemOfCollection;

            // 搜索结果，有按文件名搜索，有按目录浏览的两个功能。目录浏览既显示文件也显示文件夹。
            SearchList.OnGetItemTemplate = (int index) =>
            {
                if (_searchIds[index].StartsWith(AutoScriptManager.FolderIdStart))
                    return FolderItemPrefab;
                else
                    return ScriptItemPrefab;
            };
            // 一样大小
            SearchList.OnGetItemSize = (int index) => ScriptItemPrefab.GetComponent<RectTransform>().rect.size;
            SearchList.OnUpdateItem = OnUpdateItemOfSearch;
        }
        void OnUpdateItemOfCollection(GameObject item, int index)
        {
            var id = _collectionIds[index];
            var ui = item.GetComponent<ScriptManagerScriptItem>();
            ui.SetData(id, this);
        }

        void OnUpdateItemOfSearch(GameObject item, int index)
        {
            var id = _searchIds[index];
            if (id.StartsWith(AutoScriptManager.FolderIdStart))
            {
                id = id.Substring(AutoScriptManager.FolderIdStart.Length + Application.streamingAssetsPath.Length + 1);
                var text = id.Substring(id.LastIndexOf('/') + 1);
                var ui = item.GetComponent<ScriptManagerFolderItem>();
                // 只用下点击事件功能
                ui.SetData(text, () =>
                {
                    _directoryPath = id;
                    RefreshSearchList("");
                });
            }
            else
            {
                var ui = item.GetComponent<ScriptManagerScriptItem>();
                ui.SetData(id, this);
            }

        }

        public override void SetData(object data)
        {

            RefreshCollectionList();
            RefreshSearchList("");
            // 搜索栏 与 目录搜索方式的切换

            SearchInput.SetData("", null, (string keyword) =>
            {
                RefreshSearchList(keyword);
            });
        }

        public void RefreshCollectionList()
        {
            _collectionIds = manager.Settings.Collections;
            CollectionList.ReloadData(_collectionIds.Count);
        }
        void RefreshSearchList(string keyword)
        {
            if (keyword == "")
            {
                _searchIds = manager.BrowseDirectory(_directoryPath);
                SetDirPath();
            }
            else
            {
                _searchIds = manager.SearchScripts(keyword);
                DirPath.text = "";
            }
            SearchList.ReloadData(_searchIds.Count);
        }

        void SetDirPath()
        {
            DirPath.text = _directoryPath == "" ? "" : $"路径：{_directoryPath}";
            var rectT = ReturnDirPathBtn.GetComponent<RectTransform>();
            rectT.sizeDelta = new Vector2(DirPath.preferredWidth / 2, rectT.sizeDelta.y);
        }


        void OnReturnDirPathBtnClick()
        {
            if (SearchInput.GetText() != "")
                return;

            var index = _directoryPath.LastIndexOf('/');
            if (index >= 0)
            {
                _directoryPath = _directoryPath.Substring(0, index);
                RefreshSearchList("");
            }
        }


        void OnClearBtnClick()
        {
            SearchInput.SetText("");
        }

        void OnCreateBtnClick()
        {
            OnClearBtnClick();

            Action<string, string> OnConfirm = (string name, string _) =>
            {
                bool success = manager.CreateScript(name, _directoryPath, out string id);
                if (!success) return;
                Utils.OpenDrawProcessPanel(id);
            };

            ConfirmPanelParam param = new ConfirmPanelParam
            {
                Type = ConfirmPanelType.EditInput,
                PanelTitle = SU.GetString(SU.ChuangJianJiaoBen),
                Region0Title = SU.GetString(SU.JiaoBenMing),
                Region0Text = "",
                OnConfirm = OnConfirm,
            };
            PanelRunConfig config = new PanelRunConfig
            {
                SetPosType = PanelSetPosType.Reference,
                PosTarget = CreateBtn.GetComponent<RectTransform>(),
                PosOffset = new Vector2(0, -30),
            };

            UIManager.Inst.ShowPanel(PanelEnum.ConfirmPanel, param, config);
        }

        void OnClickSettingsBtn()
        {

            UIManager.Inst.ShowPanel(PanelEnum.ScriptManagerSettingsPanel, null);
            // Action<string, string> OnConfirm = (string name, string _) =>
            // {
            //     manager.Settings.PipeName = name;
            // };

            // ConfirmPanelParam param = new ConfirmPanelParam
            // {
            //     Type = ConfirmPanelType.EditInput,
            //     PanelTitle = "PipeName",
            //     Region0Title = "PipeName\n(重启生效)",
            //     Region0Text = manager.Settings.PipeName,
            //     OnConfirm = OnConfirm,
            // };
            // PanelRunConfig config = new PanelRunConfig
            // {
            //     SetPosType = PanelSetPosType.Reference,
            //     PosTarget = SettingsBtn.GetComponent<RectTransform>(),
            //     PosOffset = new Vector2(0, -30),
            // };

            // UIManager.Inst.ShowPanel(PanelEnum.ConfirmPanel, param, config);
        }

        void OnDisable()
        {
            manager.SaveAutoScriptSettings();
        }
    }

}