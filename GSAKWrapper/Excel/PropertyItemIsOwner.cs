﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GSAKWrapper.Excel
{
    public class PropertyItemIsOwner : PropertyItem
    {
        public PropertyItemIsOwner()
            : base("IsOwner")
        {
        }
        public override object GetValue(GeocacheData gc)
        {
            return gc.Caches.IsOwner != 0;
        }
    }
}
