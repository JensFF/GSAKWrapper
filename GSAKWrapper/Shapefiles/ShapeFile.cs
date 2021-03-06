﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using System.Collections;

namespace GSAKWrapper.Shapefiles
{
    public class ShapeFile: IDisposable
    {
        public enum ShapeType : int
        {
            NullShape = 0,
            Point = 1,
            PolyLine = 3,
            Polygon = 5,
            MultiPoint = 8,
            PointZ = 11,
            PolyLineZ = 13,
            PolygonZ = 15,
            MultiPointZ = 18,
            PointM = 21,
            PolyLineM = 23,
            PolygonM = 25,
            MultiPointM = 28,
            MultiPatch = 31
        }

        public enum CoordType : int
        {
            WGS84,
            DutchGrid
        }

        public class IndexRecord
        {
            public bool Ignore { get; set; }

            //from shx file
            public int Offset { get; set; }
            public int ContentLength { get; set; }

            //from shp file
            public ShapeType ShapeType { get; set; }
            public double XMin { get; set; }
            public double XMax { get; set; }
            public double YMin { get; set; }
            public double YMax { get; set; }

            //from dbf file
            public string Name { get; set; }
        }

        private string _shpFilename;
        private FileStream _shpFileStream = null;
        private byte[] _buffer = new byte[8];
        private CoordType _coordType;
        private AreaType _areaType;
        private string _dbfEncoding;
        private string _namePrefix;

        //shp header
        private int _shpFileSize = -1;   //The value for file length is the total length of the file in 16-bit words (including the fifty
                                        //16-bit words that make up the header)
        private int _shpVersion = -1;    //1000
        private ShapeType _shpShapeType = ShapeType.NullShape;
        private double _shpXMin;
        private double _shpXMax;
        private double _shpYMin;
        private double _shpYMax;

        private IndexRecord[] _indexRecords = null;
        private List<AreaInfo> _areaInfos = new List<AreaInfo>();
        private Hashtable _indexRecordsByName = new Hashtable();

        public ShapeFile(string shpFileName)
        {
            _shpFilename = shpFileName;
        }

        public override string ToString()
        {
            return _shpFilename;
        }

        public string NamePrefix
        {
            get { return _namePrefix; }
        }

        public List<AreaInfo> AreaInfos
        {
            get { return _areaInfos; }
        }

        public string[] GetFields()
        {
            string[] result = null;
            try
            {
                using (DotNetDBF.DBFReader dbf = new DotNetDBF.DBFReader(string.Format("{0}dbf", _shpFilename.Substring(0, _shpFilename.Length - 3)), _dbfEncoding))
                {
                    var fields = dbf.Fields;
                    result = (from s in fields select s.Name).ToArray();
                }
            }
            catch
            {
            }
            return result;
        }

        public bool Initialize(string dbfNameFieldName, CoordType coordType, AreaType areaType, string namePrefix, string dbfEncoding)
        {
            bool result = false;
            try
            {
                _coordType = coordType;
                _shpFileStream = File.OpenRead(_shpFilename);
                _areaType = areaType;
                _dbfEncoding = dbfEncoding;
                _namePrefix = namePrefix ?? "";
                int FileCode = GetInt32(_shpFileStream, false);
                if (FileCode==9994)
                {
                    _shpFileStream.Position = 24;
                    _shpFileSize = GetInt32(_shpFileStream, false);
                    _shpVersion = GetInt32(_shpFileStream, true);
                    _shpShapeType = (ShapeType)GetInt32(_shpFileStream, true);
                    _shpXMin = GetDouble(_shpFileStream, true);
                    _shpYMin = GetDouble(_shpFileStream, true);
                    _shpXMax = GetDouble(_shpFileStream, true);
                    _shpYMax = GetDouble(_shpFileStream, true);

                    using (FileStream fs = File.OpenRead(string.Format("{0}shx", _shpFilename.Substring(0, _shpFilename.Length - 3))))
                    {
                        FileCode = GetInt32(fs, false);
                        if (FileCode == 9994)
                        {
                            fs.Position = 24;
                            int shxFileSize = GetInt32(fs, false);
                            int shxVersion = GetInt32(fs, true);

                            int intRecordCount = ((shxFileSize * 2) - 100) / 8;
                            fs.Position = 100;
                            _indexRecords = new IndexRecord[intRecordCount];
                            for (int i = 0; i < intRecordCount; i++)
                            {
                                _indexRecords[i] = new IndexRecord() { Offset = GetInt32(fs, false) * 2, ContentLength = GetInt32(fs, false) * 2 };
                            }
                            for (int i = 0; i < intRecordCount; i++)
                            {
                                IndexRecord ir = _indexRecords[i];
                                _shpFileStream.Position = ir.Offset + 8;
                                ir.ShapeType = (ShapeType)GetInt32(_shpFileStream, true);
                                if (ir.ShapeType == ShapeType.NullShape)
                                {
                                    ir.ShapeType = _shpShapeType;
                                }
                                switch (ir.ShapeType)
                                {
                                    case ShapeType.Polygon:
                                    case ShapeType.PolygonZ:
                                    case ShapeType.PolygonM:
                                    case ShapeType.MultiPatch:
                                        ir.XMin = GetDouble(_shpFileStream, true);
                                        ir.YMin = GetDouble(_shpFileStream, true);
                                        ir.XMax = GetDouble(_shpFileStream, true);
                                        ir.YMax = GetDouble(_shpFileStream, true);
                                        ir.Ignore = false;
                                        break;
                                    default:
                                        ir.Ignore = true;
                                        break;
                                }
                            }
                            using (DotNetDBF.DBFReader dbf = new DotNetDBF.DBFReader(string.Format("{0}dbf", _shpFilename.Substring(0, _shpFilename.Length - 3)), _dbfEncoding))
                            {
                                var fields = dbf.Fields;
                                dbf.SetSelectFields(new string[]{dbfNameFieldName});
                                var rec = dbf.NextRecord();
                                int index = 0;
                                while (rec != null)
                                {
                                    if (!_indexRecords[index].Ignore)
                                    {
                                        var n = string.Format("{0}{1}", namePrefix, rec[0]);
                                        _indexRecords[index].Name = n;
                                        var g = _indexRecordsByName[n] as List<IndexRecord>;
                                        if (g == null)
                                        {
                                            g = new List<IndexRecord>();
                                            _indexRecordsByName.Add(n, g);
                                        }
                                        g.Add(_indexRecords[index]);
                                    }
                                    else
                                    {
                                        _indexRecords[index].Name = null;
                                    }
                                    index++;
                                    if (index < _indexRecords.Length)
                                    {
                                        rec = dbf.NextRecord();
                                    }
                                    else
                                    {
                                        rec = null;
                                    }
                                }
                            }

                            // all ok, check if we need to convert the coords to WGS84, the internal coord system
                            if (_coordType == CoordType.DutchGrid)
                            {
                                Utils.Calculus.LatLonFromRD(_shpXMin, _shpYMin, out _shpYMin, out _shpXMin);
                                Utils.Calculus.LatLonFromRD(_shpXMax, _shpYMax, out _shpYMax, out _shpXMax);

                                double lat;
                                double lon;
                                for (int i = 0; i < intRecordCount; i++)
                                {
                                    IndexRecord ir = _indexRecords[i];
                                    Utils.Calculus.LatLonFromRD(ir.XMin, ir.YMin, out lat, out lon);
                                    ir.YMin = lat;
                                    ir.XMin = lon;
                                    Utils.Calculus.LatLonFromRD(ir.XMax, ir.YMax, out lat, out lon);
                                    ir.YMax = lat;
                                    ir.XMax = lon;
                                }
                            }

                            var areaNames = _indexRecordsByName.Keys;
                            foreach (string name in areaNames)
                            {
                                var records = _indexRecordsByName[name] as List<IndexRecord>;
                                AreaInfo ai = new AreaInfo();
                                ai.ID = ai;
                                ai.Level = areaType;
                                ai.MaxLat = records.Max(x => x.YMax);
                                ai.MaxLon = records.Max(x => x.XMax);
                                ai.MinLat = records.Min(x => x.YMin);
                                ai.MinLon = records.Min(x => x.XMin);
                                ai.Name = name;
                                ai.ParentID = null; //not supported
                                _areaInfos.Add(ai);
                            }

                            result = true;
                        }
                    }
                }
            }
            catch
            {
            }
            return result;
        }

        public List<AreaInfo> GetAreasOfLocation(Utils.Location loc)
        {
            List<AreaInfo> result = null;
            if (loc.Lat >= _shpYMin && loc.Lat <= _shpYMax && loc.Lon >= _shpXMin && loc.Lon <= _shpXMax)
            {
                //all areas with point in envelope
                var ais = from r in _areaInfos where loc.Lat >= r.MinLat && loc.Lat <= r.MaxLat && loc.Lon >= r.MinLon && loc.Lon <= r.MaxLon select r;
                foreach (var ai in ais)
                {
                    if (IsLocationInArea(loc, ai))
                    {
                        if (result == null)
                        {
                            result = new List<AreaInfo>();
                        }
                        result.Add(ai);
                    }
                }

            }
            return result;
        }

        public string GetAreaNameOfLocation(double lat, double lon, AreaType areaType, string prefix)
        {
            string result = null;
            if (string.Compare(_namePrefix, prefix ?? "", true) == 0 && lat >= _shpYMin && lat <= _shpYMax && lon >= _shpXMin && lon <= _shpXMax)
            {
                //all areas with point in envelope
                var ais = from r in _areaInfos
                          where r.Level == areaType && lat >= r.MinLat && lat <= r.MaxLat && lon >= r.MinLon && lon <= r.MaxLon
                          select r;
                foreach (var ai in ais)
                {
                    if (IsLocationInArea(lat, lon, ai))
                    {
                        result = ai.Name;
                        break;
                    }
                }

            }
            return result;
        }

        public List<AreaInfo> GetAreasOfLocation(Utils.Location loc, List<AreaInfo> inAreas)
        {
            List<AreaInfo> result = null;
            if (loc.Lat >= _shpYMin && loc.Lat <= _shpYMax && loc.Lon >= _shpXMin && loc.Lon <= _shpXMax)
            {
                //all areas with point in envelope
                var ais = from r in _areaInfos 
                          join b in inAreas on r equals b
                          where loc.Lat >= r.MinLat && loc.Lat <= r.MaxLat && loc.Lon >= r.MinLon && loc.Lon <= r.MaxLon select r;
                foreach (var ai in ais)
                {
                    if (IsLocationInArea(loc, ai))
                    {
                        if (result == null)
                        {
                            result = new List<AreaInfo>();
                        }
                        result.Add(ai);
                    }
                }

            }
            return result;
        }

        public List<AreaInfo> GetEnvelopAreasOfLocation(Utils.Location loc)
        {
            List<AreaInfo> result = null;
            if (loc.Lat >= _shpYMin && loc.Lat <= _shpYMax && loc.Lon >= _shpXMin && loc.Lon <= _shpXMax)
            {
                //all areas with point in envelope
                var ais = from r in _areaInfos where loc.Lat >= r.MinLat && loc.Lat <= r.MaxLat && loc.Lon >= r.MinLon && loc.Lon <= r.MaxLon select r;
                foreach (var ai in ais)
                {
                    if (IsLocationInEnvelopArea(loc, ai))
                    {
                        if (result == null)
                        {
                            result = new List<AreaInfo>();
                        }
                        result.Add(ai);
                    }
                }

            }
            return result;
        }

        public List<AreaInfo> GetEnvelopAreasOfLocation(Utils.Location loc, List<AreaInfo> inAreas)
        {
            List<AreaInfo> result = null;
            if (loc.Lat >= _shpYMin && loc.Lat <= _shpYMax && loc.Lon >= _shpXMin && loc.Lon <= _shpXMax)
            {
                //all areas with point in envelope
                var ais = from r in _areaInfos
                          join b in inAreas on r equals b
                          where loc.Lat >= r.MinLat && loc.Lat <= r.MaxLat && loc.Lon >= r.MinLon && loc.Lon <= r.MaxLon
                          select r;
                foreach (var ai in ais)
                {
                    if (IsLocationInEnvelopArea(loc, ai))
                    {
                        if (result == null)
                        {
                            result = new List<AreaInfo>();
                        }
                        result.Add(ai);
                    }
                }

            }
            return result;
        }


        private bool IsLocationInArea(Utils.Location loc, AreaInfo area)
        {
            return IsLocationInArea( loc.Lat, loc.Lon, area);
        }

        private bool IsLocationInArea(double lat, double lon, AreaInfo area)
        {
            bool result = false;
            //point in envelope of area
            if (lat >= area.MinLat && lat <= area.MaxLat && lon >= area.MinLon && lon <= area.MaxLon)
            {
                GetPolygonOfArea(area);
                if (area.Polygons != null)
                {
                    foreach (var r in area.Polygons)
                    {
                        //point in envelope of polygon
                        if (lat >= r.MinLat && lat <= r.MaxLat && lon >= r.MinLon && lon <= r.MaxLon)
                        {
                            if (Utils.Calculus.PointInPolygon(r, lat, lon))
                            {
                                result = true;
                                break;
                            }
                        }
                    }
                }
            }
            return result;
        }

        private bool IsLocationInEnvelopArea(Utils.Location loc, AreaInfo area)
        {
            bool result = false;
            //point in envelope of area
            if (loc.Lat >= area.MinLat && loc.Lat <= area.MaxLat && loc.Lon >= area.MinLon && loc.Lon <= area.MaxLon)
            {
                GetPolygonOfArea(area);
                if (area.Polygons != null)
                {
                    foreach (var r in area.Polygons)
                    {
                        //point in envelope of polygon
                        if (loc.Lat >= r.MinLat && loc.Lat <= r.MaxLat && loc.Lon >= r.MinLon && loc.Lon <= r.MaxLon)
                        {
                            result = true;
                            break;
                        }
                    }
                }
            }
            return result;
        }

        public void GetPolygonOfArea(AreaInfo area)
        {
            if (_areaInfos.Contains(area))
            {
                //ours
                if (area.Polygons == null)
                {
                    //get all records and add the data
                    area.Polygons = new List<Utils.Polygon>();
                    try
                    {
                        var recs = _indexRecordsByName[area.Name] as List<IndexRecord>;
                        if (recs != null)
                        {
                            foreach (var rec in recs)
                            {
                                GetPolygonOfArea(area.Polygons, rec);
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void GetPolygonOfArea(List<Utils.Polygon> polygons, IndexRecord rec)
        {
            switch (rec.ShapeType)
            {
                case ShapeType.Polygon:
                    GetPolygonOfArea_Polygon(polygons, rec);
                    break;
                case ShapeType.PolygonM:
                    GetPolygonOfArea_PolygonM(polygons, rec);
                    break;
                case ShapeType.PolygonZ:
                    GetPolygonOfArea_PolygonZ(polygons, rec);
                    break;
                case ShapeType.MultiPatch:
                    GetPolygonOfArea_MultiPatch(polygons, rec);
                    break;
                default:
                    break;
            }
        }

        private void GetPolygonOfArea_Polygon(List<Utils.Polygon> polygons, IndexRecord rec)
        {
            _shpFileStream.Position = rec.Offset + 8 + 36; //skip bounding box and shapetype
            int numberOfPolygons = GetInt32(_shpFileStream, true);
            int numberOfPoints = GetInt32(_shpFileStream, true);
            int[] pointIndexFirstPointPerPolygon = new int[numberOfPolygons];
            for (int i = 0; i < numberOfPolygons; i++)
            {
                pointIndexFirstPointPerPolygon[i] = GetInt32(_shpFileStream, true);
            }
            for (int i = 0; i < numberOfPolygons; i++)
            {
                Utils.Polygon pg = new Utils.Polygon();
                int pointCount;
                if (i < numberOfPolygons - 1)
                {
                    pointCount = pointIndexFirstPointPerPolygon[i + 1] - pointIndexFirstPointPerPolygon[i];
                }
                else
                {
                    pointCount = numberOfPoints - pointIndexFirstPointPerPolygon[i];
                }
                for (int p = 0; p < pointCount; p++)
                {
                    double x = GetDouble(_shpFileStream, true);
                    double y = GetDouble(_shpFileStream, true);
                    if (_coordType == CoordType.DutchGrid)
                    {
                        pg.AddLocation(Utils.Calculus.LocationFromRD(x,y));
                    }
                    else
                    {
                        pg.AddLocation(new Utils.Location(y, x));
                    }
                }
                polygons.Add(pg);
            }
        }

        private void GetPolygonOfArea_PolygonM(List<Utils.Polygon> polygons, IndexRecord rec)
        {
            //extra M information is after the Polygon info, so..
            GetPolygonOfArea_Polygon(polygons, rec);
        }
        private void GetPolygonOfArea_PolygonZ(List<Utils.Polygon> polygons, IndexRecord rec)
        {
            //extra Z information is after the Polygon info, so..
            GetPolygonOfArea_Polygon(polygons, rec);
        }

        private void GetPolygonOfArea_MultiPatch(List<Utils.Polygon> polygons, IndexRecord rec)
        {
            //NOTE: at this point we ignore the type (outer ring, inner ring
            //this is not correct and should be implemented correctly
            //suggestion: Add a property Exclude to Framework.Data.Polygon

            _shpFileStream.Position = rec.Offset + 8 + 36; //skip bounding box and shapetype
            int numberOfPolygons = GetInt32(_shpFileStream, true);
            int numberOfPoints = GetInt32(_shpFileStream, true);
            int[] pointIndexFirstPointPerPolygon = new int[numberOfPolygons];
            int[] partsTypePerPolygon = new int[numberOfPolygons];
            for (int i = 0; i < numberOfPolygons; i++)
            {
                pointIndexFirstPointPerPolygon[i] = GetInt32(_shpFileStream, true);
            }
            for (int i = 0; i < numberOfPolygons; i++)
            {
                partsTypePerPolygon[i] = GetInt32(_shpFileStream, true);
            }
            for (int i = 0; i < numberOfPolygons; i++)
            {
                Utils.Polygon pg = new Utils.Polygon();
                int pointCount;
                if (i < numberOfPolygons - 1)
                {
                    pointCount = pointIndexFirstPointPerPolygon[i + 1] - pointIndexFirstPointPerPolygon[i];
                }
                else
                {
                    pointCount = numberOfPoints - pointIndexFirstPointPerPolygon[i];
                }
                for (int p = 0; p < pointCount; p++)
                {
                    double x = GetDouble(_shpFileStream, true);
                    double y = GetDouble(_shpFileStream, true);
                    if (_coordType == CoordType.DutchGrid)
                    {
                        pg.AddLocation(Utils.Calculus.LocationFromRD(x, y));
                    }
                    else
                    {
                        pg.AddLocation(new Utils.Location(y, x));
                    }
                }
                polygons.Add(pg);
            }
        }


        private int GetInt32(FileStream fs, bool littleEndian)
        {
            fs.Read(_buffer, 0, 4);
            if (littleEndian == BitConverter.IsLittleEndian)
            {
                return BitConverter.ToInt32(_buffer, 0);
            }
            else
            {
                return BitConverter.ToInt32(ReverseBytes(_buffer,0,4), 0);
            }
        }
        private double GetDouble(FileStream fs, bool littleEndian)
        {
            fs.Read(_buffer, 0, 8);
            if (littleEndian == BitConverter.IsLittleEndian)
            {
                return BitConverter.ToDouble(_buffer, 0);
            }
            else
            {
                return BitConverter.ToDouble(ReverseBytes(_buffer, 0, 8), 0);
            }
        }

        public static byte[] ReverseBytes(byte[] inArray, int startIndex, int length)
        {
            byte temp;
            int ctr = startIndex;
            int highCtr = startIndex + length - 1;

            for (int i = 0; i < length / 2; i++)
            {
                temp = inArray[ctr];
                inArray[ctr] = inArray[highCtr];
                inArray[highCtr] = temp;
                highCtr--;
                ctr++;
            }
            return inArray;
        }

        public void Dispose()
        {
            if (_shpFileStream != null)
            {
                _shpFileStream.Dispose();
                _shpFileStream = null;
            }
        }
    }
}
