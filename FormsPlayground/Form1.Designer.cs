
namespace FormsPlayground
{
    partial class Form1
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
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.multiPropertyControl1 = new FeatureLoom.Forms.MultiPropertyControl(2);
            this.button1 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // multiPropertyControl1
            // 
            this.multiPropertyControl1.AutoScroll = true;
            this.multiPropertyControl1.AutoSize = true;
            this.multiPropertyControl1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.multiPropertyControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.multiPropertyControl1.Location = new System.Drawing.Point(0, 0);
            this.multiPropertyControl1.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            this.multiPropertyControl1.Name = "multiPropertyControl1";            
            this.multiPropertyControl1.Size = new System.Drawing.Size(978, 540);
            this.multiPropertyControl1.TabIndex = 0;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(753, 93);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 1;
            this.button1.Text = "button1";
            this.button1.UseVisualStyleBackColor = true;

            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 24F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(978, 540);
            this.Controls.Add(this.multiPropertyControl1);
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "Form1";
            this.Text = "Form1";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private FeatureLoom.Forms.MultiPropertyControl multiPropertyControl1;
        private System.Windows.Forms.Button button1;
    }
}

