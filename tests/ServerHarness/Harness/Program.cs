using System;
using System.IO;
using System.Linq;
using Frontier.Harness.Shim;
using Frontier.Networking;
using UnityEngine;

namespace Frontier.Harness
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            string root = RepositoryRoot();
            Resources.Root = Path.Combine(root, "Assets", "Frontier", "Resources");
            string mode = args.Length > 0 ? args[0] : "check";
            try
            {
                switch (mode)
                {
                    case "check": return Scenarios.Run(args.Contains("--quick"));
                    case "serve": return DevServer.Run(args);
                    case "hash": Console.WriteLine(MatchCompatibility.Key); return 0;
                    case "fixture": return Fixture.Write(args.Length > 1 ? args[1] : "fixture.json");
                    case "editor-checks":
                        Frontier.Editor.AllocatedServerChecks.Run();
                        Frontier.Editor.ServerLifecycleChecks.Run();
                        Console.WriteLine("FRONTIER_EDITOR_SERVER_CHECKS passed");
                        return 0;
                    default:
                        Console.Error.WriteLine("Usage: FrontierServerHarness check [--quick] | serve [options] | hash | fixture <path>");
                        return 2;
                }
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.Message);
                Console.Error.WriteLine("FRONTIER_HARNESS failed");
                return 1;
            }
        }

        private static string RepositoryRoot()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "Assets", "Frontier", "Resources", "catalog.json"))) return directory.FullName;
            for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory != null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "Assets", "Frontier", "Resources", "catalog.json"))) return directory.FullName;
            throw new InvalidOperationException("Run inside the repository: Assets/Frontier/Resources/catalog.json was not found.");
        }

        internal static string Option(string[] args, string name, string fallback)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }
    }
}
