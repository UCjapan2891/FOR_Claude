using System.IO;
using NUnit.Framework;
using VRClass.Core;

namespace VRClass.Tests
{
    public class ParticipantRegistryTests
    {
        [Test]
        public void Parse_HeaderCommentsBlankLines()
        {
            var r = ParticipantRegistry.Parse("id\n# コメント\n\n1\n2\n  3  \n");

            Assert.AreEqual(3, r.Count);
            Assert.IsTrue(r.IsRegistered(1));
            Assert.IsTrue(r.IsRegistered(2));
            Assert.IsTrue(r.IsRegistered(3));
            Assert.IsFalse(r.IsRegistered(4));
            Assert.IsEmpty(r.Problems);
        }

        [Test]
        public void Parse_WithoutHeader()
        {
            var r = ParticipantRegistry.Parse("10\n11\n");
            Assert.AreEqual(2, r.Count);
            Assert.IsTrue(r.IsRegistered(10));
            Assert.IsEmpty(r.Problems);
        }

        [Test]
        public void Parse_BomAndCrLf()
        {
            var r = ParticipantRegistry.Parse("﻿id\r\n5\r\n6\r\n");
            Assert.AreEqual(2, r.Count);
            Assert.IsTrue(r.IsRegistered(5));
            Assert.IsTrue(r.IsRegistered(6));
            Assert.IsEmpty(r.Problems);
        }

        [Test]
        public void Parse_ReadsFirstColumnOnly()
        {
            var r = ParticipantRegistry.Parse("id,memo\n7,予備実験\n\"8\",\n");
            Assert.AreEqual(2, r.Count);
            Assert.IsTrue(r.IsRegistered(7));
            Assert.IsTrue(r.IsRegistered(8));
        }

        [Test]
        public void Parse_ReportsProblems()
        {
            var r = ParticipantRegistry.Parse("id\n1\nabc\n0\n-2\n1\n");

            Assert.AreEqual(1, r.Count);
            Assert.AreEqual(4, r.Problems.Count); // abc, 0, -2, 重複した 1
            StringAssert.StartsWith("3行目", r.Problems[0]);
            StringAssert.StartsWith("6行目", r.Problems[3]);
        }

        [Test]
        public void Parse_NullOrEmpty_IsEmpty()
        {
            Assert.AreEqual(0, ParticipantRegistry.Parse(null).Count);
            Assert.AreEqual(0, ParticipantRegistry.Parse("").Count);
        }

        [Test]
        public void Load_MissingFile_Throws()
        {
            string path = Path.Combine(Path.GetTempPath(), "vrclass_not_exist_" + System.Guid.NewGuid() + ".csv");
            Assert.Throws<FileNotFoundException>(() => ParticipantRegistry.Load(path));
        }

        [Test]
        public void Load_ReadsFile()
        {
            string path = Path.Combine(Path.GetTempPath(), "vrclass_participants_" + System.Guid.NewGuid() + ".csv");
            try
            {
                File.WriteAllText(path, "id\n3\n4\n");
                var r = ParticipantRegistry.Load(path);
                Assert.AreEqual(2, r.Count);
                Assert.IsTrue(r.IsRegistered(4));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestCase("12", true, 12)]
        [TestCase(" 7 ", true, 7)]
        [TestCase("１２", true, 12)] // 全角
        [TestCase("0", false, 0)]
        [TestCase("-3", false, 0)]
        [TestCase("+3", false, 0)]
        [TestCase("3.0", false, 0)]
        [TestCase("abc", false, 0)]
        [TestCase("", false, 0)]
        [TestCase(null, false, 0)]
        public void TryParseInput(string input, bool ok, int expected)
        {
            Assert.AreEqual(ok, ParticipantRegistry.TryParseInput(input, out int id));
            Assert.AreEqual(expected, id);
        }
    }
}
