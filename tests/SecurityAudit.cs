using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using WhatThePort;

// Evidence probes, not a passing security certification. Each FINDING is an observed weakness.
public static class SecurityAudit {
    static int findings;
    static void Finding(string name,bool present){Console.WriteLine((present?"FINDING ":"NOT REPRODUCED ")+name);if(present)findings++;}
    public static int Main(string[] args){
        string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
        string original=Environment.CurrentDirectory;
        try{
            string project=Path.Combine(root,"untrusted-project");Directory.CreateDirectory(project);
            string outside=Path.Combine(root,"outside-metadata");Directory.CreateDirectory(outside);
            string marker="AUDIT_PUBLIC_MARKER_ONLY";File.WriteAllText(Path.Combine(outside,"HEAD"),"ref: refs/heads/"+marker);
            File.WriteAllText(Path.Combine(project,".git"),"gitdir: "+outside);
            Environment.CurrentDirectory=project;
            var server=new Server{Key="audit-project",Port=3000};
            typeof(Scanner).GetMethod("ResolveProject",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(new Scanner(),new object[]{server,Process.GetCurrentProcess().Id,DateTime.UtcNow});
            Finding("S1 project metadata follows a path outside the project",server.Branch==marker);
            // Resolve only; deliberately do not contact an SMB server or any remote address.
            string remote=Path.GetFullPath(Path.Combine(project,@"\\audit.invalid\share\git"));
            Finding("S1 UNC gitdir path is accepted by path construction (no network performed)",remote.StartsWith(@"\\audit.invalid\"));

            // The fixture executable ignores its arguments and writes only its own marker file.
            string fixture=Path.Combine(root,"wt-fixture.exe"),planted=Path.Combine(project,"wt.exe");
            if(!File.Exists(fixture))throw new Exception("Missing harmless terminal fixture.");
            File.Copy(fixture,planted,true);Launchers.Terminal(project,"Windows Terminal",null);
            string launched=Path.Combine(project,"terminal-fixture-ran.txt");
            for(int i=0;i<20&&!File.Exists(launched);i++)Thread.Sleep(50);
            Finding("S2 bare wt.exe executes the fixture in the process current directory",File.Exists(launched));

            DateTime now=DateTime.UtcNow;
            var processes=new Dictionary<int,ProcessNode>{
                {100,new ProcessNode{Pid=100,Name="node",Started=now.AddHours(-2)}},
                {200,new ProcessNode{Pid=200,Parent=100,Name="worker",Started=now.AddMinutes(-10)}},
                {300,new ProcessNode{Pid=300,Parent=200,Name="unrelated-orphan",Started=now.AddHours(-1)}}};
            var tree=Policy.Tree(100,processes,new HashSet<int>{100});
            Finding("S3 stale parent PID adopts an older orphan which still passes root-age filtering",tree.Any(n=>n.Pid==300&&n.Started>=processes[100].Started)&&processes[300].Started<processes[200].Started);

            var idle=new Server{SampledAt=now,Observed=now.AddHours(-5),LastActive=now.AddHours(-5),Connections=0,Cpu=0};
            idle.Processes.Add(new ProcessNode{Pid=100,Name="node",Cpu=0});
            idle.Processes.Add(new ProcessNode{Pid=200,Parent=100,Name="worker",Cpu=0});
            Finding("S4 cleanup predicate has no per-process sample completeness or child-connection gate",Policy.CleanupCandidate(idle,new Settings(),now));

            string storage=Path.Combine(root,"isolated-store");Directory.CreateDirectory(storage);
            File.WriteAllText(Path.Combine(storage,"links.json"),"[{\"Port\":3000,\"Folder\":null}]");
            bool threw=false;try{new Store(storage).LinkFor(new Server{Port=3000,Folder=project});}catch(NullReferenceException){threw=true;}
            Finding("S5 a null link folder escapes loading validation and crashes lookup",threw);
            Console.WriteLine("Evidence observations: "+findings+" (S1 has two observations). No real user process was stopped; no remote request was made.");
            return findings==6?0:1;
        }catch(Exception e){Console.WriteLine("Audit probe error: "+e);return 1;}
        finally{Environment.CurrentDirectory=original;}
    }
}
