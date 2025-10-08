using System;

namespace UI
{ 
    [Serializable]
    class ClientProcessorException : Exception
    {
        public ClientProcessorException(string message) : base(message) { }
    }
}
