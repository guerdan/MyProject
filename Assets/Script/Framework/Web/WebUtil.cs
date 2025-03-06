
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Script.Framework.Web
{
    public class WebUtil : MonoBehaviour
    {

        #region 发日志
        //TKP日志流程的需求还蛮复杂.  DataLogManager.LaunchEvent()
        private static string _address = "https://120-yzzqx-houtai-sim01.tytuyoo.com/api/yzzqx/client_log"; //地址

//         public void PostLog(string address, string log, float delayTime = 0f)
//         {
//             // Utility.Log("PostLog:" + log);
// #if !UNITY_EDITOR
//             StartCoroutine(PostLogIEnumerator(address, log, delayTime));
// #endif
//         }

        // private IEnumerator PostLogIEnumerator(string address, string log, float delayTime)
        // {
        //     yield return new WaitForSecondsRealtime(delayTime);
        //     //Debug.LogWarning($"正在发送日志...{address}-日志量:{log.Length}个字符");

        //     var byteArray = Compress(Encoding.UTF8.GetBytes(log));   //zip压缩数据
        //     var encryptionArray = ConnectWebSocket.Encryption(byteArray, ConnectWebSocket.Key);//对称加密
        //     UploadHandlerRaw handler = new UploadHandlerRaw(encryptionArray);  //简单地包装数据
        //     //Debug.LogWarning($"压缩后的字节:{encryptionArray.Length}");

        //     var unityWebRequest = new UnityWebRequest(address, UnityWebRequest.kHttpVerbPOST)  //请求
        //     {
        //         uploadHandler = handler,
        //         timeout = 10
        //     };
        //     yield return unityWebRequest.SendWebRequest();  //发送请求
        //     if (unityWebRequest.isNetworkError || unityWebRequest.isHttpError)
        //     {
        //         // DataLogManager.LogPostFail();
        //     }
        //     else
        //     {
        //         // DataLogManager.LogPostSuccess();
        //     }
        //     handler.Dispose();
        //     handler = null;
        //     unityWebRequest.Dispose();
        // }

        #endregion
    }
}