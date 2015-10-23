﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace GSAKWrapper.UIControls.ActionBuilder
{
    public class ActionDidNotFindDate : ActionImplementationDate
    {
        public const string STR_NAME = "DidNotFindDate";
        public ActionDidNotFindDate()
            : base(STR_NAME, "DNFDate")
        {
        }
    }
}
