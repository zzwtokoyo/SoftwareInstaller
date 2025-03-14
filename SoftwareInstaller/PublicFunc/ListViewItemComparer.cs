using System;
using System.Windows.Forms;

namespace SoftwareInstaller.PublicFunc
{
    // ListView排序器
    public class ListViewItemComparer : System.Collections.IComparer
    {
        private int column;

        public ListViewItemComparer(int column)
        {
            this.column = column;
        }

        public int Compare(object x, object y)
        {
            ListViewItem itemX = (ListViewItem)x;
            ListViewItem itemY = (ListViewItem)y;

            if (column == 3) // 时间列，按日期排序
            {
                DateTime dateX = DateTime.Parse(itemX.SubItems[column].Text);
                DateTime dateY = DateTime.Parse(itemY.SubItems[column].Text);
                return DateTime.Compare(dateX, dateY);
            }
            return String.Compare(itemX.SubItems[column].Text, itemY.SubItems[column].Text);
        }
    }
}
