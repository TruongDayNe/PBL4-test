using System;

namespace UI
{
    [Serializable]
    class ClientExchangerImageException : ClientExchangerException
    {
        public ClientExchangerImageException(string message) : base(message) { }
    }
}