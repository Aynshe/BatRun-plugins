using Microsoft.Gaming.XboxGameBar;
using System;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace RetroBatGameModeWidget
{
    public sealed partial class WidgetPage : Page
    {
        private const string ApiBase = "http://localhost:17654";
        private XboxGameBarWidget widget;

        private DispatcherTimer healthTimer;

        public WidgetPage()
        {
            try
            {
                this.InitializeComponent();
            }
            catch (Exception ex)
            {
                try
                {
                    string path = System.IO.Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "widget_page_debug.txt");
                    System.IO.File.WriteAllText(path, $"[INIT EXCEPTION] {ex.Message}\r\n{ex.StackTrace}\r\n");
                }
                catch { }
            }
        }

        protected override void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            string logPath = System.IO.Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "widget_page_debug.txt");
            System.IO.File.WriteAllText(logPath, $"[LOG] WidgetPage OnNavigatedTo commence\r\n");
            try
            {
                base.OnNavigatedTo(e);
                this.widget = e.Parameter as XboxGameBarWidget;
                if (this.widget != null) {
                    this.widget.SettingsClicked -= Widget_SettingsClicked;
                    this.widget.SettingsClicked += Widget_SettingsClicked;
                }
                System.IO.File.AppendAllText(logPath, $"[INFO] Widget récupéré : {(this.widget != null ? "OUI" : "NON")}\r\n");

                if (this.healthTimer == null) {
                    this.healthTimer = new DispatcherTimer();
                    this.healthTimer.Interval = TimeSpan.FromSeconds(5);
                    this.healthTimer.Tick += HealthTimer_Tick;
                }
                if (!this.healthTimer.IsEnabled) this.healthTimer.Start();
                System.IO.File.AppendAllText(logPath, $"[INFO] Timer démarré (IsEnabled={this.healthTimer.IsEnabled})\r\n");

                HealthTimer_Tick(null, null);
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(logPath, $"[NAV EXCEPTION] {ex.Message}\r\n{ex.StackTrace}\r\n");
            }
        }

        protected override void OnNavigatedFrom(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            try
            {
                if (this.healthTimer != null && this.healthTimer.IsEnabled)
                {
                    this.healthTimer.Stop();
                }
                if (this.widget != null) {
                    this.widget.SettingsClicked -= Widget_SettingsClicked;
                }
            }
            catch { }
        }

        private void Widget_SettingsClicked(XboxGameBarWidget sender, object args)
        {
            // The system settings icon on the widget title bar is hidden for now.
        }

        private async void HealthTimer_Tick(object sender, object e)
        {
            string logPath = System.IO.Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "widget_page_debug.txt");
            bool ok = false;
            try
            {
                string body = await FetchAsync("/ping");
                ok = !string.IsNullOrEmpty(body) && body.StartsWith("PONG");
                System.IO.File.AppendAllText(logPath, $"[PING] Réponse: {body} (ok={ok})\r\n");
            }
            catch (Exception ex)
            {
                ok = false;
                System.IO.File.AppendAllText(logPath, $"[PING EXCEPTION] {ex.Message}\r\n");
            }

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                SetButtonsEnabled(ok);
                SetStatus(ok ? "Connected" : "Disconnected", ok ? "#4ADE80" : "#F43F5E", ok);

                if (ok)
                {
                    RefreshActiveState();
                    RefreshTriggerApp();
                    RefreshEnableState();
                    RefreshStandaloneState();
                }
            });
        }

        private async void RefreshActiveState()
        {
            try
            {
                string body = await FetchAsync("/command?cmd=STATUS");
                bool active = !string.IsNullOrEmpty(body) && body.Contains("ACTIVE");
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    SetStatus(active ? "Game Mode Active" : "Connected", active ? "#4ADE80" : "#9AA0AA", active: active, accent: true);
                });
            }
            catch
            {
            }
        }

        private async void RefreshTriggerApp()
        {
            try
            {
                string body = await FetchAsync("/command?cmd=TRIGGER");
                string name = (!string.IsNullOrEmpty(body) && body.StartsWith("TRIGGER:")) ? body.Substring(8) : "NONE";
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (string.IsNullOrEmpty(name) || name == "NONE")
                        TriggerText.Text = "Idle";
                    else
                        TriggerText.Text = "Triggered by: " + name;
                });
            }
            catch { }
        }

        private async void TargetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TargetButton.IsEnabled = false;
                string resp = await FetchAsync("/command?cmd=TARGET");
                TargetButton.IsEnabled = true;
                if (!string.IsNullOrEmpty(resp) && resp.StartsWith("OK"))
                {
                    System.Diagnostics.Debug.WriteLine("[Widget] Target acknowledged: " + resp);
                }
                else
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        SetStatus("Target failed", "#F43F5E");
                    });
                }
            }
            catch (Exception ex)
            {
                try { TargetButton.IsEnabled = true; } catch { }
                System.Diagnostics.Debug.WriteLine("[Widget] TargetButton exception: " + ex.Message);
            }
        }

        private async void EnableToggleButton_Click(object sender, RoutedEventArgs e)
        {
            EnableToggleButton.IsEnabled = false;
            try
            {
                string resp = await FetchAsync("/command?cmd=SETENABLE");
                if (!string.IsNullOrEmpty(resp) && resp.StartsWith("SETENABLE:"))
                {
                    bool isEnabled = resp.Substring("SETENABLE:".Length) == "true";
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        UpdateEnableToggleUI(isEnabled);
                        SetStatus(isEnabled ? "Enabled" : "Disabled",
                                  isEnabled ? "#4ADE80" : "#9AA0AA", connected: true, active: isEnabled, accent: true);
                    });
                    RefreshActiveState();
                }
            }
            catch
            {
            }
            finally
            {
                EnableToggleButton.IsEnabled = true;
            }
        }

        private async void EmergencyButton_Click(object sender, RoutedEventArgs e)
        {
            EmergencyButton.IsEnabled = false;
            try
            {
                // Step 1: HTTP path. If the backend is alive, ask it to undo
                // everything in-place. Quick 1.5s timeout so we fall fast to
                // the fallback.
                string resp = null;
                try
                {
                    using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(1.5)))
                    using (var http = new Windows.Web.Http.HttpClient())
                    {
                        var res = await http.GetAsync(new Uri(ApiBase + "/command?cmd=EMERGENCY")).AsTask(cts.Token);
                        resp = await res.Content.ReadAsStringAsync().AsTask(cts.Token);
                    }
                }
                catch
                {
                    resp = null;
                }

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (!string.IsNullOrEmpty(resp) && resp.StartsWith("OK:EMERGENCY"))
                    {
                        SetStatus("Emergency restore done (HTTP)", "#4ADE80", active: true, accent: true);
                        return;
                    }
                    // HTTP dead / disconnected. Show fallback warning + proceed
                    // to the kill-the-backend step below.
                    SetStatus("HTTP unavailable — killing backend (watchdog restore)",
                              "#F43F5E", active: true, accent: true);
                });

                // Step 2 (forced kill fallback): if HTTP did not answer, kill
                // the backend process via taskkill. The watchdog companion
                // observes the parent death and runs full UndoOptimizations +
                // TaskbarAutoHide restore + restore hidden windows + reload.
                // This is the strongest possible recovery: even if the backend
                // is hard-frozen in a synchronous WinAPI call, this brings the
                // system back to a clean state.
                if (resp == null || !resp.StartsWith("OK:EMERGENCY"))
                {
                    try
                    {
                        var si = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "taskkill.exe",
                            Arguments = "/im RetroBatGameMode.exe /f",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };
                        using (var p = System.Diagnostics.Process.Start(si))
                        {
                            try { p.WaitForExit(4000); } catch { }
                            try { p.StandardOutput.ReadToEnd(); } catch { }
                            try { p.StandardError.ReadToEnd(); } catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("[Widget] taskkill error: " + ex.Message);
                    }
                }

                // Re-arm the button so a second attempt is possible.
                await Task.Delay(1500);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[Widget] EmergencyButton exception: " + ex.Message);
            }
            finally
            {
                try { await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { EmergencyButton.IsEnabled = true; }); } catch { }
            }
        }

        private async void RefreshEnableState()
        {
            try
            {
                string body = await FetchAsync("/command?cmd=GETENABLE");
                bool isEnabled = !string.IsNullOrEmpty(body) && body.StartsWith("GETENABLE:true");
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    UpdateEnableToggleUI(isEnabled);
                });
            }
            catch { }
        }

        private void UpdateEnableToggleUI(bool isEnabled)
        {
            if (EnableToggleButton == null) return;
            EnableToggleButton.Content = isEnabled ? "Disable" : "Enable";
            EnableToggleButton.Background = new SolidColorBrush(
                isEnabled
                    ? Windows.UI.Color.FromArgb(255, 244, 63, 94)   // rose/red = current state is active, click to stop
                    : Windows.UI.Color.FromArgb(255, 74, 222, 128)  // green = current state is off, click to start
            );
            EnableToggleButton.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
        }

        private async void StandaloneToggleButton_Click(object sender, RoutedEventArgs e)
        {
            StandaloneToggleButton.IsEnabled = false;
            try
            {
                string current = await FetchAsync("/command?cmd=GETSTANDALONE");
                string nextMode = CycleModeNext(current);
                string resp = await FetchAsync("/command?cmd=SETSTANDALONE&mode=" + Uri.EscapeDataString(nextMode));
                if (!string.IsNullOrEmpty(resp) && resp.StartsWith("SETSTANDALONE:"))
                {
                    string modeStr = resp.Substring("SETSTANDALONE:".Length);
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        UpdateStandaloneToggleUI(modeStr);
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[Widget] StandaloneToggle exception: " + ex.Message);
            }
            finally
            {
                StandaloneToggleButton.IsEnabled = true;
            }
        }

        private async void RefreshStandaloneState()
        {
            try
            {
                string body = await FetchAsync("/command?cmd=GETSTANDALONE");
                string modeStr = ParseStandaloneResponse(body);
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    UpdateStandaloneToggleUI(modeStr);
                });
            }
            catch { }
        }

        private static string ParseStandaloneResponse(string body)
        {
            if (string.IsNullOrEmpty(body)) return "off";
            string prefix = "GETSTANDALONE:";
            if (body.StartsWith(prefix)) return body.Substring(prefix.Length);
            // tolerate legacy SETENABLE-style responses
            if (body.StartsWith("SETSTANDALONE:")) return body.Substring("SETSTANDALONE:".Length);
            return "off";
        }

        private static string CycleModeNext(string current)
        {
            string s = (current ?? "").Trim().ToLowerInvariant();
            // Strip a GETSTANDALONE: prefix if present
            if (s.StartsWith("getstandalone:")) s = s.Substring("getstandalone:".Length);
            if (s.StartsWith("setstandalone:")) s = s.Substring("setstandalone:".Length);
            if (s == "full") return "off";
            if (s == "monitor" || s == "true") return "full";
            return "monitor";
        }

        private void UpdateStandaloneToggleUI(string modeStr)
        {
            if (StandaloneToggleButton == null) return;
            ms = (modeStr ?? "off").Trim().ToLowerInvariant();
            // Color reflects the current state (not the click-to action).
            string label;
            Windows.UI.Color bg;
            if (ms == "full")
            {
                label = "Mode: Full";
                bg = Windows.UI.Color.FromArgb(255, 139, 92, 246);  // violet = always-on (Full)
            }
            else if (ms == "monitor" || ms == "true")
            {
                label = "Mode: Monitor";
                bg = Windows.UI.Color.FromArgb(255, 59, 130, 246);  // blue = monitored (RetroBat+ThirdParty)
            }
            else
            {
                label = "Mode: Off";
                bg = Windows.UI.Color.FromArgb(255, 120, 120, 130); // grey = off
            }
            StandaloneToggleButton.Content = label;
            StandaloneToggleButton.Background = new SolidColorBrush(bg);
            StandaloneToggleButton.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
        }

        private void AppsButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.widget == null) return;
            this.Frame.Navigate(typeof(AppsListPage), this.widget);
        }

        private void SetButtonsEnabled(bool enabled)
        {
            TargetButton.IsEnabled = enabled;
            EnableToggleButton.IsEnabled = enabled;
            StandaloneToggleButton.IsEnabled = enabled;
            AppsButton.IsEnabled = enabled;
            // The Emergency button is ALWAYS enabled — even when the backend
            // is "disconnected" by design: that's exactly when it's useful.
            try { EmergencyButton.IsEnabled = true; } catch { }
        }

        // Cached mode string for the button label/color. Avoids re-parsing on every UI tick.
        private static string ms = "off";

        private void SetStatus(string text, string fgColor, bool connected = false, bool active = false, bool accent = false)
        {
            StatusText.Text = text;
            var brush = new SolidColorBrush(ParseHex(fgColor));
            StatusText.Foreground = brush;
        }

        private static Windows.UI.Color ParseHex(string hex)
        {
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
            return Windows.UI.Color.FromArgb(255, r, g, b);
        }

        private static async Task<string> FetchAsync(string relativePath)
        {
            // EN: Windows.Web.Http.HttpClient has no .Timeout property — use CancellationTokenSource instead
            // FR: Windows.Web.Http.HttpClient n'a pas de propriété .Timeout — on utilise CancellationTokenSource
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
