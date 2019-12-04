﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Diffing.Core;
using AngleSharp.Dom;
using Xunit;

namespace Egil.RazorComponents.Testing
{
    public static class AssertExtensions
    {
        public static IDiff ShouldHaveSingleChange(this IReadOnlyList<IDiff> diffs)
        {
            if (diffs is null) throw new ArgumentNullException(nameof(diffs));
            Assert.Equal(1, diffs.Count);
            return diffs[0];
        }

        public static void ShouldAllBe<T>(this IEnumerable<T> collection, params Action<T>[] elementInspectors)
        {
            Assert.Collection(collection, elementInspectors);
        }

        public static void ShouldHaveChanges(this IReadOnlyList<IDiff> diffs, params Action<IDiff>[] expectedDiffAsserts)
        {
            Assert.Collection(diffs, expectedDiffAsserts);
        }

        public static void ShouldBe(this IRenderedFragment actual, string expected, string? userMessage = null)
        {
            if (actual is null) throw new ArgumentNullException(nameof(actual));
            if (expected is null) throw new ArgumentNullException(nameof(expected));

            var actualNodes = actual.GetNodes();
            var expectedNodes = actual.TestContext.HtmlParser.Parse(expected);

            actualNodes.ShouldBe(expectedNodes, userMessage);
        }

        public static void ShouldBe(this IRenderedFragment actual, IRenderedFragment expected, string? userMessage = null)
        {
            if (actual is null) throw new ArgumentNullException(nameof(actual));
            if (expected is null) throw new ArgumentNullException(nameof(expected));

            actual.GetNodes().ShouldBe(expected.GetNodes(), userMessage);
        }

        public static void ShouldBe(this INodeList actual, INodeList expected, string? userMessage = null)
        {
            var diffs = actual.CompareTo(expected);

            if (diffs.Count != 0)
            {
                var msg = diffs.ToDiffAssertMessage(expected, actual, userMessage);
                Assert.True(diffs.Count == 0, msg);
            }
        }
    }
}
