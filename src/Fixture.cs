// 自有测试窗口，不修改用户文档；用于真实桌面 UI Automation 验收。
using System;
using System.Drawing;
using System.Windows.Forms;

namespace EnglishCompanion {
    internal sealed class Fixture : Form {
        internal Fixture() {
            Text = "语伴 · 输入兼容性测试"; Size = new Size(760, 580); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Microsoft YaHei UI", 12);
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 7 };
            grid.Controls.Add(new Label { AutoSize = true, Text = "先点输入框，直接输入中文，停顿半秒后开始翻译。\n也可用 Alt 听写；这里不发送消息。" });
            grid.Controls.Add(new Label { AutoSize = true, Text = "普通文本框" });
            grid.Controls.Add(new TextBox { Multiline = true, Height = 100, Dock = DockStyle.Top, AccessibleName = "普通文本框" });
            grid.Controls.Add(new Label { AutoSize = true, Text = "富文本框" });
            grid.Controls.Add(new RichTextBox { Height = 110, Dock = DockStyle.Top, AccessibleName = "富文本框" });
            grid.Controls.Add(new Label { AutoSize = true, Text = "密码框：聚焦后应隐藏浮层" });
            grid.Controls.Add(new TextBox { UseSystemPasswordChar = true, Dock = DockStyle.Top, AccessibleName = "测试密码框" });
            Controls.Add(grid);
        }
    }
}
