using System;
using System.IO;
using System.Threading.Tasks;

namespace Identity.Tests;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            // Project root is 2 levels up from tests/Identity.Tests, or relative to current directory
            var currentDir = Directory.GetCurrentDirectory();
            var projectRoot = currentDir;
            if (currentDir.EndsWith("Identity.Tests", StringComparison.OrdinalIgnoreCase))
            {
                projectRoot = Path.GetFullPath(Path.Combine(currentDir, "..", ".."));
            }
            else if (!File.Exists(Path.Combine(projectRoot, "ToeicLearning.sln")))
            {
                var search = currentDir;
                while (!string.IsNullOrEmpty(search) && !File.Exists(Path.Combine(search, "ToeicLearning.sln")))
                {
                    var parent = Directory.GetParent(search);
                    if (parent == null) break;
                    search = parent.FullName;
                }
                if (File.Exists(Path.Combine(search, "ToeicLearning.sln")))
                {
                    projectRoot = search;
                }
            }

            Console.WriteLine($"Starting Identity Service Full Test Suite with Root: {projectRoot}");
            var suite = await IdentityTestSuite.InitializeAsync(projectRoot);
            await suite.RunAllTestsAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR IN TEST SUITE]: {ex.Message}\n{ex.StackTrace}");
            Console.ResetColor();
            return 1;
        }
    }
}
