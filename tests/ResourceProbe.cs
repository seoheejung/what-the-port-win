using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using WhatThePort;
using WhatThePort.App;
using Panel = WhatThePort.App.Panel;

// Runs the real panel and scanner with isolated preferences and cleanup disabled.
public static class ResourceProbe {
    [StructLayout(LayoutKind.Sequential)] struct MemoryCounters {
        public uint Size, PageFaultCount;
        public UIntPtr PeakWorkingSet, WorkingSet, PeakPagedPool, PagedPool, PeakNonPagedPool, NonPagedPool, Pagefile, PeakPagefile, PrivateUsage;
    }
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetProcessTimes(IntPtr process, out long created, out long exited, out long kernel, out long user);
    [DllImport("psapi.dll", SetLastError = true)] static extern bool GetProcessMemoryInfo(IntPtr process, ref MemoryCounters counters, uint size);
    [DataContract] public sealed class Phase {
        [DataMember] public string State;
        [DataMember] public double Seconds, CpuPercent, AverageWorkingSetMB, PeakWorkingSetMB, PeakPrivateMB, PeakManagedMB;
        [DataMember] public int Scans, Servers, MinimumServers=Int32.MaxValue;
    }
    [DataContract] public sealed class Report {
        [DataMember] public int LogicalProcessors, ScanSeconds;
        [DataMember] public string MeasuredAtUtc;
        [DataMember] public string RenderingMode;
        [DataMember] public bool TrayRegistered, DeferredPanel;
        [DataMember] public int FixtureServers;
        [DataMember] public List<Phase> Phases = new List<Phase>();
    }
    static readonly FieldInfo Current = typeof(Panel).GetField("current", BindingFlags.Instance | BindingFlags.NonPublic);
    [STAThread] public static int Main(string[] args) {
        if (args.Length == 2 && args[0] == "--fixture-server") return ServerFixture(args[1]);
        if (args.Length < 3 || args.Length > 5) return 2;
        int seconds;
        if (!Int32.TryParse(args[2], out seconds) || seconds < 15 || seconds > 300) return 2;
        int expectedServers = 0;
        if (args.Length >= 4 && (!Int32.TryParse(args[3], out expectedServers) || expectedServers < 0 || expectedServers > 20)) return 2;
        int[] expectedIds=args.Length==5?args[4].Split(',').Select(Int32.Parse).ToArray():new int[0];
        if(args.Length==5&&expectedIds.Length!=expectedServers)return 2;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Panel panel = null;
        int exitCode = 0;
        app.Startup += async delegate {
            try {
                new Store(args[1]).SaveSettings(new Settings { Notifications = false, Cleanup = "Off" });
                panel = new Panel(false, false, args[1]);
                bool deferred=panel.Window==null;
                if(!deferred){app.MainWindow=panel.Window;panel.Window.ShowActivated=false;new WindowInteropHelper(panel.Window).EnsureHandle();}
                panel.Start();
                await Task.Delay(5000);
                var report = new Report { LogicalProcessors = Environment.ProcessorCount, ScanSeconds = 3, MeasuredAtUtc = DateTime.UtcNow.ToString("o"), RenderingMode = RenderOptions.ProcessRenderMode.ToString(), DeferredPanel=deferred, FixtureServers=expectedServers };
                object tray=typeof(Panel).GetField("tray",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);
                report.TrayRegistered=(bool)tray.GetType().GetProperty("Visible").GetValue(tray,null);
                if(!report.TrayRegistered)throw new InvalidOperationException("Run the resource probe on a desktop with a Windows taskbar.");
                report.Phases.Add(await Measure(panel, "hidden", seconds, expectedServers, expectedIds));
                panel.ShowFromLaunch();
                ((Button)panel.Window.FindName("Pin")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(3000);
                report.Phases.Add(await Measure(panel, "visible", seconds, expectedServers, expectedIds));
                panel.Window.Hide();
                await Task.Delay(33000);
                if(deferred&&panel.Window!=null)throw new InvalidOperationException("Hidden panel resources were not released.");
                report.Phases.Add(await Measure(panel, "hidden-after-show", seconds, expectedServers, expectedIds));
                File.WriteAllText(args[0], Store.Json(report));
            } catch (Exception e) {
                Console.Error.WriteLine(e.Message);
                exitCode = 1;
            } finally {
                if (panel != null) panel.Quit(); else app.Shutdown();
            }
        };
        app.Run();
        return exitCode;
    }
    static async Task<Phase> Measure(Panel panel, string state, int seconds, int expectedServers, int[] expectedIds) {
        var memory = new List<long>();
        var result = new Phase { State = state };
        var lastScan = ((Snapshot)Current.GetValue(panel)).At;
        {
            // Query this process directly. Process.WorkingSet64 on .NET Framework
            // builds system-wide process information and can inflate the probe's heap.
            IntPtr process = GetCurrentProcess();
            double startCpu = CpuMilliseconds(process);
            var elapsed = Stopwatch.StartNew();
            do {
                await Task.Delay(1000);
                if ((panel.Window!=null&&panel.Window.IsVisible) != (state == "visible")) throw new InvalidOperationException("Panel visibility changed during measurement.");
                var counters = new MemoryCounters { Size = (uint)Marshal.SizeOf(typeof(MemoryCounters)) };
                if (!GetProcessMemoryInfo(process, ref counters, counters.Size)) throw new System.ComponentModel.Win32Exception();
                memory.Add((long)counters.WorkingSet.ToUInt64());
                result.PeakPrivateMB = Math.Max(result.PeakPrivateMB, counters.PrivateUsage.ToUInt64() / 1048576.0);
                result.PeakManagedMB = Math.Max(result.PeakManagedMB, GC.GetTotalMemory(false) / 1048576.0);
                var snapshot = (Snapshot)Current.GetValue(panel);
                if (snapshot.Error != null || snapshot.TotalMemory <= 0) throw new InvalidOperationException("Live scan failed during measurement.");
                if (snapshot.Servers.Count < expectedServers) throw new InvalidOperationException("A resource test server was not detected.");
                if(expectedIds.Any(pid=>!snapshot.Servers.Any(s=>s.Processes.Any(p=>p.Pid==pid))))throw new InvalidOperationException("An owned resource test server disappeared from monitoring.");
                if (snapshot.At != lastScan) { result.Scans++; lastScan = snapshot.At; }
                result.Servers = Math.Max(result.Servers, snapshot.Servers.Count);
                result.MinimumServers = Math.Min(result.MinimumServers, snapshot.Servers.Count);
            } while (elapsed.Elapsed.TotalSeconds < seconds);
            result.Seconds = elapsed.Elapsed.TotalSeconds;
            result.CpuPercent = (CpuMilliseconds(process) - startCpu) / elapsed.Elapsed.TotalMilliseconds / Environment.ProcessorCount * 100;
        }
        if (result.Scans < 2) throw new InvalidOperationException("Too few live scans to measure monitoring overhead.");
        result.AverageWorkingSetMB = memory.Average(v => (double)v) / 1048576;
        result.PeakWorkingSetMB = memory.Max() / 1048576.0;
        return result;
    }
    static double CpuMilliseconds(IntPtr process) {
        long created, exited, kernel, user;
        if (!GetProcessTimes(process, out created, out exited, out kernel, out user)) throw new System.ComponentModel.Win32Exception();
        return (kernel + user) / 10000.0;
    }
    static int ServerFixture(string duration) {
        int seconds;
        if (!Int32.TryParse(duration, out seconds) || seconds < 30 || seconds > 1000) return 2;
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try {
            listener.Start();
            var elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed.TotalSeconds < seconds) Thread.Sleep(100);
        } finally { listener.Stop(); }
        return 0;
    }
}
