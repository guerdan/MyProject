
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Script.Util;

namespace Script.Model.Auto
{
    public class AutoMMFUnit
    {
        public MMFPeer Client;
        public AutoPipeMsg Msg;

        public AutoMMFUnit(MMFPeer client, AutoPipeMsg msg)
        {
            Client = client;
            Msg = msg;
        }
    }


    [Serializable]
    public class AutoPipeMsg
    {
        [JsonProperty("k_m")]           // keyboard_msg
        public List<KeyboardMsg> KeyboardMsgs = new List<KeyboardMsg>();

        [JsonProperty("r_c")]           // run_command
        public ScriptRunCommand RunCommand;
    }

    [Serializable]
    public struct KeyboardMsg
    {
        [JsonProperty("k")]             // key
        public KeyboardEnum Key;
        [JsonProperty("d")]             // is_down
        public bool IsDown;


        public KeyboardMsg(KeyboardEnum key, bool isDown)
        {
            Key = key;
            IsDown = isDown;
        }
    }

    /// <summary>
    /// 发送脚本运行的相关指令
    /// </summary>
    public enum ScriptRunCommand
    {
        Undefined,
        StartScript,
        StopScript,
        TerminateScript,
    }

    // 管理员权限，才有进程通信功能。
    public partial class AutoScriptManager
    {
        MMFPeer _serverMMF;
        Dictionary<string, AutoMMFUnit> _clientMMFs = new Dictionary<string, AutoMMFUnit>();


        public void InitNamePipe()
        {
            if (!Utils.IsAdmin) return;

            string pipeName = "";
            foreach (var k in Settings.PipeMapping)
                if (Utils.UserName == k[1])
                    pipeName = k[0];

            if (pipeName != "")
            {
                _serverMMF = new MMFPeer(pipeName, true, OnMessageMMF);
            }
            Settings.PipeName = pipeName;
        }


        public void DestroyNamePipe()
        {

            if (_serverMMF != null)
            {
                _serverMMF.OnDestroy();
                _serverMMF = null;
            }

            foreach (var unit in _clientMMFs.Values)
            {
                unit.Client.OnDestroy();
            }
            _clientMMFs.Clear();
        }
        public List<string> GetAllPipeNames()
        {
            var list = new List<string>();
            foreach (var k in Settings.PipeMapping)
                if (!string.IsNullOrEmpty(k[1]))
                    list.Add(k[0]);

            return list;
        }


        #region Listen

        void OnUpdateMMF()
        {
            if (_serverMMF != null)
            {
                _serverMMF.OnUpdate();
            }
        }

        void OnMessageMMF(string msgStr)
        {
            AutoPipeMsg msgData = JsonConvert.DeserializeObject<AutoPipeMsg>(msgStr);
            DoKeyboardOper(msgData.KeyboardMsgs);
            DoRunCommand(msgData.RunCommand);
        }

        void DoKeyboardOper(List<KeyboardMsg> msgs)
        {
            foreach (KeyboardMsg msg in msgs)
            {
                WU.keybd_event_packed(msg.Key, msg.IsDown);
            }
        }

        void DoRunCommand(ScriptRunCommand msg)
        {
            if (msg == ScriptRunCommand.Undefined) return;
            if (string.IsNullOrEmpty(HotSpotScriptId)) return;

            var id = HotSpotScriptId;
            var scriptData = _scriptDatas[id];

            switch (msg)
            {
                case ScriptRunCommand.StartScript:
                    if (!scriptData.IsRunning)
                        StartScript(id);
                    break;
                case ScriptRunCommand.StopScript:
                    if (scriptData.IsRunning)
                        StopScript(id);
                    break;
                case ScriptRunCommand.TerminateScript:
                    if (!scriptData.IsEnd)
                        TerminateScript(id);
                    break;

            }
        }



        #endregion


        #region Send
        bool ThisFrameNeedSend;

        void SendPipeMsg()
        {
            if (!ThisFrameNeedSend)
                return;
            ThisFrameNeedSend = false;

            foreach (var unit in _clientMMFs.Values)
            {
                if (unit.Msg != null)
                {
                    SendPipeMsgSingle(unit.Client, unit.Msg);
                }
                unit.Msg = null;
            }
        }

        void SendPipeMsgSingle(MMFPeer client, AutoPipeMsg msg)
        {
            var json = JsonConvert.SerializeObject(msg);
            client.SendMessage(json);
        }

        void GetPipeUnit(string pipeName, out MMFPeer client, out AutoPipeMsg msg)
        {
            if (!_clientMMFs.TryGetValue(pipeName, out var unit))
            {
                client = new MMFPeer(pipeName, false, null);
                unit = new AutoMMFUnit(client, null);
                _clientMMFs[pipeName] = unit;
            }

            if (unit.Msg == null)
            {
                unit.Msg = new AutoPipeMsg();
            }

            client = unit.Client;
            msg = unit.Msg;
        }


        /// <summary>
        /// 可以扩展、可以重载
        /// </summary>
        public void AddPipeMsg(string pipeName, KeyboardMsg msg)
        {
            if (!Utils.IsAdmin) return;

            ThisFrameNeedSend = true;
            GetPipeUnit(pipeName, out _, out var pipeMsg);

            pipeMsg.KeyboardMsgs.Add(msg);
        }

        public void AddPipeMsg(string pipeName, ScriptRunCommand msg)
        {
            if (!Utils.IsAdmin) return;

            ThisFrameNeedSend = true;
            GetPipeUnit(pipeName, out _, out var pipeMsg);

            pipeMsg.RunCommand = msg;
        }




        #endregion
    }







    // public class AutoPipeUnit
    // {
    //     public PipelineServer Client;
    //     public AutoPipeMsg Msg;

    //     public AutoPipeUnit(PipelineServer client, AutoPipeMsg msg)
    //     {
    //         Client = client;
    //         Msg = msg;
    //     }
    // }


    // public partial class AutoScriptManager
    // {
    //     PipelineServer _serverPipe;
    //     Dictionary<string, AutoPipeUnit> _clientPipes;


    //     public void InitNamePipe()
    //     {
    //         return;

    //         // string domain = Environment.UserDomainName;  // 域名。没取，叫Desktop
    //         string userName = Environment.UserName;         // 登录用户名。在同台机子上是唯一的

    //         string pipeName = "";
    //         foreach (var k in Settings.PipeMapping)
    //             if (userName == k[1])
    //                 pipeName = k[0];

    //         if (pipeName != "")
    //         {
    //             _serverPipe = new PipelineServer(true, pipeName, OnMessage);
    //             _serverPipe.Start();
    //         }
    //         Settings.PipeName = pipeName;

    //         _clientPipes = new Dictionary<string, AutoPipeUnit>();


    //     }

    //     public void DestroyNamePipe()
    //     {
    //         return;

    //         if (_serverPipe != null)
    //         {
    //             _serverPipe.Close();
    //             _serverPipe = null;
    //         }

    //         foreach (var unit in _clientPipes.Values)
    //         {
    //             unit.Client.Close();
    //         }
    //         _clientPipes.Clear();
    //     }


    //     #region Listen

    //     void OnMessage(string msgStr)
    //     {
    //         AutoPipeMsg msgData = JsonConvert.DeserializeObject<AutoPipeMsg>(msgStr);
    //         DoKeyboardOper(msgData.KeyboardMsgs);
    //     }

    //     void DoKeyboardOper(List<KeyboardMsg> msgs)
    //     {
    //         foreach (KeyboardMsg msg in msgs)
    //         {
    //             WU.keybd_event_packed(msg.Key, msg.IsDown);
    //         }
    //     }


    //     #endregion




    //     #region Send
    //     bool ThisFrameNeedSend;

    //     void SendPipeMsg()
    //     {
    //         if (!ThisFrameNeedSend)
    //             return;
    //         ThisFrameNeedSend = false;

    //         foreach (var unit in _clientPipes.Values)
    //         {
    //             if (unit.Msg != null)
    //             {
    //                 SendPipeMsgSingle(unit.Client, unit.Msg);
    //             }
    //             unit.Msg = null;
    //         }

    //     }

    //     void SendPipeMsgSingle(PipelineServer client, AutoPipeMsg msg)
    //     {
    //         var json = JsonConvert.SerializeObject(msg);
    //         client.AddSendMsg(json);
    //     }



    //     void GetPipeUnit(string pipeName, out PipelineServer client, out AutoPipeMsg msg)
    //     {
    //         if (!_clientPipes.TryGetValue(pipeName, out var unit))
    //         {
    //             client = new PipelineServer(false, pipeName, null);
    //             client.Start();
    //             unit = new AutoPipeUnit(client, null);
    //             _clientPipes[pipeName] = unit;
    //         }

    //         if (unit.Msg == null)
    //         {
    //             unit.Msg = new AutoPipeMsg();
    //         }


    //         client = unit.Client;
    //         msg = unit.Msg;
    //     }


    //     /// <summary>
    //     /// 可以扩展、可以重载
    //     /// </summary>
    //     public void AddPipeMsg(string pipeName, KeyboardMsg msg)
    //     {
    //         ThisFrameNeedSend = true;
    //         GetPipeUnit(pipeName, out _, out var pipeMsg);

    //         pipeMsg.KeyboardMsgs.Add(msg);
    //     }



    //     #endregion
    // }
}