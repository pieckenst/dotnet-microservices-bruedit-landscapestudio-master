using System.Globalization;
using System.Net;
using System.Text.Json;
using Krypton.Toolkit;

namespace by.pieckenst.bru.windowsclient.landscapestudio;

internal abstract class OperationDialogBase : KryptonForm
{
    protected readonly DesktopApiClient Api;
    protected readonly FlowLayoutPanel Fields;
    protected readonly Label Status;
    protected readonly KryptonButton SaveButton;

    protected OperationDialogBase(DesktopApiClient api, string title, string subtitle, int height = 650)
    {
        Api = api ?? throw new ArgumentNullException(nameof(api));
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(620, 520);
        ClientSize = new Size(680, height);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10f);
        BackColor = Color.FromArgb(246, 248, 246);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Padding = new Padding(26), BackColor = Color.White
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Fill };
        header.Controls.Add(new Label
        {
            Text = title, AutoSize = true,
            Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 47, 40), Location = new Point(0, 0)
        });
        header.Controls.Add(new Label
        {
            Text = subtitle, AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(112, 127, 118), Location = new Point(1, 39)
        });
        root.Controls.Add(header, 0, 0);

        Fields = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, BackColor = Color.White,
            Padding = new Padding(0, 4, 10, 4)
        };
        root.Controls.Add(Fields, 0, 1);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        Status = new Label
        {
            Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(112, 127, 118), Font = new Font("Segoe UI", 8.5f)
        };
        footer.Controls.Add(Status, 0, 0);
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, AutoSize = true
        };
        var cancel = new KryptonButton { Width = 96, Height = 40, ButtonStyle = ButtonStyle.LowProfile };
        cancel.Values.Text = "Cancel";
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        SaveButton = new KryptonButton { Width = 156, Height = 40, ButtonStyle = ButtonStyle.Standalone };
        SaveButton.Values.Text = "Create record  →";
        SaveButton.StateCommon.Back.Color1 = Color.FromArgb(66, 125, 91);
        SaveButton.StateCommon.Back.Color2 = Color.FromArgb(66, 125, 91);
        SaveButton.StateCommon.Border.DrawBorders = PaletteDrawBorders.None;
        SaveButton.StateCommon.Content.ShortText.Color1 = Color.White;
        SaveButton.StateCommon.Content.ShortText.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
        SaveButton.StateTracking.Back.Color1 = Color.FromArgb(52, 104, 73);
        SaveButton.StateTracking.Back.Color2 = Color.FromArgb(52, 104, 73);
        SaveButton.Click += async (_, _) => await SaveAsync();
        actions.Controls.Add(cancel);
        actions.Controls.Add(SaveButton);
        footer.Controls.Add(actions, 1, 0);
        root.Controls.Add(footer, 0, 2);
        CancelButton = cancel;
        AcceptButton = SaveButton;
    }

    protected virtual Task SaveAsync() => Task.CompletedTask;

    protected void AddField(string label, Control editor, int width = 580, int height = 64)
    {
        var panel = new TableLayoutPanel
        {
            Width = width, Height = height, ColumnCount = 1, RowCount = 2,
            Margin = new Padding(0, 2, 0, 3), BackColor = Color.White
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label
        {
            Text = label, Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(80, 98, 86),
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold)
        }, 0, 0);
        editor.Dock = DockStyle.Fill;
        if (editor is TextBox text)
        {
            text.BorderStyle = BorderStyle.FixedSingle;
            text.Font = new Font("Segoe UI", 10f);
        }
        else if (editor is ComboBox combo)
        {
            combo.Font = new Font("Segoe UI", 10f);
            combo.IntegralHeight = false;
            combo.DropDownHeight = 220;
        }
        else if (editor is NumericUpDown number)
            number.Font = new Font("Segoe UI", 10f);
        panel.Controls.Add(editor, 0, 1);
        Fields.Controls.Add(panel);
    }

    protected void AddCheckBox(string text, CheckBox checkBox, int width = 580)
    {
        checkBox.Text = text;
        checkBox.Width = width;
        checkBox.Height = 34;
        checkBox.Font = new Font("Segoe UI", 9.5f);
        checkBox.ForeColor = Color.FromArgb(55, 73, 61);
        Fields.Controls.Add(checkBox);
    }

    protected void SetError(string message)
    {
        Status.ForeColor = Color.Firebrick;
        Status.Text = message.Length > 240 ? message[..240] : message;
    }

    protected void SetInfo(string message)
    {
        Status.ForeColor = Color.FromArgb(75, 115, 83);
        Status.Text = message;
    }

    protected async Task<bool> SubmitAsync(string path, object payload)
    {
        SaveButton.Enabled = false;
        SetInfo("Saving through the API gateway…");
        try
        {
            var result = await Api.PostAsync(path, payload);
            var id = CreateEstimateDialog.ReadString(result, "id");
            if (!Guid.TryParse(id, out var recordId) || recordId == Guid.Empty)
                throw new DesktopApiException(HttpStatusCode.OK, "The service did not return a record id. Check the service response before retrying.");
            DialogResult = DialogResult.OK;
            Close();
            return true;
        }
        catch (DesktopApiException exception)
        {
            SetError(exception.Message);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            SetError(exception is TaskCanceledException
                ? "The request timed out. The service may still be processing it; refresh the list before retrying."
                : exception.Message);
        }
        finally
        {
            if (!IsDisposed) SaveButton.Enabled = true;
        }
        return false;
    }
}

internal sealed class CreateReferenceRecordDialog : OperationDialogBase
{
    private readonly string _resource;
    private readonly Guid? _recordId;
    private readonly TextBox _name = new() { MaxLength = 160 };
    private readonly TextBox _email = new() { MaxLength = 254 };
    private readonly TextBox _phone = new() { MaxLength = 40 };
    private readonly TextBox _address = new() { MaxLength = 500 };
    private readonly NumericUpDown _area = new() { Minimum = 0.01m, Maximum = 1_000_000_000m, DecimalPlaces = 2, Increment = 1m, Value = 100m };
    private readonly TextBox _soilType = new() { MaxLength = 100 };
    private readonly TextBox _notes = new() { MaxLength = 2000, Multiline = true, ScrollBars = ScrollBars.Vertical };

    public CreateReferenceRecordDialog(DesktopApiClient api, string resource, Guid? recordId = null)
        : base(api,
            recordId.HasValue ? (resource == "Clients" ? "Edit client" : "Edit land plot") :
                (resource == "Clients" ? "New client" : "New land plot"),
            resource == "Clients"
                ? "Maintain client identity and contact details used by project records."
                : "Maintain the project site address, area and site conditions.",
            resource == "Clients" ? 560 : 700)
    {
        if (resource is not ("Clients" or "LandPlots"))
            throw new ArgumentOutOfRangeException(nameof(resource), "Only Project-owned client and land-plot records are supported.");

        _resource = resource;
        _recordId = recordId;
        if (resource == "Clients")
        {
            AddField("Display name *", _name);
            AddField("Email", _email);
            AddField("Phone", _phone);
            AddField("Billing address", _address, height: 88);
        }
        else
        {
            AddField("Plot name *", _name);
            AddField("Site address *", _address, height: 88);
            AddField("Area (m²) *", _area);
            AddField("Soil type", _soilType);
            AddField("Site notes", _notes, height: 100);
        }

        SaveButton.Values.Text = recordId.HasValue ? "Save changes  →" : "Create record  →";
        Shown += async (_, _) =>
        {
            if (_recordId.HasValue)
                await LoadRecordAsync();
        };
    }

    private async Task LoadRecordAsync()
    {
        SaveButton.Enabled = false;
        SetInfo("Loading record…");
        try
        {
            var item = await Api.GetAsync($"/{_resource}/{_recordId:D}");
            _name.Text = CreateEstimateDialog.ReadString(item, _resource == "Clients" ? "displayName" : "name") ?? string.Empty;
            if (_resource == "Clients")
            {
                _email.Text = CreateEstimateDialog.ReadString(item, "email") ?? string.Empty;
                _phone.Text = CreateEstimateDialog.ReadString(item, "phone") ?? string.Empty;
                _address.Text = CreateEstimateDialog.ReadString(item, "billingAddress") ?? string.Empty;
            }
            else
            {
                _address.Text = CreateEstimateDialog.ReadString(item, "address") ?? string.Empty;
                if (TryReadDecimal(item, "areaSquareMetres", out var area))
                    _area.Value = Math.Clamp(area, _area.Minimum, _area.Maximum);
                _soilType.Text = CreateEstimateDialog.ReadString(item, "soilType") ?? string.Empty;
                _notes.Text = CreateEstimateDialog.ReadString(item, "notes") ?? string.Empty;
            }
            SetInfo("Record loaded. Changes will be validated by the Project service.");
        }
        catch (Exception exception) when (exception is DesktopApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            SetError("Record could not be loaded: " + exception.Message);
            return;
        }
        finally
        {
            if (!IsDisposed) SaveButton.Enabled = true;
        }
    }

    protected override async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            SetError(_resource == "Clients" ? "Client display name is required." : "Land plot name is required.");
            return;
        }
        if (_resource == "LandPlots" && string.IsNullOrWhiteSpace(_address.Text))
        {
            SetError("A site address is required.");
            return;
        }

        object payload = _resource == "Clients"
            ? new
            {
                displayName = _name.Text.Trim(),
                email = Clean(_email.Text),
                phone = Clean(_phone.Text),
                billingAddress = Clean(_address.Text)
            }
            : new
            {
                name = _name.Text.Trim(),
                address = _address.Text.Trim(),
                areaSquareMetres = _area.Value,
                soilType = Clean(_soilType.Text),
                notes = Clean(_notes.Text)
            };

        if (!_recordId.HasValue)
        {
            await SubmitAsync("/" + _resource, payload);
            return;
        }

        SaveButton.Enabled = false;
        SetInfo("Saving changes through the API gateway…");
        try
        {
            await Api.PutAsync($"/{_resource}/{_recordId:D}", payload);
            SetInfo("Changes saved.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (DesktopApiException exception)
        {
            SetError(exception.Message);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            SetError(exception is TaskCanceledException
                ? "The request timed out. Refresh the record before retrying."
                : exception.Message);
        }
        finally
        {
            if (!IsDisposed) SaveButton.Enabled = true;
        }
    }

    private static bool TryReadDecimal(JsonElement source, string property, out decimal value)
    {
        value = 0m;
        return source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(property, out var raw) &&
            raw.ValueKind == JsonValueKind.Number && raw.TryGetDecimal(out value);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal sealed class CreateCatalogRecordDialog : OperationDialogBase
{
    private readonly string _resource;
    private readonly Guid? _recordId;
    private readonly TextBox _name = new() { MaxLength = 160 };
    private readonly TextBox _latinName = new() { MaxLength = 200 };
    private readonly TextBox _sku = new() { MaxLength = 64 };
    private readonly TextBox _description = new() { MaxLength = 4000 };
    private readonly ComboBox _unit = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _price = new() { Minimum = 0, Maximum = 1000000000, DecimalPlaces = 2, Increment = 1 };
    private readonly CheckBox _active = new() { Checked = true };

    public CreateCatalogRecordDialog(DesktopApiClient api, string resource, Guid? recordId = null)
        : base(api, (recordId.HasValue ? "Edit " : "New ") + ResourceLabel(resource),
            recordId.HasValue ? "Update catalog details while preserving the owning Catalog record." :
                "Create a catalog record with an active unit of measurement.", 650)
    {
        _resource = resource;
        _recordId = recordId;
        AddField("Name *", _name);
        if (resource.Equals("Plants", StringComparison.OrdinalIgnoreCase))
            AddField("Latin name", _latinName);
        if (resource.Equals("Materials", StringComparison.OrdinalIgnoreCase))
            AddField("SKU *", _sku);
        AddField("Description", _description, height: 92);
        AddField("Measurement unit *", _unit);
        AddField(resource.Equals("Work types", StringComparison.OrdinalIgnoreCase) ? "Base price" : "Unit price", _price);
        AddCheckBox("Active and available for new estimates", _active);
        SaveButton.Values.Text = recordId.HasValue ? "Save changes  →" : "Create record  →";
        SaveButton.Enabled = false;
        Shown += async (_, _) => await LoadUnitsAsync();
    }

    private static string ResourceLabel(string resource) => resource switch
    {
        "Materials" => "material",
        "Work types" => "work type",
        _ => "plant"
    };

    private string Endpoint => _resource switch
    {
        "Materials" => "Catalog/Materials",
        "Work types" => "Catalog/WorkTypes",
        _ => "Catalog/Plants"
    };

    private async Task LoadUnitsAsync()
    {
        SetInfo("Loading measurement units…");
        try
        {
            var response = await Api.GetAsync("Catalog/MeasurementUnits?page=1&pageSize=200");
            var items = CreateEstimateDialog.GetItems(response);
            if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (!CreateEstimateDialog.ReadBoolean(item, "isActive")) continue;
                    if (!Guid.TryParse(CreateEstimateDialog.ReadString(item, "id"), out var id) || id == Guid.Empty) continue;
                    var name = CreateEstimateDialog.ReadString(item, "name") ?? "Unit";
                    var symbol = CreateEstimateDialog.ReadString(item, "symbol") ?? string.Empty;
                    _unit.Items.Add(new UnitOption(id, name, symbol));
                }
            }
            if (_unit.Items.Count == 0)
            {
                SetError("No active units found. Create or activate a unit in Catalog before adding this item.");
                return;
            }

            _unit.SelectedIndex = 0;
            if (_recordId is Guid recordId)
                await LoadRecordAsync(recordId);
            else
            {
                SaveButton.Enabled = true;
                SetInfo($"Loaded {_unit.Items.Count} active measurement unit(s).");
            }
        }
        catch (Exception exception) when (exception is DesktopApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            SetError("Measurement units could not be loaded: " + exception.Message);
        }
    }

    private async Task LoadRecordAsync(Guid id)
    {
        SaveButton.Enabled = false;
        SetInfo("Loading catalog record…");
        try
        {
            var item = await Api.GetAsync($"/{Endpoint}/{id:D}");
            _name.Text = CreateEstimateDialog.ReadString(item, "name") ?? string.Empty;
            _latinName.Text = CreateEstimateDialog.ReadString(item, "latinName") ?? string.Empty;
            _sku.Text = CreateEstimateDialog.ReadString(item, "sku") ?? string.Empty;
            _description.Text = CreateEstimateDialog.ReadString(item, "description") ?? string.Empty;
            _active.Checked = CreateEstimateDialog.ReadBoolean(item, "isActive");
            var priceText = CreateEstimateDialog.ReadString(item,
                _resource.Equals("Work types", StringComparison.OrdinalIgnoreCase) ? "basePrice" : "unitPrice");
            if (decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
                _price.Value = Math.Clamp(price, _price.Minimum, _price.Maximum);

            if (Guid.TryParse(CreateEstimateDialog.ReadString(item, "measurementUnitId"), out var unitId))
                _unit.SelectedItem = _unit.Items.OfType<UnitOption>().FirstOrDefault(unit => unit.Id == unitId);
            if (_unit.SelectedItem is null)
                SetInfo("The previous unit is inactive or unavailable. Select an active replacement unit.");
            else
                SetInfo("Catalog record loaded.");
            SaveButton.Enabled = true;
        }
        catch (Exception exception) when (exception is DesktopApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            SetError("Catalog record could not be loaded: " + exception.Message);
        }
    }

    protected override async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_name.Text) || _unit.SelectedItem is not UnitOption unit)
        {
            SetError("Enter a name and select an active measurement unit.");
            return;
        }
        if (_resource.Equals("Materials", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(_sku.Text))
        {
            SetError("A material SKU is required.");
            return;
        }

        if (_resource.Equals("Materials", StringComparison.OrdinalIgnoreCase))
        {
            await SaveCatalogPayloadAsync(new
            {
                name = _name.Text.Trim(), sku = _sku.Text.Trim().ToUpperInvariant(),
                description = _description.Text.Trim(), measurementUnitId = unit.Id,
                unitPrice = _price.Value, isActive = _active.Checked
            });
        }
        else if (_resource.Equals("Work types", StringComparison.OrdinalIgnoreCase))
        {
            await SaveCatalogPayloadAsync(new
            {
                name = _name.Text.Trim(), description = _description.Text.Trim(),
                measurementUnitId = unit.Id, basePrice = _price.Value, isActive = _active.Checked
            });
        }
        else
        {
            await SaveCatalogPayloadAsync(new
            {
                name = _name.Text.Trim(), latinName = Clean(_latinName.Text),
                description = _description.Text.Trim(), measurementUnitId = unit.Id,
                unitPrice = _price.Value, isActive = _active.Checked
            });
        }
    }

    private async Task SaveCatalogPayloadAsync(object payload)
    {
        if (_recordId is not Guid id)
        {
            await SubmitAsync(Endpoint, payload);
            return;
        }

        SaveButton.Enabled = false;
        SetInfo("Saving catalog changes…");
        try
        {
            await Api.PutAsync($"/{Endpoint}/{id:D}", payload);
            SetInfo("Catalog record updated.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (DesktopApiException exception) { SetError(exception.Message); }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            SetError(exception is TaskCanceledException ? "The update timed out. Refresh the catalog record before retrying." : exception.Message);
        }
        finally
        {
            if (!IsDisposed) SaveButton.Enabled = true;
        }
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record UnitOption(Guid Id, string Name, string Symbol)
    {
        public override string ToString() => string.IsNullOrWhiteSpace(Symbol) ? Name : $"{Name} ({Symbol})";
    }
}

internal sealed class CreateSupplierDialog : OperationDialogBase
{
    private readonly Guid? _recordId;
    private readonly TextBox _code = new() { MaxLength = 32 };
    private readonly TextBox _name = new() { MaxLength = 200 };
    private readonly TextBox _contact = new() { MaxLength = 120 };
    private readonly TextBox _email = new() { MaxLength = 254 };
    private readonly TextBox _phone = new() { MaxLength = 40 };
    private readonly TextBox _taxId = new() { MaxLength = 80 };
    private readonly TextBox _address = new() { MaxLength = 500 };
    private readonly NumericUpDown _paymentDays = new() { Minimum = 0, Maximum = 365, Value = 30 };
    private readonly TextBox _currency = new() { MaxLength = 3, Text = "BYN" };
    private readonly CheckBox _active = new() { Checked = true };

    public CreateSupplierDialog(DesktopApiClient api, Guid? recordId = null)
        : base(api, recordId.HasValue ? "Edit supplier" : "New supplier",
            "Supplier contact information, purchasing terms and availability.", 790)
    {
        _recordId = recordId;
        AddField("Supplier code *", _code);
        AddField("Supplier name *", _name);
        AddField("Primary contact *", _contact);
        AddField("Email", _email);
        AddField("Phone", _phone);
        AddField("Tax identifier", _taxId);
        AddField("Address *", _address, height: 82);
        AddField("Payment terms (days)", _paymentDays);
        AddField("Currency code *", _currency);
        if (recordId.HasValue)
            AddCheckBox("Active and available for new purchase orders", _active);
        SaveButton.Values.Text = recordId.HasValue ? "Save changes  →" : "Create supplier  →";
        if (recordId.HasValue)
        {
            SaveButton.Enabled = false;
            Shown += async (_, _) => await LoadRecordAsync(recordId.Value);
        }
    }

    private async Task LoadRecordAsync(Guid id)
    {
        SetInfo("Loading supplier…");
        try
        {
            var supplier = await Api.GetAsync($"/Suppliers/{id:D}");
            _code.Text = CreateEstimateDialog.ReadString(supplier, "code") ?? string.Empty;
            _name.Text = CreateEstimateDialog.ReadString(supplier, "name") ?? string.Empty;
            _contact.Text = CreateEstimateDialog.ReadString(supplier, "contactName") ?? string.Empty;
            _email.Text = CreateEstimateDialog.ReadString(supplier, "email") ?? string.Empty;
            _phone.Text = CreateEstimateDialog.ReadString(supplier, "phone") ?? string.Empty;
            _taxId.Text = CreateEstimateDialog.ReadString(supplier, "taxIdentifier") ?? string.Empty;
            _address.Text = CreateEstimateDialog.ReadString(supplier, "address") ?? string.Empty;
            if (int.TryParse(CreateEstimateDialog.ReadString(supplier, "paymentTermDays"), out var days))
                _paymentDays.Value = Math.Clamp(days, (int)_paymentDays.Minimum, (int)_paymentDays.Maximum);
            _currency.Text = CreateEstimateDialog.ReadString(supplier, "currencyCode") ?? "BYN";
            _active.Checked = CreateEstimateDialog.ReadBoolean(supplier, "isActive");
            SetInfo("Supplier loaded.");
            SaveButton.Enabled = true;
        }
        catch (Exception exception) when (exception is DesktopApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            SetError("Supplier could not be loaded: " + exception.Message);
        }
    }

    protected override async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_code.Text) || string.IsNullOrWhiteSpace(_name.Text) ||
            string.IsNullOrWhiteSpace(_contact.Text) || string.IsNullOrWhiteSpace(_address.Text))
        {
            SetError("Supplier code, name, contact person and address are required.");
            return;
        }
        var currency = _currency.Text.Trim().ToUpperInvariant();
        if (currency.Length != 3 || !currency.All(char.IsLetter))
        {
            SetError("Enter a three-letter currency code.");
            return;
        }
        if (!string.IsNullOrWhiteSpace(_email.Text) &&
            !System.Net.Mail.MailAddress.TryCreate(_email.Text.Trim(), out _))
        {
            SetError("Enter a valid email address.");
            return;
        }

        var payload = new
        {
            code = _code.Text.Trim().ToUpperInvariant(),
            name = _name.Text.Trim(),
            contactName = _contact.Text.Trim(),
            email = Clean(_email.Text),
            phone = Clean(_phone.Text),
            taxIdentifier = Clean(_taxId.Text),
            address = _address.Text.Trim(),
            paymentTermDays = (int)_paymentDays.Value,
            currencyCode = currency
        };

        if (_recordId is not Guid id)
        {
            await SubmitAsync("Suppliers", payload);
            return;
        }

        SaveButton.Enabled = false;
        SetInfo("Saving supplier changes…");
        try
        {
            await Api.PutAsync($"/Suppliers/{id:D}", new
            {
                payload.code, payload.name, payload.contactName, payload.email, payload.phone,
                payload.taxIdentifier, payload.address, payload.paymentTermDays, payload.currencyCode,
                isActive = _active.Checked
            });
            SetInfo("Supplier updated.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (DesktopApiException exception) { SetError(exception.Message); }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            SetError(exception is TaskCanceledException ? "The update timed out. Refresh the supplier before retrying." : exception.Message);
        }
        finally
        {
            if (!IsDisposed) SaveButton.Enabled = true;
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal sealed class CreateStockItemDialog : OperationDialogBase
{
    private readonly Guid? _recordId;
    private readonly TextBox _sku = new() { MaxLength = 48 };
    private readonly TextBox _description = new() { MaxLength = 200 };
    private readonly TextBox _unit = new() { MaxLength = 24 };
    private readonly TextBox _location = new() { MaxLength = 100 };
    private readonly TextBox _catalogId = new() { MaxLength = 36, ReadOnly = true };
    private readonly NumericUpDown _reorder = new() { Minimum = 0, Maximum = 1000000000, DecimalPlaces = 3, Increment = 1 };

    public CreateStockItemDialog(DesktopApiClient api, Guid? recordId = null)
        : base(api, recordId.HasValue ? "Edit stock item" : "New stock item",
            recordId.HasValue ? "Update stock metadata without changing stock quantity or its Catalog link." :
                "Create the inventory item before recording its first movement.", 640)
    {
        _recordId = recordId;
        _catalogId.ReadOnly = recordId.HasValue;
        AddField("Stock-keeping unit (SKU) *", _sku);
        AddField("Description *", _description);
        AddField("Unit symbol *", _unit);
        AddField("Storage location *", _location);
        AddField(recordId.HasValue ? "Catalog item link (read-only)" : "Catalog item ID (optional)", _catalogId);
        AddField("Reorder level", _reorder);
        SaveButton.Values.Text = recordId.HasValue ? "Save changes  →" : "Create stock item  →";
        Shown += async (_, _) =>
        {
            if (_recordId.HasValue) await LoadRecordAsync();
        };
    }

    private async Task LoadRecordAsync()
    {
        SaveButton.Enabled = false;
        SetInfo("Loading stock item…");
        try
        {
            var item = await Api.GetAsync($"/StockItems/{_recordId!.Value:D}");
            _sku.Text = CreateEstimateDialog.ReadString(item, "sku") ?? string.Empty;
            _description.Text = CreateEstimateDialog.ReadString(item, "description") ?? string.Empty;
            _unit.Text = CreateEstimateDialog.ReadString(item, "unitSymbol") ?? string.Empty;
            _location.Text = CreateEstimateDialog.ReadString(item, "storageLocation") ?? string.Empty;
            _catalogId.Text = CreateEstimateDialog.ReadString(item, "catalogItemId") ?? string.Empty;
            if (decimal.TryParse(CreateEstimateDialog.ReadString(item, "reorderLevel"),
                    NumberStyles.Number, CultureInfo.InvariantCulture, out var reorder))
                _reorder.Value = Math.Clamp(reorder, _reorder.Minimum, _reorder.Maximum);
            SetInfo("Stock item loaded. Quantity on hand is changed only through stock movements.");
        }
        catch (Exception exception) when (exception is DesktopApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            SetError("Stock item could not be loaded: " + exception.Message);
            return;
        }
        finally
        {
            if (!IsDisposed) SaveButton.Enabled = true;
        }
    }

    protected override async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_sku.Text) || string.IsNullOrWhiteSpace(_description.Text) ||
            string.IsNullOrWhiteSpace(_unit.Text) || string.IsNullOrWhiteSpace(_location.Text))
        {
            SetError("SKU, description, unit symbol and storage location are required.");
            return;
        }

        var payload = new
        {
            sku = _sku.Text.Trim().ToUpperInvariant(),
            description = _description.Text.Trim(),
            unitSymbol = _unit.Text.Trim(),
            storageLocation = _location.Text.Trim(),
            reorderLevel = _reorder.Value
        };

        if (_recordId is Guid recordId)
        {
            SaveButton.Enabled = false;
            SetInfo("Saving stock metadata…");
            try
            {
                await Api.PutAsync($"/StockItems/{recordId:D}", payload);
                SetInfo("Stock item updated. On-hand quantity was preserved.");
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (DesktopApiException exception)
            {
                SetError(exception.Message);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                SetError(exception is TaskCanceledException
                    ? "The update timed out. Refresh the stock item before retrying."
                    : exception.Message);
            }
            finally
            {
                if (!IsDisposed) SaveButton.Enabled = true;
            }
            return;
        }

        Guid? catalogId = null;
        if (!string.IsNullOrWhiteSpace(_catalogId.Text))
        {
            if (!Guid.TryParse(_catalogId.Text.Trim(), out var parsed) || parsed == Guid.Empty)
            {
                SetError("Catalog item ID must be a valid GUID or left blank.");
                return;
            }
            catalogId = parsed;
        }

        await SubmitAsync("StockItems", new
        {
            catalogItemId = catalogId,
            payload.sku,
            payload.description,
            payload.unitSymbol,
            payload.storageLocation,
            payload.reorderLevel
        });
    }
}

internal sealed class CreatePurchaseOrderDialog : OperationDialogBase
{
    private readonly ComboBox _supplier = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _orderNumber = new() { MaxLength = 48, Text = "PO-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) };
    private readonly TextBox _projectId = new() { MaxLength = 36 };
    private readonly DateTimePicker _orderDate = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd MMM yyyy", Value = DateTime.Today };
    private readonly DateTimePicker _expectedDate = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd MMM yyyy", ShowCheckBox = true, Checked = false };
    private readonly ComboBox _itemType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _catalogId = new() { MaxLength = 36 };
    private readonly TextBox _description = new() { MaxLength = 240 };
    private readonly TextBox _unit = new() { MaxLength = 24, Text = "pcs" };
    private readonly NumericUpDown _quantity = new() { Minimum = 0.001m, Maximum = 1000000000, DecimalPlaces = 3, Value = 1, Increment = 1 };
    private readonly NumericUpDown _unitPrice = new() { Minimum = 0, Maximum = 1000000000, DecimalPlaces = 2 };
    private readonly TextBox _notes = new() { MaxLength = 4000 };

    public CreatePurchaseOrderDialog(DesktopApiClient api)
        : base(api, "New purchase order", "Create a supplier order with its first line item.", 850)
    {
        AddField("Supplier *", _supplier);
        AddField("Order number *", _orderNumber);
        AddField("Project ID (optional)", _projectId);
        AddField("Order date", _orderDate);
        AddField("Expected delivery date (optional)", _expectedDate);
        _itemType.Items.AddRange(new object[]
        {
            new ItemTypeOption(0, "Material"),
            new ItemTypeOption(1, "Plant"),
            new ItemTypeOption(2, "Equipment"),
            new ItemTypeOption(3, "Service"),
            new ItemTypeOption(4, "Other")
        });
        _itemType.SelectedIndex = 0;
        AddField("Line item type", _itemType);
        AddField("Catalog item ID (optional; required for inventory receipt)", _catalogId);
        AddField("Description *", _description);
        AddField("Unit *", _unit);
        AddField("Quantity *", _quantity);
        AddField("Unit price", _unitPrice);
        AddField("Notes", _notes);
        SaveButton.Enabled = false;
        Shown += async (_, _) => await LoadSuppliersAsync();
    }

    private async Task LoadSuppliersAsync()
    {
        SetInfo("Loading active suppliers…");
        try
        {
            var response = await Api.GetAsync("Suppliers?page=1&pageSize=200");
            var items = CreateEstimateDialog.GetItems(response);
            if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (!CreateEstimateDialog.ReadBoolean(item, "isActive")) continue;
                    if (!Guid.TryParse(CreateEstimateDialog.ReadString(item, "id"), out var id) || id == Guid.Empty) continue;
                    var name = CreateEstimateDialog.ReadString(item, "name") ?? "Supplier";
                    var code = CreateEstimateDialog.ReadString(item, "code") ?? string.Empty;
                    _supplier.Items.Add(new SupplierOption(id, string.IsNullOrWhiteSpace(code) ? name : $"{code} — {name}"));
                }
            }
            if (_supplier.Items.Count == 0)
            {
                SetError("No active suppliers found. Create a supplier before creating a purchase order.");
                return;
            }
            _supplier.SelectedIndex = 0;
            SaveButton.Enabled = true;
            SetInfo($"Loaded {_supplier.Items.Count} active supplier(s).");
        }
        catch (Exception exception) when (exception is DesktopApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            SetError("Suppliers could not be loaded: " + exception.Message);
        }
    }

    protected override async Task SaveAsync()
    {
        if (_supplier.SelectedItem is not SupplierOption supplier)
        {
            SetError("Choose an active supplier.");
            return;
        }
        if (string.IsNullOrWhiteSpace(_orderNumber.Text) || string.IsNullOrWhiteSpace(_description.Text) ||
            string.IsNullOrWhiteSpace(_unit.Text) || _itemType.SelectedItem is not ItemTypeOption itemType)
        {
            SetError("Order number, line description, unit and item type are required.");
            return;
        }
        if (_expectedDate.Checked && _expectedDate.Value.Date < _orderDate.Value.Date)
        {
            SetError("Expected delivery date cannot be earlier than the order date.");
            return;
        }

        Guid? projectId = null;
        if (!string.IsNullOrWhiteSpace(_projectId.Text))
        {
            if (!Guid.TryParse(_projectId.Text.Trim(), out var parsed) || parsed == Guid.Empty)
            {
                SetError("Project ID must be a valid GUID or left blank.");
                return;
            }
            projectId = parsed;
        }

        Guid? catalogId = null;
        if (!string.IsNullOrWhiteSpace(_catalogId.Text))
        {
            if (!Guid.TryParse(_catalogId.Text.Trim(), out var parsed) || parsed == Guid.Empty)
            {
                SetError("Catalog item ID must be a valid GUID or left blank.");
                return;
            }
            catalogId = parsed;
        }

        var expectedDate = _expectedDate.Checked ? DateOnly.FromDateTime(_expectedDate.Value.Date) : (DateOnly?)null;
        await SubmitAsync("PurchaseOrders", new
        {
            orderNumber = _orderNumber.Text.Trim(),
            supplierId = supplier.Id,
            projectId,
            orderDate = DateOnly.FromDateTime(_orderDate.Value.Date),
            expectedDeliveryDate = expectedDate,
            notes = _notes.Text.Trim(),
            lines = new[]
            {
                new
                {
                    itemType = itemType.Value,
                    catalogItemId = catalogId,
                    description = _description.Text.Trim(),
                    unitSymbol = _unit.Text.Trim(),
                    quantity = _quantity.Value,
                    unitPrice = _unitPrice.Value
                }
            }
        });
    }

    private sealed record SupplierOption(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record ItemTypeOption(int Value, string Label)
    {
        public override string ToString() => Label;
    }
}


internal sealed class StockMovementDialog : OperationDialogBase
{
    private readonly Guid _stockItemId;
    private readonly ComboBox _movementType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _quantity = new()
    {
        Minimum = 0.001m, Maximum = 1_000_000_000m, DecimalPlaces = 3, Increment = 1m, Value = 1m
    };
    private readonly TextBox _sourceDocument = new() { MaxLength = 100 };
    private readonly TextBox _projectId = new() { MaxLength = 36 };
    private readonly TextBox _notes = new() { MaxLength = 2000, Multiline = true };

    public StockMovementDialog(DesktopApiClient api, Guid stockItemId, string itemLabel)
        : base(api, "Record stock movement", $"Inventory ledger entry for {itemLabel}.", 610)
    {
        if (stockItemId == Guid.Empty) throw new ArgumentException("A stock item id is required.", nameof(stockItemId));
        _stockItemId = stockItemId;

        _movementType.Items.AddRange(new object[]
        {
            new MovementOption(0, "Receipt — stock increases"),
            new MovementOption(1, "Consumption — stock decreases"),
            new MovementOption(2, "Return — stock increases"),
            new MovementOption(3, "Adjustment — apply signed correction as increase"),
            new MovementOption(4, "Transfer in — stock increases"),
            new MovementOption(5, "Transfer out — stock decreases")
        });
        _movementType.SelectedIndex = 0;

        AddField("Movement type *", _movementType);
        AddField("Quantity *", _quantity);
        AddField("Source document *", _sourceDocument);
        AddField("Project ID (optional)", _projectId);
        AddField("Notes", _notes, height: 90);
        SetInfo("Consumption and transfer-out quantities are deducted from on-hand stock.");
    }

    protected override async Task SaveAsync()
    {
        if (_movementType.SelectedItem is not MovementOption movement)
        {
            SetError("Choose a movement type.");
            return;
        }
        if (string.IsNullOrWhiteSpace(_sourceDocument.Text))
        {
            SetError("A source document or reference is required.");
            return;
        }

        Guid? projectId = null;
        if (!string.IsNullOrWhiteSpace(_projectId.Text))
        {
            if (!Guid.TryParse(_projectId.Text.Trim(), out var parsed) || parsed == Guid.Empty)
            {
                SetError("Project ID must be a valid GUID or left blank.");
                return;
            }
            projectId = parsed;
        }

        await SubmitAsync($"StockItems/{_stockItemId:D}/movements", new
        {
            movementType = movement.Value,
            quantity = _quantity.Value,
            sourceDocument = _sourceDocument.Text.Trim(),
            projectId,
            notes = _notes.Text.Trim()
        });
    }

    private sealed record MovementOption(int Value, string Label)
    {
        public override string ToString() => Label;
    }
}

internal sealed class PurchaseOrderManagementDialog : OperationDialogBase
{
    private readonly Guid _orderId;
    private readonly Label _orderIdentity = new()
    {
        AutoSize = true, ForeColor = Color.FromArgb(55, 73, 61),
        Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold)
    };
    private readonly Label _orderState = new()
    {
        AutoSize = true, ForeColor = Color.FromArgb(112, 127, 118),
        Font = new Font("Segoe UI", 9f)
    };
    private readonly ComboBox _lines = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _quantity = new()
    {
        Minimum = 0.001m, Maximum = 1_000_000_000m, DecimalPlaces = 3, Increment = 1m, Value = 1m
    };
    private readonly DateTimePicker _actionDate = new()
    {
        Format = DateTimePickerFormat.Custom, CustomFormat = "dd MMM yyyy", Value = DateTime.Today
    };
    private KryptonButton? _cancelOrder;
    private int _status = -1;

    public PurchaseOrderManagementDialog(DesktopApiClient api, Guid orderId)
        : base(api, "Purchase order workflow", "Submit, approve and receive goods against a live purchase order.", 700)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("A purchase order id is required.", nameof(orderId));
        _orderId = orderId;
        Fields.Controls.Add(_orderIdentity);
        Fields.Controls.Add(_orderState);
        AddField("Purchase-order line", _lines, height: 74);
        AddField("Receipt quantity", _quantity);
        AddField("Action date", _actionDate);
        _lines.SelectedIndexChanged += (_, _) => UpdateRemainingQuantity();

        _cancelOrder = new KryptonButton { Width = 150, Height = 38, ButtonStyle = ButtonStyle.LowProfile };
        _cancelOrder.Values.Text = "Cancel purchase order";
        _cancelOrder.StateCommon.Content.ShortText.Color1 = Color.FromArgb(153, 62, 56);
        _cancelOrder.Click += async (_, _) => await CancelOrderAsync();
        Fields.Controls.Add(_cancelOrder);

        SaveButton.Enabled = false;
        SaveButton.Values.Text = "Loading order…";
        Shown += async (_, _) => await LoadOrderAsync();
    }

    private async Task LoadOrderAsync()
    {
        SetInfo("Loading the latest purchase-order state…");
        try
        {
            var order = await Api.GetAsync($"PurchaseOrders/{_orderId:D}");
            _orderIdentity.Text = $"{ReadText(order, "orderNumber") ?? _orderId.ToString("D")}  ·  {ReadText(order, "supplierId") ?? "Supplier not available"}";
            _status = ReadStatus(order);
            var statusName = _status switch
            {
                0 => "Draft",
                1 => "Submitted",
                2 => "Approved",
                3 => "Partially received",
                4 => "Received",
                5 => "Cancelled",
                _ => "Unknown"
            };
            _orderState.Text = $"Current status: {statusName}  ·  Order value: {ReadDecimal(order, "orderTotal").ToString("N2", CultureInfo.InvariantCulture)}";

            if (order.TryGetProperty("lines", out var lineItems) && lineItems.ValueKind == JsonValueKind.Array)
            {
                foreach (var line in lineItems.EnumerateArray())
                {
                    if (!Guid.TryParse(ReadText(line, "id"), out var lineId) || lineId == Guid.Empty)
                        continue;

                    var ordered = ReadDecimal(line, "quantity");
                    var received = ReadDecimal(line, "receivedQuantity");
                    var remaining = decimal.Round(ordered - received, 3, MidpointRounding.AwayFromZero);
                    var description = ReadText(line, "description") ?? "Purchase-order line";
                    var unit = ReadText(line, "unitSymbol") ?? string.Empty;
                    _lines.Items.Add(new PurchaseOrderLineOption(
                        lineId, description, unit, ordered, received, remaining));
                }
            }

            if (_lines.Items.Count > 0)
            {
                _lines.SelectedIndex = 0;
                UpdateRemainingQuantity();
            }

            SaveButton.Enabled = true;
            if (_cancelOrder is not null)
                _cancelOrder.Enabled = _status is 0 or 1 or 2;
            SaveButton.Values.Text = _status switch
            {
                0 => "Submit order  →",
                1 => "Approve order  →",
                2 or 3 => "Receive line  →",
                _ => "No action available"
            };

            if (_status is 2 or 3 && _lines.Items.Count == 0)
            {
                SaveButton.Enabled = false;
                SetInfo("This order has no receivable lines.");
            }
            else if (_status is 0)
                SetInfo("Submit this draft when its lines and supplier details are correct.");
            else if (_status is 1)
                SetInfo("Approval records the date and allows line receipts.");
            else if (_status is 2 or 3)
                SetInfo("Choose a line with remaining quantity, then record the quantity received.");
            else
            {
                SaveButton.Enabled = false;
                SetInfo(statusName is "Received" ? "All ordered quantities have been received." : "This purchase order is not open for further actions.");
            }
        }
        catch (Exception exception) when (exception is DesktopApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            SaveButton.Enabled = false;
            if (_cancelOrder is not null) _cancelOrder.Enabled = false;
            SetError("Purchase order could not be loaded: " + exception.Message);
        }
    }

    private void UpdateRemainingQuantity()
    {
        if (_lines.SelectedItem is not PurchaseOrderLineOption line)
            return;

        var remaining = line.Remaining;
        if (remaining <= 0)
        {
            _quantity.Enabled = false;
            SaveButton.Enabled = false;
            SetInfo("This line is fully received.");
            return;
        }

        _quantity.Enabled = true;
        _quantity.Maximum = Math.Max(_quantity.Minimum, remaining);
        _quantity.Value = Math.Min(_quantity.Maximum, Math.Max(_quantity.Minimum, Math.Min(1m, remaining)));
        if (_status is 2 or 3)
            SaveButton.Enabled = true;
    }

    protected override async Task SaveAsync()
    {
        switch (_status)
        {
            case 0:
                await SubmitAsync($"PurchaseOrders/{_orderId:D}/submit", new { });
                break;
            case 1:
                await SubmitAsync($"PurchaseOrders/{_orderId:D}/approve", new
                {
                    approvalDate = DateOnly.FromDateTime(_actionDate.Value.Date)
                });
                break;
            case 2:
            case 3:
                if (_lines.SelectedItem is not PurchaseOrderLineOption line)
                {
                    SetError("Choose a purchase-order line.");
                    return;
                }
                if (line.Remaining <= 0 || _quantity.Value > line.Remaining)
                {
                    SetError("Receipt quantity must not exceed the remaining ordered quantity.");
                    return;
                }
                await SubmitAsync($"PurchaseOrders/{_orderId:D}/receive-lines/{line.Id:D}", new
                {
                    quantity = _quantity.Value,
                    receivedDate = DateOnly.FromDateTime(_actionDate.Value.Date)
                });
                break;
            default:
                SetError("This purchase order has no available state transition.");
                break;
        }
    }

    private async Task CancelOrderAsync()
    {
        if (_status is not (0 or 1 or 2))
            return;

        var confirmed = MessageBox.Show(this,
            "Cancel this purchase order? This action cannot be undone.",
            "Confirm cancellation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirmed != DialogResult.Yes)
            return;

        await SubmitAsync($"PurchaseOrders/{_orderId:D}/cancel", new { });
    }

    private static int ReadStatus(JsonElement source)
    {
        if (!source.TryGetProperty("status", out var value) &&
            !source.TryGetProperty("Status", out value))
            return -1;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
            return numeric;
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            return text switch
            {
                "Draft" => 0,
                "Submitted" => 1,
                "Approved" => 2,
                "PartiallyReceived" => 3,
                "Received" => 4,
                "Cancelled" => 5,
                _ => -1
            };
        }
        return -1;
    }

    private static decimal ReadDecimal(JsonElement source, string name)
    {
        if (!source.TryGetProperty(name, out var value) &&
            !source.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value))
            return 0m;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            return number;
        return value.ValueKind == JsonValueKind.String &&
               decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number)
            ? number
            : 0m;
    }

    private static string? ReadText(JsonElement source, string name) =>
        CreateEstimateDialog.ReadString(source, name);

    private sealed record PurchaseOrderLineOption(
        Guid Id, string Description, string Unit, decimal Ordered, decimal Received, decimal Remaining)
    {
        public override string ToString() =>
            $"{Description} · {Received:N3}/{Ordered:N3} {Unit} received";
    }
}
