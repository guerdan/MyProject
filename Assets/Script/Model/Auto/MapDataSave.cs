
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using OpenCvSharp;
using Script.Util;
using UnityEngine;

namespace Script.Model.Auto
{
    public partial class MapData
    {


        List<(Vector2Int, PixType[,])> _sim_modify = new List<(Vector2Int, PixType[,])>();


        // void SaveErrorSmallMap(string name)
        // {
        //     if (_SaveErrorSmallMap_times >= 3)
        //         return;

        //     _SaveErrorSmallMap_times++;

        //     var folder = $"{AutoScriptManager.Inst.GetCapturePath()}/Error";
        //     if (!Directory.Exists(folder))
        //         Directory.CreateDirectory(folder);

        //     var colors = _small_map_colors;
        //     colors = IU.Color32ReverseYAxis(colors, _rectW);
        //     var bytes = IU.Color32ToByteWithoutAlpha(colors);
        //     var date = DateTime.Now;
        //     var path2 = $"{folder}/[{date.Hour}-{date.Minute}-{date.Second}][{name}].png";
        //     using (Mat mat = Mat.FromPixelData(_rectH, _rectW, MatType.CV_8UC3, bytes))
        //     {
        //         IU.SaveMat(mat, path2);
        //     }
        // }


        #region Save
        readonly Color32 color_gray = new Color32(80, 80, 80, 255);                 // 灰色
        readonly Color32 color_white = new Color32(240, 240, 240, 255);             // 边界色
        readonly Color32 color_white_unconfirm = new Color32(240, 180, 180, 255);   // 边界色，未确定
        readonly Color32 color_dark = new Color32(20, 20, 20, 255);                 // 空地色
        readonly Color32 color_dark_unconfirm = new Color32(90, 50, 50, 255);       // 空地色，未确定  
        readonly Color32 color_blue = new Color32(0, 0, 255, 255);
        readonly Color32 color_orange_dark = new Color32(180, 80, 0, 255);
        readonly Color32 color_red = new Color32(255, 0, 0, 255);
        readonly Color32 color_green = new Color32(20, 255, 20, 255);
        readonly Color32 color_green_blue = new Color32(20, 255, 255, 255);
        readonly Color32 color_yellow = new Color32(240, 240, 0, 255);

        public Color32[] GetColorsBuffer(int w, int h)
        {
            Color32[] result = null;
            if (_colorsBuffer.Item1 == w && _colorsBuffer.Item2 == h)
            {
                result = _colorsBuffer.Item3;
            }
            else
            {
                result = new Color32[w * h];
                _colorsBuffer = (w, h, result);
            }

            return result;
        }
        public void Save(string path)
        {
            Color32[] colors = GetImageMap0();
            byte[] bytes = IU.Color32ToByteWithoutAlpha(colors);
            using (Mat mat = Mat.FromPixelData(_h, _w, MatType.CV_8UC3, bytes))
            {
                IU.SaveMat(mat, path);
            }


            colors = GetDebugImage_Grid();
            bytes = IU.Color32ToByteWithoutAlpha(colors);
            var path2 = path.Substring(0, path.Length - 4) + "_grid.png";
            using (Mat mat = Mat.FromPixelData(_h, _w, MatType.CV_8UC3, bytes))
            {
                IU.SaveMat(mat, path2);
            }

            PrintResult();
        }


        public void PrintResult()
        {
            var l = GetPrintResult();
            var str = "";
            foreach (var s in l)
                str += $"{s}  ";
            DU.LogWarning(str);
        }
        public List<string> GetPrintResult()
        {
            var result = new List<string>();

            var _grid = _gridData._grid;

            int big_cell_active = 0;
            int fog_cell = 0;
            int w = _grid.GetLength(0);
            int h = _grid.GetLength(1);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var data = _grid[x, y];
                    if (data != null)
                    {
                        if (data.HasFog)
                            fog_cell++;
                        if (data.Type != BigCellType.UnInit)
                            big_cell_active++;
                    }
                }


            int build_count = _gridData.BuildCellTimes;
            int big_cell_multi = _gridData.MultiEmptyCellCount;

            result.Add($"大格子重建次数 {build_count - big_cell_active}；构造次数 {build_count}");
            result.Add($"Fog数 {fog_cell}");

            if (big_cell_multi > 0)
            {
                result.Add($"多侧空地数 {big_cell_multi}");
            }

            if (_gridData.ObstacleFill2EmptyCount > 0)
            {
                result.Add($"障碍填充转空地 {_gridData.ObstacleFill2EmptyCount}个 ");
            }

            return result;
        }

        #region DrawIcon
        /// <summary>
        /// 把icon_pos绘制到colors上
        /// </summary>
        public void DrawIcon(Color32Image colors, Vector2Int offset)
        {
            for (int type = 0; type < _icon_datas.Length; type++)
            {
                DrawIcon(colors, offset, (MapIconType)type);
            }
        }

        public void DrawIcon(Color32Image colors, Vector2Int offset, MapIconType type)
        {

            var icon_data = _icon_datas[(int)type];
            if (icon_data == null)
                return;

            var white = new Color32(255, 255, 255, 255);
            var black = new Color32(0, 0, 0, 255);

            List<Vector2Int> pos_list = new List<Vector2Int>();
            bool use_history = false;
            if (type == MapIconType.P1 || type == MapIconType.P2 || type == MapIconType.TeamP1)
            {
                var pos = FindPlayerPosAndHistory(type, out use_history);
                if (pos.x > 0)
                    pos_list.Add(pos);
            }
            else
            {
                pos_list = icon_data.InstList;
            }


            var color = icon_data.DrawColor;
            var rPos = icon_data.RecogPos;
            var size = icon_data.Size;
            foreach (var p in pos_list)
            {
                var start = p - offset - rPos;
                int xs = start.x, xe = start.x + size.x, ys = start.y, ye = start.y + size.y;

                for (int y = ys; y < ye; y++)
                    for (int x = xs; x < xe; x++)
                    {
                        if (x < 0 || x >= colors.W || y < 0 || y >= colors.H)
                            continue;
                        var index = y * colors.W + x;
                        colors.Colors[index] = color;
                    }
            }


            // 图层关系：识别点在Icon背景上面
            foreach (var p in pos_list)
            {
                var x = p.x - offset.x;
                var y = p.y - offset.y;
                if (x < 0 || x >= colors.W || y < 0 || y >= colors.H)
                    continue;
                var index = y * colors.W + x;
                colors.Colors[index] = use_history ? black : white;
            }
        }




        #endregion


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Color32 GetColor(PixType pix, bool confirm)
        {
            Color32 color = color_gray;
            if (pix == PixType.Empty)
                if (confirm) color = color_dark; else color = color_dark_unconfirm;
            if (pix == PixType.ObstacleEdge)
                if (confirm) color = color_white; else color = color_white_unconfirm;
            if (pix == PixType.Fog) color = color_blue;
            if (pix == PixType.ObstacleByBig) color = color_orange_dark;
            return color;
        }

        #region Draw map


        /// <summary>
        /// 输出可以直接喂给 Unity的Texture，而对于Bitmap需要翻转Y轴
        /// </summary>
        public Color32[] GetImageMap0(bool show_Item = true)
        {
            Color32[] colors = GetColorsBuffer(_w, _h);
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    var pix = _map[x + _range_zero.x, y + _range_zero.y];
                    var confirm = _confirm_map[x + _range_zero.x, y + _range_zero.y];
                    int index = y * _w + x;
                    Color32 color = GetColor(pix, confirm);

                    colors[index] = color;
                }

            var record = _pixRecord[PixRecordType.ObstacleEdgeOfLight];
            foreach (var p in record)
            {
                int x = p.x - _range_zero.x, y = p.y - _range_zero.y, index = y * _w + x;
                colors[index] = color_green;
            }


            var img = new Color32Image(colors, _w, _h);


            if (show_Item)
                DrawIcon(img, _range_zero);


            var record2 = _pixRecord[PixRecordType.ObstacleEdgeGapTotal];
            foreach (var p in record2)
            {
                int x = p.x - _xRange.x, y = p.y - _yRange.x, index = y * _w + x;
                colors[index] = color_green;
            }

            return colors;
        }

        #endregion
        /// <summary>
        /// 固定大小，超出实际地图的部分补灰色
        /// </summary>
        public Color32[] GetDebugImage_Map0Following(Vector2Int size, out Vector2Int start_pos)
        {
            var w = size.x;
            var h = size.y;
            Color32[] colors = GetColorsBuffer(w, h);
            start_pos = new Vector2Int(_small_map_pos.x + 100 - w / 2, _small_map_pos.y + 100 - h / 2);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var pos = new Vector2Int(x + start_pos.x, y + start_pos.y);
                    Color32 color = color_gray;

                    if (pos.x >= _xRange.x && pos.x <= _xRange.y
                        && pos.y >= _yRange.x && pos.y <= _yRange.y)
                    {
                        var pix = _map[pos.x, pos.y];
                        var confirm = _confirm_map[pos.x, pos.y];
                        color = GetColor(pix, confirm);
                    }

                    int index = y * w + x;
                    colors[index] = color;
                }

            return colors;
        }

        public void DrawPlayerIcon(Color32Image img, Vector2Int offset, MapIconType type)
        {
            if (offset.x <= 0)
                return;
            Color32 draw_color = default;
            switch (type)
            {
                case MapIconType.P1:
                    draw_color = new Color32(255, 125, 0, 255);
                    break;
                case MapIconType.P2:
                case MapIconType.TeamP1:
                    draw_color = new Color32(45, 134, 255, 255);
                    break;
            }
            foreach (var off in playerIconDraw)
                img.Colors[offset.x + off.x + (offset.y + off.y) * img.W] = draw_color;
        }

        public Color32[] GetImageLightMap()
        {
            Color32[] colors = GetColorsBuffer(_w, _h);
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    var pix = _light_map[x + _xRange.x, y + _yRange.x];
                    int index = y * _w + x;
                    Color32 color = color_gray;
                    if (pix.LightType == LightType.Light) color = new Color32(200, 200, 200, 255);

                    colors[index] = color;
                }

            if (_light_count == 0 && P1PosRecord.Count >= 9)
            {
                var light_start = _light_range[0];
                var tLen = _light_cal_map.GetLength(0);

                for (int y = 0; y < tLen; y++)
                    for (int x = 0; x < tLen; x++)
                    {
                        var pix = _light_cal_map[x, y];
                        int tx = x + light_start.x - _xRange.x;
                        int ty = y + light_start.y - _yRange.x;
                        int index = ty * _w + tx;

                        if (pix == LightType.Fresh) colors[index] = new Color32(130, 180, 130, 255);
                        if (pix == LightType.FreshEdge) colors[index] = color_blue;
                        if (pix == LightType.FreshVisited) colors[index] = new Color32(130, 130, 130, 255);
                    }

            }
            var record = _pixRecord[PixRecordType.ObstacleEdgeOfLight];
            foreach (var p in record)
            {
                int x = p.x - _xRange.x, y = p.y - _yRange.x, index = y * _w + x;
                colors[index] = color_red;
            }

            return colors;
        }


        public Color32[] GetDebugImage_SmallMapOrigin()
        {
            return _small_map_colors;
        }



        #region Draw_Small_map

        public enum SmallMapDebugType
        {
            Normal,     // 普通
            Detail,     // 详细，带上Icon
            Raw,      // 在空地遍历之前
            Error,      // 断连的第一张图
        }

        public Color32[] GetDebugImage_SmallMap(SmallMapDebugType type)
        {
            var len = _small_map.GetLength(0);
            Color32[] colors = GetColorsBuffer(len, len);
            PixType[,] select_map = null;
            if (type == SmallMapDebugType.Normal || type == SmallMapDebugType.Detail)
                select_map = _small_map;
            else if (type == SmallMapDebugType.Raw)
                select_map = _debug_small_map;
            else if (type == SmallMapDebugType.Error)
                select_map = _debug_error_small_map != null ? _debug_error_small_map : _small_map;


            for (int y = 0; y < len; y++)
                for (int x = 0; x < len; x++)
                {
                    var pix = select_map[x, y];
                    int index = y * len + x;

                    Color32 color = color_gray;
                    if (pix == PixType.EmptyTemp) color = color_dark_unconfirm;
                    if (pix == PixType.Empty) color = color_dark;
                    if (pix == PixType.ObstacleEdge) color = color_white;
                    if (pix == PixType.Fog) color = color_blue;
                    if (pix == PixType.FogArea) color = new Color32(0, 0, 180, 255);

                    colors[index] = color;
                }

            if (type == SmallMapDebugType.Normal || type == SmallMapDebugType.Raw)
            {
                var record3 = _pixRecord[PixRecordType.ObstacleEdgeGapTotal];
                foreach (var p in record3)
                {
                    int x = p.x - _small_map_pos.x, y = p.y - _small_map_pos.y, index = y * len + x;
                    if (x >= 0 && x < len && y >= 0 && y < len)
                        colors[index] = color_white_unconfirm;
                }

                var record1 = _pixRecord[PixRecordType.ObstacleEdgeEndPoint];
                foreach (var p in record1)
                {
                    int x = p.x - _small_map_pos.x, y = p.y - _small_map_pos.y, index = y * len + x;
                    colors[index] = color_red;
                }

                var record2 = _pixRecord[PixRecordType.ObstacleEdgeGap];
                foreach (var p in record2)
                {
                    int x = p.x - _small_map_pos.x, y = p.y - _small_map_pos.y, index = y * len + x;
                    colors[index] = color_green;
                }

            }

            // 画一下P1的行动轨迹
            if (type == SmallMapDebugType.Raw)
            {
                var list = GetP1ArriveList();
                foreach (var p in list)
                {
                    var sx = p.x - _small_map_pos.x;
                    var sy = p.y - _small_map_pos.y;
                    if (sx > 5 && sx < 195 && sy > 5 && sy < 195)
                    {
                        for (int y = sy - 1; y <= sy + 1; y++)
                            for (int x = sx - 1; x <= sx + 1; x++)
                            {
                                int index = y * len + x;
                                colors[index] = new Color32(255, 125, 0, 255);
                            }
                    }
                }
            }

            if (type == SmallMapDebugType.Normal)
            {
                var img = new Color32Image(colors, len, len);


                var icon_data = GetIconData(MapIconType.P1);
                if (icon_data.AddList.Count > 0)
                {
                    var p1_pos = icon_data.AddList[0];


                    var sectors = RecogUtil.GetDivinedSectors16();
                    for (int i = 0; i < sectors.Count; i++)
                    {
                        var sector = sectors[i];
                        bool has_fog = false;
                        for (int j = 0; j < sector.Count; j++)
                        {
                            var p = sector[j] + p1_pos;
                            if (p.x < 0 || p.x >= _rectW || p.y < 0 || p.y >= _rectH)
                                continue;

                            var data = select_map[p.x, p.y];
                            if (data == PixType.Fog || data == PixType.FogArea)
                            {
                                has_fog = true;
                                break;
                            }
                        }
                        
                        if (has_fog)
                            for (int j = 0; j < sector.Count; j++)
                            {
                                var p = sector[j] + p1_pos;
                                if (p.x < 0 || p.x > 199 || p.y < 0 || p.y > 199)
                                    continue;
                                var pix = select_map[p.x, p.y];
                                int index = p.y * len + p.x;
                                if (pix == PixType.Empty) colors[index] = color_dark_unconfirm;
                                if (pix == PixType.ObstacleEdge) colors[index] = color_white_unconfirm;
                            }
                    }
                }

                DrawIcon(img, _small_map_pos, MapIconType.P1);      // 匹配失败，就不会画P1
            }


            return colors;
        }


        #endregion

        public Color32[] GetDebugImage_JudgeMap()
        {

            Color32[] colors = GetColorsBuffer(_w, _h);

            var jLen = _judge_map.GetLength(0);
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    var px = x + _xRange.x;
                    var py = y + _yRange.x;
                    var pix = _map[px, py];

                    Color32 color = color_gray;
                    if (px >= _judge_map_pos.x && px < _judge_map_pos.x + jLen
                        && py >= _judge_map_pos.y && py < _judge_map_pos.y + jLen)
                    {
                        if (pix == PixType.Fog)
                            color = color_blue;
                        else
                        {
                            var jData = _judge_map[px - _judge_map_pos.x, py - _judge_map_pos.y];
                            // var r = jData.CheckResult();
                            if (pix == PixType.Empty) color = color_dark;
                            if (pix == PixType.ObstacleEdge) color = color_white;
                        }
                    }

                    int index = y * _w + x;
                    colors[index] = color;
                }

            List<Vector2Int> list = new List<Vector2Int>();

            // 红线
            // 
            for (int y = -1; y < jLen + 1; y++)
            {
                list.Add(new Vector2Int(-1, y));
                list.Add(new Vector2Int(jLen, y));
            }
            for (int x = -1; x < jLen + 1; x++)
            {
                list.Add(new Vector2Int(x, -1));
                list.Add(new Vector2Int(x, jLen));
            }

            foreach (var pos in list)
            {
                int px = pos.x + _judge_map_pos.x - _xRange.x;
                int py = pos.y + _judge_map_pos.y - _yRange.x;
                if (px >= 0 && px < _w && py >= 0 && py < _h)
                {
                    int index = py * _w + px;
                    colors[index] = color_red;
                }
            }

            return colors;
        }

        public Color32[] GetDebugImage_FogMap()
        {
            Color32[] colors = GetImageMap0(true);

            var grid = _gridData._grid;
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    int index = y * _w + x;
                    var cell = grid[(x + _xRange.x) / 5, (y + _yRange.x) / 5];

                    if (cell != null && cell.HasFog)
                        colors[index] = color_green_blue;
                }

            return colors;
        }

        public Color32[] GetDebugImage_Grid()
        {
            Color32[] colors = GetColorsBuffer(_w, _h);
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    var p_x = x + _xRange.x;
                    var p_y = y + _yRange.x;
                    var pix = _map[p_x, p_y];

                    Color32 color = color_gray;
                    if (pix == PixType.Empty) color = color_dark;
                    if (pix == PixType.ObstacleEdge) color = color_white;
                    if (pix == PixType.ObstacleByBig) color = color_orange_dark;
                    if (pix == PixType.Fog) color = color_blue;

                    int index = y * _w + x;
                    colors[index] = color;
                }

            int cx_start = _xRange.x / 5;
            int cx_end = cx_start + (int)Mathf.Ceil((float)_w / 5);
            int cy_start = _yRange.x / 5;
            int cy_end = cy_start + (int)Mathf.Ceil((float)_h / 5);

            Color32 green0 = new Color32(20, 120, 20, 255);
            Color32 green1 = new Color32(170, 210, 170, 255);

            for (int cy = cy_start; cy < cy_end; cy++)
                for (int cx = cx_start; cx < cx_end; cx++)
                {

                    var cell = _gridData._grid[cx, cy];
                    if (cell == null)
                        continue;

                    int px_start = cx * 5;
                    int px_end = px_start + 5;
                    int py_start = cy * 5;
                    int py_end = py_start + 5;
                    if (cell.Direction == 0b1111_1111)      // 全方向
                    {
                        for (int py = py_start; py < py_end; py++)
                            for (int px = px_start; px < px_end; px++)
                            {
                                if (_map[px, py] == PixType.Empty)
                                    colors[(py - _yRange.x) * _w + (px - _xRange.x)] = new Color32(20, 60, 20, 255);    //绿
                            }
                    }
                    else if (cell.Direction == 0)           // 无方向
                    {
                        for (int py = py_start; py < py_end; py++)
                            for (int px = px_start; px < px_end; px++)
                            {
                                if (_map[px, py] == PixType.Empty)
                                    colors[(py - _yRange.x) * _w + (px - _xRange.x)] = new Color32(160, 0, 0, 255);    //红
                            }

                    }
                    else                                    // 有任意方向
                    {
                        int xs = cx * 5;
                        int ys = cy * 5;

                        // 咱们不涂黄了, 改为把"方向通路"地块涂绿。
                        byte direction = cell.Direction;
                        if ((direction & (1 << 7)) != 0)
                            colors[GetColorsIndex(xs + 2, ys)] = _map[xs + 2, ys] == PixType.ObstacleEdge ? green1 : green0;

                        if ((direction & (1 << 1)) != 0)
                            colors[GetColorsIndex(xs, ys + 2)] = _map[xs, ys + 2] == PixType.ObstacleEdge ? green1 : green0;

                        if ((direction & (1 << 3)) != 0)
                            colors[GetColorsIndex(xs + 2, ys + 4)] = _map[xs + 2, ys + 4] == PixType.ObstacleEdge ? green1 : green0;

                        if ((direction & (1 << 5)) != 0)
                            colors[GetColorsIndex(xs + 4, ys + 2)] = _map[xs + 4, ys + 2] == PixType.ObstacleEdge ? green1 : green0;

                        if ((direction & (1 << 0)) != 0)
                            colors[GetColorsIndex(xs, ys)] = _map[xs, ys] == PixType.ObstacleEdge ? green1 : green0;

                        if ((direction & (1 << 2)) != 0)
                            colors[GetColorsIndex(xs, ys + 4)] = _map[xs, ys + 4] == PixType.ObstacleEdge ? green1 : green0;

                        if ((direction & (1 << 4)) != 0)
                            colors[GetColorsIndex(xs + 4, ys + 4)] = _map[xs + 4, ys + 4] == PixType.ObstacleEdge ? green1 : green0;

                        if ((direction & (1 << 6)) != 0)
                            colors[GetColorsIndex(xs + 4, ys)] = _map[xs + 4, ys] == PixType.ObstacleEdge ? green1 : green0;

                    }
                }

            // 迷雾优先级高
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    var p_x = x + _xRange.x;
                    var p_y = y + _yRange.x;
                    var pix = _map[p_x, p_y];

                    if (pix == PixType.Fog)
                    {
                        int index = y * _w + x;
                        colors[index] = color_blue;
                    }
                }

            //画角色
            DrawPlayerIcon(new Color32Image(colors, _w, _h), new Vector2Int(100, 99), MapIconType.P1);

            return colors;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetColorsIndex(int x, int y)
        {
            return (y - _yRange.x) * _w + (x - _xRange.x);
        }


        #region SaveAStar
        public Color32[] GetDebugImage_GridAStar(BigCellPathResult pathObj)
        {
            Color32[] colors = GetDebugImage_Grid();

            // 迷雾分析流程 —— 展示
            if (!_find_target_in_empty)
            {
                var record = _pixRecord[PixRecordType.LineOfFindPath];
                foreach (var p in record)
                {
                    int x = p.x - _xRange.x, y = p.y - _yRange.x, index = y * _w + x;
                    colors[index] = color_red;
                }
            }


            if (pathObj == null)
                return colors;

            // 迷雾分析流程 —— 展示
            if (!_find_target_in_empty)
            {
                var record = _pixRecord[PixRecordType.AreaOfFindNearestFog];
                foreach (var p in record)
                {
                    int x = p.x - _xRange.x, y = p.y - _yRange.x, index = y * _w + x;
                    colors[index] = color_yellow;
                }
            }

            Color32 yellow = new Color32(150, 150, 20, 255);

            // 把寻路计算过的大格子标黄，把算法耗时图形化
            var grid = _gridData._grid;
            int gw = grid.GetLength(0);
            int gh = grid.GetLength(1);
            for (int cy = 0; cy < gh; cy++)
                for (int cx = 0; cx < gw; cx++)
                {
                    var c = grid[cx, cy];
                    if (c == null) continue;
                    if (!c.Access) continue;
                    int pxs = cx * 5;
                    int pys = cy * 5;
                    int pxe = pxs + 5;
                    int pye = pys + 5;
                    for (int py = pys; py < pye; py++)
                        for (int px = pxs; px < pxe; px++)
                        {
                            int index = (py - _yRange.x) * _w + (px - _xRange.x);
                            if (IU.Color32Equal(colors[index], new Color32(20, 60, 20, 255))
                            || IU.Color32Equal(colors[index], color_dark))
                                colors[index] = yellow;
                        }
                }

            if (pathObj.Status != PathFindingResult.Success)
                return colors;

            SmallCellFinder finder = new SmallCellFinder();
            var path = pathObj.Path;

            for (int i = 0; i < path.Count; i++)
            {
                var node = path[i];
                BigCell c = node.Cell;

                var zero_pos = new Vector2Int(c.x * 5, c.y * 5);
                var map = GetByRegion(zero_pos.x, zero_pos.y, 5, 5, _map);

                var result = finder.BeginAStar(map, node.From, node.To);
                DrawColor(colors, result, zero_pos, color_green);
            }

            return colors;
        }


        #region NearestFog

        public Color32[] GetDebugImage_NearestFog()
        {
            Color32[] colors = GetImageMap0(true);

            if (_findFogPath.Status == PathFindingResult.Success)
            {
                var path = _findFogPath.Path;
                SmallCellFinder finder = new SmallCellFinder();

                // 第一次不用画。因为它的大格子没"寻路起点"
                for (int i = 0; i < path.Count - 1; i++)
                {
                    var node = path[i];
                    BigCell cell = node.Cell;

                    var zero_pos = new Vector2Int(cell.x * 5, cell.y * 5);
                    var map = GetByRegion(zero_pos.x, zero_pos.y, 5, 5, _map);

                    var result = finder.BeginAStar(map, node.From, node.To);
                    DrawColor(colors, result, zero_pos, color_green);
                }
            }
            var img = new Color32Image(colors, _w, _h);
            DrawIcon(img, _range_zero);

            return colors;
        }

        /// <summary>
        /// w、h 赋值为10的倍数。固定大小
        /// </summary>
        public Color32[] GetDebugImage_NearestFogFollowing(Vector2Int size, out Vector2Int draw_zero)
        {
            var w = size.x;
            var h = size.y;
            Color32[] colors = GetDebugImage_Map0Following(size, out draw_zero);

            if (_findFogPath.Status == PathFindingResult.Success)
            {
                var path = _findFogPath.Path;
                SmallCellFinder finder = new SmallCellFinder();
                for (int i = 0; i < path.Count - 1; i++)
                {
                    var node = path[i];
                    BigCell cell = node.Cell;

                    var zero_pos = new Vector2Int(cell.x * 5, cell.y * 5);
                    var map = GetByRegion(zero_pos.x, zero_pos.y, 5, 5, _map);

                    var result = finder.BeginAStar(map, node.From, node.To);
                    DrawColor(colors, result, zero_pos - draw_zero, color_green, w, h);
                }
            }

            var img = new Color32Image(colors, w, h);
            DrawIcon(img, draw_zero);

            return colors;
        }

        public Color32[] GetDebugImage_DetailFollowing(Vector2Int size, out Vector2Int draw_zero, BigCellPathResult pathObj = null)
        {
            var w = size.x;
            var h = size.y;
            Color32[] colors = GetDebugImage_Map0Following(size, out draw_zero);

            if (pathObj != null && pathObj.Status == PathFindingResult.Success)
            {
                var path = pathObj.Path;
                SmallCellFinder finder = new SmallCellFinder();
                for (int i = 0; i < path.Count - 1; i++)
                {
                    var node = path[i];
                    BigCell cell = node.Cell;

                    var zero_pos = new Vector2Int(cell.x * 5, cell.y * 5);
                    var map = GetByRegion(zero_pos.x, zero_pos.y, 5, 5, _map);

                    var result = finder.BeginAStar(map, node.From, node.To);
                    DrawColor(colors, result, zero_pos - draw_zero, color_green, w, h);
                }
            }

            var img = new Color32Image(colors, w, h);
            DrawIcon(img, draw_zero);

            return colors;
        }

        #endregion

        #region PlayerAStar

        public Color32[] GetDebugImage_PlayerAStar(BigCellPathResult path1, BigCellPathResult path2
                        , Vector2Int size, out Vector2Int start_pos)
        {
            var w = size.x;
            var h = size.y;
            Color32[] colors = GetDebugImage_Map0Following(size, out start_pos);
            Color32Image img = new Color32Image(colors, w, h);

            var draw_zero = new Vector2Int(_small_map_pos.x + 100 - w / 2,  // 区域地图的原点
                                           _small_map_pos.y + 100 - h / 2);  // 如果size是200*200 那么就等于_small_map_pos   

            DrawIcon(img, draw_zero);

            if (path1.Status == PathFindingResult.Success)
            {
                var path = path1.Path;
                int len = path.Count;

                SmallCellFinder finder = new SmallCellFinder();
                for (int i = 0; i < len; i++)
                {
                    var node = path[i];
                    BigCell cell = node.Cell;

                    var zero_pos = new Vector2Int(cell.x * 5, cell.y * 5);
                    bool[,] map;
                    if (i == 0 || i == len - 1)     // 避免角色的Undefined地块干扰寻路。
                        map = GetByRegionBoard(zero_pos.x, zero_pos.y, 5, 5, _map);
                    else
                        map = GetByRegion(zero_pos.x, zero_pos.y, 5, 5, _map);

                    var result = finder.BeginAStar(map, node.From, node.To);
                    if (i == 0)
                        result.RemoveAt(0);
                    DrawColor(colors, result, zero_pos - draw_zero, color_green, w, h);
                }
            }

            if (path2.Status == PathFindingResult.Success)
            {
                var path = path2.Path;
                int len = path.Count;
                SmallCellFinder finder = new SmallCellFinder();
                for (int i = 0; i < len; i++)
                {
                    var node = path[i];
                    BigCell cell = node.Cell;

                    var zero_pos = new Vector2Int(cell.x * 5, cell.y * 5);
                    bool[,] map;
                    if (i == 0 || i == len - 1)     // 避免角色的Undefined地块干扰寻路。
                        map = GetByRegionBoard(zero_pos.x, zero_pos.y, 5, 5, _map);
                    else
                        map = GetByRegion(zero_pos.x, zero_pos.y, 5, 5, _map);

                    var result = finder.BeginAStar(map, node.From, node.To);
                    if (i == 0)
                        result.RemoveAt(0);
                    DrawColor(colors, result, zero_pos - draw_zero, color_green, w, h);
                }
            }




            return colors;
        }
        #endregion


        /// <summary>
        /// 还原 (所属模拟修改地图)
        /// </summary>
        public void RestoreMapInfo()
        {
            List<Vector2Int> pixList = new List<Vector2Int>();

            for (int i = _sim_modify.Count - 1; i >= 0; i--)
            {
                var item = _sim_modify[i];
                var start_pos = item.Item1;
                var origin = item.Item2;
                for (int py = 0; py < 5; py++)
                    for (int px = 0; px < 5; px++)
                    {
                        int kx = px + start_pos.x, ky = py + start_pos.y;
                        _map[kx, ky] = origin[px, py];
                        pixList.Add(new Vector2Int(kx, ky));
                    }
            }

            _sim_modify.Clear();
            _gridData.Apply(pixList);

            // IconErrorPos = default;
        }



        bool PlayerIconHasEmpty(Vector2Int center)
        {
            var x_start = center.x - 3;
            var y_start = center.y - 3;
            // 检索7*7的范围
            for (int py = y_start; py < y_start + 7; py++)
                for (int px = x_start; px < x_start + 7; px++)
                    if (_map[px, py] == PixType.Empty)
                        return true;

            return false;
        }




        // 涂颜色
        private void DrawColor(Color32[] colors, List<Vector2Int> path, Vector2Int offset, Color32 color)
        {
            foreach (var _ in path)
            {
                var pos = _ + offset;
                int index = (pos.y - _yRange.x) * _w + (pos.x - _xRange.x);
                colors[index] = color;
            }
        }

        private void DrawColor(Color32[] colors, List<Vector2Int> path, Vector2Int offset, Color32 color, int w, int h)
        {
            foreach (var _ in path)
            {
                var pos = _ + offset;
                if (pos.x < 0 || pos.x >= w || pos.y < 0 || pos.y >= h)
                    continue;
                int index = pos.y * w + pos.x;
                colors[index] = color;
            }
        }


        /// <summary>
        /// 获取_map中一个矩形区域的可行走情况
        /// </summary>
        public static bool[,] GetByRegion(int fromX, int fromY, int w, int h, PixType[,] map)
        {
            bool[,] result = new bool[w, h];
            int x_end = fromX + w;
            int y_end = fromY + h;
            for (int y = fromY; y < y_end; y++)
                for (int x = fromX; x < x_end; x++)
                    result[x - fromX, y - fromY] = map[x, y] == PixType.Empty;
            return result;
        }

        /// <summary>
        /// 获取_map中一个矩形区域的可行走情况。更广义
        /// </summary>
        public static bool[,] GetByRegionBoard(int fromX, int fromY, int w, int h, PixType[,] map)
        {
            bool[,] result = new bool[w, h];
            int x_end = fromX + w;
            int y_end = fromY + h;
            for (int y = fromY; y < y_end; y++)
                for (int x = fromX; x < x_end; x++)
                {
                    var data = map[x, y];
                    result[x - fromX, y - fromY] = data == PixType.Empty || data == PixType.Undefined;
                }
            return result;
        }

        #endregion

        #endregion

    }
}