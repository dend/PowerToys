// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Awake.Core.Models
{
    [StructLayout(LayoutKind.Sequential)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Standard Win32 API convention.")]
    internal struct MSG
    {
        internal IntPtr hwnd;
        internal uint message;
        internal UIntPtr wParam;
        internal IntPtr lParam;
        internal int time;
        internal POINT pt;
        internal int lPrivate;
    }
}
