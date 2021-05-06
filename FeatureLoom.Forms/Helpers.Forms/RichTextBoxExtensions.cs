using System.Drawing;
using System.Windows.Forms;

namespace FeatureLoom.Helpers.Forms
{
    public static class RichTextBoxExtensions
    {
        public static void AppendText(this RichTextBox box, string text, Color color)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;

            box.SelectionColor = color;
            box.AppendText(text);
            box.SelectionColor = box.ForeColor;
        }

        public static void AppendText(this RichTextBox box, string text, FontStyle style)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;

            box.SelectionFont = new Font(box.Font, style);
            box.AppendText(text);
            box.SelectionFont = box.Font;
        }

        public static void AppendText(this RichTextBox box, string text, Color color, FontStyle style)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;

            box.SelectionFont = new Font(box.Font, style);
            box.SelectionColor = color;
            box.AppendText(text);
            box.SelectionColor = box.ForeColor;
            box.SelectionFont = box.Font;
        }

        public static void AppendText(this RichTextBox box, RichTextBuilder container)
        {
            if (container == null) return;
            foreach (var section in container.Sections)
            {
                if (section.color.HasValue && section.style.HasValue) box.AppendText(section.text, section.color.Value, section.style.Value);
                else if (section.color.HasValue) box.AppendText(section.text, section.color.Value);
                else if (section.style.HasValue) box.AppendText(section.text, section.style.Value);
                else box.AppendText(section.text);
            }
        }
    }
}