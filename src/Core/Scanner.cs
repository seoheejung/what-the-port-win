using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace WhatThePort {
    public sealed class Scanner {
        sealed class Previous { public DateTime Start, At; public double Cpu; }
        readonly Dictionary<int, Previous> previous = new Dictionary<int, Previous>();
        readonly Dictionary<string, Server> tracked = new Dictionary<string, Server>();
        readonly Dictionary<string, Tuple<DateTime,string,string>> projects = new Dictionary<string, Tuple<DateTime,string,string>>();
        static readonly HashSet<string> Runtimes = new HashSet<string>(new [] { "node", "bun", "deno", "python", "python3", "pythonw", "uv", "uvicorn", "gunicorn", "ruby", "java", "javaw", "dotnet", "php", "go", "air", "cargo", "beam.smp", "elixir", "redis-server", "postgres", "mongod", "mysqld", "sqlservr", "wtp-tests" });
        public Snapshot Scan(Settings settings) {
            var output = new Snapshot(); DateTime now=output.At;
            var sockets=Native.Sockets(); var processes=Native.Processes();
            var listeners = new HashSet<int>(sockets.Where(s => s.State == 2).Select(s => s.Pid));
            var protectedIds=Policy.Ancestors(Process.GetCurrentProcess().Id, processes);
            var sampled=new Dictionary<int, ProcessNode>(); var live = new HashSet<string>();
            foreach (var group in sockets.Where(s => s.State == 2 && s.Port >= 1024).GroupBy(s => s.Pid)) {
                ProcessNode root;
                if (!processes.TryGetValue(group.Key, out root) || (!settings.AllListeners && !Runtimes.Contains(root.Name)) || !Native.IsOwned(root.Pid)) continue;
                var nodes = Policy.Tree(root.Pid, processes, listeners); var measured = new List<ProcessNode>();
                foreach (var node in nodes) {
                    ProcessNode value;
                    if (!sampled.TryGetValue(node.Pid, out value)) {
                        try {
                            using (var p=Process.GetProcessById(node.Pid)) {
                                var start=p.StartTime.ToUniversalTime(); double cpuTime=p.TotalProcessorTime.TotalMilliseconds; double cpu=0;
                                Previous old;
                                if (previous.TryGetValue(node.Pid,out old) && Native.SameStart(start,old.Start) && now > old.At) cpu=Math.Max(0, Math.Min(100, (cpuTime-old.Cpu)/(now-old.At).TotalMilliseconds/Environment.ProcessorCount*100));
                                previous[node.Pid]=new Previous { Start=start, At=now, Cpu=cpuTime };
                                value=new ProcessNode { Pid=node.Pid, Parent=node.Parent, Name=node.Name, Started=start, Memory=p.WorkingSet64, Cpu=cpu, Depth=node.Depth };
                                sampled[node.Pid]=value;
                            }
                        } catch { continue; }
                    }
                    if(measured.Count>0&&value.Started<measured[0].Started)continue;
                    measured.Add(value);
                }
                var rootValue=measured.FirstOrDefault(p => p.Pid == root.Pid); if (rootValue == null) continue;
                var ports=group.Select(s => s.Port).Distinct().OrderBy(p => p).ToList();
                string key=rootValue.Identity;
                Server oldServer; tracked.TryGetValue(key,out oldServer);
                bool continuous=oldServer != null && (now-oldServer.SampledAt).TotalSeconds <= Math.Max(15,settings.ScanSeconds*3);
                var server=new Server { Key=key, Port=ports[0], Ports=ports, Address=group.OrderBy(s => s.Address.Contains(":") ? 1 : 0).First().Address, Name=root.Name,
                    Started=rootValue.Started, Observed=continuous ? oldServer.Observed : now, LastActive=continuous ? oldServer.LastActive : now, SampledAt=now,
                    Memory=measured.Sum(p => p.Memory), Cpu=measured.Sum(p => p.Cpu), Processes=measured,
                    Protected=Policy.IsProtectedName(root.Name) || protectedIds.Contains(root.Pid),
                    Connections=sockets.Count(s => s.State == 5 && s.Pid == root.Pid && ports.Contains(s.Port)),
                    History=oldServer == null ? new List<Sample>() : new List<Sample>(oldServer.History) };
                if (server.Connections > 0 || server.Cpu >= 2) server.LastActive=now;
                ResolveProject(server,root.Pid,now);
                foreach (int ancestor in Policy.Ancestors(root.Pid,processes)) {
                    ProcessNode p; if (processes.TryGetValue(ancestor,out p) && (p.Name == "codex" || p.Name == "claude")) { server.Agent=p.Name == "codex" ? "Codex" : "Claude Code"; break; }
                }
                server.History.Add(new Sample { At=now, Memory=server.Memory, Cpu=server.Cpu }); Policy.PruneHistory(server.History,now);
                server.Warning=Policy.Warning(server,settings);
                tracked[key]=server; live.Add(key); output.Servers.Add(server);
            }
            foreach (var key in tracked.Keys.Where(k => !live.Contains(k)).ToList()) { tracked.Remove(key); projects.Remove(key); }
            foreach (var pid in previous.Keys.Where(p => !sampled.ContainsKey(p)).ToList()) previous.Remove(pid);
            output.Servers=output.Servers.OrderBy(s => s.Port).ToList();
            Native.Memory(out output.TotalMemory,out output.FreeMemory);
            return output;
        }
        void ResolveProject(Server s,int pid,DateTime now) {
            Tuple<DateTime,string,string> cached;
            if (!projects.TryGetValue(s.Key,out cached) || (now-cached.Item1).TotalSeconds > 30) {
                string folder=Native.CurrentDirectory(pid), branch=null;
                if (!String.IsNullOrEmpty(folder)) {
                    try {
                        var dir=new DirectoryInfo(folder);
                        for (int i=0;dir != null && i<12;i++,dir=dir.Parent) {
                            string git=Path.Combine(dir.FullName,".git");
                            if (File.Exists(git)) {
                                string marker=SmallText(git).Trim(); if (marker.StartsWith("gitdir: ",StringComparison.Ordinal)) git=Path.GetFullPath(Path.Combine(dir.FullName,marker.Substring(8)));
                            }
                            string head=Path.Combine(git,"HEAD");
                            if (File.Exists(head)) { string value=SmallText(head).Trim(); branch=value.StartsWith("ref: refs/heads/",StringComparison.Ordinal) ? value.Substring(16) : value.Substring(0,Math.Min(8,value.Length)); break; }
                        }
                    } catch { }
                }
                cached=Tuple.Create(now,folder,branch); projects[s.Key]=cached;
            }
            s.Folder=cached.Item2; s.Branch=cached.Item3;
            if (!String.IsNullOrEmpty(s.Folder)) { string name=Path.GetFileName(s.Folder.TrimEnd('\\','/')); if (!String.IsNullOrWhiteSpace(name)) s.Name=name; }
        }
        static string SmallText(string path) { using(var r=new StreamReader(path)) { char[] buffer=new char[4096]; int count=r.Read(buffer,0,buffer.Length); return new string(buffer,0,count); } }
    }
    public static class ProcessControl {
        public static List<string> Stop(Server server) {
            if (server.Protected) return new List<string> {"This server is protected."};
            if ((DateTime.UtcNow-server.SampledAt).TotalSeconds > 30) return new List<string> {"Server data is stale. Refresh before stopping."};
            var errors=new List<string>();
            // Stop the listener first, then the observed descendants. Never signal a parent shell.
            foreach (var node in server.Processes.OrderBy(p => p.Depth)) { string error; if (!Native.Stop(node,out error)) errors.Add(node.Name+" ("+node.Pid+"): "+error); }
            return errors;
        }
    }
}
