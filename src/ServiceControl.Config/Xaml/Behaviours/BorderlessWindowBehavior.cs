﻿namespace ServiceControl.Config.Xaml.Behaviours
{
    using System;
    using System.Windows;
    using System.Windows.Interop;
    using Microsoft.Xaml.Behaviors;
    using Native;

    public class BorderlessWindowBehavior : Behavior<Window>
    {
        protected override void OnAttached()
        {
            if (PresentationSource.FromVisual(AssociatedObject) != null)
            {
                AddHwndHook();
            }
            else
            {
                AssociatedObject.SourceInitialized += AssociatedObject_SourceInitialized;
            }

            AssociatedObject.WindowStyle = WindowStyle.None;
            base.OnAttached();
        }

        protected override void OnDetaching()
        {
            RemoveHwndHook();
            base.OnDetaching();
        }

        void AddHwndHook()
        {
            hwndSource = PresentationSource.FromVisual(AssociatedObject) as HwndSource;
            hwndSource.AddHook(HwndHook);
            hwnd = new WindowInteropHelper(AssociatedObject).Handle;
        }

        void RemoveHwndHook()
        {
            AssociatedObject.SourceInitialized -= AssociatedObject_SourceInitialized;
            hwndSource.RemoveHook(HwndHook);
        }

        void AssociatedObject_SourceInitialized(object sender, EventArgs e)
        {
            AddHwndHook();
        }

        IntPtr HwndHook(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            var returnval = IntPtr.Zero;
            switch (message)
            {
                case NativeConstants.WM_NCCALCSIZE:
                    /* Hides the border */
                    handled = true;
                    break;

                case NativeConstants.WM_NCPAINT:
                    {
                        if (Environment.OSVersion.Version.Major >= 6)
                        {
                            var m = new MARGINS
                            {
                                bottomHeight = 1,
                                leftWidth = 1,
                                rightWidth = 1,
                                topHeight = 1
                            };
                            UnsafeNativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref m);
                        }

                        handled = true;
                    }
                    break;

                case NativeConstants.WM_NCACTIVATE:
                    {
                        /* As per http://msdn.microsoft.com/en-us/library/ms632633(VS.85).aspx , "-1" lParam
                         * "does not repaint the nonclient area to reflect the state change." */
                        returnval = UnsafeNativeMethods.DefWindowProc(hWnd, message, wParam, new IntPtr(-1));
                        handled = true;
                    }
                    break;

                case NativeConstants.WM_GETMINMAXINFO:
                    /* http://blogs.msdn.com/b/llobo/archive/2006/08/01/maximizing-window-_2800_with-windowstyle_3d00_none_2900_-considering-taskbar.aspx */
                    UnsafeNativeMethods.WmGetMinMaxInfo(hWnd, lParam);

                    /* Setting handled to false enables the application to process it's own Min/Max requirements,
                     * as mentioned by jason.bullard (comment from September 22, 2011) on http://gallery.expression.microsoft.com/ZuneWindowBehavior/ */
                    handled = false;
                    break;

                case NativeConstants.WM_NCHITTEST:

                    // don't process the message on windows that can't be resized
                    var resizeMode = AssociatedObject.ResizeMode;
                    if (resizeMode is ResizeMode.CanMinimize or ResizeMode.NoResize)
                    {
                        break;
                    }

                    // get X & Y out of the message
                    var screenPoint = new Point((short)lParam, (short)(lParam.ToInt32() >> 16));

                    // convert to window coordinates
                    var windowPoint = AssociatedObject.PointFromScreen(screenPoint);
                    var windowSize = AssociatedObject.RenderSize;
                    var windowRect = new Rect(windowSize);
                    windowRect.Inflate(-resizeWidth, -resizeWidth);

                    // don't process the message if the mouse is outside the 6px resize border
                    if (windowRect.Contains(windowPoint))
                    {
                        break;
                    }

                    var windowHeight = (int)windowSize.Height;
                    var windowWidth = (int)windowSize.Width;

                    // create the rectangles where resize arrows are shown
                    var topLeft = new Rect(0, 0, resizeWidth, resizeWidth);
                    var top = new Rect(resizeWidth, 0, windowWidth - (resizeWidth * 2), resizeWidth);
                    var topRight = new Rect(windowWidth - resizeWidth, 0, resizeWidth, resizeWidth);

                    var left = new Rect(0, resizeWidth, resizeWidth, windowHeight - (resizeWidth * 2));
                    var right = new Rect(windowWidth - resizeWidth, resizeWidth, resizeWidth, windowHeight - (resizeWidth * 2));

                    var bottomLeft = new Rect(0, windowHeight - resizeWidth, resizeWidth, resizeWidth);
                    var bottom = new Rect(resizeWidth, windowHeight - resizeWidth, windowWidth - (resizeWidth * 2), resizeWidth);
                    var bottomRight = new Rect(windowWidth - resizeWidth, windowHeight - resizeWidth, resizeWidth, resizeWidth);

                    // check if the mouse is within one of the rectangles
                    if (topLeft.Contains(windowPoint))
                    {
                        returnval = NativeConstants.HTTOPLEFT;
                    }
                    else if (top.Contains(windowPoint))
                    {
                        returnval = NativeConstants.HTTOP;
                    }
                    else if (topRight.Contains(windowPoint))
                    {
                        returnval = NativeConstants.HTTOPRIGHT;
                    }
                    else if (left.Contains(windowPoint))
                    {
                        returnval = NativeConstants.HTLEFT;
                    }
                    else if (right.Contains(windowPoint))
                    {
                        returnval = NativeConstants.HTRIGHT;
                    }
                    else if (bottomLeft.Contains(windowPoint))
                    {
                        returnval = NativeConstants.HTBOTTOMLEFT;
                    }
                    else if (bottom.Contains(windowPoint))
                    {
                        returnval = NativeConstants.HTBOTTOM;
                    }
                    else if (bottomRight.Contains(windowPoint))
                    {
                        returnval = NativeConstants.HTBOTTOMRIGHT;
                    }

                    if (returnval != IntPtr.Zero)
                    {
                        handled = true;
                    }

                    break;

                default:
                    break;
            }

            return returnval;
        }

        HwndSource hwndSource;
        IntPtr hwnd;
        const int resizeWidth = 6;
    }
}