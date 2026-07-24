using Microsoft.Gaming.XboxGameBar;
using System;
using System.Collections.Generic;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RetroBatGameModeWidget
{
    public sealed partial class App : Application
    {
        private XboxGameBarWidget widget = null;
        private WidgetPage widgetPage = null;

        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("[Widget] UnhandledException: " + e.Exception?.Message);
                e.Handled = true;
            };
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Direct launch from start menu / desktop shortcut is rare since AppListEntry="none".
            // We just create a hidden window so the process stays alive for Game Bar activation.
            var rootFrame = new Frame();
            Window.Current.Content = rootFrame;
            Window.Current.Activate();
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            base.OnActivated(args);

            string logPath = System.IO.Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "widget_debug.txt");
            System.IO.File.WriteAllText(logPath, $"[LOG] Activation commencee a {DateTime.Now}\r\n");

            if (args.Kind != ActivationKind.Protocol)
            {
                System.IO.File.AppendAllText(logPath, $"[WARN] Kind n'est pas Protocol: {args.Kind}\r\n");
                return;
            }

            var protocolArgs = args as IProtocolActivatedEventArgs;
            var uri = protocolArgs?.Uri;
            System.IO.File.AppendAllText(logPath, $"[INFO] Uri: {uri}\r\n");
            
            if (uri == null || !uri.Scheme.Equals("ms-gamebarwidget", StringComparison.OrdinalIgnoreCase))
            {
                System.IO.File.AppendAllText(logPath, $"[WARN] Scheme incorrect ou Uri null\r\n");
                return;
            }

            var widgetArgs = args as XboxGameBarWidgetActivatedEventArgs;
            if (widgetArgs == null)
            {
                System.IO.File.AppendAllText(logPath, $"[ERROR] widgetArgs est null après cast\r\n");
                return;
            }

            try
            {
                System.IO.File.AppendAllText(logPath, $"[INFO] Creation de rootFrame et XboxGameBarWidget...\r\n");
                var rootFrame = new Frame();
                widget = new XboxGameBarWidget(widgetArgs, Window.Current.CoreWindow, rootFrame);
                System.IO.File.AppendAllText(logPath, $"[INFO] XboxGameBarWidget cree avec succes.\r\n");

                // Use proper UWP navigation
                rootFrame.Navigate(typeof(WidgetPage), widget);
                System.IO.File.AppendAllText(logPath, $"[INFO] Navigation effectuee.\r\n");

                Window.Current.Content = rootFrame;
                Window.Current.Activate();
                System.IO.File.AppendAllText(logPath, $"[INFO] Window activee !\r\n");
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(logPath, $"[EXCEPTION] {ex.GetType().Name}: {ex.Message}\r\n{ex.StackTrace}\r\n");
            }
        }

        public XboxGameBarWidget GetWidget() => widget;
    }
}
