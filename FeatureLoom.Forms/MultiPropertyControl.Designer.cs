namespace FeatureLoom.Forms
{
    partial class MultiPropertyControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if(disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.propertyTable = new System.Windows.Forms.TableLayoutPanel();
            this.SuspendLayout();
            // 
            // propertyTable
            // 
            this.propertyTable.ColumnCount = 3;
            this.propertyTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.propertyTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.propertyTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.propertyTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertyTable.Location = new System.Drawing.Point(0, 0);
            this.propertyTable.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.propertyTable.Name = "propertyTable";
            this.propertyTable.RowCount = 1;
            this.propertyTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.propertyTable.Size = new System.Drawing.Size(0, 0);
            this.propertyTable.TabIndex = 0;
            // 
            // MultiPropertyControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 24F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.propertyTable);
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "MultiPropertyControl";
            this.Size = new System.Drawing.Size(0, 0);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel propertyTable;
    }
}
