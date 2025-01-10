namespace PM_Status_Check
{
    partial class WebPopup
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
            webMain = new Microsoft.Web.WebView2.WinForms.WebView2();
            ((System.ComponentModel.ISupportInitialize)webMain).BeginInit();
            SuspendLayout();
            // 
            // webMain
            // 
            webMain.AllowExternalDrop = true;
            webMain.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            webMain.CreationProperties = null;
            webMain.DefaultBackgroundColor = Color.White;
            webMain.Location = new Point(1, 3);
            webMain.Name = "webMain";
            webMain.Size = new Size(800, 451);
            webMain.TabIndex = 0;
            webMain.ZoomFactor = 1D;
            webMain.NavigationCompleted += webMain_NavigationCompleted;
            // 
            // WebPopup
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(webMain);
            Name = "WebPopup";
            Text = "WebPopup";
            ((System.ComponentModel.ISupportInitialize)webMain).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Microsoft.Web.WebView2.WinForms.WebView2 webMain;
    }
}