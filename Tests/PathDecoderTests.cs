using Pathing;
using System;
using Xunit;

namespace Tests
{
    public class PathDecoderTests
    {
        [Theory]
        [InlineData("", "")]
        [InlineData("*", "")]
        [InlineData("/", "/")]
        [InlineData("/a", "/a")]
        [InlineData("/a%", "/a%")]
        [InlineData("/a%a", "/a%a")]
        [InlineData("/a%ab", "/a%ab")]
        [InlineData("/a%41", "/a%41")]
        [InlineData("/a%25", "/a%25")]
        [InlineData("/a%2520", "/a%2520")]
        [InlineData("/a%252F", "/a%252F")]
        [InlineData("/a%2f", "/a/")]
        [InlineData("/a%2F", "/a/")]
        [InlineData("/a%2e", "/a.")]
        [InlineData("/a%2E", "/a.")]
        [InlineData("/a//", "/a/")]
        [InlineData("/a///", "/a/")]
        [InlineData("/a///a", "/a/a")]
        [InlineData("/a///ab", "/a/ab")]
        [InlineData("/a///abc", "/a/abc")]
        [InlineData("/a///abcd", "/a/abcd")]
        [InlineData("/a/b/c//..", "/a/b/")]
        [InlineData("/a/b/c////..", "/a/b/")]
        [InlineData("/a/b/c////../..", "/a/")]
        [InlineData("/a/b/c////../../..", "/")]
        [InlineData("/a//b///c////../d/..", "/a/b/")]
        [InlineData("/a///..", "/")]
        [InlineData("/a///../..", "/")]
        [InlineData("/a///../../..", "/")]
        [InlineData("/a//../", "/")]
        [InlineData("/a///../../", "/")]
        [InlineData("/a///../../../", "/")]
        [InlineData("/a///../", "/")]
        [InlineData("/a///../bcde", "/bcde")]
        [InlineData("/a//b", "/a/b")]
        [InlineData("/a/.", "/a/")]
        [InlineData("/a/./", "/a/")]
        [InlineData("/a/./b", "/a/b")]
        [InlineData("/a/%2E/", "/a/")]
        [InlineData("/a/%2e/b", "/a/b")]
        [InlineData("/a/..", "/")]
        [InlineData("/a/../", "/")]
        [InlineData("/a/../b", "/b")]
        [InlineData("/a/.%2E/", "/")]
        [InlineData("/a/%2e./b", "/b")]
        [InlineData("/a/%2e%2E/b", "/b")]
        [InlineData("/a/b/c/d/..", "/a/b/c/")]
        [InlineData("/../../a/b/c/d/", "/a/b/c/d/")]
        [InlineData("/a/../../b/c/d/", "/b/c/d/")]
        [InlineData("/?", "/")]
        [InlineData("/?query", "/")]
        [InlineData("/a/b/c?", "/a/b/c")]
        [InlineData("/a/b/c/?", "/a/b/c/")]
        [InlineData("http://foo", "/")]
        [InlineData("http://foo/", "/")]
        [InlineData("https://foo/bar", "/bar")]
        [InlineData("https://foo/bar?", "/bar")]
        [InlineData("https://foo/bar?query", "/bar")]
        [InlineData("https://foo?query/", "/")]
        [InlineData("https://foo/?query/", "/")]
        // What about %5C '\'? Was there anything in IIS un-escaping that and converting it to '/'?
        public void DecodePath(string input, string expected)
        {
            var reslt = PathDecoder.GetPathFromRawTarget(input);
            Assert.Equal(expected, reslt);
        }
    }
}
