using FeatureLoom.MessageFlow;
using FeatureLoom.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FeatureLoom.Forms
{
    public partial class MultiPropertyControl
    {
        public class Property
        {
            struct Field
            {
                public Control control;
                public bool readOnly;
                public Predicate<string> verifier;
            }

            private static uint nameCount = 0;
            private static string onFocusText = "";

            private string name;
            private MultiPropertyControl parentControl;
            private TableLayoutPanel propertyTable;
            private int rowIndex = -1;
            private Sender<PropertyEventNotification> sender;
            private Label label = new Label();
            private Field[] fields;            
            private RowStyle rowStyle = new RowStyle();
            private bool isVisible = false;

            public Property(MultiPropertyControl parentControl, TableLayoutPanel table, int rowIndex, int numFields, string name, Sender<PropertyEventNotification>  sender)
            {                
                this.parentControl = parentControl;
                this.fields = new Field[numFields];
                for (int i=0; i< fields.Length; i++) fields[i].readOnly = parentControl.readOnlyDefault;
                this.propertyTable = table;
                this.rowIndex = rowIndex;
                this.sender = sender;
                this.name = name;

                this.label.Visible = isVisible;
                this.label.Anchor = System.Windows.Forms.AnchorStyles.Right;
                this.label.AutoSize = true;
                this.label.Location = new System.Drawing.Point(3, 6);
                this.label.Name = "PropertyLabel" + nameCount;
                this.label.Size = new System.Drawing.Size(10, 10);
                this.label.Text = name;

                propertyTable.RowStyles.Insert(rowIndex, rowStyle);
                propertyTable.Controls.Add(label, 0, rowIndex);
                label.Click += (o, e) => sender.Send(new PropertyEventNotification(this.name, PropertyEvent.Clicked));

                nameCount++;

                

                parentControl.UpdateSizes();
            }

            public bool IsVisible
            {
                get => isVisible;
                set 
                {
                    if (isVisible != value)
                    {
                        isVisible = value;
                        label.Visible = isVisible;
                        foreach (var field in fields)
                        {
                            if (field.control != null) field.control.Visible = isVisible;
                        }
                    }
                }
            }

            internal int RowIndex => rowIndex;

            public bool TryFindControl(Control control, out int fieldIndex)
            {
                for(int i = 0; i < fields.Length; i++)
                {
                    if (fields[i].control == control)
                    {
                        fieldIndex = i;
                        return true;
                    }
                }

                fieldIndex = -1;
                return false;
            }

            public void ResetTypedChanges(int fieldIndex)
            {
                Control control = fields[fieldIndex].control;
                if (control != null && control.Focused && control.Text != onFocusText)
                {
                    control.Text = onFocusText;
                    SetCursorToEnd(control);
                }
            }

            internal void MoveControls(int rowIndex)
            {
                if (this.rowIndex == rowIndex) return;

                propertyTable.Controls.Add(label, 0, rowIndex);

                for (int i = 0; i < fields.Length; i++)
                {
                    if (fields[i].control != null)
                    {
                        propertyTable.Controls.Add(fields[i].control, i+1, rowIndex);
                    }
                }
                this.rowIndex = rowIndex;
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
                    fields = new Field[numFields];
                    for (int i = 0; i < numFields; i++)
                    {
                        fields[i] = oldFields[i];
                    }
                }
                else if (numFields > fields.Length)
                {
                    var oldFields = fields;
                    fields = new Field[numFields];
                    for (int i = 0; i < fields.Length; i++)
                    {
                        if (i < oldFields.Length) fields[i] = oldFields[i];
                        else fields[i].readOnly = parentControl.readOnlyDefault;
                    }
                }

                for (int i = 0; i < fields.Length; i++)
                {
                    UpdateTabIndex(i);
                }
            }
            
            public Property MoveToPosition(int position)
            {
                parentControl.MoveProperty(this, position);                
                return this;
            }

            public Label GetLabelControl() => label;

            public string Name => name;

            public int Position => rowIndex;

            internal void UpdateRowIndex()
            {
                rowIndex = propertyTable.GetRow(label);
                for(int i= 0; i < fields.Length; i++)
                {
                    UpdateTabIndex(i);
                }
            }

            private void UpdateTabIndex(int fieldIndex)
            {
                if (fields[fieldIndex].control != null) fields[fieldIndex].control.TabIndex = (rowIndex + 1) * fields.Length + fieldIndex;
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
                this.name = newName;                
            }

            public Control GetFieldControl(int fieldIndex = 0) => fieldIndex < fields.Length ? fields[fieldIndex].control : null;

            public string GetValue(int fieldIndex = 0) => GetFieldControl(fieldIndex)?.Text ?? "";

            public Property SetValue(string value, int fieldIndex = 0)
            {
                if (fieldIndex + 1 > parentControl.numFieldColumns) parentControl.SetNumFieldColumns(fieldIndex + 1);

                if (fields[fieldIndex].control == null)
                {
                    TextBox control = new TextBox();
                    control.Visible = isVisible;
                    fields[fieldIndex].control = control;
                    propertyTable.Controls.Add(control, fieldIndex + 1, rowIndex);

                    UpdateTabIndex(fieldIndex);
                    control.Enabled = !fields[fieldIndex].readOnly;
                    control.Dock = DockStyle.Fill;
                    control.Location = new Point(0, 0);
                    control.Name = "PropertyField" + nameCount;
                    control.Size = new Size(50, 26);                    
                    control.AllowDrop = true;

                    control.TextChanged += (o, e) => sender.Send(new PropertyEventNotification(this.name, PropertyEvent.ValueChanged, fieldIndex, control.Text));
                    control.GotFocus += (o, e) =>
                    {
                        onFocusText = control.Text;
                        sender.Send(new PropertyEventNotification(this.name, PropertyEvent.GotFocus, fieldIndex, control.Text));
                    };
                    control.LostFocus += (o, e) => sender.Send(new PropertyEventNotification(this.name, control.Text == onFocusText ? PropertyEvent.LostFocus : PropertyEvent.LostFocusWithChange, fieldIndex, control.Text));
                    control.Click += (o, e) => sender.Send(new PropertyEventNotification(this.name, PropertyEvent.Clicked, fieldIndex));
                    control.EnabledChanged += (o, e) => sender.Send(new PropertyEventNotification(this.name, PropertyEvent.ReadOnlyChanged, fieldIndex, !control.Enabled));

                    control.TextChanged += (o, e) =>
                    {
                        var measured = TextRenderer.MeasureText(control.Text, control.Font);
                        control.MinimumSize = measured;
                        parentControl.UpdateSizes();

                        this.TriggerVerify(fieldIndex);
                    };

                    control.DragEnter += (o, e) =>
                    {
                        if (e.Data.GetDataPresent(DataFormats.Text))
                            e.Effect = DragDropEffects.Link;
                        else
                            e.Effect = DragDropEffects.None;
                    };

                    control.DragDrop += (o, e) =>
                    {
                        string dropText = e.Data.GetData(DataFormats.Text).ToString();
                        control.Text = dropText;
                        sender.Send(new PropertyEventNotification(this.name, PropertyEvent.DroppedInValue, fieldIndex, control.Text));
                    };

                    control.KeyDown += (o, e) =>
                    {
                        if (e.KeyCode == Keys.Escape)
                        {
                            if (control.Text != onFocusText)
                            {
                                control.Text = onFocusText;
                                SetCursorToEnd(control);
                            }
                            e.Handled = true;
                            e.SuppressKeyPress = true;
                        }
                        else if (e.KeyCode == Keys.Enter)
                        {
                            this.parentControl.SelectNextControl(control, true, true, true, true);
                            e.Handled = true;
                            e.SuppressKeyPress = true;
                        }
                    };
                }

                if (fields[fieldIndex].control.Text != value) fields[fieldIndex].control.Text = value;
                SetCursorToEnd(fields[fieldIndex].control);
                parentControl.UpdateSizes();
                return this;
            }

            private static void SetCursorToEnd(Control control)
            {                
                if (control is TextBoxBase textBox)
                {
                    textBox.SelectionLength = 0;
                    textBox.SelectionStart = control.Text.Length;                    
                }
                else if (control is ComboBox comboBox)
                {
                    //comboBox.SelectionLength = 0;
                    //comboBox.SelectionStart = control.Text.Length;                    
                }
            }
            

            public Property SetValueRestrictions(IEnumerable<string> options, int fieldIndex =  0)
            {
                if (fieldIndex + 1 > parentControl.numFieldColumns) parentControl.SetNumFieldColumns(fieldIndex + 1);

                if (fields[fieldIndex].control == null || !(fields[fieldIndex].control is ComboBox))
                {                    
                    string value = null;
                    if (fields[fieldIndex].control != null) value = fields[fieldIndex].control.Text;
                    
                    RemoveField(fieldIndex);

                    ComboBox control = new ComboBox();
                    control.Visible = isVisible;
                    fields[fieldIndex].control = control;
                    propertyTable.Controls.Add(control, fieldIndex + 1, rowIndex);

                    UpdateTabIndex(fieldIndex);
                    control.Enabled = !fields[fieldIndex].readOnly;
                    control.Dock = DockStyle.Fill;
                    control.Location = new Point(0, 0);
                    control.Name = "PropertyField" + nameCount;
                    control.Size = new Size(50, 26);                    
                    control.AllowDrop = true;                    
                    control.DropDownStyle = ComboBoxStyle.DropDown;                    
                    control.DataSource = options.ToArray(); // make a copy
                    control.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                    control.AutoCompleteSource = AutoCompleteSource.ListItems;                                        

                    control.TextChanged += (o, e) =>
                    {
                        if (control.DataSource is IEnumerable<string> fieldOptions && fieldOptions.Contains(control.Text))
                        {
                            sender.Send(new PropertyEventNotification(this.name, PropertyEvent.ValueChanged, fieldIndex, control.Text));                                 
                        }
                        var measured = TextRenderer.MeasureText(control.Text, control.Font);
                        control.MinimumSize = measured;
                        parentControl.UpdateSizes();
                        this.TriggerVerify(fieldIndex);
                    };

                    control.GotFocus += (o, e) =>
                    {
                        onFocusText = control.Text;
                        sender.Send(new PropertyEventNotification(this.name, PropertyEvent.GotFocus, fieldIndex, control.Text));
                    };

                    control.LostFocus += (o, e) =>
                    {
                        if (control.DataSource is IEnumerable<string> fieldOptions && fieldOptions.Contains(control.Text))
                        {                            
                            sender.Send(new PropertyEventNotification(this.name, control.Text == onFocusText ? PropertyEvent.LostFocus : PropertyEvent.LostFocusWithChange, fieldIndex, control.Text));
                        }
                        else control.Text = onFocusText;
                    };

                    control.SelectedIndexChanged += (o, e) =>
                    {
                        if (control.DataSource is IEnumerable<string> fieldOptions && fieldOptions.Contains(control.Text))
                        {
                            sender.Send(new PropertyEventNotification(this.name, PropertyEvent.SelectionChanged, fieldIndex, control.Text));
                        }
                        else control.Text = onFocusText;
                    };

                    control.Click += (o, e) => sender.Send(new PropertyEventNotification(this.name, PropertyEvent.Clicked, fieldIndex));
                    control.EnabledChanged += (o, e) => sender.Send(new PropertyEventNotification(this.name, PropertyEvent.ReadOnlyChanged, fieldIndex, !control.Enabled));

                    if (value != null) control.Text = value;
                    else if (control.Items.Count > 0) control.SelectedIndex = 0;

                    control.DragEnter += (o, e) =>
                    {
                        if (e.Data.GetDataPresent(DataFormats.Text)) e.Effect = DragDropEffects.Link;
                        else e.Effect = DragDropEffects.None;
                    };

                    control.DragDrop += (o, e) =>
                    {
                        string dropText = e.Data.GetData(DataFormats.Text).ToString();
                        control.Text = dropText;
                        sender.Send(new PropertyEventNotification(this.name, PropertyEvent.DroppedInValue, fieldIndex, control.Text));
                    };

                    control.KeyDown += (o, e) =>
                    {
                        if (e.KeyCode == Keys.Escape)
                        {
                            if (control.Text != onFocusText)
                            {
                                control.Text = onFocusText;
                                SetCursorToEnd(control);
                            }                            
                            e.Handled = true;
                            e.SuppressKeyPress = true;
                        }
                        else if (e.KeyCode == Keys.Enter)
                        {
                            this.parentControl.SelectNextControl(control, true, true, true, true);
                            e.Handled = true;
                            e.SuppressKeyPress = true;
                        }
                    };

                    parentControl.UpdateSizes();
                }
                else
                {
                    ComboBox control = fields[fieldIndex].control as ComboBox;                    
                    control.DataSource = options.ToArray();
                    //control.Text = "";
                    if (control.Items.Count > 0) control.SelectedIndex = 0;

                    parentControl.UpdateSizes();
                }

                SetCursorToEnd(fields[fieldIndex].control);

                return this;
            }

            public Property SetCustomFieldControl(Control customControl, int fieldIndex = 0)
            {
                if (fieldIndex + 1 > parentControl.numFieldColumns) parentControl.SetNumFieldColumns(fieldIndex + 1);

                RemoveField(fieldIndex);

                var control = customControl;
                control.Visible = isVisible;
                fields[fieldIndex].control = control;                
                propertyTable.Controls.Add(control, fieldIndex + 1, rowIndex);

                control.Enabled = !fields[fieldIndex].readOnly;

                UpdateTabIndex(fieldIndex);

                control.TextChanged += (o, e) => sender.Send(new PropertyEventNotification(this.name, PropertyEvent.ValueChanged, fieldIndex, control.Text));                
                control.LostFocus += (o, e) => sender.Send(new PropertyEventNotification(this.name, control.Text == onFocusText ? PropertyEvent.LostFocus : PropertyEvent.LostFocusWithChange, fieldIndex, control.Text));
                control.Click += (o, e) => sender.Send(new PropertyEventNotification(this.name, PropertyEvent.Clicked, fieldIndex));
                control.EnabledChanged += (o, e) => sender.Send(new PropertyEventNotification(this.name, PropertyEvent.ReadOnlyChanged, fieldIndex, !control.Enabled));

                control.GotFocus += (o, e) =>
                {
                    onFocusText = control.Text;
                    sender.Send(new PropertyEventNotification(this.name, PropertyEvent.GotFocus, fieldIndex, control.Text));
                };

                control.KeyDown += (o, e) =>
                {
                    if (e.KeyCode == Keys.Escape)
                    {
                        if (control.Text != onFocusText)
                        {
                            control.Text = onFocusText;                            
                        }
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                    }
                };

                parentControl.UpdateSizes();
                return this;
            }

            public Property SetReadOnly(bool readOnly, int fieldIndex)
            {
                if (fieldIndex + 1 > parentControl.numFieldColumns) parentControl.SetNumFieldColumns(fieldIndex + 1);

                fields[fieldIndex].readOnly = readOnly;
                Control control = fields[fieldIndex].control;
                if (control != null) control.Enabled = !readOnly;
                return this;
            }

            public Property SetReadOnly(bool readOnly)
            {
                for(int i=0; i < fields.Length; i++)
                {
                    fields[i].readOnly = readOnly;
                    if (fields[i].control != null) fields[i].control.Enabled = !readOnly;
                }
                return this;
            }

            public Property RemoveField(int fieldIndex = 0)
            {
                if (fieldIndex < fields.Length && fields[fieldIndex].control != null)
                {
                    propertyTable.Controls.Remove(fields[fieldIndex].control);
                    fields[fieldIndex] = new Field();                    
                }
                return this;
            }

            public Property SetVerifier(Predicate<string> verifier, int fieldIndex = 0)
            {
                if (fieldIndex + 1 > parentControl.numFieldColumns) parentControl.SetNumFieldColumns(fieldIndex + 1);

                fields[fieldIndex].verifier = verifier;
                TriggerVerify(fieldIndex);
                return this;
            }

            private void TriggerVerify(int fieldIndex = 0)
            {
                if (fieldIndex >= fields.Length) return;

                var control = fields[fieldIndex].control;
                var verifier = fields[fieldIndex].verifier;

                bool good = true;

                if (control == null) return;
                else if (verifier != null && !verifier(control.Text)) good = false;
                else if (control is ComboBox comboBox && comboBox.DataSource is IEnumerable<string> options && !options.Contains(control.Text)) good = false;

                if (good)
                {
                    control.BackColor = Color.Empty;
                    sender.Send(new PropertyEventNotification(this.name, PropertyEvent.VerifyGood, fieldIndex, control.Text));
                }
                else
                {
                    control.BackColor = Color.LightPink;
                    sender.Send(new PropertyEventNotification(this.name, PropertyEvent.VerifyBad, fieldIndex, control.Text));
                }
            }

        }
    }
}