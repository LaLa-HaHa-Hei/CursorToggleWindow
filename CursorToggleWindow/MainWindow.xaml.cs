﻿using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using static CursorToggleWindow.NativeMethods;

namespace CursorToggleWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int MOD_ALT = 0x0001;
        private const int WH_MOUSE_LL = 14;
        private const uint SW_HIDE = 0;
        private const uint SW_SHOWNORMAL = 1;
        private const uint SW_SHOWMINIMIZED = 2;
        private const int WM_MOUSEMOVE = 0x0200;

        private IntPtr Handle = IntPtr.Zero;
        private SortDescription _sortDescriptionByVisible;
        private SortDescription _sortDescriptionByName;
        private TargetWindowInfo? _targetWindow = null;
        private IntPtr _hookId = IntPtr.Zero;
        private bool _isHook = false;
        private readonly object _targetWindowLock = new object();

        public MainWindow()
        {
            InitializeComponent();
            _sortDescriptionByVisible = new SortDescription("Visible", ListSortDirection.Descending);
            _sortDescriptionByName = new SortDescription("Name", ListSortDirection.Ascending);
        }
        private bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam)
        {
            if (!IsWindow(hwnd) || !IsWindowEnabled(hwnd)) { return true; }

            int cTextLen = GetWindowTextLength(hwnd);
            string Title;
            if (cTextLen != 0)
            {
                StringBuilder text = new(cTextLen + 1);
                _ = GetWindowText(hwnd, text, cTextLen + 1);
                Title = text.ToString();
            }
            else return true;
            bool visible;
            string visibleText;
            if (IsWindowVisible(hwnd))
            {
                visible = true;
                visibleText = "✔"; //✓✔
            }
            else
            {
                visible = false;
                visibleText = "✘"; //✗✘
            }
            //_ = GetWindowThreadProcessId(hwnd, out int pid);
            //string filePath;
            //try
            //{
            //    filePath = Process.GetProcessById(pid).MainModule?.FileName ?? "无法获取";
            //}
            //catch (Win32Exception)
            //{
            //    filePath = "没有权限";
            //}
            WindowInfoListView.Items.Add(new WindowInfo { HWND = hwnd, Name = Title, Visible = visible, FilePath = "", Pid = 0, VisibleText = visibleText });
            return true;
        }
        private void RefreshWindowListView()
        {
            WindowInfoListView.Items.Clear();
            _ = EnumWindows(EnumWindowsProc, IntPtr.Zero);
            WindowInfoListView.Items.SortDescriptions.Add(_sortDescriptionByVisible);
            WindowInfoListView.Items.SortDescriptions.Add(_sortDescriptionByName);
        }
        private void RefreshWindowsInfoListViewButton_Click(object sender, RoutedEventArgs e) => RefreshWindowListView();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Handle = new WindowInteropHelper(this).Handle;
            RefreshWindowListView();
            HotkeyWindowToggler mainWindowHotkey = new(Handle, 0xBFFF, MOD_ALT, (uint)KeyInterop.VirtualKeyFromKey(Key.P));
            mainWindowHotkey.HWNDList.Add(Handle);
            mainWindowHotkey.Register();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("退出后将无法隐藏目标窗口！\n可以通过 Alt + P 隐藏本程序\n          是否退出？", "是否退出？", MessageBoxButton.YesNo, MessageBoxImage.Question);

            // 如果用户选择“否”，则取消关闭操作
            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
            }
            // 退出
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
            }
        }

        private void OpenAboutWindow_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow w = new()
            {
                Owner = this
            };
            w.Show();
        }

        private void ShowSingleWindowButton_Click(object sender, RoutedEventArgs e)
        {
            nint HWND;
            try
            {
                HWND = nint.Parse(ShowSingleWindowTextBox.Text);
            }
            catch (System.FormatException)
            {
                MessageBox.Show("请输入句柄的数字！");
                return;
            }
            if (!IsWindow(HWND))
            {
                MessageBox.Show("句柄无效，请检查");
                return;
            }
            ShowWindow(HWND, SW_SHOWMINIMIZED);
            ShowWindow(HWND, SW_SHOWNORMAL);
        }

        class TargetWindowInfo
        {
            public IntPtr HWND { get; set; }
            public bool Visible { get; set; } = true;
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
        }

        class WindowInfo
        {
            public bool Visible { set; get; }
            public string VisibleText { set; get; } = string.Empty;
            public string Name { set; get; } = string.Empty;
            public IntPtr HWND { set; get; }
            public IntPtr Pid { set; get; }
            public string FilePath { set; get; } = string.Empty;
        }
        private bool SetHook()
        {
            using Process curProcess = Process.GetCurrentProcess();
            using ProcessModule curModule = curProcess.MainModule;
            _hookId = SetWindowsHookEx(WH_MOUSE_LL, HookCallback, GetModuleHandle(curModule.ModuleName), 0);
            if (_hookId == IntPtr.Zero)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                lock (_targetWindowLock)
                { 
                    if (_targetWindow != null && nCode >= 0)
                    {
                        int msg = wParam.ToInt32();
                        switch (msg)
                        {
                            case WM_MOUSEMOVE:
                                var ms = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                                int x = ms.pt.x;
                                int y = ms.pt.y;
                                // 光标在窗口中
                                bool isInWindow = x >= _targetWindow.Left && x <= _targetWindow.Right && y <= _targetWindow.Bottom && y >= _targetWindow.Top;
                                if (isInWindow && !_targetWindow.Visible)
                                {
                                    _targetWindow.Visible = true;
                                    //ShowWindow(_targetWindow.HWND, SW_SHOWMINIMIZED);
                                    ShowWindow(_targetWindow.HWND, SW_SHOWNORMAL);
                                }
                                else if (!isInWindow && _targetWindow.Visible)
                                {
                                    _targetWindow.Visible = false;
                                    ShowWindow(_targetWindow.HWND, SW_HIDE);
                                }
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"@@@@@@@{ex.Message}");
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void StartHookButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowInfoListView.SelectedItems.Count == 0)
                return;
            if (!_isHook)
            {
                if (!SetHook())
                {
                    MessageBox.Show("创建钩子失败");
                    return;
                }
                else
                {
                    _isHook = true;
                }
            }
            StringBuilder text = new();
            if (WindowInfoListView.SelectedItem is WindowInfo windowInfo)
            {
                if (GetWindowRect(windowInfo.HWND, out RECT rect))
                {
                    TargetWindowInfo targetWindowInfo = new()
                    {
                        HWND = windowInfo.HWND,
                        Visible = windowInfo.Visible,
                        Left = rect.Left,
                        Top = rect.Top,
                        Right = rect.Right,
                        Bottom = rect.Bottom,
                    };
                    _targetWindow = targetWindowInfo;
                    text.Append(windowInfo.HWND);
                    text.Append("; ");
                }
                else
                {
                    MessageBox.Show($"无法获取窗口“{windowInfo.Name}”的信息");
                }
            }
            TargetWindowTextBlock.Text = text.ToString();
        }

        private void StopHookButton_Click(object sender, RoutedEventArgs e)
        {
            _targetWindow = null;
            TargetWindowTextBlock.Text = "";
        }

        private void ShowTargetWindowButton_Click(object sender, RoutedEventArgs e)
        {
            if (_targetWindow != null)
            {
                ShowWindow(_targetWindow.HWND, SW_SHOWMINIMIZED);
                ShowWindow(_targetWindow.HWND, SW_SHOWNORMAL);
            }
        }
    }
}