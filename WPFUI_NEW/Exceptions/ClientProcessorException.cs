using System;

namespace WPFUI_NEW
{
    [Serializable]
    class ClientProcessorException : Exception
    {
        public ClientProcessorException(string message) : base(message) { }
    }
}
