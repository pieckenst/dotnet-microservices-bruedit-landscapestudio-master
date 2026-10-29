using System.Text.Json;
using Krypton.Toolkit;

namespace by.pieckenst.bru.windowsclient.landscapestudio;

internal sealed class CreateProjectDialog : KryptonForm
{
    private readonly DesktopApiClient _api;
    private readonly TextBox _number = new() { Dock = DockStyle.Fill, MaxLength = 40 };
    private readonly TextBox _name = new() { Dock = DockStyle.Fill, MaxLength = 200 };
    private readonly TextBox _description = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, MaxLength = 4000 };
    private readonly ComboBox _client = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _plot = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DateTimePicker _start = new() { Dock = DockStyle.Left, Width = 210, Format = DateTimePickerFormat.Custom, CustomFormat = "dd MMM yyyy", ShowCheckBox = true };
    private readonly DateTimePicker _end = new() { Dock = DockStyle.Left, Width = 210, Format = DateTimePickerFormat.Custom, CustomFormat = "dd MMM yyyy", ShowCheckBox = true };
    private readonly Label _status = new() { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(112, 127, 118), AutoSize = false };
    private readonly KryptonButton _save = new() { Width = 175, Height = 40, ButtonStyle = ButtonStyle.Standalone };
    private bool _referencesLoaded;

    public CreateProjectDialog(DesktopApiClient api)
    {
        _api = api;
        Text = "Create project";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 760);
        ClientSize = new Size(790, 790);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10f);
        BackColor = Color.FromArgb(246, 248, 246);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), ColumnCount = 1, RowCount = 8, BackColor = Color.White };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        Controls.Add(root);

        var heading = new Panel { Dock = DockStyle.Fill };
        heading.Controls.Add(new Label { Text = "New landscape project", AutoSize = true, Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold), ForeColor = Color.FromArgb(33, 47, 40) });
        heading.Controls.Add(new Label { Text = "Create a project using existing client and site records.", AutoSize = true, Location = new Point(1, 37), Font = new Font("Segoe UI", 9f), ForeColor = Color.FromArgb(112, 127, 118) });
        root.Controls.Add(heading, 0, 0);
        root.Controls.Add(Field("Project number", _number), 0, 1);
        root.Controls.Add(Field("Project name", _name), 0, 2);
        root.Controls.Add(Field("Client", _client), 0, 3);
        root.Controls.Add(Field("Land plot", _plot), 0, 4);

        var schedule = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 3, 0, 2) };
        schedule.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        schedule.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        schedule.Controls.Add(Field("Planned start (optional)", _start), 0, 0);
        schedule.Controls.Add(Field("Planned end (optional)", _end), 1, 0);
        root.Controls.Add(schedule, 0, 5);

        var descriptionPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        descriptionPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        descriptionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        descriptionPanel.Controls.Add(new Label { Text = "Description", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(80, 98, 86), Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold) }, 0, 0);
        descriptionPanel.Controls.Add(_description, 0, 1);
        root.Controls.Add(descriptionPanel, 0, 6);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _status.Text = "Loading clients and land plots…";
        bottom.Controls.Add(_status, 0, 0);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var cancel = new KryptonButton { Width = 100, Height = 40, ButtonStyle = ButtonStyle.LowProfile };
        cancel.Values.Text = "Cancel";
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        _save.Values.Text = "Create project  →";
        _save.StateCommon.Back.Color1 = Color.FromArgb(66, 125, 91);
        _save.StateCommon.Back.Color2 = Color.FromArgb(66, 125, 91);
        _save.StateCommon.Border.DrawBorders = PaletteDrawBorders.None;
        _save.StateCommon.Content.ShortText.Color1 = Color.White;
        _save.StateCommon.Content.ShortText.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
        _save.Click += async (_, _) => await SaveAsync();
        _save.Enabled = false;
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(_save);
        bottom.Controls.Add(buttons, 1, 0);
        root.Controls.Add(bottom, 0, 7);
        Shown += async (_, _) => await LoadReferencesAsync();
        AcceptButton = _save;
        CancelButton = cancel;
    }

    private static Control Field(string labelText, Control editor)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = labelText, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(80, 98, 86), Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold) }, 0, 0);
        if (editor is TextBox text) { text.BorderStyle = BorderStyle.FixedSingle; text.Font = new Font("Segoe UI", 10f); }
        if (editor is DateTimePicker date) { date.Font = new Font("Segoe UI", 9.5f); date.Margin = new Padding(0, 0, 5, 0); }
        if (editor is ComboBox combo) { combo.Font = new Font("Segoe UI", 10f); combo.IntegralHeight = false; combo.DropDownHeight = 240; }
        panel.Controls.Add(editor, 0, 1);
        return panel;
    }

    private async Task LoadReferencesAsync()
    {
        _save.Enabled = false;
        try
        {
            var clients = await _api.GetAsync("/Clients?page=1&pageSize=200");
            var plots = await _api.GetAsync("/LandPlots?page=1&pageSize=200");
            FillCombo(_client, clients, "displayName", "id");
            FillCombo(_plot, plots, "name", "id", "address");
            _referencesLoaded = _client.Items.Count > 0 && _plot.Items.Count > 0;
            _status.ForeColor = _referencesLoaded ? Color.FromArgb(75, 115, 83) : Color.DarkGoldenrod;
            _status.Text = _referencesLoaded
                ? $"Loaded {_client.Items.Count} clients and {_plot.Items.Count} land plots."
                : "At least one active client and land plot are required before creating a project.";
            _save.Enabled = _referencesLoaded;
        }
        catch (Exception exception) when (exception is DesktopApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = $"Could not load project references: {exception.Message}";
        }
    }

    private static void FillCombo(ComboBox combo, JsonElement root, string primary, string id, string? secondary = null)
    {
        var rows = root;
        if (root.ValueKind == JsonValueKind.Object &&
            (root.TryGetProperty("items", out var lower) || root.TryGetProperty("Items", out lower)))
            rows = lower;
        combo.Items.Clear();
        if (rows.ValueKind != JsonValueKind.Array) return;
        foreach (var row in rows.EnumerateArray())
        {
            if (!row.TryGetProperty(id, out var idValue) || idValue.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(idValue.GetString(), out var entityId)) continue;
            var label = ReadString(row, primary) ?? "(unnamed)";
            if (secondary is not null && ReadString(row, secondary) is { Length: > 0 } detail)
                label += " — " + detail;
            if (ReadBoolean(row, "isArchived") || ReadBoolean(row, "IsArchived")) continue;
            combo.Items.Add(new ReferenceItem(entityId, label));
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private async Task SaveAsync()
    {
        _status.ForeColor = Color.FromArgb(112, 127, 118);
        _status.Text = string.Empty;
        var number = _number.Text.Trim();
        var name = _name.Text.Trim();
        if (string.IsNullOrWhiteSpace(number) || number.Length > 40 ||
            number.Any(c => !(char.IsLetterOrDigit(c) || c is '.' or '_' or '/' or '-')))
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = "Project number is required (maximum 40 letters, digits, dots, underscores, slashes or hyphens).";
            return;
        }
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = "Project name is required and must be no longer than 200 characters.";
            return;
        }
        if (_client.SelectedItem is not ReferenceItem client || _plot.SelectedItem is not ReferenceItem plot)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = "Choose a client and a land plot.";
            return;
        }
        if (_start.Checked && _end.Checked && _end.Value.Date < _start.Value.Date)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = "Planned end date cannot precede planned start date.";
            return;
        }

        var request = new CreateProjectRequest(
            number, name, client.Id, plot.Id, _description.Text.Trim(),
            _start.Checked ? DateOnly.FromDateTime(_start.Value) : null,
            _end.Checked ? DateOnly.FromDateTime(_end.Value) : null);
        _save.Enabled = false;
        _status.Text = "Saving project…";
        try
        {
            await _api.PostAsync("Projects", request);
            _status.ForeColor = Color.FromArgb(75, 115, 83);
            _status.Text = "Project created successfully.";
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (DesktopApiException exception)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = exception.Message;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = exception is TaskCanceledException ? "The request timed out." : exception.Message;
        }
        finally
        {
            if (!IsDisposed) _save.Enabled = _referencesLoaded;
        }
    }

    private static string? ReadString(JsonElement parent, params string[] keys)
    {
        if (parent.ValueKind != JsonValueKind.Object) return null;
        foreach (var key in keys)
            if (parent.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        return null;
    }

    private static bool ReadBoolean(JsonElement parent, string key) =>
        parent.ValueKind == JsonValueKind.Object &&
        parent.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;

    private sealed record ReferenceItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record CreateProjectRequest(
        string ProjectNumber, string Name, Guid ClientId, Guid LandPlotId,
        string Description, DateOnly? PlannedStartDate, DateOnly? PlannedEndDate);
}
