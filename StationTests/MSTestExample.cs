using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StationTests
{
    [TestClass]
    public class UnitTest1
    {
        private const string Expected = "Hello World!";
        [TestMethod]
        public void TestMethod1()
        {
            var result = "Hello World!";
            Assert.AreEqual(Expected, result);
        }
    }
}
