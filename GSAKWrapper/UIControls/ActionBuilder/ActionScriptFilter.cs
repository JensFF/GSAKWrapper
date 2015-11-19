﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace GSAKWrapper.UIControls.ActionBuilder
{
    public class ActionScriptFilter : ActionImplementationCondition
    {
        public const string STR_NAME = "ScriptFilter";

        private string _script = "";
        private Script.IFilterScript _scriptObject = null;
        public ActionScriptFilter()
            : base(STR_NAME)
        {
        }

        public override SearchType SearchTypeTarget { get { return SearchType.Extra; } }
        public ObservableCollection<string> AvailableScripts { get; set; }

        public override UIElement GetUIElement()
        {
            if (AvailableScripts == null)
            {
                AvailableScripts = new ObservableCollection<string>();
            }
            if (Values.Count == 0)
            {
                Values.Add("");
            }
            StackPanel sp = new StackPanel();
            Button b = new Button();
            b.Content = Localization.TranslationManager.Instance.Translate("ScriptEditor");
            b.Click += b_Click;
            sp.Children.Add(b);

            ComboBox cb = CreateComboBox(new string[] { }, Values[0]);
            cb.DropDownOpened += cb_DropDownOpened;
            cb.IsEditable = true;
            sp.Children.Add(cb);

            Binding binding = new Binding();
            binding.Source = AvailableScripts;
            BindingOperations.SetBinding(cb, ComboBox.ItemsSourceProperty, binding);

            return sp;
        }
        public override void CommitUIData(UIElement uiElement)
        {
            StackPanel sp = uiElement as StackPanel;
            ComboBox cb = sp.Children[1] as ComboBox;
            Values[0] = cb.Text;
        }
        void b_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Dialogs.WindowScriptEditor();
            dlg.ShowDialog();
        }
        void cb_DropDownOpened(object sender, EventArgs e)
        {
            AvailableScripts.Clear();
            var opt = (from a in Settings.Settings.Default.GetScriptItems() where a.ScriptType==Script.Manager.ScriptTypeFilter orderby a.Name select a.Name).ToArray();
            foreach (var c in opt)
            {
                AvailableScripts.Add(c);
            }
        }
        public override bool PrepareRun(Database.DBCon db, string tableName)
        {
            _script = "";
            _scriptObject = null;
            if (Values.Count > 0)
            {
                _script = Values[0];
            }
            if (!string.IsNullOrEmpty(_script))
            {
                var scr = Settings.Settings.Default.GetScriptItem(_script);
                if (scr != null)
                {
                    _scriptObject = Script.Manager.Instance.LoadFilterScript(scr.Code);
                    _scriptObject.PrepareRun(this, db, tableName);
                }
            }
            return base.PrepareRun(db, tableName);
        }

        public override void Process(Operator op, string inputTableName, string targetTableName)
        {
            if (_scriptObject != null)
            {
                _scriptObject.Process(this, op, inputTableName, targetTableName);
            }
        }

        public override void FinalizeRun()
        {
            if (_scriptObject != null)
            {
                TotalProcessTime.Start();
                _scriptObject.FinalizeRun(this);
                TotalProcessTime.Stop();
            }
            base.FinalizeRun();
        }
    }
}
