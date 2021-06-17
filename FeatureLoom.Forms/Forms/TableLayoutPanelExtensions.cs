using System.Windows.Forms;

namespace FeatureLoom.Forms
{
    public static class TableLayoutPanelExtensions
    {
        public static void RemoveRowAt(this TableLayoutPanel panel, int rowIndex)
        {
            if (rowIndex >= panel.RowCount) return;

            using (panel.LayoutSuspension())
            {
                for (int col = 0; col < panel.ColumnCount; col++)
                {
                    var control = panel.GetControlFromPosition(col, rowIndex);
                    if (control != null) panel.Controls.Remove(control);
                }

                for (int row = rowIndex + 1; row < panel.RowCount; row++)
                {
                    for (int col = 0; col < panel.ColumnCount; col++)
                    {
                        var control = panel.GetControlFromPosition(col, row);
                        if (control != null) panel.SetRow(control, row - 1);
                    }
                }

                int rowStyleIndex = panel.RowCount - 1;
                if (panel.RowStyles.Count > rowStyleIndex) panel.RowStyles.RemoveAt(rowStyleIndex);

                panel.RowCount--;
            }
        }
    }
}