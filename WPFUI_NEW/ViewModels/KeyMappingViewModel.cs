using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RealTimeUdpStream.Core.Models;
using RealTimeUdpStream.Core.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace WPFUI_NEW.ViewModels
{
    public partial class KeyMappingViewModel : ObservableObject
    {
        public ICommand BackToMenuCommand { get; }

        // Tab switching
        [ObservableProperty]
        private bool _isControllerTabActive = false;

        [ObservableProperty]
        private bool _isKeyboardTabActive = true;

        public IRelayCommand ShowControllerTabCommand { get; }

        // Keyboard mapping
        [ObservableProperty]
        private ObservableCollection<KeyMappingItemViewModel> _currentKeyMappings;

        [ObservableProperty]
        private string _isCommonKeysActive = "Active";

        [ObservableProperty]
        private string _isAdvancedKeysActive = "";

        // Controller mappings (23 properties)
        [ObservableProperty] private string _leftShoulderMapping = "";
        [ObservableProperty] private string _rightShoulderMapping = "";
        [ObservableProperty] private string _leftTriggerMapping = "";
        [ObservableProperty] private string _rightTriggerMapping = "";
        [ObservableProperty] private string _leftStickUpMapping = "";
        [ObservableProperty] private string _leftStickDownMapping = "";
        [ObservableProperty] private string _leftStickLeftMapping = "";
        [ObservableProperty] private string _leftStickRightMapping = "";
        [ObservableProperty] private string _rightStickUpMapping = "";
        [ObservableProperty] private string _rightStickDownMapping = "";
        [ObservableProperty] private string _rightStickLeftMapping = "";
        [ObservableProperty] private string _rightStickRightMapping = "";
        [ObservableProperty] private string _buttonAMapping = "";
        [ObservableProperty] private string _buttonBMapping = "";
        [ObservableProperty] private string _buttonXMapping = "";
        [ObservableProperty] private string _buttonYMapping = "";
        [ObservableProperty] private string _dPadUpMapping = "";
        [ObservableProperty] private string _dPadDownMapping = "";
        [ObservableProperty] private string _dPadLeftMapping = "";
        [ObservableProperty] private string _dPadRightMapping = "";
        [ObservableProperty] private string _startMapping = "";
        [ObservableProperty] private string _backMapping = "";
        [ObservableProperty] private string _guideMapping = "";

        public IRelayCommand ShowCommonKeysCommand { get; }
        public IRelayCommand ShowAdvancedKeysCommand { get; }
        public IRelayCommand ShowKeyboardTabCommand { get; }
        public IRelayCommand ConfirmCommand { get; }

        private ObservableCollection<KeyMappingItemViewModel> _commonKeysList;
        private ObservableCollection<KeyMappingItemViewModel> _advancedKeysList;

        public KeyMappingViewModel(ICommand backToMenuCommand)
        {
            BackToMenuCommand = backToMenuCommand;
            
            ShowControllerTabCommand = new RelayCommand(ShowControllerTab);
            ShowKeyboardTabCommand = new RelayCommand(ShowKeyboardTab);
            ShowCommonKeysCommand = new RelayCommand(ShowCommonKeys);
            ShowAdvancedKeysCommand = new RelayCommand(ShowAdvancedKeys);
            ConfirmCommand = new RelayCommand(SaveMappings);

            InitializeKeyLists();
            LoadCurrentMappings();
            LoadControllerMappings();
            
            // Show common keys by default
            CurrentKeyMappings = _commonKeysList;
        }

        // Constructor cho design-time
        public KeyMappingViewModel() : this(null)
        {
        }

        private void InitializeKeyLists()
        {
            // Common Keys
            _commonKeysList = new ObservableCollection<KeyMappingItemViewModel>
            {
                // WASD cluster
                new KeyMappingItemViewModel("W", "W Key"),
                new KeyMappingItemViewModel("A", "A Key"),
                new KeyMappingItemViewModel("S", "S Key"),
                new KeyMappingItemViewModel("D", "D Key"),
                
                // QERF cluster
                new KeyMappingItemViewModel("Q", "Q Key"),
                new KeyMappingItemViewModel("E", "E Key"),
                new KeyMappingItemViewModel("R", "R Key"),
                new KeyMappingItemViewModel("F", "F Key"),
                
                // Other common
                new KeyMappingItemViewModel("T", "T Key"),
                new KeyMappingItemViewModel("C", "C Key"),
                new KeyMappingItemViewModel("V", "V Key"),
                new KeyMappingItemViewModel("X", "X Key"),
                new KeyMappingItemViewModel("Z", "Z Key"),
                new KeyMappingItemViewModel("G", "G Key"),
                new KeyMappingItemViewModel("H", "H Key"),
                new KeyMappingItemViewModel("B", "B Key"),
                
                // Additional keys (for controller mapping)
                new KeyMappingItemViewModel("J", "J Key"),
                new KeyMappingItemViewModel("K", "K Key"),
                new KeyMappingItemViewModel("L", "L Key"),
                new KeyMappingItemViewModel("M", "M Key"),
                new KeyMappingItemViewModel("N", "N Key"),
                new KeyMappingItemViewModel("O", "O Key"),
                new KeyMappingItemViewModel("P", "P Key"),
                new KeyMappingItemViewModel("U", "U Key"),
                new KeyMappingItemViewModel("Y", "Y Key"),
                
                // Control keys
                new KeyMappingItemViewModel("Space", "Space Bar"),
                new KeyMappingItemViewModel("Enter", "Enter"),
                new KeyMappingItemViewModel("Escape", "Escape"),
                
                // Numbers
                new KeyMappingItemViewModel("D1", "1 Key"),
                new KeyMappingItemViewModel("D2", "2 Key"),
                new KeyMappingItemViewModel("D3", "3 Key"),
                new KeyMappingItemViewModel("D4", "4 Key"),
                new KeyMappingItemViewModel("D5", "5 Key"),
                new KeyMappingItemViewModel("D6", "6 Key"),
                new KeyMappingItemViewModel("D7", "7 Key"),
                new KeyMappingItemViewModel("D8", "8 Key"),
                new KeyMappingItemViewModel("D9", "9 Key"),
                new KeyMappingItemViewModel("D0", "0 Key"),
            };

            // Advanced Keys
            _advancedKeysList = new ObservableCollection<KeyMappingItemViewModel>
            {
                // Modifier keys (Left/Right variants)
                new KeyMappingItemViewModel("LShift", "Left Shift"),
                new KeyMappingItemViewModel("RShift", "Right Shift"),
                new KeyMappingItemViewModel("LCtrl", "Left Control"),
                new KeyMappingItemViewModel("RCtrl", "Right Control"),
                new KeyMappingItemViewModel("LAlt", "Left Alt"),
                new KeyMappingItemViewModel("RAlt", "Right Alt"),
                
                // Special keys
                new KeyMappingItemViewModel("Tab", "Tab"),
                new KeyMappingItemViewModel("CapsLock", "Caps Lock"),
                new KeyMappingItemViewModel("Escape", "Escape"),
                
                // OEM Keys (symbols)
                new KeyMappingItemViewModel("OemOpenBrackets", "[ Key"),
                new KeyMappingItemViewModel("OemCloseBrackets", "] Key"),
                new KeyMappingItemViewModel("OemSemicolon", "; Key"),
                new KeyMappingItemViewModel("OemQuotes", "' Key"),
                new KeyMappingItemViewModel("OemPeriod", ". Key"),
                new KeyMappingItemViewModel("OemComma", ", Key"),
                new KeyMappingItemViewModel("OemQuestion", "/ Key"),
                new KeyMappingItemViewModel("OemPipe", "\\ Key"),
                new KeyMappingItemViewModel("OemMinus", "- Key"),
                new KeyMappingItemViewModel("OemPlus", "= Key"),
                
                // Function keys
                new KeyMappingItemViewModel("F1", "F1"),
                new KeyMappingItemViewModel("F2", "F2"),
                new KeyMappingItemViewModel("F3", "F3"),
                new KeyMappingItemViewModel("F4", "F4"),
                new KeyMappingItemViewModel("F5", "F5"),
                new KeyMappingItemViewModel("F6", "F6"),
                new KeyMappingItemViewModel("F7", "F7"),
                new KeyMappingItemViewModel("F8", "F8"),
                new KeyMappingItemViewModel("F9", "F9"),
                new KeyMappingItemViewModel("F10", "F10"),
                new KeyMappingItemViewModel("F11", "F11"),
                new KeyMappingItemViewModel("F12", "F12"),
                
                // Arrow keys
                new KeyMappingItemViewModel("Up", "↑ Up"),
                new KeyMappingItemViewModel("Down", "↓ Down"),
                new KeyMappingItemViewModel("Left", "← Left"),
                new KeyMappingItemViewModel("Right", "→ Right"),
                
                // NumPad
                new KeyMappingItemViewModel("NumPad0", "Numpad 0"),
                new KeyMappingItemViewModel("NumPad1", "Numpad 1"),
                new KeyMappingItemViewModel("NumPad2", "Numpad 2"),
                new KeyMappingItemViewModel("NumPad3", "Numpad 3"),
                new KeyMappingItemViewModel("NumPad4", "Numpad 4"),
                new KeyMappingItemViewModel("NumPad5", "Numpad 5"),
                new KeyMappingItemViewModel("NumPad6", "Numpad 6"),
                new KeyMappingItemViewModel("NumPad7", "Numpad 7"),
                new KeyMappingItemViewModel("NumPad8", "Numpad 8"),
                new KeyMappingItemViewModel("NumPad9", "Numpad 9"),
            };
        }

        private void LoadCurrentMappings()
        {
            try
            {
                var config = ConfigHelper.CurrentConfig;
                if (config?.KeyboardMapping == null) return;

                // Load mappings for common keys
                foreach (var item in _commonKeysList)
                {
                    if (config.KeyboardMapping.TryGetValue(item.SourceKey, out string mappedKey))
                    {
                        item.MappedKey = mappedKey;
                    }
                }

                // Load mappings for advanced keys
                foreach (var item in _advancedKeysList)
                {
                    if (config.KeyboardMapping.TryGetValue(item.SourceKey, out string mappedKey))
                    {
                        item.MappedKey = mappedKey;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KeyMappingViewModel] Error loading mappings: {ex.Message}");
            }
        }

        private void LoadControllerMappings()
        {
            try
            {
                var config = ConfigHelper.CurrentConfig;
                if (config?.ControllerMapping == null) return;

                // Load 23 controller mappings
                LeftShoulderMapping = GetControllerKeyMapping("LeftShoulder", config);
                RightShoulderMapping = GetControllerKeyMapping("RightShoulder", config);
                LeftTriggerMapping = GetControllerKeyMapping("LeftTrigger", config);
                RightTriggerMapping = GetControllerKeyMapping("RightTrigger", config);
                LeftStickUpMapping = GetControllerKeyMapping("LeftStickUp", config);
                LeftStickDownMapping = GetControllerKeyMapping("LeftStickDown", config);
                LeftStickLeftMapping = GetControllerKeyMapping("LeftStickLeft", config);
                LeftStickRightMapping = GetControllerKeyMapping("LeftStickRight", config);
                RightStickUpMapping = GetControllerKeyMapping("RightStickUp", config);
                RightStickDownMapping = GetControllerKeyMapping("RightStickDown", config);
                RightStickLeftMapping = GetControllerKeyMapping("RightStickLeft", config);
                RightStickRightMapping = GetControllerKeyMapping("RightStickRight", config);
                ButtonAMapping = GetControllerKeyMapping("ButtonA", config);
                ButtonBMapping = GetControllerKeyMapping("ButtonB", config);
                ButtonXMapping = GetControllerKeyMapping("ButtonX", config);
                ButtonYMapping = GetControllerKeyMapping("ButtonY", config);
                DPadUpMapping = GetControllerKeyMapping("DPadUp", config);
                DPadDownMapping = GetControllerKeyMapping("DPadDown", config);
                DPadLeftMapping = GetControllerKeyMapping("DPadLeft", config);
                DPadRightMapping = GetControllerKeyMapping("DPadRight", config);
                StartMapping = GetControllerKeyMapping("Start", config);
                BackMapping = GetControllerKeyMapping("Back", config);
                GuideMapping = GetControllerKeyMapping("Guide", config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KeyMappingViewModel] Error loading controller mappings: {ex.Message}");
            }
        }

        private string GetControllerKeyMapping(string actionType, RealTimeUdpStream.Core.Models.KeyMappingConfig config)
        {
            // Find which key maps to this action
            foreach (var kvp in config.ControllerMapping)
            {
                if (kvp.Value.Type.ToString() == actionType)
                {
                    return kvp.Key; // Return the key name (e.g., "I", "J", "O")
                }
            }
            return ""; // No mapping found
        }

        private void ShowControllerTab()
        {
            IsControllerTabActive = true;
            IsKeyboardTabActive = false;
        }

        private void ShowKeyboardTab()
        {
            IsControllerTabActive = false;
            IsKeyboardTabActive = true;
        }

        private void ShowCommonKeys()
        {
            CurrentKeyMappings = _commonKeysList;
            IsCommonKeysActive = "Active";
            IsAdvancedKeysActive = "";
        }

        private void ShowAdvancedKeys()
        {
            CurrentKeyMappings = _advancedKeysList;
            IsCommonKeysActive = "";
            IsAdvancedKeysActive = "Active";
        }

        private void SaveMappings()
        {
            try
            {
                var config = ConfigHelper.CurrentConfig;
                if (config == null)
                {
                    Console.WriteLine("❌ Config is null, cannot save");
                    return;
                }

                // Update keyboard mappings from both lists
                UpdateConfigFromList(_commonKeysList, config.KeyboardMapping);
                UpdateConfigFromList(_advancedKeysList, config.KeyboardMapping);

                // Update controller mappings
                UpdateControllerMappings(config);

                // Save to bin folder (same folder as .exe) - app will use this file
                string configPath = KeyMappingConfig.GetDefaultConfigPath();

                Console.WriteLine($"[KeyMappingViewModel] Saving to: {configPath}");

                // Save to bin file
                config.SaveToFile(configPath);
                
                // Reload config để áp dụng thay đổi ngay lập tức
                ConfigHelper.ReloadConfig();
                
                Console.WriteLine("✓ Mappings saved and reloaded successfully!");

                // Show success message
                System.Windows.MessageBox.Show(
                    "Mappings saved successfully!",
                    "Success",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving mappings: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"Failed to save mappings: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error
                );
            }
        }

        private void UpdateControllerMappings(RealTimeUdpStream.Core.Models.KeyMappingConfig config)
        {
            // Clear existing controller mappings
            config.ControllerMapping.Clear();

            // Add mappings for each controller action that has a key assigned
            AddControllerMapping(config, LeftShoulderMapping, "LeftShoulder");
            AddControllerMapping(config, RightShoulderMapping, "RightShoulder");
            AddControllerMapping(config, LeftTriggerMapping, "LeftTrigger");
            AddControllerMapping(config, RightTriggerMapping, "RightTrigger");
            AddControllerMapping(config, LeftStickUpMapping, "LeftStickUp");
            AddControllerMapping(config, LeftStickDownMapping, "LeftStickDown");
            AddControllerMapping(config, LeftStickLeftMapping, "LeftStickLeft");
            AddControllerMapping(config, LeftStickRightMapping, "LeftStickRight");
            AddControllerMapping(config, RightStickUpMapping, "RightStickUp");
            AddControllerMapping(config, RightStickDownMapping, "RightStickDown");
            AddControllerMapping(config, RightStickLeftMapping, "RightStickLeft");
            AddControllerMapping(config, RightStickRightMapping, "RightStickRight");
            AddControllerMapping(config, ButtonAMapping, "ButtonA");
            AddControllerMapping(config, ButtonBMapping, "ButtonB");
            AddControllerMapping(config, ButtonXMapping, "ButtonX");
            AddControllerMapping(config, ButtonYMapping, "ButtonY");
            AddControllerMapping(config, DPadUpMapping, "DPadUp");
            AddControllerMapping(config, DPadDownMapping, "DPadDown");
            AddControllerMapping(config, DPadLeftMapping, "DPadLeft");
            AddControllerMapping(config, DPadRightMapping, "DPadRight");
            AddControllerMapping(config, StartMapping, "Start");
            AddControllerMapping(config, BackMapping, "Back");
            AddControllerMapping(config, GuideMapping, "Guide");
        }

        private void AddControllerMapping(RealTimeUdpStream.Core.Models.KeyMappingConfig config, string keyName, string actionType)
        {
            if (string.IsNullOrWhiteSpace(keyName)) return;

            // Parse actionType to ControllerActionType enum
            if (Enum.TryParse<RealTimeUdpStream.Core.Models.ControllerActionType>(actionType, out var action))
            {
                config.ControllerMapping[keyName] = new RealTimeUdpStream.Core.Models.ControllerAction
                {
                    Type = action,
                    Value = 1.0f
                };
            }
        }

        private void UpdateConfigFromList(ObservableCollection<KeyMappingItemViewModel> list, Dictionary<string, string> mapping)
        {
            foreach (var item in list)
            {
                // Always update mapping, even if empty
                mapping[item.SourceKey] = item.MappedKey ?? "";
            }
        }
    }

    /// <summary>
    /// ViewModel for individual key mapping item
    /// </summary>
    public partial class KeyMappingItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _sourceKey;

        [ObservableProperty]
        private string _keyName;

        [ObservableProperty]
        private string _keyLabel;

        [ObservableProperty]
        private string _mappedKey;

        public KeyMappingItemViewModel(string sourceKey, string keyName)
        {
            SourceKey = sourceKey;
            KeyName = keyName;
            KeyLabel = GetKeyLabel(sourceKey);
            MappedKey = ""; // Default empty
        }

        private string GetKeyLabel(string key)
        {
            // Extract label for icon display
            if (key.StartsWith("D") && key.Length == 2 && char.IsDigit(key[1]))
                return key.Substring(1); // D1 → 1
            
            if (key.StartsWith("NumPad"))
                return "N" + key.Substring(6); // NumPad1 → N1
            
            if (key.StartsWith("Oem"))
            {
                return key switch
                {
                    "OemSemicolon" => ";",
                    "OemPlus" => "=",
                    "OemComma" => ",",
                    "OemMinus" => "-",
                    "OemPeriod" => ".",
                    "OemQuestion" => "/",
                    "OemTilde" => "`",
                    "OemOpenBrackets" => "[",
                    "OemPipe" => "\\",
                    "OemCloseBrackets" => "]",
                    "OemQuotes" => "'",
                    _ => key
                };
            }

            if (key == "Space") return "___";
            if (key == "Enter") return "↵";
            if (key == "Back") return "⌫";
            if (key == "Tab") return "⇥";
            if (key == "Up") return "↑";
            if (key == "Down") return "↓";
            if (key == "Left") return "←";
            if (key == "Right") return "→";

            return key.Length <= 4 ? key : key.Substring(0, 4);
        }
    }
}
