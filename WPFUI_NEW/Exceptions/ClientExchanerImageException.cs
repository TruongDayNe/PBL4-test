using System;

namespace WPFUI_NEW
{
    [Serializable]
    class ClientExchangerImageException : ClientExchangerException
    {
        public ClientExchangerImageException(string message) : base(message) { }
    }
}