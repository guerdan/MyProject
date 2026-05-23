
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace Script.Util
{
    public class PipelineServer
    {

        readonly int SendoOverTime = 5000;  // ms
        public string PipeName => _pipeName;
        string _pipeName;
        Action<string> _listen;
        Thread pipelineThread;
        bool isOn = true;
        Queue<string> poopoo = new Queue<string>();

        // 内核资源，需手动释放
        // 1.低消耗。AutoResetEvent 会让线程进入等待状态，释放 CPU 资源，直到条件满足或信号触发
        // 2.低延迟。比轮询Sleep阻塞的延迟更低。唤醒操作是由操作系统内核管理的，延迟通常在微秒到毫秒级别。
        AutoResetEvent messageEvent = new AutoResetEvent(false);

        NamedPipeServerStream _namedPipeServer;
        NamedPipeClientStream _namedPipeClient;

        DateTime _send_time;

        public PipelineServer(bool isServer, string pipeName, Action<string> listen)
        {
            _pipeName = $"Unity_{pipeName}" ;
            _listen = listen;
            if (isServer)
            {
                DU.LogWarning($"创建  {_pipeName}PipeServer");
                pipelineThread = new Thread(pipelineRecive);
            }
            else
            {
                DU.LogWarning($"创建  {_pipeName}PipeClient");
                pipelineThread = new Thread(pipelineSend);
            }
            // 这个设置的功能：主线程关闭时, 自动关闭由它创建的其他线程
            pipelineThread.IsBackground = true;
        }
        void pipelineRecive()
        {
            try
            {
                while (isOn)    // 为的是不断地收发消息。只要底下有阻塞调用，就不会死循环。
                {

                    var pipeSecurity = new PipeSecurity();

                    // 允许 Everyone 连接、读写
                    SecurityIdentifier everyoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

                    // 上下两种方式都行
                    // pipeSecurity.AddAccessRule(new PipeAccessRule(everyoneSid, PipeAccessRights.FullControl, AccessControlType.Allow));
                    pipeSecurity.AddAccessRule(new PipeAccessRule("Everyone", PipeAccessRights.FullControl, AccessControlType.Allow));

                    using (_namedPipeServer = new NamedPipeServerStream($"{_pipeName}", PipeDirection.InOut
                    , 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, pipeSecurity))
                    {
                        // 必须在 new StreamReader(_namedPipeServer)之前
                        // 因为 它的操作会给_namedPipeServer引用计数+1，导致WaitForConnection()无法强制退出。
                        _namedPipeServer.WaitForConnection();

                        using (StreamReader sr = new StreamReader(_namedPipeServer))
                        {
                            string mesg = sr.ReadToEnd();
                            _listen?.Invoke(mesg);
                            // DU.LogWarning($"收到  {DateTime.Now.Millisecond}");
                        }

                    }
                }
            }
            catch (Exception e)
            {
                // 屏蔽报错，监听必须强制退出
                DU.LogWarning($"释放  {_pipeName}PipeServer");

            }
        }

        void pipelineSend()
        {
            try
            {
                while (isOn)
                {
                    messageEvent.WaitOne();         // 等待信号
                    // DU.LogWarning($"WaitOne  {DateTime.Now.Millisecond}");
                    if (poopoo.Count > 0)
                    {
                        // 第一个参数是IP。"."等价于"localhost"为本机的意思。
                        using (_namedPipeClient = new NamedPipeClientStream(".", $"{_pipeName}"
                            , PipeDirection.InOut, PipeOptions.WriteThrough | PipeOptions.Asynchronous,
                            System.Security.Principal.TokenImpersonationLevel.None, HandleInheritability.None))
                        {

                            // 要是 _namedPipeServer.WaitForConnection();慢于Connect()。不能匹配上，会永久阻塞 = 
                            _send_time = DateTime.Now;
                            try
                            {
                                _namedPipeClient.Connect();
                            }
                            catch (Exception)
                            {
                                continue;
                            }


                            using (StreamWriter sw = new StreamWriter(_namedPipeClient))
                            {

                                sw.WriteLine(poopoo.Dequeue());
                                // DU.LogWarning($"Dispose  {DateTime.Now.Millisecond}");
                            }

                        }
                    }
                }
            }
            catch (Exception e)
            {
                DU.LogWarning($"释放  {_pipeName}PipeClient");

            }

        }
        public void Start()
        {
            pipelineThread.Start();
        }
        public void Close()
        {
            isOn = false;
            poopoo.Clear();
            messageEvent.Set();
            messageEvent.Dispose();

            if (_namedPipeServer != null)
                _namedPipeServer.Dispose();
            if (_namedPipeClient != null)
                _namedPipeClient.Dispose();

            // 不用强制销毁线程，这样容易导致漏资源细节
            // if (pipelineThread != null)
            // {
            //     pipelineThread.Interrupt();
            //     pipelineThread.Abort();
            //     pipelineThread = null;
            // }
        }

        /// <summary>
        /// 如果接收端的延迟很大，那么消息会累计在队列里
        /// </summary>
        public void AddSendMsg(string information)
        {
            poopoo.Enqueue(information);
            messageEvent.Set(); // 发送信号，唤醒等待的线程

            // 如果上个连接超时没成功，那就打断吧
            var delta = (DateTime.Now - _send_time).Milliseconds;
            if (delta > 5000)
            {
                if (_namedPipeClient != null)
                    _namedPipeClient.Dispose();
            }

        }
    }
}