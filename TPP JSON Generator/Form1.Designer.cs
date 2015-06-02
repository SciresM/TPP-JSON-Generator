namespace TPP_JSON
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
            this.B_Go = new System.Windows.Forms.Button();
            this.PB_Progress = new System.Windows.Forms.ProgressBar();
            this.TB_In = new System.Windows.Forms.TextBox();
            this.B_Open = new System.Windows.Forms.Button();
            this.CHK_TPPArceusFix = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // B_Go
            // 
            this.B_Go.Enabled = false;
            this.B_Go.Location = new System.Drawing.Point(338, 12);
            this.B_Go.Name = "B_Go";
            this.B_Go.Size = new System.Drawing.Size(86, 23);
            this.B_Go.TabIndex = 0;
            this.B_Go.Text = "Go!";
            this.B_Go.UseVisualStyleBackColor = true;
            this.B_Go.Click += new System.EventHandler(this.B_Go_Click);
            // 
            // PB_Progress
            // 
            this.PB_Progress.Location = new System.Drawing.Point(12, 46);
            this.PB_Progress.Name = "PB_Progress";
            this.PB_Progress.Size = new System.Drawing.Size(412, 23);
            this.PB_Progress.TabIndex = 1;
            // 
            // TB_In
            // 
            this.TB_In.Location = new System.Drawing.Point(13, 13);
            this.TB_In.Name = "TB_In";
            this.TB_In.ReadOnly = true;
            this.TB_In.Size = new System.Drawing.Size(231, 20);
            this.TB_In.TabIndex = 2;
            // 
            // B_Open
            // 
            this.B_Open.Location = new System.Drawing.Point(249, 12);
            this.B_Open.Name = "B_Open";
            this.B_Open.Size = new System.Drawing.Size(86, 23);
            this.B_Open.TabIndex = 3;
            this.B_Open.Text = "Open";
            this.B_Open.UseVisualStyleBackColor = true;
            this.B_Open.Click += new System.EventHandler(this.B_Open_Click);
            // 
            // CHK_TPPArceusFix
            // 
            this.CHK_TPPArceusFix.AutoSize = true;
            this.CHK_TPPArceusFix.Location = new System.Drawing.Point(13, 76);
            this.CHK_TPPArceusFix.Name = "CHK_TPPArceusFix";
            this.CHK_TPPArceusFix.Size = new System.Drawing.Size(196, 17);
            this.CHK_TPPArceusFix.TabIndex = 4;
            this.CHK_TPPArceusFix.Text = "Twitch Plays Pokemon Arceus \"Fix\"";
            this.CHK_TPPArceusFix.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(436, 98);
            this.Controls.Add(this.CHK_TPPArceusFix);
            this.Controls.Add(this.B_Open);
            this.Controls.Add(this.TB_In);
            this.Controls.Add(this.PB_Progress);
            this.Controls.Add(this.B_Go);
            this.Name = "Form1";
            this.Text = "TPP Platinum Save -> JSON Generator";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button B_Go;
        private System.Windows.Forms.ProgressBar PB_Progress;
        private System.Windows.Forms.TextBox TB_In;
        private System.Windows.Forms.Button B_Open;
        private System.Windows.Forms.CheckBox CHK_TPPArceusFix;
    }
}

