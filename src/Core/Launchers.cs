using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WhatThePort {
    public static class Launchers {
        public static string QuotePowerShell(string text) { return "'" + (text ?? "").Replace("'", "''") + "'"; }
        public static string SessionScript(ProjectLink link) {
            link.Validate();
            if (String.IsNullOrEmpty(link.Session)) throw new ArgumentException("Register the exact session ID first.");
            return "Set-Location -LiteralPath " + QuotePowerShell(link.Folder) + "; & " + (link.Agent == "Codex" ? "codex resume " : "claude --resume ") + QuotePowerShell(link.Session);
        }
        public static void Terminal(string folder, string terminal, ProjectLink session) {
            if (String.IsNullOrEmpty(folder) || !Directory.Exists(folder)) throw new IOException("The project folder is unavailable.");
            string script=session == null ? "Set-Location -LiteralPath " + QuotePowerShell(folder) : SessionScript(session);
            string args="-NoLogo -NoExit -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            if (terminal == "Windows Terminal") {
                try { Process.Start(new ProcessStartInfo("wt.exe", "new-tab powershell.exe " + args) { UseShellExecute=false, CreateNoWindow=true, WorkingDirectory=folder }); return; }
                catch (System.ComponentModel.Win32Exception) { throw new IOException("Windows Terminal was not found. Choose PowerShell in Settings."); }
            }
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"), args) { UseShellExecute=true, WorkingDirectory=folder });
        }
        public static void OpenLocal(Server server) { Process.Start(new ProcessStartInfo(server.Url) { UseShellExecute=true }); }
        public static void OpenPreview(ProjectLink link) {
            if (link == null || !ProjectLink.ValidPreview(link.Preview)) throw new ArgumentException("Register a Vercel preview URL first.");
            Process.Start(new ProcessStartInfo(link.Preview) { UseShellExecute=true });
        }
    }
}
