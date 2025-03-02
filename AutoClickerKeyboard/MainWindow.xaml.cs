using System;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Runtime.InteropServices;
using Timer = System.Timers.Timer;
using System.Diagnostics;

namespace AutoClickerKeyboard
{
    public partial class MainWindow : Window
    {
        private Timer _clickTimer;
        private byte _keyToPress = (byte)char.ToUpper(char.Parse("Q"));// 0x41; // Char code "A"

        private string _selectedProcessName = string.Empty;
        private static IntPtr _hookID = IntPtr.Zero;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static LowLevelKeyboardProc _proc = HookCallback;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        public MainWindow()
        {
            InitializeComponent();
            _clickTimer = new Timer(1000);
            _clickTimer.Elapsed += PerformClick;
            _hookID = SetHook(_proc);
            Closed += MainWindow_Closed;
            LoadProcesses();
        }
        private void LoadProcesses()
        {
            var processes = Process.GetProcesses()
                .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
                .Select(p => p.ProcessName)
                .Distinct()
                .ToList();

            cmbProcesses.ItemsSource = processes;
            cmbProcesses.SelectionChanged += (s, e) =>
            {
                if (cmbProcesses.SelectedItem != null)
                    _selectedProcessName = cmbProcesses.SelectedItem.ToString() ?? string.Empty;
            };
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                    return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(string.Empty), 0);
        }

        private void PerformClick(object? sender, ElapsedEventArgs e)
        {
            if (!_clickTimer.Enabled) return;

            if (IsTargetProcessActive())
            {
                keybd_event(_keyToPress, 0, 0, 0); // Press button
                keybd_event(_keyToPress, 0, 2, 0); // Release button
            }
        }

        #region Events

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            UnhookWindowsHookEx(_hookID);
        }
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);

                if (vkCode == 0x70 && (Keyboard.Modifiers & ModifierKeys.Control) != 0) // Ctrl + F1
                {
                    Application.Current.Dispatcher.Invoke(() => ((MainWindow)Application.Current.MainWindow).StartButton_Click(null, (RoutedEventArgs)EventArgs.Empty));
                }
                else if (vkCode == 0x71 && (Keyboard.Modifiers & ModifierKeys.Control) != 0) // Ctrl + F2
                {
                    var mainWindow = (MainWindow)Application.Current.MainWindow;
                    mainWindow._clickTimer.Stop(); // Stop Timer Now
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void StartButton_Click(object? sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtTimeBetweenPresses.Text, out int interval) && interval > 0)
            {
                _clickTimer.Interval = interval;
                _clickTimer.Start();
                SetKeyToPress();
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _clickTimer.Stop();
        }
        #endregion

        #region Helpers
        private bool IsTargetProcessActive()
        {
            IntPtr hWnd = GetForegroundWindow();
            GetWindowThreadProcessId(hWnd, out int processId);
            var process = Process.GetProcessById(processId);
            return process.ProcessName.Equals(_selectedProcessName, StringComparison.OrdinalIgnoreCase);
        }

        private void SetKeyToPress()
        {
            if (!string.IsNullOrEmpty(txtKey.Text))
            {
                char key = char.ToUpper(txtKey.Text[0]);
                _keyToPress = (byte)key;
            }
        }
        #endregion
    }
}