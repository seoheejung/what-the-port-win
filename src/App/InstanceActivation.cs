using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WhatThePort.App {
    public static class InstanceActivation {
        [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int processId);

        public static void AllowExistingInstanceForeground(){
            // The newly launched process can receive foreground permission from Explorer.
            // Transfer it to this user's existing instance before that instance handles the event.
            // Never grant foreground permission to every process (ASFW_ANY).
            using(var current=Process.GetCurrentProcess()){
                foreach(var other in Process.GetProcessesByName(current.ProcessName))using(other){
                    try{if(other.Id!=current.Id&&other.SessionId==current.SessionId&&Native.IsOwned(other.Id))AllowSetForegroundWindow(other.Id);}
                    catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
                }
            }
        }
    }
}
