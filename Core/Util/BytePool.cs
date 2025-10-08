using System;
using System.Buffers;

namespace RealTimeUdpStream.Core.Util
{
    public static class BytePool
    {
        private static readonly ArrayPool<byte> _pool = ArrayPool<byte>.Shared;

        public static byte[] Rent(int minimumLength) => _pool.Rent(minimumLength);

        public static void Return(byte[] array, bool clearArray = false) => _pool.Return(array, clearArray);
    }
}