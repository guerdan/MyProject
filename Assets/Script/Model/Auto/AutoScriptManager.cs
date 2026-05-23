

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using OpenCvSharp;
using Script.Framework;
using Script.UI.Components;
using Script.Util;
using Unity.VisualScripting;
using UnityEngine;


namespace Script.Model.Auto
{

    public enum ScriptLogType
    {
        Log,
        Warning,
        Error,
    }

    #region Settings

    /// <summary>
    /// 脚本配置
    /// </summary>
    [Serializable]
    public class AutoScriptSettings
    {
        [JsonProperty("end_index")]
        public int EndIndex = -1;

        [JsonProperty("id_dic")]   // <id, 简化路径>
        public Dictionary<string, string> _idDic = new Dictionary<string, string>();
        [JsonProperty("collections")]
        public List<string> Collections = new List<string>();
        [JsonProperty("recently_open")]       //按从旧到新
        public List<string[]> OpenRecent = new List<string[]>();

        [JsonProperty("pipe_mapping")]          // [0]:pipeName;[1]:userName
        public List<string[]> PipeMapping = new List<string[]>();

        [JsonIgnore]  // <id, 完整路径>
        public Dictionary<string, string> IdDic = new Dictionary<string, string>();

        [JsonIgnore]
        public string PipeName = "";

        // 序列化 额外工作
        public void Serialize()
        {
            _idDic.Clear();
            foreach (var kv in IdDic)
            {
                _idDic[kv.Key] = kv.Value.Substring(Application.streamingAssetsPath.Length + 1);
            }
        }

        // 反序列化 额外工作
        public void UnSerialize()
        {
            IdDic.Clear();
            foreach (var kv in _idDic)
            {
                IdDic[kv.Key] = $"{Application.streamingAssetsPath}/{kv.Value}";
            }

            if (PipeMapping.Count == 0)
            {
                for (int i = 0; i < 6; i++)
                    PipeMapping.Add(new string[2] { $"{i + 1}", "" });
            }

        }

        public Dictionary<string, string> GetPath2Id()
        {
            var result = new Dictionary<string, string>();
            foreach (var kv in IdDic)
            {
                result[kv.Value] = kv.Key;
            }
            return result;
        }

        public void AddOpenRecent(string id)
        {
            int old = OpenRecent.FindIndex((l) => l[0] == id);
            if (old > -1)
            {
                OpenRecent.RemoveAt(old);
            }

            if (OpenRecent.Count >= 5)
            {
                OpenRecent = OpenRecent.GetRange(1, 4);
            }

            var script = AutoScriptManager.Inst.GetScriptData(id);
            OpenRecent.Add(new string[] { id, script.Config.Name });

            AutoScriptManager.Inst.SaveAutoScriptSettings();
        }
    }

    #endregion
    #region Manager

    /// <summary>
    /// 总控。
    /// </summary>
    public partial class AutoScriptManager
    {
        private static AutoScriptManager _inst;
        public static AutoScriptManager Inst
        { get { if (_inst == null) _inst = new AutoScriptManager(); return _inst; } }
        public static string AutoScriptDirectoryPath = $"{Application.streamingAssetsPath}/Script";
        public static string AutoScriptSettingsPath = $"{Application.streamingAssetsPath}/Script/settings.json";
        public static string FolderIdStart = "folder-";
        public static string ScriptIdStart = "script-";
        public static string NodeIdStart = "node-";
        public static string LineIdStart = "line-";
        public event Action OnTick;                         //通知ui刷新
        public Action<string> OnChangeNodeStatus;           //通知ui刷新
        public Action OnChangeScriptStatus;                 //通知ui刷新
        public Action<ScriptLogType> OnMessageRefresh;      //通知ui刷新

        public bool InfoPanelFolded = true;
        public bool ScreenDrawDebug = false;
        public bool SaveMapCaptureStatus = false;
        public bool MapIconDebugStatus = false;

        public AutoScriptSettings Settings;


        public Dictionary<ScriptLogType, List<TextUIData>> LogDic    //debug打印日志
            = new Dictionary<ScriptLogType, List<TextUIData>>();


        /// <summary>
        /// 脚本目录.<id, (path,fileName)>
        /// </summary>
        private Dictionary<string, (string, string)> _scriptDirectory = new Dictionary<string, (string, string)>();
        /// <summary>
        /// 加载过的脚本.<id, Data>
        /// </summary>
        private Dictionary<string, AutoScriptData> _scriptDatas = new Dictionary<string, AutoScriptData>();


        public CVRect FrameCaptureRegion => _frameCaptureRegion;

        // 当前帧屏幕截屏
        public Mat FrameCaptureMat => _frameCaptureMat;
        public Vec4b[] FrameCaptureColor => _frameCaptureColor;

        private CVRect _frameCaptureRegion = default;       // 当前帧屏幕截屏范围
        private Mat _frameCaptureMat = null;                // 当前帧屏幕截屏
        private Vec4b[] _frameCaptureColor = null;          // 当前帧屏幕截屏
        public bool _saveCaptureToLocal = false;            // 是否保存截屏到本地
        public AutoScriptData _saveCaptureToLocal_script = null;     // 保存截屏到本地，提需求的脚本
        public string HotSpotScriptId;
        public bool ScreenDrawAllow;


        public void Init()
        {
            LoadScriptJsonDirectory();
            MethodParseUtil.InitDic();

            GameTimer.Inst.SetTimeOnce(this, () =>
            {
                // 写个顺序加载， 一帧只加载一个
                InitNamePipe();
                DU.RunWithTimer(() =>
                {
                    GameItemCfgManager.Inst.Init();
                }, "GameItemCfgManager.Init()");
                DU.RunWithTimer(() =>
                {
                    AffixCfgManager.Inst.Init();
                }, "AffixCfgManager.Init()");

            }, 1);
            // InitNamePipe();

            // 创建目录
            List<string> folders = new List<string>()
            {
              $"{Application.streamingAssetsPath}/Error",
            };
            foreach (var f in folders)
            {
                if (!Directory.Exists(f))
                    Directory.CreateDirectory(f);
            }

        }

        #region 脚本

        public AutoScriptData GetScriptData(string id)
        {
            if (!_scriptDirectory.TryGetValue(id, out (string, string) tuple))
            {
                // $"没有找到脚本 {id}"
                DU.LogError($"{SU.ErrorCode(1)}{id}");
                return null;
            }
            string path = tuple.Item1;
            if (!_scriptDatas.TryGetValue(id, out var data))
            {
                data = LoadScriptJson(path);
                data.Config.Id = id;                    // 防止不一致。复制粘贴的脚本有这个需求。
                data.Config.Name = tuple.Item2;

                _scriptDatas[id] = data;
            }

            return data;
        }

        public bool IsRuning(string id)
        {
            return _scriptDatas[id].IsRunning;
        }

        public void StartScript(string id)
        {
            var scriptData = _scriptDatas[id];
            if (scriptData.IsEnd)
            {
                scriptData.StartScript();
                ChangeHotScript(id);
            }
            else
            {
                scriptData.ContinueScript();
            }

            OnChangeScriptStatus?.Invoke();
        }


        public void StopScript(string id)
        {
            _scriptDatas[id].StopScript();
            OnChangeScriptStatus?.Invoke();
        }
        public void TerminateScript(string id)
        {
            _scriptDatas[id].TerminateScript();
            OnChangeScriptStatus?.Invoke();
        }

        public void OnUpdate(float deltaTime)
        {
            ScreenDrawAllow = true;
            // 让FrameCapture获取最新的帧截屏
            ClearFrameCapture();

            List<AutoScriptData> running_scripts = new List<AutoScriptData>();

            foreach (var script in _scriptDatas.Values)
                if (script.IsRunning)
                    running_scripts.Add(script);


            // Node执行Update
            foreach (var script in running_scripts)
                script.DoUpdate(deltaTime);
            // 预更新
            foreach (var script in running_scripts)
                script.BeforeAction(deltaTime);
            // 按需把AutoScriptData.Update切分成两个部分，因为中间要统计截图的范围
            _frameCaptureRegion = CalculateFrameCaptureRegion();
            RefreshFrameCapture();

            // 更新, 主要执行Action
            foreach (var script in running_scripts)
            {
                script.DoAction(deltaTime);
            }

            OnUpdateMMF();
            SendPipeMsg();
            // 通知UI刷新
            OnTick?.Invoke();
        }

        public void ChangeHotScript(string id)
        {
            if (HotSpotScriptId == null)
                HotSpotScriptId = id;
            else
            {
                var scriptData = _scriptDatas[HotSpotScriptId];
                if (scriptData.IsEnd)
                {
                    HotSpotScriptId = id;
                }
            }

        }

        public void RenameScript(string id, string newName)
        {
            if (!_scriptDirectory.TryGetValue(id, out (string, string) tuple))
            {
                // $"没有找到脚本 {id}"
                DU.LogError($"{SU.ErrorCode(1)}{id}");
                return;
            }
            string oldPath = tuple.Item1;
            string dir = Path.GetDirectoryName(oldPath).Replace("\\", "/");
            string newPath = $"{dir}/{newName}.json";

            // 重命名文件
            if (File.Exists(newPath))
            {
                // $"脚本重命名失败，文件已存在 {newPath}，防止不小心覆盖"
                DU.LogError($"{SU.ErrorCode(2)}{newPath}");
                return;
            }
            File.Move(oldPath, newPath);

            // 更新目录
            _scriptDirectory[id] = (newPath, newName);
            Settings.IdDic[id] = newPath;
            SaveAutoScriptSettings();

            // 更新数据
            var scriptData = _scriptDatas[id];
            scriptData.Config.Name = newName;

            // $"脚本重命名成功"
            DU.Log($"good");
        }

        /// <summary>
        /// 创建脚本。返回：成功否，脚本id
        /// </summary>
        public bool CreateScript(string name, string dir, out string id)
        {
            id = AutoScriptConfig.IdStart + (Settings.EndIndex + 1);
            var path = $"{Application.streamingAssetsPath}/{dir}/{name}.json";
            // 重命名文件
            if (File.Exists(path))
            {
                // $"创建脚本失败，文件已存在 {path}"
                DU.LogError($"{SU.ErrorCode(1)}{path}");
                return false;
            }

            Settings.EndIndex++;

            Settings.IdDic[id] = path;
            SaveAutoScriptSettings();

            _scriptDirectory[id] = (path, name);

            var scriptData = new AutoScriptData(new AutoScriptConfig
            {
                Id = id,
                Name = name,
            }, this);
            _scriptDatas[id] = scriptData;
            SaveScript(id);

            return true;
        }

        // 编辑界面 每两分钟保存一次，并且OnDisable时保存。
        public void SaveScript(string id)
        {
            var name = "";
            Action action = () =>
            {
                var scriptData = _scriptDatas[id];
                name = scriptData.Config.Name;
                var json = scriptData.GetSaveJson();

                // 保存到文件。
                string path = _scriptDirectory[id].Item1;
                File.WriteAllText(path, json);
            };

            var ms = DU.RunWithTimer(action);
            // DU.Log($"{name} success，耗时{ms}ms");
        }


        #region 截屏


        public void RefreshFrameCapture()
        {
            if (_frameCaptureMat == null && _frameCaptureRegion.w > 0)
            {
                //Bitmap 是一个托管对象，但它使用了非托管资源（如 GDI+ 图形资源）。
                // 因此，Bitmap 不会自动释放其非托管资源，需要手动释放。
                Bitmap bitmap = null;
                Action action = () =>
                {
                    // 在最后才给IU接口适配。IU接口的坐标系是Y轴朝下，而脚本是Y轴朝上
                    var region = IU.ReverseYAndChange(_frameCaptureRegion);
                    bitmap = WU.CaptureWindow(region);
                };
                Action action1 = () =>
                {
                    IU.BitmapToMat(bitmap, out _frameCaptureMat, out _frameCaptureColor);
                    // _frameCaptureColor = IU.BitmapToColor32(bitmap);
                    // _frameCaptureMat.GetArray(out Color32[] _frameCaptureColor);
                };
                Action action2 = () =>
                {
                    SaveCaptureToLocal(bitmap);
                };

                var ms0 = DU.RunWithTimer(action);
                var ms1 = DU.RunWithTimer(action1);

                foreach (var script in _scriptDatas.Values)
                {
                    if (!script.IsRunning) continue;
                    script.Record("CaptureWindow", ms0, false);
                    script.Record("BitmapToMat", ms1, false);
                }

                if (_saveCaptureToLocal) DU.RunWithTimer(action2);    // 0ms  "SaveCaptureToLocal"

                bitmap.Dispose();
            }
        }


        // 计算所有脚本中，所有模版匹配节点的区域的并集
        CVRect CalculateFrameCaptureRegion()
        {
            List<CVRect> regions = new List<CVRect>();
            foreach (var script in _scriptDatas.Values)
            {
                foreach (var node in script.ActiveNodes)
                    if (node.CanAction && node.NodeType == NodeType.CaptureOper)
                    {
                        var n = node as CaptureOperNode;
                        regions.Add(CVRect.ConvertV4Bigger(n.TotalRegion));
                        if (n.SaveCaptureToLocal)
                        {
                            _saveCaptureToLocal = true;
                            _saveCaptureToLocal_script = script;
                        }
                    }

            }

            var boundingBox = RecogUtil.CalBoundingBox(regions);
            return boundingBox;
        }

        public void UseCapture(Vector4 region, Action<Mat> action)
        {
            if (FrameCaptureMat == null)
                throw new Exception($"{SU.ErrorCode(1)}");

            var total_rect = FrameCaptureRegion;
            CVRect use = CVRect.ConvertV4Bigger(region);
            Mat use_mat = FrameCaptureMat;

            if (total_rect != use)
            {
                var h = total_rect.y + total_rect.h - use.y - use.h;
                use_mat = FrameCaptureMat.SubMat(new OpenCvSharp.Rect
                    (use.x - total_rect.x, h, use.w, use.h));

            }

            action(use_mat);

            if (total_rect != use)
                use_mat.Dispose();
        }

        public Color32Image GetCaptureImg(CVRect region)
        {
            CVRect total = _frameCaptureRegion;
            Vec4b[] total_v4b = _frameCaptureColor;
            int w = region.w, h = region.h;

            var xs = region.x - total.x;
            var ys = region.y - total.y;

            Color32[] colors = new Color32[w * h];
            Color32Image img = new Color32Image(colors, w, h);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var index = (total.h - 1 - (y + ys)) * total.w + x + xs;
                    var data = total_v4b[index];
                    colors[y * w + x] = new Color32(data.Item2, data.Item1, data.Item0, data.Item3);
                }

            return img;
        }


        /// <summary>
        /// 如果设置了保存截图，那么就保存。
        /// 
        /// </summary>
        void SaveCaptureToLocal(Bitmap bitmap)
        {
            var script = _saveCaptureToLocal_script;
            var folder = script.GetCapturePath();

            script.Config.CaptureEndIndex++;
            var name = $"{script.Config.CaptureEndIndex}";
            var path = $"{folder}/{name}.png";
            IU.SaveBitmap(bitmap, path);
            // bug 没有存Config。  结束脚本时存一下
            script.NeedSaveWhenEnd = true;
        }

        void ClearFrameCapture()
        {
            if (_frameCaptureMat != null)
            {
                _frameCaptureMat.Dispose();
                _frameCaptureMat = null;
            }

            _saveCaptureToLocal = false;
        }

        #endregion

        #region 加载
        // StreamingAssets/Script 目录下存所有的json脚本
        // StreamingAssets/Script/config.json 是此类型文件的配置

        public void LoadScriptJsonDirectory()
        {
            // 是否有文件夹
            if (!Directory.Exists(AutoScriptDirectoryPath))
            {
                Directory.CreateDirectory(AutoScriptDirectoryPath);
            }
            // 加载settings
            Settings = LoadAutoScriptSettings();
            var path2id = Settings.GetPath2Id();

            // 遍历目录下所有文件的名    todo  封装同步两个列表的方法
            string[] files = Directory.GetFiles(AutoScriptDirectoryPath, "*.json", SearchOption.AllDirectories);
            var exist = new HashSet<string>();
            foreach (string _ in files)
            {
                if (_.EndsWith("settings.json"))
                    continue;
                string path = _.Replace("\\", "/");
                exist.Add(path);
            }


            // 删无效的
            foreach (var kv in path2id)
                if (!exist.Contains(kv.Key))
                {
                    Settings.IdDic.Remove(kv.Value);
                    Settings.Collections.Remove(kv.Value);
                    Settings.OpenRecent.RemoveAll((l) => l[0] == kv.Value);
                }

            // 更新
            foreach (string path in exist)
            {
                if (path2id.TryGetValue(path, out string id))
                {
                    _scriptDirectory[id] = (path, Path.GetFileNameWithoutExtension(path));
                }
                else
                {
                    // 新增的脚本
                    Settings.EndIndex++;
                    id = AutoScriptConfig.IdStart + Settings.EndIndex;
                    Settings.IdDic[id] = path;
                    _scriptDirectory[id] = (path, Path.GetFileNameWithoutExtension(path));
                }
            }

            // 保存settings
            SaveAutoScriptSettings();
        }

        public AutoScriptSettings LoadAutoScriptSettings()
        {
            if (File.Exists(AutoScriptSettingsPath))
            {
                string json = File.ReadAllText(AutoScriptSettingsPath);
                var autoScriptConfig = JsonConvert.DeserializeObject<AutoScriptSettings>(json);
                autoScriptConfig.UnSerialize();
                return autoScriptConfig;
            }
            else
            {
                return new AutoScriptSettings();
            }
        }

        public void SaveAutoScriptSettings()
        {
            Settings.Serialize();
            var json = JsonConvert.SerializeObject(Settings);
            File.WriteAllText(AutoScriptSettingsPath, json);
        }


        public AutoScriptData LoadScriptJson(string path)
        {
            string json = File.ReadAllText(path);
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };

            var autoScriptConfig = JsonConvert.DeserializeObject<AutoScriptConfig>(json, settings);
            var scriptData = new AutoScriptData(autoScriptConfig, this);
            return scriptData;
        }


        #endregion

        #region 搜索

        public List<string> BrowseDirectory(string path)
        {
            // if (string.IsNullOrEmpty(path))
            //     path = "Script";

            List<string> r = new List<string>();

            path = $"{Application.streamingAssetsPath}/{path}";

            // 获取所有子文件夹
            string[] directories = Directory.GetDirectories(path);
            foreach (string _ in directories)
            {
                var dir = _.Replace("\\", "/");
                r.Add(FolderIdStart + dir);
            }

            // 获取所有文件
            var path2id = Settings.GetPath2Id();

            string[] files = Directory.GetFiles(path, "*.json");
            foreach (string _ in files)
            {
                var file_path = _.Replace("\\", "/");
                if (path2id.ContainsKey(file_path))
                    r.Add(path2id[file_path]);
            }

            return r;
        }


        public List<string> SearchScripts(string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
                return new List<string>();

            List<string> r = new List<string>();
            foreach (var kv in _scriptDirectory)
            {
                if (kv.Value.Item2.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    r.Add(kv.Key);
                }
            }
            Utils.CommonSort(r);
            return r;
        }

        #endregion
        #endregion

        #region 画布

        List<BaseNodeData> _copyCanvasData;
        public void CopyCanvas(AutoScriptData targetScript, string canvas_id)
        {
            _copyCanvasData = new List<BaseNodeData>();
            List<string> node_ids = targetScript.Canvas2Node[canvas_id];


            // 深拷贝出来一份
            foreach (var node_id in node_ids)
            {
                var node = targetScript.NodeDatas[node_id];
                BaseNodeData copy = AutoScriptData.CreateNodeRaw(node.NodeType);
                copy.Id = node.Id;
                copy.Pos = node.Pos;
                copy.Links = new Dictionary<string, NodeLinkInfo>(node.Links);
                copy.Copy(node);
                copy.Serialize();

                _copyCanvasData.Add(copy);
            }

        }

        /// <summary>
        /// 复制粘贴只持续一次效用
        /// </summary>
        public void PasteCanvas(AutoScriptData targetScript, string canvas_id)
        {
            if (_copyCanvasData == null)
                return;

            var id_start = targetScript.Config.EndIndex + 1;
            targetScript.Config.EndIndex += _copyCanvasData.Count;


            var id_map = new Dictionary<string, string>();
            for (int i = 0; i < _copyCanvasData.Count; i++)
            {
                var node = _copyCanvasData[i];
                var id = $"{i + id_start}";
                id_map.Add(node.Id, id);
                node.Id = id;
                node.CanvasId = canvas_id;
                targetScript.NodeDatas.Add(node.Id, node);
            }

            // 调整连线id
            // 
            for (int i = 0; i < _copyCanvasData.Count; i++)
            {
                var node = _copyCanvasData[i];

                List<NodeLinkInfo> new_links = new List<NodeLinkInfo>();
                for (int k = 0; k < node.Raw_Links.Count; k++)
                {
                    var link = node.Raw_Links[k];
                    if (id_map.TryGetValue(link.OtherId, out string new_id))
                    {
                        link.OtherId = new_id;
                        new_links.Add(link);
                    }
                }

                node.Raw_Links = new_links;
            }

            // 初始化, 需要分开
            for (int i = 0; i < _copyCanvasData.Count; i++)
            {
                var node = _copyCanvasData[i];
                node.UnSerialize();
            }
            for (int i = 0; i < _copyCanvasData.Count; i++)
            {
                var node = _copyCanvasData[i];
                node.Init(targetScript);
            }
            for (int i = 0; i < _copyCanvasData.Count; i++)
            {
                var node = _copyCanvasData[i];
                node.AfterInit();
            }

            _copyCanvasData = null;
        }

        #endregion

        #region 节点

        public BaseNodeData GetNode(AutoScriptData scriptData, string id)
        {
            return scriptData.NodeDatas[id];
        }

        public BaseNodeData CreateNode(AutoScriptData scriptData, string canvas_id, Vector2 pos, NodeType type, string id = null)
        {
            var node = scriptData.CreateNode(canvas_id, pos, type, id);
            return node;
        }
        public BaseNodeData CopyNode(AutoScriptData scriptData, string canvas_id, Vector2 pos, string target_id)
        {
            var node = scriptData.CopyNode(canvas_id, pos, target_id);
            return node;
        }

        public void DeleteNode(AutoScriptData scriptData, string id)
        {
            scriptData.DeleteNode(id);
        }

        // 有些操作至少要一帧
        public float GetNodeMinDalay(NodeType type)
        {
            switch (type)
            {
                case NodeType.CaptureOper:
                case NodeType.MouseOper:
                case NodeType.KeyBoardOper:
                    return Utils.MinFrameTime;
                default:
                    return 0;
            }
        }

        #endregion


        #region 线段

        public void AddLine(AutoScriptData scriptData, string fromId, string toId,
                            NodeDoor from_door, NodeDoor to_door)
        {
            var fromNode = scriptData.NodeDatas[fromId];
            var toNode = scriptData.NodeDatas[toId];

            fromNode.AddLink(from_door, toId, to_door);
            toNode.AddLink(to_door, fromId, from_door);

            fromNode.RefreshLineIds();
            toNode.RefreshLineIds();
        }

        public void DeleteLine(AutoScriptData scriptData, string fromId, string toId)
        {
            var fromNode = GetNode(scriptData, fromId);
            var toNode = GetNode(scriptData, toId);

            var info = fromNode.Links[toId];
            fromNode.RemoveLink(info.SelfDoor, toId);
            toNode.RemoveLink(info.OtherDoor, fromId);

            scriptData.RefreshSlot(fromNode);
            scriptData.RefreshSlot(toNode);

            fromNode.RefreshLineIds();
            toNode.RefreshLineIds();
        }



        public HashSet<string> GetLinesByNode(AutoScriptData scriptData, string id)
        {
            BaseNodeData node = GetNode(scriptData, id);
            return node.LineIds;
        }

        #endregion

        #region 其他

        /// <summary>
        /// 每个分类上限100，达200时清理。
        /// </summary>
        public void AddLog(ScriptLogType type, string content)
        {
            if (!LogDic.TryGetValue(type, out var list))
            {
                list = new List<TextUIData>();
                LogDic[type] = list;
            }

            if (list.Count >= 200)
            {
                list.RemoveRange(0, 100);
            }

            string str = $"[{DateTime.Now.ToString("HH:mm:ss")}]{Utils.SpaceStr}{content}";
            list.Add(new TextUIData(str));

            OnMessageRefresh?.Invoke(type);
        }
        public void OnDestroy()
        {
            DestroyNamePipe();
        }


        public string GetCapturePath()
        {
            return $"{Application.streamingAssetsPath}/Capture";
        }

        #endregion
    }



    #endregion
}