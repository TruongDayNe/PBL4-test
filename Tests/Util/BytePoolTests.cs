using RealTimeUdpStream.Core.Util;
using Xunit;

public class BytePoolTests
{
    [Fact]
    public void Rent_ReturnsArrayOfAtLeastMinimumLength()
    {
        int minLength = 100;
        byte[] array = BytePool.Rent(minLength);
        Assert.NotNull(array);
        Assert.True(array.Length >= minLength);
        BytePool.Return(array); // Trả lại mảng vào pool
    }

    [Fact]
    public void Return_CanBeCalledWithoutError()
    {
        byte[] array = BytePool.Rent(10);
        var exception = Record.Exception(() => BytePool.Return(array));
        Assert.Null(exception);
    }
}