using System.Globalization;
using System.Threading;
using NUnit.Framework;
using VRClass.Core;

namespace VRClass.Tests
{
    public class CsvFormatTests
    {
        [TestCase(null, "")]
        [TestCase("", "")]
        [TestCase("abc", "abc")]
        [TestCase("ア,イ", "\"ア,イ\"")]
        [TestCase("say \"hi\"", "\"say \"\"hi\"\"\"")]
        [TestCase("a\nb", "\"a\nb\"")]
        public void Escape(string input, string expected)
        {
            Assert.AreEqual(expected, CsvFormat.Escape(input));
        }

        [Test]
        public void Join_EscapesEachValue()
        {
            Assert.AreEqual("1,\"x,y\",,z", CsvFormat.Join(new[] { "1", "x,y", null, "z" }));
        }

        [TestCase(0.0, "0.000")]
        [TestCase(1.23456, "1.235")]
        [TestCase(12.0004, "12.000")]
        [TestCase(512.5, "512.500")]
        public void Seconds_ThreeDecimals(double s, string expected)
        {
            Assert.AreEqual(expected, CsvFormat.Seconds(s));
        }

        [Test]
        public void Number_NullIsBlank()
        {
            Assert.AreEqual("", CsvFormat.Number((double?)null, 2));
            Assert.AreEqual("20.50", CsvFormat.Number((double?)20.5, 2));
        }

        [Test]
        public void Format_IgnoresCurrentCulture()
        {
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                // 小数点が「,」になるカルチャでも「.」で書く
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                Assert.AreEqual("1.500", CsvFormat.Seconds(1.5));
                Assert.AreEqual("0.25", CsvFormat.Number(0.25, 2));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}
