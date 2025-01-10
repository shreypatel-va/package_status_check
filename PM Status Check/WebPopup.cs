using Microsoft.Web.WebView2.Core;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PM_Status_Check
{
    public partial class WebPopup : Form
    {
        private static CoreWebView2Environment? WebView2Env = null;
        public int NavState { get; set; } = 0;
        public string NavTo { get; set; } = string.Empty;

        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public WebPopup()
        {
            Log.Information("Initializing Web Popup");
            InitializeComponent();
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            if (WebView2Env == null)
            {
                var filePath = Path.Combine(PM_Status_Check.Files.GetDataDirectory(), @"IDT\WebMain");
                Log.Information("Setting WebView File Path {filePath}", filePath);
                WebView2Env = await CoreWebView2Environment.CreateAsync(null, filePath);
            }
            Log.Information("Ensuring Core Web View 2");
            await webMain.EnsureCoreWebView2Async(WebView2Env);
        }

        public async Task<string?> CaseflowOneTime(string url)
        {
            Log.Information("Caseflow One Time");
            await _semaphore.WaitAsync();

            Log.Information("Navigating to {url}", url);
            NavTo = url;
            NavState = 0;
            await InitializeAsync();
            webMain.CoreWebView2.Navigate(url);
            Log.Information("ShowDialog");
            this.ShowDialog();

            _semaphore.Release();
            Log.Information("Complete NavState {NavState}", NavState);
            if (NavState == 1)
            {
                return await webMain.ExecuteScriptAsync("document.documentElement.innerHTML");
            }
            return null;
        }

        private void webMain_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!string.IsNullOrEmpty(NavTo) && webMain.Source.ToString().ToLower().StartsWith(NavTo.ToLower()))
            {
                NavState = 1;
                this.Hide();
            }
        }
    }
}
