#pragma warning disable CA1416
#pragma warning disable CS8605
#pragma warning disable CS8632
#nullable disable
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace RetroBatAttractMode
{
    class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        // ====================================================================
        // WIN32 API - ECRITURE DIRECTE DANS LA CONSOLE ATTACHEE (sans buffer)
        // WriteConsoleOutputCharacter + GetStdHandle.
        // Utilise car Console.Out peut bufferiser silencieusement
        // sous AllocConsole + WinExe. Ces appels ecrivent directement
        // au buffer de l'ecran de la console Windows.
        // ====================================================================
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool WriteConsole(IntPtr hConsoleOutput, string lpBuffer, uint nNumberOfCharsToWrite, out uint lpNumberOfCharsWritten, IntPtr lpReserved);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetConsoleTextAttribute(IntPtr hConsoleOutput, ushort wAttributes);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetConsoleScreenBufferInfo(IntPtr hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

        [StructLayout(LayoutKind.Sequential)]
        struct CONSOLE_SCREEN_BUFFER_INFO
        {
            public COORD dwSize;
            public COORD dwCursorPosition;
            public ushort wAttributes;
            public COORD srWindow;
            public bool bMaximumWindowSize;
            public bool bPopupAttributes;
            public bool bMenuAttributes;
            public bool bFullscreenAttributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct COORD
        {
            public short X;
            public short Y;
        }

        const int STD_OUTPUT_HANDLE = -11;
        const int STD_ERROR_HANDLE = -12;

        // Ecrit une ligne dans la console via Win32 (garantit l'affichage).
        static void WriteConsoleDirect(string line)
        {
            try
            {
                IntPtr hOut = GetStdHandle(STD_OUTPUT_HANDLE);
                if (hOut == IntPtr.Zero || hOut.ToInt64() == -1) return;
                string fullLine = line + "\r\n";
                uint written;
                WriteConsole(hOut, fullLine, (uint)fullLine.Length, out written, IntPtr.Zero);
            }
            catch { }
        }

        // ====================================================================
        // WIN32 API - EMULATION DES INPUTS SOURIS (DÉFILEMENT ET CLICS)
        // ====================================================================
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        const uint INPUT_MOUSE = 0;
        const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        const uint MOUSEEVENTF_LEFTUP = 0x0004;
        const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        const uint MOUSEEVENTF_WHEEL = 0x0800;
        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint KEYEVENTF_SCANCODE = 0x0008;
        const ushort VK_RETURN = 0x0D;
        const ushort VK_ESCAPE = 0x1B;

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        // ====================================================================
        // WIN32 API - DETECTION DE L'ACTIVITÉ SYSTEME (CLAVIER & SOURIS)
        // ====================================================================
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        // Hooks globaux bas niveau pour capturer les vraies activités physiques (sans faux positifs de polling de souris de jeu)
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static LowLevelKeyboardProc _proc = HookCallback;
        private static LowLevelMouseProc _mouseProc = MouseHookCallback;

        private static IntPtr _hookID = IntPtr.Zero;
        private static IntPtr _mouseHookID = IntPtr.Zero;

        private static bool KeyboardActivityDetected = false;
        private static bool MouseActivityDetected = false;

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                if (curModule != null && curModule.ModuleName != null)
                {
                    return SetWindowsHookEx(13, proc, GetModuleHandle(curModule.ModuleName), 0);
                }
                return IntPtr.Zero;
            }
        }

        private static IntPtr SetMouseHook(LowLevelMouseProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                if (curModule != null && curModule.ModuleName != null)
                {
                    return SetWindowsHookEx(14, proc, GetModuleHandle(curModule.ModuleName), 0);
                }
                return IntPtr.Zero;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)0x0100 || wParam == (IntPtr)0x0104)) // WM_KEYDOWN ou WM_SYSKEYDOWN
            {
                try
                {
                    KBDLLHOOKSTRUCT kbd = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                    // Ignorer les touches simulees par notre programme (LLKHF_INJECTED = 0x10)
                    if ((kbd.flags & 0x10) == 0)
                    {
                        KeyboardActivityDetected = true;
                    }
                }
                catch
                {
                    KeyboardActivityDetected = true;
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                // WM_LBUTTONDOWN = 0x0201, WM_RBUTTONDOWN = 0x0204, WM_MBUTTONDOWN = 0x0207, WM_MOUSEWHEEL = 0x020A
                if (wParam == (IntPtr)0x0201 || wParam == (IntPtr)0x0204 || wParam == (IntPtr)0x0207 || wParam == (IntPtr)0x020A)
                {
                    try
                    {
                        MSLLHOOKSTRUCT msl = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                        // Ignorer les clics/defilements simules par notre programme (MSLLF_INJECTED = 0x01)
                        if ((msl.flags & 0x01) == 0)
                        {
                            MouseActivityDetected = true;
                        }
                    }
                    catch
                    {
                        MouseActivityDetected = true;
                    }
                }
            }
            return CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [DllImport("user32.dll")]
        static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        static extern IntPtr DispatchMessage(ref MSG lpMsg);

        static void HookThread()
        {
            _hookID = SetHook(_proc);
            _mouseHookID = SetMouseHook(_mouseProc);

            if (_hookID != IntPtr.Zero || _mouseHookID != IntPtr.Zero)
            {
                MSG msg;
                while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }

                if (_hookID != IntPtr.Zero) UnhookWindowsHookEx(_hookID);
                if (_mouseHookID != IntPtr.Zero) UnhookWindowsHookEx(_mouseHookID);
            }
        }

        // ====================================================================
        // WIN32 API - DETECTION DE L'ACTIVITÉ MANETTES (XINPUT)
        // ====================================================================
        [StructLayout(LayoutKind.Sequential)]
        public struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        class XInputHelper
        {
            [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
            private static extern int XInputGetState14(int dwUserIndex, ref XINPUT_STATE pState);

            [DllImport("xinput1_3.dll", EntryPoint = "XInputGetState")]
            private static extern int XInputGetState13(int dwUserIndex, ref XINPUT_STATE pState);

            [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
            private static extern int XInputGetState910(int dwUserIndex, ref XINPUT_STATE pState);

            private static bool _useDll14 = true;
            private static bool _useDll13 = true;
            private static bool _useDll910 = true;

            public static bool GetState(int userIndex, ref XINPUT_STATE state)
            {
                if (_useDll14)
                {
                    try { return XInputGetState14(userIndex, ref state) == 0; }
                    catch (DllNotFoundException) { _useDll14 = false; }
                }
                if (_useDll13)
                {
                    try { return XInputGetState13(userIndex, ref state) == 0; }
                    catch (DllNotFoundException) { _useDll13 = false; }
                }
                if (_useDll910)
                {
                    try { return XInputGetState910(userIndex, ref state) == 0; }
                    catch (DllNotFoundException) { _useDll910 = false; }
                }
                return false;
            }
        }

        // ====================================================================
        // WINMM JOYSTICK API - DETECTION UNIVERSELLE (DirectInput & generiques)
        // Couvre jusqu'a 16 joysticks detectes par Windows, independamment de XInput.
        // ====================================================================
        [StructLayout(LayoutKind.Sequential)]
        public struct JOYINFOEX
        {
            public uint dwSize;
            public uint dwFlags;
            public uint dwXpos;
            public uint dwYpos;
            public uint dwZpos;
            public uint dwRpos;
            public uint dwUpos;
            public uint dwVpos;
            public uint dwButtons;
            public uint dwButtonNumber;
            public uint dwPOV;
            public uint dwReserved1;
            public uint dwReserved2;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct JOYCAPS
        {
            public ushort wMid;
            public ushort wPid;
            public string szPname;
            public uint wXmin;
            public uint wXmax;
            public uint wYmin;
            public uint wYmax;
            public uint wZmin;
            public uint wZmax;
            public uint wNumButtons;
            public uint wPeriodMin;
            public uint wPeriodMax;
            public uint wRmin;
            public uint wRmax;
            public uint wUmin;
            public uint wUmax;
            public uint wVmin;
            public uint wVmax;
            public uint wCaps;
            public uint wMaxAxes;
            public uint wNumAxes;
            public uint wMaxButtons;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szRegKey;
            public uint wJoyId;
        }

        const uint JOY_RETURNALL = 0x000000FF;
        const uint JOYSTICKID1 = 0;
        const uint JOYSTICKID2 = 1;
        const int JOYERR_NOERROR = 0;

        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        static extern int joyGetNumDevs();

        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        static extern int joyGetDevCaps(uint uJoyID, ref JOYCAPS pjc, int cbjc);

        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        static extern int joyGetPosEx(uint uJoyID, ref JOYINFOEX pji);

        // Etat precedent memorise pour detecter les variations (winmm)
        static Dictionary<uint, JOYINFOEX> LastJoyInfo = new Dictionary<uint, JOYINFOEX>();
        static List<uint> AvailableJoysticks = new List<uint>();
        static DateTime LastJoystickScan = DateTime.MinValue;

        static void RefreshJoystickList()
        {
            // Ne rescanne qu'une fois toutes les 10 secondes (evite le cout de joyGetDevCaps a chaque boucle)
            if ((DateTime.Now - LastJoystickScan).TotalSeconds < 10 && AvailableJoysticks.Count > 0) return;
            LastJoystickScan = DateTime.Now;

            AvailableJoysticks.Clear();
            int maxDevs = joyGetNumDevs();
            if (maxDevs < 0) maxDevs = 16;
            for (uint i = 0; i < maxDevs; i++)
            {
                JOYCAPS caps = new JOYCAPS();
                try
                {
                    if (joyGetDevCaps(i, ref caps, Marshal.SizeOf(typeof(JOYCAPS))) == JOYERR_NOERROR)
                    {
                        // Verifier qu'un peripherique est reellement connecte en essayant de lire son etat
                        JOYINFOEX info = new JOYINFOEX();
                        info.dwSize = (uint)Marshal.SizeOf(typeof(JOYINFOEX));
                        info.dwFlags = JOY_RETURNALL;
                        if (joyGetPosEx(i, ref info) == JOYERR_NOERROR)
                        {
                            AvailableJoysticks.Add(i);
                        }
                    }
                }
                catch { }
            }
            if (AvailableJoysticks.Count > 0)
            {
                WriteLog($"[winmm] {AvailableJoysticks.Count} joystick(s) detected.");
            }
        }

        // Seuil relatif pour considerer qu'un axe analogique winmm a bouge (en % de l'etendue).
        const double JoyAxisRelativeThreshold = 0.04; // 4% de l'etendue totale

        static bool CheckWinmmJoystickActivity()
        {
            bool active = false;
            RefreshJoystickList();

            foreach (uint id in AvailableJoysticks)
            {
                JOYCAPS caps = new JOYCAPS();
                if (joyGetDevCaps(id, ref caps, Marshal.SizeOf(typeof(JOYCAPS))) != JOYERR_NOERROR)
                    continue;

                JOYINFOEX info = new JOYINFOEX();
                info.dwSize = (uint)Marshal.SizeOf(typeof(JOYINFOEX));
                info.dwFlags = JOY_RETURNALL;
                if (joyGetPosEx(id, ref info) != JOYERR_NOERROR)
                    continue;

                // Bouton presse => activite immediate
                if (info.dwButtons != 0 || info.dwButtonNumber != 0)
                    active = true;

                // Comparaison avec l'etat precedent pour detecter un MVEMENT (plus fiable que des seuils absolus,
                // car Certaines manettes renvoient une valeur non-centree au repos).
                if (LastJoyInfo.TryGetValue(id, out JOYINFOEX prev))
                {
                    uint rangeX = caps.wXmax - caps.wXmin; if (rangeX == 0) rangeX = 1;
                    uint rangeY = caps.wYmax - caps.wYmin; if (rangeY == 0) rangeY = 1;
                    uint rangeZ = caps.wZmax - caps.wZmin; if (rangeZ == 0) rangeZ = 1;

                    double dx = Math.Abs((double)(info.dwXpos - prev.dwXpos)) / rangeX;
                    double dy = Math.Abs((double)(info.dwYpos - prev.dwYpos)) / rangeY;
                    double dz = Math.Abs((double)(info.dwZpos - prev.dwZpos)) / rangeZ;

                    if (dx > JoyAxisRelativeThreshold || dy > JoyAxisRelativeThreshold || dz > JoyAxisRelativeThreshold)
                        active = true;

                    // Variation du chapeau (POV/HAT)
                    if (info.dwPOV != prev.dwPOV && info.dwPOV != 0xFFFFFFFF && prev.dwPOV != 0xFFFFFFFF)
                        active = true;

                    // Variation des axes de rotation si supportes
                    if (caps.wCaps != 0)
                    {
                        uint rangeR = caps.wRmax - caps.wRmin; if (rangeR == 0) rangeR = 1;
                        uint rangeU = caps.wUmax - caps.wUmin; if (rangeU == 0) rangeU = 1;
                        uint rangeV = caps.wVmax - caps.wVmin; if (rangeV == 0) rangeV = 1;

                        if ((caps.wCaps & 0x02) != 0) // JOYCAPS_HASR
                            if (Math.Abs((double)(info.dwRpos - prev.dwRpos)) / rangeR > JoyAxisRelativeThreshold) active = true;
                        if ((caps.wCaps & 0x04) != 0) // JOYCAPS_HASU
                            if (Math.Abs((double)(info.dwUpos - prev.dwUpos)) / rangeU > JoyAxisRelativeThreshold) active = true;
                        if ((caps.wCaps & 0x08) != 0) // JOYCAPS_HASV
                            if (Math.Abs((double)(info.dwVpos - prev.dwVpos)) / rangeV > JoyAxisRelativeThreshold) active = true;
                    }
                }

                LastJoyInfo[id] = info;
            }

            return active;
        }

        // ====================================================================
        // SDL2 - CHARGEMENT DYNAMIQUE (couvre toutes les manettes supportees par SDL)
        // SDL2.dll est charge depuis le dossier de l'exe si present. Sans SDL,
        // cette partie est silencieusement ignoree.
        // ====================================================================
        static class SdlGamepadHelper
        {
            private static IntPtr _sdlLib = IntPtr.Zero;
            private static bool _initTried = false;
            private static bool _available = false;

            const uint SDL_INIT_JOYSTICK = 0x00000200;
            const uint SDL_INIT_GAMECONTROLLER = 0x00002000;
            const int SDL_HAT_CENTERED = 0;

            // Delegates marshalé's pour les fonctions SDL
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate int dSDL_InitSubSystem(uint flags);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate int dSDL_WasInit(uint flags);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate int dSDL_NumJoysticks();
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate IntPtr dSDL_JoystickOpen(int device_index);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate void dSDL_JoystickClose(IntPtr joystick);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate short dSDL_JoystickGetAxis(IntPtr joystick, int axis);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate byte dSDL_JoystickGetHat(IntPtr joystick, int hat);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate byte dSDL_JoystickGetButton(IntPtr joystick, int button);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate int dSDL_JoystickNumAxes(IntPtr joystick);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate int dSDL_JoystickNumHats(IntPtr joystick);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate int dSDL_JoystickNumButtons(IntPtr joystick);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate void dSDL_JoystickUpdate();
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate IntPtr dSDL_GameControllerOpen(int joystick_index);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate void dSDL_GameControllerClose(IntPtr controller);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate short dSDL_GameControllerGetAxis(IntPtr controller, int axis);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate byte dSDL_GameControllerGetButton(IntPtr controller, int button);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate void dSDL_GameControllerUpdate();

            static dSDL_InitSubSystem _initSubSystem;
            static dSDL_NumJoysticks _numJoysticks;
            static dSDL_JoystickOpen _joystickOpen;
            static dSDL_JoystickClose _joystickClose;
            static dSDL_JoystickGetAxis _joystickGetAxis;
            static dSDL_JoystickGetHat _joystickGetHat;
            static dSDL_JoystickGetButton _joystickGetButton;
            static dSDL_JoystickNumAxes _joystickNumAxes;
            static dSDL_JoystickNumHats _joystickNumHats;
            static dSDL_JoystickNumButtons _joystickNumButtons;
            static dSDL_JoystickUpdate _joystickUpdate;
            static dSDL_GameControllerOpen _controllerOpen;
            static dSDL_GameControllerClose _controllerClose;
            static dSDL_GameControllerGetAxis _controllerGetAxis;
            static dSDL_GameControllerGetButton _controllerGetButton;
            static dSDL_GameControllerUpdate _controllerUpdate;
            static dSDL_WasInit _wasInit;

            // Etat SDL memorise
            static IntPtr[] OpenJoysticks = new IntPtr[16];
            static short[,] LastJoystickAxes = new short[16, 8];
            static byte[] LastJoystickHats = new byte[16];
            static int[] JoystickAxisCount = new int[16];
            static int[] JoystickHatCount = new int[16];

            static IntPtr SafeGetProcAddress(string name)
            {
                if (_sdlLib == IntPtr.Zero) return IntPtr.Zero;
                return GetProcAddress(_sdlLib, name);
            }

            static T LoadDelegate<T>(string name) where T : class
            {
                IntPtr p = SafeGetProcAddress(name);
                if (p == IntPtr.Zero) return null;
                return (T)(object)Marshal.GetDelegateForFunctionPointer(p, typeof(T));
            }

            public static void TryInitialize()
            {
                if (_initTried) return;
                _initTried = true;

                // Chercher SDL2.dll dans le dossier de l'exe, puis dans le PATH
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = new string[] {
                    Path.Combine(exeDir, "SDL2.dll"),
                    Path.Combine(exeDir, "SDL3.dll") // SDL3 ok pour certaines fonctions compatibles (non garanti mais tente)
                };
                foreach (string path in candidates)
                {
                    if (File.Exists(path))
                    {
                        _sdlLib = LoadLibrary(path);
                        if (_sdlLib != IntPtr.Zero)
                        {
                            WriteLog($"[SDL] Loaded: {path}");
                            break;
                        }
                    }
                }
                if (_sdlLib == IntPtr.Zero)
                {
                    // Tenter via le PATH systeme
                    try { _sdlLib = LoadLibrary("SDL2.dll"); } catch { }
                }
                if (_sdlLib == IntPtr.Zero)
                {
                    return; // SDL non disponible - on continuera sans
                }

                _initSubSystem = LoadDelegate<dSDL_InitSubSystem>("SDL_InitSubSystem");
                _numJoysticks = LoadDelegate<dSDL_NumJoysticks>("SDL_NumJoysticks");
                _joystickOpen = LoadDelegate<dSDL_JoystickOpen>("SDL_JoystickOpen");
                _joystickClose = LoadDelegate<dSDL_JoystickClose>("SDL_JoystickClose");
                _joystickGetAxis = LoadDelegate<dSDL_JoystickGetAxis>("SDL_JoystickGetAxis");
                _joystickGetHat = LoadDelegate<dSDL_JoystickGetHat>("SDL_JoystickGetHat");
                _joystickGetButton = LoadDelegate<dSDL_JoystickGetButton>("SDL_JoystickGetButton");
                _joystickNumAxes = LoadDelegate<dSDL_JoystickNumAxes>("SDL_JoystickNumAxes");
                _joystickNumHats = LoadDelegate<dSDL_JoystickNumHats>("SDL_JoystickNumHats");
                _joystickNumButtons = LoadDelegate<dSDL_JoystickNumButtons>("SDL_JoystickNumButtons");
                _joystickUpdate = LoadDelegate<dSDL_JoystickUpdate>("SDL_JoystickUpdate");
                _controllerOpen = LoadDelegate<dSDL_GameControllerOpen>("SDL_GameControllerOpen");
                _controllerClose = LoadDelegate<dSDL_GameControllerClose>("SDL_GameControllerClose");
                _controllerGetAxis = LoadDelegate<dSDL_GameControllerGetAxis>("SDL_GameControllerGetAxis");
                _controllerGetButton = LoadDelegate<dSDL_GameControllerGetButton>("SDL_GameControllerGetButton");
                _controllerUpdate = LoadDelegate<dSDL_GameControllerUpdate>("SDL_GameControllerUpdate");
                _wasInit = LoadDelegate<dSDL_WasInit>("SDL_WasInit");

                if (_initSubSystem == null) return;
                try
                {
                    int r = _initSubSystem(SDL_INIT_JOYSTICK | SDL_INIT_GAMECONTROLLER);
                    if (r == 0)
                    {
                        _available = true;
                        WriteLog("[SDL] Joystick + gamecontroller subsystem initialized.");
                    }
                    else
                    {
                        WriteLog("[SDL] Subsystem initialization failed (code " + r + ").");
                    }
                }
                catch (Exception ex)
                {
                    WriteLog("[SDL] Init error: " + ex.Message);
                }
            }

            public static bool IsAvailable => _available;

            // Seuil SDL pour les axes analogiques (zone morte ~6% du max)
            const short SdlAxisDeadzone = 2000;

            public static bool CheckActivity()
            {
                if (!_available || _numJoysticks == null) return false;

                try
                {
                    if (_joystickUpdate != null) _joystickUpdate();

                    int count = _numJoysticks();
                    bool active = false;

                    for (int i = 0; i < Math.Min(count, 16); i++)
                    {
                        // Ouvrir le joystick si pas deja ouvert
                        if (OpenJoysticks[i] == IntPtr.Zero)
                        {
                            if (_joystickOpen != null)
                            {
                                OpenJoysticks[i] = _joystickOpen(i);
                                if (OpenJoysticks[i] != IntPtr.Zero)
                                {
                                    if (_joystickNumAxes != null) JoystickAxisCount[i] = _joystickNumAxes(OpenJoysticks[i]);
                                    if (_joystickNumHats != null) JoystickHatCount[i] = _joystickNumHats(OpenJoysticks[i]);
                                    WriteLog($"[SDL] Joystick {i} opened ({JoystickAxisCount[i]} axes, {JoystickHatCount[i]} HATs).");
                                }
                            }
                            continue;
                        }

                        IntPtr joy = OpenJoysticks[i];
                        if (joy == IntPtr.Zero) continue;

                        // Boutons
                        if (_joystickGetButton != null && _joystickNumButtons != null)
                        {
                            int nb = _joystickNumButtons(joy);
                            for (int b = 0; b < nb && b < 32; b++)
                            {
                                if (_joystickGetButton(joy, b) != 0)
                                {
                                    active = true;
                                    break;
                                }
                            }
                        }

                        // HAT (croix directionnelle) - variation = activite
                        if (_joystickGetHat != null && JoystickHatCount[i] > 0)
                        {
                            byte hat = _joystickGetHat(joy, 0);
                            if (hat != LastJoystickHats[i] && (hat != SDL_HAT_CENTERED || LastJoystickHats[i] != SDL_HAT_CENTERED))
                                active = true;
                            LastJoystickHats[i] = hat;
                        }

                        // Axes - variation superieure a un seuil = activite
                        if (_joystickGetAxis != null)
                        {
                            int nbAxes = JoystickAxisCount[i];
                            for (int a = 0; a < nbAxes && a < 8; a++)
                            {
                                short v = _joystickGetAxis(joy, a);
                                short prev = LastJoystickAxes[i, a];
                                if (Math.Abs((int)v - (int)prev) > SdlAxisDeadzone)
                                    active = true;
                                // Detection hors zone morte absolue (au cas ou la valeur de depart ne serait pas centree)
                                if (Math.Abs(v) > 12000)
                                    active = true;
                                LastJoystickAxes[i, a] = v;
                            }
                        }
                    }

                    return active;
                }
                catch
                {
                    return false;
                }
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);


        // ====================================================================
        // VARIABLES DE CONFIGURATION ET D'ETAT
        // ====================================================================
        // Active (true) ou desactive (false) l'Attract Mode. Quand false, le processus
        // reste en vie (utile car CreateStartScript le lance au demarrage d'ES) mais se
        // met en veille : aucune detection d'activite, aucun defilement, aucune touche
        // simulee. Rechargeable en live via config.ini a tout moment.
        static bool Enabled = true;
        static int GameDisplayDelay = 15;         // secondes
        static int InactivityTimeout = 60;       // secondes
        static int MaxGamesPerSystem = 3;
        static int MinScrollTicks = 2;
        static int MaxScrollTicks = 8;
        static int ScrollDelayMs = 80;
        static bool LogToFile = true;
        static string EnterKey = "X";
        static string ExitKey = "Z";
        static bool OnlyWhenFocused = true;
        static bool CreateStartScript = true;
        static bool ShowConsole = false;

        static string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "attract_mode_log.txt");
        static string SystemFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "system-selected.txt");
        static string GameFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "game-selected.txt");
        static string RunningFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "game-running.txt");
        // Fichiers sentinelles pilotés par les scripts RetroBat screensaver-start/stop
        static string ScreensaverStartFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screensaver-start.txt");
        static string ScreensaverStopFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screensaver-stop.txt");

        static Dictionary<int, XINPUT_STATE> LastGamepadStates = new Dictionary<int, XINPUT_STATE>();
        static Random Rand = new Random();

        // Variables d'état interne
        static bool IsAttractModeActive = false;
        static int GamesCountInCurrentSystem = 0;
        static string LastSelectedGameRom = "";
        static string LastSelectedSystem = "";

        // Suivi robuste de l'inactivité utilisateur
        static POINT LastMousePos = new POINT();
        static int UserInactivitySeconds = 0;

        // Rechargement live de config.ini (FileSystemWatcher + debounce)
        static string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
        static volatile bool ConfigDirty = false;
        static Timer _configDebounceTimer;
        static FileSystemWatcher _configWatcher;
        static DateTime _lastConfigLoadUtc = DateTime.MinValue;

        static Mutex mutex = new Mutex(true, "{RetroBatAttractMode-Instance}");

        static void Main(string[] args)
        {
            if (!mutex.WaitOne(TimeSpan.Zero, true))
            {
                return; // Une instance est déjà en cours d'exécution
            }

            // Migrer un config.ini existant pour lui ajouter la cle Enable si absente
            EnsureEnableKeyInConfig();

            // Chargement de la configuration INI si elle existe
            LoadConfig();

            // Activer la surveillance live des modifications de config.ini
            StartConfigWatcher();

            if (ShowConsole)
            {
                AllocConsole();
                Console.Title = "RetroBat Attract Mode Assistant";
            }

            WriteLog("Launching RetroBat Attract Mode Assistant x64...");

            // Tenter d'installer automatiquement les scripts d'intégration dans RetroBat
            AutoInstallScripts();

            WriteLog($"Configuration loaded : \n- Inactivity delay : {InactivityTimeout}s\n- Game display time : {GameDisplayDelay}s\n- Max games per system : {MaxGamesPerSystem}");

            // Initialiser les positions de départ et les manettes
            GetCursorPos(out LastMousePos);
            CheckGamepadActivity();

            // Démarrer le hook de clavier et de souris global dans un thread dédié
            Thread hookThread = new Thread(HookThread);
            hookThread.IsBackground = true;
            hookThread.Start();

            // S'assurer de nettoyer un éventuel fichier résiduel de jeu en cours
            if (File.Exists(RunningFile))
            {
                try { File.Delete(RunningFile); } catch { }
            }

            // Nettoyer d'éventuels fichiers sentinelles screensaver résiduels (démarrage a froid)
            if (File.Exists(ScreensaverStartFile))
            {
                try { File.Delete(ScreensaverStartFile); } catch { }
            }
            if (File.Exists(ScreensaverStopFile))
            {
                try { File.Delete(ScreensaverStopFile); } catch { }
            }

            while (true)
            {
                try
                {
                    // 0a. Rechargement live de config.ini si modifie (FileSystemWatcher)
                    if (ConfigDirty)
                    {
                        ConfigDirty = false;
                        LoadConfig();
                    }

                    // 0b. Mode veille : Enable=false. Le processus reste en vie mais
                    // ne reagit a aucun event. On peut le reactiver en live via config.ini.
                    if (!Enabled)
                    {
                        if (IsAttractModeActive)
                        {
                            WriteLog("Enable=false received: putting Attract Mode to sleep (standby, no events handled).");
                            IsAttractModeActive = false;
                            GamesCountInCurrentSystem = 0;
                        }
                        WriteConsole("[Standby] Enable=false. Attract Mode disabled in live config.");
                        Thread.Sleep(1000);
                        continue;
                    }

                    // 0. Vérifier si EmulationStation est toujours en cours d'exécution
                    if (Process.GetProcessesByName("emulationstation").Length == 0)
                    {
                        WriteLog("EmulationStation is closed. Stopping the assistant.");
                        break;
                    }

                    // 1. Vérifier si un jeu est en cours d'exécution via le fichier d'état
                    bool isGameRunning = File.Exists(RunningFile);

                    // 1b. Gestion des signaux screensaver emis par les scripts RetroBat
                    //     - screensaver-stop.txt present => fin de pause : on supprime
                    //       screensaver-start.txt puis screensaver-stop.txt (auto-nettoyage)
                    //     - screensaver-start.txt present (et pas de STOP en attente) =>
                    //       l'Attract Mode reste en pause tant qu'il est la.
                    bool isScreensaverActive = false;
                    if (File.Exists(ScreensaverStopFile))
                    {
                        if (IsAttractModeActive || UserInactivitySeconds > 0)
                        {
                            WriteLog("Signal screensaver-stop received: resuming Attract Mode cycle.");
                        }
                        IsAttractModeActive = false;
                        GamesCountInCurrentSystem = 0;
                        UserInactivitySeconds = 0;

                        // Supprimer d'abord le sentinel START (fin effective de la pause)
                        if (File.Exists(ScreensaverStartFile))
                        {
                            try { File.Delete(ScreensaverStartFile); } catch { }
                        }
                        // Puis auto-nettoyer le sentinel STOP
                        try { File.Delete(ScreensaverStopFile); } catch { }
                    }
                    else if (File.Exists(ScreensaverStartFile))
                    {
                        isScreensaverActive = true;
                        if (IsAttractModeActive)
                        {
                            WriteLog("Signal screensaver-start received: pausing Attract Mode (RetroBat screensaver).");
                            IsAttractModeActive = false;
                            GamesCountInCurrentSystem = 0;
                        }
                    }

                    // 2. Vérifier l'activité manette
                    bool controllerActive = CheckGamepadActivity();

                    // 3. Vérifier l'activité de la souris (changement de position physique réelle)
                    POINT currentMousePos;
                    bool mouseMoved = false;
                    if (GetCursorPos(out currentMousePos))
                    {
                        if (currentMousePos.X != LastMousePos.X || currentMousePos.Y != LastMousePos.Y)
                        {
                            mouseMoved = true;
                            LastMousePos = currentMousePos;
                        }
                    }

                    // 4. Vérifier l'activité du clavier et de la souris via les hooks globaux
                    bool keyboardActive = KeyboardActivityDetected;
                    if (keyboardActive)
                    {
                        KeyboardActivityDetected = false; // Réinitialiser le drapeau
                    }

                    bool mouseClicked = MouseActivityDetected;
                    if (mouseClicked)
                    {
                        MouseActivityDetected = false; // Réinitialiser le drapeau
                    }

                    // Calcul de l'état d'inactivité
                    if (isGameRunning || controllerActive || mouseMoved || keyboardActive || mouseClicked)
                    {
                        if (IsAttractModeActive || UserInactivitySeconds > 0)
                        {
                            if (isGameRunning)
                            {
                                WriteLog("A game has started! Immediately disabling Attract Mode.");
                            }
                            else if (controllerActive)
                            {
                                WriteLog("Controller activity detected! Stopping Attract Mode.");
                            }
                            else if (mouseMoved)
                            {
                                WriteLog("Physical mouse movement detected! Stopping Attract Mode.");
                            }
                            else if (keyboardActive)
                            {
                                WriteLog("Keyboard activity detected! Stopping Attract Mode.");
                            }
                            else if (mouseClicked)
                            {
                                WriteLog("Mouse click or scroll detected! Stopping Attract Mode.");
                            }

                            IsAttractModeActive = false;
                            GamesCountInCurrentSystem = 0;
                        }
                        UserInactivitySeconds = 0;
                    }
                    else
                    {
                        UserInactivitySeconds++;
                    }

                    bool isUserInactive = UserInactivitySeconds >= InactivityTimeout;

                    // 5. Check if EmulationStation is in foreground (if OnlyWhenFocused enabled)
                    if (OnlyWhenFocused && !IsEmulationStationFocused())
                    {
                        if (IsAttractModeActive)
                        {
                            WriteLog("EmulationStation no longer in foreground! Pausing Attract Mode (Standby Mode).");
                            IsAttractModeActive = false;
                            GamesCountInCurrentSystem = 0;
                        }
                        UserInactivitySeconds = 0;
                        WriteConsole($"[Standby] ES not in focus. Inactivity: {UserInactivitySeconds}/{InactivityTimeout}s");
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (isGameRunning)
                    {
                        // Security mode: a game is running under RetroBat
                        WriteConsole($"[Game running] Attract Mode paused. Controllers: {controllerActive}");
                        Thread.Sleep(1000);
                    }
                    else if (isScreensaverActive)
                    {
                        WriteConsole("[Screensaver] RetroBat screensaver active. Paused.");
                        Thread.Sleep(1000);
                    }
                    else if (isUserInactive)
                    {
                        if (!IsAttractModeActive)
                        {
                            WriteLog($"Inactivity detected ({UserInactivitySeconds}s). Activating Attract Mode...");
                            IsAttractModeActive = true;
                            GamesCountInCurrentSystem = 0;
                        }

                        ExecuteAttractModeCycle();
                    }
                    else
                    {
                        // Regular display in console during idle mode
                        string waitingContext = "";
                        if (CheckIfInsideGameList(out string currentSystem))
                        {
                            ReadSelectedGame(out string gameSys, out string selectedRom, out string selectedGameName);
                            waitingContext = $" | [In {currentSystem}] Game: {selectedGameName}";
                        }
                        else if (!string.IsNullOrEmpty(currentSystem))
                        {
                            waitingContext = $" | [In System Picker] System: {currentSystem}";
                        }

                        WriteConsole($"[Idle] Inactivity: {UserInactivitySeconds}/{InactivityTimeout}s{waitingContext} | Controllers: {controllerActive}");
                        Thread.Sleep(1000);
                    }
                }
                catch (Exception ex)
                {
                    WriteLog($"Error in main loop: {ex.Message}");
                    Thread.Sleep(2000);
                }
            }
        }

        // ====================================================================
        // COEURS DU MODE ATTRACT (LOGIQUE DE NAVIGATION)
        // ====================================================================
        static void ExecuteAttractModeCycle()
        {
            bool isInsideGameList = CheckIfInsideGameList(out string currentSystem);

            if (isInsideGameList)
            {
                ReadSelectedGame(out string gameSys, out string selectedRom, out string selectedGameName);
                WriteConsole($"[ATTRACT ACTIVE] Game list | System: {currentSystem} | Game: '{selectedGameName}'");
            }
            else
            {
                WriteConsole($"[ATTRACT ACTIVE] System picker | System: {currentSystem}");
            }

            if (!isInsideGameList)
            {
                // We are on the systems screen.
                WriteLog("Navigating through systems list...");

                // Fast random scroll
                ScrollRandomly();
                Thread.Sleep(1000); // Give RetroBat time to write the system file

                // Read the system we landed on
                string detectedSystem = ReadSelectedSystem();

                WriteLog($"System selected: '{detectedSystem}'. Entering the system...");
                ushort enterVk = GetVirtualKey(EnterKey, 0x58); // 0x58 = VK_X
                WriteLog($"Pressing entry key '{EnterKey}' (VK: 0x{enterVk:X2})...");
                SimulateKeyPress(enterVk);
                GamesCountInCurrentSystem = 0;
                LastSelectedSystem = detectedSystem;
                Thread.Sleep(2000); // Wait for the system to load the game list
            }
            else
            {
                // Nous sommes à l'intérieur de la liste des jeux d'un système.
                if (GamesCountInCurrentSystem >= MaxGamesPerSystem)
                {
                    WriteLog($"Game limit reached ({GamesCountInCurrentSystem}/{MaxGamesPerSystem}) for system '{currentSystem}'. Returning to systems.");
                    ushort exitVk = GetVirtualKey(ExitKey, 0x5A); // 0x5A = VK_Z
                    WriteLog($"Pressing exit key '{ExitKey}' (VK: 0x{exitVk:X2})...");
                    SimulateKeyPress(exitVk);
                    GamesCountInCurrentSystem = 0;
                    Thread.Sleep(2000);
                    return;
                }

                WriteLog($"Searching for a game in '{currentSystem}'... (Game {GamesCountInCurrentSystem + 1}/{MaxGamesPerSystem})");

                // Random scroll
                ScrollRandomly();
                Thread.Sleep(1200); // Wait for the selected game file to be updated

                // Detect the selected game to avoid duplicates
                string selectedRom = "";
                string selectedGameName = "";
                ReadSelectedGame(out string gameSys, out selectedRom, out selectedGameName);

                if (!string.IsNullOrEmpty(selectedRom) && selectedRom.Equals(LastSelectedGameRom, StringComparison.OrdinalIgnoreCase))
                {
                    WriteLog("Same game as previous detected. Shifting one step.");
                    SimulateScroll(Rand.Next(0, 2) == 0 ? 1 : -1);
                    Thread.Sleep(1000);
                    ReadSelectedGame(out gameSys, out selectedRom, out selectedGameName);
                }

                LastSelectedGameRom = selectedRom;
                GamesCountInCurrentSystem++;

                WriteLog($"Presenting game: '{selectedGameName}' ({currentSystem})");
                WriteLog($"Display delay started for {GameDisplayDelay} seconds...");

                // Réinitialiser les drapeaux d'activité pour l'attente passive
                KeyboardActivityDetected = false;
                MouseActivityDetected = false;
                GetCursorPos(out LastMousePos);

                // Attendre le délai configuré (par tranches d'une seconde pour pouvoir détecter
                // une éventuelle activité utilisateur entre-temps !)
                for (int s = 0; s < GameDisplayDelay; s++)
                {
                    Thread.Sleep(1000);

                    POINT checkMousePos;
                    bool realMouseMoved = false;
                    if (GetCursorPos(out checkMousePos))
                    {
                        if (checkMousePos.X != LastMousePos.X || checkMousePos.Y != LastMousePos.Y)
                        {
                            realMouseMoved = true;
                            LastMousePos = checkMousePos;
                        }
                    }

                    bool realKeyboardActive = KeyboardActivityDetected;
                    if (realKeyboardActive) KeyboardActivityDetected = false;

                    bool realMouseClicked = MouseActivityDetected;
                    if (realMouseClicked) MouseActivityDetected = false;

                    if (CheckGamepadActivity() || realMouseMoved || realKeyboardActive || realMouseClicked)
                    {
                        WriteLog("User activity detected during game display! Interrupted.");
                        return;
                    }
                    WriteConsole($"[Progress] '{selectedGameName}': {s + 1}/{GameDisplayDelay}s");
                }
            }
        }

        // ====================================================================
        // METHODES DE DETECTION DU CONTEXTE (LECTURE DE FICHIERS SCRIPTS)
        // ====================================================================
        static bool CheckIfInsideGameList(out string currentSystem)
        {
            currentSystem = "";
            if (!File.Exists(SystemFile)) return false;

            try
            {
                string sysName = File.ReadAllText(SystemFile).Trim();
                currentSystem = sysName;

                if (!File.Exists(GameFile)) return false;

                DateTime sysTime = File.GetLastWriteTime(SystemFile);
                DateTime gameTime = File.GetLastWriteTime(GameFile);

                // Si game-selected.txt est plus récent ou de date identique au system-selected.txt,
                // cela prouve que le dernier événement enregistré est un jeu et qu'on est entré dans un système.
                if (gameTime >= sysTime)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                // On exclusive access by RetroBat, we ignore temporarily
                WriteConsole($"[File locked]: {ex.Message}");
            }

            return false;
        }

        static string ReadSelectedSystem()
        {
            try
            {
                if (File.Exists(SystemFile))
                {
                    return File.ReadAllText(SystemFile).Trim();
                }
            }
            catch { }
            return "";
        }

        static void ReadSelectedGame(out string systemName, out string romPath, out string gameName)
        {
            systemName = "";
            romPath = "";
            gameName = "";

            try
            {
                if (File.Exists(GameFile))
                {
                    string content = File.ReadAllText(GameFile).Trim();
                    int firstSpace = content.IndexOf(' ');
                    if (firstSpace > 0)
                    {
                        systemName = content.Substring(0, firstSpace);
                        string remaining = content.Substring(firstSpace + 1);

                        int firstQuote = remaining.IndexOf('"');
                        if (firstQuote > 0)
                        {
                            romPath = remaining.Substring(0, firstQuote).Trim();
                            int lastQuote = remaining.LastIndexOf('"');
                            if (lastQuote > firstQuote)
                            {
                                gameName = remaining.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                            }
                            else
                            {
                                gameName = remaining.Substring(firstQuote).Replace("\"", "");
                            }
                        }
                        else
                        {
                            romPath = remaining;
                            gameName = Path.GetFileNameWithoutExtension(remaining);
                        }
                    }
                }
            }
            catch { }
        }

        // ====================================================================
        // EMULATION DE SCROLLING ET CLICS SOURIS VIA SENDINPUT
        // ====================================================================
        static void ScrollRandomly()
        {
            int direction = Rand.Next(0, 2) == 0 ? 1 : -1; // 1 = Haut, -1 = Bas
            int ticks = Rand.Next(MinScrollTicks, MaxScrollTicks + 1);

            WriteLog($"Scrolling simulation: {(direction == 1 ? "UP" : "DOWN")} {ticks} steps...");

            for (int i = 0; i < ticks; i++)
            {
                SimulateScroll(direction);
                Thread.Sleep(ScrollDelayMs);
            }
        }

        static void SimulateScroll(int clicks)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0] = new INPUT { type = INPUT_MOUSE };
            inputs[0].u.mi = new MOUSEINPUT
            {
                dx = 0,
                dy = 0,
                mouseData = unchecked((uint)(clicks * 120)), // 120 est le WHEEL_DELTA standard
                dwFlags = MOUSEEVENTF_WHEEL,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            };
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        static void SimulateLeftClick()
        {
            INPUT[] inputs = new INPUT[2];
            inputs[0] = new INPUT { type = INPUT_MOUSE };
            inputs[0].u.mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN, time = 0, dwExtraInfo = IntPtr.Zero };
            inputs[1] = new INPUT { type = INPUT_MOUSE };
            inputs[1].u.mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP, time = 0, dwExtraInfo = IntPtr.Zero };
            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        static void SimulateRightClick()
        {
            INPUT[] inputs = new INPUT[2];
            inputs[0] = new INPUT { type = INPUT_MOUSE };
            inputs[0].u.mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_RIGHTDOWN, time = 0, dwExtraInfo = IntPtr.Zero };
            inputs[1] = new INPUT { type = INPUT_MOUSE };
            inputs[1].u.mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_RIGHTUP, time = 0, dwExtraInfo = IntPtr.Zero };
            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        static void SimulateKeyPress(ushort vk)
        {
            ushort scanCode = (ushort)MapVirtualKey(vk, 0);
            INPUT[] inputs = new INPUT[2];
            inputs[0] = new INPUT { type = INPUT_KEYBOARD };
            inputs[0].u.ki = new KEYBDINPUT { wVk = vk, wScan = scanCode, dwFlags = KEYEVENTF_SCANCODE, time = 0, dwExtraInfo = IntPtr.Zero };
            inputs[1] = new INPUT { type = INPUT_KEYBOARD };
            inputs[1].u.ki = new KEYBDINPUT { wVk = vk, wScan = scanCode, dwFlags = KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP, time = 0, dwExtraInfo = IntPtr.Zero };
            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        static ushort GetVirtualKey(string keyName, ushort defaultVk)
        {
            if (string.IsNullOrEmpty(keyName)) return defaultVk;
            string upper = keyName.Trim().ToUpper();
            if (upper.Length == 1)
            {
                char c = upper[0];
                if (c >= 'A' && c <= 'Z') return (ushort)c;
                if (c >= '0' && c <= '9') return (ushort)c;
            }
            switch (upper)
            {
                case "ENTER":
                case "RETURN":
                    return 0x0D; // VK_RETURN
                case "ESCAPE":
                case "ESC":
                    return 0x1B; // VK_ESCAPE
                case "SPACE":
                    return 0x20; // VK_SPACE
                case "BACK":
                case "BACKSPACE":
                    return 0x08; // VK_BACK
                case "TAB":
                    return 0x09; // VK_TAB
                case "UP":
                    return 0x26; // VK_UP
                case "DOWN":
                    return 0x28; // VK_DOWN
                case "LEFT":
                    return 0x25; // VK_LEFT
                case "RIGHT":
                    return 0x27; // VK_RIGHT
                default:
                    if (upper.StartsWith("0X"))
                    {
                        try { return Convert.ToUInt16(upper.Substring(2), 16); } catch { }
                    }
                    else if (ushort.TryParse(upper, out ushort val))
                    {
                        return val;
                    }
                    break;
            }
            return defaultVk;
        }

        static bool IsEmulationStationFocused()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return false;

                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                if (pid == 0) return false;

                using (System.Diagnostics.Process proc = System.Diagnostics.Process.GetProcessById((int)pid))
                {
                    if (proc != null)
                    {
                        string procName = proc.ProcessName;
                        return procName.Equals("emulationstation", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch
            {
                // En cas d'erreur, on considère non-focused par sécurité
            }
            return false;
        }

        // ====================================================================
        // METHODES DE DETECTION D'IDLE ET LOGS
        // ====================================================================
        static bool _sdlInitDone = false;

        static bool CheckGamepadActivity()
        {
            bool active = false;

            // ----------- 1) XInput (manettes Xbox /Compatibles) -----------
            XINPUT_STATE state = new XINPUT_STATE();
            for (int i = 0; i < 4; i++)
            {
                if (XInputHelper.GetState(i, ref state))
                {
                    // Un bouton est presse
                    if (state.Gamepad.wButtons != 0) active = true;

                    // Les gachettes depassent le seuil de zone morte (abaisse a 20 pour plus de sensibilite)
                    if (state.Gamepad.bLeftTrigger > 20 || state.Gamepad.bRightTrigger > 20) active = true;

                    // Les joysticks depassent la zone morte (abaisse a 5000 pour detecter les petits mouvements)
                    if (Math.Abs(state.Gamepad.sThumbLX) > 5000 || Math.Abs(state.Gamepad.sThumbLY) > 5000) active = true;
                    if (Math.Abs(state.Gamepad.sThumbRX) > 5000 || Math.Abs(state.Gamepad.sThumbRY) > 5000) active = true;

                    // Comparaison avec l'etat precedent pour detecter un MOUVEMENT
                    // (delta abaisse a 500 pour les mouvements fins)
                    if (LastGamepadStates.TryGetValue(i, out var lastState))
                    {
                        if (state.dwPacketNumber != lastState.dwPacketNumber)
                        {
                            if (state.Gamepad.wButtons != lastState.Gamepad.wButtons ||
                                state.Gamepad.bLeftTrigger != lastState.Gamepad.bLeftTrigger ||
                                state.Gamepad.bRightTrigger != lastState.Gamepad.bRightTrigger ||
                                Math.Abs(state.Gamepad.sThumbLX - lastState.Gamepad.sThumbLX) > 500 ||
                                Math.Abs(state.Gamepad.sThumbLY - lastState.Gamepad.sThumbLY) > 500 ||
                                Math.Abs(state.Gamepad.sThumbRX - lastState.Gamepad.sThumbRX) > 500 ||
                                Math.Abs(state.Gamepad.sThumbRY - lastState.Gamepad.sThumbRY) > 500)
                            {
                                active = true;
                            }
                        }
                    }
                    LastGamepadStates[i] = state;
                }
            }

            // ----------- 2) winmm joystick (DirectInput & manettes generiques) -----------
            // Couvre 16 joysticks - la majorite des manettes NON-XInput.
            try
            {
                if (CheckWinmmJoystickActivity()) active = true;
            }
            catch (Exception ex)
            {
                // On ne doit pas planter la boucle principale a cause d'une erreur winmm
                WriteConsole($"[winmm] error: {ex.Message}");
            }

            // ----------- 3) SDL2 (couverture maximale si SDL2.dll present) -----------
            // Initialisation lazy une seule fois.
            if (!_sdlInitDone)
            {
                _sdlInitDone = true;
                try { SdlGamepadHelper.TryInitialize(); } catch { }
            }
            if (SdlGamepadHelper.IsAvailable)
            {
                try
                {
                    if (SdlGamepadHelper.CheckActivity()) active = true;
                }
                catch (Exception ex)
                {
                    WriteConsole($"[SDL] error: {ex.Message}");
                }
            }

            return active;
        }

        static void WriteLog(string message)
        {
            string formattedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

            // Ecriture directe via Win32 WriteConsole pour garantir l'affichage
            // immediat (le flux peut rester sinon dans le buffer Console.Out).
            WriteConsoleDirect(formattedMessage);

            if (LogToFile)
            {
                try
                {
                    File.AppendAllText(LogPath, formattedMessage + Environment.NewLine);
                }
                catch { }
            }
        }

        static void WriteConsole(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            WriteConsoleDirect(line);
        }

        static void LoadConfig()
        {
            if (!File.Exists(ConfigPath))
            {
                SaveDefaultConfig(ConfigPath);
                return;
            }

            // Snapshot avant pour differ
            bool oldEnabled = Enabled;
            int oldGameDisplayDelay = GameDisplayDelay;
            int oldInactivityTimeout = InactivityTimeout;
            int oldMaxGamesPerSystem = MaxGamesPerSystem;
            int oldMinScrollTicks = MinScrollTicks;
            int oldMaxScrollTicks = MaxScrollTicks;
            int oldScrollDelayMs = ScrollDelayMs;
            bool oldLogToFile = LogToFile;
            string oldEnterKey = EnterKey;
            string oldExitKey = ExitKey;
            bool oldOnlyWhenFocused = OnlyWhenFocused;
            bool oldCreateStartScript = CreateStartScript;
            bool oldShowConsole = ShowConsole;

            bool isInitialLoad = (_lastConfigLoadUtc == DateTime.MinValue);

            try
            {
                string[] lines = File.ReadAllLines(ConfigPath);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#") || trimmed.StartsWith("["))
                        continue;

                    int equalIndex = trimmed.IndexOf('=');
                    if (equalIndex > 0)
                    {
                        string key = trimmed.Substring(0, equalIndex).Trim().ToLower();
                        string val = trimmed.Substring(equalIndex + 1).Trim();

                        switch (key)
                        {
                            case "enable":
                                Enabled = val.Equals("true", StringComparison.OrdinalIgnoreCase);
                                break;
                            case "gamedisplaydelay":
                                int.TryParse(val, out GameDisplayDelay);
                                break;
                            case "inactivitytimeout":
                                int.TryParse(val, out InactivityTimeout);
                                break;

                            case "maxgamespersystem":
                                int.TryParse(val, out MaxGamesPerSystem);
                                break;
                            case "minscrollticks":
                                int.TryParse(val, out MinScrollTicks);
                                break;
                            case "maxscrollticks":
                                int.TryParse(val, out MaxScrollTicks);
                                break;
                            case "scrolldelayms":
                                int.TryParse(val, out ScrollDelayMs);
                                break;
                            case "logtofile":
                                LogToFile = val.Equals("true", StringComparison.OrdinalIgnoreCase);
                                break;
                            case "enterkey":
                                EnterKey = val;
                                break;
                            case "exitkey":
                                ExitKey = val;
                                break;
                            case "onlywhenfocused":
                                OnlyWhenFocused = val.Equals("true", StringComparison.OrdinalIgnoreCase);
                                break;
                            case "createstartscript":
                                CreateStartScript = val.Equals("true", StringComparison.OrdinalIgnoreCase);
                                break;
                            case "showconsole":
                                ShowConsole = val.Equals("true", StringComparison.OrdinalIgnoreCase);
                                break;
                        }
                    }
                }

                _lastConfigLoadUtc = DateTime.UtcNow;

                if (!isInitialLoad)
                {
                    // Loguer le diff de configuration applique en live
                    var changes = new List<string>();
                    if (Enabled != oldEnabled) changes.Add($"Enabled={oldEnabled}->{Enabled}");
                    if (GameDisplayDelay != oldGameDisplayDelay) changes.Add($"GameDisplayDelay={oldGameDisplayDelay}->{GameDisplayDelay}");
                    if (InactivityTimeout != oldInactivityTimeout) changes.Add($"InactivityTimeout={oldInactivityTimeout}->{InactivityTimeout}");
                    if (MaxGamesPerSystem != oldMaxGamesPerSystem) changes.Add($"MaxGamesPerSystem={oldMaxGamesPerSystem}->{MaxGamesPerSystem}");
                    if (MinScrollTicks != oldMinScrollTicks) changes.Add($"MinScrollTicks={oldMinScrollTicks}->{MinScrollTicks}");
                    if (MaxScrollTicks != oldMaxScrollTicks) changes.Add($"MaxScrollTicks={oldMaxScrollTicks}->{MaxScrollTicks}");
                    if (ScrollDelayMs != oldScrollDelayMs) changes.Add($"ScrollDelayMs={oldScrollDelayMs}->{ScrollDelayMs}");
                    if (LogToFile != oldLogToFile) changes.Add($"LogToFile={oldLogToFile}->{LogToFile}");
                    if (!string.Equals(EnterKey, oldEnterKey, StringComparison.OrdinalIgnoreCase)) changes.Add($"EnterKey={oldEnterKey}->{EnterKey}");
                    if (!string.Equals(ExitKey, oldExitKey, StringComparison.OrdinalIgnoreCase)) changes.Add($"ExitKey={oldExitKey}->{ExitKey}");
                    if (OnlyWhenFocused != oldOnlyWhenFocused) changes.Add($"OnlyWhenFocused={oldOnlyWhenFocused}->{OnlyWhenFocused}");
                    if (CreateStartScript != oldCreateStartScript) changes.Add($"CreateStartScript={oldCreateStartScript}->{CreateStartScript}");
                    if (ShowConsole != oldShowConsole) changes.Add($"ShowConsole={oldShowConsole}->{ShowConsole} (non effectif en live)");

                    if (changes.Count > 0)
                    {
                        WriteLog("[Config live] Changes applied: " + string.Join(", ", changes));
                    }
                    else
                    {
                        WriteLog("[Config live] Config.ini reloaded: no keys changed.");
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog($"Unable to read configuration (using defaults): {ex.Message}");
            }
        }

        // ====================================================================
        // SURVEILLANCE LIVE DE config.ini (FileSystemWatcher + debounce)
        // ====================================================================
        static void StartConfigWatcher()
        {
            try
            {
                _configWatcher = new FileSystemWatcher();
                _configWatcher.Path = AppDomain.CurrentDomain.BaseDirectory;
                _configWatcher.Filter = "config.ini";
                _configWatcher.IncludeSubdirectories = false;
                _configWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime;

                FileSystemEventHandler onChanged = (s, e) => ArmConfigReload();
                RenamedEventHandler onRenamed = (s, e) =>
                {
                    // Un fichier renomme EN config.ini doit declencher un rechargement
                    if (!string.IsNullOrEmpty(e.Name) &&
                        e.Name.Equals("config.ini", StringComparison.OrdinalIgnoreCase))
                    {
                        ArmConfigReload();
                    }
                };

                _configWatcher.Changed += onChanged;
                _configWatcher.Created += onChanged;
                _configWatcher.Renamed += onRenamed;
                _configWatcher.Error += (s, e) =>
                {
                    WriteLog($"[Config watcher] Error: {e.GetException().Message}. Will retry on next pass.");
                    // Re-armement defensif
                    try { _configWatcher.EnableRaisingEvents = true; } catch { }
                };

                _configWatcher.EnableRaisingEvents = true;
                WriteLog("[Config watcher] Live config.ini monitoring enabled.");
            }
            catch (Exception ex)
            {
                WriteLog($"[Config watcher] Could not enable live monitoring: {ex.Message}");
                _configWatcher = null;
            }
        }

        static void ArmConfigReload()
        {
            // Debounce : si l'editeur ecrit le fichier en plusieurs passes
            // (sauvegarde atomique, swap, etc.), on attend 500ms de stabilite.
            try
            {
                if (_configDebounceTimer != null)
                {
                    _configDebounceTimer.Change(500, Timeout.Infinite);
                }
                else
                {
                    _configDebounceTimer = new Timer(_ => { ConfigDirty = true; }, null, 500, Timeout.Infinite);
                }
            }
            catch
            {
                ConfigDirty = true;
            }
        }

        static void SaveDefaultConfig(string path)
        {
            try
            {
                string defaultIni = @"; ====================================================================
; RETROBAT ATTRACT MODE CONFIGURATION FILE
; ====================================================================

[Settings]

; Enable (true) or disable (false) Attract Mode.
; Reloaded live: no restart needed to toggle.
Enable=true

; Seconds to stay on a game to play its video
GameDisplayDelay=" + GameDisplayDelay + @"

; Seconds of inactivity before Attract Mode starts
InactivityTimeout=" + InactivityTimeout + @"

; Max games shown per system before going back to the system list
MaxGamesPerSystem=" + MaxGamesPerSystem + @"

; Scroll simulation: min/max wheel ticks per scroll, ms between ticks
MinScrollTicks=" + MinScrollTicks + @"
MaxScrollTicks=" + MaxScrollTicks + @"
ScrollDelayMs=" + ScrollDelayMs + @"

; Log actions to attract_mode_log.txt
LogToFile=" + (LogToFile ? "true" : "false") + @"

; Key to enter a system (default: X). Single letters or special keys
; (ENTER, ESCAPE, SPACE, BACK, TAB, UP, DOWN, LEFT, RIGHT) or hex (0x0D)
EnterKey=" + EnterKey + @"

; Key to exit a system (default: Z)
ExitKey=" + ExitKey + @"

; Pause Attract Mode if EmulationStation is not in foreground (default: true)
OnlyWhenFocused=" + (OnlyWhenFocused ? "true" : "false") + @"

; Create an auto-start script in retrobat/scripts/start to launch with ES (default: false)
CreateStartScript=" + (CreateStartScript ? "true" : "false") + @"

; Show a console for live logs (default: false)
ShowConsole=" + (ShowConsole ? "true" : "false") + @"
";
                File.WriteAllText(path, defaultIni);
                WriteLog("Default config.ini file generated successfully.");
            }
            catch { }
        }

        // ====================================================================
        // MIGRATION : Insere la cle Enable en tete d'un config.ini existant
        // si elle est absente. Preserve tout le contenu original.
        // ====================================================================
        static void EnsureEnableKeyInConfig()
        {
            if (!File.Exists(ConfigPath)) return;

            try
            {
                string content = File.ReadAllText(ConfigPath);

                // Extraire toutes les valeurs existantes du fichier en attendant
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                foreach (string ln in lines)
                {
                    string t = ln.Trim();
                    if (t.StartsWith(";") || t.StartsWith("#") || t.StartsWith("[")) continue;
                    int eq = t.IndexOf('=');
                    if (eq > 0)
                    {
                        string k = t.Substring(0, eq).Trim();
                        string v = t.Substring(eq + 1).Trim();
                        values[k] = v;
                    }
                }

                bool hasEnable = values.ContainsKey("Enable");
                bool needsRegen = !hasEnable;

                if (hasEnable)
                {
                    // Verifier que les commentaires sont presents : au moins une ligne
                    // de commentaire directement avant une cle (pas juste l'en-tete).
                    bool hasComments = false;
                    for (int i = 0; i < lines.Length - 1; i++)
                    {
                        string cur = lines[i].Trim();
                        string next = lines[i + 1].Trim();
                        if (cur.StartsWith(";") && !next.StartsWith(";") && !next.StartsWith("[") && !string.IsNullOrEmpty(next))
                        {
                            hasComments = true;
                            break;
                        }
                    }
                    if (!hasComments) needsRegen = true;
                }

                if (!needsRegen) return;

                // Regenerer avec commentaires modernes en conservant les valeurs
                // personnalisees de l'utilisateur (fallback aux defauts si absent).
                string GetVal(string key, string fallback)
                {
                    return values.TryGetValue(key, out string v) ? v : fallback;
                }

                string regenerated = @"; ====================================================================
; RETROBAT ATTRACT MODE CONFIGURATION FILE
; Save as ""config.ini"" in the same folder as RetroBatAttractMode.exe
; ====================================================================

[Settings]

; Enable (true) or disable (false) Attract Mode.
; Reloaded live: no restart needed to toggle.
Enable=" + GetVal("Enable", "true") + @"

; Seconds to stay on a game to play its video
GameDisplayDelay=" + GetVal("GameDisplayDelay", "15") + @"

; Seconds of inactivity before Attract Mode starts
InactivityTimeout=" + GetVal("InactivityTimeout", "60") + @"

; Max games shown per system before going back to the system list
MaxGamesPerSystem=" + GetVal("MaxGamesPerSystem", "3") + @"

; Scroll simulation: min/max wheel ticks per scroll, ms between ticks
MinScrollTicks=" + GetVal("MinScrollTicks", "2") + @"
MaxScrollTicks=" + GetVal("MaxScrollTicks", "8") + @"
ScrollDelayMs=" + GetVal("ScrollDelayMs", "80") + @"

; Log actions to attract_mode_log.txt
LogToFile=" + GetVal("LogToFile", "true") + @"

; Key to enter a system (default: X). Single letters or special keys
; (ENTER, ESCAPE, SPACE, BACK, TAB, UP, DOWN, LEFT, RIGHT) or hex (0x0D)
EnterKey=" + GetVal("EnterKey", "X") + @"

; Key to exit a system (default: Z)
ExitKey=" + GetVal("ExitKey", "Z") + @"

; Pause Attract Mode if EmulationStation is not in foreground (default: true)
OnlyWhenFocused=" + GetVal("OnlyWhenFocused", "true") + @"

; Create an auto-start script in retrobat/scripts/start to launch with ES (default: false)
CreateStartScript=" + GetVal("CreateStartScript", "false") + @"

; Show a console for live logs (default: false)
ShowConsole=" + GetVal("ShowConsole", "false") + @"
";

                File.WriteAllText(ConfigPath, regenerated);
                WriteLog("[Config migration] Config.ini regenerated with comments (user values preserved).");
            }
            catch (Exception ex)
            {
                WriteLog($"[Config migration] Could not migrate config.ini: {ex.Message}");
            }
        }

       static void AutoInstallScripts()
        {
            try
            {
                string retroBatPath = null;
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\RetroBat"))
                {
                    if (key != null)
                    {
                        retroBatPath = key.GetValue("LatestKnownInstallPath")?.ToString();
                    }
                }

                if (string.IsNullOrEmpty(retroBatPath))
                {
                    WriteLog("RetroBat installation folder not found in registry (HKCU\\Software\\RetroBat\\LatestKnownInstallPath).");
                    return;
                }

                WriteLog("RetroBat folder detected: " + retroBatPath);

                string scriptsRoot = Path.Combine(retroBatPath, @"emulationstation\.emulationstation\scripts");
                if (!Directory.Exists(scriptsRoot))
                {
                    Directory.CreateDirectory(scriptsRoot);
                }

                string myDir = AppDomain.CurrentDomain.BaseDirectory;
                if (!myDir.EndsWith("\\")) myDir += "\\";

                // 1. system-selected
                string systemSelectedDir = Path.Combine(scriptsRoot, "system-selected");
                Directory.CreateDirectory(systemSelectedDir);
                string systemBatPath = Path.Combine(systemSelectedDir, "AttractMode-system-selected.bat");
                string[] systemLines = new string[] {
                    "@echo off",
                    ":: This script saves the system you are currently browsing in RetroBat.",
                    ":: %1 is the argument containing the system ID (e.g., snes, neogeo...)",
                    "> \"" + myDir + "system-selected.txt\" echo %1"
                };
                File.WriteAllLines(systemBatPath, systemLines);
                WriteLog("system-selected script installed: " + systemBatPath);

                // 2. game-selected
                string gameSelectedDir = Path.Combine(scriptsRoot, "game-selected");
                Directory.CreateDirectory(gameSelectedDir);
                string gameBatPath = Path.Combine(gameSelectedDir, "AttractMode-game-selected.bat");
                string[] gameLines = new string[] {
                    "@echo off",
                    ":: This script saves the game you are currently highlighted on in RetroBat.",
                    ":: %1 = system, %2 = rom path, %3 = game title",
                    "",
                    "> \"" + myDir + "game-selected.txt\" echo %1 %2 \"%~3\""
                };
                File.WriteAllLines(gameBatPath, gameLines);
                WriteLog("game-selected script installed: " + gameBatPath);

                // 3. game-start
                string gameStartDir = Path.Combine(scriptsRoot, "game-start");
                Directory.CreateDirectory(gameStartDir);
                string startBatPath = Path.Combine(gameStartDir, "AttractMode-start-game.bat");
                string[] startLines = new string[] {
                    "@echo off",
                    ":: This script tells the C# assistant that a game has started.",
                    ":: This instantly disables Attract Mode so it doesn't interrupt the gameplay.",
                    ":: %1 = system, %2 = rom path, %3 = game title",
                    "> \"" + myDir + "game-running.txt\" echo %1 %2 %3"
                };
                File.WriteAllLines(startBatPath, startLines);
                WriteLog("game-start script installed: " + startBatPath);

                // 4. game-end
                string gameEndDir = Path.Combine(scriptsRoot, "game-end");
                Directory.CreateDirectory(gameEndDir);
                string endBatPath = Path.Combine(gameEndDir, "AttractMode-end-game.bat");
                string[] endLines = new string[] {
                    "@echo off",
                    ":: This script runs when the game closes to allow Attract Mode to run again.",
                    "if exist \"" + myDir + "game-running.txt\" (",
                    "    del \"" + myDir + "game-running.txt\"",
                    ")"
                };
                File.WriteAllLines(endBatPath, endLines);
                WriteLog("game-end script installed: " + endBatPath);

                // 5. screensaver-start (RetroBat starts its screensaver)
                string screensaverStartDir = Path.Combine(scriptsRoot, "screensaver-start");
                Directory.CreateDirectory(screensaverStartDir);
                string screensaverStartBatPath = Path.Combine(screensaverStartDir, "AttractMode-screensaver-start.bat");
                string[] screensaverStartLines = new string[] {
                    "@echo off",
                    ":: This script is called by EmulationStation when its screensaver starts.",
                    ":: It pauses the Attract Mode assistant.",
                    "echo screensaver-start > \"" + myDir + "screensaver-start.txt\""
                };
                File.WriteAllLines(screensaverStartBatPath, screensaverStartLines);
                WriteLog("screensaver-start script installed: " + screensaverStartBatPath);

                // 6. screensaver-stop (RetroBat stops its screensaver)
                string screensaverStopDir = Path.Combine(scriptsRoot, "screensaver-stop");
                Directory.CreateDirectory(screensaverStopDir);
                string screensaverStopBatPath = Path.Combine(screensaverStopDir, "AttractMode-screensaver-stop.bat");
                string[] screensaverStopLines = new string[] {
                    "@echo off",
                    ":: This script is called by EmulationStation when its screensaver stops.",
                    ":: It resumes the Attract Mode assistant by requesting the cleanup of the START sentinel.",
                    "echo screensaver-stop > \"" + myDir + "screensaver-stop.txt\""
                };
                File.WriteAllLines(screensaverStopBatPath, screensaverStopLines);
                WriteLog("screensaver-stop script installed: " + screensaverStopBatPath);

                // 7. start (Optionnel)
                string startDir = Path.Combine(scriptsRoot, "start");
                if (CreateStartScript)
                {
                    Directory.CreateDirectory(startDir);
                    string attractBatPath = Path.Combine(startDir, "AttractMode.bat");
                    string exePath = Path.Combine(myDir, "AttractMode.exe");
                    string[] attractLines = new string[] {
                        "@echo off",
                        "start \"\" \"" + exePath + "\""
                    };
                    File.WriteAllLines(attractBatPath, attractLines);
                    WriteLog("Auto-start script installed: " + attractBatPath);
                }
                else
                {
                    if (!Directory.Exists(startDir)) Directory.CreateDirectory(startDir);
                }

                // S'assurer que les dossiers de base existent (pour en avoir 5 au total si besoin)
                string quitDir = Path.Combine(scriptsRoot, "quit");
                if (!Directory.Exists(quitDir)) Directory.CreateDirectory(quitDir);

                WriteLog("All RetroBat integration scripts installed/updated successfully!");
            }
            catch (Exception ex)
            {
                WriteLog("Error during RetroBat script auto-install: " + ex.Message);
            }
        }
    }
}
