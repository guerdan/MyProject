
using System;
using System.Collections.Generic;
using Script.Util;
using UnityEngine;

namespace Script.UI.Components
{
    public class MenuStrPack
    {
        public List<MenuStrPack> Pack = new List<MenuStrPack>();     // 本项的展开项
        public string Name;                 // 显示名字
        public Action DoAction;             // Action
        public int Level;                   // 所属层级。从一级菜单开始
        public int Index;                   // 在层级中的序数
        public bool IsMenu => Pack.Count > 0;       //是否为菜单选项,能够继续展开的那种

    }
    // "name1_name2_name3" 用名字拼, 打字麻烦，转枚举
    // "1_2_3" 用序数拼, 后期增删项不好维护, 好实现
    // 最终结构   点击调用可以查id字典。显示用
    public class MenuSystem : MonoBehaviour
    {
        public bool Init => _data != null;
        Vector2 _size;
        float _width;

        [SerializeField] private MenuComp[] Menus;         //菜单

        [HideInInspector] public int[] HoverRecord;      //展开选项记录
        List<MenuStrPack> _data;

        void Awake()
        {
            _size = ((RectTransform)transform).rect.size;
            foreach (var m in Menus)
                Utils.SetActive(m, false);
        }

        public void SetData(List<(string, string, Action)> strList, float width = 100, int maxShowCount = 10)
        {
            _width = width;
            _data = new List<MenuStrPack>();
            int max_level_record = 0;

            foreach (var source in strList)
            {
                var level_len = Apply(source);
                max_level_record = Mathf.Max(level_len, max_level_record);
            }

            HoverRecord = new int[max_level_record];
        }


        public void ChangeData(List<(string, string, Action)> strList)
        {
            int max_level_record = 0;

            foreach (var source in strList)
            {
                var level_len = Apply(source);
                max_level_record = Mathf.Max(level_len, max_level_record);
            }

            if (max_level_record > HoverRecord.Length)
            {
                var temp = new int[max_level_record];
                for (int i = 0; i < HoverRecord.Length; i++)
                {
                    temp[i] = HoverRecord[i];
                }
            }
        }

        int Apply((string, string, Action) source)
        {
            var index_info = source.Item1;
            var name = source.Item2;
            var action = source.Item3;

            var list = _data;
            string[] levelL = index_info.Split("_"); // level_list: 1,2,3
            int level_len = levelL.Length;

            for (int i = 0; i < level_len; i++)
            {
                var index = int.Parse(levelL[i]);
                Supply(list, index + 1);
                var item = list[index];
                if (item == null)
                {
                    item = new MenuStrPack();
                    list[index] = item;
                }

                if (i == level_len - 1)
                {
                    item.Name = name;
                    item.Level = level_len;
                    item.Index = index;
                    item.DoAction = action;
                }

                list = item.Pack;
            }

            return level_len;
        }

        public void Open()
        {
            for (int i = 0; i < HoverRecord.Length; i++)
            {
                HoverRecord[i] = -1;
            }
            Utils.SetActive(Menus[0], true);
            Menus[0].SetData(_data, this, _width);
            ChangeHoverRecord(1, -1);
        }

        void Supply(List<MenuStrPack> list, int count)
        {
            for (int i = list.Count; i < count; i++)
            {
                list.Add(null);
            }
        }

        void Update()
        {

            if (Input.GetMouseButtonDown(0))
            {
                bool close = true;
                for (int i = 0; i < HoverRecord.Length; i++)
                {
                    if (HoverRecord[i] >= 0 && Utils.IsPointerOverUIObject(Menus[i].gameObject, Root.Inst.Canvas))
                    {
                        close = false;
                        break;
                    }
                }

                if (close)
                    Utils.SetActive(this, false);
            }

        }
        public void Click(MenuStrPack item)
        {
            item.DoAction?.Invoke();
            Utils.SetActive(this, false);
        }
        public void Hover(MenuStrPack item)
        {
            MenuComp current = Menus[item.Level - 1];
            MenuComp next = Menus[item.Level];

            Utils.SetActive(next, true);
            next.SetData(item.Pack, this, _width);

            var screenP = current.GetItemScreenPos(item.Index);
            var menusRectT = next.GetComponent<RectTransform>();
            var relativePos = Utils.GetPos(menusRectT, screenP, default);
            var pos = relativePos + new Vector2(current._itemWidth / 2 + 1, 21.5f) + new Vector2(-_size.x / 2, _size.y / 2);
            menusRectT.anchoredPosition = pos;
        }

        public void ChangeHoverRecord(int level, int index)
        {
            // 清空后面层级的记录，以及关闭后面层级的菜单

            for (int i = level; i < HoverRecord.Length; i++)
            {
                HoverRecord[i] = -1;
                Utils.SetActive(Menus[i], false);
            }

            HoverRecord[level - 1] = index;
            Menus[level - 1].Refresh();
        }


    }
}