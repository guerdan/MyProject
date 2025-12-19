
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Script.Util;

namespace Script.Model.ListStruct
{

    public struct MyStruct : IComparable<MyStruct>
    {
        public int Value;

        public MyStruct(int value)
        {
            Value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(MyStruct other)
        {
            return Value.CompareTo(other.Value);
        }
    }

    public class BSTNode<T> 
    {
        public T Value;
        public BSTNode<T> Left;
        public BSTNode<T> Right;

        public BSTNode(T value)
        {
            Value = value;
            Left = null;
            Right = null;
        }
    }

    public class BinarySearchTree<T> 
    {
        private BSTNode<T> _root;
        private Func<T, T, int> _comparer;
        private bool _delete_success;

        // 可以自定义比较器
        public BinarySearchTree(Func<T, T, int> comparer)
        {
            _root = null;
            _comparer = comparer;
        }

        // 查找
        public bool Search(T value)
        {
            return Search(_root, value) != null;
        }

        private BSTNode<T> Search(BSTNode<T> node, T value)
        {
            if (node == null || _comparer(node.Value, value) == 0)
                return node;

            if (_comparer(node.Value, value) < 0)
                return Search(node.Left, value);
            else
                return Search(node.Right, value);
        }

        // 是否为空
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Empty()
        {
            return _root == null;
        }


        // 插入
        public void Insert(T value)
        {
            _root = Insert(_root, value);
        }

        private BSTNode<T> Insert(BSTNode<T> node, T value)
        {
            if (node == null)
                return new BSTNode<T>(value);


            int compare = _comparer(value, node.Value);

            if (compare < 0)
                node.Left = Insert(node.Left, value);
            else if (compare > 0)
                node.Right = Insert(node.Right, value);

            return node;
        }

        // 删除
        public bool Delete(T value)
        {
            _delete_success = false;
            _root = Delete(_root, value);
            return _delete_success;
        }

        private BSTNode<T> Delete(BSTNode<T> node, T value)
        {
            if (node == null)
                return null;

            int compare = _comparer(value, node.Value);

            if (compare < 0)
            {
                node.Left = Delete(node.Left, value);
            }
            else if (compare > 0)
            {
                node.Right = Delete(node.Right, value);
            }
            else
            {
                _delete_success = true;
                // 情况 1: 叶子节点
                if (node.Left == null && node.Right == null)
                    return null;

                // 情况 2: 只有一个子节点
                if (node.Left == null)
                    return node.Right;
                if (node.Right == null)
                    return node.Left;

                // 情况 3: 有两个子节点
                // 找到中序后继（右子树的最小值）
                // todo (FindMin + Delete) 优化下
                BSTNode<T> successor = FindMinAndDelete(node.Right);
                node.Value = successor.Value;
                if (node.Right == successor)
                {
                    var right = node.Right.Right;
                    node.Right = right != null ? right : null;
                }

                // BSTNode<T> successor = FindMinAndDelete(node.Right);
                // node.Value = successor.Value;
                // node.Right = Delete(node.Right, successor.Value);
            }

            return node;
        }

        private BSTNode<T> FindMinAndDelete(BSTNode<T> node)
        {
            BSTNode<T> last = null;
            while (node.Left != null)
            {
                last = node;
                node = node.Left;
            }

            // node是被删节点,无左节点
            if (last != null)
                last.Left = node.Right != null ? node.Right : null;

            return node;
        }
        public BSTNode<T> FindMin()
        {
            var node = _root;
            while (node.Left != null)
                node = node.Left;
            return node;
        }

        // 中序遍历
        List<int> _list;
        string _str = "";
        int _count = 0;
        public void InOrderTraversal()
        {
            _str = "";
            _list = new List<int>();
            InOrderTraversal(_root);
            DU.Log(_str);

            bool log = false;
            for (int i = 1; i < _list.Count; i++)
            {
                if (_list[i] < _list[i - 1])
                {
                    log = true;
                    break;
                }
            }
            if (log)
                DU.LogError("排序错误");
        }

        private void InOrderTraversal(BSTNode<T> node)
        {
            if (node == null)
                return;

            InOrderTraversal(node.Left);
            _str += node.Value.ToString() + " ";
            _list.Add(int.Parse(node.Value.ToString()));
            InOrderTraversal(node.Right);
        }

        public void InOrderTraversalCount()
        {
            _count = 0;
            InOrderTraversalCount(_root);
            DU.Log($"数量 {_count} 树高 {GetHeight()}");
        }
        private void InOrderTraversalCount(BSTNode<T> node)
        {
            if (node == null)
                return;

            InOrderTraversalCount(node.Left);
            _count++;
            InOrderTraversalCount(node.Right);
        }

        public int GetHeight()
        {
            return GetHeight(_root);
        }

        private int GetHeight(BSTNode<T> node)
        {
            if (node == null)
                return 0;

            int leftHeight = GetHeight(node.Left);
            int rightHeight = GetHeight(node.Right);

            return Math.Max(leftHeight, rightHeight) + 1;
        }

       

    }

}