using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Memory-Mapped Files 双端
/// </summary>
public class MMFPeer
{

    private bool _is_server;
    private Action<string> _onMessage;
    private string _name;

    // 共享配置（两端必须相同）
    private string MAP_NAME = "Global\\Unity_MMF_Safe_Channel";
    private string EVENT_WRITE = "Global\\Unity_MMF_WriteEvent";
    private string EVENT_READ = "Global\\Unity_MMF_ReadEvent";

    private const int BUFFER_SIZE = 4096;
    private const int HEAD_SIZE = 4;
    private const int DATA_SIZE = BUFFER_SIZE - HEAD_SIZE;

    // 共享内存
    private MemoryMappedFile _mmf;
    private MemoryMappedViewAccessor _accessor;

    // 同步信号（无轮询关键）
    private EventWaitHandle _canWrite;              // _canRead / _canWrite = 保护两个 Unity 进程之间的冲突（UnityA ↔ UnityB）
    private EventWaitHandle _canRead;

    // 线程安全
    private readonly object _lock = new object();   // _lock = 保护自己 Unity 内部的多线程冲突（线程 A、线程 B）
    private bool _connected;

    public MMFPeer(string name, bool is_server, Action<string> onMessage)
    {
        _name = name;
        _is_server = is_server;
        _onMessage = onMessage;
        MAP_NAME = $"Global\\Unity_MMF_Safe_Channel_{name}";
        EVENT_WRITE = $"Global\\Unity_MMF_WriteEvent_{name}";
        EVENT_READ = $"Global\\Unity_MMF_ReadEvent_{name}";

        TryConnectOrCreate();
    }

    /// <summary>
    /// 这个间隔还是慢了。要是频率高于帧间隔，就会阻塞信息。
    /// </summary>
    public void OnUpdate()
    {
        if (!_connected)
            return;

        // 非轮询：等待信号，有消息才读
        lock (_lock)
        {
            try
            {
                // 检查是否有新消息（不阻塞）
                if (_canRead.WaitOne(0))
                {
                    if (ReadMessage(out var msg, out var len))
                    {
                        DU.LogWarning($"【收到】{_name}管道收到: {msg} 长度{len} 帧序: {Time.frameCount}");
                        _onMessage?.Invoke(msg);
                        _canWrite.Set();                        // 通知对方可以继续发
                    }
                }
            }
            catch
            {
                Disconnect();
            }
        }
    }



    #region 连接逻辑
    private void TryConnectOrCreate()
    {
        // 先尝试打开事件（比 MMF 更快）
        var isCreator = !EventWaitHandle.TryOpenExisting(EVENT_WRITE, out _canWrite);

        try
        {
            if (isCreator)
            {
                // 创建者：初始化 MMF + 事件
                CreateMMF();
                CreateEvents();
                DU.LogWarning($"【创建】{_name}管道");
            }
            else
            {
                // 连接者：打开 MMF + 事件
                _mmf = MemoryMappedFile.OpenExisting(MAP_NAME);
                _accessor = _mmf.CreateViewAccessor();
                EventWaitHandle.TryOpenExisting(EVENT_READ, out _canRead);
                DU.LogWarning($"【连接】{_name}管道");
            }

            _connected = true;
        }
        catch
        {
            Disconnect();
        }
    }

    private void CreateMMF()
    {
        var sec = new MemoryMappedFileSecurity();
        sec.AddAccessRule(new AccessRule<MemoryMappedFileRights>(
            new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            MemoryMappedFileRights.FullControl,
            AccessControlType.Allow));

        _mmf = MemoryMappedFile.CreateOrOpen(
            MAP_NAME, BUFFER_SIZE,
            MemoryMappedFileAccess.ReadWrite,
            MemoryMappedFileOptions.None, sec,
            HandleInheritability.None);

        _accessor = _mmf.CreateViewAccessor();
        _accessor.Write(0, 0); // 初始为空
    }

    private void CreateEvents()
    {
        var eventSec = new EventWaitHandleSecurity();
        eventSec.AddAccessRule(new EventWaitHandleAccessRule(
            new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            EventWaitHandleRights.FullControl,
            AccessControlType.Allow));

        _canWrite = new EventWaitHandle(true, EventResetMode.AutoReset, EVENT_WRITE, out _, eventSec);
        _canRead = new EventWaitHandle(false, EventResetMode.AutoReset, EVENT_READ, out _, eventSec);
    }


    #endregion

    #region 收发消息
    public void SendMessage(string text)
    {
        lock (_lock)
        {
            if (!_connected)
                return;

            try
            {
                byte[] data = Encoding.UTF8.GetBytes(text);
                if (data.Length > DATA_SIZE) return;

                // 等待发送许可（无自旋）
                if (!_canWrite.WaitOne(100)) return;

                _accessor.Write(0, data.Length);
                _accessor.WriteArray(HEAD_SIZE, data, 0, data.Length);

                _canRead.Set(); // 通知对方有新消息
            }
            catch
            {
                Disconnect();
            }
        }
    }

    private bool ReadMessage(out string message, out int len)
    {
        message = null;
        len = 0;
        if (_accessor == null) return false;

        len = _accessor.ReadInt32(0);
        if (len <= 0 || len > DATA_SIZE)
        {
            DU.LogError($"【MMFPeer】消息过长\n {message}");
            return false;
        }

        byte[] buffer = new byte[len];
        _accessor.ReadArray(HEAD_SIZE, buffer, 0, len);
        _accessor.Write(0, 0); // 清空标记

        message = Encoding.UTF8.GetString(buffer);
        return true;
    }
    #endregion

    #region 断开与释放
    private void Disconnect()
    {
        _connected = false;

        lock (_lock)
        {
            _accessor?.Dispose();
            _mmf?.Dispose();
            _canWrite?.Close();
            _canRead?.Close();

            _accessor = null;
            _mmf = null;
            _canWrite = null;
            _canRead = null;

            DU.LogWarning($"【断开】{_name}管道已销毁");
        }
    }

    public void OnDestroy()
    {
        Disconnect();
    }
    #endregion


}