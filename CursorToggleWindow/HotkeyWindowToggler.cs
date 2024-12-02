﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace CursorToggleWindow
{
    public class HotkeyWindowToggler
    {
        const int WM_HOTKEY = 0x0312;
        const uint SW_HIDE = 0;
        const uint SW_SHOWNORMAL = 1;
        const uint SW_SHOWMINIMIZED = 2;


        public List<IntPtr> HWNDList { get; set; } = [];
        public bool IsWindowVisible { get; set; } = true;
        public bool IsRegistered { get; private set; } = false;

        private readonly nint _handle;
        private readonly int _id;
        private readonly uint _fsModifiers;
        private readonly uint _vk;
        private bool _disposed = false;

        public HotkeyWindowToggler(nint handle, int id, uint fsModifiers, uint vk)
        {
            _handle = handle;
            _id = id;
            _fsModifiers = fsModifiers;
            _vk = vk;

            HwndSource hwndSource = HwndSource.FromHwnd(_handle);
            if (hwndSource != null)
            {
                hwndSource.AddHook(WndProc);
            }
            else
            {
                throw new InvalidOperationException("无法获取窗口的 HwndSource 对象。");
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                //if (disposing)
                //{
                //    // 释放托管资源
                //}

                // 释放非托管资源
                if (IsRegistered)
                {
                    NativeMethods.UnregisterHotKey(_handle, _id);
                    IsRegistered = false;
                }

                _disposed = true;
            }
        }
        public bool Register()
        {
            if (!NativeMethods.RegisterHotKey(_handle, _id, _fsModifiers, _vk))
            {
                return false;
            }
            else
            {
                IsRegistered = true;
                return true;
            }
        }
        public bool UnRegister()
        {
            if (NativeMethods.UnregisterHotKey(_handle, _id))
            {
                IsRegistered = false;
                return true;
            }
            else
            {
                return false;
            }
        }
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == _id)
            {
                ToggleWindowVisibility();
                handled = true;  // 标记消息已处理
            }
            else
            {
                handled = false;
            }
            return IntPtr.Zero;
        }

        private void ToggleWindowVisibility()
        {
            // 隐藏
            if (IsWindowVisible)
            {
                foreach (IntPtr HWND in HWNDList)
                {
                    try
                    {
                        NativeMethods.ShowWindow(HWND, SW_HIDE);
                    }
                    catch { }
                }
                IsWindowVisible = false;
            }
            // 显示
            else
            {
                foreach (IntPtr HWND in HWNDList)
                {
                    try
                    {
                        NativeMethods.ShowWindow(HWND, SW_SHOWMINIMIZED);
                        NativeMethods.ShowWindow(HWND, SW_SHOWNORMAL);
                    }
                    catch { }
                }
                IsWindowVisible = true;
            }
        }
    }
}
