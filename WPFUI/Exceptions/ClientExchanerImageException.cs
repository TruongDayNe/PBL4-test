using System;

namespace WPFUI
{
    [Serializable]
    class ClientExchangerImageException : ClientExchangerException
    {
        public ClientExchangerImageException(string message) : base(message) { }
    }
}