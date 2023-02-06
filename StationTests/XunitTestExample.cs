using System;
using Xunit;
using System.IO;

namespace StationTests
{
    public class XunitTestExample
    {
        private const string Expected = "Hello World!";

        [Fact]
        public void Test1()
        {
            var result = "Hello World!";
            Assert.Equal(Expected, result);
        }
    }
}
