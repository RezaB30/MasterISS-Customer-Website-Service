
namespace RadiusR.API.Test_Unit
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
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.PartnerAuthUsernameBox = new System.Windows.Forms.TextBox();
            this.PartnerAuthPasswordBox = new System.Windows.Forms.TextBox();
            this.AuthBtn = new System.Windows.Forms.Button();
            this.AuthenticateResultLbl = new System.Windows.Forms.Label();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Location = new System.Drawing.Point(12, 12);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(870, 521);
            this.tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            this.tabPage1.BackColor = System.Drawing.Color.LightGray;
            this.tabPage1.Controls.Add(this.AuthenticateResultLbl);
            this.tabPage1.Controls.Add(this.AuthBtn);
            this.tabPage1.Controls.Add(this.PartnerAuthPasswordBox);
            this.tabPage1.Controls.Add(this.PartnerAuthUsernameBox);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(862, 495);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Partner Authenticate";
            // 
            // tabPage2
            // 
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(862, 495);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "tabPage2";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // PartnerAuthUsernameBox
            // 
            this.PartnerAuthUsernameBox.Location = new System.Drawing.Point(317, 47);
            this.PartnerAuthUsernameBox.Multiline = true;
            this.PartnerAuthUsernameBox.Name = "PartnerAuthUsernameBox";
            this.PartnerAuthUsernameBox.Size = new System.Drawing.Size(175, 26);
            this.PartnerAuthUsernameBox.TabIndex = 0;
            // 
            // PartnerAuthPasswordBox
            // 
            this.PartnerAuthPasswordBox.Location = new System.Drawing.Point(317, 98);
            this.PartnerAuthPasswordBox.Multiline = true;
            this.PartnerAuthPasswordBox.Name = "PartnerAuthPasswordBox";
            this.PartnerAuthPasswordBox.Size = new System.Drawing.Size(175, 27);
            this.PartnerAuthPasswordBox.TabIndex = 1;
            // 
            // AuthBtn
            // 
            this.AuthBtn.Location = new System.Drawing.Point(368, 150);
            this.AuthBtn.Name = "AuthBtn";
            this.AuthBtn.Size = new System.Drawing.Size(75, 23);
            this.AuthBtn.TabIndex = 2;
            this.AuthBtn.Text = "Login";
            this.AuthBtn.UseVisualStyleBackColor = true;
            this.AuthBtn.Click += new System.EventHandler(this.AuthBtn_Click);
            // 
            // AuthenticateResultLbl
            // 
            this.AuthenticateResultLbl.AutoSize = true;
            this.AuthenticateResultLbl.Location = new System.Drawing.Point(385, 273);
            this.AuthenticateResultLbl.Name = "AuthenticateResultLbl";
            this.AuthenticateResultLbl.Size = new System.Drawing.Size(37, 13);
            this.AuthenticateResultLbl.TabIndex = 3;
            this.AuthenticateResultLbl.Text = "Result";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(894, 545);
            this.Controls.Add(this.tabControl1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.Label AuthenticateResultLbl;
        private System.Windows.Forms.Button AuthBtn;
        private System.Windows.Forms.TextBox PartnerAuthPasswordBox;
        private System.Windows.Forms.TextBox PartnerAuthUsernameBox;
        private System.Windows.Forms.TabPage tabPage2;
    }
}

