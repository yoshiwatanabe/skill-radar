using System;
using System.IO;

namespace SkillRadar.Console.Services
{
    public static class EnvironmentLoader
    {
        public static void LoadFromFile(string filePath = ".env")
        {
            // Look for .env file in current directory and up the directory tree
            var currentDir = Directory.GetCurrentDirectory();
            var envPath = FindEnvFile(currentDir, filePath);
            
            if (envPath != null && File.Exists(envPath))
            {
                LoadEnvironmentFile(envPath);
                System.Console.WriteLine($"⚙️  Loaded environment variables from {envPath}");
            }
            else
            {
                System.Console.WriteLine("⚙️  No .env file found, using system environment variables");
            }
        }


        private static string? FindEnvFile(string startDir, string fileName)
        {
            var currentDir = new DirectoryInfo(startDir);
            
            while (currentDir != null)
            {
                var envPath = Path.Combine(currentDir.FullName, fileName);
                if (File.Exists(envPath))
                {
                    return envPath;
                }
                currentDir = currentDir.Parent;
            }
            
            return null;
        }

        private static void LoadEnvironmentFile(string filePath)
        {
            try
            {
                var lines = File.ReadAllLines(filePath);
                
                foreach (var line in lines)
                {
                    // Skip empty lines and comments
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;
                    
                    // Parse KEY=VALUE format
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();
                        
                        // Remove quotes if present
                        if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                            (value.StartsWith("'") && value.EndsWith("'")))
                        {
                            value = value.Substring(1, value.Length - 2);
                        }
                        
                        // Only set if not already in environment (system env takes precedence)
                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                        {
                            Environment.SetEnvironmentVariable(key, value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"⚠️  Error loading .env file: {ex.Message}");
            }
        }
    }
}