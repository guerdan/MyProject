using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;


namespace Script.UI.Component
{


    public class VirtualListComp : MonoBehaviour
    {
        public class VirtualListViewItemChild
        {
            GameObject node = null;
            Vector3 originPos = Vector3.zero;
            int zIndex = 0;
        }

        /// <summary>
        /// item实例
        /// </summary>
        public class VirtualListCompItemNode
        {
            public GameObject inst = null;
            private RectTransform node = null;
            public GameObject template = null;
            private List<VirtualListViewItemChild> children = new List<VirtualListViewItemChild>();


            public void setItemTemplate(GameObject inst, GameObject template)
            {
                this.inst = inst;
                this.node = inst.GetComponent<RectTransform>();
                this.template = template;

                node.pivot = new Vector2(0.5f, 0.5f);
                //需要统一锚点。置于顶部。
                node.anchorMin = new Vector2(0, 1);
                node.anchorMax = new Vector2(0, 1);
            }

            public void setParent(Transform parent)
            {
                node.SetParent(parent, false);
            }

            public void SetPosition(Vector3 pos)
            {
                node.anchoredPosition = pos;
            }

            public List<VirtualListViewItemChild> getChildren()
            {
                return children;
            }

            public void setSiblingIndex(int index)
            {
                node.SetSiblingIndex(index);
            }
            public int getSiblingIndex()
            {
                return node.GetSiblingIndex();
            }
        }

        /// <summary>
        /// Item的预设坑位
        /// </summary>
        public class VirtualListCompItem
        {
            public VirtualListCompItemNode node = null;
            public int idx = -1;
            public Vector3 pos = Vector3.zero;
            public Rect size = Rect.zero;
        }


        [SerializeField] private int paddingTop = 0; //顶边距
        [SerializeField] private int paddingRight = 0; //右边距
        [SerializeField] private int paddingBottom = 0; //底边距
        [SerializeField] private int paddingLeft = 0; //左边距
        [SerializeField] private int spacingX = 0; //列距
        [SerializeField] private int spacingY = 0; //行距


        private Func<int, Rect> getItemSize = null;
        private Func<int, GameObject> getItemTemplate = null;
        private Action<GameObject, int> updateItem = null;
        // private Action<GameObject, int> clickItem = null;
        // private Action<GameObject, int> freeItem = null;
        // private Action scrollEnd = null;

        //item大小回调
        public Func<int, Rect> OnGetItemSize { set { this.getItemSize = value; } }
        //item模板回调
        public Func<int, GameObject> OnGetItemTemplate { set { this.getItemTemplate = value; } }
        //item更新回调
        public Action<GameObject, int> OnUpdateItem { set { this.updateItem = value; } }

        private readonly int _bufferZone = 10; //缓冲区大小
        private ScrollRect _scrollView = null;
        private bool _horizontal;
        private RectTransform _scrollViewRect;
        private RectTransform _scrollContent;
        private bool _inited;
        private bool _needReload;
        /// <summary>
        /// 数据源内item个数
        /// </summary>
        private int _itemCount;

        private List<VirtualListCompItem> _itemList = new List<VirtualListCompItem>();

        //这个对象池，在Unity中key要替换成什么才合适。只能是名字了
        private Dictionary<GameObject, List<VirtualListCompItemNode>> _recyclePool = new Dictionary<GameObject, List<VirtualListCompItemNode>>();


        void Awake()
        {
            this._scrollView = GetComponent<ScrollRect>();
            this._horizontal = this._scrollView.horizontal;
            this._scrollViewRect = this._scrollView.GetComponent<RectTransform>();
            this._scrollContent = this._scrollView.content;
            // 内容的锚点在左上角
            this._scrollContent.pivot = new Vector2(0, 1);
            this._scrollContent.anchorMin = new Vector2(0, 1);
            this._scrollContent.anchorMax = new Vector2(0, 1);

            // 监听滑动事件
            this._scrollView.onValueChanged.AddListener(OnScrolling);

            // 实现点击 clickItem
            // this.node.on(Node.EventType.TOUCH_START, this.onTouchStart, this);
            // this.node.on(Node.EventType.TOUCH_MOVE, this.onTouchMove, this);
            // this.node.on(Node.EventType.TOUCH_END, this.onTouchEnd, this);
            this._inited = true;
            //更新scroll布局
            _scrollViewRect.ForceUpdateRectTransforms();

            //更新view布局
            _scrollView.viewport.ForceUpdateRectTransforms();

            //更新content布局
            _scrollContent.ForceUpdateRectTransforms();

            //删除布局，大小手动计算
            // this._scrollContent.removeComponent(Widget);

            //更新位置
            if (_horizontal)
            {
                _scrollView.horizontalNormalizedPosition = 0;
            }
            else
            {
                _scrollView.verticalNormalizedPosition = 1;
            }

            if (this._needReload)
            {
                this.doReload();
            }
        }

        void OnDestroy()
        {
            this._scrollView.onValueChanged.RemoveAllListeners();
            this._itemList.Clear();
            this._recyclePool.Clear();
        }



        //刷新列表
        public void reloadData(int itemCount, bool reset_pos = true)
        {
            _itemCount = itemCount;

            if (!_inited)
            {
                _needReload = true;
                return;
            }

            if (reset_pos)
            {
                // _scrollView.stopAutoScroll();
                if (_horizontal)
                {
                    _scrollView.horizontalNormalizedPosition = 0;
                }
                else
                {
                    _scrollView.verticalNormalizedPosition = 1;
                }
            }
            doReload();
        }

        //更新列表数据
        public void updateData()
        {
            if (!this._inited)
            {
                return;
            }

            this.UpdateContent();
            this.UpdateItems(true);
        }

        private void doReload()
        {
            var needCount = _itemCount;

            int length = _itemList.Count();
            if (needCount >= length)
            {
                for (var i = length; i < needCount; ++i)
                {
                    var item = new VirtualListCompItem();
                    item.idx = i;
                    _itemList.Add(item);
                }
            }
            else
            {
                for (var i = needCount; i < length; ++i)
                {
                    var item = _itemList[i];
                    if (item.node != null)
                    {
                        FreeNode(item);
                    }
                }
                _itemList = _itemList.GetRange(0, needCount);
            }

            UpdateContent();
            UpdateItems(true);
        }

        private void UpdateContent()
        {

            //计算位置
            for (var i = 0; i < _itemCount; ++i)
            {
                calcItemPostionInfo(i);
            }

            if (_itemCount > 0)
            {
                var lastItem = _itemList[_itemCount - 1];
                //设置大小
                if (_horizontal)
                {
                    var totalWidth = lastItem.pos.x + lastItem.size.width / 2 + paddingRight;
                    _scrollContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, totalWidth);
                }
                else
                {
                    var totalHeight = Math.Abs(lastItem.pos.y - lastItem.size.height / 2 - paddingBottom);
                    _scrollContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);
                }
            }
        }

        private void calcItemPostionInfo(int idx)
        {

            var item = _itemList[idx];
            item.size = getItemSize(idx);
            if (idx == 0)
            {
                //第一个单元格位置
                item.pos = new Vector3(paddingLeft + item.size.width / 2, -paddingTop - item.size.height / 2, 0);
            }
            else
            {
                //后面的单元格依赖前面的单元格
                var itemPre = _itemList[idx - 1];

                if (_horizontal)
                {
                    //上一个单元格位置
                    float startY = itemPre.pos.y - itemPre.size.y / 2;
                    float startX = 0;
                    if (Math.Abs(startY - item.size.height - paddingBottom) > _scrollContent.rect.height)
                    {
                        //新列开始
                        startY = -paddingTop;
                        startX = itemPre.pos.x + itemPre.size.width / 2;
                        startX += spacingX;
                    }
                    else
                    {
                        startX = itemPre.pos.x - itemPre.size.width / 2;
                        startY -= spacingY;
                    }

                    item.pos = new Vector3(startX + item.size.width / 2, startY - item.size.height / 2, 0);
                }
                else
                {
                    //上一个单元格位置
                    float startX = itemPre.pos.x + itemPre.size.width / 2;
                    float startY = 0;
                    if (startX + item.size.width + paddingRight > _scrollContent.rect.width)
                    {
                        //新行开始
                        startX = paddingLeft;
                        startY = itemPre.pos.y - itemPre.size.height / 2;
                        startY -= spacingY;
                    }
                    else
                    {
                        startY = itemPre.pos.y + itemPre.size.height / 2;
                        startX += spacingX;
                    }

                    item.pos = new Vector3(startX + item.size.width / 2, startY - item.size.height / 2, 0);
                }
            }
        }


        void OnScrolling(Vector2 pos)
        {
            //数量少，不需要更新
            if (_horizontal)
            {
                if (_scrollContent.rect.width < _scrollViewRect.rect.width + _bufferZone * 2)
                {
                    return;
                }
            }
            else
            {
                if (_scrollContent.rect.height < _scrollViewRect.rect.height + _bufferZone * 2)
                {
                    return;
                }
            }

            UpdateItems();
        }

        List<bool> showList;
        private void UpdateItems(bool forceUpdate = false)
        {

            List<VirtualListCompItem> inViewList = new List<VirtualListCompItem>();
            var itemCount = _itemList.Count;

            showList = new List<bool>(itemCount);
            for (var i = 0; i < itemCount; ++i)
            {
                instantiateFunc(i, forceUpdate, inViewList);
            }
            string s = "";
            for (var i = 0; i < showList.Count; ++i)
            {
                s += $"{i}:{showList[i]} ";
            }
            // DU.LogWarning(s);

            // this.frameLoad(itemCount, this.instantiateFunc.bind(this), 5, 0, forceUpdate, inViewList);

            //排序节点，下层覆盖上层，用于挖矿地图
            // if (this._useCoverageOrder)
            // {
            //     let need = false
            //     for (let i = 0; i < inViewList.length - 1; i++)
            //     {
            //         let l = inViewList[i].node.getSiblingIndex()
            //         let r = inViewList[i + 1].node.getSiblingIndex()
            //         if (l >= r)
            //         {
            //             need = true
            //         }
            //     }

            //     if (need)
            //     {
            //         for (let i = 0; i < inViewList.length; i++)
            //         {
            //             inViewList[i].node.setSiblingIndex(i);
            //         }
            //         // console.warn("VirtualList ui sort")
            //     }
            // }
        }

        /// <summary>
        /// 分帧加载
        /// </summary>
        // public void frameLoad(int loop, Func func, int frameTime = 5, int __index = 0, bool forceUpdate, inViewList: VirtualListViewItem[])
        // {
        //     var end = 0;
        //     var dt = 0;
        //     for (var i = 0; i < loop; ++i)
        //     {
        //         if (__index >= loop)
        //         {
        //             break;
        //         }

        //         func && func(__index, forceUpdate, inViewList);

        //         __index++;
        //         end = new Date().getTime();
        //         dt = end - start;
        //         if (dt > frameTime)
        //         {
        //             setTimeout(() =>
        //             {
        //                 this._scrollViewNode?.isValid && this.frameLoad(loop, func, frameTime, __index, forceUpdate, inViewList);
        //             }, 10);
        //             break;
        //         }
        //     }
        // }




        /// <summary>
        /// 根据item位置，计算它是否在可视区域内，并实例化或更新它
        /// </summary>
        public void instantiateFunc(int index, bool forceUpdate, List<VirtualListCompItem> inViewList)
        {
            if (!_scrollViewRect) return;
            double offset = 0;

            //以左上角为原点
            if (_horizontal)
            {
                offset = -_scrollContent.anchoredPosition.x;
            }
            else
            {
                offset = -_scrollContent.anchoredPosition.y;
            }
            var item = _itemList[index];
            var isItemInView = true;
            //rect的x、y值代表UI元素左下角到轴点（Pivot）的相对位置
            if (_horizontal)
            {
                if (item.pos.x - item.size.width / 2 > offset + _scrollViewRect.rect.width + _bufferZone)
                {
                    //检查是否超过右部可视区域
                    isItemInView = false;
                }
                else if (item.pos.x + item.size.width / 2 < offset - _bufferZone)
                {
                    //检查是否超过左部可视区域
                    isItemInView = false;
                }
            }
            else
            {
                if (item.pos.y + item.size.height / 2 < offset - _scrollViewRect.rect.height - _bufferZone)
                {
                    //检查是否超过下部可视区域
                    isItemInView = false;
                }
                else if (item.pos.y - item.size.height / 2 > offset + _bufferZone)
                {
                    //检查是否超过上部可视区域
                    isItemInView = false;
                }
            }

            showList.Add(isItemInView);

            if (isItemInView)
            {
                if (item.node == null)
                {
                    item.node = GetNode(item);
                    item.node.inst.SetActive(true);
                    item.node.SetPosition(item.pos);
                    updateItem(item.node.inst, item.idx);
                }
                else
                {

                    if (item.node.template == getItemTemplate(index))
                    {
                        if (forceUpdate)
                        {
                            item.node.SetPosition(item.pos);
                            updateItem(item.node.inst, item.idx);
                        }
                    }
                    else
                    {
                        //在虚拟列表同时有多种item的情况下，如果item种类变了需要换重新换item
                        FreeNode(item);
                        item.node = GetNode(item);
                        item.node.SetPosition(item.pos);
                        updateItem(item.node.inst, item.idx);
                    }
                }
                inViewList.Add(item);
            }
            else
            {
                if (item.node != null)
                {
                    FreeNode(item);
                    item.node = null;
                }
            }
        }


        /// <summary>
        /// 有缓存就从对象池中获取
        /// 没有缓存就实例化一个新的实例
        /// </summary>
        private VirtualListCompItemNode GetNode(VirtualListCompItem item)
        {
            int index = item.idx;
            var itemTemplate = getItemTemplate(index);
            if (_recyclePool.TryGetValue(itemTemplate, out var lst) && lst.Count() > 0)
            {
                var itemNode = lst[lst.Count - 1];
                lst.RemoveAt(lst.Count - 1);
                return itemNode;
            }
            else
            {
                var itemNode = Instantiate(itemTemplate);
                itemNode.gameObject.SetActive(true);
                var virtualNode = new VirtualListCompItemNode();
                virtualNode.setItemTemplate(itemNode, itemTemplate);
                virtualNode.setParent(_scrollView.content);

                return virtualNode;
            }
        }

        private void FreeNode(VirtualListCompItem item)
        {
            var itemNode = item.node;
            //隐藏
            itemNode.inst.SetActive(false);
            if (!_recyclePool.TryGetValue(itemNode.template, out var lst))
            {
                //没有缓存就创建一个新的列表
                lst = new List<VirtualListCompItemNode>();
                _recyclePool[itemNode.template] = lst;
            }

            lst.Add(itemNode);

        }


        //竖着滚动到目标项位置, 效果为：目标项上部在ScrollView的左上角
        public void ScrollToItemVertical(int index,int offset = 0)
        {
            var item = _itemList[index];
            if (item == null) return;
            var y = item.pos.y;
            _scrollView.verticalNormalizedPosition = 1 - (-y - item.size.height/2 + offset) / (_scrollContent.rect.height - _scrollViewRect.rect.height);
            updateData();
        }
    }

}

