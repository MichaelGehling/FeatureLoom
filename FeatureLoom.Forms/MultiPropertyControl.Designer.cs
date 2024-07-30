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
            this.pageControlsPanel = new System.Windows.Forms.Panel();
            this.buttonScrollAllDown = new System.Windows.Forms.Button();
            this.buttonScrollAllUp = new System.Windows.Forms.Button();
            this.buttonScrollPageDown = new System.Windows.Forms.Button();
            this.buttonScrollPageUp = new System.Windows.Forms.Button();
            this.buttonScrollItemDown = new System.Windows.Forms.Button();
            this.buttonScrollItemUp = new System.Windows.Forms.Button();

            this.SuspendLayout();
            // 
            // propertyTable
            // 
            this.propertyTable.ColumnCount = 1;
            this.propertyTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.propertyTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertyTable.Location = new System.Drawing.Point(0, 50); // Adjusted starting Y position
            this.propertyTable.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.propertyTable.Name = "propertyTable";
            this.propertyTable.RowCount = 1;
            this.propertyTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.propertyTable.Size = new System.Drawing.Size(800, 450); // Example size
            this.propertyTable.TabIndex = 1;
            // 
            // pageControlsPanel
            // 
            this.pageControlsPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.pageControlsPanel.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.pageControlsPanel.Name = "pageControlsPanel";
            this.pageControlsPanel.Size = new System.Drawing.Size(800, 50); // Set a fixed height
            this.pageControlsPanel.TabIndex = 0;
            this.pageControlsPanel.Controls.Add(this.buttonScrollAllUp);
            this.pageControlsPanel.Controls.Add(this.buttonScrollPageUp);
            this.pageControlsPanel.Controls.Add(this.buttonScrollItemUp);
            this.pageControlsPanel.Controls.Add(this.buttonScrollItemDown);
            this.pageControlsPanel.Controls.Add(this.buttonScrollPageDown);
            this.pageControlsPanel.Controls.Add(this.buttonScrollAllDown);
            // 
            // buttonScrollItemUp
            // 
            this.buttonScrollItemUp.Location = new System.Drawing.Point(150, 10);
            this.buttonScrollItemUp.Name = "buttonScrollItemUp";
            this.buttonScrollItemUp.Size = new System.Drawing.Size(60, 40); // Set size
            this.buttonScrollItemUp.Text = "<";
            this.buttonScrollItemUp.UseVisualStyleBackColor = true;
            // 
            // buttonScrollItemDown
            // 
            this.buttonScrollItemDown.Location = new System.Drawing.Point(220, 10);
            this.buttonScrollItemDown.Name = "buttonScrollItemDown";
            this.buttonScrollItemDown.Size = new System.Drawing.Size(60, 40); // Set size
            this.buttonScrollItemDown.Text = ">";
            this.buttonScrollItemDown.UseVisualStyleBackColor = true;
            // 
            // buttonScrollPageUp
            // 
            this.buttonScrollPageUp.Location = new System.Drawing.Point(80, 10);
            this.buttonScrollPageUp.Name = "buttonScrollPageUp";
            this.buttonScrollPageUp.Size = new System.Drawing.Size(60, 40); // Set size
            this.buttonScrollPageUp.Text = "<<";
            this.buttonScrollPageUp.UseVisualStyleBackColor = true;
            // 
            // buttonScrollPageDown
            // 
            this.buttonScrollPageDown.Location = new System.Drawing.Point(290, 10);
            this.buttonScrollPageDown.Name = "buttonScrollPageDown";
            this.buttonScrollPageDown.Size = new System.Drawing.Size(60, 40); // Set size
            this.buttonScrollPageDown.Text = ">>";
            this.buttonScrollPageDown.UseVisualStyleBackColor = true;
            // 
            // buttonScrollAllUp
            // 
            this.buttonScrollAllUp.Location = new System.Drawing.Point(10, 10);
            this.buttonScrollAllUp.Name = "buttonScrollAllUp";
            this.buttonScrollAllUp.Size = new System.Drawing.Size(60, 40); // Set size
            this.buttonScrollAllUp.Text = "<<<";
            this.buttonScrollAllUp.UseVisualStyleBackColor = true;
            // 
            // buttonScrollAllDown
            // 
            this.buttonScrollAllDown.Location = new System.Drawing.Point(360, 10);
            this.buttonScrollAllDown.Name = "buttonScrollAllDown";
            this.buttonScrollAllDown.Size = new System.Drawing.Size(60, 40); // Set size
            this.buttonScrollAllDown.Text = ">>>";
            this.buttonScrollAllDown.UseVisualStyleBackColor = true;
            // 
            // MultiPropertyControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 24F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.propertyTable);
            this.Controls.Add(this.pageControlsPanel);
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "MultiPropertyControl";
            this.Size = new System.Drawing.Size(800, 500); // Set a reasonable default size
            this.ResumeLayout(false);
        }




        #endregion
        private System.Windows.Forms.Panel pageControlsPanel;
        private System.Windows.Forms.Button buttonScrollItemUp;
        private System.Windows.Forms.Button buttonScrollItemDown;
        private System.Windows.Forms.Button buttonScrollPageUp;
        private System.Windows.Forms.Button buttonScrollPageDown;
        private System.Windows.Forms.Button buttonScrollAllUp;
        private System.Windows.Forms.Button buttonScrollAllDown;
        private System.Windows.Forms.TableLayoutPanel propertyTable;
    }
}
