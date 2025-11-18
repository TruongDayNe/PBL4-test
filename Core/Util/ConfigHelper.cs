using RealTimeUdpStream.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RealTimeUdpStream.Core.Util
{
    /// <summary>
    /// Helper class để load và apply key mapping config
    /// </summary>
    public static class ConfigHelper
    {
        private static KeyMappingConfig _currentConfig;

        /// <summary>
        /// Load config từ file (hoặc tạo mới nếu chưa có)
        /// </summary>
        public static KeyMappingConfig LoadConfig(string configPath = null)
        {
            try
            {
                if (configPath == null)
                    configPath = KeyMappingConfig.GetDefaultConfigPath();

                Console.WriteLine($"[ConfigHelper] Loading config from: {configPath}");
                _currentConfig = KeyMappingConfig.LoadFromFile(configPath);
                
                if (_currentConfig.Validate())
                {
                    Console.WriteLine("✓ Config loaded successfully!");
                    Debug.WriteLine("✓ Config loaded successfully!");
                    Console.WriteLine(_currentConfig.ToReadableString());
                    Debug.WriteLine(_currentConfig.ToReadableString());
                }
                else
                {
                    Console.WriteLine("⚠️ Config validation failed, using anyway");
                    Debug.WriteLine("⚠️ Config validation failed, using anyway");
                }

                return _currentConfig;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load config: {ex.Message}");
                Console.WriteLine($"Exception type: {ex.GetType().Name}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                // Log inner exception if exists
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    Console.WriteLine($"Inner stack trace: {ex.InnerException.StackTrace}");
                }
                
                Debug.WriteLine($"❌ Failed to load config: {ex.Message}");
                Debug.WriteLine("Using default config...");
                Console.WriteLine("Using default config...");
                _currentConfig = KeyMappingConfig.CreateDefault();
                return _currentConfig;
            }
        }

        /// <summary>
        /// Get current loaded config
        /// </summary>
        public static KeyMappingConfig GetConfig()
        {
            if (_currentConfig == null)
            {
                Console.WriteLine("⚠️ [ConfigHelper.GetConfig] Config not loaded yet, loading from file...");
                Debug.WriteLine("⚠️ Config not loaded yet, loading from file...");
                LoadConfig();
            }

            Console.WriteLine($"[ConfigHelper.GetConfig] Returning config with {_currentConfig.KeyboardMapping.Count} keyboard mappings");
            
            // Log first mapping to verify
            if (_currentConfig.KeyboardMapping.Count > 0)
            {
                var first = _currentConfig.KeyboardMapping.First();
                Console.WriteLine($"[ConfigHelper.GetConfig] First mapping: {first.Key} → {first.Value}");
                
                // Specifically check W mapping
                if (_currentConfig.KeyboardMapping.ContainsKey("W"))
                {
                    Console.WriteLine($"[ConfigHelper.GetConfig] ✓ W → {_currentConfig.KeyboardMapping["W"]}");
                }
            }
            
            return _currentConfig;
        }

        /// <summary>
        /// Save current config
        /// </summary>
        public static void SaveConfig(string configPath = null)
        {
            if (_currentConfig == null)
            {
                Debug.WriteLine("❌ No config to save");
                return;
            }

            if (configPath == null)
                configPath = KeyMappingConfig.GetDefaultConfigPath();

            _currentConfig.SaveToFile(configPath);
        }

        /// <summary>
        /// Create AudioConfig from loaded KeyMappingConfig
        /// </summary>
        public static AudioConfig CreateAudioConfig()
        {
            var config = GetConfig();
            var audioSettings = config.AudioSettings;

            if (audioSettings.Codec.ToUpper() == "OPUS")
            {
                return AudioConfig.CreateOpusConfig(audioSettings.Bitrate);
            }
            else
            {
                return AudioConfig.CreateDefault(); // PCM16
            }
        }

        /// <summary>
        /// Get keyboard mapping dictionary
        /// </summary>
        public static Dictionary<string, string> GetKeyboardMapping()
        {
            return GetConfig().KeyboardMapping;
        }

        /// <summary>
        /// Get controller mapping dictionary
        /// </summary>
        public static Dictionary<string, ControllerAction> GetControllerMapping()
        {
            return GetConfig().ControllerMapping;
        }

        /// <summary>
        /// Export config summary to text file
        /// </summary>
        public static void ExportConfigSummary(string outputPath)
        {
            var config = GetConfig();
            var summary = config.ToReadableString();
            
            File.WriteAllText(outputPath, summary);
            Debug.WriteLine($"✓ Config summary exported to: {outputPath}");
        }

        /// <summary>
        /// Reload config from file (useful for hot-reload)
        /// </summary>
        public static void ReloadConfig()
        {
            Debug.WriteLine("🔄 Reloading config...");
            LoadConfig();
        }
    }
}
