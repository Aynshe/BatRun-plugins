using Microsoft.Gaming.XboxGameBar;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace RetroBatGameModeWidget
{
    public sealed partial class AppsListPage : Page
    {
        private const string ApiBase = "http://localhost:17654";
        private XboxGameBarWidget widget;
        private DispatcherTimer statusTimer;

        public AppsListPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            this.widget = e.Parameter as XboxGameBarWidget;

            try
            {
                SystemNavigationManager.GetForCurrentView().BackRequested -= Apps_BackRequested;
                SystemNavigationManager.GetForCurrentView().BackRequested += Apps_BackRequested;
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            }
            catch { }

            _ = LoadAppsAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            try
            {
                SystemNavigationManager.GetForCurrentView().BackRequested -= Apps_BackRequested;
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
                if (statusTimer != null)
                {
                    statusTimer.Stop();
                    statusTimer = null;
                }
            }
            catch { }
            base.OnNavigatedFrom(e);
        }

        private void Apps_BackRequested(object sender, BackRequestedEventArgs e)
        {
            try
            {
                e.Handled = true;
                if (Frame != null && Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
                else if (Frame != null)
                {
                    Frame.Navigate(typeof(WidgetPage), widget);
                }
            }
            catch { }
        }

        private async Task LoadAppsAsync()
        {
            try
            {
                string body = await FetchAsync("/command?cmd=APPS");
                var apps = ParseApps(body);
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => RenderApps(apps));
            }
            catch { }
        }

        private void RenderApps(List<string> apps)
        {
            if (apps.Count == 0)
            {
                EmptyText.Visibility = Visibility.Visible;
                AppsList.ItemsSource = null;
            }
            else
            {
                EmptyText.Visibility = Visibility.Collapsed;
                AppsList.ItemsSource = apps;
            }
        }

        private async void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button b && b.Tag is string name))
            {
                return;
            }

            b.IsEnabled = false;
            try
            {
                string resp = await FetchAsync("/command?cmd=REMOVEAPP&name=" + Uri.EscapeDataString(name));
                if (!string.IsNullOrEmpty(resp) && resp.StartsWith("OK:REMOVED"))
                {
                    ShowStatus(string.Format(LoadString("removed_fmt"), name), "#4ADE80");
                    await LoadAppsAsync();
                }
                else if (!string.IsNullOrEmpty(resp) && resp.StartsWith("ERROR:protected_app"))
                {
                    ShowStatus(string.Format(LoadString("protected_fmt"), name), "#F43F5E");
                    b.IsEnabled = true;
                }
                else
                {
                    ShowStatus(string.Format(LoadString("remove_failed_fmt"), name, resp ?? "<no response>"), "#F43F5E");
                    b.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                ShowStatus(string.Format(LoadString("remove_ex_fmt"), ex.GetType().Name), "#F43F5E");
                b.IsEnabled = true;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Frame != null && Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
                else if (Frame != null)
                {
                    Frame.Navigate(typeof(WidgetPage), widget);
                }
            }
            catch { }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadAppsAsync();
        }

        private void ShowStatus(string text, string hexColor)
        {
            try
            {
                StatusText.Text = text;
                StatusText.Foreground = new SolidColorBrush(ParseHex(hexColor));

                if (statusTimer == null)
                {
                    statusTimer = new DispatcherTimer();
                    statusTimer.Tick += StatusTimer_Tick;
                }
                statusTimer.Stop();
                statusTimer.Interval = TimeSpan.FromSeconds(4);
                statusTimer.Start();
            }
            catch { }
        }

        private void StatusTimer_Tick(object sender, object e)
        {
            try
            {
                if (statusTimer != null) statusTimer.Stop();
                StatusText.Text = "";
            }
            catch { }
        }

        private static string LoadString(string key)
        {
            string lang = Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride;
            bool french = lang != null && lang.StartsWith("fr", StringComparison.OrdinalIgnoreCase);
            if (key == "removed_fmt") return french ? "Supprimé : {0}" : "Removed: {0}";
            if (key == "protected_fmt") return french ? "'{0}' est protégé, suppression refusée." : "'{0}' is protected, removal refused.";
            if (key == "remove_failed_fmt") return french ? "'{0}' non supprimé : {1}" : "'{0}' not removed: {1}";
            if (key == "remove_ex_fmt") return french ? "Erreur réseau : {0}" : "Network error: {0}";
            return key;
        }

        private static Windows.UI.Color ParseHex(string hex)
        {
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
            return Windows.UI.Color.FromArgb(255, r, g, b);
        }

        private static List<string> ParseApps(string body)
        {
            var list = new List<string>();
            if (!string.IsNullOrEmpty(body) && body.StartsWith("APPS:"))
            {
                string csv = body.Substring(5);
                if (!string.IsNullOrEmpty(csv))
                {
                    string[] parts = csv.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var p in parts)
                    {
                        string name = p.Trim();
                        if (!string.IsNullOrEmpty(name)) list.Add(name);
                    }
                }
            }
            return list;
        }

        private static async Task<string> FetchAsync(string relativePath)
        {
            using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3)))
            using (var http = new Windows.Web.Http.HttpClient())
            {
                string url = ApiBase + relativePath;
                var res = await http.GetAsync(new Uri(url)).AsTask(cts.Token);
                return await res.Content.ReadAsStringAsync().AsTask(cts.Token);
            }
        }
    }
}
