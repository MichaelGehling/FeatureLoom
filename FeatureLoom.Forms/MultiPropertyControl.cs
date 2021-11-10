using FeatureLoom.MessageFlow;
using FeatureLoom.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FeatureLoom.Forms
{
    public partial class MultiPropertyControl : UserControl
    {

        public class Property
        {
            private static uint nameCount = 0;
            private static string onFocusText = "";

            private string name;
            private MultiPropertyControl parentControl;
            private TableLayoutPanel propertyTable;
            private int rowIndex;
            private Sender<PropertyEventNotification> sender;
            private Label label = new Label();
            private Control[] fields;            
            private RowStyle rowStyle = new RowStyle();
            private Predicate<string>[] verifiers;
            private bool readOnly = false;                        

            public Property(MultiPropertyControl parentControl, TableLayoutPanel table, int rowIndex, int numFields, string name, Sender<PropertyEventNotification>  sender)
            {
                this.parentControl = parentControl;
                this.fields = new Control[numFields];
                this.verifiers = new Predicate<string>[numFields];
                this.propertyTable = table;
                this.rowIndex = rowIndex;
                this.sender = sender;
                this.name = name;

                propertyTable.RowStyles.Insert(rowIndex, rowStyle);
                propertyTable.Controls.Add(label, 0, rowIndex);
                label.Click += (o, e) => sender.Send(new PropertyEventNotification(label.Text, PropertyEvent.Clicked));

                nameCount++;

                this.label.Anchor = System.Windows.Forms.AnchorStyles.Right;
                this.label.AutoSize = true;
                this.label.Location = new System.Drawing.Point(3, 6);
                this.label.Name = "PropertyLabel" + nameCount;
                this.label.Size = new System.Drawing.Size(10, 10);
                this.label.Text = name;

                parentControl.UpdateSizes();
            }

            internal void ChangeNumberOfFields(int numFields)
            {
                                
                if (numFields < fields.Length)
                {
                    for(int i = numFields; i < fields.Length; i++)
                    {
                        RemoveField(i);
                    }

                    var oldFields = fields;
                    fields = new Control[numFields];
                    for (int i = 0; i < numFields; i++)
                    {
                        fields[i] = oldFields[i];
                    }
                }
                else if (numFields > fields.Length)
                {
                    var oldFields = fields;
                    fields = new Control[numFields];
                    for (int i = 0; i < oldFields.Length; i++)
                    {
                        fields[i] = oldFields[i];
                    }
                }
            }            

            public Label GetLabelControl() => label;

            public string Name => name;

            public int RowIndex => rowIndex;

            internal void UpdateRowIndex()
            {
                rowIndex = propertyTable.GetRow(label);
            }

            public Property SetLabel(string labelText)
            {
                label.Text = labelText;
                return this;
            }

            public void Rename(string newName)
            {
                parentControl.RenameProperty(name, newName);
                if (label.Text == this.name) label.Text = newName;
                name = newName;                
            }

            public Control GetFieldControl(int fieldIndex = 0) => fields[fieldIndex];

            public string GetValue(int fieldIndex = 0) => fields[fieldIndex]?.Text ?? "";

            public Property SetValue(string value, int fieldIndex = 0)
            {
                if (fields[fieldIndex] == null)
                {
                    TextBox field = new TextBox();
                    fields[fieldIndex] = field;
                    propertyTable.Controls.Add(field, fieldIndex + 1, rowIndex);

                    field.Enabled = !readOnly;
                    field.Dock = DockStyle.Fill;
                    field.Location = new Point(0, 0);
                    field.Name = "PropertyField" + nameCount;
                    field.Size = new Size(50, 26);
                    field.TabIndex = 1;
                    field.AllowDrop = true;

                    field.TextChanged += (o, e) => sender.Send(new PropertyEventNotification(label.Text, PropertyEvent.ValueChanged, fieldIndex, field.Text));
                    field.GotFocus += (o, e) =>
                    {
                        onFocusText = field.Text;
                        sender.Send(new PropertyEventNotification(label.Text, PropertyEvent.GotFocus, fieldIndex, field.Text));
                    };
                    field.LostFocus += (o, e) => sender.Send(new PropertyEventNotification(label.Text, PropertyEvent.LostFocus, fieldIndex, field.Text));
                    field.Click += (o, e) => sender.Send(new PropertyEventNotification(label.Text, PropertyEvent.Clicked, fieldIndex));
                    field.EnabledChanged += (o, e) => sender.Send(new PropertyEventNotification(label.Text, PropertyEvent.ReadOnlyChanged, fieldIndex, !field.Enabled));

                    field.TextChanged += (o, e) =>
                    {
                        var measured = TextRenderer.MeasureText(field.Text, field.Font);
                        field.MinimumSize = measured;
                        parentControl.UpdateSizes();

                        this.TriggerVerify(fieldIndex);
                    };

                    field.DragEnter += (o, e) =>
                    {
                        if (e.Data.GetDataPresent(DataFormats.Text))
                            e.Effect = DragDropEffects.Link;
                        else
                            e.Effect = DragDropEffects.None;
                    };

                    field.DragDrop += (o, e) =>
                    {
                        string dropText = e.Data.GetData(DataFormats.Text).ToString();
                        field.Text = dropText;
                        sender.Send(new PropertyEventNotification(label.Text, PropertyEvent.DroppedInValue, fieldIndex, field.Text));
                    };
                }

                fields[fieldIndex].Text = value;
                parentControl.UpdateSizes();
                return this;
            }

            public Property SetValueRestrictions(IEnumerable<string> options, int fieldIndex =  0)
            {
                if (fields[fieldIndex] == null || !(fields[fieldIndex] is ComboBox))
                {                    
                    string value = "";
                    if (fields[fieldIndex] != null) value = fields[fieldIndex].Text;
                    
                    RemoveField(fieldIndex);

                    ComboBox field = new ComboBox();
                    fields[fieldIndex] = field;
                    propertyTable.Controls.Add(field, fieldIndex + 1, rowIndex);

                    field.Enabled = !readOnly;
                    field.Dock = DockStyle.Fill;
                    field.Location = new Point(0, 0);
                    field.Name = "PropertyField" + nameCount;
                    field.Size = new Size(50, 26);
                    field.TabIndex = 1;
                    field.AllowDrop = true;                    
                    field.DropDownStyle = ComboBoxStyle.DropDown;                    
                    field.DataSource = options;
                    field.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                    field.AutoCompleteSource = AutoCompleteSource.ListItems;

                    field.TextChanged += (o, e) =>
                    {
                        if (field.DataSource is IEnumerable<string> fieldOptions && fieldOptions.Contains(field.Text))
                        {
                            sender.Send(new PropertyEventNotification(label.Text, PropertyEvent.ValueChanged, fieldIndex, field.Text));                                 
                        }
                        var measured = TextRenderer.MeasureText(field.Text, field.Font);
                        field.MinimumSize = measured;
                        parentControl.UpdateSizes();
                        this.TriggerVerify(fieldIndex);
                    };
                    field.GotFocus += (o, e) =>
                    {
                        onFocusText = field.Text;
                        sender.Send(new PropertyEventNotification(label.Text, PropertyEvent.GotFocus, fieldIndex, field.Text));
                    };
                    field.LostFocus += (o, e) =>
                    {
                        if (field.DataSource is IEnumerable<string> fieldOptions && fieldOptions.Contains(field.Text))
                        {
                            sender.Send(new PropertyEventNotification(label.Text, PropertyEvent.LostFocus, fieldIndex, field.Text));
                        }
                        else field.Text = onFocusText;
                    };
                    field.Click += (o, e) => sender.Send(new PropertyEventNotification(label.Text, PropertyEvent.Clicked, fieldIndex));
                    field.EnabledChanged += (o, e) => sender.Send(new PropertyEventNotification(label.Text, PropertyEvent.ReadOnlyChanged, fieldIndex, !field.Enabled));


                    field.Text = value;

                    field.DragEnter += (o, e) =>
                    {
                        if (e.Data.GetDataPresent(DataFormats.Text)) e.Effect = DragDropEffects.Link;
                        else e.Effect = DragDropEffects.None;
                    };

                    field.DragDrop += (o, e) =>
                    {
                        string dropText = e.Data.GetData(DataFormats.Text).ToString();
                        field.Text = dropText;
                        sender.Send(new PropertyEventNotification(label.Text, PropertyEvent.DroppedInValue, fieldIndex, field.Text));
                    };

                    parentControl.UpdateSizes();
                }
                else
                {
                    ComboBox field = fields[fieldIndex] as ComboBox;                    
                    field.DataSource = options;
                    field.Text = "";

                    parentControl.UpdateSizes();
                }

                return this;
            }

            public Property SetCustomFieldControl(Control customControl, int fieldIndex = 0)
            {
                RemoveField(fieldIndex);

                var field = customControl;
                fields[fieldIndex] = field;                
                propertyTable.Controls.Add(field, fieldIndex + 1, rowIndex);

                field.Enabled = !readOnly;

                field.TextChanged += (o, e) => sender.Send(new PropertyEventNotification(label.Text, PropertyEvent.ValueChanged, fieldIndex, field.Text));
                field.GotFocus += (o, e) => sender.Send(new PropertyEventNotification(label.Text, PropertyEvent.GotFocus, fieldIndex, field.Text));
                field.LostFocus += (o, e) => sender.Send(new PropertyEventNotification(label.Text, PropertyEvent.LostFocus, fieldIndex, field.Text));
                field.Click += (o, e) => sender.Send(new PropertyEventNotification(label.Text, PropertyEvent.Clicked, fieldIndex));
                field.EnabledChanged += (o, e) => sender.Send(new PropertyEventNotification(label.Text, PropertyEvent.ReadOnlyChanged, fieldIndex, !field.Enabled));

                parentControl.UpdateSizes();
                return this;
            }

            public Property SetReadOnly(bool readOnly, int fieldIndex)
            {
                this.readOnly = readOnly;
                Control field = fields[fieldIndex];
                if (field != null) field.Enabled = !readOnly;
                return this;
            }

            public Property SetReadOnly(bool readOnly)
            {
                foreach(Control field in fields)
                {
                    if (field != null) field.Enabled = !readOnly;
                }
                return this;
            }

            public Property RemoveField(int fieldIndex = 0)
            {
                if (fields[fieldIndex] != null)
                {
                    propertyTable.Controls.Remove(fields[fieldIndex]);
                    fields[fieldIndex] = null;
                }
                return this;
            }

            public Property SetVerifier(Predicate<string> verifier, int fieldIndex = 0)
            {
                verifiers[fieldIndex] = verifier;
                TriggerVerify(fieldIndex);
                return this;
            }

            private void TriggerVerify(int fieldIndex = 0)
            {
                var field = fields[fieldIndex];
                var verifier = verifiers[fieldIndex];

                if (field == null) return;
                else if (verifier != null && !verifier(field.Text)) field.BackColor = Color.LightPink;
                else if (field is ComboBox comboBox && comboBox.DataSource is IEnumerable<string> options && !options.Contains(field.Text)) field.BackColor = Color.LightPink;
                else field.BackColor = Color.Empty;
            }

        }


        private int numFieldColumns = 0;
        private Dictionary<string, Property> properties = new Dictionary<string, Property>();

        private Sender<PropertyEventNotification> sender = new Sender<PropertyEventNotification>();
        public IMessageSource<PropertyEventNotification> PropertyEventNotifier => sender;

        public class PropertyEventNotification
        {
            public PropertyEventNotification(string propertyName, PropertyEvent @event, int fieldIndex = -1, object parameter = null)
            {
                PropertyName = propertyName;
                Event = @event;
                Parameter = parameter;
                FieldIndex = fieldIndex;
            }

            public string PropertyName { get; }
            public PropertyEvent Event { get; }
            public int FieldIndex { get; }
            public object Parameter { get; }
        }

        public enum PropertyEvent
        {
            Created,
            Removed,
            ValueChanged,
            Clicked,
            GotFocus,
            LostFocus,
            DroppedInValue,
            ReadOnlyChanged
        }

        public MultiPropertyControl(int numFieldColumns = 1, ColumnStyle defaultColumnStyle = null)
        {            
            InitializeComponent();
            SetNumFieldColumns(numFieldColumns, defaultColumnStyle);
            Resize += (o, e) => UpdateSizes();                  
        }        

        public void SetNumFieldColumns(int numFieldColumns, ColumnStyle defaultColumnStyle = null)
        {
            if (numFieldColumns == this.numFieldColumns) return;

            this.propertyTable.ColumnCount = numFieldColumns + 1;
            if (numFieldColumns > this.numFieldColumns)
            {
                int numColsToAdd = numFieldColumns - this.numFieldColumns;
                this.numFieldColumns = numFieldColumns;                
                for (int i = 0; i < numColsToAdd; i++)
                {
                    this.propertyTable.ColumnStyles.Add(defaultColumnStyle ?? new ColumnStyle(SizeType.Percent, 100F));
                }
            }

            foreach (var property in properties)
            {
                property.Value.ChangeNumberOfFields(numFieldColumns);
            }

            UpdateSizes();
        }

        public void SetFieldColumnStyle(int fieldColumn, ColumnStyle columnStyle)
        {
            this.propertyTable.ColumnStyles[fieldColumn+1] = columnStyle;
        }

        public Property GetProperty(string name)
        {
            if (!properties.TryGetValue(name, out var property))
            {
                property = AddProperty(name);
            }
            return property;
        }

        private Property AddProperty(string name)
        {
            Property property;
            using (this.LayoutSuspension())
            {                
                int rowIndex = propertyTable.RowCount++;

                property = new Property(this, propertyTable, rowIndex, numFieldColumns,  name, sender);
                properties.Add(name, property);                
            }
            sender.Send(new PropertyEventNotification(name, PropertyEvent.Created));
            return property;
        }

        public void SetReadOnly(bool readOnly, string propertyName = null)
        {
            if (propertyName == null)
            {                
                foreach (var property in properties.Values)
                {
                    property.SetReadOnly(readOnly);
                }
            }
            else if (properties.TryGetValue(propertyName, out var property))
            {
                property.SetReadOnly(readOnly);
            }
        }

        private void UpdateSizes()
        {
            using (this.LayoutSuspension())
            {
                int scrollBarOffset = this.Width > propertyTable.PreferredSize.Width ? 0 : 25;
                this.MinimumSize = new Size(0, propertyTable.PreferredSize.Height + scrollBarOffset);
                this.AutoScrollMinSize = propertyTable.PreferredSize;
            }
            
        }

        private void RenameProperty(string name, string newName)
        {
            properties[newName] = properties[name];
            properties.Remove(name);
        }

        public void RemoveProperty(string label) => RemoveProperty(label, true);

        private void RemoveProperty(string label, bool updateRowIndicies)
        {
            var property = properties[label];
            properties.Remove(label);
            using (this.LayoutSuspension())
            {
                int rowIndex = propertyTable.GetPositionFromControl(property.GetLabelControl()).Row;
                propertyTable.RemoveRowAt(rowIndex);
                UpdateSizes();
            }
            if (updateRowIndicies) UpdateRowIndicies();
            sender.Send(new PropertyEventNotification(property.Name, PropertyEvent.Removed));
        }

        private void UpdateRowIndicies()
        {
            foreach(var property in properties.Values)
            {
                property.UpdateRowIndex();
            }
        }

        public IEnumerable<Property> GetProperties()
        {
            return properties.Values.OrderBy(property => property.RowIndex);
        }

        public void Clear()
        {
            properties.Clear();
            using (this.LayoutSuspension())
            {
                propertyTable.RowCount = 0;
                propertyTable.Controls.Clear();
                propertyTable.RowStyles.Clear();
                propertyTable.RowCount = 1;
                propertyTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                UpdateSizes();
            }            
        }

        public void Clear(params string[] exceptions)
        {
            if (exceptions.EmptyOrNull()) Clear();
            else
            {
                using (this.LayoutSuspension())
                {
                    var toRemove = properties.Keys.Except(exceptions).ToArray();
                    foreach (var label in toRemove)
                    {
                        RemoveProperty(label, false);
                    }
                    UpdateRowIndicies();
                    UpdateSizes();
                }
            }            
        }
    }
}