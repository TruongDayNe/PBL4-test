using System;

namespace WPFUI_NEW
{
    [Serializable]
    class ScreenProcessorException : Exception
    {
        public ScreenProcessorException(string message) : base(message) { }
    }
}
