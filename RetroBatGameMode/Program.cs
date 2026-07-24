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

        static extern IntPtr GetForegroundWindow();



        [DllImport("user32.dll")]

        static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);



        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]

        static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);



        [DllImport("user32.dll", SetLastError = true)]

        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);



        [DllImport("user32.dll", SetLastError = true)]

        [return: MarshalAs(UnmanagedType.Bool)]

        static extern bool SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);



        const uint MSGFLT_ADD = 0x0001;

        const uint MSGFLT_REMOVE = 0x0002;



        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]

        static extern long BroadcastSystemMessage(uint dwFlags, ref uint lpdwRecipients, uint uiMessage, IntPtr wParam, IntPtr lParam);



        const uint WM_CLOSE = 0x0010;

        const uint MSG_EXIT_EXPLORER = 0x05B4;

        const uint WM_SETTINGCHANGE = 0x001A;

        const IntPtr HWND_BROADCAST = (IntPtr)0xFFFF;

        const IntPtr HWND_TOP = (IntPtr)0;

        const IntPtr HWND_TOPMOST = (IntPtr)(-1);

        const IntPtr HWND_BOTTOM = (IntPtr)1;

        const uint SWP_NOACTIVATE = 0x0010;

        const uint SWP_NOSIZE = 0x0001;

        const uint SWP_NOZORDER = 0x0004;

        const uint SWP_FRAMECHANGED = 0x0020;

        const int  SW_SHOWNOACTIVATE = 4;

        const uint SMTO_ABORTIFHUNG = 0x0008;

        const uint SMTO_NORMAL = 0x0000;

        const uint SMTO_NOTIMEOUTIFNOTHUNG = 0x0008;

        const uint BSF_ALLCOMPONENTS = 0x00000004;

        const uint BSF_IGNORECURRENTTASK = 0x00000002;

        const uint BSF_POSTMESSAGE = 0x00000010;

        const uint BSF_SENDNOTIFYMESSAGE = 0x00000100;



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



        [DllImport("user32.dll", SetLastError = true)]

        [return: MarshalAs(UnmanagedType.Bool)]

        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);



        [DllImport("user32.dll", SetLastError = true)]

        [return: MarshalAs(UnmanagedType.Bool)]

        static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);



        [DllImport("gdi32.dll", SetLastError = true)]

        static extern IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);



        [DllImport("gdi32.dll", SetLastError = true)]

        [return: MarshalAs(UnmanagedType.Bool)]

        static extern bool DeleteObject(IntPtr hObject);





        [DllImport("user32.dll", SetLastError = true)]

        [return: MarshalAs(UnmanagedType.Bool)]

        static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);



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



        const uint ABM_NEW         = 0x00000000;

        const uint ABM_REMOVE      = 0x00000001;

        const uint ABM_QUERYPOS    = 0x00000002;

        const uint ABM_SETPOS      = 0x00000003;

        const uint ABM_GETSTATE    = 0x00000004;

        const uint ABM_GETTASKBARPOS = 0x00000005;

        const uint ABM_ACTIVATE    = 0x00000006;

        const uint ABM_GETAUTOHIDEBAR = 0x00000007;

        const uint ABM_SETAUTOHIDEBAR = 0x00000008;

        const uint ABM_WINDOWPOSCHANGED = 0x00000009;

        const uint ABM_SETSTATE    = 0x0000000A;

        const int ABS_NORMAL = 0x0;

        const int ABS_TOPMOST = 0x1;

        const int ABS_AUTOHIDE = 0x2;

        const int ABE_BOTTOM = 0x3;



        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]

        struct APPBARDATA

        {

            public int cbSize;

            public IntPtr hWnd;

            public uint uCallbackMessage;

            public uint uEdge;

            public RECT rc;

            public IntPtr lParam;

        }



        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]

        static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);



        const uint SPI_GETWORKAREA = 0x0030;

        const uint SPI_SETWORKAREA = 0x002F;

        const uint SPIF_UPDATEINIFILE = 0x0001;

        const uint SPIF_SENDCHANGE = 0x0002;

        const uint SPIF_FULL = SPIF_UPDATEINIFILE | SPIF_SENDCHANGE;



        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]

        [return: MarshalAs(UnmanagedType.Bool)]

        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);



        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]

        [return: MarshalAs(UnmanagedType.Bool)]

        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);



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

        static bool gameBarWidgetEnabled = false;

        static bool taskbarAutoHide = false;

        // 0 = Soft (SW_HIDE + offscreen, no restart explorer = no flicker)

        // 1 = Full (registry write + explorer restart = apps maximalees plein ecran

        //     but 1-2s de flicker Windows a chaque apply/restore).

        static bool taskbarAutoHideMode = true; // 1 = Full pour r_tro-compat configs existantes.

        static int lastTaskbarState = -1;

        static bool autoHideCurrentlyApplied = false;

        static RECT lastWorkArea = new RECT { Left = 0, Top = 0, Right = 0, Bottom = 0 };

        static bool workAreaCurrentlyExtended = false;

        // Original (pre-shuffle) bounding rect of Shell_TrayWnd so we can put it

        // back exactly where it was once we stop the optimization. Saved into the

        // runtime INI key TaskbarSavedRect as well so the watchdog can recover.

        static string configFormatVersion = ""; // Set at startup from AssemblyVersion (v1.2.1.0 -> v1.2.1). Triggers config.ini regen when changed.
        static RECT lastShellTrayRect = new RECT { Left = 0, Top = 0, Right = 0, Bottom = 0 };

        static bool shellTrayMoved = false;

        // v1.5.8: track whether WE flipped the autohide registry bit so

        // we can restore it on undo. Persisted to INI TaskbarAutoHideWasOff.

        static bool savedRegistryAutoHideOff = false;

        // Guard flag: prevents concurrent install/uninstall triggered from

        // both the tray menu and the live INI poll.



        // StandaloneMonitor modes:

        //   Off     = process exits when RetroBat/EmulationStation closes (legacy default)

        //   Monitor = process stays alive, optimizes iff RetroBat/ES OR any ThirdPartyApp is running

        //   Full    = process stays alive, optimizes unconditionally (ThirdPartyApps list ignored)

        enum StandaloneMode { Off, Monitor, Full }

        static StandaloneMode standaloneMonitor = StandaloneMode.Off;



        static StandaloneMode ParseStandaloneMode(string raw)

        {

            if (string.IsNullOrEmpty(raw)) return StandaloneMode.Off;

            string s = raw.Trim().ToLowerInvariant();

            if (s == "full") return StandaloneMode.Full;

            if (s == "true" || s == "1" || s == "yes" || s == "monitor" || s == "on")

                return StandaloneMode.Monitor;

            return StandaloneMode.Off;

        }



        static string StandaloneModeToString(StandaloneMode m)

        {

            if (m == StandaloneMode.Full) return "full";

            if (m == StandaloneMode.Monitor) return "true";

            return "false";

        }

        static bool widgetOperationPending = false;

        static bool isOptimized = false;

        static bool requestExit = false;

        static int targetTimer = 5;

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



            // 4. Restore taskbar autohide state if it was modified by the crashed parent.

            //    The previous taskbar state is persisted in TaskbarAutoHideSavedState.

            if (ReadIniBool("Settings", "TaskbarAutoHide", false, iniPath))

            {

                int saved = ReadIniInt("Settings", "TaskbarAutoHideSavedState", -1, iniPath);

                if (saved >= 0)

                {

                    try

                    {

                        APPBARDATA abd = new APPBARDATA

                        {

                            cbSize = Marshal.SizeOf(typeof(APPBARDATA)),

                            hWnd = FindWindow("Shell_TrayWnd", null),

                            lParam = (IntPtr)saved

                        };

                        SHAppBarMessage(ABM_SETSTATE, ref abd);

                        WritePrivateProfileString("Settings", "TaskbarAutoHideSavedState", "", iniPath);

                        WritePrivateProfileString(null, null, null, iniPath);

                        Log("[Watchdog] Restored taskbar state to " + saved + " (autohide removed).");

                    }

                    catch (Exception ex)

                    {

                        Log("[Watchdog] Error restoring taskbar state: " + ex.Message);

                    }

                }

            }



            // 5. Restore the work area if it was extended by the crashed parent.

            //    WorkAreaSavedRect holds the primary monitor's previous work area.

            try

            {

                string workAreaStr = ReadIniString("Settings", "WorkAreaSavedRect", "", iniPath);

                if (!string.IsNullOrEmpty(workAreaStr))

                {

                    RECT wr = StringToRect(workAreaStr);

                    if (wr.Right > wr.Left && wr.Bottom > wr.Top)

                    {

                        if (SystemParametersInfo(SPI_SETWORKAREA, 0, ref wr, SPIF_FULL))

                        {

                            Log("[Watchdog] Restored primary work area to " + RectToString(wr) + ".");

                        }

                    }

                    WritePrivateProfileString("Settings", "WorkAreaSavedRect", "", iniPath);

                    WritePrivateProfileString(null, null, null, iniPath);

                }

            }

            catch (Exception ex) { Log("[Watchdog] Error restoring work area: " + ex.Message); }



            // 6. Restore Shell_TrayWnd to its original position if the crashed

            //    parent had pushed it off-screen.

            try

            {

                string trayStr = ReadIniString("Settings", "TaskbarSavedRect", "", iniPath);

                if (!string.IsNullOrEmpty(trayStr))

                {

                    RECT trRect = StringToRect(trayStr);

                    if (trRect.Right > trRect.Left && trRect.Bottom > trRect.Top)

                    {

                        IntPtr tray = FindWindow("Shell_TrayWnd", null);

                        if (tray != IntPtr.Zero)

                        {

                            int width = trRect.Right - trRect.Left;

                            int height = trRect.Bottom - trRect.Top;

                            SetWindowPos(tray, IntPtr.Zero, trRect.Left, trRect.Top, width, height,

                                SWP_NOACTIVATE | SWP_NOZORDER);

                            Log("[Watchdog] Restored Shell_TrayWnd to ["

                                + trRect.Left + "," + trRect.Top + " - "

                                + trRect.Right + "," + trRect.Bottom + "].");

                        }

                    }

                    WritePrivateProfileString("Settings", "TaskbarSavedRect", "", iniPath);

                    WritePrivateProfileString(null, null, null, iniPath);

                }

            }

            catch (Exception ex) { Log("[Watchdog] Error restoring Shell_TrayWnd position: " + ex.Message); }





            Log("[Watchdog] Restoration completed. Watchdog exiting.");

        }



        static void EnsureConfigWithComments(string iniPath)

        {

            try

            {

                // Migration: read EnableOptimization (old key) ? Enable (new key)

                bool migratedEnabled = enabled;

                bool hadLegacyKey = false;

                bool needsMigration = false;

                if (File.Exists(iniPath))

                {

                    string legacyVal = ReadIniString("Settings", "EnableOptimization", "", iniPath);

                    if (!string.IsNullOrEmpty(legacyVal))

                    {

                        migratedEnabled = legacyVal.ToLower() == "true" || legacyVal == "1" || legacyVal == "yes";

                        hadLegacyKey = true;

                        needsMigration = true;

                    }

                    // Check if we need to add missing new options (StandaloneMonitor, ThirdPartyApps)

                    // by reading them and checking if they're at default (which means they weren't in file)

                    if (ReadIniString("Settings", "ThirdPartyApps", "NOT_PRESENT", iniPath) == "NOT_PRESENT")

                    {

                        Log("[Migration] Adding missing options 'StandaloneMonitor' and 'ThirdPartyApps' to config.ini.");

                        needsMigration = true;

                    }

                    // Also detect missing newer options (GameBarWidgetEnabled) to migrate them in

                    if (ReadIniString("Settings", "GameBarWidgetEnabled", "NOT_PRESENT", iniPath) == "NOT_PRESENT")

                    {

                        Log("[Migration] Adding missing option 'GameBarWidgetEnabled' to config.ini.");

                        needsMigration = true;

                    }

                    // Detect missing TaskbarAutoHide (autohide taskbar phantom fix)

                    if (ReadIniString("Settings", "TaskbarAutoHide", "NOT_PRESENT", iniPath) == "NOT_PRESENT")

                    {

                        Log("[Migration] Adding missing option 'TaskbarAutoHide' to config.ini.");

                        needsMigration = true;

                    }

                    // Detect missing TaskbarAutoHideMode (Soft/Full selection).

                    if (ReadIniString("Settings", "TaskbarAutoHideMode", "NOT_PRESENT", iniPath) == "NOT_PRESENT")

                    {

                        Log("[Migration] Adding missing option 'TaskbarAutoHideMode' to config.ini.");

                        needsMigration = true;

                    }

                    // Detect missing EmergencyRestore (widget panic back-channel).

                    if (ReadIniString("Settings", "EmergencyRestore", "NOT_PRESENT", iniPath) == "NOT_PRESENT")

                    {

                        Log("[Migration] Adding missing option 'EmergencyRestore' to config.ini.");

                        needsMigration = true;

                    }

                    // Detect outdated StandaloneMonitor comment block (legacy 2-mode documentation).

                    // If the new 'full' mode marker is absent from the comment, refresh the file so

                    // the user sees the up-to-date docs without losing their saved values.

                    try

                    {

                        string iniText = File.ReadAllText(iniPath);

                        if (!iniText.Contains("'full'") && !iniText.Contains("\"full\""))

                        {

                            Log("[Migration] Refreshing outdated StandaloneMonitor comment block in config.ini.");

                            needsMigration = true;

                        }

                        else

                        {

                            // hasFullComment silently tracked but unused (intended: future-proof marker)

                        }

                    }

                    catch (Exception scanEx)

                    {

                        Log("[Migration] Could not scan config.ini for outdated comments: " + scanEx.Message);

                    }

                }



                if (!File.Exists(iniPath))

                {

                    // Write all settings with explanatory comments

                    EnsureConfigWithCommentsWrite(iniPath, enabled, true,

                        killExplorer, emptyWorkingSet,

                        suspendBackgroundApps, showOverlay,

                        language, autoStartWithRetroBat,

                        showConsole, logToFile,

                        whitelist, hideNonSuspendedWindows,

                        hideWhitelist,

                        standaloneMonitor, thirdPartyApps,

                        targetTimer,

                        "", "", "", "",

                        gameBarWidgetEnabled, false);

                }

                else if (needsMigration)

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

                    StandaloneMode curStandaloneMonitor = ParseStandaloneMode(ReadIniString("Settings", "StandaloneMonitor", StandaloneModeToString(standaloneMonitor), iniPath));

                    string curThirdPartyApps = ReadIniString("Settings", "ThirdPartyApps", thirdPartyApps, iniPath);

                    bool curGameBarWidgetEnabled = ReadIniBool("Settings", "GameBarWidgetEnabled", gameBarWidgetEnabled, iniPath);

                    bool curTaskbarAutoHide = ReadIniBool("Settings", "TaskbarAutoHide", taskbarAutoHide, iniPath);

                    bool curTaskbarAutoHideMode = ReadIniBool("Settings", "TaskbarAutoHideMode", taskbarAutoHideMode, iniPath);

                    int curTargetTimer = ReadIniInt("Settings", "TargetTimer", targetTimer, iniPath);



                    // We must preserve runtime state keys (SuspendedApps, HiddenApps, etc.)

                    string curSuspendedApps = ReadIniString("Settings", "SuspendedApps", "", iniPath);

                    string curLastSuspendedApps = ReadIniString("Settings", "LastSuspendedApps", "", iniPath);

                    string curHiddenApps = ReadIniString("Settings", "HiddenApps", "", iniPath);

                    string curLastHiddenApps = ReadIniString("Settings", "LastHiddenApps", "", iniPath);



                    if (hadLegacyKey)

                    {

                        Log("[Migration] Migrating config.ini: 'EnableOptimization' ? 'Enable', adding comments.");

                    }

                    EnsureConfigWithCommentsWrite(iniPath, migratedEnabled, false, curKillExplorer, curEmptyWorkingSet,

                        curSuspendBackgroundApps, curShowOverlay, curLanguage, curAutoStartWithRetroBat,

                        curShowConsole, curLogToFile, curWhitelist, curHideNonSuspendedWindows, curHideWhitelist,

                        curStandaloneMonitor, curThirdPartyApps, curTargetTimer,

                        curSuspendedApps, curLastSuspendedApps, curHiddenApps, curLastHiddenApps,

                        curGameBarWidgetEnabled, curTaskbarAutoHide, curTaskbarAutoHideMode,

                        ReadIniString("Settings", "EmergencyRestore", "0", iniPath));

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

            StandaloneMode pStandaloneMonitor = StandaloneMode.Off, string pThirdPartyApps = "",

            int pTargetTimer = 5,

            string pSuspendedApps = "", string pLastSuspendedApps = "",

            string pHiddenApps = "", string pLastHiddenApps = "",

            bool pGameBarWidgetEnabled = false,

            bool pTaskbarAutoHide = false,

            bool pTaskbarAutoHideMode = true,

            string pEmergencyRestore = "0")

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

            sb.AppendLine("; ConfigFormatVersion : auto-written by the optimizer at regeneration");

            sb.AppendLine("; time. Lets the running backend detect when the file was last rewritten");

            sb.AppendLine("; by a NEW backend version, so comment / option updates take effect");

            sb.AppendLine("; automatically on the next start. DO NOT edit by hand.");

            sb.AppendLine("ConfigFormatVersion=" + configFormatVersion);

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

            sb.AppendLine("; --- Taskbar auto-hide -----------------------------------------------");

            sb.AppendLine("; TaskbarAutoHide : When KillExplorer=true, additionally tell the Windows");

            sb.AppendLine(";                  shell to enter auto-hide (ABM_SETSTATE ABS_AUTOHIDE) so");

            sb.AppendLine(";                  the taskbar \"ghost strip\" no longer reserves screen");

            sb.AppendLine(";                  height. Also extends the work area (SPI_SETWORKAREA) to");

            sb.AppendLine(";                  the full screen bounds on every monitor, AND physically");

            sb.AppendLine(";                  relocates Shell_TrayWnd off-screen via SetWindowPos so");

            sb.AppendLine(";                  DWM can no longer re-reserve the taskbar area after");

            sb.AppendLine(";                  SPI_SETWORKAREA. Lets fullscreen-windowed launchers");

            sb.AppendLine(";                  without a borderless mode actually reach the bottom edge.");

            sb.AppendLine(";                  Three runtime keys back the recovery: the taskbar state");

            sb.AppendLine(";                  (TaskbarAutoHideSavedState), the previous primary");

            sb.AppendLine(";                  work-area rect (WorkAreaSavedRect), and the original");

            sb.AppendLine(";                  Shell_TrayWnd rect (TaskbarSavedRect). All three are");

            sb.AppendLine(";                  restored on stop, hot-reload, or by the watchdog.");

            sb.AppendLine(";                  (Hot-reloadable)");

            sb.AppendLine("TaskbarAutoHide=" + (pTaskbarAutoHide ? "true" : "false"));

            sb.AppendLine("; TaskbarAutoHideMode : Strategy used when TaskbarAutoHide=true. Two");

            sb.AppendLine(";                  values are accepted:");

            sb.AppendLine(";                    false = Soft  (no explorer restart; the bar is hidden");

            sb.AppendLine(";                                via SW_HIDE + SetWindowRgn(empty) + ");

            sb.AppendLine(";                                SetWindowPos off-screen and the work area");

            sb.AppendLine(";                                is extended. Does NOT make maximized non-");

            sb.AppendLine(";                                borderless apps extend to the bottom edge on");

            sb.AppendLine(";                                Win11. Safe, no flicker, Game Bar unaffected.");

            sb.AppendLine(";                    true  = Full  (StuckRects3 setting patched + explorer.exe");

            sb.AppendLine(";                                restarted so the shell reads the new value).");

            sb.AppendLine(";                                Maximized non-borderless apps reach the full");

            sb.AppendLine(";                                bottom of the screen because Windows itself");

            sb.AppendLine(";                                hides/shrinks the taskbar. Costs a 1-2s desktop");

            sb.AppendLine(";                                flicker at every apply/restore. Game Bar is");

            sb.AppendLine(";                                re-mounted after the restart.");

            sb.AppendLine(";                  Default: true (Full) for backward compatibility. Choose");

            sb.AppendLine(";                  false (Soft) if you do not care about Win11 maximized apps");

            sb.AppendLine(";                  and want zero flicker.");

            sb.AppendLine("TaskbarAutoHideMode=" + (pTaskbarAutoHideMode ? "true" : "false"));

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

            sb.AppendLine(";                    running. Three values are accepted:");

            sb.AppendLine(";                      - 'false' (default) : the process exits automatically");

            sb.AppendLine(";                                            when RetroBat/EmulationStation closes.");

            sb.AppendLine(";                      - 'true'  : the process keeps running in idle/standby");

            sb.AppendLine(";                                 mode and starts optimizing as soon as");

            sb.AppendLine(";                                 RetroBat OR any process listed in");

            sb.AppendLine(";                                 'ThirdPartyApps' is detected.");

            sb.AppendLine(";                      - 'full'  : the process keeps running and stays");

            sb.AppendLine(";                                 optimized at all times, regardless of");

            sb.AppendLine(";                                 RetroBat or ThirdPartyApps. Use for a");

            sb.AppendLine(";                                 permanent 'gaming mode' session.");

            sb.AppendLine(";                    (Hot-reloadable. Round-trips Off/Monitor/Full from");

            sb.AppendLine(";                     tray menu and Game Bar widget.)");

            sb.AppendLine("StandaloneMonitor=" + StandaloneModeToString(pStandaloneMonitor));

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

            sb.AppendLine("; --- Target mode -----------------------------------------------------");

            sb.AppendLine("; TargetTimer : Duration in seconds of the countdown shown by the overlay");

            sb.AppendLine(";               when 'Target' mode is activated (from the system tray");

            sb.AppendLine(";               icon or the GameBar widget). At the end of the countdown");

            sb.AppendLine(";               the foreground application is added to ThirdPartyApps at");

            sb.AppendLine(";               runtime (not written back to config.ini).");

            sb.AppendLine(";               Range 1-30. (Hot-reloadable)");

            sb.AppendLine("TargetTimer=" + pTargetTimer);

            sb.AppendLine();

            sb.AppendLine("; --- Game Bar widget -------------------------------------------------");

            sb.AppendLine("; GameBarWidgetEnabled : Install (true) or remove (false) the Windows");

            sb.AppendLine(";                        Game Bar widget allowing to control this");

            sb.AppendLine(";                        optimizer with a controller via the Game Bar");

            sb.AppendLine(";                        overlay (Win+G). Requires Windows Developer");

            sb.AppendLine(";                        Mode (Settings > For developers). The widget");

            sb.AppendLine(";                        files are copied to the Game Bar package");

            sb.AppendLine(";                        LocalState\\Widgets\\RetroBatGameMode folder.");

            sb.AppendLine(";                        (Hot-reloadable)");

            sb.AppendLine("GameBarWidgetEnabled=" + (pGameBarWidgetEnabled ? "true" : "false"));

            sb.AppendLine();

            sb.AppendLine("; --- Runtime state (do not edit manually) ----------------------------");

            sb.AppendLine("; These keys track active/recoverable state and are managed by the");

            sb.AppendLine("; program and watchdog. Do not modify them manually.");

            sb.AppendLine("; EmergencyRestore : [runtime + emergency back-channel] Set to '1' by");

            sb.AppendLine(";                    the Game Bar widget (or any external tool / notepad)");

            sb.AppendLine(";                    to force an immediate full undo of all currently");

            sb.AppendLine(";                    applied optimizations. The backend polls the INI");

            sb.AppendLine(";                    every 2s and processes this flag BEFORE the normal");

            sb.AppendLine(";                    hot-reload path; it also flips Enable=false and then");

            sb.AppendLine(";                    clears the flag. Use this as a panic restore when the");

            sb.AppendLine(";                    HTTP path is broken but the backend is still alive.");

            sb.AppendLine("EmergencyRestore=" + pEmergencyRestore);

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



        static int ReadIniInt(string section, string key, int defaultValue, string filePath)

        {

            string val = ReadIniString(section, key, defaultValue.ToString(), filePath);

            int result;

            if (int.TryParse(val, out result)) return result;

            return defaultValue;

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



            // Resolve the running backend's assembly version NOW so the
            // first regeneration marker is set before we read/modify the
            // config.ini below.
            try
            {
                var asmNameTmp = System.Reflection.Assembly.GetExecutingAssembly().GetName();
                var asmV = asmNameTmp.Version;
                configFormatVersion = asmV != null ? ("v" + asmV.ToString()) : "v1.5.70.0";
            }
            catch (Exception ex) { Log("[Config] version resolve error: " + ex.Message); }

            // v1.5.19: detect config.ini format drift and force a regeneration
            // when the running backend version differs from the one that wrote
            // the file. The marker ConfigFormatVersion=v<assemblyVersion> is
            // appended at the top of generated files, so the user always
            // gets the latest comments/keys without losing their values.
            try
            {
                if (File.Exists(iniPath))
                {
                    string onDisk = ReadIniString("Settings", "ConfigFormatVersion", "", iniPath);
                    if (string.IsNullOrEmpty(onDisk) || onDisk != configFormatVersion)
                    {
                        Log("[Config] version mismatch: on-disk='" + (onDisk ?? "<none>")
                            + "', current='" + configFormatVersion + "' -> regenerating config.ini.");
                        EnsureConfigWithComments(iniPath);
                    }
                }
                else
                {
                    EnsureConfigWithComments(iniPath);
                }
            }
            catch (Exception ex) { Log("[Config] regen check error: " + ex.Message); }

            // EnsureConfigWithComments(iniPath) is called inside the try above now.



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

                standaloneMonitor = ParseStandaloneMode(ReadIniString("Settings", "StandaloneMonitor", StandaloneModeToString(standaloneMonitor), iniPath));

                thirdPartyApps = ReadIniString("Settings", "ThirdPartyApps", thirdPartyApps, iniPath);

                gameBarWidgetEnabled = ReadIniBool("Settings", "GameBarWidgetEnabled", gameBarWidgetEnabled, iniPath);

                targetTimer = ReadIniInt("Settings", "TargetTimer", targetTimer, iniPath);

                gameBarWidgetEnabled = ReadIniBool("Settings", "GameBarWidgetEnabled", gameBarWidgetEnabled, iniPath);

            taskbarAutoHide = ReadIniBool("Settings", "TaskbarAutoHide", taskbarAutoHide, iniPath);

            taskbarAutoHideMode = ReadIniBool("Settings", "TaskbarAutoHideMode", taskbarAutoHideMode, iniPath);

                taskbarAutoHideMode = ReadIniBool("Settings", "TaskbarAutoHideMode", taskbarAutoHideMode, iniPath);

                int savedTaskbarState = ReadIniInt("Settings", "TaskbarAutoHideSavedState", -1, iniPath);

                if (savedTaskbarState >= 0)

                {

                    lastTaskbarState = savedTaskbarState;

                    Log("[Startup] Found TaskbarAutoHideSavedState=" + savedTaskbarState + " from a previous session.");

                    if (!isOptimized)

                    {

                        RestoreTaskbarState(savedTaskbarState);

                    }

                }



                // Stale WorkAreaSavedRect guard: if a previous session crashed after

                // extending the work area but BEFORE persisting TaskbarAutoHideSavedState

                // (or if the user turned TaskbarAutoHide off in the meantime), we still

                // need to restore the work area to its normal bounds.

                string staleWorkArea = ReadIniString("Settings", "WorkAreaSavedRect", "", iniPath);

                if (!string.IsNullOrEmpty(staleWorkArea) && !isOptimized)

                {

                    RECT wr = StringToRect(staleWorkArea);

                    if (wr.Right > wr.Left && wr.Bottom > wr.Top)

                    {

                        SystemParametersInfo(SPI_SETWORKAREA, 0, ref wr, SPIF_FULL);

                        Log("[Startup] Restored stale work area to " + RectToString(wr) + " from previous session.");

                    }

                    try

                    {

                        WritePrivateProfileString("Settings", "WorkAreaSavedRect", "", iniPath);

                        WritePrivateProfileString(null, null, null, iniPath);

                    }

                    catch { }

                }



                // Stale TaskbarSavedRect guard: same idea _ if a previous session

                // crashed after relocating Shell_TrayWnd off-screen, put it back where

                // it belongs so the user has their visible desktop back.

                string staleTrayRect = ReadIniString("Settings", "TaskbarSavedRect", "", iniPath);

                if (!string.IsNullOrEmpty(staleTrayRect) && !isOptimized)

                {

                    RECT trRect = StringToRect(staleTrayRect);

                    if (trRect.Right > trRect.Left && trRect.Bottom > trRect.Top)

                    {

                        try

                        {

                            IntPtr trayHandle = FindWindow("Shell_TrayWnd", null);

                            if (trayHandle != IntPtr.Zero)

                            {

                                int twidth = trRect.Right - trRect.Left;

                                int theight = trRect.Bottom - trRect.Top;

                                SetWindowPos(trayHandle, IntPtr.Zero, trRect.Left, trRect.Top, twidth, theight,

                                    SWP_NOACTIVATE | SWP_NOZORDER);

                                Log("[Startup] Restored stale Shell_TrayWnd to ["

                                    + trRect.Left + "," + trRect.Top + " - "

                                    + trRect.Right + "," + trRect.Bottom + "].");

                            }

                        }

                        catch (Exception ex) { Log("[Startup] Restore Shell_TrayWnd error: " + ex.Message); }

                    }

                    try

                    {

                        WritePrivateProfileString("Settings", "TaskbarSavedRect", "", iniPath);

                        WritePrivateProfileString(null, null, null, iniPath);

                    }

                    catch { }

                }

            }



            var asmName = System.Reflection.Assembly.GetExecutingAssembly().GetName();

            var asmVersion = asmName.Version;

            string versionStr = asmVersion != null ? "v" + asmVersion.ToString(3) : "v1.5.70";

            // Persist the full version for config.ini format-marker comparisons.
            configFormatVersion = asmVersion != null ? "v" + asmVersion.ToString() : "v1.5.70.0";

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



            // In standalone mode, we do NOT exit if RetroBat is absent _ we keep running as an idle sentinel.

            // Only the standard mode (Off) waits for RetroBat/EmulationStation at startup (30 s timeout).

            if (standaloneMonitor == StandaloneMode.Off && !WaitForEmulationStationToStart(30000))

            {

                Log("EmulationStation not found and StandaloneMonitor=Off. Exiting in 5 seconds...");

                if (showConsole) Thread.Sleep(5000);

                return;

            }



            if (standaloneMonitor != StandaloneMode.Off)

            {

                Log("StandaloneMonitor=" + StandaloneModeToString(standaloneMonitor) + " _ process stays alive in idle mode.");

                Log("[Standalone] Waiting for any monitored app (RetroBat or ThirdPartyApps). Full mode ignores ThirdPartyApps.");

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



                StartUiThread();



                StartHttpServer();



                // Initial Game Bar widget sync at startup:

                // If GameBarWidgetEnabled=true in INI but the widget is not installed, install it.

                // If GameBarWidgetEnabled=false in INI but the widget IS installed, uninstall it.

                // (No "write INI to false because not installed" -- the INI is the

                // user's intent, the system state is a consequence.)

                SyncWidgetStateFromIni(true);



                RunMonitoringLoop(iniPath);

            }

            catch (Exception ex)

            {

                Log($"Error during optimization execution: {ex.Message}");

            }

            finally

            {

                CleanupNotifyIcon();

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



        // EmergencyRestore: back-channel used when the normal widget HTTP path is

        // dead but the backend main loop is still alive (so it can poll the INI).

        // The widget (or any external tool / notepad) can set EmergencyRestore=1

        // in [Settings] to force an immediate full undo of currently-applied

        // optimizations. The flag is reset to 0 by the backend once processed.

        static void CheckEmergencyRestore(string iniPath)

        {

            try

            {

                if (!isOptimized) return;

                string val = ReadIniString("Settings", "EmergencyRestore", "", iniPath).Trim();

                if (string.IsNullOrEmpty(val)) return;

                if (val.Equals("1", StringComparison.OrdinalIgnoreCase) ||

                    val.Equals("true", StringComparison.OrdinalIgnoreCase) ||

                    val.Equals("yes", StringComparison.OrdinalIgnoreCase))

                {

                    Log("[Emergency] EmergencyRestore=1 detected in config.ini _ forcing full undo.");

                    try

                    {

                        UndoOptimizations(suspendBackgroundApps, hideNonSuspendedWindows, killExplorer);

                        isOptimized = false;

                        // Also flip Enable off so the polling loop doesn't re-apply

                        // immediately afterwards. The user explicitly wants out.

                        enabled = false;

                        WritePrivateProfileString("Settings", "Enable", "false", iniPath);

                    }

                    catch (Exception ex) { Log("[Emergency] Undo error: " + ex.Message); }

                    finally

                    {

                        // Always clear the flag _ even on failure _ so the next

                        // poll cycle doesn't re-trigger an undo storm.

                        try { WritePrivateProfileString("Settings", "EmergencyRestore", "0", iniPath); }

                        catch { }

                    }

                    if (showOverlay)

                    {

                        try { ShowNotification(GetLangMessage("restoring"), true); } catch { }

                    }

                    Log("[Emergency] Emergency restore complete. EmergencyRestore flag cleared.");

                }

            }

            catch (Exception ex) { Log("[Emergency] Check error: " + ex.Message); }

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



                if (taskbarAutoHide)

                {

                    if (!taskbarAutoHideMode) ApplyTaskbarAutoHideSoft();

                    else ApplyTaskbarAutoHideFull();

                }

            }

            catch (Exception ex)

            {

                Log("Error hiding explorer.exe elements: " + ex.Message);

            }

        }



        static void StartExplorer()

        {

            Log("Starting/Restoring explorer.exe...");



            if (autoHideCurrentlyApplied)

            {

                int saved = lastTaskbarState >= 0

                    ? lastTaskbarState

                    : ReadIniInt("Settings", "TaskbarAutoHideSavedState", -1, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini"));

                RestoreTaskbarState(saved);

            }



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



        static void BroadcastWorkAreaChange(string reason)

        {

            // The work area was just modified. Tell every interested component so that:

            //  * top-level windows can re-cache their SM_CYMAXIMIZED-style metrics and reflow;

            //  * DWM itself recomputes the live per-monitor work area (BSF_ALLCOMPONENTS

            //    reaches DWM and the immersive shell, neither of which always listens

            //    to a plain user-mode broadcast);

            //  * the shell (explorer.exe) refreshes any cached taskbar visuals.

            //

            // lParam for WM_SETTINGCHANGE MUST point at a Unicode string naming the

            // changed section ("WorkArea"). Without it a lot of hosted apps simply

            // ignore the message and keep their boot-time maximized size.

            int sentCount = 0;

            IntPtr workAreaStr = IntPtr.Zero;

            try

            {

                workAreaStr = Marshal.StringToHGlobalAnsi("WorkArea");



                // Path A: non-blocking post. Fast and the app message queues

                // will process these when they get to it.

                if (PostMessage(HWND_BROADCAST, WM_SETTINGCHANGE, (IntPtr)SPI_SETWORKAREA, IntPtr.Zero))

                    sentCount++;



                // Path B: synchronise with 250ms abort-if-hung timeout so a

                // single unresponsive app never freezes the backend.

                IntPtr res;

                bool ok = SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE,

                    (IntPtr)SPI_SETWORKAREA, workAreaStr,

                    SMTO_ABORTIFHUNG, 250, out res);

                if (ok) sentCount++;



                // Path C: BSF_ALLCOMPONENTS + BSF_SENDNOTIFYMESSAGE reaches

                // shell components and DWM directly. Signature returns `long`

                // now (correctly sized for x64, fixed after marshal crash).

                uint recipients = 0;

                long bsmRet = BroadcastSystemMessage(

                    BSF_ALLCOMPONENTS | BSF_SENDNOTIFYMESSAGE,

                    ref recipients, WM_SETTINGCHANGE, (IntPtr)SPI_SETWORKAREA, IntPtr.Zero);

                if (bsmRet > 0) sentCount++;



                Log("[WorkArea] Broadcast work-area change (" + reason + ") via " + sentCount + "/3 channel(s).");

            }

            catch (Exception ex)

            {

                Log("[WorkArea] Broadcast work-area change error: " + ex.Message);

            }

            finally

            {

                if (workAreaStr != IntPtr.Zero) Marshal.FreeHGlobal(workAreaStr);

            }

        }



        static int QueryTaskbarState()

        {

            try

            {

                APPBARDATA abd = new APPBARDATA { cbSize = Marshal.SizeOf(typeof(APPBARDATA)) };

                abd.hWnd = FindWindow("Shell_TrayWnd", null);

                return (int)SHAppBarMessage(ABM_GETSTATE, ref abd);

            }

            catch (Exception ex) { Log("[Taskbar] ABM_GETSTATE error: " + ex.Message); return -1; }

        }



        // ?? Soft TaskbarHide (no-flicker) ????????????????????????????????????

        // Masque la taskbar via SW_HIDE + d_placement off-screen. Plus simple

        // et sans aucun restart de explorer.exe (donc aucun flicker, Game

        // Bar intacte, aucun service d'arri_re-plan perturb_).

        //

        // MAIS : sur Win11, les applications MAXIMIS_ES non-borderless ne

        // prendront PAS 100% de l'_cran _ leur zone fant_me en bas persiste

        // car DWM continue _ r_server la bande. Ce mode est adapt_ pour:

        //   _ Apps qui g_rent elles-m_mes le fullscreen (jeux, _mulateurs)

        //   _ Masquer juste visuellement la barre sans casser Game Bar

        static void ApplyTaskbarAutoHideSoft()

        {

            if (autoHideCurrentlyApplied) return;



            // Capture l'_tat initial + persist (pour crash recovery).

            int cur = QueryTaskbarState();

            if (cur < 0) cur = 0;

            lastTaskbarState = cur;

            try

            {

                string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

                WritePrivateProfileString("Settings", "TaskbarAutoHideSavedState", cur.ToString(), iniPath);

                WritePrivateProfileString(null, null, null, iniPath);

            }

            catch (Exception ex) { Log("[Taskbar] Persist saved state error: " + ex.Message); }



            try

            {

                IntPtr tray = FindWindow("Shell_TrayWnd", null);

                if (tray == IntPtr.Zero) { Log("[Taskbar-Soft] Shell_TrayWnd not found."); return; }



                RECT cur2;

                if (!GetWindowRect(tray, out cur2)) { Log("[Taskbar-Soft] GetWindowRect failed."); return; }

                lastShellTrayRect = cur2;

                try

                {

                    string iniPath2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

                    WritePrivateProfileString("Settings", "TaskbarSavedRect",

                        cur2.Left + "," + cur2.Top + "," + cur2.Right + "," + cur2.Bottom, iniPath2);

                    WritePrivateProfileString(null, null, null, iniPath2);

                }

                catch (Exception ex) { Log("[Taskbar-Soft] Persist TaskbarSavedRect error: " + ex.Message); }



                // 1. Empty region ? no pixels rendered for Shell_TrayWnd.

                IntPtr emptyRgn = CreateRectRgn(0, 0, 0, 0);

                bool regionOk = false;

                if (emptyRgn != IntPtr.Zero)

                {

                    int rgnRes = SetWindowRgn(tray, emptyRgn, true);

                    if (rgnRes == 0) DeleteObject(emptyRgn);

                    else regionOk = true;

                }

                if (regionOk) Log("[Taskbar-Soft] Empty region applied to Shell_TrayWnd.");



                // 2. Move off-screen below the lowest monitor plus safety.

                int tY = 0;

                foreach (var scr in System.Windows.Forms.Screen.AllScreens)

                {

                    int bot = scr.Bounds.Bottom;

                    if (bot > tY) tY = bot;

                }

                tY += 500;



                int width = cur2.Right - cur2.Left;

                int height = cur2.Bottom - cur2.Top;

                bool moved = SetWindowPos(tray, IntPtr.Zero, cur2.Left, tY, 0, 0,

                    SWP_NOACTIVATE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

                if (!moved)

                {

                    int err = Marshal.GetLastWin32Error();

                    Log("[Taskbar-Soft] SetWindowPos(offscreen) failed (err=" + err + ").");

                    moved = MoveWindow(tray, cur2.Left, tY, width, height, false);

                }

                if (moved)

                {

                    shellTrayMoved = true;

                    Log("[Taskbar-Soft] Shell_TrayWnd moved offscreen to y=" + tY + ".");

                }



                // 3. ShowWindow(SW_HIDE) on top of everything, so even ABM_SETSTATE

                //    autohide popping or mouse hover doesn't show it.

                ShowWindow(tray, SW_HIDE);



                // 4. ABM_SETSTATE ABS_AUTOHIDE so apps that listen (Win32 native)

                //    see the autohide flag. This will partially help work area.

                try

                {

                    APPBARDATA abd = new APPBARDATA

                    {

                        cbSize = Marshal.SizeOf(typeof(APPBARDATA)),

                        hWnd = tray,

                        lParam = (IntPtr)ABS_AUTOHIDE

                    };

                    SHAppBarMessage(ABM_SETSTATE, ref abd);

                }

                catch (Exception ex) { Log("[Taskbar-Soft] ABM_SETSTATE error: " + ex.Message); }



                // 5. Extend the work area to full screen on every monitor + broadcast.

                try { ApplyWorkAreaExtend(); } catch { }

                try { BroadcastWorkAreaChange("soft-apply"); } catch { }



                autoHideCurrentlyApplied = true;

                Log("[Taskbar-Soft] Soft autohide applied (SW_HIDE + offscreen + work area extend).");

            }

            catch (Exception ex)

            {

                Log("[Taskbar-Soft] Apply error: " + ex.Message);

            }

        }



        static void ApplyTaskbarAutoHideFull()
        {

            // Full mode deliberately skips the autoHideCurrentlyApplied guard.
            // KillExplorer may be called repeatedly (e.g. Monitor detect +
            // Widget SETENABLE), and every call must force the registry
            // write + explorer restart so the user-selected strategy is
            // applied consistently. Soft keeps the guard to avoid useless
            // double SW_HIDE/offscreen.

            if (autoHideCurrentlyApplied)
            {
                Log("[Taskbar-Full] autoHideCurrentlyApplied=true, re-entering to force registry+restart.");
            }

            int cur = QueryTaskbarState();
            if (cur < 0) { Log("[Taskbar-Full] Cannot query state; autohide skipped."); return; }
            lastTaskbarState = cur;

            try
            {
                string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                WritePrivateProfileString("Settings", "TaskbarAutoHideSavedState", cur.ToString(), iniPath);
                WritePrivateProfileString(null, null, null, iniPath);
            }
            catch (Exception ex) { Log("[Taskbar-Full] Persist saved state error: " + ex.Message); }

            // v1.5.20 Full mode: registry-based autohide (StuckRects3) +
            // ABM_SETSTATE + explorer restart (Win11) or TraySettings broadcast
            // (Win10). NO offscreen/SW_HIDE — those belong to Soft only.
            // When the user chooses Full, they want the true Windows taskbar
            // autohide so that maximized non-borderless apps reach the full
            // bottom edge of every monitor. The cost is a 1-2s desktop flicker
            // on every apply/restore from the explorer restart.

            IntPtr tray = FindWindow("Shell_TrayWnd", null);
            if (tray == IntPtr.Zero) { Log("[Taskbar-Full] Shell_TrayWnd not found; registry path only."); }

            RECT curRect;
            if (tray != IntPtr.Zero && GetWindowRect(tray, out curRect))
            {
                lastShellTrayRect = curRect;
                try
                {
                    string iniPath2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                    WritePrivateProfileString("Settings", "TaskbarSavedRect",
                        curRect.Left + "," + curRect.Top + "," + curRect.Right + "," + curRect.Bottom, iniPath2);
                    WritePrivateProfileString(null, null, null, iniPath2);
                }
                catch { }
            }
            int oldEdge = GetTaskbarEdge();

            bool autoHideWasOff = (oldEdge < 0) || (oldEdge == 0x02);
            // Track whether WE flipped the registry bit so the undo knows it
            // must restore 0x02. If the edge is already 0x03 we leave the flag
            // untouched, but in Full mode we still force-execute the writes +
            // restart below so the strategy is actually applied, not assumed.
            if (autoHideWasOff) savedRegistryAutoHideOff = true;

            try
            {
                string iniR = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                WritePrivateProfileString("Settings", "TaskbarAutoHideWasOff",
                    autoHideWasOff ? "true" : "false", iniR);
                WritePrivateProfileString(null, null, null, iniR);
            }
            catch { }

            // Full mode ALWAYS forces the autohide state: write StuckRects3
            // byte[8]=0x03 (idempotent if already set) + ABM_SETSTATE +
            // explorer restart (Win11) / TraySettings broadcast (Win10).
            // Skipping this when the registry is already 0x03 left the taskbar
            // merely SW_HIDDEN by KillExplorer, which looked like Soft mode.
            SetTaskbarEdge(0x03); // bottom + autohide
            if (autoHideWasOff)
                Log("[Taskbar-Full] StuckRects3 edge 0x02->0x03 written.");
            else
                Log("[Taskbar-Full] StuckRects3 re-written 0x03 (was already ON).");

            bool isWin11Modern = IsWin11_22H2OrLater();

            try
            {
                APPBARDATA abd = new APPBARDATA
                {
                    cbSize = Marshal.SizeOf(typeof(APPBARDATA)),
                    hWnd = tray,
                    lParam = (IntPtr)ABS_AUTOHIDE
                };
                if (tray != IntPtr.Zero) SHAppBarMessage(ABM_SETSTATE, ref abd);
                Log("[Taskbar-Full] ABM_SETSTATE ABS_AUTOHIDE applied.");
            }
            catch (Exception ex) { Log("[Taskbar-Full] ABM_SETSTATE error: " + ex.Message); }

            if (isWin11Modern)
            {
                if (tray != IntPtr.Zero)
                {
                    try
                    {
                        int won = MultiChannelTaskbarSignal(tray, ABS_AUTOHIDE);
                        Log("[Taskbar-Full] Multi-channel broadcast pre-kill (" + won + "/5 channels).");
                    }
                    catch (Exception ex) { Log("[Taskbar-Full] pre-kill broadcast error: " + ex.Message); }
                }

                // v1.5.16: write AFTER kill + write 0x03.
                RestartExplorer(0x03);
                Log("[Taskbar-Full] Win11: ABM_SETSTATE + RestartExplorer(0x03, post-kill) + Game Bar remount. Done.");
            }
            else
            {
                try
                {
                    IntPtr pst = Marshal.StringToHGlobalAnsi("TraySettings");
                    SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, pst,
                        SMTO_ABORTIFHUNG, 300, out IntPtr res);
                    Marshal.FreeHGlobal(pst);
                    Log("[Taskbar-Full] Sent TraySettings broadcast (Win10 path).");
                }
                catch (Exception ex) { Log("[Taskbar-Full] TraySettings broadcast error: " + ex.Message); }
                Log("[Taskbar-Full] Win10: RegistrySet + ABM_SETSTATE + TraySettings. Done.");
            }

            // Mark the shell tray as "moved" so MoveTaskbarBack() (called from
            // RestoreTaskbarState on undo) will actually run its
            // savedRegistryAutoHideOff branch and restore StuckRects3=0x02 +
            // restart explorer. Without this flag the restore registry path is
            // gated behind if(!shellTrayMoved) return; and never runs in
            // Full mode (which never physically moves the tray offscreen).
            shellTrayMoved = true;

            autoHideCurrentlyApplied = true;
        }



        // EnumPropsEx callback signature (for GetLastActivePopup-style

        // enumerations). Walk every top-level window of every thread and ask

        // Windows to recompute its frame; this shoves the new work-area down

        // the throat of already-maximized apps that would otherwise carry

        // their stale height.

        static void RefreshMaximizedWindowFrames()

        {

            int refreshed = 0;

            try

            {

                EnumWindows((hWnd, _) =>

                {

                    try

                    {

                        if (!IsWindowVisible(hWnd)) return true;

                        // Only refresh top-level windows that belong to a

                        // different process (skip our own + the explorer

                        // taskbar itself).

                        uint pid;

                        GetWindowThreadProcessId(hWnd, out pid);

                        if (pid == (uint)Process.GetCurrentProcess().Id) return true;



                        // GetWindowText is cheaper than DwmGetWindowAttribute,

                        // good enough as a guard against exiting early.

                        var cls = new System.Text.StringBuilder(64);

                        GetClassName(hWnd, cls, 64);

                        string clsName = cls.ToString();

                        if (clsName == "Shell_TrayWnd" || clsName == "Shell_SecondaryTrayWnd")

                            return true;



                        // SWP_FRAMECHANGED forces a full WM_NCCALCSIZE cycle.

                        // SWP_NOMOVE|NOSIZE|NOZORDER|NOACTIVATE keep the

                        // current position/size so we ONLY trigger the frame

                        // recompute _ no spurious resizing/topping/focus.

                        const uint SWP_NOMOVE = 0x0002;

                        bool ok = SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,

                            SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

                        if (ok) refreshed++;

                    }

                    catch { }

                    return true;

                }, IntPtr.Zero);

                Log("[WorkArea] Refreshed frames of " + refreshed + " visible top-level window(s).");

            }

            catch (Exception ex) { Log("[WorkArea] RefreshMaximizedWindowFrames error: " + ex.Message); }

        }



        static void MoveTaskbarBack()

        {

            if (!shellTrayMoved) return;

            try

            {

                IntPtr tray = FindWindow("Shell_TrayWnd", null);

                if (tray == IntPtr.Zero) { Log("[Taskbar] Shell_TrayWnd not found on restore."); return; }



                RECT target = lastShellTrayRect;

                if (target.Right <= target.Left || target.Bottom <= target.Top)

                {

                    target = StringToRect(ReadIniString("Settings", "TaskbarSavedRect", "",

                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini")));

                }



                // v1.5.11: restore. Bring tray back visible + (if we flipped

                // autohide) reset StuckRects3 byte[8]=0x02 and broadcast

                // "TraySettings" so explorer.exe picks it up without a restart.

                ShowWindow(tray, SW_SHOW);

                shellTrayMoved = false;

                Log("[Taskbar] Shell_TrayWnd shown again (SW_SHOW).");



                if (savedRegistryAutoHideOff)

                {

                    SetTaskbarEdge(0x02); // bottom, no autohide

                    int won = MultiChannelTaskbarSignal(tray, 0); // 0 = reset autohide

                    Log("[Taskbar] Restore: edge=0x02 pre-kill + multi-channel (" + won + "/5).");

                    // v1.5.16: restart again so explorer re-launches with

                    // byte[8]=02 (confirmed fixes reliable off on this Win11).

                    RestartExplorer(0x02);

                    Log("[Taskbar] Restore: edge=0x02 + multi-channel + RestartExplorer. Done.");

                    savedRegistryAutoHideOff = false;

                }



                try

                {

                    string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

                    WritePrivateProfileString("Settings", "TaskbarSavedRect", "", iniPath);

                    WritePrivateProfileString("Settings", "TaskbarAutoHideWasOff", "", iniPath);

                    WritePrivateProfileString(null, null, null, iniPath);

                }

                catch { }

            }

            catch (Exception ex) { Log("[Taskbar] MoveTaskbarBack error: " + ex.Message); }

        }



        static RECT StringToRect(string s)

        {

            var r = new RECT { Left = 0, Top = 0, Right = 0, Bottom = 0 };

            if (string.IsNullOrEmpty(s)) return r;

            string[] parts = s.Split(',');

            if (parts.Length != 4) return r;

            int.TryParse(parts[0], out r.Left);

            int.TryParse(parts[1], out r.Top);

            int.TryParse(parts[2], out r.Right);

            int.TryParse(parts[3], out r.Bottom);

            return r;

        }



        static string RectToString(RECT r)

        {

            return r.Left + "," + r.Top + "," + r.Right + "," + r.Bottom;

        }



        static void ApplyWorkAreaExtend()

        {

            if (workAreaCurrentlyExtended) return;

            try

            {

                // Save current work area of every monitor (so the watchdog can restore).

                // SystemParametersInfo(SPI_GETWORKAREA) only returns the *primary*

                // monitor's work area; to cover multi-monitor setups we loop over

                // Screen.AllScreens (which calls GetMonitorInfo under the hood) and

                // we persist the primary work area in INI for crash recovery.

                RECT prevPrimary = new RECT();

                if (SystemParametersInfo(SPI_GETWORKAREA, 0, ref prevPrimary, 0))

                {

                    lastWorkArea = prevPrimary;

                    try

                    {

                        string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

                        WritePrivateProfileString("Settings", "WorkAreaSavedRect", RectToString(prevPrimary), iniPath);

                        WritePrivateProfileString(null, null, null, iniPath);

                    }

                    catch (Exception ex) { Log("[WorkArea] Persist saved rect error: " + ex.Message); }

                }



                int applied = 0;

                foreach (var scr in Screen.AllScreens)

                {

                    try

                    {

                        RECT full = new RECT

                        {

                            Left = scr.Bounds.Left,

                            Top = scr.Bounds.Top,

                            Right = scr.Bounds.Right,

                            Bottom = scr.Bounds.Bottom

                        };

                        if (SystemParametersInfo(SPI_SETWORKAREA, 0, ref full, SPIF_FULL))

                        {

                            applied++;

                        }

                        else

                        {

                            int err = Marshal.GetLastWin32Error();

                            Log("[WorkArea] SPI_SETWORKAREA failed for monitor "

                                + scr.DeviceName + " (err=" + err + ").");

                        }

                    }

                    catch (Exception ex) { Log("[WorkArea] Error extending work area for monitor: " + ex.Message); }

                }



                workAreaCurrentlyExtended = true;

                Log("[WorkArea] Extended work area to full screen on " + applied + " monitor(s).");

            }

            catch (Exception ex) { Log("[WorkArea] ApplyWorkAreaExtend error: " + ex.Message); }

        }



        static void RestoreWorkArea()

        {

            if (!workAreaCurrentlyExtended) return;

            try

            {

                RECT target = lastWorkArea;

                bool fromIni = false;

                if (target.Right <= target.Left || target.Bottom <= target.Top)

                {

                    // No valid in-memory saved rect: fall back to INI (crash recovery).

                    target = StringToRect(ReadIniString("Settings", "WorkAreaSavedRect", "",

                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini")));

                    fromIni = true;

                }



                if (target.Right > target.Left && target.Bottom > target.Top)

                {

                    if (SystemParametersInfo(SPI_SETWORKAREA, 0, ref target, SPIF_FULL))

                    {

                        Log("[WorkArea] Restored primary work area to "

                            + RectToString(target) + (fromIni ? " (from INI)." : "."));

                    }

                    else

                    {

                        int err = Marshal.GetLastWin32Error();

                        Log("[WorkArea] SPI_SETWORKAREA restore failed (err=" + err + ").");

                    }

                }



                workAreaCurrentlyExtended = false;



                try

                {

                    string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

                    WritePrivateProfileString("Settings", "WorkAreaSavedRect", "", iniPath);

                    WritePrivateProfileString(null, null, null, iniPath);

                }

                catch { }

            }

            catch (Exception ex) { Log("[WorkArea] RestoreWorkArea error: " + ex.Message); }

        }



        static void RestoreTaskbarState(int savedState)

        {

            // ORDER IS CRITICAL: first restore the work area to the original

            // (smaller) rect so the system knows the taskbar will reappear.

            if (workAreaCurrentlyExtended) RestoreWorkArea();



            // Then physically restore Shell_TrayWnd to its original position;

            // DWM will then honour the restored work area + new taskbar spot.

            MoveTaskbarBack();



            // Only NOW broadcast the work-area change so that apps resize

            // themselves. Broadcasting BEFORE the tray is back on screen would

            // tell apps the work area shrank while the tray is still offscreen,

            // which can trigger deadlocks in hung apps' WM_SETTINGCHANGE handlers.

            // We run the broadcast inside a blind try/catch on its own; failure

            // here must never kill the restore pathway.

            try { BroadcastWorkAreaChange("restore"); }

            catch { Log("[Taskbar] BroadcastWorkAreaChange(restore) failed; taskbar was restored anyway."); }



            // Same treatment as apply: shove the restored (smaller) work area

            // into the throat of currently-maximized apps so they shrink to

            // leave room for the returning taskbar.

            RefreshMaximizedWindowFrames();



            if (savedState < 0) { Log("[Taskbar] No saved state, skip restore."); return; }

            try

            {

                APPBARDATA abd = new APPBARDATA

                {

                    cbSize = Marshal.SizeOf(typeof(APPBARDATA)),

                    hWnd = FindWindow("Shell_TrayWnd", null),

                    lParam = (IntPtr)savedState

                };

                SHAppBarMessage(ABM_SETSTATE, ref abd);

                autoHideCurrentlyApplied = false;

                Log("[Taskbar] Restored taskbar state to " + savedState + ".");

            }

            catch (Exception ex) { Log("[Taskbar] ABM_SETSTATE restore error: " + ex.Message); }

            try

            {

                string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

                WritePrivateProfileString("Settings", "TaskbarAutoHideSavedState", "", iniPath);

                WritePrivateProfileString(null, null, null, iniPath);

            }

            catch { }

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

            // Only Monitor mode uses ThirdPartyApps as triggers. Full mode ignores them; Off mode has no need.

            if (standaloneMonitor != StandaloneMode.Monitor) return list;

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

            // Full mode: process is always considered "monitored" _ optimizations stay active regardless.

            if (standaloneMonitor == StandaloneMode.Full) return true;

            if (standaloneMonitor == StandaloneMode.Off) return false;

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



        static string GetTriggeringAppName()

        {

            try

            {

                if (Process.GetProcessesByName("emulationstation").Length > 0) return "emulationstation";

                if (Process.GetProcessesByName("retrobat").Length > 0) return "retrobat";

                string apps = Volatile.Read(ref thirdPartyApps);

                if (!string.IsNullOrEmpty(apps))

                {

                    string[] parts = apps.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var part in parts)

                    {

                        string name = part.Trim().Replace(".exe", "");

                        if (!string.IsNullOrEmpty(name) && Process.GetProcessesByName(name).Length > 0)

                            return name;

                    }

                }

                return "NONE";

            }

            catch { return "NONE"; }

        }



        static string RemoveThirdPartyApp(string name)

        {

            try

            {

                if (string.IsNullOrEmpty(name))

                    return "ERROR:empty_name";

                string lower = name.Trim().ToLowerInvariant();

                if (lower == "retrobatgamemode")

                    return "ERROR:protected_app";



                string current = Volatile.Read(ref thirdPartyApps);

                var apps = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

                bool found = false;

                if (!string.IsNullOrEmpty(current))

                {

                    string[] parts = current.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var p in parts)

                    {

                        string trimmed = p.Trim();

                        if (trimmed.Replace(".exe", "").Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))

                            found = true;

                        else

                            apps.Add(trimmed);

                    }

                }

                if (!found)

                    return "OK:NOTFOUND";



                string newValue = string.Join(",", apps);

                Volatile.Write(ref thirdPartyApps, newValue);



                string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

                WritePrivateProfileString("Settings", "ThirdPartyApps", newValue, iniPath);

                WritePrivateProfileString(null, null, null, iniPath);



                Log("[Widget] Removed '" + name + "' from ThirdPartyApps. New value: " + newValue);



                try

                {

                    DateTime now = File.GetLastWriteTime(iniPath);

                    if (now > DateTime.Now) now = DateTime.Now;

                    ReloadAndApplyConfig(iniPath, ref now);

                }

                catch { }



                return "OK:REMOVED";

            }

            catch (Exception ex)

            {

                return "ERROR:" + ex.Message;

            }

        }



        static string SetStandaloneMode(StandaloneMode mode)

        {

            try

            {

                string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

                StandaloneMode prev = standaloneMonitor;

                standaloneMonitor = mode;

                WritePrivateProfileString("Settings", "StandaloneMonitor", StandaloneModeToString(mode), iniPath);

                WritePrivateProfileString(null, null, null, iniPath);



                try

                {

                    DateTime now = File.GetLastWriteTime(iniPath);

                    if (now > DateTime.Now) now = DateTime.Now;

                    ReloadAndApplyConfig(iniPath, ref now);

                }

                catch (Exception ex)

                {

                    Log("[Widget] Reload after SETSTANDALONE failed: " + ex.Message);

                }



                Log("[Widget] SETSTANDALONE " + StandaloneModeToString(prev) + " -> " + StandaloneModeToString(mode));

                ShowModeChangeOverlay(mode);

                return "SETSTANDALONE:" + StandaloneModeToString(mode);

            }

            catch (Exception ex)

            {

                Log("[Widget] SETSTANDALONE error: " + ex.Message);

                return "ERROR:" + ex.Message;

            }

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

                    if (standaloneMonitor == StandaloneMode.Off)

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

                    // Back-channel: check EmergencyRestore=1 FIRST, before the

                    // standard hot-reload, so a widget unable to reach HTTP can

                    // still force a full undo by writing this single key.

                    CheckEmergencyRestore(iniPath);



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

            StandaloneMode prevStandaloneMonitor = standaloneMonitor;

            bool prevGameBarWidgetEnabled = gameBarWidgetEnabled;

            bool prevTaskbarAutoHide = taskbarAutoHide;

            string prevWhitelist = whitelist;

            string prevHideWhitelist = hideWhitelist;

            string prevThirdPartyApps = thirdPartyApps;

            int prevTargetTimer = targetTimer;



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

            standaloneMonitor = ParseStandaloneMode(ReadIniString("Settings", "StandaloneMonitor", StandaloneModeToString(standaloneMonitor), iniPath));

            gameBarWidgetEnabled = ReadIniBool("Settings", "GameBarWidgetEnabled", gameBarWidgetEnabled, iniPath);

            thirdPartyApps = ReadIniString("Settings", "ThirdPartyApps", thirdPartyApps, iniPath);

            targetTimer = ReadIniInt("Settings", "TargetTimer", targetTimer, iniPath);

            taskbarAutoHide = ReadIniBool("Settings", "TaskbarAutoHide", taskbarAutoHide, iniPath);

            // v1.5.20: hot-reload TaskbarAutoHideMode so Soft/Full can switch
            // live without a backend restart. Without this the static stayed
            // frozen at its startup value, so changing the INI from Soft to Full
            // (or reverse) was silently ignored until the backend was restarted.
            bool prevTaskbarAutoHideMode = taskbarAutoHideMode;
            taskbarAutoHideMode = ReadIniBool("Settings", "TaskbarAutoHideMode", taskbarAutoHideMode, iniPath);

            // Game Bar widget: live INI toggle (same logic as startup).

            // The INI value is the user's *intent*. The actual system state is

            // updated to match the INI:

            //   - INI=true  + system absent  -> install

            //   - INI=false + system present -> uninstall

            //   - any other case               -> nothing (aligned)

            // Calling InstallGameBarWidget() / UninstallGameBarWidget() also

            // re-writes the INI flag via SetWidgetInstalledFlag() so the

            // INI and the system stay coherent after each operation.

            bool newGameBarWidgetEnabled = ReadIniBool("Settings", "GameBarWidgetEnabled", gameBarWidgetEnabled, iniPath);

            gameBarWidgetEnabled = newGameBarWidgetEnabled;

            try { SyncWidgetStateFromIni(); }

            catch (Exception ex) { Log("[Widget] Sync from INI failed: " + ex.Message); }



            bool anyOptionChanged =

                killExplorer != prevKillExplorer ||

                emptyWorkingSet != prevEmptyWorkingSet ||

                suspendBackgroundApps != prevSuspendBackgroundApps ||

                hideNonSuspendedWindows != prevHideNonSuspendedWindows ||

                showOverlay != prevShowOverlay ||

                whitelist != prevWhitelist ||

                hideWhitelist != prevHideWhitelist ||

                thirdPartyApps != prevThirdPartyApps ||

                standaloneMonitor != prevStandaloneMonitor ||

                gameBarWidgetEnabled != prevGameBarWidgetEnabled ||

                targetTimer != prevTargetTimer ||

                taskbarAutoHide != prevTaskbarAutoHide;



            // Synchronize widget installation state when toggled from the INI.

            if (gameBarWidgetEnabled != prevGameBarWidgetEnabled)

            {

                try

                {

                    if (gameBarWidgetEnabled) InstallGameBarWidget(true);

                    else UninstallGameBarWidget(true);

                }

                catch (Exception wex)

                {

                    Log("[Live] Error applying GameBarWidgetEnabled change: " + wex.Message);

                }

            }



            if (wasEnabled && !enabled)

            {

                Log("[Live] Enable set to false. Undoing all optimizations...");

                UndoOptimizations(prevSuspendBackgroundApps, prevHideNonSuspendedWindows, prevKillExplorer);

                isOptimized = false;

                if (showOverlay) ShowNotification(GetLangMessage("restoring"), true);



                // If we just disabled AND no RetroBat/EmulationStation runs AND non-standalone ? exit

                if (standaloneMonitor == StandaloneMode.Off && !IsRetroBatRunning())

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



            // Edge case: TaskbarAutoHide toggled while killExplorer=false (no KillExplorer

            // cycle runs through StartExplorer/KillExplorer). Handle the autohide apply/restore

            // locally so the taskbar never stays stuck in autohide when the option is turned off.

            if (taskbarAutoHide != prevTaskbarAutoHide && !killExplorer)

            {

                if (taskbarAutoHide && isOptimized)

                {

                    // Off?On while optimized but KillExplorer=false: apply autohide now.
                    // v1.5.20: route through Soft/Full dispatch so the mode
                    // chosen in config.ini is actually honoured.

                    Log("[Live] TaskbarAutoHide turned on (KillExplorer=false). Applying autohide (" + (taskbarAutoHideMode ? "Full" : "Soft") + ").");

                    if (!taskbarAutoHideMode) ApplyTaskbarAutoHideSoft();

                    else ApplyTaskbarAutoHideFull();

                }

                else if (!taskbarAutoHide && prevTaskbarAutoHide)

                {

                    // On?Off while already applied: restore taskbar state now.

                    Log("[Live] TaskbarAutoHide turned off (KillExplorer=false). Restoring taskbar locally.");

                    int saved = lastTaskbarState >= 0

                        ? lastTaskbarState

                        : ReadIniInt("Settings", "TaskbarAutoHideSavedState", -1, iniPath);

                    RestoreTaskbarState(saved);

                }

            }

            // v1.5.20: hot-reload of TaskbarAutoHideMode (Soft <-> Full).
            // If autohide is currently applied and the user flipped the mode
            // in config.ini, we must undo the OLD strategy then re-apply with
            // the NEW one. Restart-free Soft and registry+restart Full are
            // mutually exclusive: never let one bleed into the other.
            if (taskbarAutoHideMode != prevTaskbarAutoHideMode && isOptimized && taskbarAutoHide)
            {
                Log("[Live] TaskbarAutoHideMode changed " + (prevTaskbarAutoHideMode ? "Full" : "Soft") +
                    " -> " + (taskbarAutoHideMode ? "Full" : "Soft") + " while applied. Re-applying.");

                int saved = lastTaskbarState >= 0
                    ? lastTaskbarState
                    : ReadIniInt("Settings", "TaskbarAutoHideSavedState", -1, iniPath);

                // Undo current strategy (restore registry/workarea/tray).
                RestoreTaskbarState(saved);

                // Re-apply with the new strategy. Use the same dispatch as
                // KillExplorer so Soft/Full never mix.
                if (!taskbarAutoHideMode) ApplyTaskbarAutoHideSoft();
                else ApplyTaskbarAutoHideFull();

            }



            // StandaloneMonitor transition to Off: if RetroBat is NOT running, exit the process

            // because non-standalone mode only makes sense while EmulationStation/RetroBat is active.

            // Monitor?Full transitions don't trigger exit (process stays alive in both modes).

            if (prevStandaloneMonitor != StandaloneMode.Off && standaloneMonitor == StandaloneMode.Off && !IsRetroBatRunning())

            {

                if (isOptimized)

                {

                    Log("[Live] StandaloneMonitor set to Off, no RetroBat running. Undoing optimizations before exit...");

                    UndoOptimizations(prevSuspendBackgroundApps, prevHideNonSuspendedWindows, prevKillExplorer);

                    isOptimized = false;

                    if (showOverlay) ShowNotification(GetLangMessage("restoring"), true);

                }

                Log("[Live] StandaloneMonitor set to Off and no RetroBat/EmulationStation running. Requesting exit.");

                requestExit = true;

            }



            // GameBarWidgetEnabled transition false?true: auto-install widget

            if (!prevGameBarWidgetEnabled && gameBarWidgetEnabled)

            {

                Log("[Live] GameBarWidgetEnabled turned on. Auto-installing widget...");

                if (!CheckDeveloperMode())

                {

                    gameBarWidgetEnabled = false;

                    Log("[Live] Developer mode not enabled. GameBarWidgetEnabled kept false.");

                }

                else

                {

                    try

                    {

                        InstallGameBarWidget(true);

                    }

                    catch (Exception ex)

                    {

                        Log("[Live] Auto-install widget failed: " + ex.Message);

                        gameBarWidgetEnabled = false;

                    }

                }

            }

            else if (prevGameBarWidgetEnabled && !gameBarWidgetEnabled)

            {

                Log("[Live] GameBarWidgetEnabled turned off. Uninstalling widget...");

                try

                {

                    UninstallGameBarWidget(true);

                }

                catch (Exception ex)

                {

                    Log("[Live] Uninstall widget failed: " + ex.Message);

                }

            }



            // Update lastRead to the real file time AFTER we finished reading (avoid re-trigger loop)

            try

            {

                lastRead = File.GetLastWriteTime(iniPath);

                if (lastRead > DateTime.Now) lastRead = DateTime.Now;

            }

            catch { }

        }



        static bool CheckDeveloperMode()

        {

            try

            {

                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"))

                {

                    if (key != null)

                    {

                        object? val = key.GetValue("AllowDevelopmentWithoutDevLicense");

                        if (val is int intVal && intVal == 1)

                            return true;

                    }

                }

            }

            catch (Exception ex)

            {

                Log("[GameBar] Error checking developer mode: " + ex.Message);

            }

            return false;

        }



        static void ShowDeveloperModeDialog()

        {

            string title = GetLangMessage("widget_warning_title");

            string text = GetLangMessage("widget_warning_text");

            string yesLabel = GetLangMessage("widget_confirm_yes");

            string noLabel = GetLangMessage("widget_confirm_no");



            var result = System.Windows.Forms.MessageBox.Show(

                text, title,

                System.Windows.Forms.MessageBoxButtons.YesNo,

                System.Windows.Forms.MessageBoxIcon.Warning,

                System.Windows.Forms.MessageBoxDefaultButton.Button1);



            if (result == System.Windows.Forms.DialogResult.Yes)

            {

                try

                {

                    Process.Start(new ProcessStartInfo

                    {

                        FileName = "ms-settings:developers",

                        UseShellExecute = true

                    });

                    Log("[GameBar] Opened Windows Developer Settings.");

                }

                catch (Exception ex)

                {

                    Log("[GameBar] Failed to open Developer Settings: " + ex.Message);

                }

            }

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

            else if (key == "target_start")

            {

                if (lang == "fr") return "Mode cible activé. Ciblage de l'application au premier plan dans " + targetTimer + " s...";

                return "Target mode activated. Capturing foreground app in " + targetTimer + " s...";

            }

            else if (key == "target_running")

            {

                if (lang == "fr") return "Ciblage... {0} s";

                return "Targeting... {0} s";

            }

            else if (key == "target_added")

            {

                if (lang == "fr") return "Application '{0}' ajoutée à la liste surveillée.";

                return "Application '{0}' added to the monitored list.";

            }

            else if (key == "target_cancelled")

            {

                if (lang == "fr") return "Mode cible annulé.";

                return "Target mode cancelled.";

            }

            else if (key == "menu_target")

            {

                if (lang == "fr") return "Cible";

                return "Target";

            }

            else if (key == "menu_target_tooltip")

            {

                if (lang == "fr") return "Capturer l'application actuellement au premier plan" + Environment.NewLine +

                    "et l'ajouter à la liste surveillée (ThirdPartyApps)." + Environment.NewLine +

                    "Active automatiquement StandaloneMonitor si nécessaire.";

                return "Capture the currently focused foreground application" + Environment.NewLine +

                    "and add it to the monitored list (ThirdPartyApps)." + Environment.NewLine +

                    "Automatically enables StandaloneMonitor if needed.";

            }

            else if (key == "menu_standalone_format")

            {

                if (lang == "fr") return "Mode Standalone";

                return "Standalone mode";

            }

            else if (key == "menu_standalone_off")

            {

                if (lang == "fr") return "Désactivé";

                return "Off";

            }

            else if (key == "menu_standalone_monitor")

            {

                if (lang == "fr") return "Surveillance";

                return "Monitor";

            }

            else if (key == "menu_standalone_full")

            {

                if (lang == "fr") return "Permanent";

                return "Full";

            }

            else if (key == "menu_standalone_mode_tooltip")

            {

                if (lang == "fr") return "Cycle: Désactivé ? Surveillance ? Permanent." + Environment.NewLine +

                    "Désactivé : quitte quand aucun processus surveillé ne tourne." + Environment.NewLine +

                    "Surveillance : reste actif si une appli ThirdPartyApps tourne." + Environment.NewLine +

                    "Permanent : reste actif en permanence (ignore ThirdPartyApps).";

                return "Cycle: Off ? Monitor ? Full." + Environment.NewLine +

                    "Off: exits when no monitored process is running." + Environment.NewLine +

                    "Monitor: stays alive while any ThirdPartyApps process is running." + Environment.NewLine +

                    "Full: stays alive permanently (ignores ThirdPartyApps).";

            }

            else if (key == "menu_enable_tooltip")

            {

                if (lang == "fr") return "Active ou désactive le Game Mode." + Environment.NewLine +

                    "Quand désactivé, toutes les optimisations sont annulées" + Environment.NewLine +

                    "et le système est restauré. Le processus reste en veille.";

                return "Toggle Game Mode on or off." + Environment.NewLine +

                    "When off, all optimizations are undone and" + Environment.NewLine +

                    "the system is restored. The process keeps running in standby.";

            }

            else if (key == "menu_exit_tooltip")

            {

                if (lang == "fr") return "Ferme proprement RetroBatGameMode." + Environment.NewLine +

                    "Toutes les optimisations sont annulées et le système" + Environment.NewLine +

                    "est restauré avant la fermeture.";

                return "Cleanly exit RetroBatGameMode." + Environment.NewLine +

                    "All optimizations are undone and the system" + Environment.NewLine +

                    "is restored before closing.";

            }

            else if (key == "menu_config")

            {

                if (lang == "fr") return "Ouvrir config.ini";

                return "Open config.ini";

            }

            else if (key == "menu_config_tooltip")

            {

                if (lang == "fr") return "Ouvre le fichier config.ini dans le Bloc-notes" + Environment.NewLine +

                    "pour modifier les paramètres." + Environment.NewLine +

                    "Les changements sont appliqués en direct (mode live).";

                return "Open config.ini in Notepad to edit settings." + Environment.NewLine +

                    "Changes are applied live without restart.";

            }

            else if (key == "menu_enable")

            {

                if (lang == "fr") return "Activer";

                return "Enable";

            }

            else if (key == "menu_disable")

            {

                if (lang == "fr") return "Désactiver";

                return "Disable";

            }

            else if (key == "menu_exit")

            {

                if (lang == "fr") return "Quitter";

                return "Exit";

            }

            else if (key == "menu_install_widget")

            {

                if (lang == "fr") return "Installer le widget Game Bar";

                return "Install Game Bar Widget";

            }

            else if (key == "menu_uninstall_widget")

            {

                if (lang == "fr") return "Désinstaller le widget Game Bar";

                return "Uninstall Game Bar Widget";

            }

            else if (key == "menu_install_widget_tooltip")

            {

                if (lang == "fr") return "Installe le widget RetroBat Game Mode dans la Game Bar" + Environment.NewLine +

                    "(Win+G) pour piloter l'optimiseur à la manette." + Environment.NewLine +

                    "Nécessite le mode développeur Windows.";

                return "Install the RetroBat Game Mode widget into the Game Bar" + Environment.NewLine +

                    "(Win+G) to control the optimizer with a controller." + Environment.NewLine +

                    "Requires Windows Developer Mode.";

            }

            else if (key == "menu_uninstall_widget_tooltip")

            {

                if (lang == "fr") return "Supprime le widget RetroBat Game Mode de la Game Bar." + Environment.NewLine +

                    "Le serveur HTTP local reste actif.";

                return "Remove the RetroBat Game Mode widget from the Game Bar." + Environment.NewLine +

                    "The local HTTP server remains active.";

            }

            else if (key == "widget_warning_title")

            {

                if (lang == "fr") return "Mode développeur requis";

                return "Developer mode required";

            }

            else if (key == "widget_warning_text")

            {

                if (lang == "fr") return "Le widget Game Bar nécessite l'activation du mode développeur Windows." + Environment.NewLine +

                    "Activez-le dans Paramètres > Pour les développeurs, puis relancez l'installation." + Environment.NewLine +

                    Environment.NewLine +

                    "Voulez-vous ouvrir les Paramètres Windows maintenant ?";

                return "The Game Bar widget requires Windows Developer Mode to be enabled." + Environment.NewLine +

                    "Enable it in Settings > For developers, then run the install again." + Environment.NewLine +

                    Environment.NewLine +

                    "Open Windows Settings now?";

            }

            else if (key == "widget_confirm_yes")

            {

                if (lang == "fr") return "Oui";

                return "Yes";

            }

            else if (key == "widget_confirm_no")

            {

                if (lang == "fr") return "Non";

                return "No";

            }

            else if (key == "widget_installed_title")

            {

                if (lang == "fr") return "Widget Game Bar installé";

                return "Game Bar widget installed";

            }

            else if (key == "widget_installed_text")

            {

                if (lang == "fr") return "Ouvrez Win+G et ajoutez \"RetroBat Game Mode\" à la Game Bar pour piloter l'optimiseur à la manette.\r\n\r\nVous pouvez maintenant désactiver le Mode développeur dans Paramètres > Système > Pour les développeurs (il n'est nécessaire que pour installer / mettre à jour le widget).";

                return "Open Win+G and pin the \"RetroBat Game Mode\" widget to control the optimizer with a controller.\r\n\r\nYou can now TURN OFF Developer Mode in Settings > System > For developers (it is only needed to install / update the widget).";

            }

            else if (key == "widget_install_failed_title")

            {

                if (lang == "fr") return "échec de l'installation du widget";

                return "Widget installation failed";

            }

            else if (key == "widget_install_failed_text")

            {

                if (lang == "fr") return "Consultez retrobat_gamemode.log pour les détails. Vérifiez que le .msix signé est bien présent dans le sous-dossier RetroBatGameModeWidget\\.";

                return "Check retrobat_gamemode.log for details. Make sure the signed .msix is present in the RetroBatGameModeWidget\\ subfolder.";

            }

            else if (key == "widget_uninstalled_title")

            {

                if (lang == "fr") return "Widget Game Bar désinstallé";

                return "Game Bar widget uninstalled";

            }

            else if (key == "widget_uninstalled_text")

            {

                if (lang == "fr") return "Le widget a été retiré de la Game Bar.";

                return "The widget was removed from the Game Bar.";

            }

            else if (key == "widget_uninstall_failed_title")

            {

                if (lang == "fr") return "Désinstallation partielle";

                return "Uninstall inconclusive";

            }

            else if (key == "widget_uninstall_failed_text")

            {

                if (lang == "fr") return "Le widget n'est peut-être plus installé. Vérifiez dans Win+G ? Galerie.";

                return "The widget may already be uninstalled. Check Win+G ? Widget gallery.";

            }

            else if (key == "widget_missing_files_text")

            {

                if (lang == "fr") return "Fichiers manquants : le sous-dossier RetroBatGameModeWidget\\ doit contenir Install_Widget_Package.bat + Install_Widget_Package.ps1 + Uninstall_Widget_Package.bat + RetroBatGameModeWidget.Package_*.msix signé.";

                return "Missing files: the RetroBatGameModeWidget\\ subfolder must contain Install_Widget_Package.bat + Install_Widget_Package.ps1 + Uninstall_Widget_Package.bat + signed RetroBatGameModeWidget.Package_*.msix.";

            }

            else if (key == "widget_install_success")

            {

                if (lang == "fr") return "Widget installé avec succès !";

                return "Widget installed successfully!";

            }

            else if (key == "widget_install_failure")

            {

                if (lang == "fr") return "échec de l'installation du widget.";

                return "Widget installation failed.";

            }

            else if (key == "widget_uninstall_success")

            {

                if (lang == "fr") return "Widget désinstallé avec succès !";

                return "Widget uninstalled successfully!";

            }

            else if (key == "widget_uninstall_failure")

            {

                if (lang == "fr") return "échec de la désinstallation du widget.";

                return "Widget uninstallation failed.";

            }

            else if (key == "widget_missing_files_title")

            {

                if (lang == "fr") return "Fichiers manquants";

                return "Missing files";

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



            public OverlayForm(string message, bool isRestoring, bool isModeChange = false)

            {

                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;

                this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;

                this.ShowInTaskbar = false;

                this.TopMost = true;

                this.BackColor = System.Drawing.Color.FromArgb(24, 24, 26);

                this.Opacity = 0.92;



                System.Drawing.Rectangle bounds = System.Windows.Forms.Screen.PrimaryScreen != null ? System.Windows.Forms.Screen.PrimaryScreen.Bounds : new System.Drawing.Rectangle(0, 0, 1920, 1080);

                int width = 520;

                int height = isModeChange ? 95 : 65;

                this.Width = width;

                this.Height = height;

                this.Location = new System.Drawing.Point(bounds.Width / 2 - width / 2, isModeChange ? 80 : 50);



                System.Windows.Forms.Label lbl = new System.Windows.Forms.Label();

                lbl.Text = message;

                lbl.ForeColor = System.Drawing.Color.White;

                lbl.Font = new System.Drawing.Font("Segoe UI", isModeChange ? 16 : 12, System.Drawing.FontStyle.Bold);

                lbl.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

                lbl.Dock = System.Windows.Forms.DockStyle.Fill;

                this.Controls.Add(lbl);



                System.Windows.Forms.Panel accent = new System.Windows.Forms.Panel();

                accent.Height = isModeChange ? 5 : 4;

                accent.Dock = System.Windows.Forms.DockStyle.Bottom;

                System.Drawing.Color baseColor;

                if (isModeChange) baseColor = System.Drawing.Color.FromArgb(139, 92, 246); // violet/purple for mode change

                else baseColor = isRestoring ? System.Drawing.Color.FromArgb(244, 63, 94) : System.Drawing.Color.FromArgb(74, 222, 128);

                accent.BackColor = baseColor;

                this.Controls.Add(accent);



                System.Windows.Forms.Panel topAccent = new System.Windows.Forms.Panel();

                topAccent.Height = isModeChange ? 5 : 0;

                topAccent.Dock = System.Windows.Forms.DockStyle.Top;

                topAccent.BackColor = baseColor;

                if (isModeChange) this.Controls.Add(topAccent);

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



        static void ShowModeChangeOverlay(StandaloneMode mode)

        {

            bool french = false;

            try { french = (language ?? "en").StartsWith("fr", StringComparison.OrdinalIgnoreCase) || System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("fr"); } catch { }

            string label;

            if (mode == StandaloneMode.Off)         label = french ? "Mode Standalone : Désactivé"       : "Standalone mode: Off";

            else if (mode == StandaloneMode.Monitor) label = french ? "Mode Standalone : Surveillance"  : "Standalone mode: Monitor";

            else                                     label = french ? "Mode Standalone : Permanent"     : "Standalone mode: Full";

            ShowModeNotification(label);

        }



        static void ShowModeNotification(string message)

        {

            Thread t = new Thread(() =>

            {

                try

                {

                    OverlayForm overlay = new OverlayForm(message, false, true);



                    System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();

                    timer.Interval = 4000;

                    timer.Tick += (s, e) => { overlay.Close(); System.Windows.Forms.Application.ExitThread(); };

                    timer.Start();



                    System.Windows.Forms.Application.Run(overlay);

                }

                catch { }

            });

            t.SetApartmentState(ApartmentState.STA);

            t.Start();

        }



        static System.Windows.Forms.NotifyIcon? notifyIcon = null;

        static System.Threading.Thread? uiThread = null;

        static System.Windows.Forms.ContextMenuStrip? contextMenu = null;

        static bool targetInProgress = false;





        static System.Windows.Forms.Form? trayHiddenForm = null;



        static void StartUiThread()

        {

            uiThread = new Thread(() =>

            {

                try

                {

                    SetupNotifyIcon();

                    System.Windows.Forms.Application.Run();

                }

                catch (Exception ex)

                {

                    Log("[UI] Error in UI thread: " + ex.Message);

                }

            });

            uiThread.SetApartmentState(ApartmentState.STA);

            uiThread.IsBackground = true;

            uiThread.Start();

        }



        // Marshal a delegate onto the UI thread that hosts the tray icon.

        // Required because WinForms NotifyIcon.ShowBalloonTip() must be called

        // from the same STA thread that created the NotifyIcon (otherwise

        // the call silently does nothing or throws InvalidOperationException).

        static void InvokeOnUiThread(Action action)

        {

            try

            {

                if (trayHiddenForm != null && trayHiddenForm.InvokeRequired)

                {

                    trayHiddenForm.Invoke(action);

                    return;

                }

                if (trayHiddenForm != null && !trayHiddenForm.IsDisposed)

                {

                    action();

                    return;

                }

                // Last resort: direct call (probably already on UI thread).

                action();

            }

            catch (Exception ex)

            {

                Log("[UI] InvokeOnUiThread failed: " + ex.Message);

            }

        }



        static void SetupNotifyIcon()

        {

            contextMenu = new System.Windows.Forms.ContextMenuStrip();



            var targetItem = new System.Windows.Forms.ToolStripMenuItem(GetLangMessage("menu_target"), null, (s, e) => ActivateTargetMode());

            targetItem.ToolTipText = GetLangMessage("menu_target_tooltip");

            contextMenu.Items.Add(targetItem);



            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());



            string enableLabel = enabled ? GetLangMessage("menu_disable") : GetLangMessage("menu_enable");

            var toggleItem = new System.Windows.Forms.ToolStripMenuItem(enableLabel, null, (s, e) => ToggleEnableLive());

            toggleItem.ToolTipText = GetLangMessage("menu_enable_tooltip");

            contextMenu.Items.Add(toggleItem);



            var modeLabel = GetStandaloneModeTrayLabel();

            var modeItem = new System.Windows.Forms.ToolStripMenuItem(modeLabel, null, (s, e) => CycleStandaloneModeFromTray());

            modeItem.Name = "standaloneModeItem";

            modeItem.ToolTipText = GetLangMessage("menu_standalone_mode_tooltip");

            contextMenu.Items.Add(modeItem);



            var configItem = new System.Windows.Forms.ToolStripMenuItem(GetLangMessage("menu_config"), null, (s, e) => OpenConfigIni());

            configItem.ToolTipText = GetLangMessage("menu_config_tooltip");

            contextMenu.Items.Add(configItem);



            var widgetItem = new System.Windows.Forms.ToolStripMenuItem(

                gameBarWidgetEnabled ? GetLangMessage("menu_uninstall_widget") : GetLangMessage("menu_install_widget"),

                null,

                (s, e) => ToggleGameBarWidgetFromTray());

            widgetItem.Name = "widgetItem";

            widgetItem.ToolTipText = gameBarWidgetEnabled ? GetLangMessage("menu_uninstall_widget_tooltip") : GetLangMessage("menu_install_widget_tooltip");

            contextMenu.Items.Add(widgetItem);



            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());



            var exitItem = new System.Windows.Forms.ToolStripMenuItem(GetLangMessage("menu_exit"), null, (s, e) => RequestExitLive());

            exitItem.ToolTipText = GetLangMessage("menu_exit_tooltip");

            contextMenu.Items.Add(exitItem);



            // Hidden message-only form used as an Invoke target so that the

            // monitoring thread can marshal calls onto the tray's STA thread

            // (NotifyIcon.ShowBalloonTip() / ContextMenuStrip mutations must

            // happen on the thread that owns them).

            trayHiddenForm = new System.Windows.Forms.Form

            {

                FormBorderStyle = System.Windows.Forms.FormBorderStyle.None,

                ShowInTaskbar = false,

                WindowState = System.Windows.Forms.FormWindowState.Minimized,

                Width = 0,

                Height = 0,

                Opacity = 0

            };

            trayHiddenForm.Load += (s, e) => trayHiddenForm.Hide();



            notifyIcon = new System.Windows.Forms.NotifyIcon();

            try

            {

                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "icon.ico");

                if (File.Exists(iconPath))

                {

                    notifyIcon.Icon = new Icon(iconPath);

                }

                else

                {

                    notifyIcon.Icon = SystemIcons.Application;

                }

            }

            catch

            {

                notifyIcon.Icon = SystemIcons.Application;

            }

            notifyIcon.Text = "RetroBat Game Mode";

            notifyIcon.Visible = true;

            notifyIcon.ContextMenuStrip = contextMenu;

        }



        static void OpenConfigIni()

        {

            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

            try

            {

                Process.Start(new ProcessStartInfo

                {

                    FileName = "notepad.exe",

                    Arguments = "\"" + iniPath + "\"",

                    UseShellExecute = true

                });

                Log("[UI] Opening config.ini in Notepad: " + iniPath);

            }

            catch (Exception ex)

            {

                Log("[UI] Error opening config.ini: " + ex.Message);

            }

        }



        static void ToggleEnableLive()

        {

            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

            try

            {

                bool newEnabled = !enabled;

                WritePrivateProfileString("Settings", "Enable", newEnabled ? "true" : "false", iniPath);

                WritePrivateProfileString(null, null, null, iniPath);



                // Update the menu item text: show "Disable" when Enabled is true, "Enable" when Enabled is false

                if (contextMenu != null)

                {

                    foreach (System.Windows.Forms.ToolStripItem item in contextMenu.Items)

                    {

                        if (item is System.Windows.Forms.ToolStripMenuItem mi &&

                            (mi.Text == GetLangMessage("menu_enable") || mi.Text == GetLangMessage("menu_disable") ||

                             mi.ToolTipText == GetLangMessage("menu_enable_tooltip")))

                        {

                            mi.Text = newEnabled ? GetLangMessage("menu_disable") : GetLangMessage("menu_enable");

                            break;

                        }

                    }

                }



                Log("[UI] Enable toggled to " + newEnabled + " via tray icon. config.ini updated; live reload will apply.");

            }

            catch (Exception ex)

            {

                Log("[UI] Error toggling Enabled via tray: " + ex.Message);

            }

        }



        static void RequestExitLive()

        {

            Log("[UI] Exit requested via tray icon.");

            requestExit = true;

        }



        static void CleanupNotifyIcon()

        {

            try

            {

                if (notifyIcon != null)

                {

                    if (notifyIcon.Visible) notifyIcon.Visible = false;

                    notifyIcon.Dispose();

                    notifyIcon = null;

                }

                if (contextMenu != null)

                {

                    contextMenu.Dispose();

                    contextMenu = null;

                }

                if (uiThread != null && uiThread.IsAlive)

                {

                    // Signal the STA thread's message loop to exit

                    System.Windows.Forms.Application.Exit();

                }

            }

            catch (Exception ex)

            {

                Log("[UI] Error cleaning up tray icon: " + ex.Message);

            }

        }



        static string LocateWidgetMsixBundle()

        {

            try

            {

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;



                // 0. RetroBatGameModeWidget\ subfolder (canonical layout).

                //    Contains the signed RetroBatGameModeWidget.Package_*.msix produced by the WAP project.

                string widgetDir = Path.Combine(baseDir, "RetroBatGameModeWidget");

                if (Directory.Exists(widgetDir))

                {

                    // Prefer .Package_*.msix (WAP-built, signed by VS).

                    foreach (string f in Directory.GetFiles(widgetDir, "RetroBatGameModeWidget.Package_*.msix"))

                    {

                        return f;

                    }

                    // Then any .msixbundle or .msix in that subfolder.

                    foreach (string f in Directory.GetFiles(widgetDir, "RetroBatGameModeWidget_*.msixbundle"))

                    {

                        return f;

                    }

                    foreach (string f in Directory.GetFiles(widgetDir, "RetroBatGameModeWidget_*.msix"))

                    {

                        return f;

                    }

                }



                // 1. .msixbundle directly next to the optimizer exe (legacy layout).

                foreach (string f in Directory.GetFiles(baseDir, "RetroBatGameModeWidget_*.msixbundle"))

                {

                    return f;

                }

                // 2. .msix directly next to the optimizer exe (legacy layout).

                foreach (string f in Directory.GetFiles(baseDir, "RetroBatGameModeWidget_*.msix"))

                {

                    return f;

                }

                // 3. AppPackages folder produced by VS Create App Packages.

                string appPackages = Path.Combine(baseDir, "AppPackages");

                if (Directory.Exists(appPackages))

                {

                    foreach (string f in Directory.GetFiles(appPackages, "RetroBatGameModeWidget_*", SearchOption.AllDirectories))

                    {

                        if (f.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase) ||

                            f.EndsWith(".msix", StringComparison.OrdinalIgnoreCase))

                        {

                            return f;

                        }

                    }

                }

                // 4. Sibling UWP project folder (developer convenience).

                string sibling = Path.Combine(baseDir, "..", "..", "RetroBatGameModeWidget", "AppPackages");

                if (Directory.Exists(sibling))

                {

                    foreach (string f in Directory.GetFiles(sibling, "RetroBatGameModeWidget_*", SearchOption.AllDirectories))

                    {

                        if (f.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase) ||

                            f.EndsWith(".msix", StringComparison.OrdinalIgnoreCase))

                        {

                            return f;

                        }

                    }

                }

            }

            catch (Exception ex)

            {

                Log("[Widget] Error locating widget package bundle: " + ex.Message);

            }

            return "";

        }



        static void SetWidgetInstalledFlag(bool installed)

        {

            try

            {

                gameBarWidgetEnabled = installed;

                string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

                WritePrivateProfileString("Settings", "GameBarWidgetEnabled", installed ? "true" : "false", iniPath);

                WritePrivateProfileString(null, null, null, iniPath);

            }

            catch (Exception ex)

            {

                Log("[Widget] Error updating GameBarWidgetEnabled in INI: " + ex.Message);

            }

        }



        static int RunPowerShell(string psCommand, string friendlyAction)

        {

            try

            {

                Log("[Widget] " + friendlyAction + "...");

                string args = "-NoProfile -ExecutionPolicy Bypass -Command \"" + psCommand + "\"";

                var psi = new ProcessStartInfo

                {

                    FileName = "powershell.exe",

                    Arguments = args,

                    UseShellExecute = false,

                    CreateNoWindow = true,

                    RedirectStandardOutput = true,

                    RedirectStandardError = true

                };

                using (var p = Process.Start(psi))

                {

                    if (p != null)

                    {

                        string stdout = p.StandardOutput.ReadToEnd();

                        string stderr = p.StandardError.ReadToEnd();

                        p.WaitForExit();

                        if (!string.IsNullOrEmpty(stdout)) Log("[Widget][PS] " + stdout.Trim());

                        if (!string.IsNullOrEmpty(stderr)) Log("[Widget][PS][ERR] " + stderr.Trim());

                        return p.ExitCode;

                    }

                }

            }

            catch (Exception ex)

            {

                Log("[Widget] PowerShell invocation failed: " + ex.Message);

            }

            return -1;

        }



        static void InstallGameBarWidget(bool fromLiveReload)

        {

            try

            {

                if (!CheckDeveloperMode())

                {

                    Log("[Widget] Developer Mode is not enabled. Aborting widget installation.");

                    ShowDeveloperModeDialog();

                    if (gameBarWidgetEnabled)

                    {

                        SetWidgetInstalledFlag(false);

                        UpdateWidgetTrayMenuItem();

                    }

                    return;

                }



                string widgetDir = GetWidgetScriptDir();

                string installerBat = Path.Combine(widgetDir, "Install_Widget_Package.bat");

                if (!File.Exists(installerBat))

                {

                    Log("[Widget] Install script not found: " + installerBat);

                    Log("[Widget] The RetroBatGameModeWidget\\ subfolder (with Install_Widget_Package.bat and the signed");

                    Log("[Widget] RetroBatGameModeWidget.Package_*.msix) must sit next to RetroBatGameMode.exe.");

                    if (!fromLiveReload)

                    {

                        ShowWidgetNotificationSafe(

                            GetLangMessage("widget_install_failed_title"),

                            GetLangMessage("widget_missing_files_text"),

                            System.Windows.Forms.ToolTipIcon.Warning);

                        ShowWidgetModalPopup(

                            GetLangMessage("widget_missing_files_title"),

                            GetLangMessage("widget_missing_files_text"));

                    }

                    return;

                }



                // Verify a .msix is present in the widget folder before launching the .bat.

                string msix = LocateWidgetMsixBundle();

                if (string.IsNullOrEmpty(msix))

                {

                    Log("[Widget] Could not locate the Game Bar widget MSIX in the RetroBatGameModeWidget\\ subfolder.");

                    if (!fromLiveReload)

                    {

                        ShowWidgetNotificationSafe(

                            GetLangMessage("widget_install_failed_title"),

                            GetLangMessage("widget_missing_files_text"),

                            System.Windows.Forms.ToolTipIcon.Warning);

                        ShowWidgetModalPopup(

                            GetLangMessage("widget_missing_files_title"),

                            GetLangMessage("widget_missing_files_text"));

                    }

                    return;

                }



                Log("[Widget] Launching " + installerBat + " (delegates to Install_Widget_Package.ps1).");

                int exitCode = RunBatFile(installerBat, widgetDir, "--no-pause");

                if (exitCode == 0)

                {

                    SetWidgetInstalledFlag(true);

                    Log("[Widget] Game Bar widget installed successfully (via Install_Widget_Package.bat).");

                    ShowWidgetNotificationSafe(

                        GetLangMessage("widget_installed_title"),

                        GetLangMessage("widget_installed_text"),

                        System.Windows.Forms.ToolTipIcon.Info);

                    ShowWidgetModalPopup(

                        GetLangMessage("widget_install_success"),

                        GetLangMessage("widget_installed_text"));

                    try

                    {

                        string pfn = GetWidgetPackageFamilyName();

                        if (!string.IsNullOrEmpty(pfn))

                        {

                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo

                            {

                                FileName = "CheckNetIsolation.exe",

                                Arguments = "LoopbackExempt -a -n=\"" + pfn + "\"",

                                UseShellExecute = false,

                                CreateNoWindow = true

                            })?.WaitForExit(5000);

                            Log("[Widget] Loopback exemption enabled for " + pfn + " (allows widget to connect to localhost backend on port 17654).");

                        }

                    }

                    catch (Exception ex) { Log("[Widget] Loopback exemption step failed: " + ex.Message); }

                }

                else

                {

                    Log("[Widget] Game Bar widget installation failed (exit code " + exitCode + ").");

                    ShowWidgetNotificationSafe(

                        GetLangMessage("widget_install_failed_title"),

                        GetLangMessage("widget_install_failed_text"),

                        System.Windows.Forms.ToolTipIcon.Error);

                    ShowWidgetModalPopup(

                        GetLangMessage("widget_install_failure"),

                        GetLangMessage("widget_install_failed_text"));

                    if (gameBarWidgetEnabled)

                    {

                        SetWidgetInstalledFlag(false);

                        if (!fromLiveReload) UpdateWidgetTrayMenuItem();

                    }

                }

            }

            catch (Exception ex)

            {

                Log("[Widget] Installation failed: " + ex.Message);

            }

            if (!fromLiveReload) UpdateWidgetTrayMenuItem();

        }



        static void UninstallGameBarWidget(bool fromLiveReload)

        {

            try

            {

                string widgetDir = GetWidgetScriptDir();

                string uninstallerBat = Path.Combine(widgetDir, "Uninstall_Widget_Package.bat");

                if (File.Exists(uninstallerBat))

                {

                    Log("[Widget] Launching " + uninstallerBat + ".");

                    int exitCode = RunBatFile(uninstallerBat, widgetDir, "--no-pause");

                    if (exitCode == 0)

                    {

                        SetWidgetInstalledFlag(false);

                        Log("[Widget] Game Bar widget uninstalled (via Uninstall_Widget_Package.bat).");

                        ShowWidgetNotificationSafe(

                            GetLangMessage("widget_uninstalled_title"),

                            GetLangMessage("widget_uninstalled_text"),

                            System.Windows.Forms.ToolTipIcon.Info);

                        ShowWidgetModalPopup(

                            GetLangMessage("widget_uninstall_success"),

                            GetLangMessage("widget_uninstalled_text"));

                    }

                    else

                    {

                        Log("[Widget] Game Bar widget uninstallation reported exit code " + exitCode + " (it may have been already removed).");

                        SetWidgetInstalledFlag(false);

                        ShowWidgetNotificationSafe(

                            GetLangMessage("widget_uninstall_failed_title"),

                            GetLangMessage("widget_uninstall_failed_text"),

                            System.Windows.Forms.ToolTipIcon.Warning);

                        ShowWidgetModalPopup(

                            GetLangMessage("widget_uninstall_failure"),

                            GetLangMessage("widget_uninstall_failed_text"));

                    }

                }

                else

                {

                    // Fallback: directly call Remove-AppxPackage if the .bat script is missing.

                    Log("[Widget] Uninstall script not found (" + uninstallerBat + "). Falling back to Remove-AppxPackage.");

                    int exitCode = RunPowerShell("Get-AppxPackage -Name *RetroBatGameModeWidget* | Remove-AppxPackage", "Uninstalling widget MSIX");

                    if (exitCode == 0)

                    {

                        SetWidgetInstalledFlag(false);

                        Log("[Widget] Game Bar widget uninstalled.");

                        ShowWidgetNotificationSafe(

                            GetLangMessage("widget_uninstalled_title"),

                            GetLangMessage("widget_uninstalled_text"),

                            System.Windows.Forms.ToolTipIcon.Info);

                        ShowWidgetModalPopup(

                            GetLangMessage("widget_uninstall_success"),

                            GetLangMessage("widget_uninstalled_text"));

                    }

                    else

                    {

                        Log("[Widget] Game Bar widget uninstallation reported exit code " + exitCode + " (it may have been already removed).");

                        SetWidgetInstalledFlag(false);

                        ShowWidgetNotificationSafe(

                            GetLangMessage("widget_uninstall_failed_title"),

                            GetLangMessage("widget_uninstall_failed_text"),

                            System.Windows.Forms.ToolTipIcon.Warning);

                        ShowWidgetModalPopup(

                            GetLangMessage("widget_uninstall_failure"),

                            GetLangMessage("widget_uninstall_failed_text"));

                    }

                }

            }

            catch (Exception ex)

            {

                Log("[Widget] Uninstallation failed: " + ex.Message);

            }

            if (!fromLiveReload) UpdateWidgetTrayMenuItem();

        }



        // Returns the path of the RetroBatGameModeWidget\ subfolder that lives next to

        // RetroBatGameMode.exe and contains the install/uninstall .bat scripts and the

        // signed RetroBatGameModeWidget.Package_*.msix.

        static string GetWidgetScriptDir()

        {

            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RetroBatGameModeWidget"));

        }



        static int RunBatFile(string batPath, string workDir, string arguments = "")

        {

            try

            {

                System.Diagnostics.Process p = new System.Diagnostics.Process();

                p.StartInfo.FileName = batPath;

                p.StartInfo.WorkingDirectory = workDir;

                p.StartInfo.UseShellExecute = false;

                p.StartInfo.CreateNoWindow = true;

                if (!string.IsNullOrEmpty(arguments))

                {

                    p.StartInfo.Arguments = arguments;

                }

                p.Start();

                p.WaitForExit(120000); // install/uninstall can take up to 2 minutes

                return p.ExitCode;

            }

            catch (Exception ex)

            {

                Log("[Widget] Failed to run " + batPath + ": " + ex.Message);

                return -1;

            }

        }



        static string GetWidgetPackageFamilyName()

        {

            try

            {

                System.Diagnostics.Process p = new System.Diagnostics.Process();

                p.StartInfo.FileName = "powershell.exe";

                p.StartInfo.Arguments = "-NoProfile -Command \"(Get-AppxPackage -Name *RetroBatGameModeWidget*).PackageFamilyName\"";

                p.StartInfo.UseShellExecute = false;

                p.StartInfo.CreateNoWindow = true;

                p.StartInfo.RedirectStandardOutput = true;

                p.Start();

                string output = p.StandardOutput.ReadToEnd();

                p.WaitForExit(5000);

                return (output ?? "").Trim();

            }

            catch { return ""; }

        }



        // Returns true if the RetroBatGameModeWidget Appx package is actually

        // installed on the system for the current user (independent of the INI flag).

        static bool IsWidgetInstalledOnSystem()

        {

            try

            {

                string pfn = GetWidgetPackageFamilyName();

                return !string.IsNullOrEmpty(pfn);

            }

            catch { return false; }

        }



        // Sync the Game Bar widget installation state to match the INI flag.

        //   gameBarWidgetEnabled=true  + widget not installed  -> install widget

        //   gameBarWidgetEnabled=false + widget installed      -> uninstall widget

        //   any other case                                    -> no-op

        // Designed to be called both at startup and from the live INI poll.

        // The widgetOperationPending guard prevents concurrent installs from

        // (e.g.) the systray menu and the live poll firing at the same time.

        static void SyncWidgetStateFromIni(bool fromStartup = false)

        {

            try

            {

                if (widgetOperationPending)

                {

                    Log("[Widget] Sync from INI skipped: operation already in progress.");

                    return;

                }

                bool wantEnabled = gameBarWidgetEnabled;

                bool onSystem = IsWidgetInstalledOnSystem();

                if (wantEnabled && !onSystem)

                {

                    Log("[Widget] Sync: INI says enabled but widget not installed on system. Installing.");

                    widgetOperationPending = true;

                    try { InstallGameBarWidget(true); }

                    finally { widgetOperationPending = false; }

                }

                else if (!wantEnabled && onSystem)

                {

                    // At startup, treat "INI says disabled but widget IS installed"
                    // as a stale INI value (e.g. a regen or hand-edit through notepad
                    // flipped the flag to false). In that case, do NOT trigger a
                    // uninstall synchronously — instead re-align INI to true and
                    // log the auto-heal. Live reload (fromStartup=false) keeps
                    // the original behaviour: user toggled the flag while backend
                    // was active, so honour the uninstall request.
                    if (fromStartup)
                    {
                        Log("[Widget] Sync (startup): INI says disabled but widget IS installed on system. Auto-healing INI to true and skipping startup uninstall.");
                        try
                        {
                            gameBarWidgetEnabled = true;
                            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                            WritePrivateProfileString("Settings", "GameBarWidgetEnabled", "true", iniPath);
                            WritePrivateProfileString(null, null, null, iniPath);
                        }
                        catch (Exception ex) { Log("[Widget] Auto-heal write failed: " + ex.Message); }
                    }
                    else
                    {
                        Log("[Widget] Sync: INI says disabled but widget IS installed on system. Uninstalling.");
                        widgetOperationPending = true;
                        try { UninstallGameBarWidget(true); }
                        finally { widgetOperationPending = false; }
                    }

                }

                else

                {

                    Log("[Widget] Sync: INI state and system state are aligned (INI=" + wantEnabled + ", system=" + onSystem + ").");

                }

                UpdateWidgetTrayMenuItem();

            }

            catch (Exception ex)

            {

                Log("[Widget] SyncWidgetStateFromIni failed: " + ex.Message);

                widgetOperationPending = false;

            }

        }



        static void UpdateWidgetTrayMenuItem()

        {

            try

            {

                if (contextMenu == null) return;

                foreach (System.Windows.Forms.ToolStripItem item in contextMenu.Items)

                {

                    if (item.Name == "widgetItem")

                    {

                        var mi = item as System.Windows.Forms.ToolStripMenuItem;

                        if (mi != null)

                        {

                            mi.Text = gameBarWidgetEnabled ? GetLangMessage("menu_uninstall_widget") : GetLangMessage("menu_install_widget");

                            mi.ToolTipText = gameBarWidgetEnabled ? GetLangMessage("menu_uninstall_widget_tooltip") : GetLangMessage("menu_install_widget_tooltip");

                        }

                        return;

                    }

                }

            }

            catch (Exception ex)

            {

                Log("[Widget] Error updating tray menu item: " + ex.Message);

            }

        }



        static string GetStandaloneModeTrayLabel()

        {

            string modeStr;

            if (standaloneMonitor == StandaloneMode.Full) modeStr = GetLangMessage("menu_standalone_full");

            else if (standaloneMonitor == StandaloneMode.Monitor) modeStr = GetLangMessage("menu_standalone_monitor");

            else modeStr = GetLangMessage("menu_standalone_off");

            return GetLangMessage("menu_standalone_format") + ": " + modeStr;

        }



        // Cycle StandaloneMode from the tray menu: Off -> Monitor -> Full -> Off.

        // The actual state change goes through SetStandaloneMode() (HTTP endpoint),

        // ensuring the same path as widget-side changes: INI write + ReloadAndApplyConfig + overlay + tray refresh.

        static void CycleStandaloneModeFromTray()

        {

            try

            {

                StandaloneMode next = StandaloneMode.Off;

                if (standaloneMonitor == StandaloneMode.Off) next = StandaloneMode.Monitor;

                else if (standaloneMonitor == StandaloneMode.Monitor) next = StandaloneMode.Full;

                else next = StandaloneMode.Off;

                SetStandaloneMode(next);

                UpdateStandaloneTrayMenuItem();

            }

            catch (Exception ex)

            {

                Log("[Tray] CycleStandaloneMode failed: " + ex.Message);

            }

        }



        static void UpdateStandaloneTrayMenuItem()

        {

            try

            {

                if (contextMenu == null) return;

                foreach (System.Windows.Forms.ToolStripItem item in contextMenu.Items)

                {

                    if (item.Name == "standaloneModeItem")

                    {

                        var mi = item as System.Windows.Forms.ToolStripMenuItem;

                        if (mi != null)

                        {

                            mi.Text = GetStandaloneModeTrayLabel();

                        }

                        return;

                    }

                }

            }

            catch (Exception ex)

            {

                Log("[Tray] Error updating standalone mode menu item: " + ex.Message);

            }

        }



        // Show a balloon-tip notification from the tray icon when widget install/uninstall

        // finishes (since the .bat runs in the background with no console output).

        // Must be invoked on the UI thread (the tray one) to avoid WinForms cross-thread issues.

        static void ShowWidgetNotification(string title, string body, System.Windows.Forms.ToolTipIcon icon)

        {

            try

            {

                if (notifyIcon == null) return;

                notifyIcon.BalloonTipTitle = title ?? "";

                notifyIcon.BalloonTipText = body ?? "";

                notifyIcon.BalloonTipIcon = icon;

                notifyIcon.Visible = true; // ensure visible so balloon shows

                notifyIcon.ShowBalloonTip(8000); // 8s display

            }

            catch (Exception ex)

            {

                Log("[Widget] Failed to show balloon notification: " + ex.Message);

            }

        }



        // Same but marshalled to the UI thread if current thread != UI thread.

        static void ShowWidgetNotificationSafe(string title, string body, System.Windows.Forms.ToolTipIcon icon)

        {

            try

            {

                InvokeOnUiThread(() => ShowWidgetNotification(title, body, icon));

            }

            catch (Exception ex)

            {

                Log("[Widget] Failed to dispatch balloon notification: " + ex.Message);

            }

        }



        static void ShowWidgetModalPopup(string title, string message)

        {

            try

            {

                MessageBox.Show(trayHiddenForm ?? new Form { Text = title }, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

            }

            catch (Exception ex)

            {

                Log("[Widget] Failed to show modal popup: " + ex.Message);

            }

        }



        static void ToggleGameBarWidgetFromTray()

        {

            try

            {

                // The tray menu's Click event runs on the STA UI thread which
                // also pumps the NotifyIcon message loop. Running an .Install /
                // .Uninstall bat synchronously here would freeze the tray menu
                // for up to 2 minutes (p.WaitForExit(120000) inside). We move
                // the work to a background thread so the UI stays responsive;
                // any popup notification or status update is later marshalled
                // back through UpdateWidgetTrayMenuItem() once the operation
                // completes (see InstallGameBarWidget/UninstallGameBarWidget).
                bool wantInstall = !gameBarWidgetEnabled;
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        // Re-check inside the worker to avoid races with hot-reload.
                        if (wantInstall) InstallGameBarWidget(false);
                        else UninstallGameBarWidget(false);
                    }
                    catch (Exception ex)
                    {
                        Log("[Widget] Background install/uninstall failed: " + ex.Message);
                    }
                });
            }

            catch (Exception ex)

            {

                Log("[Widget] Error toggling widget from tray: " + ex.Message);

            }

        }



        static void HideGameBar()

        {

            try

            {

                IntPtr hWnd = FindWindow("Windows.Gaming.GameBar", null);

                if (hWnd != IntPtr.Zero)

                {

                    ShowWindow(hWnd, SW_HIDE);

                    Log("[Widget] Game Bar hidden.");

                }

                else

                {

                    hWnd = FindWindow("ApplicationFrameWindow", "Game Bar");

                    if (hWnd != IntPtr.Zero)

                    {

                        ShowWindow(hWnd, SW_HIDE);

                        Log("[Widget] Game Bar (ApplicationFrame) hidden.");

                    }

                }

            }

            catch (Exception ex)

            {

                Log("[Widget] Error hiding Game Bar: " + ex.Message);

            }

        }



        static string ProcessCommand(string cmd)

        {

            try

            {

                if (string.IsNullOrEmpty(cmd)) return "ERROR:empty";

                string upper = cmd.Trim().ToUpperInvariant();

                switch (upper)

                {

                    case "TARGET":

                        ActivateTargetMode();

                        return "OK:TARGET";

                    case "ENABLE":

                        if (!enabled)

                        {

                            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

                            WritePrivateProfileString("Settings", "Enable", "true", iniPath);

                            WritePrivateProfileString(null, null, null, iniPath);

                        }

                        return "OK:ENABLE";

                    case "DISABLE":

                        if (enabled)

                        {

                            string iniPath2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

                            WritePrivateProfileString("Settings", "Enable", "false", iniPath2);

                            WritePrivateProfileString(null, null, null, iniPath2);

                        }

                        return "OK:DISABLE";

                    case "GETENABLE":

                        return "GETENABLE:" + (enabled ? "true" : "false");

                    case "GETSTANDALONE":

                        return "GETSTANDALONE:" + StandaloneModeToString(standaloneMonitor);

                    case "SETENABLE":

                        {

                            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

                            bool newVal = !enabled;

                            try

                            {

                                WritePrivateProfileString("Settings", "Enable", newVal ? "true" : "false", iniPath);

                                WritePrivateProfileString(null, null, null, iniPath);

                                DateTime now = File.GetLastWriteTime(iniPath);

                                if (now > DateTime.Now) now = DateTime.Now;

                                ReloadAndApplyConfig(iniPath, ref now);

                                Log("[Widget] SETENABLE -> " + (enabled ? "true" : "false"));

                                return "SETENABLE:" + (enabled ? "true" : "false");

                            }

                            catch (Exception ex)

                            {

                                Log("[Widget] SETENABLE error: " + ex.Message);

                                return "ERROR:" + ex.Message;

                            }

                        }

                    case "EXIT":

                        requestExit = true;

                        return "OK:EXIT";

                    case "PING":

                        return "PONG";

                    case "STATUS":

                        return isOptimized ? "STATUS:ACTIVE" : "STATUS:INACTIVE";

                    case "EMERGENCY":

                        {

                            // HTTP variant of EmergencyRestore. Force immediate

                            // full undo regardless of running state, then flip

                            // Enable off so the polling loop doesn't re-apply.

                            try

                            {

                                string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

                                Log("[Widget] EMERGENCY received via HTTP _ forcing full undo.");

                                if (isOptimized)

                                {

                                    UndoOptimizations(suspendBackgroundApps, hideNonSuspendedWindows, killExplorer);

                                    isOptimized = false;

                                }

                                enabled = false;

                                WritePrivateProfileString("Settings", "Enable", "false", iniPath);

                                WritePrivateProfileString("Settings", "EmergencyRestore", "0", iniPath);

                                WritePrivateProfileString(null, null, null, iniPath);

                                return "OK:EMERGENCY";

                            }

                            catch (Exception ex)

                            {

                                Log("[Widget] EMERGENCY error: " + ex.Message);

                                return "ERROR:" + ex.Message;

                            }

                        }

                    case "APPS":

                        return "APPS:" + (Volatile.Read(ref thirdPartyApps) ?? "");

                    case "TRIGGER":

                        return "TRIGGER:" + GetTriggeringAppName();

                    default:

                        return "ERROR:unknown";

                }

            }

            catch (Exception ex)

            {

                Log("[Widget] ProcessCommand failed: " + ex.Message);

                return "ERROR:" + ex.Message;

            }

        }



        static void StartHttpServer()

        {

            Thread t = new Thread(() =>

            {

                try

                {

                    System.Net.HttpListener listener = new System.Net.HttpListener();

                    listener.Prefixes.Add("http://localhost:17654/");

                    listener.Start();

                    Log("[Widget] HTTP server listening on http://localhost:17654/");

                    while (!requestExit)

                        {

                            try

                            {

                                System.Net.HttpListenerContext ctx = listener.GetContext();

                                // Dispatch each request onto a ThreadPool worker so
                                // a slow handler (SETENABLE during a long apply)
                                // can never starve the next /PING or /STATUS poll.
                                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                                {
                                    try { HandleHttpContext(ctx); }
                                    catch (Exception ex) { Log("[Widget] handler error: " + ex.Message); }
                                });

                        }

                        catch (System.Net.HttpListenerException)

                        {

                            break;

                        }

                        catch (Exception ex)

                        {

                            Log("[Widget] HTTP server iteration error: " + ex.Message);

                            Thread.Sleep(100);

                        }

                    }

                    try { listener.Stop(); } catch { }

                    try { listener.Close(); } catch { }

                    Log("[Widget] HTTP server stopped.");

                }

                catch (Exception ex)

                {

                    Log("[Widget] HTTP server failed to start: " + ex.Message);

                }

            });

            t.IsBackground = true;

            t.Start();

        }

        // v1.5.19: per-request handler dispatched by StartHttpServer onto a
        // ThreadPool worker, so a slow /command call cannot block subsequent
        // /PING or /STATUS polls.
        static void HandleHttpContext(System.Net.HttpListenerContext ctx)
        {
            try
            {
                string path = (ctx.Request.Url != null ? ctx.Request.Url.AbsolutePath : "/").ToUpperInvariant();
                string cmd = ctx.Request.QueryString["cmd"] ?? "";
                string body = "";
                try
                {
                    using (var sr = new System.IO.StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
                    { body = sr.ReadToEnd(); }
                }
                catch { }
                if (!string.IsNullOrEmpty(body) && string.IsNullOrEmpty(cmd))
                { cmd = body.Trim(); }

                string resp;
                string name = ctx.Request.QueryString["name"] ?? "";
                string modeArg = ctx.Request.QueryString["mode"] ?? "";

                if (path == "/PING") { resp = "PONG"; }
                else if (path == "/COMMAND" && cmd.ToUpperInvariant() == "REMOVEAPP")
                { resp = RemoveThirdPartyApp(name); }
                else if (path == "/COMMAND" && cmd.ToUpperInvariant() == "SETSTANDALONE")
                {
                    string m = (modeArg ?? "").Trim().ToLowerInvariant();
                    if (m == "off") resp = SetStandaloneMode(StandaloneMode.Off);
                    else if (m == "monitor" || m == "true") resp = SetStandaloneMode(StandaloneMode.Monitor);
                    else if (m == "full") resp = SetStandaloneMode(StandaloneMode.Full);
                    else resp = "ERROR:invalid_mode (\"" + (modeArg ?? "") + "\" _ use off|monitor|full)";
                }
                else
                { resp = ProcessCommand(cmd); }

                try { ctx.Response.StatusCode = 200; } catch { }
                try { ctx.Response.AddHeader("Access-Control-Allow-Origin", "*"); } catch { }
                try { ctx.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS"); } catch { }
                try { ctx.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type"); } catch { }
                byte[] buf = System.Text.Encoding.UTF8.GetBytes(resp);
                try { ctx.Response.ContentLength64 = buf.Length; } catch { }
                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                try { ctx.Response.OutputStream.Close(); } catch { }
            }
            catch (Exception ex)
            {
                Log("[Widget] HTTP handler error: " + ex.Message);
            }
        }



        static void ActivateTargetMode()

        {

            if (targetInProgress) return;

            targetInProgress = true;



            int sec = targetTimer;

            if (sec < 1) sec = 1;

            if (sec > 30) sec = 30;



            Log("[Target] Target mode activated. Countdown " + sec + "s before capturing foreground app.");



            Thread t = new Thread(() =>

            {

                try

                {

                    TargetOverlayForm overlay = new TargetOverlayForm(sec);

                    overlay.FormClosed += (s, e) => { System.Windows.Forms.Application.ExitThread(); };



                    System.Windows.Forms.Application.Run(overlay);

                }

                catch (Exception ex)

                {

                    Log("[Target] Error during target overlay: " + ex.Message);

                }

                finally

                {

                    targetInProgress = false;

                }

            });

            t.SetApartmentState(ApartmentState.STA);

            t.Start();

        }



        static void CaptureForegroundApp()

        {

            try

            {

                IntPtr hwnd = GetForegroundWindow();

                if (hwnd == IntPtr.Zero)

                {

                    Log("[Target] No foreground window detected.");

                    ShowNotification(string.Format(GetLangMessage("target_cancelled")), false);

                    return;

                }



                uint pid;

                GetWindowThreadProcessId(hwnd, out pid);

                if (pid == 0)

                {

                    Log("[Target] Could not get PID of foreground window.");

                    ShowNotification(string.Format(GetLangMessage("target_cancelled")), false);

                    return;

                }



                string procName;

                try

                {

                    using (var proc = Process.GetProcessById((int)pid))

                    {

                        procName = proc.ProcessName;

                    }

                }

                catch (Exception ex)

                {

                    Log("[Target] Could not get process name for PID " + pid + ": " + ex.Message);

                    ShowNotification(string.Format(GetLangMessage("target_cancelled")), false);

                    return;

                }



                if (string.IsNullOrEmpty(procName) ||

                    procName.Equals("RetroBatGameMode", StringComparison.OrdinalIgnoreCase) ||

                    procName.Equals("explorer", StringComparison.OrdinalIgnoreCase) ||

                    procName.Equals("emulationstation", StringComparison.OrdinalIgnoreCase) ||

                    procName.Equals("retrobat", StringComparison.OrdinalIgnoreCase) ||

                    procName.Equals("dwm", StringComparison.OrdinalIgnoreCase) ||

                    procName.Equals("GameBar", StringComparison.OrdinalIgnoreCase) ||

                    procName.IndexOf("GameBar", StringComparison.OrdinalIgnoreCase) >= 0)

                {

                    Log("[Target] Foreground app '" + procName + "' is blacklisted from targeting.");

                    ShowNotification(string.Format(GetLangMessage("target_cancelled")), false);

                    return;

                }



                // Persist ThirdPartyApps and StandaloneMonitor to INI so the live reload picks them up

                var existing = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrEmpty(thirdPartyApps))

                {

                    string[] parts = thirdPartyApps.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var p in parts) existing.Add(p.Trim());

                }

                bool added = false;

                if (!existing.Contains(procName))

                {

                    existing.Add(procName);

                    Volatile.Write(ref thirdPartyApps, string.Join(",", existing));

                    added = true;

                    Log("[Target] Added '" + procName + "' to ThirdPartyApps. New value: " + Volatile.Read(ref thirdPartyApps));

                }

                else

                {

                    Log("[Target] '" + procName + "' is already in ThirdPartyApps. No change.");

                }



                bool enabledStandalone = false;

                if (standaloneMonitor == StandaloneMode.Off)

                {

                    Log("[Target] Enabling StandaloneMonitor=Monitor automatically because a target was captured.");

                    standaloneMonitor = StandaloneMode.Monitor;

                    enabledStandalone = true;

                }



                // Write the changes to config.ini so the live reload and watchdog can use them

                if (added || enabledStandalone)

                {

                    string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

                    try

                    {

                        if (added)

                        {

                            WritePrivateProfileString("Settings", "ThirdPartyApps", thirdPartyApps, iniPath);

                        }

                        if (enabledStandalone)

                        {

                            WritePrivateProfileString("Settings", "StandaloneMonitor", StandaloneModeToString(standaloneMonitor), iniPath);

                        }

                        WritePrivateProfileString(null, null, null, iniPath);

                        Log("[Target] Persisted changes to config.ini.");



                        // If optimizations are already active and a new app was added,

                        // re-apply optimizations directly so the newly captured app gets suspended/hidden

                        // while already frozen processes stay frozen. Then sync the live-reload snapshot

                        // so the polling loop won't trigger an undo/redo cycle on the next tick.

                        if (isOptimized && added)

                        {

                            Log("[Target] Re-applying optimizations to include the newly captured app...");

                            ApplyOptimizations();

                        }



                        // Sync: run ReloadAndApplyConfig now so the live-reload snapshot is up-to-date

                        // and the polling loop won't see any change on the next iteration.

                        DateTime now = File.GetLastWriteTime(iniPath);

                        if (now > DateTime.Now) now = DateTime.Now;

                        ReloadAndApplyConfig(iniPath, ref now);

                    }

                    catch (Exception ex)

                    {

                        Log("[Target] Error persisting targeted app to config.ini: " + ex.Message);

                    }

                }



                // Hide the Game Bar overlay if it is currently visible, so the user returns to

                // a clean desktop right after capturing the foreground app from a widget command.

                HideGameBar();



                ShowNotification(string.Format(GetLangMessage("target_added"), procName), false);

            }

            catch (Exception ex)

            {

                Log("[Target] Error capturing foreground app: " + ex.Message);

                ShowNotification(string.Format(GetLangMessage("target_cancelled")), false);

            }

        }



        class TargetOverlayForm : System.Windows.Forms.Form

        {

            protected override bool ShowWithoutActivation => true;



            protected override System.Windows.Forms.CreateParams CreateParams

            {

                get

                {

                    System.Windows.Forms.CreateParams cp = base.CreateParams;

                    cp.ExStyle |= 0x00000008 | 0x08000000 | 0x00000080 | 0x00000020;

                    return cp;

                }

            }



            private int remaining;

            private System.Windows.Forms.Label lbl;

            private System.Windows.Forms.Timer timer;



            public TargetOverlayForm(int seconds)

            {

                this.remaining = seconds;

                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;

                this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;

                this.ShowInTaskbar = false;

                this.TopMost = true;

                this.BackColor = System.Drawing.Color.FromArgb(24, 24, 26);

                this.Opacity = 0.90;



                System.Drawing.Rectangle bounds = System.Windows.Forms.Screen.PrimaryScreen != null

                    ? System.Windows.Forms.Screen.PrimaryScreen.Bounds

                    : new System.Drawing.Rectangle(0, 0, 1920, 1080);

                int width = 500;

                int height = 80;

                this.Width = width;

                this.Height = height;

                this.Location = new System.Drawing.Point(bounds.Width / 2 - width / 2, 50);



                lbl = new System.Windows.Forms.Label();

                lbl.ForeColor = System.Drawing.Color.White;

                lbl.Font = new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Bold);

                lbl.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

                lbl.Dock = System.Windows.Forms.DockStyle.Fill;

                this.Controls.Add(lbl);



                System.Windows.Forms.Panel accent = new System.Windows.Forms.Panel();

                accent.Height = 4;

                accent.Dock = System.Windows.Forms.DockStyle.Bottom;

                accent.BackColor = System.Drawing.Color.FromArgb(59, 130, 246); // blue for targeting

                this.Controls.Add(accent);



                UpdateLabel();



                timer = new System.Windows.Forms.Timer();

                timer.Interval = 1000;

                timer.Tick += (s, e) =>

                {

                    remaining--;

                    if (remaining <= 0)

                    {

                        timer.Stop();

                        this.Close();

                        CaptureForegroundApp();

                    }

                    else

                    {

                        UpdateLabel();

                    }

                };

                timer.Start();

            }



            private void UpdateLabel()

            {

                string template = GetLangMessage("target_running");

                lbl.Text = template.Replace("{0}", remaining.ToString());

            }

        }



        // ?? v1.5.9 StuckRects registry helpers ?????????????????????????????

        // The StuckRects3 "Settings" binary is a TAGRECT-like struct where:

        //   byte[0..3]   = reserved flags

        //   byte[4..7]   = monitor handle

        //   byte[8..11]  = edge: 0x01=Top, 0x02=Bottom (non-autohide), 0x03=Bottom+Autohide

        //                  0x04=Left, 0x05=Right, 0x06=TopAutohide, 0x07=BottomAutohide (per docs)

        // Confirmed experimentally on this Windows 11 build: writing edge=0x03

        // (with byte[3] = 0x00 reserved unchanged) re-applies the Windows

        // "Auto-hide the taskbar" toggle bit, exactly like the Settings UI.

        // NOTE: explorer.exe ignores WM_SETTINGCHANGE broadcast for this key _

        // it only re-reads on its own startup. Therefore SetTaskbarStuckRects

        // is only useful immediately followed by RestartExplorer().

        static byte[] ReadStuckRects3Settings()

        {

            try

            {

                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(

                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3"))

                {

                    if (key == null) return null;

                    return key.GetValue("Settings") as byte[];

                }

            }

            catch (Exception ex) { Log("[Registry] ReadStuckRects3 error: " + ex.Message); }

            return null;

        }



        // Returns the current taskbar edge byte (= ABE_* | ABS_AUTOHIDE bit).

        // Returns -1 if registry key is missing or unreadable.

        static int GetTaskbarEdge()

        {

            byte[] s = ReadStuckRects3Settings();

            if (s == null || s.Length < 9) return -1;

            return s[8];

        }



        // Writes the new edge byte and broadcasts the change to all apps.

        // explorer.exe still has to be restarted (RestartExplorer) for it

        // to honor the new value.

        static void SetTaskbarEdge(int newEdge)

        {

            try

            {

                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(

                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3", true))

                {

                    if (key == null)

                    {

                        Log("[Registry] StuckRects3 missing _ cannot write taskbar edge.");

                        return;

                    }

                    byte[]? s = key.GetValue("Settings") as byte[];

                    if (s == null || s.Length < 12)

                    {

                        Log("[Registry] StuckRects3 Settings length too short: " + (s == null ? 0 : s.Length));

                        return;

                    }

                    s[8] = (byte)newEdge;

                    key.SetValue("Settings", s, Microsoft.Win32.RegistryValueKind.Binary);

                    Log("[Registry] StuckRects3 edge byte[8] = 0x" + newEdge.ToString("X2") + " written.");



                    // v1.5.15: read-back to confirm the value actually stuck.

                    // If something (defender, admin redirection, virtualisation)

                    // rewrote it, we want visibility BEFORE explorer restart.

                    byte[]? verify = key.GetValue("Settings") as byte[];

                    if (verify != null && verify.Length >= 12)

                    {

                        if (verify[8] == (byte)newEdge)

                            Log("[Registry] Verify: byte[8] confirmed = 0x" + verify[8].ToString("X2"));

                        else

                            Log("[Registry] Verify FAIL: byte[8] = 0x" + verify[8].ToString("X2") + " (expected 0x" + newEdge.ToString("X2") + ")");

                    }

                }

            }

            catch (Exception ex) { Log("[Registry] SetTaskbarEdge error: " + ex.Message); }

        }



        // Returns true if the current Windows is Win10 or Win11 < 22H2 (old

        // legacy Win32 taskbar that honours "TraySettings" broadcast and

        // doesn't need an explorer restart to re-read StuckRects3).

        // Returns false (=> restart required) for Win11 22H2+ where the taskbar

        // is now WinUI/XAML-backed.

        static bool IsWin11_22H2OrLater()

        {

            try

            {

                int ver = Environment.OSVersion.Version.Build;

                // Win11 build numbers start at 22000 (21H2 = 22000, 22H2 = 22621).

                if (Environment.OSVersion.Version.Major == 10 && ver >= 22621)

                    return true;

            }

            catch (Exception ex) { Log("[System] OSVersion detection error: " + ex.Message); }

            return false;

        }



        // ???????????????????????????????????????????????????????????????????????

        // v1.5.13 multi-channel taskbar nudge. Drives explorer.exe's taskbar

        // state via every reasonable WinAPI in parallel. Used for Win11 22H2+

        // where pure registry writes + broadcasts may not stick because the

        // XAML taskbar holds state in memory.

        //

        // Channels fired (returns count honoured):

        //   1. ABM_SETSTATE (state machine, in-memory)

        //   2. WM_SETTINGCHANGE "TraySettings" SendMessageTimeout

        //   3. WM_SETTINGCHANGE "TraySettings" PostMessage (fallback non-blocking)

        //   4. WM_SETTINGCHANGE "StuckRects"  SendMessageTimeout

        //   5. ABM_ACTIVATE  (re-engage appbar handler)

        // We SW_HIDE on the window anyway as a safety net.

        static int MultiChannelTaskbarSignal(IntPtr tray, int absState)

        {

            int fired = 0;

            try

            {

                APPBARDATA abd = new APPBARDATA

                {

                    cbSize = Marshal.SizeOf(typeof(APPBARDATA)),

                    hWnd = tray,

                    lParam = (IntPtr)absState

                };

                SHAppBarMessage(ABM_SETSTATE, ref abd);

                fired++;

            }

            catch (Exception ex) { Log("[Taskbar] ABM_SETSTATE error: " + ex.Message); }

            try

            {

                IntPtr pst = Marshal.StringToHGlobalAnsi("TraySettings");

                IntPtr res;

                SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, pst,

                    SMTO_ABORTIFHUNG, 200, out res);

                Marshal.FreeHGlobal(pst);

                fired++;

            }

            catch (Exception ex) { Log("[Taskbar] TraySettings broadcast error: " + ex.Message); }

            try

            {

                IntPtr pst = Marshal.StringToHGlobalAnsi("TraySettings");

                PostMessage(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, pst);

                Marshal.FreeHGlobal(pst);

                fired++;

            }

            catch (Exception ex) { Log("[Taskbar] TraySettings PostMessage error: " + ex.Message); }

            try

            {

                IntPtr pst = Marshal.StringToHGlobalAnsi("StuckRects");

                IntPtr res;

                SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, pst,

                    SMTO_ABORTIFHUNG, 200, out res);

                Marshal.FreeHGlobal(pst);

                fired++;

            }

            catch (Exception ex) { Log("[Taskbar] StuckRects broadcast error: " + ex.Message); }

            try

            {

                APPBARDATA abd2 = new APPBARDATA

                {

                    cbSize = Marshal.SizeOf(typeof(APPBARDATA)),

                    hWnd = tray

                };

                SHAppBarMessage(ABM_ACTIVATE, ref abd2);

                fired++;

            }

            catch (Exception ex) { Log("[Taskbar] ABM_ACTIVATE error: " + ex.Message); }

            return fired;

        }



        // Restart explorer.exe helper kept for emergency use (e.g. if the Game

        // Bar widget has bugged out). NOT called by the normal pipeline

        // because restarting explorer breaks Game Bar (Win+G) until the next

        // sign-in / shell restart. Disabled by default.

        static void RestartExplorer(int edgeAfterRestart = -1)

        {

            try

            {

                Log("[Taskbar] Restarting explorer.exe (edge=" + (edgeAfterRestart < 0 ? "preserved" : edgeAfterRestart.ToString("X2")) + ")...");

                // Kill explorer.exe FIRST. Win10/11 keeps Game Bar companion

                // processes alive but kills the shell host. Critically: killing

                // explorer first means there is NO live explorer process able

                // to write back the old (g_default=02) edge value when we are

                // about to write 03 for the *next* launch.

                Process.Start(new ProcessStartInfo

                {

                    FileName = "taskkill.exe",

                    Arguments = "/f /im explorer.exe",

                    UseShellExecute = false,

                    CreateNoWindow = true,

                    RedirectStandardOutput = true

                }).WaitForExit(5000);

                Thread.Sleep(800);



                // v1.5.16: write the desired edge value AFTER explorer is

                // dead, so there is zero contention with the live shell's

                // in-memory cache. explorer.exe is gone, so the next time

                // it launches it MUST read fresh from the registry.

                if (edgeAfterRestart >= 0)

                {

                    SetTaskbarEdge(edgeAfterRestart);

                    Log("[Taskbar] StuckRects3 byte[8] = 0x" + edgeAfterRestart.ToString("X2") + " re-written after explorer was killed.");

                }



                // Relaunch explorer. The shell startup reads StuckRects3

                // BEFORE the taskbar is registered, so our freshly written

                // 0x03 value gets picked up by the new Taskbar UI.

                Process.Start(new ProcessStartInfo

                {

                    FileName = "explorer.exe",

                    UseShellExecute = true

                });

                // Give the shell time to fully initialise. Win11 24H2 needs

                // ~2-3s for explorer.exe to register its tray window again.

                Thread.Sleep(2200);

                Log("[Taskbar] explorer.exe restarted.");



                // As soon as the new Shell_TrayWnd exists, hide it once AND

                // shove it off-screen via SetWindowPos. This guarantees the

                // tray cannot pop in on mouse hover at the bottom edge.

                IntPtr tray = IntPtr.Zero;

                for (int i = 0; i < 30; i++)

                {

                    tray = FindWindow("Shell_TrayWnd", null);

                    if (tray != IntPtr.Zero) break;

                    Thread.Sleep(150);

                }

                if (tray != IntPtr.Zero)

                {

                    ShowWindow(tray, SW_HIDE);

                    // Combined with SW_HIDE for absolute stealth: move off-screen

                    // so the autohide logic can't pop it back on hover.

                    int tY = -3000;

                    try

                    {

                        foreach (var scr in System.Windows.Forms.Screen.AllScreens)

                        {

                            int bot = scr.Bounds.Bottom;

                            if (bot > tY) { tY = bot + 1000; }

                        }

                        if (tY < 0) tY = 0;

                    }

                    catch { tY = 0; }

                    SetWindowPos(tray, IntPtr.Zero,

                        scrSafeLeft(), tY, 0, 0,

                        SWP_NOACTIVATE | SWP_NOSIZE | SWP_NOZORDER);

                    Log("[Taskbar] Post-restart tray hidden and moved offscreen at y=" + tY);

                }

                else

                {

                    Log("[Taskbar] Warning: Shell_TrayWnd not found 4s after restart.");

                }



                // Re-mount the Game Bar companion processes if they were

                // actually killed. (`taskkill /f /im explorer.exe` usually

                // doesn't kill these, but XP/Win10 quirks may; be defensive.)

                ReRmsGameBarIfNeeded();

            }

            catch (Exception ex) { Log("[Taskbar] RestartExplorer error: " + ex.Message); }

        }



        // Helper to get a safe left X for SetWindowPos (any monitor,

        // primary by default).

        static int scrSafeLeft()

        {

            try

            {

                return System.Windows.Forms.Screen.PrimaryScreen.Bounds.Left;

            }

            catch { return 0; }

        }



        // Re-registers the Game Bar MSIX package by asking AppxDeploymentService

        // to re-register the already-installed bundle. This wakes the Game Bar

        // (Win+G, Game Bar widget hotkey) back up in case it stalled after the

        // explorer restart. Idempotent if Game Bar is already responsive.

        static void ReRmsGameBarIfNeeded()

        {

            try

            {

                bool need = false;

                foreach (var p in Process.GetProcessesByName("GameBarPresenceWriter"))

                {

                    need = true; break;

                }

                if (!need)

                {

                    foreach (var p in Process.GetProcessesByName("XboxGameBarWidgets"))

                    {

                        need = true; break;

                    }

                }



                if (!need)

                {

                    Log("[GameBar] Companion processes alive after restart, no re-mount needed.");

                    return;

                }



                Log("[GameBar] Re-mounting Game Bar bundle to recover Win+G hook...");

                var psi = new ProcessStartInfo

                {

                    FileName = "powershell.exe",

                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Get-AppxPackage -AllUsers Microsoft.WindowsGameBar| ForEach-Object { Add-AppxPackage -DisabledDevelopmentMode -Register ($_.InstallLocation + '\\AppXManifest.xml') }\"",

                    UseShellExecute = false,

                    CreateNoWindow = true

                };

                var proc = Process.Start(psi);

                try { if (proc != null) proc.WaitForExit(15000); } catch { }

                // Give the re-registration a couple of seconds before the next

                // instruction (e.g. SH_HIDE) so the shell can pick up the

                // freshly registered widget host without collateral damage.

                Thread.Sleep(1500);

            }

            catch (Exception ex)

            {

                Log("[GameBar] ReRmsGameBarIfNeeded error (non-fatal): " + ex.Message);

            }

        }

    }

}