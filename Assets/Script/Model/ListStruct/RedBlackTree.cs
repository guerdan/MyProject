
using System;
using System.Runtime.CompilerServices;
using Script.Util;

namespace Script.Model.ListStruct
{
    public enum NodeColor
    {
        Red,
        Black
    }

    public class RedBlackNode<T> where T : IComparable<T>
    {
        public T Value;
        public NodeColor Color;
        public RedBlackNode<T> Left;
        public RedBlackNode<T> Right;
        public RedBlackNode<T> Parent;

        public RedBlackNode(T value)
        {
            Value = value;
            Color = NodeColor.Red; // 新插入的节点默认为红色
        }
    }

    public class RedBlackTree<T> where T : IComparable<T>
    {
        private RedBlackNode<T> _root;

        // 插入节点
        public void Insert(T value)
        {
            var newNode = new RedBlackNode<T>(value);
            if (_root == null)
            {
                _root = newNode;
                _root.Color = NodeColor.Black; // 根节点必须是黑色
                return;
            }

            RedBlackNode<T> parent = null;
            var current = _root;

            // 按照二叉搜索树规则找到插入位置
            while (current != null)
            {
                parent = current;
                if (value.CompareTo(current.Value) < 0)
                    current = current.Left;
                else
                    current = current.Right;
            }

            newNode.Parent = parent;
            if (value.CompareTo(parent.Value) < 0)
                parent.Left = newNode;
            else
                parent.Right = newNode;

            // 调整红黑树
            FixInsert(newNode);
        }

        private void FixInsert(RedBlackNode<T> node)
        {
            while (node != _root && node.Parent.Color == NodeColor.Red)
            {
                var parent = node.Parent;
                if (parent == parent.Parent.Left)
                {
                    var uncle = parent.Parent.Right;

                    // 情况 1: 叔叔节点是红色
                    if (uncle != null && uncle.Color == NodeColor.Red)
                    {
                        parent.Color = NodeColor.Black;
                        uncle.Color = NodeColor.Black;
                        parent.Parent.Color = NodeColor.Red;
                        node = parent.Parent;
                    }
                    else
                    {
                        // 情况 2: 叔叔节点是黑色，当前节点是右子节点
                        if (node == parent.Right)
                        {
                            node = parent;
                            RotateLeft(node);
                        }

                        // 情况 3: 叔叔节点是黑色，当前节点是左子节点
                        node.Parent.Color = NodeColor.Black;
                        node.Parent.Parent.Color = NodeColor.Red;
                        RotateRight(node.Parent.Parent);
                    }
                }
                else
                {
                    var uncle = parent.Parent.Left;

                    // 对称处理
                    if (uncle != null && uncle.Color == NodeColor.Red)
                    {
                        parent.Color = NodeColor.Black;
                        uncle.Color = NodeColor.Black;
                        parent.Parent.Color = NodeColor.Red;
                        node = parent.Parent;
                    }
                    else
                    {
                        if (node == parent.Left)
                        {
                            node = parent;
                            RotateRight(node);
                        }

                        node.Parent.Color = NodeColor.Black;
                        node.Parent.Parent.Color = NodeColor.Red;
                        RotateLeft(node.Parent.Parent);
                    }
                }
            }

            _root.Color = NodeColor.Black; // 根节点始终是黑色
        }

        private void RotateLeft(RedBlackNode<T> node)
        {
            var pivot = node.Right;
            node.Right = pivot.Left;
            var parent = node.Parent;

            if (pivot.Left != null)
                pivot.Left.Parent = node;

            pivot.Parent = parent;

            if (parent == null)
                _root = pivot;
            else if (node == parent.Left)
                parent.Left = pivot;
            else
                parent.Right = pivot;

            pivot.Left = node;
            node.Parent = pivot;
        }

        private void RotateRight(RedBlackNode<T> node)
        {
            var pivot = node.Left;
            node.Left = pivot.Right;
            var parent = node.Parent;

            if (pivot.Right != null)
                pivot.Right.Parent = node;

            pivot.Parent = parent;

            if (parent == null)
                _root = pivot;
            else if (node == parent.Right)
                parent.Right = pivot;
            else
                parent.Left = pivot;

            pivot.Right = node;
            node.Parent = pivot;
        }

        // 删除节点
        public void Delete(T value)
        {
            var nodeToDelete = FindNode(_root, value);
            if (nodeToDelete == null)
                return;

            DeleteNode(nodeToDelete);
        }

        private void DeleteNode(RedBlackNode<T> node)
        {
            RedBlackNode<T> replacement; // 替代节点
            RedBlackNode<T> parent = node.Parent;
            NodeColor originalColor = node.Color; // 记录被删除节点的颜色

            if (node.Left == null) // 情况 1: 没有左子节点，用右子节替代并变黑
            {
                replacement = node.Right;
                Transplant(node, node.Right); // 用右子节点替代当前节点
            }
            else if (node.Right == null) // 情况 2: 没有右子节点，用左子节替代并变黑
            {
                replacement = node.Left;
                Transplant(node, node.Left); // 用左子节点替代当前节点
            }
            else
            {
                // 情况 3: 有两个子节点，替换后问题就转移到删后继节点了。
                //
                // 操作：先找到后继节点N1, 把N1替换到N的位置
                // N1原来就没有左子节点属于情况1，所以原位置的空挡处理--用右子节替代并变黑
                //
                // 找到后继节点（右子树的最小节点）
                var successor = FindMin(node.Right);
                originalColor = successor.Color;
                replacement = successor.Right;

                // 用后继节点的右子节点替代后继节点
                Transplant(successor, successor.Right);
                parent = successor.Parent;

                // 用后继节点替代当前节点，值替代就行
                node.Value = successor.Value;
            }

            // 如果删除的节点是黑色，修复红黑树
            if (originalColor == NodeColor.Black)
                FixDelete(replacement, parent);
        }

        // 如果删的是叶子节点，node就是null，就不知道删的是左还是右
        private void FixDelete(RedBlackNode<T> node, RedBlackNode<T> parent)
        {
            while (node != _root && (node == null || node.Color == NodeColor.Black))
            {
                // 删除黑叶节点时，必然有兄弟节点。因为在原本稳定的情况下，单黑子节点违反"黑路同"
                if (node == parent.Left || node == null && parent.Right != null) // 当前节点是左子节点
                {
                    var sibling = parent.Right; // 获取兄弟节点

                    // 情况 1: 兄弟节点是红色
                    if (sibling != null && sibling.Color == NodeColor.Red)
                    {
                        sibling.Color = NodeColor.Black;
                        parent.Color = NodeColor.Red;
                        RotateLeft(parent);
                        sibling = parent.Right;
                    }

                    // 情况 2: 兄弟节点是黑色，且两个子节点也是黑色
                    if (sibling == null || (sibling.Left == null || sibling.Left.Color == NodeColor.Black)
                        && (sibling.Right == null || sibling.Right.Color == NodeColor.Black))
                    {
                        if (sibling != null)
                            sibling.Color = NodeColor.Red;
                        node = parent;
                        parent = node.Parent;
                    }
                    else
                    {
                        // 情况 3: 兄弟节点是黑色，左子节点是红色，右子节点是黑色
                        if (sibling.Right == null || sibling.Right.Color == NodeColor.Black)
                        {
                            sibling.Left.Color = NodeColor.Black;
                            sibling.Color = NodeColor.Red;
                            RotateRight(sibling);
                            sibling = parent.Right;
                        }

                        // 情况 4: 兄弟节点是黑色，右子节点是红色
                        sibling.Color = parent.Color;
                        parent.Color = NodeColor.Black;
                        sibling.Right.Color = NodeColor.Black;
                        RotateLeft(parent);
                        node = _root;
                    }
                }
                else
                {
                    // 对称处理
                    var sibling = parent.Left;

                    if (sibling != null && sibling.Color == NodeColor.Red)
                    {
                        sibling.Color = NodeColor.Black;
                        parent.Color = NodeColor.Red;
                        RotateRight(parent);
                        sibling = parent.Left;
                    }

                    if (sibling == null || (sibling.Right == null || sibling.Right.Color == NodeColor.Black)
                        && (sibling.Left == null || sibling.Left.Color == NodeColor.Black))
                    {
                        if (sibling != null)
                            sibling.Color = NodeColor.Red;
                        node = parent;
                        parent = node.Parent;
                    }
                    else
                    {
                        if (sibling.Left == null || sibling.Left.Color == NodeColor.Black)
                        {
                            sibling.Right.Color = NodeColor.Black;
                            sibling.Color = NodeColor.Red;
                            RotateLeft(sibling);
                            sibling = parent.Left;
                        }

                        sibling.Color = parent.Color;
                        parent.Color = NodeColor.Black;
                        sibling.Left.Color = NodeColor.Black;
                        RotateRight(parent);
                        node = _root;
                    }
                }
            }

            if (node != null)
                node.Color = NodeColor.Black;
        }

        private void Transplant(RedBlackNode<T> self, RedBlackNode<T> v)
        {
            var parent = self.Parent;
            if (parent == null)
                _root = v;
            else if (self == parent.Left)
                parent.Left = v;
            else
                parent.Right = v;

            if (v != null)
                v.Parent = parent;
        }

        private RedBlackNode<T> FindNode(RedBlackNode<T> node, T value)
        {
            while (node != null)
            {
                int compare = value.CompareTo(node.Value);
                if (compare == 0)
                    return node;
                else if (compare < 0)
                    node = node.Left;
                else
                    node = node.Right;
            }
            return null;
        }

        private RedBlackNode<T> FindMin(RedBlackNode<T> node)
        {
            while (node.Left != null)
                node = node.Left;
            return node;
        }

        private NodeColor GetColor(RedBlackNode<T> node)
        {
            return node == null ? NodeColor.Black : node.Color;
        }

        // 中序遍历
        public void InOrderTraversal()
        {
            InOrderTraversal(_root);
            // Console.WriteLine();
        }

        private void InOrderTraversal(RedBlackNode<T> node)
        {
            if (node == null)
                return;

            InOrderTraversal(node.Left);
            // Console.Write($"{node.Value}({node.Color}) ");
            InOrderTraversal(node.Right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T FindMin()
        {
            var node = _root;
            while (node.Left != null)
                node = node.Left;
            return node.Value;
        }

        // 是否为空
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Empty()
        {
            return _root == null;
        }
    }
}