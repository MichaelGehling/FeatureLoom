using FeatureLoom.MessageFlow;
using FeatureLoom.Extensions;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Collections;

namespace FeatureLoom.Forms
{
    public partial class MultiPropertyControl : UserControl
    {
        private int numFieldColumns = 0;
        private bool readOnlyDefault = false;
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
            LostFocusWithChange,
            SelectionChanged,
            DroppedInValue,
            ReadOnlyChanged,
            VerifyGood,
            VerifyBad            
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

        public int CountProperties => properties.Count;

        public bool ExistsProperty(string name) => properties.ContainsKey(name);

        public bool TryGetProperty(string name, out Property property) => properties.TryGetValue(name, out property);

        private void MoveProperty(Property property, int targetRowIndex)
        {
            if (property.Position != targetRowIndex)
            {
                using (this.LayoutSuspension())
                {
                    var rowStyle = propertyTable.RowStyles[property.Position];
                    propertyTable.RemoveRowAt(property.Position);
                    propertyTable.InsertRowAt(targetRowIndex, new RowStyle(rowStyle.SizeType, rowStyle.Height));
                    property.MoveControls(targetRowIndex);
                    UpdateRowIndicies();
                }
            }
            else
            {
                // already at the right position
            }
        }

        private Property AddProperty(string name)
        {
            Property property;
            using (this.LayoutSuspension())
            {                
                int rowIndex = propertyTable.RowCount-1;
                propertyTable.RowCount++;

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
                readOnlyDefault = readOnly;
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

        public void ResetTypedChanges()
        {
            var control = this.FindFocusedControl();     
            if (control != null && control.FindParent<MultiPropertyControl>() == this)
            {
                foreach(Property property in properties.Values)
                {
                    if (property.TryFindControl(control, out int fieldIndex))
                    {
                        property.ResetTypedChanges(fieldIndex);
                    }
                }
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
            return properties.Values.OrderBy(property => property.Position);
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
            Clear(exceptions as IEnumerable<string>);
        }

        public void Clear(IEnumerable<string> exceptions)
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