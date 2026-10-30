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

internal sealed class CreateCatalogRecordDialog : OperationDialogBase
{
    private readonly string _resource;
    private readonly TextBox _name = new() { MaxLength = 160 };
    private readonly TextBox _latinName = new() { MaxLength = 200 };
    private readonly TextBox _sku = new() { MaxLength = 64 };
    private readonly TextBox _description = new() { MaxLength = 4000 };
    private readonly ComboBox _unit = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _price = new() { Minimum = 0, Maximum = 1000000000, DecimalPlaces = 2, Increment = 1 };
    private readonly CheckBox _active = new() { Checked = true };

    public CreateCatalogRecordDialog(DesktopApiClient api, string resource)
        : base(api, "New " + ResourceLabel(resource), "Create a catalog record with an active unit of measurement.", 650)
    {
        _resource = resource;
        AddField("Name *", _name);
        if (resource.Equals("Plants", StringComparison.OrdinalIgnoreCase))
            AddField("Latin name", _latinName);
        if (resource.Equals("Materials", StringComparison.OrdinalIgnoreCase))
            AddField("SKU *", _sku);
        AddField("Description", _description, height: 92);
        AddField("Measurement unit *", _unit);
        AddField(resource.Equals("Work types", StringComparison.OrdinalIgnoreCase) ? "Base price" : "Unit price", _price);
        AddCheckBox("Active and available for new estimates", _active);
        SaveButton.Enabled = false;
        Shown += async (_, _) => await LoadUnitsAsync();
    }

    private static string ResourceLabel(string resource) => resource switch
    {
        "Materials" => "material",
        "Work types" => "work type",
        _ => "plant"
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
            if (_unit.Items.Count > 0)
            {
                _unit.SelectedIndex = 0;
                SaveButton.Enabled = true;
                SetInfo($"Loaded {_unit.Items.Count} active measurement unit(s).");
            }
            else
            {
                SetError("No active units found. Create a unit in Catalog before adding this item.");
            }
        }
        catch (Exception exception) when (exception is DesktopApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            SetError("Measurement units could not be loaded: " + exception.Message);
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

        object payload = _resource switch
        {
            "Materials" => new
            {
                name = _name.Text.Trim(), sku = _sku.Text.Trim().ToUpperInvariant(),
                description = _description.Text.Trim(), measurementUnitId = unit.Id,
                unitPrice = _price.Value, isActive = _active.Checked
            },
            "Work types" => new
            {
                name = _name.Text.Trim(), description = _description.Text.Trim(),
                measurementUnitId = unit.Id, basePrice = _price.Value, isActive = _active.Checked
            },
            _ => new
            {
                name = _name.Text.Trim(), latinName = Clean(_latinName.Text),
                description = _description.Text.Trim(), measurementUnitId = unit.Id,
                unitPrice = _price.Value, isActive = _active.Checked
            }
        };
        var endpoint = _resource switch
        {
            "Materials" => "Catalog/Materials",
            "Work types" => "Catalog/WorkTypes",
            _ => "Catalog/Plants"
        };
        await SubmitAsync(endpoint, payload);
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
    private readonly TextBox _code = new() { MaxLength = 32 };
    private readonly TextBox _name = new() { MaxLength = 200 };
    private readonly TextBox _contact = new() { MaxLength = 120 };
    private readonly TextBox _email = new() { MaxLength = 254 };
    private readonly TextBox _phone = new() { MaxLength = 40 };
    private readonly TextBox _taxId = new() { MaxLength = 80 };
    private readonly TextBox _address = new() { MaxLength = 500 };
    private readonly NumericUpDown _paymentDays = new() { Minimum = 0, Maximum = 365, Value = 30 };
    private readonly TextBox _currency = new() { MaxLength = 3, Text = "BYN" };

    public CreateSupplierDialog(DesktopApiClient api)
        : base(api, "New supplier", "Supplier contact information and purchasing terms.", 760)
    {
        AddField("Supplier code *", _code);
        AddField("Supplier name *", _name);
        AddField("Primary contact *", _contact);
        AddField("Email", _email);
        AddField("Phone", _phone);
        AddField("Tax identifier", _taxId);
        AddField("Address *", _address, height: 82);
        AddField("Payment terms (days)", _paymentDays);
        AddField("Currency code *", _currency);
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

        await SubmitAsync("Suppliers", new
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
        });
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal sealed class CreateStockItemDialog : OperationDialogBase
{
    private readonly TextBox _sku = new() { MaxLength = 48 };
    private readonly TextBox _description = new() { MaxLength = 200 };
    private readonly TextBox _unit = new() { MaxLength = 24 };
    private readonly TextBox _location = new() { MaxLength = 100 };
    private readonly TextBox _catalogId = new() { MaxLength = 36 };
    private readonly NumericUpDown _reorder = new() { Minimum = 0, Maximum = 1000000000, DecimalPlaces = 3, Increment = 1 };

    public CreateStockItemDialog(DesktopApiClient api)
        : base(api, "New stock item", "Create the inventory item before recording its first movement.", 640)
    {
        AddField("Stock-keeping unit (SKU) *", _sku);
        AddField("Description *", _description);
        AddField("Unit symbol *", _unit);
        AddField("Storage location *", _location);
        AddField("Catalog item ID (optional)", _catalogId);
        AddField("Reorder level", _reorder);
    }

    protected override async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_sku.Text) || string.IsNullOrWhiteSpace(_description.Text) ||
            string.IsNullOrWhiteSpace(_unit.Text) || string.IsNullOrWhiteSpace(_location.Text))
        {
            SetError("SKU, description, unit symbol and storage location are required.");
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
            sku = _sku.Text.Trim().ToUpperInvariant(),
            description = _description.Text.Trim(),
            unitSymbol = _unit.Text.Trim(),
            storageLocation = _location.Text.Trim(),
            reorderLevel = _reorder.Value
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
