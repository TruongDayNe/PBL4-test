using System;

namespace UI
{
    [Serializable]
    class ScreenProcessorException : Exception
    {
        public ScreenProcessorException(string message) : base(message) { }
    }
}
