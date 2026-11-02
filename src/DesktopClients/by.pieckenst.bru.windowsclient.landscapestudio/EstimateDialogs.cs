using System.Data;
using System.Text.Json;
using Krypton.Toolkit;

namespace by.pieckenst.bru.windowsclient.landscapestudio;

internal sealed class CreateEstimateDialog : KryptonForm
{
    private readonly DesktopApiClient _api;
    private readonly ComboBox _project = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _version = new() { Dock = DockStyle.Fill, MaxLength = 40, Text = "V1" };
    private readonly TextBox _notes = new() { Dock = DockStyle.Fill, Multiline = true, MaxLength = 4000 };
    private readonly Label _status = new() { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(112, 127, 118), AutoSize = false };
    private readonly KryptonButton _save = new() { Width = 155, Height = 40, ButtonStyle = ButtonStyle.Standalone };

    public CreateEstimateDialog(DesktopApiClient api)
    {
        _api = api;
        Text = "Create estimate";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(650, 500);
        ClientSize = new Size(680, 510);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10f);
        BackColor = Color.FromArgb(246, 248, 246);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(28), BackColor = Color.White };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 73));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 73));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        Controls.Add(root);

        var heading = new Panel { Dock = DockStyle.Fill };
        heading.Controls.Add(new Label { Text = "New estimate draft", AutoSize = true, Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold), ForeColor = Color.FromArgb(33, 47, 40) });
        heading.Controls.Add(new Label { Text = "Choose a project and create a versioned estimate.", AutoSize = true, Location = new Point(1, 34), Font = new Font("Segoe UI", 9f), ForeColor = Color.FromArgb(112, 127, 118) });
        root.Controls.Add(heading, 0, 0);
        root.Controls.Add(Field("Project", _project), 0, 1);
        root.Controls.Add(Field("Version", _version), 0, 2);
        var notePanel = Field("Notes", _notes);
        root.Controls.Add(notePanel, 0, 3);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(_status, 0, 0);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var cancel = new KryptonButton { Width = 95, Height = 40, ButtonStyle = ButtonStyle.LowProfile };
        cancel.Values.Text = "Cancel";
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        _save.Values.Text = "Create draft  →";
        SetPrimaryStyle(_save);
        _save.Enabled = false;
        _save.Click += async (_, _) => await CreateAsync();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(_save);
        bottom.Controls.Add(buttons, 1, 0);
        root.Controls.Add(bottom, 0, 4);
        Shown += async (_, _) => await LoadProjectsAsync();
        AcceptButton = _save;
        CancelButton = cancel;
    }

    public Guid CreatedEstimateId { get; private set; }

    private static Control Field(string label, Control input)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(80, 98, 86), Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold) }, 0, 0);
        if (input is TextBox text)
        {
            text.BorderStyle = BorderStyle.FixedSingle;
            text.Font = new Font("Segoe UI", 10f);
        }
        panel.Controls.Add(input, 0, 1);
        return panel;
    }

    private async Task LoadProjectsAsync()
    {
        _status.Text = "Loading projects…";
        try
        {
            var response = await _api.GetAsync("/Projects?page=1&pageSize=200");
            var items = GetItems(response);
            _project.Items.Clear();
            if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (ReadBoolean(item, "isArchived")) continue;
                    if (!Guid.TryParse(ReadString(item, "id"), out var id) || id == Guid.Empty) continue;
                    var number = ReadString(item, "projectNumber") ?? string.Empty;
                    var name = ReadString(item, "name") ?? "(unnamed project)";
                    _project.Items.Add(new ProjectItem(id, string.IsNullOrWhiteSpace(number) ? name : $"{number} — {name}"));
                }
            }
            if (_project.Items.Count > 0)
            {
                _project.SelectedIndex = 0;
                _save.Enabled = true;
                _status.Text = $"Loaded {_project.Items.Count} project(s).";
            }
            else
            {
                _status.ForeColor = Color.DarkGoldenrod;
                _status.Text = "Create a project first; estimates must belong to an existing project.";
            }
        }
        catch (Exception exception) when (exception is DesktopApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = $"Could not load projects: {exception.Message}";
        }
    }

    private async Task CreateAsync()
    {
        if (_project.SelectedItem is not ProjectItem project)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = "Select a project.";
            return;
        }
        var version = _version.Text.Trim();
        if (string.IsNullOrWhiteSpace(version) || version.Length > 40)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = "A version label of 1 to 40 characters is required.";
            return;
        }
        _save.Enabled = false;
        _status.ForeColor = Color.FromArgb(112, 127, 118);
        _status.Text = "Creating estimate draft…";
        try
        {
            var result = await _api.PostAsync("Estimates", new { projectId = project.Id, version, notes = _notes.Text.Trim() });
            if (!Guid.TryParse(ReadString(result, "id"), out var id) || id == Guid.Empty)
                throw new DesktopApiException(System.Net.HttpStatusCode.OK, "The Project API did not return the new estimate id.");
            CreatedEstimateId = id;
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
            if (!IsDisposed) _save.Enabled = _project.Items.Count > 0;
        }
    }

    internal static JsonElement GetItems(JsonElement response)
    {
        if (response.ValueKind == JsonValueKind.Object)
        {
            if (TryGetProperty(response, "items", out var items)) return items;
            if (TryGetProperty(response, "data", out var data) && data.ValueKind == JsonValueKind.Array) return data;
        }
        return response;
    }

    internal static bool TryGetProperty(JsonElement source, string name, out JsonElement value)
    {
        if (source.ValueKind == JsonValueKind.Object)
            foreach (var property in source.EnumerateObject())
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
        value = default;
        return false;
    }

    internal static string? ReadString(JsonElement source, string name)
    {
        if (!TryGetProperty(source, name, out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    internal static bool ReadBoolean(JsonElement source, string name) =>
        TryGetProperty(source, name, out var value) && value.ValueKind == JsonValueKind.True;

    internal static void SetPrimaryStyle(KryptonButton button)
    {
        button.StateCommon.Back.Color1 = Color.FromArgb(66, 125, 91);
        button.StateCommon.Back.Color2 = Color.FromArgb(66, 125, 91);
        button.StateCommon.Border.DrawBorders = PaletteDrawBorders.None;
        button.StateCommon.Content.ShortText.Color1 = Color.White;
        button.StateCommon.Content.ShortText.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
        button.StateTracking.Back.Color1 = Color.FromArgb(52, 104, 73);
        button.StateTracking.Back.Color2 = Color.FromArgb(52, 104, 73);
    }

    private sealed record ProjectItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }
}

internal sealed class EstimateDetailsDialog : KryptonForm
{
    private readonly DesktopApiClient _api;
    private readonly Guid _estimateId;
    private readonly Label _summary = new() { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(112, 127, 118), AutoSize = false };
    private readonly Label _status = new() { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(112, 127, 118), AutoSize = false };
    private readonly DataGridView _grid = CreateGrid();
    private readonly ComboBox _itemType = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _catalogItem = new() { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
    private bool _loadingCatalogItems;
    private bool _suppressCatalogReload;
    private readonly TextBox _description = new() { Width = 230, MaxLength = 500 };
    private readonly NumericUpDown _quantity = new() { Width = 86, DecimalPlaces = 3, Minimum = .001M, Maximum = 1000000000M, Value = 1, Increment = .5M };
    private readonly TextBox _unit = new() { Width = 64, MaxLength = 24, Text = "pcs" };
    private readonly NumericUpDown _price = new() { Width = 105, DecimalPlaces = 2, Minimum = 0, Maximum = 1000000000M, Increment = 1M };
    private readonly NumericUpDown _cost = new() { Width = 105, DecimalPlaces = 2, Minimum = 0, Maximum = 1000000000M, Increment = 1M };
    private readonly CheckBox _hasCost = new() { Text = "Cost known", AutoSize = true, Checked = false };
    private readonly KryptonButton _add = new() { Width = 125, Height = 36, ButtonStyle = ButtonStyle.Standalone };
    private readonly KryptonButton _remove = new() { Width = 105, Height = 36, ButtonStyle = ButtonStyle.LowProfile };
    private readonly KryptonButton _submit = new() { Width = 155, Height = 36, ButtonStyle = ButtonStyle.Standalone };
    private readonly KryptonButton _approve = new() { Width = 115, Height = 36, ButtonStyle = ButtonStyle.Standalone };
    private readonly KryptonButton _reject = new() { Width = 100, Height = 36, ButtonStyle = ButtonStyle.Standalone };
    private string _state = "Draft";
    private bool _busy;

    public EstimateDetailsDialog(DesktopApiClient api, Guid estimateId)
    {
        _api = api;
        _estimateId = estimateId;
        Text = "Estimate details";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1000, 730);
        ClientSize = new Size(1160, 800);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.FromArgb(246, 248, 246);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(20), BackColor = Color.White };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 165));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.Controls.Add(new Label { Text = "Estimate details", AutoSize = true, ForeColor = Color.FromArgb(33, 47, 40), Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold) }, 0, 0);
        header.Controls.Add(_summary, 0, 1);
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(_grid, 0, 1);
        root.Controls.Add(BuildLineEditor(), 0, 2);
        root.Controls.Add(BuildTransitions(), 0, 3);

        _itemType.Items.Add(new ItemType(0, "Work"));
        _itemType.Items.Add(new ItemType(1, "Material"));
        _itemType.Items.Add(new ItemType(2, "Plant"));
        _itemType.Items.Add(new ItemType(3, "Equipment"));
        _itemType.Items.Add(new ItemType(4, "Other"));
        _itemType.SelectedIndex = 1;
        _catalogItem.Items.Add(new CatalogItemOption(null, 1, "Custom line — no Catalog reference", string.Empty, "pcs", 0m));
        _catalogItem.SelectedIndex = 0;
        _itemType.SelectedIndexChanged += async (_, _) =>
        {
            if (!_suppressCatalogReload)
                await LoadCatalogItemsAsync();
        };
        _catalogItem.SelectedIndexChanged += (_, _) => ApplyCatalogSelection();
        _add.Values.Text = "Add line item";
        CreateEstimateDialog.SetPrimaryStyle(_add);
        _add.Click += async (_, _) => await AddLineItemAsync();
        _remove.Values.Text = "Remove line";
        _remove.StateCommon.Content.ShortText.Color1 = Color.FromArgb(153, 62, 56);
        _remove.Enabled = false;
        _remove.Click += async (_, _) => await RemoveLineItemAsync();
        _grid.SelectionChanged += (_, _) => UpdateRemoveButton();
        _submit.Values.Text = "Submit for approval";
        CreateEstimateDialog.SetPrimaryStyle(_submit);
        _submit.Click += async (_, _) => await TransitionAsync("submit");
        _approve.Values.Text = "Approve";
        CreateEstimateDialog.SetPrimaryStyle(_approve);
        _approve.Click += async (_, _) => await TransitionAsync("approve");
        _reject.Values.Text = "Reject";
        _reject.Click += async (_, _) => await TransitionAsync("reject");
        Shown += async (_, _) => { await LoadCatalogItemsAsync(); await RefreshEstimateAsync(); };
    }

    private Control BuildLineEditor()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(0, 8, 0, 0), Margin = Padding.Empty };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = "Add line item (drafts only)", AutoSize = true, ForeColor = Color.FromArgb(33, 47, 40), Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold) }, 0, 0);
        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true, Margin = Padding.Empty };
        _itemType.Font = _catalogItem.Font = _description.Font = _unit.Font = new Font("Segoe UI", 9f);
        _quantity.Font = _price.Font = _cost.Font = new Font("Segoe UI", 9f);
        row.Controls.Add(_itemType);
        row.Controls.Add(_catalogItem);
        row.Controls.Add(_description);
        row.Controls.Add(_quantity);
        row.Controls.Add(_unit);
        row.Controls.Add(_price);
        row.Controls.Add(_cost);
        row.Controls.Add(_hasCost);
        row.Controls.Add(_add);
        row.Controls.Add(_remove);
        panel.Controls.Add(row, 0, 1);
        panel.Controls.Add(_status, 0, 2);
        return panel;
    }

    private Control BuildTransitions()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true, Padding = new Padding(0, 6, 0, 0) };
        var refresh = new KryptonButton { Width = 100, Height = 36, ButtonStyle = ButtonStyle.LowProfile };
        refresh.Values.Text = "Refresh";
        refresh.Click += async (_, _) => await RefreshEstimateAsync();
        var close = new KryptonButton { Width = 100, Height = 36, ButtonStyle = ButtonStyle.LowProfile };
        close.Values.Text = "Close";
        close.Click += (_, _) => Close();
        panel.Controls.Add(_submit);
        panel.Controls.Add(_approve);
        panel.Controls.Add(_reject);
        panel.Controls.Add(refresh);
        panel.Controls.Add(close);
        return panel;
    }

    private async Task RefreshEstimateAsync()
    {
        SetBusy(true);
        _status.ForeColor = Color.FromArgb(112, 127, 118);
        _status.Text = "Loading estimate…";
        try
        {
            var estimate = await _api.GetAsync($"Estimates/{_estimateId:D}");
            _state = ReadEnum(estimate, "status", ["Draft", "Submitted", "Approved", "Rejected", "Superseded"]);
            var version = CreateEstimateDialog.ReadString(estimate, "version") ?? string.Empty;
            var total = ReadDecimal(estimate, "estimatedTotal");
            var project = CreateEstimateDialog.ReadString(estimate, "projectId") ?? string.Empty;
            _summary.Text = $"Version {version}   ·   Status: {_state}   ·   Total: {total:N2}   ·   Project {project}";
            var lineItems = CreateEstimateDialog.TryGetProperty(estimate, "lineItems", out var returnedLines)
                ? returnedLines
                : default;
            _grid.DataSource = ToDataTable(lineItems);
            _status.ForeColor = Color.FromArgb(75, 115, 83);
            _status.Text = $"Loaded {(_grid.Rows.Count):N0} line-item row(s).";
        }
        catch (Exception exception) when (exception is DesktopApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = exception.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task LoadCatalogItemsAsync()
    {
        if (_loadingCatalogItems || IsDisposed) return;
        if (_itemType.SelectedItem is not ItemType itemType) return;

        var resource = itemType.Value switch
        {
            0 => "work-types",
            1 => "materials",
            2 => "plants",
            _ => null
        };

        _loadingCatalogItems = true;
        _catalogItem.Enabled = false;
        try
        {
            _catalogItem.BeginUpdate();
            _catalogItem.Items.Clear();
            _catalogItem.Items.Add(new CatalogItemOption(
                null, itemType.Value, "Custom line — no Catalog reference", string.Empty, "pcs", 0m));
            _catalogItem.SelectedIndex = 0;

            if (resource is null)
            {
                _status.ForeColor = Color.FromArgb(112, 127, 118);
                _status.Text = "Equipment and other lines are entered manually; they are not Catalog item types.";
                return;
            }

            var unitsResponse = await _api.GetAsync("Catalog/MeasurementUnits?page=1&pageSize=200");
            var units = CreateEstimateDialog.GetItems(unitsResponse);
            var unitSymbols = new Dictionary<Guid, string>();
            if (units.ValueKind == JsonValueKind.Array)
            {
                foreach (var unit in units.EnumerateArray())
                {
                    if (!CreateEstimateDialog.ReadBoolean(unit, "isActive")) continue;
                    if (!Guid.TryParse(CreateEstimateDialog.ReadString(unit, "id"), out var unitId) || unitId == Guid.Empty) continue;
                    var symbol = CreateEstimateDialog.ReadString(unit, "symbol");
                    if (!string.IsNullOrWhiteSpace(symbol)) unitSymbols[unitId] = symbol.Trim();
                }
            }

            var response = await _api.GetAsync($"Catalog/{resource}?page=1&pageSize=200");
            var items = CreateEstimateDialog.GetItems(response);
            var loaded = 0;
            if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (!CreateEstimateDialog.ReadBoolean(item, "isActive")) continue;
                    if (!Guid.TryParse(CreateEstimateDialog.ReadString(item, "id"), out var id) || id == Guid.Empty) continue;
                    if (!Guid.TryParse(CreateEstimateDialog.ReadString(item, "measurementUnitId"), out var unitId) ||
                        !unitSymbols.TryGetValue(unitId, out var symbol)) continue;

                    var name = CreateEstimateDialog.ReadString(item, "name");
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var description = CreateEstimateDialog.ReadString(item, "description") ?? name;
                    var priceProperty = itemType.Value == 0 ? "basePrice" : "unitPrice";
                    var unitPrice = ReadDecimal(item, priceProperty);
                    _catalogItem.Items.Add(new CatalogItemOption(id, itemType.Value,
                        name.Trim(), description.Trim(), symbol, unitPrice));
                    loaded++;
                }
            }

            _status.ForeColor = loaded > 0 ? Color.FromArgb(75, 115, 83) : Color.DarkGoldenrod;
            _status.Text = loaded > 0
                ? $"Loaded {loaded} active Catalog item(s). Select one to prefill the estimate line."
                : "No active Catalog items with active measurement units are available. A custom line can still be added.";
        }
        catch (Exception exception) when (exception is DesktopApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            _status.ForeColor = Color.DarkGoldenrod;
            _status.Text = $"Catalog lookup unavailable; custom estimate lines remain available. {exception.Message}";
            if (_catalogItem.Items.Count == 0)
            {
                _catalogItem.Items.Add(new CatalogItemOption(
                    null, itemType.Value, "Custom line — no Catalog reference", string.Empty, "pcs", 0m));
                _catalogItem.SelectedIndex = 0;
            }
        }
        finally
        {
            if (_catalogItem.IsHandleCreated) _catalogItem.EndUpdate();
            _catalogItem.Enabled = !IsDisposed;
            _loadingCatalogItems = false;
        }
    }

    private void ApplyCatalogSelection()
    {
        if (_catalogItem.SelectedItem is not CatalogItemOption selected || !selected.Id.HasValue)
            return;

        var matchingType = Enumerable.Range(0, _itemType.Items.Count)
            .Select(index => _itemType.Items[index])
            .OfType<ItemType>()
            .FirstOrDefault(item => item.Value == selected.ItemType);
        if (matchingType is not null && !Equals(_itemType.SelectedItem, matchingType))
        {
            _suppressCatalogReload = true;
            try { _itemType.SelectedItem = matchingType; }
            finally { _suppressCatalogReload = false; }
        }

        _description.Text = selected.Description;
        _unit.Text = selected.UnitSymbol;
        _price.Value = Math.Clamp(decimal.Round(selected.UnitPrice, 2), _price.Minimum, _price.Maximum);
        _status.ForeColor = Color.FromArgb(75, 115, 83);
        _status.Text = $"Catalog item selected: {selected.Name}. You can adjust the estimate description and selling price.";
    }

    private async Task AddLineItemAsync()
    {
        if (_state != "Draft")
        {
            _status.Text = "Line items can only be edited while the estimate is a draft.";
            return;
        }
        if (_itemType.SelectedItem is not ItemType itemType || string.IsNullOrWhiteSpace(_description.Text) || string.IsNullOrWhiteSpace(_unit.Text))
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = "Choose a line type and enter a description and unit.";
            return;
        }
        SetBusy(true);
        try
        {
            var payload = new
            {
                itemType = itemType.Value,
                description = _description.Text.Trim(),
                quantity = _quantity.Value,
                unitSymbol = _unit.Text.Trim(),
                unitPrice = _price.Value,
                unitCost = _hasCost.Checked ? _cost.Value : (decimal?)null,
                catalogItemId = (_catalogItem.SelectedItem as CatalogItemOption)?.Id
            };
            await _api.PostAsync($"Estimates/{_estimateId:D}/line-items", payload);
            _description.Clear();
            _status.ForeColor = Color.FromArgb(75, 115, 83);
            _status.Text = "Line item added.";
            await RefreshEstimateAsync();
        }
        catch (Exception exception) when (exception is DesktopApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = exception.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void UpdateRemoveButton() =>
        _remove.Enabled = !_busy && _state == "Draft" && _grid.SelectedRows.Count > 0;

    private async Task RemoveLineItemAsync()
    {
        if (_state != "Draft" || _grid.SelectedRows.Count == 0)
            return;

        var idColumn = _grid.Columns.Cast<DataGridViewColumn>()
            .FirstOrDefault(column => column.Name.Equals("id", StringComparison.OrdinalIgnoreCase));
        var rawId = idColumn is null
            ? null
            : _grid.SelectedRows[0].Cells[idColumn.Index].Value?.ToString();

        if (!Guid.TryParse(rawId, out var lineItemId) || lineItemId == Guid.Empty)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = "The selected row does not contain a valid estimate line-item id.";
            return;
        }

        var confirmed = MessageBox.Show(this,
            "Remove the selected line item from this draft estimate?",
            "Confirm line removal", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirmed != DialogResult.Yes)
            return;

        SetBusy(true);
        try
        {
            await _api.DeleteAsync($"Estimates/{_estimateId:D}/line-items/{lineItemId:D}");
            _status.ForeColor = Color.FromArgb(75, 115, 83);
            _status.Text = "Line item removed.";
            await RefreshEstimateAsync();
        }
        catch (Exception exception) when (exception is DesktopApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = exception.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task TransitionAsync(string action)
    {
        if (action == "submit" && _state != "Draft" ||
            action is "approve" or "reject" && _state != "Submitted")
        {
            _status.ForeColor = Color.DarkGoldenrod;
            _status.Text = $"The {action} action is not available while this estimate is {_state}.";
            return;
        }
        SetBusy(true);
        try
        {
            await _api.PostAsync($"Estimates/{_estimateId:D}/{action}", new { });
            _status.ForeColor = Color.FromArgb(75, 115, 83);
            _status.Text = $"Estimate {action} request completed.";
            await RefreshEstimateAsync();
        }
        catch (Exception exception) when (exception is DesktopApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = exception.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _add.Enabled = !busy && _state == "Draft";
        _remove.Enabled = !busy && _state == "Draft" && _grid.SelectedRows.Count > 0;
        _itemType.Enabled = _catalogItem.Enabled = _description.Enabled = _unit.Enabled = !busy && _state == "Draft";
        _quantity.Enabled = _price.Enabled = _cost.Enabled = _hasCost.Enabled = !busy && _state == "Draft";
        _submit.Enabled = !busy && _state == "Draft" && _grid.Rows.Count > 0;
        _approve.Enabled = _reject.Enabled = !busy && _state == "Submitted";
        UseWaitCursor = busy;
    }

    private static DataTable ToDataTable(JsonElement items)
    {
        var table = new DataTable();
        if (items.ValueKind != JsonValueKind.Array) return table;
        var rows = items.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).ToArray();
        var columns = rows.SelectMany(x => x.EnumerateObject())
            .Where(p => p.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null or JsonValueKind.Undefined))
            .Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var column in columns) table.Columns.Add(column, typeof(string));
        foreach (var row in rows)
        {
            var data = table.NewRow();
            foreach (var property in row.EnumerateObject())
                if (table.Columns.Contains(property.Name))
                    data[property.Name] = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() ?? string.Empty : property.Value.ToString();
            table.Rows.Add(data);
        }
        return table;
    }

    private static string ReadEnum(JsonElement source, string name, string[] values)
    {
        if (!CreateEstimateDialog.TryGetProperty(source, name, out var value)) return "Unknown";
        if (value.ValueKind == JsonValueKind.String) return value.GetString() ?? "Unknown";
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var index) && index >= 0 && index < values.Length
            ? values[index] : "Unknown";
    }

    private static decimal ReadDecimal(JsonElement source, string name) =>
        CreateEstimateDialog.TryGetProperty(source, name, out var value) && value.TryGetDecimal(out var number) ? number : 0m;

    private static DataGridView CreateGrid() => new()
    {
        Dock = DockStyle.Fill, BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
        GridColor = Color.FromArgb(232, 238, 231), EnableHeadersVisualStyles = false,
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(248, 250, 247), ForeColor = Color.FromArgb(112, 127, 118),
            Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold), SelectionBackColor = Color.FromArgb(248, 250, 247),
            SelectionForeColor = Color.FromArgb(112, 127, 118), Padding = new Padding(5)
        },
        DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White, ForeColor = Color.FromArgb(55, 73, 61), SelectionBackColor = Color.FromArgb(230, 240, 229),
            SelectionForeColor = Color.FromArgb(33, 47, 40), Font = new Font("Segoe UI", 9f), Padding = new Padding(4)
        },
        RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells, ReadOnly = true, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect
    };

    private sealed record ItemType(int Value, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record CatalogItemOption(
        Guid? Id, int ItemType, string Name, string Description, string UnitSymbol, decimal UnitPrice)
    {
        public override string ToString() => Name;
    }
}
