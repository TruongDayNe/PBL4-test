using System;
using System.IO;
using System.Text.Json;
using RealTimeUdpStream.Core.Models;

namespace TestJsonLoad
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Testing JSON Deserialization ===\n");

            string configPath = @"D:\PBL4\PBL4-test\WPFUI_NEW\bin\Debug\net8.0-windows\keymapping.json";
            
            Console.WriteLine($"Reading from: {configPath}");
            
            if (!File.Exists(configPath))
            {
                Console.WriteLine("❌ File not found!");
                return;
            }

            try
            {
                string jsonText = File.ReadAllText(configPath);
                Console.WriteLine("\n--- JSON Content ---");
                Console.WriteLine(jsonText);
                Console.WriteLine("--- End JSON ---\n");

                Console.WriteLine("Attempting deserialization...");
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                };

                var config = JsonSerializer.Deserialize<KeyMappingConfig>(jsonText, options);
                
                Console.WriteLine("\n✓ Deserialization SUCCESS!");
                Console.WriteLine($"\nKeyboard mappings loaded: {config.KeyboardMapping.Count}");
                
                foreach (var kvp in config.KeyboardMapping)
                {
                    Console.WriteLine($"  {kvp.Key} → {kvp.Value}");
                }

                if (config.KeyboardMapping.ContainsKey("W"))
                {
                    string mappedKey = config.KeyboardMapping["W"];
                    if (mappedKey == "Z")
                    {
                        Console.WriteLine("\n✓✓✓ W → Z mapping is CORRECT!");
                    }
                    else
                    {
                        Console.WriteLine($"\n❌ W → {mappedKey} (Expected Z)");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ FAILED: {ex.GetType().Name}");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"\nInner Exception: {ex.InnerException.GetType().Name}");
                    Console.WriteLine($"Inner Message: {ex.InnerException.Message}");
                    Console.WriteLine($"\nInner Stack:\n{ex.InnerException.StackTrace}");
                }
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
