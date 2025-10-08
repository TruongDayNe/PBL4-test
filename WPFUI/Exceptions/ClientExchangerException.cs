using System;

namespace WPFUI
{
    [Serializable]
    class ClientExchangerException : Exception
    {
        public ClientExchangerException(string message) : base(message) { }
    }
}