using System.ComponentModel;
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
        private const int WM_MOUSEMOVE = 0x0200;
        private const uint SW_SHOWMINIMIZED = 2;
        //private const int GWL_EXSTYLE = -20;
        //private const int WS_EX_LAYERED = 0x80000;
        //private const uint LWA_ALPHA = 0x2;
        //private const int WS_EX_APPWINDOW = 0x00040000;
        //private const int WS_EX_TOOLWINDOW = 0x00000080;

        private IntPtr Handle = IntPtr.Zero; // MainWindow的句柄
        private SortDescription _sortDescriptionByVisible;
        private SortDescription _sortDescriptionByName;
        private List<TargetWindowInfo> _targetWindow = []; // 需要隐藏的目标窗口信息
        private IntPtr _hookId = IntPtr.Zero; // 鼠标钩子id
        private bool _isHook = false; // 是否安装鼠标钩子
        //private readonly object _targetWindowLock = new();

        public MainWindow()
        {
            InitializeComponent();
            _sortDescriptionByVisible = new("Visible", ListSortDirection.Descending);
            _sortDescriptionByName = new("Name", ListSortDirection.Ascending);
        }
        private void RefreshWindowsInfoListViewButton_Click(object sender, RoutedEventArgs e) => RefreshWindowListView();
        private void RefreshWindowListView()
        {
            WindowInfoListView.Items.Clear();
            _ = EnumWindows(EnumWindowsProc, IntPtr.Zero);
            WindowInfoListView.Items.SortDescriptions.Add(_sortDescriptionByVisible);
            WindowInfoListView.Items.SortDescriptions.Add(_sortDescriptionByName);
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
            foreach (var item in WindowInfoListView.SelectedItems)
            {
                if (item is WindowInfo windowInfo)
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
                        _targetWindow.Add(targetWindowInfo);
                        text.Append(windowInfo.HWND);
                        text.Append("; ");
                    }
                    else
                    {
                        MessageBox.Show($"无法获取窗口“{windowInfo.Name}”的信息");
                    }
                }
            }
            TargetWindowTextBlock.Text = text.ToString();
        }

        private void StopHookButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isHook)
            {
                if (!Unhook())
                {
                    MessageBox.Show("卸载钩子失败");
                    return;
                }
                else
                {
                    _isHook = false;
                    _targetWindow.Clear();
                    TargetWindowTextBlock.Text = "";
                }
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
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
                            for (int i = 0; i < _targetWindow.Count; i++)
                            {
                                bool isInWindow = x >= _targetWindow[i].Left && x <= _targetWindow[i].Right && y <= _targetWindow[i].Bottom && y >= _targetWindow[i].Top;
                                if (isInWindow)
                                {
                                    GetWindowRect(_targetWindow[i].HWND, out RECT rect);
                                    _targetWindow[i].Left = rect.Left;
                                    _targetWindow[i].Top = rect.Top;
                                    _targetWindow[i].Right = rect.Right;
                                    _targetWindow[i].Bottom = rect.Bottom;
                                    if (!_targetWindow[i].Visible)
                                    {
                                        // 显示
                                        _targetWindow[i].Visible = true;
                                        //ShowWindow(_targetWindow[i].HWND, SW_SHOWMINIMIZED); // 可以在显示时置顶窗口，但是卡顿
                                        ShowWindow(_targetWindow[i].HWND, SW_SHOWNORMAL);
                                    }
                                }
                                else if (!isInWindow && _targetWindow[i].Visible)
                                {
                                    // 隐藏
                                    _targetWindow[i].Visible = false;
                                    ShowWindow(_targetWindow[i].HWND, SW_HIDE);
                                }
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"@@@@@@@{ex.Message}");
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Handle = new WindowInteropHelper(this).Handle; // 获取MainWindow句柄
            RefreshWindowListView();
            // 创建快捷键 Alt + P 隐藏/显示 MainWindow
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

        private void OpenAboutWindowMenuItem_Click(object sender, RoutedEventArgs e)
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
        private bool Unhook()
        {
            if (UnhookWindowsHookEx(_hookId))
            {
                _hookId = IntPtr.Zero;
                return true;
            }
            else
            {
                return false;
            }
        }

        private void ShowTargetWindowButton_Click(object sender, RoutedEventArgs e)
        {
            if (_targetWindow != null)
            {
                for(int i  = 0; i < _targetWindow.Count; i++)
                {
                    ShowWindow(_targetWindow[i].HWND, SW_SHOWMINIMIZED);
                    ShowWindow(_targetWindow[i].HWND, SW_SHOWNORMAL);
                }
            }
        }
        /// <summary>
        /// 需要隐藏的窗口信息
        /// </summary>
        class TargetWindowInfo
        {
            public IntPtr HWND { get; set; }
            public bool Visible { get; set; } = true;
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
        }
        /// <summary>
        /// 窗口中列表显示的窗口信息
        /// </summary>
        class WindowInfo
        {
            public bool Visible { set; get; }
            public string VisibleText { set; get; } = string.Empty;
            public string Name { set; get; } = string.Empty;
            public IntPtr HWND { set; get; }
            public IntPtr Pid { set; get; }
            public string FilePath { set; get; } = string.Empty;
        }
    }
}