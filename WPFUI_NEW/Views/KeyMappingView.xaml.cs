using System.Windows.Controls;
using System.Windows.Input;
using WPFUI_NEW.ViewModels;

namespace WPFUI_NEW.Views
{
    public partial class KeyMappingView : UserControl
    {
        public KeyMappingView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handle key press in input box - capture the key
        /// </summary>
        private void KeyInputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true; // Prevent default behavior

            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Get the actual key (handle special keys)
            Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;

            // Handle Backspace - clear the mapping
            if (key == Key.Back)
            {
                textBox.Text = "";
                if (textBox.Tag is KeyMappingItemViewModel viewModel)
                {
                    viewModel.MappedKey = "";
                }
                return;
            }

            // Ignore modifier keys alone
            if (key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt)
            {
                return;
            }

            // Convert Key to string
            string keyName = ConvertKeyToString(key);
            
            // Update the TextBox
            textBox.Text = keyName;

            // Update the ViewModel
            if (textBox.Tag is KeyMappingItemViewModel viewModel2)
            {
                viewModel2.MappedKey = keyName;
            }

            // Move focus away to prevent further input
            textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        /// <summary>
        /// Clear textbox on focus to allow new input
        /// </summary>
        private void KeyInputBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                textBox.SelectAll();
            }
        }

        /// <summary>
        /// Convert WPF Key to VirtualKey string name
        /// </summary>
        private string ConvertKeyToString(Key key)
        {
            // Numbers
            if (key >= Key.D0 && key <= Key.D9)
                return "D" + (key - Key.D0);

            // Letters
            if (key >= Key.A && key <= Key.Z)
                return key.ToString();

            // Function keys
            if (key >= Key.F1 && key <= Key.F24)
                return key.ToString();

            // NumPad
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return "NumPad" + (key - Key.NumPad0);

            // Special mappings
            return key switch
            {
                Key.Space => "Space",
                Key.Enter => "Enter",
                Key.Back => "Back",
                Key.Tab => "Tab",
                Key.Escape => "Escape",
                Key.CapsLock => "CapsLock",
                Key.LeftShift => "LShift",
                Key.RightShift => "RShift",
                Key.LeftCtrl => "LCtrl",
                Key.RightCtrl => "RCtrl",
                Key.LeftAlt => "LAlt",
                Key.RightAlt => "RAlt",
                Key.Up => "Up",
                Key.Down => "Down",
                Key.Left => "Left",
                Key.Right => "Right",
                Key.PageUp => "PageUp",
                Key.PageDown => "PageDown",
                Key.Home => "Home",
                Key.End => "End",
                Key.Insert => "Insert",
                Key.Delete => "Delete",
                Key.Multiply => "Multiply",
                Key.Add => "Add",
                Key.Subtract => "Subtract",
                Key.Decimal => "Decimal",
                Key.Divide => "Divide",
                Key.NumLock => "NumLock",
                Key.Scroll => "ScrollLock",
                Key.Pause => "Pause",
                Key.PrintScreen => "PrintScreen",
                Key.OemSemicolon => "OemSemicolon",  // ;
                Key.OemPlus => "OemPlus",            // =
                Key.OemComma => "OemComma",          // ,
                Key.OemMinus => "OemMinus",          // -
                Key.OemPeriod => "OemPeriod",        // .
                Key.OemQuestion => "OemQuestion",    // /
                Key.OemTilde => "OemTilde",          // `
                Key.OemOpenBrackets => "OemOpenBrackets",   // [
                Key.OemPipe => "OemPipe",            // \
                Key.OemCloseBrackets => "OemCloseBrackets", // ]
                Key.OemQuotes => "OemQuotes",        // '
                _ => key.ToString()
            };
        }
    }
}
