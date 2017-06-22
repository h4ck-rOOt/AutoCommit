using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace AutoCommit
{
    class Program
    {
        const string CONFIG_FILE_NAME = "autocommit.conf";
        const string PROJECT_NAME = "AutoCommit";

        static bool dirty = false, doAdd = true, doPush = false, autoCommit = false;
        static Dictionary<string, string> config = new Dictionary<string, string>();
        static string watchPath = string.Empty, projectPath = string.Empty;
        static ManualResetEventSlim resetEvent;
        static FileSystemWatcher watcher;
        static Timer timer;
        static string author, commitMessage;
        static int commitInterval;

        static void Main(string[] args)
        {
            var configFile = CONFIG_FILE_NAME;

            if (args.Length > 0)
                configFile = args[0]; // conf name

            ReadConfig(configFile);

            if (!Init())
                return;

            Log($"WatchPath = {watchPath}");
            Log($"ProjectPath = {projectPath}");
            Log($"Author = {author}");
            Log($"CommitMessage = {commitMessage}");
            Log($"CommitInterval = {TimeSpan.FromSeconds(commitInterval)}");
            Log($"DoAdd = {doAdd}");
            Log($"DoPush = {doPush}");
            Log($"AutoCommit = {autoCommit}");

            resetEvent = new ManualResetEventSlim(false);

            watcher = new FileSystemWatcher(watchPath, "*.*");

            watcher.Changed += Watcher_Changed; // alle change events (+created)
            //watcher.Created += Watcher_Changed; // dateien erzeugen, bereits in changed
            watcher.Deleted += Watcher_Changed; // directory ändert sich bei deleted
            watcher.Renamed += Watcher_Changed;

            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;

            timer = new Timer(Timer_Tick, null, 0, (int)TimeSpan.FromSeconds(commitInterval).TotalMilliseconds);

            Log("ResetEvent wait...");
            resetEvent.Wait();
        }

        private static bool Init()
        {
            if (config.ContainsKey("WatchPath"))
                watchPath = config["WatchPath"];
            else
            {
                Console.WriteLine("WatchPath should be submitted in autocommit.conf");
                return false;
            }

            if (config.ContainsKey("ProjectPath"))
                projectPath = config["ProjectPath"];
            else
            {
                Console.WriteLine("ProjectPath is missing...");
                return false;
            }

            author = GetConfigValue("Author", "AutoCommit Bot <autocommit@local.int>");
            commitMessage = GetConfigValue("CommitMessage", "autocommit");

            if (!bool.TryParse(GetConfigValue("DoAdd", "true"), out doAdd))
                doAdd = true;

            if (!bool.TryParse(GetConfigValue("DoPush", "false"), out doPush))
                doPush = false;

            if (!bool.TryParse(GetConfigValue("AutoCommit", "false"), out autoCommit))
                autoCommit = false;

            if (!int.TryParse(GetConfigValue("CommitInterval", "1800"), out commitInterval))
                commitInterval = 1800;

            return true;
        }

        private static string GetConfigValue(string id, string defaultValue)
        {
            return config.ContainsKey(id) ? config[id] : defaultValue;
        }

        private static void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(e.FullPath) && Directory.Exists(e.FullPath))
                Log("Directory changed: " + e.FullPath);
            else
                Log($"File changed ({e.ChangeType}): {e.FullPath}");

            dirty = true;
        }

        private static void Timer_Tick(object args)
        {
            if (dirty)
            {
                int exit = 0;

                timer.Change(Timeout.Infinite, Timeout.Infinite);
                watcher.EnableRaisingEvents = false;
                dirty = false;

                var tmpCommitMessage = commitMessage;

                if (doAdd)
                {
                    var proc_add = Process.Start(new ProcessStartInfo("git", "add *") { WorkingDirectory = projectPath, CreateNoWindow = true });
                    proc_add.WaitForExit();
                    exit = proc_add.ExitCode;
                    Log("Git add exited with code: " + exit);
                }

                if(!autoCommit)
                {
                    Console.Beep();
                    Console.Beep();
                    Console.Beep();
                    Log("Please input a commit message:");
                    string input = Console.ReadLine();

                    if (!string.IsNullOrWhiteSpace(input))
                        tmpCommitMessage = input;
                }

                var proc_commit = Process.Start(new ProcessStartInfo("git", $"commit -m \"{tmpCommitMessage}\" --author=\"{author}\"") { WorkingDirectory = projectPath, CreateNoWindow = true });
                proc_commit.WaitForExit();
                exit = proc_commit.ExitCode;

                Log("Git commit exited with code: " + exit);

                if (doPush)
                {
                    var proc_push = Process.Start(new ProcessStartInfo("git", "push") { WorkingDirectory = projectPath, CreateNoWindow = true });
                    proc_push.WaitForExit();
                    exit = proc_push.ExitCode;

                    Log("Git push exited with code: " + exit);
                }

                watcher.EnableRaisingEvents = true;
                timer.Change(0, commitInterval);
            }
        }

        private static void ReadConfig(string configFileName)
        {
            if (File.Exists(configFileName))
            {
                foreach (var line in File.ReadAllLines(configFileName))
                {
                    var current = line;
                    int comment = current.IndexOf('#');

                    if (comment != -1)
                        current = current.Remove(comment);

                    if (current.Contains("="))
                    {
                        var splitted = current.Split('=');

                        if (splitted.Length != 2)
                            continue;

                        var key = splitted[0].Trim();
                        var value = splitted[1].Trim();

                        if (config.ContainsKey(key))
                            config[key] = value;
                        else
                            config.Add(key, value);
                    }
                }
            }
        }

        static void Log(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Console.WriteLine($"[{PROJECT_NAME}:{Path.GetFileName(sourceFilePath)}@{sourceLineNumber}]: {message}");
        }
    }
}