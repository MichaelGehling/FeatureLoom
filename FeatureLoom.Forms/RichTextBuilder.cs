using FeatureLoom.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace FeatureLoom.Forms
{
    public class RichTextBuilder
    {
        private List<Section> sections = new List<Section>();
        public IEnumerable<Section> Sections => sections;
        private Lazy<StringBuilder> sb = new Lazy<StringBuilder>();

        public override string ToString()
        {
            if (sb.Value.Length == 0)
            {
                foreach (var section in sections)
                {
                    sb.Value.Append(section.text);
                }
            }
            return sb.ToString();
        }

        public RichTextBuilder Append(RichTextBuilder container)
        {
            foreach (var section in container.Sections)
            {
                sections.Add(section);
            }
            return this;
        }

        public RichTextBuilder Append(string text)
        {
            sections.Add(new Section(text));
            return this;
        }

        public RichTextBuilder Append(string text, Color color)
        {
            sections.Add(new Section(text, color));
            return this;
        }

        public RichTextBuilder Append(string text, FontStyle style)
        {
            sections.Add(new Section(text, style));
            return this;
        }

        public RichTextBuilder Append(string text, Color color, FontStyle style)
        {
            sections.Add(new Section(text, color, style));
            return this;
        }

        public readonly struct Section
        {
            public readonly string text;
            public readonly Color? color;
            public readonly FontStyle? style;

            public Section(string text, Color color, FontStyle style)
            {
                this.text = text.EmptyIfNull();
                this.color = color;
                this.style = style;
            }

            public Section(string text, Color color)
            {
                this.text = text.EmptyIfNull();
                this.color = color;
                this.style = null;
            }

            public Section(string text, FontStyle style)
            {
                this.text = text.EmptyIfNull();
                this.color = null;
                this.style = style;
            }

            public Section(string text)
            {
                this.text = text.EmptyIfNull();
                this.color = null;
                this.style = null;
            }
        }
    }
}