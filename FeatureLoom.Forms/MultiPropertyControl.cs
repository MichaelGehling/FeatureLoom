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
        private class Property
        {
            public Label label = new Label();
            public ComboBox field = new ComboBox();
            public Control extension = null;
            public RowStyle rowStyle = new RowStyle();            
            public Predicate<string> verifier = null;
            private static uint count = 0;

            public Property(string labelText, bool readOnly, Sender<PropertyEventNotification>  sender, Control extensionControl = null)
            {
                this.extension = extensionControl;

                count++;

                this.label.Anchor = System.Windows.Forms.AnchorStyles.Right;
                this.label.AutoSize = true;
                this.label.Location = new System.Drawing.Point(3, 6);
                this.label.Name = "PropertyLabel" + count;
                this.label.Size = new System.Drawing.Size(10, 10);
                //this.label.TabIndex = 0;
                this.label.Text = labelText;

                this.field.Dock = System.Windows.Forms.DockStyle.Fill;
                this.field.Location = new System.Drawing.Point(60, 3);
                this.field.Name = "PropertyTextbox" + count;
                this.field.Size = new System.Drawing.Size(50, 26);
                this.field.TabIndex = 1;
                this.field.Enabled = !readOnly;                
                this.field.AllowDrop = true;

                UpdateExtension(extensionControl);

                field.TextChanged += (o, e) =>
                {
                    var measured = TextRenderer.MeasureText(field.Text, field.Font);
                    field.MinimumSize = measured;
                    field.FindParent<MultiPropertyControl>()?.UpdateSizes();

                    this.TriggerVerify();
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
                    sender.Send(new PropertyEventNotification(label.Text, PropertyEvent.DroppedInValue, field.Text));
                };
            }

            public void UpdateExtension(Control newExtension)
            {
                if (extension != newExtension)
                {
                    extension = newExtension;
                    if (extension != null)
                    {
                        this.extension.Dock = DockStyle.Fill;
                        this.extension.Enabled = field.Enabled;
                    }
                }
            }

            public void TriggerVerify()
            {
                if (verifier != null && !verifier(field.Text)) field.BackColor = Color.LightPink;
                else field.BackColor = Color.Empty;
            }
        }

        private Dictionary<string, Property> properties = new Dictionary<string, Property>();
        private bool readOnly = false;
        public bool ReadOnly
        {
            get => readOnly;
            set 
            {
                readOnly = value;
                foreach(var property in properties.Values)
                {
                    property.field.Enabled = !readOnly;
                    if (property.extension != null) property.extension.Enabled = !readOnly;
                }
            }
        }
        private Sender<PropertyEventNotification> sender = new Sender<PropertyEventNotification>();
        public IMessageSource<PropertyEventNotification> PropertyEventNotifier => sender;

        public class PropertyEventNotification
        {
            public PropertyEventNotification(string propertyName, PropertyEvent @event, object parameter = null)
            {
                PropertyName = propertyName;
                Event = @event;
                Parameter = parameter;
            }

            public string PropertyName { get; }
            public PropertyEvent Event { get; }
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

        public MultiPropertyControl()
        {
            InitializeComponent();

            Resize += (o, e) => UpdateSizes();
        }

        private void ConnectPropertyEvents(Property property)
        {
            property.field.TextChanged += (o, e) => sender.Send(new PropertyEventNotification(property.label.Text, PropertyEvent.ValueChanged, property.field.Text));
            property.field.GotFocus += (o, e) => sender.Send(new PropertyEventNotification(property.label.Text, PropertyEvent.GotFocus, property.field.Text));
            property.field.LostFocus += (o, e) => sender.Send(new PropertyEventNotification(property.label.Text, PropertyEvent.LostFocus, property.field.Text));
            property.field.Click += (o, e) => sender.Send(new PropertyEventNotification(property.label.Text, PropertyEvent.Clicked));
            property.label.Click += (o, e) => sender.Send(new PropertyEventNotification(property.label.Text, PropertyEvent.Clicked));
            property.field.EnabledChanged += (o, e) => sender.Send(new PropertyEventNotification(property.label.Text, PropertyEvent.ReadOnlyChanged, !property.field.Enabled));
        }

        private void AddProperty(string label, string value, Control extensionControl)
        {
            Property property = new Property(label, readOnly, sender, extensionControl);
            property.field.Text = value;
            properties.Add(label, property);

            int rowIndex = propertyTable.RowCount - 1;

            using (this.LayoutSuspension())
            {
                propertyTable.RowCount++;
                propertyTable.RowStyles.Insert(rowIndex, property.rowStyle);
                propertyTable.Controls.Add(property.label, 0, rowIndex);
                propertyTable.Controls.Add(property.field, 1, rowIndex);
                if (property.extension != null) propertyTable.Controls.Add(property.extension, 2, rowIndex);

                UpdateSizes();
            }

            ConnectPropertyEvents(property);
            sender.Send(new PropertyEventNotification(label, PropertyEvent.Created, value));
        }

        public void SetReadOnly(bool readOnly, string label = null)
        {
            if (label == null)
            {
                this.readOnly = readOnly;
                foreach (var property in properties.Values)
                {
                    property.field.Enabled = !readOnly;
                }
            }
            else if (properties.TryGetValue(label, out var property))
            {
                property.field.Enabled = !readOnly;
            }
        }

        public void SetProperty(string label, string value, Control extensionControl = null)
        {
            if (properties.TryGetValue(label, out var property))
            {
                property.field.Text = value;
                property.TriggerVerify();
                if (extensionControl != null) property.UpdateExtension(extensionControl);                
            }
            else AddProperty(label, value, extensionControl);
        }

        public void SetProperty(string label, string value, Predicate<string> verifier, Control extensionControl = null)
        {
            SetProperty(label, value, extensionControl);
            SetPropertyVerifier(label, verifier);
        }

        public void SetProperty(string label, string value, bool readOnly, Predicate<string> verifier, Control extensionControl = null)
        {
            SetProperty(label, value, readOnly, extensionControl);
            SetPropertyVerifier(label, verifier);
        }

        public void SetProperty(string label, string value, bool readOnly, Control extensionControl = null)
        {
            SetProperty(label, value, extensionControl);
            SetReadOnly(readOnly, label);
        }

        public void SetPropertySelectionList(string label, IEnumerable<string> options, bool restrictToOptions)
        {
            if (properties.TryGetValue(label, out var property))
            {
                property.field.DataSource = options;
                if (restrictToOptions) property.field.DropDownStyle = ComboBoxStyle.DropDownList;
                else property.field.DropDownStyle = ComboBoxStyle.DropDown;
            }
        }

        public void SetPropertyVerifier(string label, Predicate<string> verifier)
        {
            if (properties.TryGetValue(label, out var property))
            {
                property.verifier = verifier;
                property.TriggerVerify();
            }
        }

        private void UpdateSizes()
        {
            int scrollBarOffset = this.Width > propertyTable.PreferredSize.Width ? 0 : 25;
            this.MinimumSize = new Size(0, propertyTable.PreferredSize.Height + scrollBarOffset);
            this.AutoScrollMinSize = propertyTable.PreferredSize;
        }

        public string GetProperty(string label)
        {
            if (properties.TryGetValue(label, out var property)) return property.field.Text;
            else return null;
        }

        public ComboBox GetFieldControl(string label)
        {
            if (properties.TryGetValue(label, out var property)) return property.field;
            else return null;
        }

        public Label GetLabelControl(string label)
        {
            if (properties.TryGetValue(label, out var property)) return property.label;
            else return null;
        }

        public Control GetExtension(string label)
        {
            if (properties.TryGetValue(label, out var property)) return property.extension;
            else return null;
        }

        public void RemoveProperty(string label)
        {
            var property = properties[label];
            properties.Remove(label);
            using (this.LayoutSuspension())
            {
                int rowIndex = propertyTable.GetPositionFromControl(property.label).Row;
                propertyTable.RemoveRowAt(rowIndex);
                UpdateSizes();
            }

            sender.Send(new PropertyEventNotification(property.label.Text, PropertyEvent.Removed, property.field.Text));
        }

        public IEnumerable<string> GetPropertyLabels() => properties.Keys;

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
                        RemoveProperty(label);
                    }
                    UpdateSizes();
                }
            }            
        }
    }
}