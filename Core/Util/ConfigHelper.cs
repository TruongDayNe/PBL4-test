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
        private static DateTime _lastLoadTime = DateTime.MinValue;
        
        /// <summary>
        /// Event fired khi config file thay đổi
        /// </summary>
        public static event Action OnConfigChanged;

        /// <summary>
        /// Get current loaded config
        /// </summary>
        public static KeyMappingConfig CurrentConfig => _currentConfig;

        /// <summary>
        /// Get path to project root config file (not bin copy)
        /// </summary>
        public static string GetProjectRootConfigPath()
        {
            string binPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keymapping.json");
            string projectRootPath = FindProjectRootConfigFile(binPath);
            
            if (projectRootPath != null)
                return projectRootPath;
            
            // Fallback to bin path
            return binPath;
        }

        /// <summary>
        /// Load config từ file (hoặc tạo mới nếu chưa có)
        /// </summary>
        public static KeyMappingConfig LoadConfig(string configPath = null)
        {
            try
            {
                if (configPath == null)
                {
                    // Dùng file trong bin folder (cùng folder với .exe)
                    configPath = KeyMappingConfig.GetDefaultConfigPath();
                    Console.WriteLine($"[ConfigHelper] Using bin config: {configPath}");
                }

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
                
                // Lưu thời gian load
                _lastLoadTime = DateTime.Now;
                Console.WriteLine($"⏰ Config loaded at: {_lastLoadTime:HH:mm:ss.fff}");

                return _currentConfig;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ CRITICAL: Failed to load config: {ex.Message}");
                Console.WriteLine($"Exception type: {ex.GetType().Name}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                // Log inner exception if exists
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    Console.WriteLine($"Inner stack trace: {ex.InnerException.StackTrace}");
                }
                
                Debug.WriteLine($"❌ CRITICAL: Failed to load config: {ex.Message}");
                
                // THROW EXCEPTION - không dùng default config nữa
                throw new InvalidOperationException(
                    "Failed to load keymapping.json. App cannot run without config file. " +
                    "Please ensure keymapping.json exists and System.Text.Json dependencies are correct.", 
                    ex);
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

                // LUÔN DÙNG FILE TRONG BIN FOLDER (không tìm project root nữa)
                var watchPath = configPath;

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
                _lastLoadTime = DateTime.Now;
                
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
            // Nếu chưa load lần nào, load ngay
            if (_currentConfig == null)
            {
                Console.WriteLine("⚠️ [ConfigHelper.GetConfig] Config not loaded yet, loading from file...");
                Debug.WriteLine("⚠️ Config not loaded yet, loading from file...");
                LoadConfig();
                return _currentConfig;
            }

            // Check xem file có thay đổi không (so sánh timestamp)
            try
            {
                // Dùng file trong bin (cùng folder với .exe)
                var configPath = _watchedConfigPath ?? KeyMappingConfig.GetDefaultConfigPath();
                if (File.Exists(configPath))
                {
                    var fileTime = File.GetLastWriteTime(configPath);
                    
                    // Reload nếu file time mới hơn last load time
                    // Hoặc nếu last load time chưa được set (lần đầu)
                    if (fileTime > _lastLoadTime || _lastLoadTime == DateTime.MinValue)
                    {
                        bool isFirstLoad = (_lastLoadTime == DateTime.MinValue);
                        
                        // Chỉ log nếu không phải lần đầu
                        if (!isFirstLoad)
                        {
                            Console.WriteLine($"🔄 [ConfigHelper.GetConfig] File changed detected!");
                            Console.WriteLine($"   File time: {fileTime:HH:mm:ss.fff}");
                            Console.WriteLine($"   Last load: {_lastLoadTime:HH:mm:ss.fff}");
                            Console.WriteLine("   Reloading config...");
                        }
                        
                        _currentConfig = KeyMappingConfig.LoadFromFile(configPath);
                        _lastLoadTime = DateTime.Now;
                        
                        // Fire event để các component khác biết (chỉ khi không phải lần đầu)
                        if (!isFirstLoad)
                        {
                            OnConfigChanged?.Invoke();
                        }
                        
                        Console.WriteLine($"✓ Config reloaded! W mapping: {(_currentConfig.KeyboardMapping.ContainsKey("W") ? _currentConfig.KeyboardMapping["W"] : "N/A")}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ [ConfigHelper.GetConfig] Error checking file timestamp: {ex.Message}");
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
            Console.WriteLine("🔄 Reloading config...");
            
            // Clear last load time để force reload lần tiếp theo
            _lastLoadTime = DateTime.MinValue;
            
            LoadConfig();
            
            // Fire event để notify các manager áp dụng config mới
            OnConfigChanged?.Invoke();
            Console.WriteLine("✓ Config reloaded and applied!");
            Debug.WriteLine("✓ Config reloaded and applied!");
        }
    }
}
