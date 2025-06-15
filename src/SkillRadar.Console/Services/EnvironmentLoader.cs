using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace SkillRadar.Console.Services
{
    public static class EnvironmentLoader
    {
        private static readonly string[] RequiredSecrets = new[]
        {
            "OPENAI_API_KEY",
            "NEWS_API_KEY", 
            "REDDIT_CLIENT_ID",
            "REDDIT_CLIENT_SECRET",
            "AZURE_STORAGE_CONNECTION_STRING",
            "AZURE_COMMUNICATION_CONNECTION_STRING",
            "EMAIL_SENDER_ADDRESS",
            "EMAIL_RECIPIENT_ADDRESS"
        };

        public static async Task LoadAsync(string filePath = ".env")
        {
            // First, try to load from Key Vault if available
            await TryLoadFromKeyVaultAsync();
            
            // Then, load from .env file (will not override existing environment variables)
            LoadFromFile(filePath);
        }

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

        private static async Task TryLoadFromKeyVaultAsync()
        {
            try
            {
                var keyVaultUri = Environment.GetEnvironmentVariable("AZURE_KEYVAULT_URI");
                if (string.IsNullOrEmpty(keyVaultUri))
                {
                    System.Console.WriteLine("🔑 No AZURE_KEYVAULT_URI found, skipping Key Vault");
                    return;
                }

                System.Console.WriteLine($"🔑 Loading secrets from Key Vault: {keyVaultUri}");
                
                var credential = new DefaultAzureCredential();
                var client = new SecretClient(new Uri(keyVaultUri), credential);

                foreach (var secretName in RequiredSecrets)
                {
                    // Only load from Key Vault if not already set in environment
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(secretName)))
                    {
                        try
                        {
                            var secretResponse = await client.GetSecretAsync(ConvertToKebabCase(secretName));
                            Environment.SetEnvironmentVariable(secretName, secretResponse.Value.Value);
                            System.Console.WriteLine($"✅ Loaded {secretName} from Key Vault");
                        }
                        catch (Exception ex)
                        {
                            System.Console.WriteLine($"⚠️  Failed to load {secretName} from Key Vault: {ex.Message}");
                        }
                    }
                    else
                    {
                        System.Console.WriteLine($"🔄 {secretName} already set, skipping Key Vault");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"⚠️  Key Vault connection failed: {ex.Message}");
                System.Console.WriteLine("🔄 Falling back to environment variables/.env file");
            }
        }

        private static string ConvertToKebabCase(string input)
        {
            // Convert OPENAI_API_KEY to openai-api-key
            return input.ToLowerInvariant().Replace('_', '-');
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