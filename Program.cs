using NoWinKey.Properties;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace NoWinKey
{
    internal static class Program
    {
        private static Mutex s_mutex;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (IsSingleInstance())
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Application.Run(new TrayApplicationContext());

                s_mutex.ReleaseMutex();
            }
        }

        private static bool IsSingleInstance()
        {
            string mutexName = "NoWinKey";

            s_mutex = new Mutex(true, mutexName, out bool createNew);

            return createNew;
        }
    }

    public class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private bool _isStarted = true;
        private readonly DisableWinKey _runner;

        public TrayApplicationContext()
        {
            _trayIcon = new NotifyIcon()
            {
                Icon = Resources.NoWinIcon,
                ContextMenu = new ContextMenu(new MenuItem[] {
                    new MenuItem("Toggle", OnToggle),
                    new MenuItem("Exit", OnExit)
                }),
                Visible = true
            };

            _runner = new DisableWinKey();
        }

        private void OnToggle(object sender, EventArgs e)
        {
            if (_isStarted)
            {
                _trayIcon.Icon = Resources.WinIcon;
                _runner.Stop();
            }
            else
            {
                _trayIcon.Icon = Resources.NoWinIcon;
                _runner.Start();
            }

            _isStarted = !_isStarted;
        }

        private void OnExit(object sender, EventArgs e)
        {
            _trayIcon.Visible = false;

            Application.Exit();
        }
    }

    public class DisableWinKey
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int WM_KEYDOWN = 0x0100;

        private static IntPtr s_hookID = IntPtr.Zero;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == VK_LWIN || vkCode == VK_RWIN)
                {
                    return (IntPtr)1;
                }
            }

            return CallNextHookEx(s_hookID, nCode, wParam, lParam);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        public DisableWinKey()
        {
            Start();
        }

        public void Start()
        {
            s_hookID = SetHook(HookCallback);
        }

        public void Stop()
        {
            UnhookWindowsHookEx(s_hookID);
        }
    }
}