using Microsoft.Extensions.Logging;
using UnAccess.Core.Exceptions;

namespace UnAccess.Desktop.Ui;

internal static class UiErrors
{
    public static void Show(ILogger logger, IWin32Window? owner, Exception exception)
    {
        if (exception is UnAccessException known)
        {
            logger.LogInformation("İşlem reddedildi: {Message}", known.UserMessage);
            MessageBox.Show(owner, known.UserMessage, "UnAccess", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        logger.LogError(exception, "Beklenmeyen hata");
        MessageBox.Show(
            owner,
            "İşlem tamamlanamadı. Teknik ayrıntı günlük dosyasına yazıldı.",
            "UnAccess",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}

internal static class Grids
{
    public static DataGridView Create()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoGenerateColumns = false,
            RowHeadersVisible = false,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.FixedSingle,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToOrderColumns = true
        };
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);
        return grid;
    }

    public static DataGridViewTextBoxColumn Text(string property, string header, float weight)
    {
        return new DataGridViewTextBoxColumn
        {
            Name = property,
            DataPropertyName = property,
            HeaderText = header,
            FillWeight = weight,
            MinimumWidth = 70,
            SortMode = DataGridViewColumnSortMode.Automatic
        };
    }
}

internal static class FormChrome
{
    public static void Apply(Form form, string title, Size clientSize)
    {
        form.Text = title;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.StartPosition = FormStartPosition.CenterParent;
        form.MinimizeBox = false;
        form.MaximizeBox = false;
        form.ShowInTaskbar = false;
        form.ClientSize = clientSize;
        form.Font = SystemFonts.MessageBoxFont;
        form.AutoScaleMode = AutoScaleMode.Font;
    }

    public static TableLayoutPanel Fields(int rows)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = rows,
            Padding = new Padding(12, 12, 12, 4)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < rows; row++)
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        return grid;
    }

    public static void AddField(TableLayoutPanel grid, int row, string label, Control editor)
    {
        grid.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 8, 0)
        }, 0, row);

        editor.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        editor.Margin = new Padding(0, 4, 0, 0);
        grid.Controls.Add(editor, 1, row);
    }

    public static FlowLayoutPanel Buttons(Form form, out Button save, out Button cancel)
    {
        save = new Button { Text = "Kaydet", Width = 96, Height = 28, Margin = new Padding(0, 8, 8, 8) };
        cancel = new Button
        {
            Text = "Vazgeç",
            Width = 96,
            Height = 28,
            Margin = new Padding(0, 8, 12, 8),
            DialogResult = DialogResult.Cancel
        };
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48
        };
        panel.Controls.Add(cancel);
        panel.Controls.Add(save);
        form.AcceptButton = save;
        form.CancelButton = cancel;
        return panel;
    }
}

internal static class Catalog
{
    public static ComboBox Lookup(IEnumerable<UnAccess.Core.Models.LookupItem> items, int? selectedId, bool includeAll)
    {
        var list = new List<UnAccess.Core.Models.LookupItem>();
        if (includeAll)
            list.Add(new UnAccess.Core.Models.LookupItem { Id = 0, Name = "Tümü" });
        list.AddRange(items);
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        combo.DisplayMember = nameof(UnAccess.Core.Models.LookupItem.Display);
        combo.ValueMember = nameof(UnAccess.Core.Models.LookupItem.Id);
        combo.DataSource = list;
        if (selectedId is int id)
            combo.SelectedValue = id;
        return combo;
    }

    public static string ActiveText(bool isActive) => isActive ? "Aktif" : "Pasif";

    public static string YesNo(bool value) => value ? "Evet" : "Hayır";

    public static string Date(DateOnly value) => value.ToString("dd.MM.yyyy");

    public static string Date(DateOnly? value) => value?.ToString("dd.MM.yyyy") ?? "";
}
