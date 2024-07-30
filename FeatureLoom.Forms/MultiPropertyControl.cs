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
        private int visiblePropertyOffset = 0;
        private int pageSize = 30;
        private bool autoPageResize = true;
        private bool showPageControl = true;

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
            buttonScrollAllUp.Click += (o, e) =>
            {
                visiblePropertyOffset = 0;
                UpdateVisibility();
            };
            buttonScrollPageUp.Click += (o, e) =>
            {
                visiblePropertyOffset = (visiblePropertyOffset - pageSize).ClampLow(0);
                UpdateVisibility();
            };
            buttonScrollItemUp.Click += (o, e) =>
            {
                visiblePropertyOffset = (visiblePropertyOffset - 1).ClampLow(0);
                UpdateVisibility();
            };
            buttonScrollItemDown.Click += (o, e) =>
            {
                visiblePropertyOffset = (visiblePropertyOffset + 1).ClampHigh(properties.Count - pageSize);
                UpdateVisibility();
            };
            buttonScrollPageDown.Click += (o, e) =>
            {
                visiblePropertyOffset = (visiblePropertyOffset + pageSize).ClampHigh(properties.Count - pageSize);
                UpdateVisibility();
            };
            buttonScrollAllDown.Click += (o, e) =>
            {
                visiblePropertyOffset = properties.Count - pageSize;
                UpdateVisibility();
            };

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

        public bool EditEnabled
        {
            get => this.propertyTable.Enabled;
            set => this.propertyTable.Enabled = value;
        }

        public int CountProperties => properties.Count;

        public int PageSize
        {
            get => pageSize;
            set
            {
                if (pageSize != value)
                {
                    pageSize = value;
                    UpdateVisibility();
                }
            }
        }

        public int VisiblePropertyOffset
        {
            get => visiblePropertyOffset;
            set
            {
                if (visiblePropertyOffset != value)
                {
                    visiblePropertyOffset = value.Clamp(0, properties.Count - pageSize);
                    UpdateVisibility();
                }
            }
        }

        public bool AutoPageResize
        {
            get => autoPageResize;
            set
            {
                autoPageResize = value;
                UpdateSizes();
            }
        }

        public bool ShowPageControl
        {
            get => showPageControl;
            set
            {
                showPageControl = value;  
                this.pageControlsPanel.Visible = showPageControl;
                UpdateSizes();
            }
        }

        public bool ExistsProperty(string name) => properties.ContainsKey(name);

        public bool TryGetProperty(string name, out Property property) => properties.TryGetValue(name, out property);

        private void MoveProperty(Property property, int targetRowIndex)
        {
            if (property.Position != targetRowIndex)
            {
                //using (this.LayoutSuspension())
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

        private void UpdateVisibility()
        {
            using (this.LayoutSuspension())
            {
                visiblePropertyOffset = visiblePropertyOffset.Clamp(0, properties.Count - pageSize);
                this.pageControlsPanel.Visible = properties.Count > pageSize ? showPageControl : false;
                int firstVisible = visiblePropertyOffset;
                int lastVisible = visiblePropertyOffset + pageSize - 1;
                foreach (var property in properties.Values)
                {
                    property.IsVisible = property.RowIndex >= firstVisible && property.RowIndex <= lastVisible;
                }
            }
        }

        private Property AddProperty(string name)
        {
            Property property;
            //using (this.LayoutSuspension())
            {                
                int rowIndex = propertyTable.RowCount-1;
                propertyTable.RowCount++;

                property = new Property(this, propertyTable, rowIndex, numFieldColumns,  name, sender);
                properties.Add(name, property);                
            }
            UpdateVisibility();
            UpdateSizes();
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

            if (autoPageResize)
            {
                int tableHeight = this.propertyTable.PreferredSize.Height;
                int spaceForTable = this.Height - (this.pageControlsPanel.Visible ? this.pageControlsPanel.Height : 0);

                while (tableHeight < spaceForTable && pageSize < properties.Count)
                {
                    pageSize += 1;
                    UpdateVisibility();
                    tableHeight = this.propertyTable.PreferredSize.Height;
                    spaceForTable = this.Height - (this.pageControlsPanel.Visible ? this.pageControlsPanel.Height : 0);
                }

                while (tableHeight > spaceForTable && pageSize > 0)
                {
                    pageSize -= 1;
                    UpdateVisibility();
                    tableHeight = this.propertyTable.PreferredSize.Height;
                    spaceForTable = this.Height - (this.pageControlsPanel.Visible ? this.pageControlsPanel.Height : 0);
                }
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
            //using (this.LayoutSuspension())
            {
                int rowIndex = propertyTable.GetPositionFromControl(property.GetLabelControl()).Row;
                if (rowIndex >= 0) propertyTable.RemoveRowAt(rowIndex);
                if (updateRowIndicies) UpdateSizes();
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
            UpdateVisibility();
        }

        public IEnumerable<Property> GetProperties()
        {
            return properties.Values.OrderBy(property => property.Position);
        }

        public void Clear()
        {
            properties.Clear();
            //using (this.LayoutSuspension())
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
                //using (this.LayoutSuspension())
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