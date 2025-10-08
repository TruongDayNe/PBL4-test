using System;

namespace WPFUI
{
    [Serializable]
    class ClientProcessorException : Exception
    {
        public ClientProcessorException(string message) : base(message) { }
    }
}
