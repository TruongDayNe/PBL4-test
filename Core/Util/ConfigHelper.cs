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
        private static FileSystemWatcher _configWatcher;
        private static string _watchedConfigPath;
        
        /// <summary>
        /// Event fired khi config file thay đổi
        /// </summary>
        public static event Action OnConfigChanged;

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

                // Setup file watcher nếu chưa có
                if (_configWatcher == null || _watchedConfigPath != configPath)
                {
                    SetupFileWatcher(configPath);
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
        /// Setup FileSystemWatcher để tự động reload config khi file thay đổi
        /// </summary>
        private static void SetupFileWatcher(string configPath)
        {
            try
            {
                // Dispose watcher cũ nếu có
                _configWatcher?.Dispose();

                // Tìm file gốc trong project root (không phải file copy trong bin)
                var projectRootFile = FindProjectRootConfigFile(configPath);
                var watchPath = projectRootFile ?? configPath;

                var directory = Path.GetDirectoryName(watchPath);
                var fileName = Path.GetFileName(watchPath);

                _configWatcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };

                _configWatcher.Changed += OnConfigFileChanged;
                _watchedConfigPath = watchPath;

                Console.WriteLine($"🔍 Watching config file: {watchPath}");
                if (projectRootFile != null && projectRootFile != configPath)
                {
                    Console.WriteLine($"   (Project root file, will copy to: {configPath})");
                }
                Debug.WriteLine($"🔍 File watcher setup for: {watchPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Failed to setup file watcher: {ex.Message}");
                Debug.WriteLine($"⚠️ Failed to setup file watcher: {ex.Message}");
            }
        }

        /// <summary>
        /// Tìm file config trong project root
        /// </summary>
        private static string FindProjectRootConfigFile(string binConfigPath)
        {
            try
            {
                // Nếu file hiện tại trong bin/Debug, tìm file gốc trong project root
                var currentDir = Path.GetDirectoryName(binConfigPath);
                
                // Navigate up từ bin/Debug/net8.0-windows đến project root
                var projectRoot = currentDir;
                for (int i = 0; i < 5; i++) // Tối đa 5 levels up
                {
                    var parentDir = Directory.GetParent(projectRoot);
                    if (parentDir == null) break;
                    
                    projectRoot = parentDir.FullName;
                    var candidatePath = Path.Combine(projectRoot, "keymapping.json");
                    
                    if (File.Exists(candidatePath))
                    {
                        Console.WriteLine($"[ConfigHelper] Found project root config: {candidatePath}");
                        return candidatePath;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigHelper] Error finding project root: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// Handler khi config file thay đổi
        /// </summary>
        private static void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                // Đợi 200ms để file được ghi xong (tăng từ 100ms để chắc chắn)
                System.Threading.Thread.Sleep(200);

                Console.WriteLine($"🔄 Config file changed, reloading: {e.FullPath}");
                Debug.WriteLine($"🔄 Config file changed, reloading...");

                // Reload config từ file gốc
                _currentConfig = KeyMappingConfig.LoadFromFile(_watchedConfigPath);
                Console.WriteLine($"✓ Config reloaded from: {_watchedConfigPath}");
                Console.WriteLine(_currentConfig.ToReadableString());

                // Trigger event để các component khác biết config đã đổi
                OnConfigChanged?.Invoke();
                
                Console.WriteLine($"✓ Config change event fired to {OnConfigChanged?.GetInvocationList().Length ?? 0} subscribers");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error reloading config: {ex.Message}");
                Debug.WriteLine($"❌ Error reloading config: {ex.Message}");
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
