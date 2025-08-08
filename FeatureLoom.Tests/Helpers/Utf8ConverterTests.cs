using FeatureLoom.Collections;
using FeatureLoom.Helpers;
using System.Text;
using Xunit;

namespace FeatureLoom.Helpers
{
    public class Utf8ConverterTests
    {
        #region ASCII and Basic UTF-8 Tests

        [Fact]
        public void DecodeUtf8_SimpleAscii_DecodesCorrectly()
        {
            // Arrange
            var input = "Hello, world!";
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes(input));

            // Act
            var result = bytes.DecodeUtf8ToString();

            // Assert
            Assert.Equal(input, result);
        }

        [Fact]
        public void DecodeUtf8_EmptyString_ReturnsEmptyString()
        {
            // Arrange
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes(""));

            // Act
            var result = bytes.DecodeUtf8ToString();

            // Assert
            Assert.Equal("", result);
        }

        #endregion

        #region Escape Sequence Tests

        [Theory]
        [InlineData("Line1\\nLine2", "Line1\nLine2")]
        [InlineData("Tab\\tSeparated", "Tab\tSeparated")]
        [InlineData("Carriage\\rReturn", "Carriage\rReturn")]
        [InlineData("Form\\fFeed", "Form\fFeed")]
        [InlineData("Back\\bSpace", "Back\bSpace")]
        [InlineData("Escaped\\\\Backslash", "Escaped\\Backslash")]
        public void DecodeUtf8_CommonEscapes_DecodesCorrectly(string input, string expected)
        {
            // Arrange
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes(input));

            // Act
            var result = bytes.DecodeUtf8ToString();

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void DecodeUtf8_BackslashAtEnd_HandledAsLiteralBackslash()
        {
            // Arrange
            var input = "Trailing backslash\\";
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes(input));

            // Act
            var result = bytes.DecodeUtf8ToString();

            // Assert
            Assert.Equal("Trailing backslash\\", result);
        }

        [Fact]
        public void DecodeUtf8_UnknownEscape_HandledAsLiteralCharacter()
        {
            // Arrange
            var input = "Unknown \\x escape";
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes(input));

            // Act
            var result = bytes.DecodeUtf8ToString();

            // Assert
            Assert.Equal("Unknown x escape", result);
        }

        #endregion

        #region Unicode Escape Tests

        [Theory]
        [InlineData("ASCII A: \\u0041", "ASCII A: A")]
        [InlineData("Greek Omega: \\u03A9", "Greek Omega: Ω")]
        [InlineData("Heart Symbol: \\u2764", "Heart Symbol: ❤")]
        public void DecodeUtf8_UnicodeEscapes_DecodesCorrectly(string input, string expected)
        {
            // Arrange
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes(input));

            // Act
            var result = bytes.DecodeUtf8ToString();

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Incomplete \\u", "Incomplete \\u")]
        [InlineData("Incomplete \\u12", "Incomplete \\u12")]
        [InlineData("Incomplete \\u123", "Incomplete \\u123")]
        public void DecodeUtf8_IncompleteUnicodeEscape_HandledAsLiteral(string input, string expected)
        {
            // Arrange
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes(input));

            // Act
            var result = bytes.DecodeUtf8ToString();

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Invalid \\u123Z", "Invalid \\u123Z")]
        [InlineData("Invalid \\uGHIJ", "Invalid \\uGHIJ")]
        public void DecodeUtf8_InvalidHexInUnicodeEscape_HandledAsLiteral(string input, string expected)
        {
            // Arrange
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes(input));

            // Act
            var result = bytes.DecodeUtf8ToString();

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region Surrogate Pair Tests

        [Fact]
        public void DecodeUtf8_SurrogatePairs_DecodesCorrectly()
        {
            // Arrange - surrogate pair Unicode characters (e.g., emoji)
            // 😀 is encoded as \uD83D\uDE00
            var input = "Smiley: \\uD83D\\uDE00";
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes(input));

            // Act
            var result = bytes.DecodeUtf8ToString();

            // Assert
            Assert.Equal("Smiley: 😀", result);
        }

        [Fact]
        public void DecodeUtf8_HighSurrogateWithoutLow_HandledAsChar()
        {
            // Arrange - high surrogate without a following low surrogate
            var input = "Incomplete surrogate: \\uD83D other text";
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes(input));

            // Act
            var result = bytes.DecodeUtf8ToString();

            // Assert
            Assert.Equal("Incomplete surrogate: \uD83D other text", result);
        }

        [Fact]
        public void DecodeUtf8_HighSurrogateWithInvalidLow_HandlesGracefully()
        {
            // Arrange - high surrogate followed by invalid low surrogate
            var input = "Invalid surrogate pair: \\uD83D\\u1234";
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes(input));

            // Act
            var result = bytes.DecodeUtf8ToString();

            // Assert
            Assert.Equal("Invalid surrogate pair: \uD83D\\u1234", result);
        }

        #endregion

        #region Multibyte UTF-8 Tests

        [Fact]
        public void DecodeUtf8_MultibyteChinese_DecodesCorrectly()
        {
            // Arrange - Chinese characters (3-byte UTF-8)
            var input = "你好，世界";
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes(input));

            // Act
            var result = bytes.DecodeUtf8ToString();

            // Assert
            Assert.Equal(input, result);
        }

        [Fact]
        public void DecodeUtf8_MultibyteFourBytesEmoji_DecodesCorrectly()
        {
            // Arrange - emoji (4-byte UTF-8)
            var input = "😀👋🌍";
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes(input));

            // Act
            var result = bytes.DecodeUtf8ToString();

            // Assert
            Assert.Equal(input, result);
        }

        [Fact]
        public void DecodeUtf8_IncompleteMultibyte_HandlesGracefully()
        {
            // Arrange - incomplete multibyte sequence
            // First byte of a 3-byte character without the following bytes
            byte[] incompleteUtf8 = { 0xE4 }; // First byte of Chinese character 你
            var bytes = new ByteSegment(incompleteUtf8);

            // Act
            var result = bytes.DecodeUtf8ToString();

            // Assert
            Assert.Equal("", result); // Should handle incomplete UTF-8 gracefully
        }

        #endregion

        #region StringBuilder Reuse Tests

        [Fact]
        public void DecodeUtf8_WithReuseStringBuilder_WorksCorrectly()
        {
            // Arrange
            var input = "Test string with reused StringBuilder";
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder();

            // Act
            var result = bytes.DecodeUtf8ToString(sb);

            // Assert
            Assert.Equal(input, result);
            Assert.Equal(0, sb.Length); // StringBuilder should be cleared after use
        }

        #endregion

        #region Encoding Tests

        [Fact]
        public void EncodeToUtf8_SimpleString_EncodesCorrectly()
        {
            // Arrange
            var input = "Hello, world!";

            // Act
            var bytes = input.EncodeToUtf8();
            var roundTrip = bytes.DecodeUtf8ToString();

            // Assert
            Assert.Equal(input, roundTrip);
        }

        [Fact]
        public void EncodeToUtf8_WithUnicode_EncodesCorrectly()
        {
            // Arrange
            var input = "Unicode test: 你好，世界 and emoji 😀";

            // Act
            var bytes = input.EncodeToUtf8();
            var roundTrip = bytes.DecodeUtf8ToString();

            // Assert
            Assert.Equal(input, roundTrip);
        }

        [Fact]
        public void EncodeToUtf8_WithTextSegment_EncodesCorrectly()
        {
            // Arrange
            var fullString = "This is a longer string";
            var segment = new TextSegment(fullString, 5, 7); // "is a lo"

            // Act
            var bytes = segment.EncodeToUtf8();
            var roundTrip = bytes.DecodeUtf8ToString();

            // Assert
            Assert.Equal("is a lo", roundTrip);
        }

        #endregion

        #region ArraySegment and Span Tests

        [Fact]
        public void DecodeUtf8ToChars_ReturnsCorrectCharArray()
        {
            // Arrange
            var input = "Test with chars array";
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes(input));

            // Act
            var chars = bytes.DecodeUtf8ToChars();
            var result = new string(chars.Array, chars.Offset, chars.Count);

            // Assert
            Assert.Equal(input, result);
        }

#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
        [Fact]
        public void DecodeUtf8ToSpanOfChars_ReturnsCorrectSpan()
        {
            // Arrange
            var input = "Test with span of chars";
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes(input));

            // Act
            var span = bytes.DecodeUtf8ToSpanOfChars();
            var result = new string(span);

            // Assert
            Assert.Equal(input, result);
        }
#endif

        #endregion

        #region Mixed Content Tests

        [Fact]
        public void DecodeUtf8_MixedContent_DecodesCorrectly()
        {
            // Arrange - mix of ASCII, escapes, Unicode, and surrogate pairs
            var input = "ASCII with \\t tab, Unicode Ω (\\u03A9), and emoji \\uD83D\\uDE00";
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes(input));

            // Act
            var result = bytes.DecodeUtf8ToString();

            // Assert
            Assert.Equal("ASCII with \t tab, Unicode Ω (Ω), and emoji 😀", result);
        }

        #endregion

        #region Memory Usage Tests

        [Fact]
        public void DecodeUtf8_LargeString_CompletesWithoutException()
        {
            // This is a basic check for large strings - not a true performance test
            // but ensures the code can handle reasonably large input

            // Arrange
            var builder = new StringBuilder(10000);
            for (int i = 0; i < 1000; i++)
            {
                builder.Append("Line ").Append(i).Append(": Some text with Unicode Ω and escape \\t sequences.\n");
            }
            var input = builder.ToString();
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes(input));

            // Act & Assert - should complete without exception
            var result = bytes.DecodeUtf8ToString();

            // Basic verification that the result contains expected content
            Assert.Contains("Line 0:", result);
            Assert.Contains("Line 999:", result);
        }

        #endregion
    }
}