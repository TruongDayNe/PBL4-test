using System;

namespace WPFUI
{
    [Serializable]
    class ScreenProcessorException : Exception
    {
        public ScreenProcessorException(string message) : base(message) { }
    }
}
