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
            public TextBox textbox = new TextBox();
            public Control extension = null;
            public RowStyle rowStyle = new RowStyle();            
            public Predicate<string> verifier = null;
            private static uint count = 0;

            public Property(string labelText, bool readOnly, Control extensionControl = null)
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

                this.textbox.Dock = System.Windows.Forms.DockStyle.Fill;
                this.textbox.Location = new System.Drawing.Point(60, 3);
                this.textbox.Name = "PropertyTextbox" + count;
                this.textbox.Size = new System.Drawing.Size(50, 26);
                this.textbox.TabIndex = 1;
                this.textbox.ReadOnly = readOnly;
                this.textbox.Multiline = true;
                this.textbox.WordWrap = true;
                this.textbox.AcceptsReturn = true;

                UpdateExtension(extensionControl);

                textbox.TextChanged += (o, e) =>
                {
                    var measured = TextRenderer.MeasureText(textbox.Text, textbox.Font);
                    textbox.MinimumSize = measured;
                    textbox.FindParent<MultiPropertyControl>()?.UpdateSizes();

                    this.TriggerVerify();
                };
            }

            public void UpdateExtension(Control newExtension)
            {
                if (extension != newExtension)
                {
                    extension = newExtension;
                    if (extension != null)
                    {
                        this.extension.Dock = System.Windows.Forms.DockStyle.Fill;
                    }
                }
            }

            public void TriggerVerify()
            {
                if (verifier != null && !verifier(textbox.Text)) textbox.BackColor = Color.LightPink;
                else textbox.BackColor = TextBox.DefaultBackColor;
            }
        }

        private Dictionary<string, Property> properties = new Dictionary<string, Property>();
        public bool readOnly = false;
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
            ReadOnlyChanged
        }

        public MultiPropertyControl()
        {
            InitializeComponent();

            Resize += (o, e) => UpdateSizes();
        }

        private void ConnectPropertyEvents(Property property)
        {
            property.textbox.TextChanged += (o, e) => sender.Send(new PropertyEventNotification(property.label.Text, PropertyEvent.ValueChanged, property.textbox.Text));
            property.textbox.GotFocus += (o, e) => sender.Send(new PropertyEventNotification(property.label.Text, PropertyEvent.GotFocus, property.textbox.Text));
            property.textbox.LostFocus += (o, e) => sender.Send(new PropertyEventNotification(property.label.Text, PropertyEvent.LostFocus, property.textbox.Text));
            property.textbox.Click += (o, e) => sender.Send(new PropertyEventNotification(property.label.Text, PropertyEvent.Clicked));
            property.label.Click += (o, e) => sender.Send(new PropertyEventNotification(property.label.Text, PropertyEvent.Clicked));
            property.textbox.ReadOnlyChanged += (o, e) => sender.Send(new PropertyEventNotification(property.label.Text, PropertyEvent.ReadOnlyChanged, property.textbox.ReadOnly));
        }

        private void AddProperty(string label, string value, Control extensionControl)
        {
            Property property = new Property(label, readOnly, extensionControl);
            property.textbox.Text = value;
            properties.Add(label, property);

            int rowIndex = propertyTable.RowCount - 1;

            using (this.LayoutSuspension())
            {
                propertyTable.RowCount++;
                propertyTable.RowStyles.Insert(rowIndex, property.rowStyle);
                propertyTable.Controls.Add(property.label, 0, rowIndex);
                propertyTable.Controls.Add(property.textbox, 1, rowIndex);
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
                    property.textbox.ReadOnly = readOnly;
                }
            }
            else if (properties.TryGetValue(label, out var property))
            {
                property.textbox.ReadOnly = readOnly;
            }
        }

        public void SetProperty(string label, string value, Control extensionControl = null)
        {
            if (properties.TryGetValue(label, out var property))
            {
                property.textbox.Text = value;
                property.UpdateExtension(extensionControl);
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
            if (properties.TryGetValue(label, out var property)) return property.textbox.Text;
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

            sender.Send(new PropertyEventNotification(property.label.Text, PropertyEvent.Removed, property.textbox.Text));
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
                }
            }
        }
    }
}