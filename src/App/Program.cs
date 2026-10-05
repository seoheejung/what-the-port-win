using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace WhatThePort.App {
    public static class Program {
        [STAThread] public static int Main(string[] args){
            bool snapshot=args.Contains("--snapshot"),smoke=args.Contains("--smoke-test"),demo=snapshot||args.Contains("--demo");
            int smokeExit=0;
            string instance="Local\\WhatThePort.Windows"+(smoke?".Smoke."+Guid.NewGuid().ToString("N"):demo?".Demo":"");
            bool first=true;using(var signal=new EventWaitHandle(false,EventResetMode.AutoReset,instance+".Show"))using(var mutex=new Mutex(true,instance,out first)){
                if(!first&&!snapshot){signal.Set();return 0;}
                try{
                    var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
                    app.DispatcherUnhandledException+=delegate(object sender,System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e){MessageBox.Show(e.Exception.Message,"What the Port",MessageBoxButton.OK,MessageBoxImage.Error);e.Handled=true;};
                    var panel=new Panel(demo,snapshot,smoke?Path.Combine(Path.GetTempPath(),"wtp-smoke-"+Guid.NewGuid().ToString("N")):null);app.MainWindow=panel.Window;
                    var registration=ThreadPool.RegisterWaitForSingleObject(signal,delegate{app.Dispatcher.BeginInvoke(new Action(()=>panel.Show()));},null,Timeout.Infinite,false);
                    if(smoke){int i=Array.IndexOf(args,"--smoke-test");if(i+1>=args.Length)throw new ArgumentException("--smoke-test requires a report path.");app.Dispatcher.BeginInvoke(new Action(async delegate{smokeExit=await panel.LiveSmoke(args[i+1])?0:1;}));}
                    else if(snapshot){int i=Array.IndexOf(args,"--snapshot");if(i+1>=args.Length)throw new ArgumentException("--snapshot requires a directory.");app.Dispatcher.BeginInvoke(new Action(()=>panel.CaptureAll(Path.GetFullPath(args[i+1]))));}
                    else{if(!args.Contains("--background"))panel.Show();else new System.Windows.Interop.WindowInteropHelper(panel.Window).EnsureHandle();panel.Start();}
                    app.Run();registration.Unregister(null);return smokeExit;
                }catch(Exception e){if(snapshot){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"snapshot-error.log"),e.ToString());}else MessageBox.Show(e.Message,"What the Port could not start");return 1;}
            }
        }
    }
}
