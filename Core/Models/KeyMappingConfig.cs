using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RealTimeUdpStream.Core.Models
{
    /// <summary>
    /// Cấu hình mapping phím từ CLIENT sang HOST
    /// </summary>
    public class KeyMappingConfig
    {
        /// <summary>
        /// Keyboard mapping: CLIENT key → HOST key
        /// VD: W → T, A → F, S → G, D → H
        /// </summary>
        public Dictionary<string, string> KeyboardMapping { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Controller mapping: CLIENT key → Xbox controller button/axis
        /// VD: I → LeftStickUp, J → LeftStickLeft, O → ButtonA
        /// </summary>
        public Dictionary<string, ControllerAction> ControllerMapping { get; set; } = new Dictionary<string, ControllerAction>();

        /// <summary>
        /// Audio codec configuration
        /// </summary>
        public AudioCodecSettings AudioSettings { get; set; } = new AudioCodecSettings();

        /// <summary>
        /// Tạo cấu hình mặc định (WASD→TFGH, IJKL→Joystick, O→ButtonA)
        /// </summary>
        public static KeyMappingConfig CreateDefault()
        {
            return new KeyMappingConfig
            {
                KeyboardMapping = new Dictionary<string, string>
                {
                    { "W", "T" },
                    { "A", "F" },
                    { "S", "G" },
                    { "D", "H" }
                },
                ControllerMapping = new Dictionary<string, ControllerAction>
                {
                    { "I", new ControllerAction { Type = ControllerActionType.LeftStickUp } },
                    { "J", new ControllerAction { Type = ControllerActionType.LeftStickLeft } },
                    { "K", new ControllerAction { Type = ControllerActionType.LeftStickDown } },
                    { "L", new ControllerAction { Type = ControllerActionType.LeftStickRight } },
                    { "O", new ControllerAction { Type = ControllerActionType.ButtonA } },
                    { "P", new ControllerAction { Type = ControllerActionType.ButtonB } },
                    { "U", new ControllerAction { Type = ControllerActionType.ButtonX } },
                    { "Y", new ControllerAction { Type = ControllerActionType.ButtonY } }
                },
                AudioSettings = new AudioCodecSettings
                {
                    Codec = "OPUS",
                    Bitrate = 96000,
                    SampleRate = 48000,
                    Channels = 2
                }
            };
        }

        /// <summary>
        /// Lưu config vào file JSON
        /// </summary>
        public void SaveToFile(string filePath)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(filePath, json);
                Console.WriteLine($"✓ Config saved to: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to save config: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Load config từ file JSON
        /// </summary>
        public static KeyMappingConfig LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"⚠️ Config file not found: {filePath}");
                    Console.WriteLine("Creating default config...");
                    var defaultConfig = CreateDefault();
                    defaultConfig.SaveToFile(filePath);
                    return defaultConfig;
                }

                string json = File.ReadAllText(filePath);
                
                Console.WriteLine("--- JSON Content (first 500 chars) ---");
                Console.WriteLine(json.Length > 500 ? json.Substring(0, 500) + "..." : json);
                Console.WriteLine("--- Deserializing ---");
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                };
                
                var config = JsonSerializer.Deserialize<KeyMappingConfig>(json, options);
                
                if (config == null)
                {
                    Console.WriteLine("⚠️ Failed to parse config, using default");
                    return CreateDefault();
                }

                Console.WriteLine($"✓ Config loaded from: {filePath}");
                Console.WriteLine($"  - Keyboard mappings: {config.KeyboardMapping.Count}");
                
                // Verify W mapping specifically
                if (config.KeyboardMapping.ContainsKey("W"))
                {
                    Console.WriteLine($"  ✓ W is mapped to: {config.KeyboardMapping["W"]}");
                }
                else
                {
                    Console.WriteLine("  ❌ W mapping NOT FOUND in config!");
                }
                
                // Debug: print first few keyboard mappings
                int count = 0;
                foreach (var kvp in config.KeyboardMapping)
                {
                    Console.WriteLine($"    {kvp.Key} → {kvp.Value}");
                    if (++count >= 5) break;
                }
                
                Console.WriteLine($"  - Controller mappings: {config.ControllerMapping.Count}");
                Console.WriteLine($"  - Audio codec: {config.AudioSettings.Codec} @ {config.AudioSettings.Bitrate / 1000}kbps");
                
                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load config: {ex.GetType().Name}");
                Console.WriteLine($"   Message: {ex.Message}");
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                }
                
                Console.WriteLine($"   Stack: {ex.StackTrace?.Split('\n')[0]}"); // First line only
                Console.WriteLine("Using default config...");
                return CreateDefault();
            }
        }

        /// <summary>
        /// Get default config file path
        /// </summary>
        public static string GetDefaultConfigPath()
        {
            string appFolder = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appFolder, "keymapping.json");
        }

        /// <summary>
        /// Validate config
        /// </summary>
        public bool Validate()
        {
            // Check keyboard mapping
            if (KeyboardMapping == null || KeyboardMapping.Count == 0)
            {
                Console.WriteLine("⚠️ Warning: No keyboard mappings defined");
            }

            // Check controller mapping
            if (ControllerMapping == null || ControllerMapping.Count == 0)
            {
                Console.WriteLine("⚠️ Warning: No controller mappings defined");
            }

            // Check audio settings
            if (AudioSettings == null)
            {
                Console.WriteLine("❌ Error: AudioSettings is null");
                return false;
            }

            if (AudioSettings.SampleRate <= 0 || AudioSettings.Channels <= 0)
            {
                Console.WriteLine("❌ Error: Invalid audio settings");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Export to readable text format
        /// </summary>
        public string ToReadableString()
        {
            var lines = new List<string>();
            lines.Add("========== KEY MAPPING CONFIGURATION ==========");
            lines.Add("");
            
            lines.Add("KEYBOARD MAPPING (Client → Host):");
            foreach (var kvp in KeyboardMapping.OrderBy(x => x.Key))
            {
                lines.Add($"  {kvp.Key} → {kvp.Value}");
            }
            lines.Add("");
            
            lines.Add("CONTROLLER MAPPING (Client → Xbox Controller):");
            foreach (var kvp in ControllerMapping.OrderBy(x => x.Key))
            {
                lines.Add($"  {kvp.Key} → {kvp.Value.Type}");
            }
            lines.Add("");
            
            lines.Add("AUDIO SETTINGS:");
            lines.Add($"  Codec: {AudioSettings.Codec}");
            lines.Add($"  Bitrate: {AudioSettings.Bitrate / 1000} Kbps");
            lines.Add($"  Sample Rate: {AudioSettings.SampleRate} Hz");
            lines.Add($"  Channels: {AudioSettings.Channels}");
            lines.Add("");
            lines.Add("===============================================");
            
            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// Controller action type
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ControllerActionType
    {
        // Left Stick
        LeftStickUp,
        LeftStickDown,
        LeftStickLeft,
        LeftStickRight,

        // Right Stick
        RightStickUp,
        RightStickDown,
        RightStickLeft,
        RightStickRight,

        // Buttons
        ButtonA,
        ButtonB,
        ButtonX,
        ButtonY,

        // Shoulders
        LeftShoulder,
        RightShoulder,

        // Triggers
        LeftTrigger,
        RightTrigger,

        // D-Pad
        DPadUp,
        DPadDown,
        DPadLeft,
        DPadRight,

        // Special
        Start,
        Back,
        Guide
    }

    /// <summary>
    /// Controller action mapping
    /// </summary>
    public class ControllerAction
    {
        public ControllerActionType Type { get; set; }
        
        /// <summary>
        /// Value cho analog inputs (0.0 - 1.0)
        /// Default = 1.0 (full press)
        /// </summary>
        public float Value { get; set; } = 1.0f;
    }

    /// <summary>
    /// Audio codec settings
    /// </summary>
    public class AudioCodecSettings
    {
        public string Codec { get; set; } = "OPUS"; // PCM16, OPUS
        public int Bitrate { get; set; } = 96000; // For OPUS (64000, 96000, 128000)
        public int SampleRate { get; set; } = 48000;
        public int Channels { get; set; } = 2;
    }
}
