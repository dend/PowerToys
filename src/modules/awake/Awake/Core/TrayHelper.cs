// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Awake.Core.Models;
using Awake.Core.Native;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Awake.Core
{
    /// <summary>
    /// Helper class used to manage the system tray.
    /// </summary>
    /// <remarks>
    /// Because Awake is a console application, there is no built-in
    /// way to embed UI components so we have to heavily rely on the native Windows API.
    /// </remarks>
    internal static class TrayHelper
    {
        private static IntPtr _trayMenu;

        private static IntPtr TrayMenu { get => _trayMenu; set => _trayMenu = value; }

        private static NotifyIcon TrayIcon { get; set; }

        static TrayHelper()
        {
            TrayIcon = new NotifyIcon();
        }

        internal delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

#pragma warning disable SA1310 // Field names should not contain underscore
        private const int WM_MOUSEMOVE = 0x0200;
        private const uint WS_OVERLAPPEDWINDOW = 0xcf0000;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint CS_USEDEFAULT = 0x80000000;
        private const uint CS_DBLCLKS = 8;
        private const uint CS_VREDRAW = 1;
        private const uint CS_HREDRAW = 2;
        private const uint COLOR_WINDOW = 5;
        private const uint COLOR_BACKGROUND = 1;
        private const uint IDC_CROSS = 32515;
        private const uint WM_DESTROY = 2;
        private const uint WM_PAINT = 0x0f;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_LBUTTONDBLCLK = 0x0203;
        private const uint WM_RBUTTONUP = 0x0205;

        private const int NIM_ADD = 0x00000000;
        private const int NIM_DELETE = 0x00000002;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_TIP = 0x00000004;
        private const int NIF_INFO = 0x00000010;
        private const int NIIF_INFO = 0x00000001;

        private const int WM_APP = 0x8000;
        private const int NOTIFICATION_MESSAGE = WM_APP + 100;
#pragma warning restore SA1310 // Field names should not contain underscore

        private static WndProc delegWndProc = MyWndProc;

        public static void CreateTray(Icon icon)
        {
            WNDCLASSEX wind_class = default(WNDCLASSEX);
            wind_class.cbSize = Marshal.SizeOf(typeof(WNDCLASSEX));
            wind_class.cbClsExtra = 0;
            wind_class.cbWndExtra = 0;
            wind_class.hInstance = Marshal.GetHINSTANCE(Assembly.GetExecutingAssembly().GetModules()[0]);
            wind_class.hIcon = IntPtr.Zero;
            wind_class.lpszClassName = "myClass";
            wind_class.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(delegWndProc);
            wind_class.hIconSm = IntPtr.Zero;
            var x = Native.Bridge.RegisterClassEx(ref wind_class);

            IntPtr hwnd = Native.Bridge.CreateWindowEx(
                              0,
                              "myClass",
                              "WindowClassName",
                              0x00000000,
                              0,
                              0,
                              300,
                              400,
                              IntPtr.Zero,
                              IntPtr.Zero,
                              wind_class.hInstance,
                              IntPtr.Zero);

            var data = new NotifyIconData();
            data.hWnd = hwnd;
            data.uID = 1;
            data.uFlags = 0x00000001 | 0x00000002 | 0x00000004 | 0x00000008 | 0x00000080; // NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_STATE | NIF_SHOWTIP;
            data.szTip = "Awake";
            data.dwState = 0;
            data.uCallbackMessage = NOTIFICATION_MESSAGE;
            data.dwStateMask = 0x00000002; // NIS_SHAREDICON
            data.uTimeoutOrVersion = 4;
            data.hIcon = icon.Handle;
            var trayIcon = Native.Bridge.Shell_NotifyIcon(NIM_DELETE, ref data);
            trayIcon = Native.Bridge.Shell_NotifyIcon(NIM_ADD, ref data);

#pragma warning disable CS0219 // Variable is assigned but its value is never used
            int d = 0;
#pragma warning restore CS0219 // Variable is assigned but its value is never used

            // return data;
        }

        private static IntPtr MyWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                // All GUI painting must be done here
                case WM_PAINT:
                    break;

                case WM_LBUTTONDBLCLK:
                    MessageBox.Show("Doubleclick");
                    break;

                case WM_RBUTTONUP:
                    Logger.LogInfo("RBUTTON!");
                    break;

                case WM_DESTROY:
                    Native.Bridge.DestroyWindow(hWnd);

                    break;

                default:
                    break;
            }

            return Native.Bridge.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        public static void OnRightClick()
        {
            Logger.LogInfo("Right click happened.");
        }

        public static void InitializeTray(string text, Icon icon, ManualResetEvent? exitSignal, ContextMenuStrip? contextMenu = null)
        {
            CreateTray(icon);

            // Task.Factory.StartNew(
            //    (tray) =>
            //    {
            //        try
            //        {
            //            Logger.LogInfo("Setting up the tray.");
            //            if (tray != null)
            //            {
            //                ((NotifyIcon)tray).Text = text;
            //                ((NotifyIcon)tray).Icon = icon;
            //                ((NotifyIcon)tray).ContextMenuStrip = contextMenu;
            //                ((NotifyIcon)tray).Visible = true;
            //                ((NotifyIcon)tray).MouseClick += TrayClickHandler;
            //                Application.AddMessageFilter(new TrayMessageFilter(exitSignal));
            //                Application.Run();
            //                Logger.LogInfo("Tray setup complete.");
            //            }
            //        }
            //        catch (Exception ex)
            //        {
            //            Logger.LogError($"An error occurred initializing the tray. {ex.Message}");
            //            Logger.LogError($"{ex.StackTrace}");
            //        }
            //    },
            //    TrayIcon);
        }

        /// <summary>
        /// Function used to construct the context menu in the tray natively.
        /// </summary>
        /// <remarks>
        /// We need to use the Windows API here instead of the common control exposed
        /// by NotifyIcon because the one that is built into the Windows Forms stack
        /// hasn't been updated in a while and is looking like Office XP. That introduces
        /// scalability and coloring changes on any OS past Windows XP.
        /// </remarks>
        /// <param name="sender">The sender that triggers the handler.</param>
        /// <param name="e">MouseEventArgs instance containing mouse click event information.</param>
        private static void TrayClickHandler(object? sender, MouseEventArgs e)
        {
            IntPtr windowHandle = Manager.GetHiddenWindow();

            if (windowHandle != IntPtr.Zero)
            {
                Bridge.SetForegroundWindow(windowHandle);
                Bridge.TrackPopupMenuEx(TrayMenu, 0, Cursor.Position.X, Cursor.Position.Y, windowHandle, IntPtr.Zero);
            }
        }

        internal static void SetTray(string text, AwakeSettings settings, bool startedFromPowerToys)
        {
            SetTray(
                text,
                settings.Properties.KeepDisplayOn,
                settings.Properties.Mode,
                settings.Properties.CustomTrayTimes,
                startedFromPowerToys);
        }

        public static void SetTray(string text, bool keepDisplayOn, AwakeMode mode, Dictionary<string, int> trayTimeShortcuts, bool startedFromPowerToys)
        {
            if (TrayMenu != IntPtr.Zero)
            {
                var destructionStatus = Bridge.DestroyMenu(TrayMenu);
                if (destructionStatus != true)
                {
                    Logger.LogError("Failed to destroy menu.");
                }
            }

            TrayMenu = Bridge.CreatePopupMenu();

            if (TrayMenu != IntPtr.Zero)
            {
                if (!startedFromPowerToys)
                {
                    // If Awake is started from PowerToys, the correct way to exit it is disabling it from Settings.
                    Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING, (uint)TrayCommands.TC_EXIT, "Exit");
                    Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_SEPARATOR, 0, string.Empty);
                }

                Bridge.InsertMenu(TrayMenu, 0,  Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING | (keepDisplayOn ? Native.Constants.MF_CHECKED : Native.Constants.MF_UNCHECKED) | (mode == AwakeMode.PASSIVE ? Native.Constants.MF_DISABLED : Native.Constants.MF_ENABLED), (uint)TrayCommands.TC_DISPLAY_SETTING, "Keep screen on");
            }

            // In case there are no tray shortcuts defined for the application default to a
            // reasonable initial set.
            if (trayTimeShortcuts.Count == 0)
            {
                trayTimeShortcuts.AddRange(Manager.GetDefaultTrayOptions());
            }

            var awakeTimeMenu = Bridge.CreatePopupMenu();
            for (int i = 0; i < trayTimeShortcuts.Count; i++)
            {
                Bridge.InsertMenu(awakeTimeMenu, (uint)i, Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING, (uint)TrayCommands.TC_TIME + (uint)i, trayTimeShortcuts.ElementAt(i).Key);
            }

            Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_SEPARATOR, 0, string.Empty);

            Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING | (mode == AwakeMode.PASSIVE ? Native.Constants.MF_CHECKED : Native.Constants.MF_UNCHECKED), (uint)TrayCommands.TC_MODE_PASSIVE, "Off (keep using the selected power plan)");
            Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING | (mode == AwakeMode.INDEFINITE ? Native.Constants.MF_CHECKED : Native.Constants.MF_UNCHECKED), (uint)TrayCommands.TC_MODE_INDEFINITE, "Keep awake indefinitely");
            Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_POPUP | (mode == AwakeMode.TIMED ? Native.Constants.MF_CHECKED : Native.Constants.MF_UNCHECKED), (uint)awakeTimeMenu, "Keep awake on interval");
            Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING | Native.Constants.MF_DISABLED | (mode == AwakeMode.EXPIRABLE ? Native.Constants.MF_CHECKED : Native.Constants.MF_UNCHECKED), (uint)TrayCommands.TC_MODE_EXPIRABLE, "Keep awake until expiration date and time");

            TrayIcon.Text = text;
        }

        private sealed class CheckButtonToolStripMenuItemAccessibleObject : ToolStripItem.ToolStripItemAccessibleObject
        {
            private readonly CheckButtonToolStripMenuItem _menuItem;

            public CheckButtonToolStripMenuItemAccessibleObject(CheckButtonToolStripMenuItem menuItem)
                : base(menuItem)
            {
                _menuItem = menuItem;
            }

            public override AccessibleRole Role => AccessibleRole.CheckButton;

            public override string Name => _menuItem.Text + ", " + Role + ", " + (_menuItem.Checked ? "Checked" : "Unchecked");
        }

        private sealed class CheckButtonToolStripMenuItem : ToolStripMenuItem
        {
            protected override AccessibleObject CreateAccessibilityInstance()
            {
                return new CheckButtonToolStripMenuItemAccessibleObject(this);
            }
        }
    }
}
