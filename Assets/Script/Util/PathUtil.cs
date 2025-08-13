
namespace Script.Util
{
    public static class PathUtil
    {
        public static string SquareFrameUIPath = "Common/Prefabs/Component/SquareFrameUI";
    // public static RefreshItemListByCount(itemList : Node[], count : number, prefab : Node,
    //      parent : Node, process : (item : Node, index : number) => void) 
    // {
    //     if (itemList == null || prefab == null) return
    //     if (count == null || count == 0) {
    //         itemList.forEach(node => node.active = false)
    //         return
    //     }

    //     let need = count - itemList.length
    //     for (let i = 0; i < need; i++) {
    //         let node = instantiate(prefab)
    //         parent.addChild(node)
    //         node.scale = prefab.scale;
    //         itemList.push(node);
    //     }

    //     for (let i = 0; i < itemList.length; i++)
    //     {
    //         let node = itemList[i];
    //         let show = i < count;
    //         node.active = show

    //         if (show)
    //         {
    //             process(node, i);
    //             node.setSiblingIndex(parent.children.length - 1); //设置到最底部
    //         }

    //     }
    // }
    }
}