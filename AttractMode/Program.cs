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
        // DIRECTINPUT API - DETECTION DES VOLANTS ET PERIPHERIQUES DE JEU
        // ====================================================================
        [StructLayout(LayoutKind.Sequential)]
        public struct DIDEVICEINSTANCE
        {
            public uint dwSize;
            public uint dwDevType;
            public Guid guidInstance;
            public Guid guidProduct;
            public uint dwDevTypeFF;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string tszProductName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string tszInstanceName;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DIJOYSTATE
        {
            public long lX;
            public long lY;
            public long lZ;
            public long lRx;
            public long lRy;
            public long lRz;
            public int rglSlider0;
            public int rglSlider1;
            public uint rgdwPOV0;
            public uint rgdwPOV1;
            public uint rgdwPOV2;
            public uint rgdwPOV3;
            public uint rgbButtons0;
            public uint rgbButtons1;
            public uint rgbButtons2;
            public uint rgbButtons3;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DIJOYSTATE2
        {
            public long lX;
            public long lY;
            public long lZ;
            public long lRx;
            public long lRy;
            public long lRz;
            public int rglSlider0;
            public int rglSlider1;
            public int rglSlider2;
            public int rglSlider3;
            public uint rgdwPOV0;
            public uint rgdwPOV1;
            public uint rgdwPOV2;
            public uint rgdwPOV3;
            public uint rgbButtons0;
            public uint rgbButtons1;
            public uint rgbButtons2;
            public uint rgbButtons3;
            public uint rgbButtons4;
            public uint rgbButtons5;
            public uint rgbButtons6;
            public uint rgbButtons7;
            public long lVX;
            public long lVY;
            public long lVZ;
            public long lVRx;
            public long lVRy;
            public long lVRz;
            public int rglVSlider0;
            public int rglVSlider1;
            public int rglVSlider2;
            public int rglVSlider3;
            public long lAX;
            public long lAY;
            public long lAZ;
            public long lARx;
            public long lARy;
            public long lARz;
            public int rglASlider0;
            public int rglASlider1;
            public int rglASlider2;
            public int rglASlider3;
            public long lFX;
            public long lFY;
            public long lFZ;
            public long lFRx;
            public long lFRy;
            public long lFRz;
            public int rglFSlider0;
            public int rglFSlider1;
            public int rglFSlider2;
            public int rglFSlider3;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DIPROPDWORD
        {
            public uint dwSize;
            public uint dwHeaderSize;
            public uint dwObj;
            public uint dwHow;
            public uint dwData;
        }

        const uint DI8_2BUTTONS = 0x02;
        const uint DIJOFS_X = 0;
        const uint DIJOFS_Y = 4;
        const uint DIJOFS_Z = 8;
        const uint DIJOFS_RX = 12;
        const uint DIJOFS_RY = 16;
        const uint DIJOFS_RZ = 20;
        const uint DIJOFS_SLIDER0 = 24;
        const uint DIJOFS_SLIDER1 = 28;
        const uint DIJOFS_POV0 = 32;
        const uint DIJOFS_POV1 = 36;
        const uint DIJOFS_POV2 = 40;
        const uint DIJOFS_POV3 = 44;
        const uint DIJOFS_BUTTON0 = 48;

        const uint DIDFT_ALL = 0x00000000;
        const uint DI8DEVCLASS_GAMECTRL = 0x05;
        const uint DI8DEVTYPE_JOYSTICK = 0x02;
        const uint DI8DEVTYPE_GAMEPAD = 0x03;
        const uint DI8DEVTYPE_DRIVING = 0x04;
        const uint DI8DEVTYPE_FLIGHT = 0x05;
        const uint DIEDFL_ALLDEVICES = 0x00000000;
        const uint DIEDFL_ATTACHEDONLY = 0x00000001;

        delegate bool DIEnumDevicesCallback(ref DIDEVICEINSTANCE lpddi, IntPtr pvRef);

        [DllImport("dinput8.dll", SetLastError = true)]
        static extern int DirectInput8Create(IntPtr hinst, uint dwVersion, ref Guid riidltf, out IntPtr ppvOut, IntPtr punkOuter);

        static Guid IID_IDirectInput8 = new Guid("BF798031-483A-4DA2-BB60-ED5F22317914");

        [DllImport("dinput8.dll", SetLastError = true)]
        static extern int IDirectInput8_EnumDevices(IntPtr pInstance, uint dwDevType, DIEnumDevicesCallback lpCallback, IntPtr pvRef, uint dwFlags);

        [DllImport("dinput8.dll", SetLastError = true)]
        static extern int IDirectInput8_CreateDevice(IntPtr pInstance, Guid rguid, out IntPtr ppDevice, IntPtr pUnkOuter);

        [DllImport("dinput8.dll", SetLastError = true)]
        static extern int IDirectInputDevice8_SetDataFormat(IntPtr pDevice, IntPtr lpdf);

        [DllImport("dinput8.dll", SetLastError = true)]
        static extern int IDirectInputDevice8_Acquire(IntPtr pDevice);

        [DllImport("dinput8.dll", SetLastError = true)]
        static extern int IDirectInputDevice8_Poll(IntPtr pDevice);

        [DllImport("dinput8.dll", SetLastError = true)]
        static extern int IDirectInputDevice8_GetDeviceState(IntPtr pDevice, uint cbData, IntPtr lpvData);

        [DllImport("dinput8.dll", SetLastError = true)]
        static extern int IDirectInputDevice8_Unacquire(IntPtr pDevice);

        [DllImport("dinput8.dll", SetLastError = true)]
        static extern int IDirectInputDevice8_Release(IntPtr pDevice);

        [DllImport("dinput8.dll", SetLastError = true)]
        static extern int IDirectInput8_Release(IntPtr pInstance);

        private static IntPtr _directInput = IntPtr.Zero;
        private static List<IntPtr> _directInputDevices = new List<IntPtr>();
        private static List<DIDEVICEINSTANCE> _directInputDeviceInstances = new List<DIDEVICEINSTANCE>();
        private static bool _directInputInitialized = false;
        private static DateTime _lastDirectInputScan = DateTime.MinValue;

        static bool InitializeDirectInput()
        {
            try
            {
                IntPtr hInstance = GetModuleHandle(null);
                
                // Essayer DirectInput8Create avec IDirectInput8 interface
                int hr = DirectInput8Create(hInstance, 0x0800, ref IID_IDirectInput8, out _directInput, IntPtr.Zero);
                
                if (hr != 0 || _directInput == IntPtr.Zero)
                {
                    WriteLog($"[DirectInput] DirectInput8Create failed with HRESULT: 0x{hr:X8}");
                    WriteLog("[DirectInput] Note: DirectInput may not be available on modern Windows. Consider using XInput or SDL2 instead.");
                    return false;
                }

                WriteLog("[DirectInput] Initialized successfully");

                // Enumerate all game controllers
                DIEnumDevicesCallback callback = EnumDevicesCallback;
                hr = IDirectInput8_EnumDevices(_directInput, DI8DEVCLASS_GAMECTRL, callback, IntPtr.Zero, DIEDFL_ATTACHEDONLY);

                if (hr != 0)
                {
                    WriteLog($"[DirectInput] EnumDevices failed with HRESULT: 0x{hr:X8}");
                }

                _directInputInitialized = true;
                return true;
            }
            catch (DllNotFoundException)
            {
                WriteLog("[DirectInput] dinput8.dll not found - DirectInput not available on this system");
                return false;
            }
            catch (Exception ex)
            {
                WriteLog($"[DirectInput] Initialization error: {ex.Message}");
                return false;
            }
        }

        static bool EnumDevicesCallback(ref DIDEVICEINSTANCE lpddi, IntPtr pvRef)
        {
            try
            {
                _directInputDeviceInstances.Add(lpddi);
                WriteLog($"[DirectInput] Device found: '{lpddi.tszProductName}' - Type: 0x{lpddi.dwDevType:X8}");

                // Create device
                int hr = IDirectInput8_CreateDevice(_directInput, lpddi.guidInstance, out IntPtr device, IntPtr.Zero);
                if (hr == 0 && device != IntPtr.Zero)
                {
                    _directInputDevices.Add(device);
                    WriteLog($"[DirectInput] Device created successfully for '{lpddi.tszProductName}'");
                }
                else
                {
                    WriteLog($"[DirectInput] Failed to create device for '{lpddi.tszProductName}': 0x{hr:X8}");
                }
            }
            catch (Exception ex)
            {
                WriteLog($"[DirectInput] Callback error: {ex.Message}");
            }
            return true; // Continue enumeration
        }

        static bool CheckDirectInputActivity()
        {
            if (!_directInputInitialized)
            {
                if ((DateTime.Now - _lastDirectInputScan).TotalSeconds > 30)
                {
                    _lastDirectInputScan = DateTime.Now;
                    InitializeDirectInput();
                }
                return false;
            }

            bool active = false;

            for (int i = 0; i < _directInputDevices.Count; i++)
            {
                IntPtr device = _directInputDevices[i];
                try
                {
                    // Try to poll the device
                    int hr = IDirectInputDevice8_Poll(device);
                    
                    if (hr == 0)
                    {
                        // Get device state
                        DIJOYSTATE2 state = new DIJOYSTATE2();
                        int stateSize = Marshal.SizeOf(typeof(DIJOYSTATE2));
                        IntPtr statePtr = Marshal.AllocHGlobal(stateSize);
                        
                        try
                        {
                            Marshal.StructureToPtr(state, statePtr, false);
                            hr = IDirectInputDevice8_GetDeviceState(device, (uint)stateSize, statePtr);
                            
                            if (hr == 0)
                            {
                                state = Marshal.PtrToStructure<DIJOYSTATE2>(statePtr);

                                // Check buttons (128 buttons max in DIJOYSTATE2)
                                for (int b = 0; b < 128; b++)
                                {
                                    int buttonIndex = b / 32;
                                    int buttonBit = b % 32;
                                    if (buttonIndex < 8)
                                    {
                                        uint buttonValue = (buttonIndex == 0) ? state.rgbButtons0 :
                                                          (buttonIndex == 1) ? state.rgbButtons1 :
                                                          (buttonIndex == 2) ? state.rgbButtons2 :
                                                          (buttonIndex == 3) ? state.rgbButtons3 :
                                                          (buttonIndex == 4) ? state.rgbButtons4 :
                                                          (buttonIndex == 5) ? state.rgbButtons5 :
                                                          (buttonIndex == 6) ? state.rgbButtons6 : state.rgbButtons7;
                                        
                                        if ((buttonValue & (1u << buttonBit)) != 0)
                                        {
                                            active = true;
                                            break;
                                        }
                                    }
                                }

                                // Check axes (with reduced threshold for wheels)
                                if (!active)
                                {
                                    if (Math.Abs(state.lX) > 500) active = true;
                                    if (Math.Abs(state.lY) > 500) active = true;
                                    if (Math.Abs(state.lZ) > 500) active = true;
                                    if (Math.Abs(state.lRx) > 500) active = true;
                                    if (Math.Abs(state.lRy) > 500) active = true;
                                    if (Math.Abs(state.lRz) > 500) active = true;
                                }

                                // Check POV hats
                                if (!active)
                                {
                                    if (state.rgdwPOV0 != 0xFFFFFFFF) active = true;
                                    if (state.rgdwPOV1 != 0xFFFFFFFF) active = true;
                                    if (state.rgdwPOV2 != 0xFFFFFFFF) active = true;
                                    if (state.rgdwPOV3 != 0xFFFFFFFF) active = true;
                                }
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(statePtr);
                        }
                    }
                }
                catch
                {
                    // Device might be disconnected, skip it
                }
            }

            return active;
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
            WriteLog($"[winmm] Scanning up to {maxDevs} joystick devices...");
            
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
                            WriteLog($"[winmm] Device ID {i}: '{caps.szPname}' - {caps.wNumButtons} buttons, {caps.wNumAxes} axes, Caps: 0x{caps.wCaps:X4}");
                        }
                    }
                }
                catch { }
            }
            if (AvailableJoysticks.Count > 0)
            {
                WriteLog($"[winmm] {AvailableJoysticks.Count} joystick(s) detected and active.");
            }
            else
            {
                WriteLog("[winmm] No active joysticks detected.");
            }
        }

        // Seuil relatif pour considerer qu'un axe analogique winmm a bouge (en % de l'etendue).
        const double JoyAxisRelativeThreshold = 0.02; // 2% de l'etendue totale (plus sensible)
        // Seuil absolu minimum en unites brutes (evite le bruit de derive des volants)
        // Pour range 0-65535, 1000 = ~1.5%
        const uint JoyAxisAbsoluteThreshold = 1000;

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

                // Comparaison avec l'etat precedent pour detecter un MOUVEMENT (delta)
                // Plus fiable que des seuils absolus, car certaines manettes renvoient
                // une valeur non-centree au repos.
                if (LastJoyInfo.TryGetValue(id, out JOYINFOEX prev))
                {
                    // Front montant sur n'importe quel bouton (un appui bref detecte meme
                    // si on poll a 1Hz : si le bouton est encore presse maintenant mais
                    // ne l'etait pas avant, c'est un nouvel appui).
                    uint newButtons = info.dwButtons & ~prev.dwButtons;
                    if (newButtons != 0)
                        active = true;

                    // dwButtonNumber = index du premier bouton presse (0xFFFFFFFF si aucun).
                    // Si on a un appui alors qu'on en avait pas avant => activite.
                    bool wasAnyPressed = prev.dwButtons != 0;
                    bool isAnyPressed = info.dwButtons != 0;
                    if (isAnyPressed && !wasAnyPressed)
                        active = true;
                    if (prev.dwButtonNumber != info.dwButtonNumber)
                        active = true;

                    // Comparaison axes (delta relatif + seuil absolu pour filtrer le bruit)
                    uint rangeX = caps.wXmax - caps.wXmin; if (rangeX == 0) rangeX = 1;
                    uint rangeY = caps.wYmax - caps.wYmin; if (rangeY == 0) rangeY = 1;
                    uint rangeZ = caps.wZmax - caps.wZmin; if (rangeZ == 0) rangeZ = 1;

                    uint dxRaw = (uint)Math.Abs((long)info.dwXpos - (long)prev.dwXpos);
                    uint dyRaw = (uint)Math.Abs((long)info.dwYpos - (long)prev.dwYpos);
                    uint dzRaw = (uint)Math.Abs((long)info.dwZpos - (long)prev.dwZpos);

                    double dx = (double)dxRaw / rangeX;
                    double dy = (double)dyRaw / rangeY;
                    double dz = (double)dzRaw / rangeZ;

                    // Seuil relatif ET absolu (les deux doivent etre depasses pour eviter
                    // le bruit constant de derive des volants au repos)
                    if ((dx > JoyAxisRelativeThreshold && dxRaw > JoyAxisAbsoluteThreshold) ||
                        (dy > JoyAxisRelativeThreshold && dyRaw > JoyAxisAbsoluteThreshold) ||
                        (dz > JoyAxisRelativeThreshold && dzRaw > JoyAxisAbsoluteThreshold))
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
                        {
                            uint dR = (uint)Math.Abs((long)info.dwRpos - (long)prev.dwRpos);
                            if ((double)dR / rangeR > JoyAxisRelativeThreshold && dR > JoyAxisAbsoluteThreshold) active = true;
                        }
                        if ((caps.wCaps & 0x04) != 0) // JOYCAPS_HASU
                        {
                            uint dU = (uint)Math.Abs((long)info.dwUpos - (long)prev.dwUpos);
                            if ((double)dU / rangeU > JoyAxisRelativeThreshold && dU > JoyAxisAbsoluteThreshold) active = true;
                        }
                        if ((caps.wCaps & 0x08) != 0) // JOYCAPS_HASV
                        {
                            uint dV = (uint)Math.Abs((long)info.dwVpos - (long)prev.dwVpos);
                            if ((double)dV / rangeV > JoyAxisRelativeThreshold && dV > JoyAxisAbsoluteThreshold) active = true;
                        }
                    }
                }

                LastJoyInfo[id] = info;
            }

            return active;
        }

        // ====================================================================
        // SDL2/SDL3 - CHARGEMENT DYNAMIQUE (couvre toutes les manettes supportees par SDL)
        // SDL2.dll ou SDL3.dll est charge depuis le dossier de l'exe si present. Sans SDL,
        // cette partie est silencieusement ignoree.
        // ====================================================================
        static class SdlGamepadHelper
        {
            private static IntPtr _sdlLib = IntPtr.Zero;
            private static bool _initTried = false;
            private static bool _available = false;
            private static bool _isSdl3 = false; // Détecter si on utilise SDL3

            const uint SDL_INIT_JOYSTICK = 0x00000200;
            const uint SDL_INIT_GAMECONTROLLER = 0x00002000;
            const int SDL_HAT_CENTERED = 0;

            // Delegates marshalé's pour les fonctions SDL2 (désactivé - non utilisé)
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // delegate int dSDL2_Init(uint flags);
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // delegate int dSDL2_InitSubSystem(uint flags);
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // delegate int dSDL2_WasInit(uint flags);
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // delegate int dSDL2_NumJoysticks();
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // delegate IntPtr dSDL2_JoystickOpen(int device_index);
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // delegate void dSDL2_JoystickClose(IntPtr joystick);
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // delegate short dSDL2_JoystickGetAxis(IntPtr joystick, int axis);
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // delegate byte dSDL2_JoystickGetHat(IntPtr joystick, int hat);
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // delegate byte dSDL2_JoystickGetButton(IntPtr joystick, int button);
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // delegate int dSDL2_JoystickNumAxes(IntPtr joystick);
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // delegate int dSDL2_JoystickNumHats(IntPtr joystick);
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // delegate int dSDL2_JoystickNumButtons(IntPtr joystick);
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // delegate void dSDL2_JoystickUpdate();
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // delegate IntPtr dSDL2_GameControllerOpen(int joystick_index);
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // delegate void dSDL2_GameControllerClose(IntPtr controller);
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // delegate short dSDL2_GameControllerGetAxis(IntPtr controller, int axis);
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // delegate byte dSDL2_GameControllerGetButton(IntPtr controller, int button);
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // delegate void dSDL2_GameControllerUpdate();

            // Delegates marshalé's pour les fonctions SDL3 (API différente)
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate int dSDL3_Init(uint flags);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate IntPtr dSDL3_GetJoysticks(out int count);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate IntPtr dSDL3_OpenJoystick(uint instance_id);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate void dSDL3_CloseJoystick(IntPtr joystick);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate float dSDL3_GetJoystickAxis(IntPtr joystick, int axis);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate byte dSDL3_GetJoystickHat(IntPtr joystick, int hat);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate byte dSDL3_GetJoystickButton(IntPtr joystick, int button);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate int dSDL3_GetNumJoystickAxes(IntPtr joystick);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate int dSDL3_GetNumJoystickHats(IntPtr joystick);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate int dSDL3_GetNumJoystickButtons(IntPtr joystick);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate void dSDL3_UpdateJoysticks();
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate uint dSDL3_GetJoystickInstanceID(IntPtr joystick);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            delegate int dSDL3_InitSubSystem(uint flags);

            // Variables SDL2 (désactivé - non utilisé)
            // static dSDL2_Init _sdl2Init;
            // static dSDL2_InitSubSystem _sdl2InitSubSystem;
            // static dSDL2_NumJoysticks _sdl2NumJoysticks;
            // static dSDL2_JoystickOpen _sdl2JoystickOpen;
            // static dSDL2_JoystickClose _sdl2JoystickClose;
            // static dSDL2_JoystickGetAxis _sdl2JoystickGetAxis;
            // static dSDL2_JoystickGetHat _sdl2JoystickGetHat;
            // static dSDL2_JoystickGetButton _sdl2JoystickGetButton;
            // static dSDL2_JoystickNumAxes _sdl2JoystickNumAxes;
            // static dSDL2_JoystickNumHats _sdl2JoystickNumHats;
            // static dSDL2_JoystickNumButtons _sdl2JoystickNumButtons;
            // static dSDL2_JoystickUpdate _sdl2JoystickUpdate;
            // static dSDL2_WasInit _sdl2WasInit;

            // Variables SDL3
            static dSDL3_Init _sdl3Init;
            static dSDL3_GetJoysticks _sdl3GetJoysticks;
            static dSDL3_OpenJoystick _sdl3OpenJoystick;
            static dSDL3_CloseJoystick _sdl3CloseJoystick;
            static dSDL3_GetJoystickAxis _sdl3GetJoystickAxis;
            static dSDL3_GetJoystickHat _sdl3GetJoystickHat;
            static dSDL3_GetJoystickButton _sdl3GetJoystickButton;
            static dSDL3_GetNumJoystickAxes _sdl3GetNumJoystickAxes;
            static dSDL3_GetNumJoystickHats _sdl3GetNumJoystickHats;
            static dSDL3_GetNumJoystickButtons _sdl3GetNumJoystickButtons;
            static dSDL3_UpdateJoysticks _sdl3UpdateJoysticks;
            static dSDL3_GetJoystickInstanceID _sdl3GetJoystickInstanceID;
            static dSDL3_InitSubSystem _sdl3InitSubSystem;

            // Etat SDL memorise
            static IntPtr[] OpenJoysticks = new IntPtr[16];
            static float[,] LastJoystickAxesFloat = new float[16, 16]; // Pour SDL3 (float)
            static byte[] LastJoystickHats = new byte[64]; // Augmenté à 64 (16 joysticks * 4 HATs max)
            static byte[] LastJoystickButtons = new byte[16 * 128]; // 16 joysticks * 128 boutons max : pour détection front montant
            static int[] JoystickAxisCount = new int[16];
            static int[] JoystickHatCount = new int[16];
            static int[] JoystickButtonCount = new int[16];
            static uint[] JoystickInstanceIDs = new uint[16]; // Pour SDL3
            static bool SdlDebugLogging = false; // Désactivé pour éviter le spam - réactiver via env var ATTRACT_SDL_DEBUG=1

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

                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                
                // Essayer SDL3 d'abord (plus moderne)
                string[] candidates = new string[] {
                    Path.Combine(exeDir, "SDL3.dll"),
                    Path.Combine(exeDir, "SDL2.dll")
                };
                
                foreach (string path in candidates)
                {
                    if (File.Exists(path))
                    {
                        _sdlLib = LoadLibrary(path);
                        if (_sdlLib != IntPtr.Zero)
                        {
                            WriteLog($"[SDL] Loaded: {path}");
                            _isSdl3 = path.Contains("SDL3");
                            break;
                        }
                    }
                }
                
                if (_sdlLib == IntPtr.Zero)
                {
                    // Tenter via le PATH systeme
                    try { _sdlLib = LoadLibrary("SDL3.dll"); if (_sdlLib != IntPtr.Zero) _isSdl3 = true; } catch { }
                    if (_sdlLib == IntPtr.Zero)
                    {
                        try { _sdlLib = LoadLibrary("SDL2.dll"); if (_sdlLib != IntPtr.Zero) _isSdl3 = false; } catch { }
                    }
                }
                
                if (_sdlLib == IntPtr.Zero)
                {
                    return; // SDL non disponible - on continuera sans
                }

                if (_isSdl3)
                {
                    // Charger les fonctions SDL3
                    _sdl3Init = LoadDelegate<dSDL3_Init>("SDL_Init");
                    _sdl3GetJoysticks = LoadDelegate<dSDL3_GetJoysticks>("SDL_GetJoysticks");
                    _sdl3OpenJoystick = LoadDelegate<dSDL3_OpenJoystick>("SDL_OpenJoystick");
                    _sdl3CloseJoystick = LoadDelegate<dSDL3_CloseJoystick>("SDL_CloseJoystick");
                    _sdl3GetJoystickAxis = LoadDelegate<dSDL3_GetJoystickAxis>("SDL_GetJoystickAxis");
                    _sdl3GetJoystickHat = LoadDelegate<dSDL3_GetJoystickHat>("SDL_GetJoystickHat");
                    _sdl3GetJoystickButton = LoadDelegate<dSDL3_GetJoystickButton>("SDL_GetJoystickButton");
                    _sdl3GetNumJoystickAxes = LoadDelegate<dSDL3_GetNumJoystickAxes>("SDL_GetNumJoystickAxes");
                    _sdl3GetNumJoystickHats = LoadDelegate<dSDL3_GetNumJoystickHats>("SDL_GetNumJoystickHats");
                    _sdl3GetNumJoystickButtons = LoadDelegate<dSDL3_GetNumJoystickButtons>("SDL_GetNumJoystickButtons");
                    _sdl3UpdateJoysticks = LoadDelegate<dSDL3_UpdateJoysticks>("SDL_UpdateJoysticks");
                    _sdl3GetJoystickInstanceID = LoadDelegate<dSDL3_GetJoystickInstanceID>("SDL_GetJoystickInstanceID");
                    _sdl3InitSubSystem = LoadDelegate<dSDL3_InitSubSystem>("SDL_InitSubSystem");

                    WriteLog($"[SDL3] SDL_Init loaded: {_sdl3Init != null}");
                    WriteLog($"[SDL3] SDL_GetJoysticks loaded: {_sdl3GetJoysticks != null}");
                    WriteLog($"[SDL3] SDL_OpenJoystick loaded: {_sdl3OpenJoystick != null}");
                    WriteLog($"[SDL3] SDL_InitSubSystem loaded: {_sdl3InitSubSystem != null}");

                    if (_sdl3Init == null)
                    {
                        WriteLog("[SDL3] Failed to load SDL3_Init function.");
                        return;
                    }
                    else
                    {
                        // NOTE: En SDL3, SDL_Init() retourne SDL_bool (int) :
                        //   1 (true)  = SUCCES
                        //   0 (false) = ECHEC
                        // C'est l'INVERSE de SDL2 o 0 = succes.
                        // De plus, SDL_INIT_JOYSTICK et SDL_INIT_GAMECONTROLLER ont ete
                        // supprimes en SDL3 (subsystem unify). On privilegie donc SDL_Init(0).
                        bool initSuccess = false;

                        uint[] initFlags = new uint[] {
                            0, // Pas d'initialisation specifique (recommande en SDL3)
                            SDL_INIT_JOYSTICK,                        // Legacy SDL2 (peut echouer en SDL3)
                            SDL_INIT_JOYSTICK | SDL_INIT_GAMECONTROLLER
                        };

                        string[] flagNames = new string[] {
                            "none",
                            "joystick",
                            "joystick+gamecontroller"
                        };

                        for (int i = 0; i < initFlags.Length; i++)
                        {
                            try
                            {
                                int r = _sdl3Init(initFlags[i]);
                                WriteLog($"[SDL3] SDL_Init({flagNames[i]}) returned: {r} (0x{r:X8})");
                                // SDL3 : r != 0 => succes (renvoie SDL_TRUE = 1)
                                if (r != 0)
                                {
                                    _available = true;
                                    initSuccess = true;
                                    WriteLog($"[SDL3] Successfully initialized with {flagNames[i]} flags.");
                                    break;
                                }
                                else
                                {
                                    WriteLog($"[SDL3] Init failed for flag {flagNames[i]} (SDL_FALSE).");
                                }
                            }
                            catch (Exception ex)
                            {
                                WriteLog($"[SDL3] Init with {flagNames[i]} exception: {ex.Message}");
                            }
                        }

                        if (!initSuccess)
                        {
                            WriteLog("[SDL3] All initialization methods failed. SDL3 may not be compatible with this system or DLL version.");
                        }
                        else if (_available && _sdl3InitSubSystem != null)
                        {
                            // Forcer l'initialisation du subsystem joystick explicitement
                            // En SDL3, SDL_INIT_JOYSTICK = 0x200 (peut encore exister pour compat)
                            const uint SDL_INIT_JOYSTICK = 0x200;
                            int r = _sdl3InitSubSystem(SDL_INIT_JOYSTICK);
                            WriteLog($"[SDL3] SDL_InitSubSystem(JOYSTICK) returned: {r} (0x{r:X8})");
                        }
                    }
                }
            }

            public static bool IsAvailable => _available;

            // Seuil SDL pour les axes analogiques (zone morte ~2% du max - plus sensible pour les volants)
            const float SdlAxisDeadzone = 0.02f; // 2% pour SDL3 (float) - delta
            const float SdlAxisAbsoluteThreshold = 0.05f; // 5% pour SDL3 - valeur absolue (baissé de 25%)

            public static bool CheckActivity()
            {
                if (!_available) return false;

                try
                {
                    bool active = false;
                    bool debug = SdlDebugLogging;

                    // Active le debug si variable d'environnement positionnee
                    try
                    {
                        string envDbg = Environment.GetEnvironmentVariable("ATTRACT_SDL_DEBUG");
                        if (!string.IsNullOrEmpty(envDbg) && (envDbg == "1" || envDbg.Equals("true", StringComparison.OrdinalIgnoreCase)))
                            debug = true;
                    }
                    catch { }

                    // SDL3 API uniquement
                    if (_sdl3UpdateJoysticks != null)
                    {
                        _sdl3UpdateJoysticks();
                    }
                    else if (debug)
                    {
                        WriteLog("[SDL3] WARNING: SDL_UpdateJoysticks is NULL");
                    }

                    // Obtenir la liste des joysticks SDL3
                    int count = 0;
                    IntPtr idsPtr = IntPtr.Zero;
                    if (_sdl3GetJoysticks != null)
                    {
                        idsPtr = _sdl3GetJoysticks(out count);
                        if (debug) WriteLog($"[SDL3] GetJoysticks returned count: {count}, idsPtr: 0x{idsPtr.ToInt64():X}");
                    }
                    else if (debug)
                    {
                        WriteLog("[SDL3] WARNING: SDL_GetJoysticks is NULL");
                    }

                    uint[] ids = new uint[16];
                    if (idsPtr != IntPtr.Zero && count > 0)
                    {
                        int maxToRead = Math.Min(count, 16);
                        for (int j = 0; j < maxToRead; j++)
                        {
                            ids[j] = (uint)Marshal.ReadInt32(idsPtr + j * sizeof(uint));
                        }
                    }

                    int loopCount = Math.Min(count, 16);
                    for (int i = 0; i < loopCount; i++)
                    {
                        uint instanceID = ids[i];

                        // Ouvrir le joystick si pas deja ouvert
                        if (OpenJoysticks[i] == IntPtr.Zero || JoystickInstanceIDs[i] != instanceID)
                        {
                            if (_sdl3OpenJoystick != null)
                            {
                                if (OpenJoysticks[i] != IntPtr.Zero && _sdl3CloseJoystick != null)
                                    _sdl3CloseJoystick(OpenJoysticks[i]);

                                OpenJoysticks[i] = _sdl3OpenJoystick(instanceID);
                                JoystickInstanceIDs[i] = instanceID;

                                if (OpenJoysticks[i] != IntPtr.Zero)
                                {
                                    if (_sdl3GetNumJoystickAxes != null) JoystickAxisCount[i] = _sdl3GetNumJoystickAxes(OpenJoysticks[i]);
                                    if (_sdl3GetNumJoystickHats != null) JoystickHatCount[i] = _sdl3GetNumJoystickHats(OpenJoysticks[i]);
                                    if (_sdl3GetNumJoystickButtons != null) JoystickButtonCount[i] = _sdl3GetNumJoystickButtons(OpenJoysticks[i]);
                                    // Une seule fois a l'ouverture : log informatif
                                    WriteLog($"[SDL3] Joystick {i} opened ({JoystickAxisCount[i]} axes, {JoystickHatCount[i]} HATs, {JoystickButtonCount[i]} buttons).");

                                    // Initialiser LastJoystickAxesFloat avec les valeurs actuelles
                                    if (_sdl3GetJoystickAxis != null && JoystickAxisCount[i] > 0)
                                    {
                                        IntPtr joyInit = OpenJoysticks[i];
                                        for (int a = 0; a < JoystickAxisCount[i] && a < 16; a++)
                                        {
                                            float v = _sdl3GetJoystickAxis(joyInit, a);
                                            LastJoystickAxesFloat[i, a] = v;
                                        }
                                    }

                                    // Initialiser l'etat precedent des boutons (cas ou on rouvre apres deconnexion)
                                    int btnArrayBase = i * 128;
                                    for (int b = 0; b < JoystickButtonCount[i] && b < 128; b++)
                                    {
                                        LastJoystickButtons[btnArrayBase + b] = 0;
                                    }
                                    int hatArrayBase = i * 4;
                                    for (int h = 0; h < JoystickHatCount[i] && h < 4; h++)
                                    {
                                        LastJoystickHats[hatArrayBase + h] = SDL_HAT_CENTERED;
                                    }
                                }
                            }
                        }

                        IntPtr joy = OpenJoysticks[i];
                        if (joy == IntPtr.Zero) continue;

                        // ---- BOUTONS : detection de FRONT MONTANT (press) ----
                        // Le probleme des volants DInput avec polling ~1Hz est qu'on peut
                        // rater completement un appui bref. Solution : on detecte
                        // strictement la transition 0 -> 1.
                        if (_sdl3GetJoystickButton != null)
                        {
                            int nb = JoystickButtonCount[i] > 0 ? JoystickButtonCount[i] : _sdl3GetNumJoystickButtons(joy);
                            int btnBase = i * 128;
                            for (int b = 0; b < nb && b < 128; b++)
                            {
                                byte pressed = _sdl3GetJoystickButton(joy, b);
                                byte prev = LastJoystickButtons[btnBase + b];
                                // Front montant (0 -> 1) => activite immediate
                                if (pressed != 0 && prev == 0)
                                {
                                    if (debug) WriteLog($"[SDL3] Button {b} PRESSED (rising edge)");
                                    active = true;
                                }
                                LastJoystickButtons[btnBase + b] = pressed;
                            }
                        }

                        // ---- HAT : detection de TOUT CHANGEMENT ----
                        // Sur un volant, le HAT represente souvent le D-Pad. Un mouvement
                        // bref de direction doit etre capture meme entre 2 polls.
                        if (_sdl3GetJoystickHat != null && JoystickHatCount[i] > 0)
                        {
                            int hatBase = i * 4;
                            for (int h = 0; h < JoystickHatCount[i] && h < 4; h++)
                            {
                                byte hat = _sdl3GetJoystickHat(joy, h);
                                byte prev = LastJoystickHats[hatBase + h];
                                if (hat != prev)
                                {
                                    if (debug) WriteLog($"[SDL3] HAT {h}: value={hat} (prev={prev})");
                                    // Tout changement de HAT (y compris vers CENTERED) compte
                                    if (hat != SDL_HAT_CENTERED || prev != SDL_HAT_CENTERED)
                                        active = true;
                                    LastJoystickHats[hatBase + h] = hat;
                                }
                            }
                        }

                        // ---- AXES : detection par DELTA uniquement ----
                        // On ne considere PAS la valeur absolue : un volant au repos a
                        // souvent une legere derive. Seul un mouvement reel compte.
                        if (_sdl3GetJoystickAxis != null)
                        {
                            int nbAxes = JoystickAxisCount[i];
                            for (int a = 0; a < nbAxes && a < 16; a++)
                            {
                                float v = _sdl3GetJoystickAxis(joy, a);
                                float prev = LastJoystickAxesFloat[i, a];
                                float delta = Math.Abs(v - prev);

                                if (delta > SdlAxisDeadzone)
                                {
                                    if (debug) WriteLog($"[SDL3] Axis {a}: v={v:F4}, prev={prev:F4}, delta={delta:F4}");
                                    active = true;
                                }

                                LastJoystickAxesFloat[i, a] = v;
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

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FreeLibrary(IntPtr hModule);

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

        // Système de mémoire anti-répétition (20 minutes)
        static Dictionary<string, DateTime> RecentlyShownGames = new Dictionary<string, DateTime>();
        static Dictionary<string, DateTime> RecentlyShownSystems = new Dictionary<string, DateTime>();
        static readonly TimeSpan MemoryDuration = TimeSpan.FromMinutes(20);

        // Suivi robuste de l'inactivité utilisateur
        static POINT LastMousePos = new POINT();
        static DateTime LastActivityTime = DateTime.MinValue;

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

            // DirectInput désactivé - SDL2 est utilisé à la place pour détecter les volants
            // InitializeDirectInput();

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
                    // TEMPORAIREMENT DESACTIVE POUR TEST SDL3
                    // if (Process.GetProcessesByName("emulationstation").Length == 0)
                    // {
                    //     WriteLog("EmulationStation is closed. Stopping the assistant.");
                    //     break;
                    // }

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
                        if (IsAttractModeActive || LastActivityTime != DateTime.MinValue)
                        {
                            WriteLog("Signal screensaver-stop received: resuming Attract Mode cycle.");
                        }
                        IsAttractModeActive = false;
                        GamesCountInCurrentSystem = 0;
                        LastActivityTime = DateTime.MinValue;

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
                            LastActivityTime = DateTime.MinValue;
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

                    // 4b. Fallback polling GetAsyncKeyState pour detecter les inputs
                    // injectes (RustDesk, TeamViewer, RDP, etc.) qui passent au travers
                    // du hook LLKHF_INJECTED. On check les touches principales.
                    if (!keyboardActive)
                    {
                        // VK codes: A-Z (0x41-0x5A), 0-9 (0x30-0x39), ESPACE (0x20),
                        // ENTREE (0x0D), ECHAP (0x1B), FLECHES (0x25-0x28),
                        // MAJ (0x10), CTRL (0x11), ALT (0x12), TAB (0x09)
                        ushort[] keysToCheck = new ushort[] {
                            0x20, 0x0D, 0x1B, 0x09, 0x10, 0x11, 0x12,
                            0x25, 0x26, 0x27, 0x28,
                            0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39,
                            0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x4C, 0x4D,
                            0x4E, 0x4F, 0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A
                        };
                        foreach (ushort vk in keysToCheck)
                        {
                            if ((GetAsyncKeyState(vk) & 0x8000) != 0)
                            {
                                keyboardActive = true;
                                break;
                            }
                        }
                    }

                    bool realMouseClicked = MouseActivityDetected;
                    if (realMouseClicked)
                    {
                        MouseActivityDetected = false; // Réinitialiser le drapeau
                    }

                    // Calcul de l'état d'inactivité
                    bool anyActivity = isGameRunning || controllerActive || mouseMoved || keyboardActive || realMouseClicked;
                    if (anyActivity)
                    {
                        LastActivityTime = DateTime.Now; // Mettre à jour l'heure de dernière activité (toujours)
                        
                        // Log et reset UNIQUEMENT si l'attract mode était ACTIVEMENT en cours
                        if (IsAttractModeActive)
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
                            else if (realMouseClicked)
                            {
                                WriteLog("Mouse click or scroll detected! Stopping Attract Mode.");
                            }

                            IsAttractModeActive = false;
                            GamesCountInCurrentSystem = 0;
                        }
                    }

                    // Calculer secondes d'inactivité depuis dernière activité
                    int inactiveSeconds = 0;
                    if (LastActivityTime != DateTime.MinValue)
                    {
                        inactiveSeconds = (int)(DateTime.Now - LastActivityTime).TotalSeconds;
                    }

                    bool isUserInactive = inactiveSeconds >= InactivityTimeout;

                    // 5. Check if EmulationStation is in foreground (if OnlyWhenFocused enabled)
                    if (OnlyWhenFocused && !IsEmulationStationFocused())
                    {
                        if (IsAttractModeActive)
                        {
                            WriteLog("EmulationStation no longer in foreground! Pausing Attract Mode (Standby Mode).");
                            IsAttractModeActive = false;
                            GamesCountInCurrentSystem = 0;
                        }
                        LastActivityTime = DateTime.MinValue;
                        WriteConsole($"[Standby] ES not in focus. Inactivity: {inactiveSeconds}/{InactivityTimeout}s");
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
                            WriteLog($"Inactivity detected ({inactiveSeconds}s). Activating Attract Mode...");
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

                        WriteConsole($"[Idle] Inactivity: {inactiveSeconds}/{InactivityTimeout}s{waitingContext} | Controllers: {controllerActive}");
                        Thread.Sleep(200);
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
        // ====================================================================
        // GESTION DE LA MEMOIRE ANTI-REPETITION (20 MINUTES)
        // ====================================================================
        static void CleanOldMemoryEntries()
        {
            DateTime now = DateTime.Now;
            var expiredGames = RecentlyShownGames.Where(kvp => (now - kvp.Value) > MemoryDuration).ToList();
            foreach (var kvp in expiredGames)
            {
                RecentlyShownGames.Remove(kvp.Key);
            }

            var expiredSystems = RecentlyShownSystems.Where(kvp => (now - kvp.Value) > MemoryDuration).ToList();
            foreach (var kvp in expiredSystems)
            {
                RecentlyShownSystems.Remove(kvp.Key);
            }
        }

        static bool IsGameRecentlyShown(string gameKey)
        {
            CleanOldMemoryEntries();
            return RecentlyShownGames.ContainsKey(gameKey);
        }

        static bool IsSystemRecentlyShown(string systemName)
        {
            CleanOldMemoryEntries();
            return RecentlyShownSystems.ContainsKey(systemName);
        }

        static void MarkGameAsShown(string gameKey)
        {
            RecentlyShownGames[gameKey] = DateTime.Now;
        }

        static void MarkSystemAsShown(string systemName)
        {
            RecentlyShownSystems[systemName] = DateTime.Now;
        }

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

                // Nettoyer les anciennes entrées de mémoire
                CleanOldMemoryEntries();

                // Fast random scroll avec vérification anti-répétition
                string detectedSystem = "";
                int attempts = 0;
                const int maxAttempts = 10;

                while (attempts < maxAttempts)
                {
                    ScrollRandomly();
                    Thread.Sleep(1000); // Give RetroBat time to write the system file

                    // Check user activity during scrolling - stop immediately if detected
                    if (CheckGamepadActivity())
                    {
                        LastActivityTime = DateTime.Now;
                        WriteLog("Controller activity detected during system scrolling! Stopping Attract Mode.");
                        IsAttractModeActive = false;
                        GamesCountInCurrentSystem = 0;
                        return;
                    }

                    detectedSystem = ReadSelectedSystem();

                    // Vérifier si ce système a été affiché récemment
                    if (!IsSystemRecentlyShown(detectedSystem))
                    {
                        break;
                    }

                    WriteLog($"System '{detectedSystem}' was shown recently (within 20min). Trying another...");
                    attempts++;
                }

                if (attempts >= maxAttempts)
                {
                    WriteLog("Could not find a system not recently shown after 10 attempts. Using current selection.");
                }

                WriteLog($"System selected: '{detectedSystem}'. Entering the system...");
                ushort enterVk = GetVirtualKey(EnterKey, 0x58); // 0x58 = VK_X
                WriteLog($"Pressing entry key '{EnterKey}' (VK: 0x{enterVk:X2})...");
                SimulateKeyPress(enterVk);
                GamesCountInCurrentSystem = 0;
                LastSelectedSystem = detectedSystem;
                MarkSystemAsShown(detectedSystem);
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

                // Nettoyer les anciennes entrées de mémoire
                CleanOldMemoryEntries();

                // Random scroll avec vérification anti-répétition
                string selectedRom = "";
                string selectedGameName = "";
                int attempts = 0;
                const int maxAttempts = 10;

                while (attempts < maxAttempts)
                {
                    ScrollRandomly();
                    Thread.Sleep(1200); // Wait for the selected game file to be updated

                    // Check user activity during scrolling - stop immediately if detected
                    if (CheckGamepadActivity())
                    {
                        LastActivityTime = DateTime.Now;
                        WriteLog("Controller activity detected during game scrolling! Stopping Attract Mode.");
                        IsAttractModeActive = false;
                        GamesCountInCurrentSystem = 0;
                        return;
                    }

                    ReadSelectedGame(out string gameSys, out selectedRom, out selectedGameName);
                    string gameKey = $"{currentSystem}|{selectedRom}";

                    // Vérifier si ce jeu a été affiché récemment
                    if (!IsGameRecentlyShown(gameKey) && !selectedRom.Equals(LastSelectedGameRom, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    WriteLog($"Game '{selectedGameName}' was shown recently (within 20min) or is same as previous. Trying another...");
                    attempts++;
                }

                if (attempts >= maxAttempts)
                {
                    WriteLog("Could not find a game not recently shown after 10 attempts. Using current selection.");
                }

                LastSelectedGameRom = selectedRom;
                GamesCountInCurrentSystem++;
                MarkGameAsShown($"{currentSystem}|{selectedRom}");

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
                        LastActivityTime = DateTime.Now;
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

            // DirectInput désactivé - SDL2 est utilisé à la place
            // ----------- 0) DirectInput (volants, joysticks, manettes DirectInput) -----------
            // try
            // {
            //     if (CheckDirectInputActivity()) active = true;
            // }
            // catch (Exception ex)
            // {
            //     WriteConsole($"[DirectInput] error: {ex.Message}");
            // }

            // ----------- 1) XInput (manettes Xbox /Compatibles) -----------
            XINPUT_STATE state = new XINPUT_STATE();
            for (int i = 0; i < 4; i++)
            {
                if (XInputHelper.GetState(i, ref state))
                {
                    // Un bouton est presse
                    if (state.Gamepad.wButtons != 0) active = true;

                    // Les gachettes depassent le seuil de zone morte (abaisse a 10 pour plus de sensibilite)
                    if (state.Gamepad.bLeftTrigger > 10 || state.Gamepad.bRightTrigger > 10) active = true;

                    // Les joysticks depassent la zone morte (abaisse a 3000 pour detecter les petits mouvements)
                    if (Math.Abs(state.Gamepad.sThumbLX) > 3000 || Math.Abs(state.Gamepad.sThumbLY) > 3000) active = true;
                    if (Math.Abs(state.Gamepad.sThumbRX) > 3000 || Math.Abs(state.Gamepad.sThumbRY) > 3000) active = true;

                    // Comparaison avec l'etat precedent pour detecter un MOUVEMENT
                    // (delta abaisse a 300 pour les mouvements fins)
                    if (LastGamepadStates.TryGetValue(i, out var lastState))
                    {
                        if (state.dwPacketNumber != lastState.dwPacketNumber)
                        {
                            if (state.Gamepad.wButtons != lastState.Gamepad.wButtons ||
                                state.Gamepad.bLeftTrigger != lastState.Gamepad.bLeftTrigger ||
                                state.Gamepad.bRightTrigger != lastState.Gamepad.bRightTrigger ||
                                Math.Abs(state.Gamepad.sThumbLX - lastState.Gamepad.sThumbLX) > 300 ||
                                Math.Abs(state.Gamepad.sThumbLY - lastState.Gamepad.sThumbLY) > 300 ||
                                Math.Abs(state.Gamepad.sThumbRX - lastState.Gamepad.sThumbRX) > 300 ||
                                Math.Abs(state.Gamepad.sThumbRY - lastState.Gamepad.sThumbRY) > 300)
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
