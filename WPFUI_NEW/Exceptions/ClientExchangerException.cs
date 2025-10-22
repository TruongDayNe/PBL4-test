using System;

namespace WPFUI_NEW
{
    [Serializable]
    class ClientExchangerException : Exception
    {
        public ClientExchangerException(string message) : base(message) { }
    }
}