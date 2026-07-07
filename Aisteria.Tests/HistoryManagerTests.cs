using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aisteria.Tests
{
    [TestClass]
    public class HistoryManagerTests
    {
        [TestMethod]
        public void AddQuestion_AddsToTop()
        {
            var list = new List<string> { "old" };
            HistoryManager.AddQuestion(list, "new");
            Assert.AreEqual("new", list[0]);
            Assert.AreEqual(2, list.Count);
        }

        [TestMethod]
        public void AddQuestion_DuplicateMovedToTop_NotDuplicated()
        {
            var list = new List<string> { "a", "b", "c" };
            HistoryManager.AddQuestion(list, "c");
            CollectionAssert.AreEqual(new[] { "c", "a", "b" }, list);
        }

        [TestMethod]
        public void AddQuestion_IgnoresBlank()
        {
            var list = new List<string>();
            HistoryManager.AddQuestion(list, "   ");
            HistoryManager.AddQuestion(list, "");
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public void AddQuestion_TrimsSurroundingWhitespace()
        {
            var list = new List<string>();
            HistoryManager.AddQuestion(list, "  hello  ");
            Assert.AreEqual("hello", list[0]);
        }

        [TestMethod]
        public void AddQuestion_MultiLinePromptKeptAsSingleEntry()
        {
            var list = new List<string>();
            HistoryManager.AddQuestion(list, "line1\nline2\nline3");
            Assert.AreEqual(1, list.Count);
            Assert.IsTrue(list[0].Contains("\n"), "internal newlines must be preserved");
        }

        [TestMethod]
        public void AddQuestion_CapsAt200_NewestFirst()
        {
            var list = new List<string>();
            for (int i = 0; i < 205; i++)
                HistoryManager.AddQuestion(list, "q" + i);

            Assert.AreEqual(200, list.Count);
            Assert.AreEqual("q204", list[0]);
        }
    }
}
