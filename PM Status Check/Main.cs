using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PM_Status_Check
{
    public partial class Main : Form
    {
        private int totalPackages = 0;
        private int successfulSynchronizations = 0;
        private int failedSynchronizations = 0;
        private StringBuilder outputBuilder = new StringBuilder();
        private Button executeButton;

        public Main()
        {
            InitializeComponent();
            InitializeUI();
        }

        private void InitializeUI()
        {
            // Add a button to execute the main functionality
            executeButton = new Button
            {
                Text = "Run Synchronization",
                Width = 250,
                Height = 40,
                Top = 10,
                Left = 10,
                BackColor = System.Drawing.Color.FromArgb(131, 197, 190), // Background color
                ForeColor = System.Drawing.Color.White,         // Text color
                Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold), // Font style
                FlatStyle = FlatStyle.Flat                     // Flat style for modern look
            };
            executeButton.FlatAppearance.BorderSize = 1;
            executeButton.FlatAppearance.BorderColor = System.Drawing.Color.DarkGreen;
            executeButton.Click += ExecuteButton_Click;
            this.Controls.Add(executeButton);

            // Add a TextBox for output
            TextBox outputBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Width = this.ClientSize.Width - 20,
                Height = this.ClientSize.Height - 60,
                Top = 60,
                Left = 10,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            outputBox.Name = "OutputBox";
            this.Controls.Add(outputBox);
        }

        private async void ExecuteButton_Click(object sender, EventArgs e)
        {
            executeButton.Text = "Synchronization In Progress...";
            executeButton.Width = 250;
            executeButton.Height = 40;
            executeButton.Top = 10;
            executeButton.Left = 10;
            executeButton.Enabled = false;

            await RunMainLogic();

            executeButton.Text = "Run Synchronization";
            executeButton.Width = 250;
            executeButton.Height = 40;
            executeButton.Top = 10;
            executeButton.Left = 10;
            executeButton.Enabled = true;
        }

        private async Task RunMainLogic()
        {
            totalPackages = 0;
            successfulSynchronizations = 0;
            failedSynchronizations = 0;

            try
            {
                // Redirect output to the TextBox
                TextBox outputBox = (TextBox)this.Controls["OutputBox"];

                var statuses = await GraphHelper.UpdatePMStatuses();

                foreach (var status in statuses)
                {
                    totalPackages++;
                    outputBuilder.AppendLine($"Found {status.Id}");
                    outputBox.Text = outputBuilder.ToString();
                    outputBox.SelectionStart = outputBox.Text.Length;
                    outputBox.ScrollToCaret();

                    // Simulate processing and update counts
                    if (status.SyncStatus == "Synchronized") successfulSynchronizations++;
                    else failedSynchronizations++;
                }

                outputBuilder.AppendLine("Synchronization Complete.");
                outputBox.Text = outputBuilder.ToString();

                // Summarize the results
                outputBuilder.AppendLine("\nSummary:");
                outputBuilder.AppendLine($"Total Packages Found: {totalPackages}");
                outputBuilder.AppendLine($"Successful Synchronizations: {successfulSynchronizations}");
                outputBuilder.AppendLine($"Failed Synchronizations: {failedSynchronizations}");
                outputBox.Text = outputBuilder.ToString();
            }
            catch (Exception ex)
            {
                outputBuilder.AppendLine($"Error: {ex.Message}");
                TextBox outputBox = (TextBox)this.Controls["OutputBox"];
                outputBox.Text = outputBuilder.ToString();
            }
        }
    }
}
