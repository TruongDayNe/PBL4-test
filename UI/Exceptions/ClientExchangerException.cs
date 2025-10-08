using System;

namespace UI
{
    [Serializable]
    class ClientExchangerException : Exception
    {
        public ClientExchangerException(string message) : base(message) { }
    }
}