using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;

namespace WhatThePort.App {
    public static class PanelPlacement {
        const double Margin = 12;
        [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left,Top,Right,Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X,Y; public NativePoint(int x,int y){X=x;Y=y;} }
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out NativeRect rect);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(NativePoint point,uint flags);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);

        public static Rect PhysicalBounds(Window window){
            NativeRect bounds;if(!GetWindowRect(new WindowInteropHelper(window).Handle,out bounds))throw new InvalidOperationException("Could not read panel position.");
            return new Rect(bounds.Left,bounds.Top,bounds.Right-bounds.Left,bounds.Bottom-bounds.Top);
        }
        public static void Place(Window window,double height,Point? saved,bool opening,bool fromTray){
            var handle=new WindowInteropHelper(window).EnsureHandle();
            Rect current=PhysicalBounds(window);System.Windows.Forms.Screen screen;
            if(opening&&saved.HasValue)screen=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)saved.Value.X,(int)saved.Value.Y-1));
            else if(opening)screen=fromTray?System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position):System.Windows.Forms.Screen.PrimaryScreen;
            else screen=System.Windows.Forms.Screen.FromHandle(handle);
            var area=screen.WorkingArea;uint dpiX,dpiY;
            var monitor=MonitorFromPoint(new NativePoint(area.Left+area.Width/2,area.Top+area.Height/2),2);
            if(GetDpiForMonitor(monitor,0,out dpiX,out dpiY)!=0){dpiX=96;dpiY=96;}
            double scale=dpiY/96.0;
            Rect? previous=opening?(saved.HasValue?(Rect?)new Rect(saved.Value.X,saved.Value.Y-1,1,1):null):current;
            var bounds=Calculate(new Rect(area.Left,area.Top,area.Width,area.Height),new Size(440*scale,height*scale),previous,12*scale);
            // Native physical coordinates avoid ambiguous virtual-desktop DIP origins across monitors.
            window.Width=bounds.Width/scale;window.Height=bounds.Height/scale;
            if(!SetWindowPos(handle,IntPtr.Zero,(int)Math.Round(bounds.Left),(int)Math.Round(bounds.Top),(int)Math.Round(bounds.Width),(int)Math.Round(bounds.Height),0x0014))throw new InvalidOperationException("Could not position panel.");
        }

        public static Rect WorkArea(Window window) {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return SystemParameters.WorkArea;
            var screen = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
            var source = PresentationSource.FromVisual(window);
            var transform = source == null || source.CompositionTarget == null
                ? Matrix.Identity : source.CompositionTarget.TransformFromDevice;
            return ToDips(new Rect(screen.Left, screen.Top, screen.Width, screen.Height), transform);
        }

        public static Rect ToDips(Rect pixels, Matrix transform) {
            return new Rect(transform.Transform(pixels.TopLeft), transform.Transform(pixels.BottomRight));
        }

        // Keep the existing bottom edge while growing upward, within this monitor's work area.
        public static Rect Calculate(Rect workArea, Size desired, Rect? previous, double margin=Margin) {
            double insetX = Math.Min(margin, workArea.Width / 4);
            double insetY = Math.Min(margin, workArea.Height / 4);
            var available = new Rect(workArea.Left + insetX, workArea.Top + insetY,
                workArea.Width - 2 * insetX, workArea.Height - 2 * insetY);
            double width = Math.Min(desired.Width, available.Width);
            double height = Math.Min(desired.Height, available.Height);
            double left = previous.HasValue ? previous.Value.Left : available.Right - width;
            double bottom = previous.HasValue ? previous.Value.Bottom : available.Bottom;
            left = Math.Max(available.Left, Math.Min(left, available.Right - width));
            bottom = Math.Max(available.Top + height, Math.Min(bottom, available.Bottom));
            return new Rect(left, bottom - height, width, height);
        }
    }
}
