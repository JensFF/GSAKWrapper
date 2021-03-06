﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GSAKWrapper.Shapefiles
{
    public class ShapeFileInfo
    {
        public bool Enabled { get; set; }
        public string Filename { get; set; }
        public string TableName { get; set; }
        public List<string> TableNames { get; set; }
        public string TCoord { get; set; }
        public List<string> TCoords { get; set; }
        public string TArea { get; set; }
        public List<string> TAreas { get; set; }
        public string Prefix { get; set; }
        public string Encoding { get; set; }
        public List<string> TEncodings { get; set; }
    }
}
