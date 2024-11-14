using UnityEngine;

namespace Script.Tween
{
    public class MoveParent:MonoBehaviour
    {
        public GameObject child;

        
        public GameObject myObject;
        void Start()
        {
            // var go = child.GetComponent<MoveRect>().myObject;
            // Helper.Log(go != null ? "has" :"no"); // 输出: True

            
            
            myObject = new GameObject("MyObject");

            // 销毁 GameObject
            Destroy(myObject);

            // 在下一帧之前，myObject 引用仍然存在
            Helper.Log((myObject != null).ToString()); // 输出: True

            // 尝试访问已销毁对象的属性
            Helper.Log(myObject.name); // 输出: MyObject
        }
        
        void Update()
        {

            // 检查对象是否已销毁
            if (myObject == null)
            {
                Helper.Log("myObject is null");
            }
            else
            {
                Helper.Log(myObject.name); // 安全访问属性
            }
        }

    }
}