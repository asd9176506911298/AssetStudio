using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AssetStudio
{
    [Serializable]
    public class SpriteInfo
    {
        public List<int> sid;
        public List<int> sx;
        public List<int> sy;
        public List<int> sxr;
        public List<int> syr;
        public List<int> swidth;
        public List<int> sheight;
        public List<string> scollectionname;
        public List<string> spath;
        public List<bool> sfilpped;
        public List<SpritePackingRotation> srotate;
        public List<string> spathPoints;

        public SpriteInfo()
        {
            sid = new List<int>();
            sx = new List<int>();
            sy = new List<int>();
            sxr = new List<int>();
            syr = new List<int>();
            swidth = new List<int>();
            sheight = new List<int>();
            scollectionname = new List<string>();
            spath = new List<string>();
            sfilpped = new List<bool>();
            srotate = new List<SpritePackingRotation>();
            spathPoints = new List<string>();
        }

        public void Add(int _sid, int _sx, int _sy, int _sxr, int _syr, int _swidth, int _sheight, string _scollectionname, string _spath, bool _sfilpped, SpritePackingRotation _rotate)
        {
            sid.Add(_sid);
            sx.Add(_sx);
            sy.Add(_sy);
            sxr.Add(_sxr);
            syr.Add(_syr);
            swidth.Add(_swidth);
            sheight.Add(_sheight);
            scollectionname.Add(_scollectionname);
            spath.Add(_spath);
            sfilpped.Add(_sfilpped);
            srotate.Add(_rotate);
            spathPoints.Add("[]"); // 空座標
        }

        public void SavePathPoints(int index, PointF[] points)
        {
            if (index >= 0 && index < sid.Count)
            {
                var pointList = points.Select(p => new float[] { p.X, p.Y }).ToList();
                spathPoints[index] = JsonSerializer.Serialize(pointList);
            }
        }

        public PointF[] GetPathPoints(int index)
        {
            if (index >= 0 && index < spathPoints.Count && spathPoints[index] != "[]")
            {
                var pointArray = JsonSerializer.Deserialize<float[][]>(spathPoints[index]);
                return pointArray.Select(p => new PointF(p[0], p[1])).ToArray();
            }
            return new PointF[0];
        }

        public void Sort()
        {
            // 1. 生成排序鍵 (folder + 尾數)
            var sortKeys = spath.Select(path =>
            {
                string folder = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/").ToLower() ?? "";
                string filename = System.IO.Path.GetFileNameWithoutExtension(path);
                var match = Regex.Match(filename, @"(\d+)$");
                int number = match.Success ? int.Parse(match.Groups[1].Value) : -1;
                return (folder, number);
            }).ToList();

            // 2. 生成排序索引
            var order = sortKeys
                .Select((key, index) => new { key, index })
                .OrderBy(x => x.key.folder)
                .ThenBy(x => x.key.number)
                .Select(x => x.index)
                .ToList();

            // 3. 同步重排所有 List 欄位
            sid = order.Select(i => sid[i]).ToList();
            sx = order.Select(i => sx[i]).ToList();
            sy = order.Select(i => sy[i]).ToList();
            sxr = order.Select(i => sxr[i]).ToList();
            syr = order.Select(i => syr[i]).ToList();
            swidth = order.Select(i => swidth[i]).ToList();
            sheight = order.Select(i => sheight[i]).ToList();
            scollectionname = order.Select(i => scollectionname[i]).ToList();
            spath = order.Select(i => spath[i]).ToList();
            sfilpped = order.Select(i => sfilpped[i]).ToList();
            srotate = order.Select(i => srotate[i]).ToList();
            spathPoints = order.Select(i => spathPoints[i]).ToList();
        }

        public void clear()
        {
            sid.Clear();
            sx.Clear();
            sy.Clear();
            sxr.Clear();
            syr.Clear();
            swidth.Clear();
            sheight.Clear();
            scollectionname.Clear();
            spath.Clear();
            sfilpped.Clear();
            srotate.Clear();
            spathPoints.Clear();
        }
    }
}