using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WhatThePort.App {
    // A persistent native message window keeps monitoring independent of the panel.
    public sealed class TrayIcon : IDisposable {
        const int Callback = 0x8002;
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct IconData {
            public uint Size; public IntPtr Window; public uint Id, Flags, Message; public IntPtr Icon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string Tip;
            public uint State, StateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=256)] public string Info;
            public uint Version;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=64)] public string Title;
            public uint InfoFlags; public Guid Guid; public IntPtr BalloonIcon;
        }
        [StructLayout(LayoutKind.Sequential)] struct Point { public int X,Y; }
        [DllImport("shell32.dll", CharSet=CharSet.Unicode,SetLastError=true)] static extern bool Shell_NotifyIcon(uint operation,ref IconData data);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern bool AppendMenu(IntPtr menu,uint flags,UIntPtr id,string text);
        [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr menu);
        [DllImport("user32.dll")] static extern uint TrackPopupMenuEx(IntPtr menu,uint flags,int x,int y,IntPtr owner,IntPtr parameters);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint message,IntPtr wparam,IntPtr lparam);
        [DllImport("user32.dll", SetLastError=true)] static extern IntPtr CreateIconFromResourceEx(byte[] data,uint size,bool icon,uint version,int width,int height,uint flags);
        readonly IntPtr window,normalIcon,warningIcon;
        readonly HwndSource source;
        readonly uint taskbarCreated;
        bool disposed,warning;
        string text="What the Port";
        string[] labels=new string[0];
        Action<int> menuAction;
        public event Action Click;
        public event Action BalloonClick;
        public bool Visible { get; private set; }

        public TrayIcon(HwndSource owner) {
            source=owner;window=owner.Handle;
            taskbarCreated=RegisterWindowMessage("TaskbarCreated");
            normalIcon=CreateIcon(false);
            try {warningIcon=CreateIcon(true);source.AddHook(Hook);Add();}
            catch {Dispose();throw;}
        }
        IconData Data(uint flags) {return new IconData{Size=(uint)Marshal.SizeOf(typeof(IconData)),Window=window,Id=1,Flags=flags,Message=Callback,Icon=warning?warningIcon:normalIcon,Tip=text,Info="",Title=""};}
        void Add() {
            if(disposed)return;
            var data=Data(1|2|4|0x80); // Message, icon, tooltip, standard tooltip with version 4.
            Visible=Shell_NotifyIcon(0,ref data)||Shell_NotifyIcon(1,ref data);
            if(Visible){data.Version=4;Shell_NotifyIcon(4,ref data);}
        }
        public void Update(string tip,bool hasWarning) {
            tip=Limit(tip,127);
            if(disposed||(Visible&&text==tip&&warning==hasWarning))return;
            text=tip;warning=hasWarning;
            if(!Visible){Add();return;}
            var data=Data(2|4|0x80);Shell_NotifyIcon(1,ref data);
        }
        public void SetMenu(string[] items,Action<int> action){labels=(string[])items.Clone();menuAction=action;}
        public void Notify(string title,string message) {
            if(disposed||!Visible)return;
            var data=Data(0x10);data.Title=Limit(title,63);data.Info=Limit(message,255);data.InfoFlags=0x80; // Respect quiet time.
            Shell_NotifyIcon(1,ref data);
        }
        static string Limit(string value,int length){value=value??"";return value.Length<=length?value:value.Substring(0,length);}
        IntPtr Hook(IntPtr hwnd,int message,IntPtr wparam,IntPtr lparam,ref bool handled) {
            if(disposed)return IntPtr.Zero;
            if(message==taskbarCreated){Add();return IntPtr.Zero;}
            if(message!=Callback)return IntPtr.Zero;
            int notification=(int)(lparam.ToInt64()&0xffff);
            if(((lparam.ToInt64()>>16)&0xffff)!=1)return IntPtr.Zero;
            handled=true;
            if(notification==0x400||notification==0x401){if(Click!=null)Click();} // Mouse or keyboard selection.
            else if(notification==0x405){if(BalloonClick!=null)BalloonClick();}
            else if(notification==0x7B)ShowMenu(wparam);
            return IntPtr.Zero;
        }
        void ShowMenu(IntPtr coordinates) {
            var menu=CreatePopupMenu();if(menu==IntPtr.Zero)return;
            uint selected=0;
            try {
                for(int i=0;i<labels.Length;i++)AppendMenu(menu,labels[i]==null?0x800u:0u,new UIntPtr((uint)i+1),labels[i]);
                int x=(short)(coordinates.ToInt64()&0xffff),y=(short)((coordinates.ToInt64()>>16)&0xffff);
                if(x==-1&&y==-1){Point cursor;if(GetCursorPos(out cursor)){x=cursor.X;y=cursor.Y;}}
                SetForegroundWindow(window);
                selected=TrackPopupMenuEx(menu,0x100|0x2,x,y,window,IntPtr.Zero);
                PostMessage(window,0,IntPtr.Zero,IntPtr.Zero);
            } finally {DestroyMenu(menu);}
            if(selected>0&&selected<=labels.Length&&labels[selected-1]!=null&&menuAction!=null)menuAction((int)selected-1);
        }
        static IntPtr CreateIcon(bool warning) {
            const int size=32;
            byte[] bytes;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)) {
                writer.Write(40);writer.Write(size);writer.Write(size*2);writer.Write((ushort)1);writer.Write((ushort)32);
                writer.Write(0);writer.Write(size*size*4);writer.Write(0);writer.Write(0);writer.Write(0);writer.Write(0);
                for(int y=size-1;y>=0;y--)for(int x=0;x<size;x++) {
                    int coverage=0;
                    for(int sy=0;sy<4;sy++)for(int sx=0;sx<4;sx++) {
                        double px=x+(sx+.5)/4,py=y+(sy+.5)/4;
                        int column=(int)Math.Round((px-5)/7),row=(int)Math.Round((py-5)/7);
                        double dx=px-(5+column*7),dy=py-(5+row*7);
                        if(column>=0&&column<4&&row>=0&&row<4&&dx*dx+dy*dy<=4)coverage++;
                    }
                    writer.Write((byte)(warning?36:224));writer.Write((byte)(warning?178:219));writer.Write((byte)(warning?255:125));writer.Write((byte)(coverage*255/16));
                }
                writer.Write(new byte[size*size/8]);writer.Flush();bytes=stream.ToArray();
            }
            IntPtr icon=CreateIconFromResourceEx(bytes,(uint)bytes.Length,true,0x00030000,size,size,0);
            if(icon==IntPtr.Zero)throw new Win32Exception();return icon;
        }
        public void Dispose() {
            if(disposed)return;disposed=true;
            if(Visible){var data=Data(0);Shell_NotifyIcon(2,ref data);Visible=false;}
            if(source!=null&&!source.IsDisposed)source.RemoveHook(Hook);
            if(normalIcon!=IntPtr.Zero)Native.DestroyIcon(normalIcon);
            if(warningIcon!=IntPtr.Zero)Native.DestroyIcon(warningIcon);
        }
    }
}
