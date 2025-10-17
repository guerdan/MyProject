
using System;
using System.Runtime.CompilerServices;

namespace Script.Model.ListStruct
{
    public class AVLNode<T> where T : IComparable<T>
    {
        public T Value;
        public AVLNode<T> Left;
        public AVLNode<T> Right;
        public int Height;

        public AVLNode(T value)
        {
            Value = value;
            Height = 1; // 初始高度为 1
        }
    }

    public class AVLTree<T> where T : IComparable<T>
    {
        private AVLNode<T> _root;

        // 获取节点高度
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetHeight(AVLNode<T> node)
        {
            return node == null ? 0 : node.Height;
        }

        // 计算平衡因子
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetBalanceFactor(AVLNode<T> node)
        {
            return node == null ? 0 : GetHeight(node.Left) - GetHeight(node.Right);
        }

        // 更新节点高度
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateHeight(AVLNode<T> node)
        {
            node.Height = Math.Max(GetHeight(node.Left), GetHeight(node.Right)) + 1;
        }

        // 右旋
        // 平衡因子 = 2 就右旋
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AVLNode<T> RotateRight(AVLNode<T> y)
        {
            AVLNode<T> x = y.Left;
            AVLNode<T> T2 = x.Right;

            // 旋转
            x.Right = y;
            y.Left = T2;

            // 更新高度
            UpdateHeight(y);
            UpdateHeight(x);

            return x;
        }

        // 左旋
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AVLNode<T> RotateLeft(AVLNode<T> x)
        {
            AVLNode<T> y = x.Right;
            AVLNode<T> T2 = y.Left;

            // 旋转
            y.Left = x;
            x.Right = T2;

            // 更新高度
            UpdateHeight(x);
            UpdateHeight(y);

            return y;
        }

        bool _insert_rotate = false;
        // 插入节点
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(T value)
        {
            _insert_rotate = false;
            _root = Insert(_root, value);
        }
        private AVLNode<T> Insert(AVLNode<T> node, T value)
        {
            if (node == null)
                return new AVLNode<T>(value);

            int compare = value.CompareTo(node.Value);
            if (compare < 0)
                node.Left = Insert(node.Left, value);
            else if (compare > 0)
                node.Right = Insert(node.Right, value);
            else
                return node; // 不允许插入重复值

            // 更新高度
            UpdateHeight(node);

            // 旋转调整一次就够了，上层的节点会自动平衡
            //
            // if (!_insert_rotate)
            // {
            //     return node;
            // }


            // 检查平衡因子
            int balance = GetBalanceFactor(node);
            if (balance > 1)
            {
                // _insert_rotate = true;

                int left_compare = value.CompareTo(node.Left.Value);
                if (left_compare < 0)
                    // 左左情况
                    return RotateRight(node);
                else
                {
                    // 左右情况
                    node.Left = RotateLeft(node.Left);
                    return RotateRight(node);
                }
            }
            if (balance < -1)
            {
                // _insert_rotate = true;

                int right_compare = value.CompareTo(node.Right.Value);
                if (right_compare > 0)
                    // 右右情况
                    return RotateLeft(node);
                else
                {
                    // 右左情况
                    node.Right = RotateRight(node.Right);
                    return RotateLeft(node);
                }
            }

            return node;
        }

        // 删除节点
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Delete(T value)
        {
            _root = Delete(_root, value);
        }

        private AVLNode<T> Delete(AVLNode<T> node, T value)
        {
            if (node == null)
                return null;

            int compare = value.CompareTo(node.Value);

            // 找到要删除的节点
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
                // 情况 1: 叶子节点
                if (node.Left == null && node.Right == null)
                    return null;

                // 情况 2: 只有一个子节点
                if (node.Left == null)
                    return node.Right;
                if (node.Right == null)
                    return node.Left;

                // 情况 3: 有两个子节点
                // 找到右子树的最小值
                AVLNode<T> successor = FindMin(node.Right);
                node.Value = successor.Value;
                node.Right = Delete(node.Right, successor.Value);
            }

            // 更新高度
            UpdateHeight(node);

            // 检查平衡因子
            int balance = GetBalanceFactor(node);

            // 左左情况
            if (balance > 1)
                if (GetBalanceFactor(node.Left) >= 0)
                    return RotateRight(node);
                else
                {
                    // 左右情况
                    node.Left = RotateLeft(node.Left);
                    return RotateRight(node);
                }

            if (balance < -1)
                if (GetBalanceFactor(node.Right) <= 0)
                    return RotateLeft(node);
                else
                {
                    // 右左情况
                    node.Right = RotateRight(node.Right);
                    return RotateLeft(node);
                }

            return node;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AVLNode<T> FindMin(AVLNode<T> node)
        {
            while (node.Left != null)
                node = node.Left;
            return node;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AVLNode<T> FindMin()
        {
            var node = _root;
            while (node.Left != null)
                node = node.Left;
            return node;
        }

        // 是否为空
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Empty()
        {
            return _root == null;
        }

        // 中序遍历
        public void InOrderTraversal()
        {
            InOrderTraversal(_root);
            Console.WriteLine();
        }

        private void InOrderTraversal(AVLNode<T> node)
        {
            if (node == null)
                return;

            InOrderTraversal(node.Left);
            Console.Write(node.Value + " ");
            InOrderTraversal(node.Right);
        }
    }
}