using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RetroBatGameMode
{
    class Program
    {
        [DllImport("psapi.dll")]
        static extern int EmptyWorkingSet(IntPtr hwProc);

        [DllImport("ntdll.dll")]
        public static extern int NtSuspendProcess(IntPtr processHandle);

        [DllImport("ntdll.dll")]
        public static extern int NtResumeProcess(IntPtr processHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool WritePrivateProfileString(string? Section, string? Key, string? Value, string FilePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern int GetPrivateProfileString(string Section, string Key, string Default, System.Text.StringBuilder RetVal, int Size, string FilePath);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        const uint WM_CLOSE = 0x0010;
        const uint MSG_EXIT_EXPLORER = 0x05B4;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetTokenInformation(
            IntPtr TokenHandle,
            int TokenInformationClass,
            IntPtr TokenInformation,
            int TokenInformationLength,
            out int ReturnLength);

        const int SW_SHOW = 5;
        const int SW_HIDE = 0;

        const uint PROCESS_SUSPEND_RESUME = 0x0800;
        const uint PROCESS_SET_QUOTA = 0x0100;
        const uint PROCESS_QUERY_INFORMATION = 0x0400;
        const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        static bool enabled = true;
        static bool killExplorer = true;
        static bool emptyWorkingSet = true;
        static bool suspendBackgroundApps = true;
        static bool hideNonSuspendedWindows = true;
        static bool showOverlay = true;
        static bool showConsole = false;
        static bool autoStartWithRetroBat = true;
        static bool logToFile = true;
        static bool standaloneMonitor = false;
        static bool isOptimized = false;
        static bool requestExit = false;
        static string language = "system";
        static string whitelist = "retrobat, emulationstation, vlc, batrun, BatRunGuardian, GameBar, AttractMode";
        static string hideWhitelist = "retrobat, emulationstation, retroarch, vlc, explorer, batrun, BatRunGuardian, GameBar, AttractMode";
        static string thirdPartyApps = "";

        static System.Collections.Generic.List<int> suspendedProcessIds = new System.Collections.Generic.List<int>();
        static System.Collections.Generic.List<IntPtr> hiddenWindows = new System.Collections.Generic.List<IntPtr>();
        static System.Collections.Generic.List<IntPtr> hiddenNonSuspendedWindows = new System.Collections.Generic.List<IntPtr>();
        static bool isSystemRestored = true;
        static EventWaitHandle? cleanExitEvent = null;
        static Mutex? appMutex = null;

        static bool IsProcessElevated(int pid)
        {
            try
            {
                if (pid == 0 || pid == 4) return true; // Idle or System process is always elevated/privileged

                IntPtr hProcess = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
                if (hProcess == IntPtr.Zero)
                {
                    hProcess = OpenProcess(0x0400, false, pid); // PROCESS_QUERY_INFORMATION = 0x0400
                }

                if (hProcess == IntPtr.Zero)
                {
                    // If we cannot open the process to query it, it's either system or running as elevated Admin while we are not.
                    // Treat it as elevated/privileged to safely ignore it.
                    return true;
                }

                IntPtr tokenHandle = IntPtr.Zero;
                try
                {
                    if (OpenProcessToken(hProcess, 0x0008, out tokenHandle)) // TOKEN_QUERY = 0x0008
                    {
                        int elevationSize = Marshal.SizeOf(typeof(int));
                        IntPtr elevationPtr = Marshal.AllocHGlobal(elevationSize);
                        try
                        {
                            int returnLength = 0;
                            if (GetTokenInformation(tokenHandle, 20, elevationPtr, elevationSize, out returnLength)) // TokenElevation = 20
                            {
                                int isElevated = Marshal.ReadInt32(elevationPtr);
                                return isElevated != 0;
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(elevationPtr);
                        }
                    }
                }
                finally
                {
                    if (tokenHandle != IntPtr.Zero)
                    {
                        CloseHandle(tokenHandle);
                    }
                    CloseHandle(hProcess);
                }
            }
            catch
            {
                // In case of error, err on the side of safety: treat as elevated/privileged to ignore it
                return true;
            }
            return false;
        }

        static bool IsRealAppWindow(IntPtr hWnd)
        {
            if (!IsWindowVisible(hWnd)) return false;

            // Get window title
            System.Text.StringBuilder title = new System.Text.StringBuilder(512);
            GetWindowText(hWnd, title, title.Capacity);
            string titleStr = title.ToString().Trim();
            if (string.IsNullOrEmpty(titleStr)) return false;

            // Avoid some known system background window titles and framework helpers
            if (titleStr.Equals("Hidden Window", StringComparison.OrdinalIgnoreCase) ||
                titleStr.Equals("Default IME", StringComparison.OrdinalIgnoreCase) ||
                titleStr.Equals("MSCTFIME UI", StringComparison.OrdinalIgnoreCase) ||
                titleStr.IndexOf("GDI+", StringComparison.OrdinalIgnoreCase) >= 0 ||
                titleStr.IndexOf("GDI+ Window", StringComparison.OrdinalIgnoreCase) >= 0 ||
                titleStr.IndexOf("OleMainThread", StringComparison.OrdinalIgnoreCase) >= 0 ||
                titleStr.IndexOf("Cicero", StringComparison.OrdinalIgnoreCase) >= 0 ||
                titleStr.IndexOf("DDE Server", StringComparison.OrdinalIgnoreCase) >= 0 ||
                titleStr.IndexOf("HwndWrapper", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            // Get class name
            System.Text.StringBuilder className = new System.Text.StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            string classStr = className.ToString();

            // Ignore common system background classes and framework internal messaging/rendering targets
            if (classStr.Equals("Message", StringComparison.OrdinalIgnoreCase) ||
                classStr.IndexOf("IME", StringComparison.OrdinalIgnoreCase) >= 0 ||
                classStr.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
                classStr.IndexOf("GDI+", StringComparison.OrdinalIgnoreCase) >= 0 ||
                classStr.IndexOf("GdiPlus", StringComparison.OrdinalIgnoreCase) >= 0 ||
                classStr.Equals("OleMainThreadWndClass", StringComparison.OrdinalIgnoreCase) ||
                classStr.Equals("CiceroUIWndFrame", StringComparison.OrdinalIgnoreCase) ||
                classStr.Equals("DDEServerWindow", StringComparison.OrdinalIgnoreCase) ||
                classStr.Equals("ClipbrdTrackerClass", StringComparison.OrdinalIgnoreCase) ||
                classStr.Equals("SysFader", StringComparison.OrdinalIgnoreCase) ||
                classStr.Equals("DirectCompositionWindowClass", StringComparison.OrdinalIgnoreCase) ||
                classStr.IndexOf("DWM", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            // Get window styles
            int style = GetWindowLong(hWnd, -16); // GWL_STYLE = -16
            int exStyle = GetWindowLong(hWnd, -20); // GWL_EXSTYLE = -20

            // Should not be a child window (WS_CHILD = 0x40000000)
            if ((style & 0x40000000) != 0) return false;

            // Should not be a tool window (WS_EX_TOOLWINDOW = 0x00000080)
            if ((exStyle & 0x00000080) != 0) return false;

            // Check if it has an owner. If it does, it must have WS_EX_APPWINDOW style (0x00040000) to be a real app window
            IntPtr owner = GetWindow(hWnd, 4); // GW_OWNER = 4
            if (owner != IntPtr.Zero)
            {
                if ((exStyle & 0x00040000) == 0) return false;
            }

            // Verify size is non-zero (greater than 40x40)
            RECT rect;
            if (GetWindowRect(hWnd, out rect))
            {
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                if (width <= 40 || height <= 40) return false;
            }

            return true;
        }

        static bool IsRealAppWindowHiddenCheck(IntPtr hWnd)
        {
            // Get window title
            System.Text.StringBuilder title = new System.Text.StringBuilder(512);
            GetWindowText(hWnd, title, title.Capacity);
            string titleStr = title.ToString().Trim();
            if (string.IsNullOrEmpty(titleStr)) return false;

            // Avoid some known system background window titles and framework helpers
            if (titleStr.Equals("Hidden Window", StringComparison.OrdinalIgnoreCase) ||
                titleStr.Equals("Default IME", StringComparison.OrdinalIgnoreCase) ||
                titleStr.Equals("MSCTFIME UI", StringComparison.OrdinalIgnoreCase) ||
                titleStr.IndexOf("GDI+", StringComparison.OrdinalIgnoreCase) >= 0 ||
                titleStr.IndexOf("GDI+ Window", StringComparison.OrdinalIgnoreCase) >= 0 ||
                titleStr.IndexOf("OleMainThread", StringComparison.OrdinalIgnoreCase) >= 0 ||
                titleStr.IndexOf("Cicero", StringComparison.OrdinalIgnoreCase) >= 0 ||
                titleStr.IndexOf("DDE Server", StringComparison.OrdinalIgnoreCase) >= 0 ||
                titleStr.IndexOf("HwndWrapper", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            // Get class name
            System.Text.StringBuilder className = new System.Text.StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            string classStr = className.ToString();

            // Ignore common system background classes and framework internal messaging/rendering targets
            if (classStr.Equals("Message", StringComparison.OrdinalIgnoreCase) ||
                classStr.IndexOf("IME", StringComparison.OrdinalIgnoreCase) >= 0 ||
                classStr.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
                classStr.IndexOf("GDI+", StringComparison.OrdinalIgnoreCase) >= 0 ||
                classStr.IndexOf("GdiPlus", StringComparison.OrdinalIgnoreCase) >= 0 ||
                classStr.Equals("OleMainThreadWndClass", StringComparison.OrdinalIgnoreCase) ||
                classStr.Equals("CiceroUIWndFrame", StringComparison.OrdinalIgnoreCase) ||
                classStr.Equals("DDEServerWindow", StringComparison.OrdinalIgnoreCase) ||
                classStr.Equals("ClipbrdTrackerClass", StringComparison.OrdinalIgnoreCase) ||
                classStr.Equals("SysFader", StringComparison.OrdinalIgnoreCase) ||
                classStr.Equals("DirectCompositionWindowClass", StringComparison.OrdinalIgnoreCase) ||
                classStr.IndexOf("DWM", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            // Get window styles
            int style = GetWindowLong(hWnd, -16); // GWL_STYLE = -16
            int exStyle = GetWindowLong(hWnd, -20); // GWL_EXSTYLE = -20

            // Should not be a child window (WS_CHILD = 0x40000000)
            if ((style & 0x40000000) != 0) return false;

            // Should not be a tool window (WS_EX_TOOLWINDOW = 0x00000080)
            if ((exStyle & 0x00000080) != 0) return false;

            // Check if it has an owner. If it does, it must have WS_EX_APPWINDOW style (0x00040000) to be a real app window
            IntPtr owner = GetWindow(hWnd, 4); // GW_OWNER = 4
            if (owner != IntPtr.Zero)
            {
                if ((exStyle & 0x00040000) == 0) return false;
            }

            return true;
        }

        static void RestoreWindowsForProcesses(System.Collections.Generic.HashSet<string> processNames)
        {
            try
            {
                Log("[Recovery] Searching for hidden windows belonging to resumed processes...");
                int restoredCount = 0;
                EnumWindows((hWnd, lParam) =>
                {
                    try
                    {
                        uint pid;
                        GetWindowThreadProcessId(hWnd, out pid);
                        if (pid != 0)
                        {
                            using (var proc = Process.GetProcessById((int)pid))
                            {
                                if (proc != null && processNames.Contains(proc.ProcessName))
                                {
                                    if (IsRealAppWindowHiddenCheck(hWnd))
                                    {
                                        System.Text.StringBuilder title = new System.Text.StringBuilder(512);
                                        GetWindowText(hWnd, title, title.Capacity);
                                        string titleStr = title.ToString().Trim();

                                        ShowWindowAsync(hWnd, SW_SHOW);
                                        restoredCount++;
                                        Log($"[Recovery] Restored window handle {hWnd} (Title: '{titleStr}') for process '{proc.ProcessName}' (PID {pid})");
                                    }
                                    else
                                    {
                                        Log($"[Recovery] Ignored background/hidden window handle {hWnd} for process '{proc.ProcessName}' to avoid exposing ghost window.");
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                    return true;
                }, IntPtr.Zero);
                Log($"[Recovery] Window restoration completed. Restored {restoredCount} window(s).");
            }
            catch (Exception ex)
            {
                Log("[Recovery] Error during window restoration for resumed processes: " + ex.Message);
            }
        }

        static void HideActiveAppWindows()
        {
            if (!hideNonSuspendedWindows) return;

            try
            {
                Log("[Optimizer] Hiding active windows for non-whitelisted processes...");

                // Parse suspension whitelist
                string[] suspListArgs = whitelist.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                System.Collections.Generic.HashSet<string> suspensionWhite = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var w in suspListArgs)
                    suspensionWhite.Add(w.Trim().Replace(".exe", ""));

                // Parse hide whitelist
                string[] hideListArgs = hideWhitelist.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                System.Collections.Generic.HashSet<string> hideWhite = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var w in hideListArgs)
                    hideWhite.Add(w.Trim().Replace(".exe", ""));

                // ThirdPartyApps must never be hidden: they are the apps we optimize FOR
                foreach (var thirdParty in GetThirdPartyAppNames())
                    hideWhite.Add(thirdParty);

                // Ultra-minimal critical safe list of processes whose windows should NEVER be hidden under any circumstances
                // We exclude "dwm" and other system components so that if they have visible windows and are not in hideWhitelist, they CAN be hidden as requested.
                System.Collections.Generic.HashSet<string> criticalSystemList = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "retrobatgamemode", "conhost", "system", "idle"
                };

                var hiddenProcessNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

                EnumWindows((hWnd, lParam) =>
                {
                    try
                    {
                        if (IsRealAppWindow(hWnd))
                        {
                            uint pid;
                            GetWindowThreadProcessId(hWnd, out pid);
                            if (pid != 0 && pid != Process.GetCurrentProcess().Id)
                            {
                                using (var proc = Process.GetProcessById((int)pid))
                                {
                                    if (proc != null)
                                    {
                                        string procName = proc.ProcessName;

                                        // Skip processes running with Administrator / elevated privileges
                                        if (IsProcessElevated(proc.Id))
                                        {
                                            return true;
                                        }

                                        // Condition for hiding a window:
                                        // 1. If the process is in the suspension whitelist, but NOT in the hideWhitelist, we must hide it!
                                        // 2. Otherwise, if it is NOT in the hideWhitelist AND not in the critical system safe list, we hide it!
                                        bool isSuspensionWhitelisted = suspensionWhite.Contains(procName);
                                        bool isHideWhitelisted = hideWhite.Contains(procName);
                                        bool isCriticalSystem = criticalSystemList.Contains(procName);

                                        bool shouldHide = false;
                                        if (isSuspensionWhitelisted && !isHideWhitelisted)
                                        {
                                            shouldHide = true;
                                        }
                                        else if (!isHideWhitelisted && !isCriticalSystem)
                                        {
                                            shouldHide = true;
                                        }

                                        if (shouldHide)
                                        {
                                            // Avoid duplicates
                                            if (!hiddenNonSuspendedWindows.Contains(hWnd))
                                            {
                                                System.Text.StringBuilder title = new System.Text.StringBuilder(512);
                                                GetWindowText(hWnd, title, title.Capacity);
                                                string titleStr = title.ToString().Trim();

                                                hiddenNonSuspendedWindows.Add(hWnd);
                                                hiddenProcessNames.Add(procName);
                                                ShowWindowAsync(hWnd, SW_HIDE);
                                                Log($"[Optimizer] Hid active window {hWnd} (Title: '{titleStr}') of process '{procName}' (PID {pid})");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                    return true;
                }, IntPtr.Zero);

                // Write the list of hidden apps to config.ini so they can be recovered if crashed
                if (hiddenProcessNames.Count > 0)
                {
                    string hiddenAppsStr = string.Join(",", hiddenProcessNames);
                    string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                    WritePrivateProfileString("Settings", "HiddenApps", hiddenAppsStr, iniPath);
                    WritePrivateProfileString("Settings", "LastHiddenApps", hiddenAppsStr, iniPath);
                    WritePrivateProfileString(null, null, null, iniPath);
                    Log($"[Optimizer] Saved hidden apps to config.ini: {hiddenAppsStr}");
                }
            }
            catch (Exception ex)
            {
                Log("[Optimizer] Error hiding active app windows: " + ex.Message);
            }
        }

        static void RestoreActiveAppWindows()
        {
            if (!hideNonSuspendedWindows) return;

            Log("[Optimizer] Restoring hidden active windows...");
            foreach (var hWnd in hiddenNonSuspendedWindows)
            {
                try
                {
                    ShowWindowAsync(hWnd, SW_SHOW);
                    Log($"[Restore] Restored active window handle {hWnd}");
                }
                catch (Exception ex)
                {
                    Log($"[Restore] Error restoring hidden active window {hWnd}: {ex.Message}");
                }
            }
            hiddenNonSuspendedWindows.Clear();

            try
            {
                string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                WritePrivateProfileString("Settings", "HiddenApps", "", iniPath);
                WritePrivateProfileString(null, null, null, iniPath); // Flush to disk
                Log("Cleared HiddenApps from INI.");
            }
            catch (Exception ex)
            {
                Log("Error clearing HiddenApps from INI: " + ex.Message);
            }
        }

        static void RecoverHiddenWindows(string iniPath)
        {
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder(2048);
                GetPrivateProfileString("Settings", "HiddenApps", "", sb, sb.Capacity, iniPath);
                string hiddenAppsStr = sb.ToString();

                if (string.IsNullOrEmpty(hiddenAppsStr))
                {
                    GetPrivateProfileString("Settings", "LastHiddenApps", "", sb, sb.Capacity, iniPath);
                    hiddenAppsStr = sb.ToString();
                }

                if (!string.IsNullOrEmpty(hiddenAppsStr))
                {
                    Log("Found hidden apps from a previous session: " + hiddenAppsStr);
                    string[] appNames = hiddenAppsStr.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    var hiddenProcessNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var appName in appNames)
                    {
                        hiddenProcessNames.Add(appName.Trim());
                    }

                    RestoreWindowsForProcesses(hiddenProcessNames);

                    // Clear the key in INI since we have restored them
                    WritePrivateProfileString("Settings", "HiddenApps", "", iniPath);
                    WritePrivateProfileString(null, null, null, iniPath);
                }
            }
            catch (Exception ex)
            {
                Log("Error during recovery of hidden apps: " + ex.Message);
            }
        }

        static void RunWatchdog(int parentPid)
        {
            // Hide own console window to stay silent in background
            IntPtr consoleWindow = GetConsoleWindow();
            if (consoleWindow != IntPtr.Zero)
            {
                ShowWindow(consoleWindow, SW_HIDE);
            }

            Log("[Watchdog] Started. Monitoring parent process " + parentPid + "...");

            Process? parentProcess = null;
            try
            {
                parentProcess = Process.GetProcessById(parentPid);
            }
            catch
            {
                Log("[Watchdog] Parent process " + parentPid + " is not running. Watchdog exiting.");
                return;
            }

            // Wait for parent process to exit
            while (parentProcess != null && !parentProcess.HasExited)
            {
                Thread.Sleep(500);
            }

            Log("[Watchdog] Parent process exited. Checking for clean exit signal...");
            bool cleanExit = false;
            try
            {
                using (var cleEvent = EventWaitHandle.OpenExisting("RetroBatGameModeCleanExit_" + parentPid))
                {
                    cleanExit = cleEvent.WaitOne(200);
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // cleanExit remains false (parent crashed or was terminated)
            }
            catch (Exception ex)
            {
                Log("[Watchdog] Error checking clean exit event: " + ex.Message);
            }

            if (cleanExit)
            {
                Log("[Watchdog] Parent process exited cleanly. Watchdog exiting without taking action.");
                return;
            }

            Log("[Watchdog] Parent process terminated abnormally! Initiating system state restoration...");

            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
            bool killExplorerVal = ReadIniBool("Settings", "KillExplorer", true, iniPath);
            bool suspendAppsVal = ReadIniBool("Settings", "SuspendBackgroundApps", true, iniPath);
            bool hideWindowsVal = ReadIniBool("Settings", "HideNonSuspendedWindows", true, iniPath);

            // 1. Resume suspended processes using LastSuspendedApps or SuspendedApps
            if (suspendAppsVal)
            {
                try
                {
                    string suspendedAppsStr = ReadIniString("Settings", "SuspendedApps", "", iniPath);
                    if (string.IsNullOrEmpty(suspendedAppsStr))
                    {
                        suspendedAppsStr = ReadIniString("Settings", "LastSuspendedApps", "", iniPath);
                    }

                    if (!string.IsNullOrEmpty(suspendedAppsStr))
                    {
                        Log("[Watchdog] Resuming processes from previous session: " + suspendedAppsStr);
                        string[] appNames = suspendedAppsStr.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        var resumedProcessNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var appName in appNames)
                        {
                            string trimmedName = appName.Trim();
                            if (string.IsNullOrEmpty(trimmedName)) continue;
                            resumedProcessNames.Add(trimmedName);

                            var processes = Process.GetProcessesByName(trimmedName);
                            foreach (var proc in processes)
                            {
                                try
                                {
                                    IntPtr hProcess = OpenProcess(PROCESS_SUSPEND_RESUME, false, proc.Id);
                                    if (hProcess != IntPtr.Zero)
                                    {
                                        NtResumeProcess(hProcess);
                                        CloseHandle(hProcess);
                                        Log("[Watchdog] Successfully resumed process: " + proc.ProcessName + " (PID " + proc.Id + ")");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log("[Watchdog] Failed to resume process " + trimmedName + ": " + ex.Message);
                                }
                            }
                        }

                        // Restore hidden windows for these resumed processes
                        RestoreWindowsForProcesses(resumedProcessNames);
                    }
                }
                catch (Exception ex)
                {
                    Log("[Watchdog] Error resuming suspended processes: " + ex.Message);
                }
            }

            // 1.5. Restore hidden windows of non-suspended apps using LastHiddenApps or HiddenApps
            if (hideWindowsVal)
            {
                try
                {
                    string hiddenAppsStr = ReadIniString("Settings", "HiddenApps", "", iniPath);
                    if (string.IsNullOrEmpty(hiddenAppsStr))
                    {
                        hiddenAppsStr = ReadIniString("Settings", "LastHiddenApps", "", iniPath);
                    }

                    if (!string.IsNullOrEmpty(hiddenAppsStr))
                    {
                        Log("[Watchdog] Restoring hidden windows from previous session: " + hiddenAppsStr);
                        string[] appNames = hiddenAppsStr.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        var hiddenProcessNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var appName in appNames)
                        {
                            string trimmedName = appName.Trim();
                            if (string.IsNullOrEmpty(trimmedName)) continue;
                            hiddenProcessNames.Add(trimmedName);
                        }

                        // Restore hidden windows for these processes
                        RestoreWindowsForProcesses(hiddenProcessNames);
                    }
                }
                catch (Exception ex)
                {
                    Log("[Watchdog] Error restoring hidden windows: " + ex.Message);
                }
            }

            // 2. Restore Explorer elements
            if (killExplorerVal)
            {
                try
                {
                    Log("[Watchdog] Restoring Explorer.exe components...");
                    IntPtr tray = FindWindow("Shell_TrayWnd", null);
                    if (tray != IntPtr.Zero) ShowWindow(tray, SW_SHOW);

                    IntPtr secondaryTray = FindWindow("Shell_SecondaryTrayWnd", null);
                    if (secondaryTray != IntPtr.Zero) ShowWindow(secondaryTray, SW_SHOW);

                    IntPtr startBtn = FindWindow("Button", "Start");
                    if (startBtn != IntPtr.Zero) ShowWindow(startBtn, SW_SHOW);

                    IntPtr progman = FindWindow("Progman", null);
                    if (progman != IntPtr.Zero) ShowWindow(progman, SW_SHOW);

                    // Restore hidden folder windows (CabinetWClass)
                    EnumWindows((hWnd, lParam) =>
                    {
                        try
                        {
                            System.Text.StringBuilder className = new System.Text.StringBuilder(256);
                            GetClassName(hWnd, className, className.Capacity);
                            if (className.ToString() == "CabinetWClass")
                            {
                                if (!IsWindowVisible(hWnd))
                                {
                                    ShowWindowAsync(hWnd, SW_SHOW);
                                    Log("[Watchdog] Restored hidden folder window: " + hWnd);
                                }
                            }
                        }
                        catch { }
                        return true;
                    }, IntPtr.Zero);
                }
                catch (Exception ex)
                {
                    Log("[Watchdog] Error restoring explorer components: " + ex.Message);
                }
            }

            // 3. Clear SuspendedApps and HiddenApps from INI so we don't restore them again next time
            try
            {
                WritePrivateProfileString("Settings", "SuspendedApps", "", iniPath);
                WritePrivateProfileString("Settings", "HiddenApps", "", iniPath);
                WritePrivateProfileString(null, null, null, iniPath); // Flush
            }
            catch { }

            Log("[Watchdog] Restoration completed. Watchdog exiting.");
        }

        static void EnsureConfigWithComments(string iniPath)
        {
            try
            {
                // Migration: read EnableOptimization (old key) → Enable (new key)
                bool migratedEnabled = enabled;
                bool hadLegacyKey = false;
                if (File.Exists(iniPath))
                {
                    string legacyVal = ReadIniString("Settings", "EnableOptimization", "", iniPath);
                    if (!string.IsNullOrEmpty(legacyVal))
                    {
                        migratedEnabled = legacyVal.ToLower() == "true" || legacyVal == "1" || legacyVal == "yes";
                        hadLegacyKey = true;
                    }
                    // Check if we need to add missing new options (StandaloneMonitor, ThirdPartyApps)
                    // by reading them and checking if they're at default (which means they weren't in file)
                    string thirdPartyCheck = ReadIniString("Settings", "ThirdPartyApps", "NOT_PRESENT", iniPath);
                    if (thirdPartyCheck == "NOT_PRESENT")
                    {
                        // Neither new option exists in this old config, we need to rewrite to add them
                        Log("[Migration] Adding missing options 'StandaloneMonitor' and 'ThirdPartyApps' to config.ini.");
                    }
                }

                if (!File.Exists(iniPath))
                {
                    // Write all settings with explanatory comments
                    EnsureConfigWithCommentsWrite(iniPath, enabled, true);
                }
                else if (hadLegacyKey || ReadIniString("Settings", "ThirdPartyApps", "NOT_PRESENT", iniPath) == "NOT_PRESENT")
                {
                    // Migrate: read all existing values then rewrite with comments + renamed key + new options
                    bool curKillExplorer = ReadIniBool("Settings", "KillExplorer", killExplorer, iniPath);
                    bool curEmptyWorkingSet = ReadIniBool("Settings", "EmptyWorkingSet", emptyWorkingSet, iniPath);
                    bool curSuspendBackgroundApps = ReadIniBool("Settings", "SuspendBackgroundApps", suspendBackgroundApps, iniPath);
                    bool curShowOverlay = ReadIniBool("Settings", "ShowOverlay", showOverlay, iniPath);
                    string curLanguage = ReadIniString("Settings", "Language", language, iniPath);
                    bool curAutoStartWithRetroBat = ReadIniBool("Settings", "AutoStartWithRetroBat", autoStartWithRetroBat, iniPath);
                    bool curShowConsole = ReadIniBool("Settings", "ShowConsole", showConsole, iniPath);
                    bool curLogToFile = ReadIniBool("Settings", "LogToFile", logToFile, iniPath);
                    string curWhitelist = ReadIniString("Settings", "Whitelist", whitelist, iniPath);
                    bool curHideNonSuspendedWindows = ReadIniBool("Settings", "HideNonSuspendedWindows", hideNonSuspendedWindows, iniPath);
                    string curHideWhitelist = ReadIniString("Settings", "HideWhitelist", hideWhitelist, iniPath);
                    bool curStandaloneMonitor = ReadIniBool("Settings", "StandaloneMonitor", standaloneMonitor, iniPath);
                    string curThirdPartyApps = ReadIniString("Settings", "ThirdPartyApps", thirdPartyApps, iniPath);

                    // We must preserve runtime state keys (SuspendedApps, HiddenApps, etc.)
                    string curSuspendedApps = ReadIniString("Settings", "SuspendedApps", "", iniPath);
                    string curLastSuspendedApps = ReadIniString("Settings", "LastSuspendedApps", "", iniPath);
                    string curHiddenApps = ReadIniString("Settings", "HiddenApps", "", iniPath);
                    string curLastHiddenApps = ReadIniString("Settings", "LastHiddenApps", "", iniPath);

                    if (hadLegacyKey)
                    {
                        Log("[Migration] Migrating config.ini: 'EnableOptimization' → 'Enable', adding comments.");
                    }
                    EnsureConfigWithCommentsWrite(iniPath, migratedEnabled, false, curKillExplorer, curEmptyWorkingSet,
                        curSuspendBackgroundApps, curShowOverlay, curLanguage, curAutoStartWithRetroBat,
                        curShowConsole, curLogToFile, curWhitelist, curHideNonSuspendedWindows, curHideWhitelist,
                        curStandaloneMonitor, curThirdPartyApps,
                        curSuspendedApps, curLastSuspendedApps, curHiddenApps, curLastHiddenApps);
                }
            }
            catch (Exception ex)
            {
                Log("Error ensuring config.ini with comments: " + ex.Message);
            }
        }

        static void EnsureConfigWithCommentsWrite(string iniPath,
            bool writeEnabled, bool isFresh,
            bool pKillExplorer = true, bool pEmptyWorkingSet = true,
            bool pSuspendBackgroundApps = true, bool pShowOverlay = true,
            string pLanguage = "system", bool pAutoStartWithRetroBat = true,
            bool pShowConsole = false, bool pLogToFile = true,
            string pWhitelist = "", bool pHideNonSuspendedWindows = true,
            string pHideWhitelist = "",
            bool pStandaloneMonitor = false, string pThirdPartyApps = "",
            string pSuspendedApps = "", string pLastSuspendedApps = "",
            string pHiddenApps = "", string pLastHiddenApps = "")
        {
            // Build the INI file manually with explanatory comments (WritePrivateProfileString cannot write comments)
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("; ====================================================================");
            sb.AppendLine("; RETROBAT GAME MODE OPTIMIZER CONFIGURATION FILE");
            sb.AppendLine("; All options below are hot-reloadable: editing this file while the");
            sb.AppendLine("; program is running applies changes instantly (live mode) without");
            sb.AppendLine("; restarting the process. Toggle 'Enable' to enable/disable on the fly.");
            sb.AppendLine("; ====================================================================");
            sb.AppendLine();
            sb.AppendLine("[Settings]");
            sb.AppendLine();
            sb.AppendLine("; --- Master switch --------------------------------------------------");
            sb.AppendLine("; Enable  : Enable (true) or disable (false) all optimizations at");
            sb.AppendLine(";           once. When set to false the system is fully restored and");
            sb.AppendLine(";           the process keeps running, waiting for 'Enable=true' to");
            sb.AppendLine(";           re-apply optimizations. (Hot-reloadable)");
            sb.AppendLine("Enable=" + (writeEnabled ? "true" : "false"));
            sb.AppendLine();
            sb.AppendLine("; --- Explorer / Shell ------------------------------------------------");
            sb.AppendLine("; KillExplorer : Hide the taskbar, desktop icons and Explorer folder");
            sb.AppendLine(";                windows to free resources and avoid focus stealing.");
            sb.AppendLine(";                The explorer.exe process is left alive to prevent");
            sb.AppendLine(";                Windows from auto-restarting it. (Hot-reloadable)");
            sb.AppendLine("KillExplorer=" + (pKillExplorer ? "true" : "false"));
            sb.AppendLine();
            sb.AppendLine("; --- Memory ----------------------------------------------------------");
            sb.AppendLine("; EmptyWorkingSet : Force Windows to trim the working set (RAM)");
            sb.AppendLine(";                   of all accessible processes to reclaim memory.");
            sb.AppendLine(";                   (Hot-reloadable)");
            sb.AppendLine("EmptyWorkingSet=" + (pEmptyWorkingSet ? "true" : "false"));
            sb.AppendLine();
            sb.AppendLine("; --- Background apps -------------------------------------------------");
            sb.AppendLine("; SuspendBackgroundApps : Suspend (freeze) non-essential background");
            sb.AppendLine(";                         applications (browsers, launchers, etc.)");
            sb.AppendLine(";                         to free CPU/GPU for the game. Elevated");
            sb.AppendLine(";                         processes are never suspended.");
            sb.AppendLine(";                         (Hot-reloadable)");
            sb.AppendLine("SuspendBackgroundApps=" + (pSuspendBackgroundApps ? "true" : "false"));
            sb.AppendLine();
            sb.AppendLine("; --- Window hiding ---------------------------------------------------");
            sb.AppendLine("; HideNonSuspendedWindows : Hide visible windows of apps that are not");
            sb.AppendLine(";                          in the HideWhitelist, even if they are not");
            sb.AppendLine(";                          suspended, to get a clean desktop.");
            sb.AppendLine(";                          (Hot-reloadable)");
            sb.AppendLine("HideNonSuspendedWindows=" + (pHideNonSuspendedWindows ? "true" : "false"));
            sb.AppendLine();
            sb.AppendLine("; --- Overlay / UI -----------------------------------------------------");
            sb.AppendLine("; ShowOverlay : Show a brief on-screen notification when Game Mode is");
            sb.AppendLine(";              enabled (green) or disabled (red).");
            sb.AppendLine(";              (Hot-reloadable)");
            sb.AppendLine("ShowOverlay=" + (pShowOverlay ? "true" : "false"));
            sb.AppendLine();
            sb.AppendLine("; --- Language ---------------------------------------------------------");
            sb.AppendLine("; Language : Language for overlay messages. Values: 'system' (auto),");
            sb.AppendLine(";            'fr' (French) or 'en' (English). (Hot-reloadable)");
            sb.AppendLine("Language=" + pLanguage);
            sb.AppendLine();
            sb.AppendLine("; --- Auto-start -------------------------------------------------------");
            sb.AppendLine("; AutoStartWithRetroBat : Automatically generate a startup script in");
            sb.AppendLine(";                        RetroBat's EmulationStation start scripts");
            sb.AppendLine(";                        folder so this optimizer launches with ES.");
            sb.AppendLine(";                        (Applied at startup only)");
            sb.AppendLine("AutoStartWithRetroBat=" + (pAutoStartWithRetroBat ? "true" : "false"));
            sb.AppendLine();
            sb.AppendLine("; --- Console ----------------------------------------------------------");
            sb.AppendLine("; ShowConsole : Allocate and show a debug console window alongside");
            sb.AppendLine(";              the process. (Applied at startup only)");
            sb.AppendLine("ShowConsole=" + (pShowConsole ? "true" : "false"));
            sb.AppendLine();
            sb.AppendLine("; --- Logging ----------------------------------------------------------");
            sb.AppendLine("; LogToFile : Write log entries to RetroBatGameMode.log next to the");
            sb.AppendLine(";             executable (rotated at 1 MB). (Hot-reloadable)");
            sb.AppendLine("LogToFile=" + (pLogToFile ? "true" : "false"));
            sb.AppendLine();
            sb.AppendLine("; --- Whitelists -------------------------------------------------------");
            sb.AppendLine("; Whitelist : Comma-separated list of process names that will never");
            sb.AppendLine(";             be suspended. Add emulators or media players here.");
            sb.AppendLine(";             (Hot-reloadable)");
            sb.AppendLine("Whitelist=" + pWhitelist);
            sb.AppendLine();
            sb.AppendLine("; HideWhitelist : Comma-separated list of process names whose");
            sb.AppendLine(";                 windows will never be hidden. Usually contains");
            sb.AppendLine(";                 the emulator/front-end and Explorer.");
            sb.AppendLine(";                 (Hot-reloadable)");
            sb.AppendLine("HideWhitelist=" + pHideWhitelist);
            sb.AppendLine();
            sb.AppendLine("; --- Standalone monitor (third-party app surveillance) -----------------");
            sb.AppendLine("; StandaloneMonitor : Allow the optimizer to keep running and stay");
            sb.AppendLine(";                    active even when RetroBat/EmulationStation is not");
            sb.AppendLine(";                    running. When 'true', the process keeps running in");
            sb.AppendLine(";                    idle/standby mode and starts optimizing as soon as");
            sb.AppendLine(";                    RetroBat OR any process listed in 'ThirdPartyApps'");
            sb.AppendLine(";                    is detected. When 'false' (default), the process");
            sb.AppendLine(";                    exits automatically after RetroBat/EmulationStation");
            sb.AppendLine(";                    closes. (Hot-reloadable)");
            sb.AppendLine("StandaloneMonitor=" + (pStandaloneMonitor ? "true" : "false"));
            sb.AppendLine();
            sb.AppendLine("; ThirdPartyApps : Comma-separated list of additional process names");
            sb.AppendLine(";                  that should ALSO trigger optimizations (in addition");
            sb.AppendLine(";                  to RetroBat/EmulationStation). When any monitored");
            sb.AppendLine(";                  app is running, optimizations are applied. The");
            sb.AppendLine(";                  system is restored only when ALL monitored apps");
            sb.AppendLine(";                  (RetroBat + ThirdPartyApps) have closed.");
            sb.AppendLine(";                  Example: 'chrome, firefox, vlc' (without quotes).");
            sb.AppendLine(";                  (Hot-reloadable). Empty by default.");
            sb.AppendLine("ThirdPartyApps=" + pThirdPartyApps);
            sb.AppendLine();
            sb.AppendLine("; --- Runtime state (do not edit manually) ----------------------------");
            sb.AppendLine("; These keys track active/recoverable state and are managed by the");
            sb.AppendLine("; program and watchdog. Do not modify them manually.");
            sb.AppendLine("SuspendedApps=" + pSuspendedApps);
            sb.AppendLine("LastSuspendedApps=" + pLastSuspendedApps);
            sb.AppendLine("HiddenApps=" + pHiddenApps);
            sb.AppendLine("LastHiddenApps=" + pLastHiddenApps);
            sb.AppendLine();

            File.WriteAllText(iniPath, sb.ToString());
            Log("config.ini " + (isFresh ? "created" : "rewritten") + " with explanatory comments.");
        }

        static string ReadIniString(string section, string key, string defaultValue, string filePath)
        {
            var temp = new System.Text.StringBuilder(255);
            int i = GetPrivateProfileString(section, key, defaultValue, temp, 255, filePath);
            return temp.ToString();
        }

        static bool ReadIniBool(string section, string key, bool defaultValue, string filePath)
        {
            string val = ReadIniString(section, key, defaultValue ? "true" : "false", filePath).ToLower();
            return val == "true" || val == "1" || val == "yes";
        }

        static void Log(string message)
        {
            string formattedMessage = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + message;
            Console.WriteLine(message);

            if (logToFile)
            {
                try
                {
                    string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RetroBatGameMode.log");
                    string oldLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RetroBatGameMode.old");

                    if (File.Exists(logPath))
                    {
                        var fileInfo = new FileInfo(logPath);
                        if (fileInfo.Length >= 1048576) // 1 Mo = 1,048,576 octets
                        {
                            try
                            {
                                if (File.Exists(oldLogPath))
                                {
                                    File.Delete(oldLogPath);
                                }
                                File.Move(logPath, oldLogPath);
                            }
                            catch { }
                        }
                    }

                    File.AppendAllText(logPath, formattedMessage + Environment.NewLine);
                }
                catch
                {
                    try
                    {
                        string fallbackLogPath = Path.Combine(Path.GetTempPath(), "RetroBatGameMode.log");
                        string fallbackOldPath = Path.Combine(Path.GetTempPath(), "RetroBatGameMode.old");

                        if (File.Exists(fallbackLogPath))
                        {
                            var fileInfo = new FileInfo(fallbackLogPath);
                            if (fileInfo.Length >= 1048576)
                            {
                                try
                                {
                                    if (File.Exists(fallbackOldPath))
                                    {
                                        File.Delete(fallbackOldPath);
                                    }
                                    File.Move(fallbackLogPath, fallbackOldPath);
                                }
                                catch { }
                            }
                        }

                        File.AppendAllText(fallbackLogPath, formattedMessage + " (fallback log)" + Environment.NewLine);
                    }
                    catch { }
                }
            }
        }

        static bool IsAdministrator()
        {
            try
            {
                using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                {
                    var principal = new System.Security.Principal.WindowsPrincipal(identity);
                    return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }

        static void Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "--watchdog")
            {
                int parentPid;
                if (int.TryParse(args[1], out parentPid))
                {
                    RunWatchdog(parentPid);
                }
                return;
            }

            // Prevent multiple instances using a robust Process name check (ignoring the watchdog)
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var mainModule = currentProcess.MainModule;
                if (mainModule != null && !string.IsNullOrEmpty(mainModule.FileName))
                {
                    string processName = Path.GetFileNameWithoutExtension(mainModule.FileName);
                    if (Process.GetProcessesByName(processName).Length > 1)
                    {
                        return; // Exit immediately if another main instance of the same exe is running
                    }
                }
            }
            catch { }

            // Double check using Mutex for system-wide atomicity
            bool createdNew;
            appMutex = new Mutex(true, "RetroBatGameModeMutex", out createdNew);
            if (!createdNew)
            {
                appMutex.Dispose();
                return; // Already running
            }

            // Create EventWaitHandle to coordinate clean shutdown with the watchdog
            try
            {
                cleanExitEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "RetroBatGameModeCleanExit_" + Process.GetCurrentProcess().Id);
            }
            catch { }

            // Setup global exception and exit handlers to restore system in case of crash
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Log("CRITICAL: Unhandled exception detected. Force-restoring system state...");
                if (e.ExceptionObject is Exception ex)
                {
                    Log($"Exception details: {ex.Message}");
                }
                RestoreSystem();
            };

            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                if (!isSystemRestored)
                {
                    Log("Process exiting unexpectedly. Force-restoring system state...");
                    RestoreSystem();
                }
            };

            try
            {
                Application.ThreadException += (sender, e) =>
                {
                    Log($"CRITICAL: Thread exception detected: {e.Exception.Message}. Force-restoring system state...");
                    RestoreSystem();
                };
            }
            catch { }

            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

            EnsureConfigWithComments(iniPath);

            if (File.Exists(iniPath))
            {
                enabled = ReadIniBool("Settings", "Enable", enabled, iniPath);
                killExplorer = ReadIniBool("Settings", "KillExplorer", killExplorer, iniPath);
                emptyWorkingSet = ReadIniBool("Settings", "EmptyWorkingSet", emptyWorkingSet, iniPath);
                suspendBackgroundApps = ReadIniBool("Settings", "SuspendBackgroundApps", suspendBackgroundApps, iniPath);
                showOverlay = ReadIniBool("Settings", "ShowOverlay", showOverlay, iniPath);
                language = ReadIniString("Settings", "Language", language, iniPath);
                autoStartWithRetroBat = ReadIniBool("Settings", "AutoStartWithRetroBat", autoStartWithRetroBat, iniPath);
                showConsole = ReadIniBool("Settings", "ShowConsole", showConsole, iniPath);
                logToFile = ReadIniBool("Settings", "LogToFile", logToFile, iniPath);
                whitelist = ReadIniString("Settings", "Whitelist", whitelist, iniPath);
                hideNonSuspendedWindows = ReadIniBool("Settings", "HideNonSuspendedWindows", hideNonSuspendedWindows, iniPath);
                hideWhitelist = ReadIniString("Settings", "HideWhitelist", hideWhitelist, iniPath);
                standaloneMonitor = ReadIniBool("Settings", "StandaloneMonitor", standaloneMonitor, iniPath);
                thirdPartyApps = ReadIniString("Settings", "ThirdPartyApps", thirdPartyApps, iniPath);
            }

            var asmName = System.Reflection.Assembly.GetExecutingAssembly().GetName();
            var asmVersion = asmName.Version;
            string versionStr = asmVersion != null ? "v" + asmVersion.ToString(3) : "v1.2.0";
            Log("RetroBat Game Mode Optimizer Started (" + versionStr + ")");

            if (!IsAdministrator())
            {
                Log("WARNING: Application is not running as Administrator. Some optimizations (like EmptyWorkingSet or SuspendApp on high privileged apps) may fail.");
            }

            // Recovery, console and autostart happen regardless of `enabled` so the live-mode
            // watchdog loop can toggle optimizations dynamically while the process keeps running.
            RecoverSuspendedApps(iniPath);
            RecoverHiddenWindows(iniPath);

            if (showConsole)
            {
                AllocConsole();
                IntPtr handle = GetConsoleWindow();
                if (handle != IntPtr.Zero)
                {
                    ShowWindow(handle, SW_SHOW);
                }

                var writer = new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                Console.SetOut(writer);
            }

            ManageAutoStartScript();

            // In standalone mode, we do NOT exit if RetroBat is absent — we keep running as an idle sentinel.
            // Only the standard mode waits for RetroBat/EmulationStation at startup (30 s timeout).
            if (!standaloneMonitor && !WaitForEmulationStationToStart(30000))
            {
                Log("EmulationStation not found and StandaloneMonitor=false. Exiting in 5 seconds...");
                if (showConsole) Thread.Sleep(5000);
                return;
            }

            if (standaloneMonitor)
            {
                Log("[Standalone] Standalone monitor mode active. Waiting for any monitored app (RetroBat or ThirdPartyApps)...");
            }
            else
            {
                Log("[Monitor] RetroBat/EmulationStation detected. Entering monitoring loop...");
            }

            try
            {
                isSystemRestored = false;

                // Spawn the watchdog process to ensure system restoration on crash or termination
                try
                {
                    var mainModule = Process.GetCurrentProcess().MainModule;
                    string? currentExe = mainModule?.FileName;
                    if (!string.IsNullOrEmpty(currentExe))
                    {
                        ProcessStartInfo psi = new ProcessStartInfo();
                        psi.FileName = currentExe;
                        psi.Arguments = "--watchdog " + Process.GetCurrentProcess().Id;
                        psi.UseShellExecute = false;
                        psi.CreateNoWindow = true;
                        Process.Start(psi);
                        Log("Spawned watchdog process successfully.");
                    }
                    else
                    {
                        Log("Failed to spawn watchdog process: MainModule or FileName was null.");
                    }
                }
                catch (Exception ex)
                {
                    Log("Failed to spawn watchdog process: " + ex.Message);
                }

                RunMonitoringLoop(iniPath);
            }
            catch (Exception ex)
            {
                Log($"Error during optimization execution: {ex.Message}");
            }
            finally
            {
                RestoreSystem();
                if (appMutex != null)
                {
                    try { appMutex.Dispose(); } catch { }
                }
            }

            if (showConsole)
            {
                Log("Optimization finished. Console closing in 3 seconds...");
                Thread.Sleep(3000);
            }
        }

        static void ApplyOptimizations()
        {
            Log("[Optimizer] Applying optimizations...");
            try
            {
                if (killExplorer) KillExplorer();
                if (suspendBackgroundApps) SuspendApps();
                if (hideNonSuspendedWindows) HideActiveAppWindows();
                if (emptyWorkingSet) EmptyAllWorkingSets();
            }
            catch (Exception ex)
            {
                Log("[Optimizer] Error applying optimizations: " + ex.Message);
            }
        }

        static void UndoOptimizations(bool suspendWasActive, bool hideWasActive, bool killWasActive)
        {
            Log("[Optimizer] Undoing optimizations...");
            try
            {
                if (suspendWasActive) ResumeApps();
                if (hideWasActive) RestoreActiveAppWindows();
                if (killWasActive) StartExplorer();
            }
            catch (Exception ex)
            {
                Log("[Optimizer] Error undoing optimizations: " + ex.Message);
            }
        }

        static void RestoreSystem()
        {
            if (isSystemRestored) return;
            isSystemRestored = true;

            if (cleanExitEvent != null)
            {
                try
                {
                    cleanExitEvent.Set();
                    cleanExitEvent.Close();
                }
                catch { }
            }

            Log("Restoring System...");
            UndoOptimizations(suspendBackgroundApps, hideNonSuspendedWindows, killExplorer);
            if (showOverlay)
            {
                ShowNotification(GetLangMessage("restoring"), true);
                Thread.Sleep(4000); // Laisse l'overlay s'afficher 4 secondes avant de fermer
            }
        }

        static void KillExplorer()
        {
            Log("explorer.exe optimization: Hiding the Taskbar, Desktop, and Folder windows to prevent auto-restart loops...");
            try
            {
                IntPtr tray = FindWindow("Shell_TrayWnd", null);
                if (tray != IntPtr.Zero) ShowWindow(tray, SW_HIDE);

                IntPtr secondaryTray = FindWindow("Shell_SecondaryTrayWnd", null);
                if (secondaryTray != IntPtr.Zero) ShowWindow(secondaryTray, SW_HIDE);

                IntPtr startBtn = FindWindow("Button", "Start");
                if (startBtn != IntPtr.Zero) ShowWindow(startBtn, SW_HIDE);

                IntPtr progman = FindWindow("Progman", null);
                if (progman != IntPtr.Zero) ShowWindow(progman, SW_HIDE);

                // Hide all explorer folder windows
                EnumWindows((hWnd, lParam) =>
                {
                    if (IsWindowVisible(hWnd))
                    {
                        System.Text.StringBuilder className = new System.Text.StringBuilder(256);
                        GetClassName(hWnd, className, className.Capacity);
                        if (className.ToString() == "CabinetWClass")
                        {
                            hiddenWindows.Add(hWnd);
                            ShowWindowAsync(hWnd, SW_HIDE);
                        }
                    }
                    return true;
                }, IntPtr.Zero);

                Log("Taskbar, Desktop, and Folders hidden. (Explorer process left alive to prevent Winlogon restart).");
            }
            catch (Exception ex)
            {
                Log("Error hiding explorer.exe elements: " + ex.Message);
            }
        }

        static void StartExplorer()
        {
            Log("Starting/Restoring explorer.exe...");

            try
            {
                // Restore hidden windows in case it was only hidden
                IntPtr tray = FindWindow("Shell_TrayWnd", null);
                if (tray != IntPtr.Zero) ShowWindow(tray, SW_SHOW);

                IntPtr secondaryTray = FindWindow("Shell_SecondaryTrayWnd", null);
                if (secondaryTray != IntPtr.Zero) ShowWindow(secondaryTray, SW_SHOW);

                IntPtr startBtn = FindWindow("Button", "Start");
                if (startBtn != IntPtr.Zero) ShowWindow(startBtn, SW_SHOW);

                IntPtr progman = FindWindow("Progman", null);
                if (progman != IntPtr.Zero) ShowWindow(progman, SW_SHOW);

                foreach (var hWnd in hiddenWindows)
                {
                    try { ShowWindowAsync(hWnd, SW_SHOW); } catch { }
                }
                hiddenWindows.Clear();

                // Disaster recovery: Search and restore ANY CabinetWClass (Explorer folder) windows that are currently hidden
                EnumWindows((hWnd, lParam) =>
                {
                    try
                    {
                        System.Text.StringBuilder className = new System.Text.StringBuilder(256);
                        GetClassName(hWnd, className, className.Capacity);
                        if (className.ToString() == "CabinetWClass")
                        {
                            if (!IsWindowVisible(hWnd))
                            {
                                ShowWindowAsync(hWnd, SW_SHOW);
                            }
                        }
                    }
                    catch { }
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log($"Error restoring windows in StartExplorer: {ex.Message}");
            }

            // Small delay to let processes settle before checking
            Thread.Sleep(500);

            // Attempt to start explorer and verify it's running
            int attempts = 3;
            while (attempts > 0)
            {
                if (Process.GetProcessesByName("explorer").Length == 0)
                {
                    try
                    {
                        Process.Start("explorer.exe");
                        Log("explorer.exe started.");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log("Error starting explorer.exe: " + ex.Message);
                    }
                }
                else
                {
                    Log("explorer.exe is already running.");
                    break;
                }
                attempts--;
                Thread.Sleep(1000);
            }
        }

        static string GetRetroBatPath()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\RetroBat"))
                {
                    if (key != null)
                    {
                        string? path = key.GetValue("InstallPath") as string ?? key.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrEmpty(path)) return path;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Registry read error for RetroBat: " + ex.Message);
            }

            string? currentDir = AppDomain.CurrentDomain.BaseDirectory;
            if (currentDir != null)
            {
                currentDir = currentDir.TrimEnd('\\');
                while (currentDir != null)
                {
                    if (Directory.Exists(Path.Combine(currentDir, "emulationstation")))
                    {
                        return currentDir;
                    }
                    var parent = Directory.GetParent(currentDir);
                    currentDir = parent != null ? parent.FullName : null;
                }
            }

            return AppDomain.CurrentDomain.BaseDirectory ?? "";
        }

        static void ManageAutoStartScript()
        {
            try
            {
                string retroBatPath = GetRetroBatPath();
                string scriptsDir = Path.Combine(retroBatPath, "emulationstation", ".emulationstation", "scripts", "start");
                string batFile = Path.Combine(scriptsDir, "RetroBatGameMode.bat");

                if (autoStartWithRetroBat)
                {
                    if (!Directory.Exists(scriptsDir))
                    {
                        try { Directory.CreateDirectory(scriptsDir); } catch { }
                    }

                    if (Directory.Exists(scriptsDir))
                    {
                        string exePath = AppDomain.CurrentDomain.BaseDirectory + "RetroBatGameMode.exe";
                        try
                        {
                            var mainModule = Process.GetCurrentProcess().MainModule;
                            if (mainModule != null && !string.IsNullOrEmpty(mainModule.FileName))
                            {
                                exePath = mainModule.FileName;
                            }
                        }
                        catch { }

                        string content = $"@echo off\r\nstart \"\" \"{exePath}\"";
                        File.WriteAllText(batFile, content);
                        Log($"Generated script in {batFile}");
                    }
                }
                else
                {
                    if (File.Exists(batFile))
                    {
                        try
                        {
                            File.Delete(batFile);
                            Log($"Deleted script {batFile}");
                        }
                        catch (Exception ex)
                        {
                            Log("Could not delete auto start script: " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Error managing auto start script: " + ex.Message);
            }
        }

        static System.Collections.Generic.HashSet<int> GetWindowedProcessIds()
        {
            var pids = new System.Collections.Generic.HashSet<int>();

            // 1. Collect processes with active main window handles
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (proc.MainWindowHandle != IntPtr.Zero)
                    {
                        pids.Add(proc.Id);
                    }
                }
                catch { }
            }

            // 2. Supplement with EnumWindows to find visible top-level windows (even if MainWindowHandle is Zero)
            try
            {
                EnumWindows((hWnd, lParam) =>
                {
                    try
                    {
                        if (IsWindowVisible(hWnd))
                        {
                            uint pid;
                            GetWindowThreadProcessId(hWnd, out pid);
                            if (pid != 0)
                            {
                                pids.Add((int)pid);
                            }
                        }
                    }
                    catch { }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }

            return pids;
        }

        static void SuspendApps()
        {
            string[] whitelistArgs = whitelist.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            System.Collections.Generic.HashSet<string> safeList = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "explorer", "emulationstation", "retroarch", "retrobat", "retrobatgamemode", "cmd", "conhost", "taskmgr", "devenv",
                "textinputhost", "searchhost", "startmenuexperiencehost", "shellexperiencehost", "sihost", "ctfmon", "dwm", "taskhostw",
                "runtimebroker", "applicationframehost", "systemsettings", "system", "idle", "winlogon", "csrss", "smss", "lsass",
                "spoolsv", "svchost", "fontdrvhost", "widgets", "gamingservices", "gamingservicesnet", "msedge", "edge", "attractmode",
                "batrun","batrunguardian", "gamebar"
            };

            foreach (var w in whitelistArgs)
                safeList.Add(w.Trim().Replace(".exe", ""));

            // ThirdPartyApps must never be suspended: they are the apps we optimize FOR
            foreach (var thirdParty in GetThirdPartyAppNames())
                safeList.Add(thirdParty);

            var windowedPids = GetWindowedProcessIds();
            var targetProcessNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add standard web browsers / major apps by default
            string[] rawTargets = { "chrome", "firefox", "discord", "opera", "brave", "steam", "epicgames", "galaxy", "uplay", "origin" };
            foreach (var t in rawTargets)
                targetProcessNames.Add(t);

            // Add any other active windowed process name (filtering safelist)
            foreach (var pid in windowedPids)
            {
                try
                {
                    using (var proc = Process.GetProcessById(pid))
                    {
                        if (proc != null && !safeList.Contains(proc.ProcessName))
                        {
                            // Skip processes running with Administrator / elevated privileges
                            if (IsProcessElevated(proc.Id))
                            {
                                continue;
                            }
                            targetProcessNames.Add(proc.ProcessName);
                        }
                    }
                }
                catch { }
            }

            // Minimize ALL windows of the target processes asynchronously before suspending (avoids frozen windows remaining visible)
            foreach (var pid in windowedPids)
            {
                try
                {
                    using (var proc = Process.GetProcessById(pid))
                    {
                        if (proc != null && targetProcessNames.Contains(proc.ProcessName))
                        {
                            // Skip processes running with Administrator / elevated privileges
                            if (IsProcessElevated(proc.Id))
                            {
                                continue;
                            }

                            IntPtr hWnd = proc.MainWindowHandle;
                            if (hWnd != IntPtr.Zero && IsWindowVisible(hWnd))
                            {
                                hiddenWindows.Add(hWnd);
                                ShowWindowAsync(hWnd, SW_HIDE);
                            }
                        }
                    }
                }
                catch { }
            }

            try
            {
                EnumWindows((hWnd, lParam) =>
                {
                    if (IsWindowVisible(hWnd))
                    {
                        uint pid;
                        GetWindowThreadProcessId(hWnd, out pid);
                        if (pid != 0)
                        {
                            try
                            {
                                using (var proc = Process.GetProcessById((int)pid))
                                {
                                    if (proc != null && targetProcessNames.Contains(proc.ProcessName))
                                    {
                                        if (IsProcessElevated(proc.Id))
                                        {
                                            return true;
                                        }

                                        if (!hiddenWindows.Contains(hWnd))
                                        {
                                            hiddenWindows.Add(hWnd);
                                            ShowWindowAsync(hWnd, SW_HIDE);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log($"Error getting process by ID in EnumWindows: {ex.Message}");
                            }
                        }
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log($"Error in EnumWindows for minimization: {ex.Message}");
            }

            // Allow window minimize animations to complete before freezing process execution
            Thread.Sleep(500);

            System.Collections.Generic.List<string> suspendedNames = new System.Collections.Generic.List<string>();

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (process.Id == Process.GetCurrentProcess().Id) continue;
                    if (safeList.Contains(process.ProcessName)) continue;

                    if (targetProcessNames.Contains(process.ProcessName))
                    {
                        // Skip processes running with Administrator / elevated privileges
                        if (IsProcessElevated(process.Id))
                        {
                            continue;
                        }

                        IntPtr hProcess = OpenProcess(PROCESS_SUSPEND_RESUME, false, process.Id);
                        if (hProcess != IntPtr.Zero)
                        {
                            int result = NtSuspendProcess(hProcess);
                            CloseHandle(hProcess);
                            suspendedProcessIds.Add(process.Id);
                            if (!suspendedNames.Contains(process.ProcessName))
                                suspendedNames.Add(process.ProcessName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error suspending process {process.ProcessName} (ID: {process.Id}): {ex.Message}");
                }
            }

            try
            {
                string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                string appsList = string.Join(", ", suspendedNames);
                bool success1 = WritePrivateProfileString("Settings", "SuspendedApps", appsList, iniPath);
                bool success2 = WritePrivateProfileString("Settings", "LastSuspendedApps", appsList, iniPath);
                WritePrivateProfileString(null, null, null, iniPath); // Flush to disk
                Log($"Suspended apps saved to INI (Success={success1 && success2}): " + appsList);
            }
            catch (Exception ex)
            {
                Log($"Failed to write SuspendedApps to INI: {ex.Message}");
            }
        }

        static void ResumeApps()
        {
            Log("Resuming suspended processes...");
            foreach (var pid in suspendedProcessIds)
            {
                try
                {
                    IntPtr hProcess = OpenProcess(PROCESS_SUSPEND_RESUME, false, pid);
                    if (hProcess != IntPtr.Zero)
                    {
                        NtResumeProcess(hProcess);
                        CloseHandle(hProcess);
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error resuming process {pid}: {ex.Message}");
                }
            }
            suspendedProcessIds.Clear();

            Log("Restoring hidden windows...");
            foreach (var hWnd in hiddenWindows)
            {
                try
                {
                    ShowWindowAsync(hWnd, SW_SHOW);
                }
                catch (Exception ex)
                {
                    Log($"Error restoring hidden window {hWnd}: {ex.Message}");
                }
            }
            hiddenWindows.Clear();

            try
            {
                string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                bool success = WritePrivateProfileString("Settings", "SuspendedApps", "", iniPath);
                WritePrivateProfileString(null, null, null, iniPath); // Flush to disk
                Log($"Cleared SuspendedApps from INI (Success={success}).");
            }
            catch (Exception ex)
            {
                Log($"Failed to clear SuspendedApps from INI: {ex.Message}");
            }
            Log("All suspended processes resumed.");
        }

        static void RecoverSuspendedApps(string iniPath)
        {
            try
            {
                string suspendedAppsStr = ReadIniString("Settings", "SuspendedApps", "", iniPath);
                if (!string.IsNullOrEmpty(suspendedAppsStr))
                {
                    Log("Found suspended apps from a previous session: " + suspendedAppsStr);
                    string[] appNames = suspendedAppsStr.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    var resumedProcessNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var appName in appNames)
                    {
                        string trimmedName = appName.Trim();
                        if (string.IsNullOrEmpty(trimmedName)) continue;
                        resumedProcessNames.Add(trimmedName);

                        var processes = Process.GetProcessesByName(trimmedName);
                        if (processes.Length > 0)
                        {
                            Log($"Resuming {processes.Length} instance(s) of '{trimmedName}' from crashed/aborted previous session...");
                            foreach (var proc in processes)
                            {
                                try
                                {
                                    IntPtr hProcess = OpenProcess(PROCESS_SUSPEND_RESUME, false, proc.Id);
                                    if (hProcess != IntPtr.Zero)
                                    {
                                        NtResumeProcess(hProcess);
                                        CloseHandle(hProcess);
                                        Log($"Successfully resumed process: {proc.ProcessName} (PID {proc.Id})");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log($"Failed to resume PID {proc.Id}: {ex.Message}");
                                }
                            }
                        }
                    }

                    // Restore hidden windows for these resumed processes
                    RestoreWindowsForProcesses(resumedProcessNames);

                    // Clear the key in INI since we have resumed them
                    WritePrivateProfileString("Settings", "SuspendedApps", "", iniPath);
                    WritePrivateProfileString(null, null, null, iniPath);
                    Log("Cleared SuspendedApps from INI after recovery.");

                    // DISASTER RECOVERY: Since a previous crash was detected, make sure Explorer elements are restored!
                    if (killExplorer)
                    {
                        Log("Disaster Recovery: Previous crash detected, restoring taskbar, desktop, and folder windows...");
                        StartExplorer();
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Error during recovery of suspended apps: " + ex.Message);
            }
        }

        static void EmptyAllWorkingSets()
        {
            Log("Emptying working sets to reclaim RAM...");
            int count = 0;
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    // Skip processes running with Administrator / elevated privileges
                    if (IsProcessElevated(process.Id))
                    {
                        continue;
                    }

                    IntPtr hProcess = OpenProcess(PROCESS_SET_QUOTA | PROCESS_QUERY_LIMITED_INFORMATION, false, process.Id);
                    if (hProcess == IntPtr.Zero)
                    {
                        hProcess = OpenProcess(PROCESS_SET_QUOTA | PROCESS_QUERY_INFORMATION, false, process.Id);
                    }

                    if (hProcess != IntPtr.Zero)
                    {
                        int result = EmptyWorkingSet(hProcess);
                        CloseHandle(hProcess);
                        if (result != 0) count++;
                    }
                    else
                    {
                        int result = EmptyWorkingSet(process.Handle);
                        if (result != 0) count++;
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error emptying working set for process {process.ProcessName}: {ex.Message}");
                }
            }
            Log($"Reclaimed RAM for {count} processes.");
        }

        static bool WaitForEmulationStationToStart(int timeoutMs)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                if (Process.GetProcessesByName("emulationstation").Length > 0 || Process.GetProcessesByName("retrobat").Length > 0)
                {
                    return true;
                }
                Thread.Sleep(2000);
                waited += 2000;
            }
            return false;
        }

        static System.Collections.Generic.List<string> GetThirdPartyAppNames()
        {
            var list = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(thirdPartyApps))
            {
                string[] apps = thirdPartyApps.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var app in apps)
                {
                    string name = app.Trim().Replace(".exe", "");
                    if (!string.IsNullOrEmpty(name)) list.Add(name);
                }
            }
            return list;
        }

        static bool IsRetroBatRunning()
        {
            return Process.GetProcessesByName("emulationstation").Length > 0 ||
                   Process.GetProcessesByName("retrobat").Length > 0;
        }

        static bool IsAnyMonitoredAppRunning()
        {
            if (IsRetroBatRunning()) return true;
            if (!standaloneMonitor) return false;
            if (!string.IsNullOrEmpty(thirdPartyApps))
            {
                string[] apps = thirdPartyApps.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var app in apps)
                {
                    string name = app.Trim().Replace(".exe", "");
                    if (!string.IsNullOrEmpty(name) && Process.GetProcessesByName(name).Length > 0)
                        return true;
                }
            }
            return false;
        }

        static void RunMonitoringLoop(string iniPath)
        {
            DateTime lastRead = File.GetLastWriteTime(iniPath);
            while (true)
            {
                if (requestExit)
                {
                    Log("[Monitor] Exit requested (StandaloneMonitor set to false without RetroBat). Shutting down...");
                    break;
                }

                // In non-standalone mode, only RetroBat/EmulationStation counts.
                // In standalone mode, any monitored app (RetroBat + ThirdPartyApps) counts.
                bool anyRunning = IsAnyMonitoredAppRunning();

                if (!anyRunning && isOptimized)
                {
                    Log("[Monitor] All monitored applications closed. Undoing optimizations...");
                    UndoOptimizations(suspendBackgroundApps, hideNonSuspendedWindows, killExplorer);
                    isOptimized = false;
                    if (showOverlay) ShowNotification(GetLangMessage("restoring"), true);

                    // In non-standalone mode, exit after undoing (RetroBat was the only reason to run)
                    if (!standaloneMonitor)
                    {
                        Log("[Monitor] Non-standalone mode: exiting after RetroBat/EmulationStation closed.");
                        break;
                    }
                }
                else if (anyRunning && !isOptimized && enabled)
                {
                    Log("[Monitor] Monitored application detected. Applying optimizations...");
                    if (showOverlay) ShowNotification(GetLangMessage("optimizing"), false);
                    ApplyOptimizations();
                    isOptimized = true;
                }

                // Poll config.ini for live changes
                try
                {
                    DateTime lastWrite = File.GetLastWriteTime(iniPath);
                    if (lastWrite > lastRead)
                    {
                        lastRead = lastWrite;
                        Thread.Sleep(100); // Small delay to ensure file write is complete
                        ReloadAndApplyConfig(iniPath, ref lastRead);
                    }
                }
                catch { }

                Thread.Sleep(2000);
            }
        }

        static void ReloadAndApplyConfig(string iniPath, ref DateTime lastRead)
        {
            // Snapshot current values to detect what changed
            bool wasEnabled = enabled;
            bool prevKillExplorer = killExplorer;
            bool prevEmptyWorkingSet = emptyWorkingSet;
            bool prevSuspendBackgroundApps = suspendBackgroundApps;
            bool prevHideNonSuspendedWindows = hideNonSuspendedWindows;
            bool prevShowOverlay = showOverlay;
            bool prevStandaloneMonitor = standaloneMonitor;
            string prevWhitelist = whitelist;
            string prevHideWhitelist = hideWhitelist;
            string prevThirdPartyApps = thirdPartyApps;

            // Reload all settings from INI
            enabled = ReadIniBool("Settings", "Enable", enabled, iniPath);
            killExplorer = ReadIniBool("Settings", "KillExplorer", killExplorer, iniPath);
            emptyWorkingSet = ReadIniBool("Settings", "EmptyWorkingSet", emptyWorkingSet, iniPath);
            suspendBackgroundApps = ReadIniBool("Settings", "SuspendBackgroundApps", suspendBackgroundApps, iniPath);
            hideNonSuspendedWindows = ReadIniBool("Settings", "HideNonSuspendedWindows", hideNonSuspendedWindows, iniPath);
            showOverlay = ReadIniBool("Settings", "ShowOverlay", showOverlay, iniPath);
            language = ReadIniString("Settings", "Language", language, iniPath);
            showConsole = ReadIniBool("Settings", "ShowConsole", showConsole, iniPath);
            logToFile = ReadIniBool("Settings", "LogToFile", logToFile, iniPath);
            whitelist = ReadIniString("Settings", "Whitelist", whitelist, iniPath);
            hideWhitelist = ReadIniString("Settings", "HideWhitelist", hideWhitelist, iniPath);
            standaloneMonitor = ReadIniBool("Settings", "StandaloneMonitor", standaloneMonitor, iniPath);
            thirdPartyApps = ReadIniString("Settings", "ThirdPartyApps", thirdPartyApps, iniPath);

            bool anyOptionChanged =
                killExplorer != prevKillExplorer ||
                emptyWorkingSet != prevEmptyWorkingSet ||
                suspendBackgroundApps != prevSuspendBackgroundApps ||
                hideNonSuspendedWindows != prevHideNonSuspendedWindows ||
                showOverlay != prevShowOverlay ||
                whitelist != prevWhitelist ||
                hideWhitelist != prevHideWhitelist ||
                thirdPartyApps != prevThirdPartyApps ||
                standaloneMonitor != prevStandaloneMonitor;

            if (wasEnabled && !enabled)
            {
                Log("[Live] Enable set to false. Undoing all optimizations...");
                UndoOptimizations(prevSuspendBackgroundApps, prevHideNonSuspendedWindows, prevKillExplorer);
                isOptimized = false;
                if (showOverlay) ShowNotification(GetLangMessage("restoring"), true);

                // If we just disabled AND no RetroBat/EmulationStation runs AND non-standalone → exit
                if (!standaloneMonitor && !IsRetroBatRunning())
                {
                    Log("[Live] Non-standalone mode and no RetroBat running. Requesting exit.");
                    requestExit = true;
                }
            }
            else if (!wasEnabled && enabled && IsAnyMonitoredAppRunning())
            {
                Log("[Live] Enable set to true. Applying optimizations...");
                if (showOverlay) ShowNotification(GetLangMessage("optimizing"), false);
                ApplyOptimizations();
                isOptimized = true;
            }
            else if (enabled && anyOptionChanged && isOptimized)
            {
                Log("[Live] Settings changed while enabled. Re-applying optimizations with new values...");
                UndoOptimizations(prevSuspendBackgroundApps, prevHideNonSuspendedWindows, prevKillExplorer);
                ApplyOptimizations();
                // isOptimized remains true
            }
            else if (enabled && anyOptionChanged && !isOptimized && IsAnyMonitoredAppRunning())
            {
                Log("[Live] Settings changed and a monitored app is now running. Applying optimizations...");
                if (showOverlay) ShowNotification(GetLangMessage("optimizing"), false);
                ApplyOptimizations();
                isOptimized = true;
            }
            else
            {
                Log("[Live] Config reloaded (no actionable change detected).");
            }

            // StandaloneMonitor transition true→false: if RetroBat is NOT running, exit the process
            // because non-standalone mode only makes sense while EmulationStation/RetroBat is active.
            if (prevStandaloneMonitor && !standaloneMonitor && !IsRetroBatRunning())
            {
                if (isOptimized)
                {
                    Log("[Live] StandaloneMonitor set to false, no RetroBat running. Undoing optimizations before exit...");
                    UndoOptimizations(prevSuspendBackgroundApps, prevHideNonSuspendedWindows, prevKillExplorer);
                    isOptimized = false;
                    if (showOverlay) ShowNotification(GetLangMessage("restoring"), true);
                }
                Log("[Live] StandaloneMonitor set to false and no RetroBat/EmulationStation running. Requesting exit.");
                requestExit = true;
            }

            // Update lastRead to the real file time AFTER we finished reading (avoid re-trigger loop)
            try
            {
                lastRead = File.GetLastWriteTime(iniPath);
                if (lastRead > DateTime.Now) lastRead = DateTime.Now;
            }
            catch { }
        }

        static string GetLangMessage(string key)
        {
            string lang = language;
            if (lang == "system")
            {
                lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            }

            if (key == "optimizing")
            {
                if (lang == "fr") return "Mode Jeu activé. Optimisation du système en cours...";
                return "Game Mode enabled. Optimizing system...";
            }
            else if (key == "restoring")
            {
                if (lang == "fr") return "Mode Jeu désactivé. Restauration du système...";
                return "Game Mode disabled. Restoring system...";
            }
            return "";
        }

        class OverlayForm : System.Windows.Forms.Form
        {
            protected override bool ShowWithoutActivation => true;

            protected override System.Windows.Forms.CreateParams CreateParams
            {
                get
                {
                    System.Windows.Forms.CreateParams cp = base.CreateParams;
                    // WS_EX_TOPMOST = 0x00000008, WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOOLWINDOW = 0x00000080, WS_EX_TRANSPARENT = 0x00000020
                    cp.ExStyle |= 0x08000008 | 0x08000000 | 0x00000080 | 0x00000020;
                    return cp;
                }
            }

            public OverlayForm(string message, bool isRestoring)
            {
                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
                this.ShowInTaskbar = false;
                this.TopMost = true;
                this.BackColor = System.Drawing.Color.FromArgb(24, 24, 26);
                this.Opacity = 0.90;

                System.Drawing.Rectangle bounds = System.Windows.Forms.Screen.PrimaryScreen != null ? System.Windows.Forms.Screen.PrimaryScreen.Bounds : new System.Drawing.Rectangle(0, 0, 1920, 1080);
                int width = 500;
                int height = 65;
                this.Width = width;
                this.Height = height;
                this.Location = new System.Drawing.Point(bounds.Width / 2 - width / 2, 50);

                System.Windows.Forms.Label lbl = new System.Windows.Forms.Label();
                lbl.Text = message;
                lbl.ForeColor = System.Drawing.Color.White;
                lbl.Font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold);
                lbl.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
                lbl.Dock = System.Windows.Forms.DockStyle.Fill;
                this.Controls.Add(lbl);

                System.Windows.Forms.Panel accent = new System.Windows.Forms.Panel();
                accent.Height = 4;
                accent.Dock = System.Windows.Forms.DockStyle.Bottom;
                accent.BackColor = isRestoring ? System.Drawing.Color.FromArgb(244, 63, 94) : System.Drawing.Color.FromArgb(74, 222, 128); // Rose/Red for Restoring, Green for Optimizing
                this.Controls.Add(accent);
            }
        }

        static void ShowNotification(string message, bool isRestoring = false)
        {
            Thread t = new Thread(() =>
            {
                try
                {
                    OverlayForm overlay = new OverlayForm(message, isRestoring);

                    System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
                    timer.Interval = 3500;
                    timer.Tick += (s, e) => { overlay.Close(); System.Windows.Forms.Application.ExitThread(); };
                    timer.Start();

                    System.Windows.Forms.Application.Run(overlay);
                }
                catch { }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }
    }
}
