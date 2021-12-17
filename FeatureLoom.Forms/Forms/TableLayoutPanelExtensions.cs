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

                if (panel.RowStyles.Count > rowIndex) panel.RowStyles.RemoveAt(rowIndex);

                panel.RowCount--;
            }
        }

        public static void InsertRowAt(this TableLayoutPanel panel, int rowIndex, RowStyle rowStyle = null)
        {
            if (rowIndex > panel.RowCount) return;

            using (panel.LayoutSuspension())
            {
                panel.RowCount++;
                panel.RowStyles.Insert(rowIndex, rowStyle ?? new RowStyle());
                                                             
                for (int row = panel.RowCount - 2; row >= rowIndex; row--)
                {
                    for (int col = 0; col < panel.ColumnCount; col++)
                    {
                        var control = panel.GetControlFromPosition(col, row);
                        if (control != null) panel.SetCellPosition(control, new TableLayoutPanelCellPosition(col, row + 1));
                    }
                }
            }
        }
    }
}