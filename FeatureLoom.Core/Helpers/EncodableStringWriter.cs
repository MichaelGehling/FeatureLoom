using System.IO;
using System.Text;

namespace FeatureLoom.Helpers
{
    /// <summary>
    /// A StringWriter that allows specifying the encoding to be used.
    /// </summary>
    public sealed class EncodableStringWriter : StringWriter
    {
        /// <summary>
        /// Gets the encoding in which the output is written.
        /// </summary>
        public override Encoding Encoding { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EncodableStringWriter"/> class using UTF-8 encoding.
        /// </summary>
        public EncodableStringWriter() : this(Encoding.UTF8)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EncodableStringWriter"/> class with the specified encoding.
        /// </summary>
        /// <param name="encoding">The encoding to use. If null, UTF-8 is used.</param>
        public EncodableStringWriter(Encoding encoding)
        {
            Encoding = encoding ?? Encoding.UTF8;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EncodableStringWriter"/> class with the specified <see cref="StringBuilder"/> and encoding.
        /// </summary>
        /// <param name="sb">The StringBuilder to write to.</param>
        /// <param name="encoding">The encoding to use. If null, UTF-8 is used.</param>
        public EncodableStringWriter(StringBuilder sb, Encoding encoding)
            : base(sb)
        {
            Encoding = encoding ?? Encoding.UTF8;
        }
    }
}