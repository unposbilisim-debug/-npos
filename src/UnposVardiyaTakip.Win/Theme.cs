namespace UnposVardiyaTakip.Win;

internal static class Theme
{
    public static readonly Color Bg = Color.FromArgb(8, 16, 24);
    public static readonly Color Panel = Color.FromArgb(18, 32, 51);
    public static readonly Color Gold = Color.FromArgb(245, 185, 66);
    public static readonly Color Text = Color.FromArgb(232, 238, 246);
    public static readonly Color Muted = Color.FromArgb(142, 160, 181);
    public static readonly Color Ok = Color.FromArgb(61, 214, 140);
    public static readonly Color Bad = Color.FromArgb(255, 107, 107);

    public static void Apply(Form form)
    {
        form.BackColor = Bg;
        form.ForeColor = Text;
        form.Font = new Font("Segoe UI", 10F);
    }

    public static Button GoldButton(string text)
    {
        return new Button
        {
            Text = text,
            BackColor = Gold,
            ForeColor = Color.FromArgb(26, 18, 3),
            FlatStyle = FlatStyle.Flat,
            Height = 36,
            Padding = new Padding(12, 4, 12, 4),
            AutoSize = true,
            Cursor = Cursors.Hand,
        };
    }

    public static Button GhostButton(string text)
    {
        var btn = new Button
        {
            Text = text,
            BackColor = Panel,
            ForeColor = Text,
            FlatStyle = FlatStyle.Flat,
            Height = 36,
            AutoSize = true,
            Cursor = Cursors.Hand,
        };
        btn.FlatAppearance.BorderColor = Color.FromArgb(80, 245, 185, 66);
        return btn;
    }

    public static DataGridView Grid()
    {
        var grid = new DataGridView
        {
            BackgroundColor = Panel,
            ForeColor = Text,
            GridColor = Color.FromArgb(40, 56, 74),
            BorderStyle = BorderStyle.None,
            EnableHeadersVisualStyles = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            Dock = DockStyle.Fill,
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(15, 39, 68);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Gold;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F);
        grid.DefaultCellStyle.BackColor = Panel;
        grid.DefaultCellStyle.ForeColor = Text;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(40, 60, 86);
        grid.DefaultCellStyle.SelectionForeColor = Gold;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(14, 26, 40);
        return grid;
    }
}
