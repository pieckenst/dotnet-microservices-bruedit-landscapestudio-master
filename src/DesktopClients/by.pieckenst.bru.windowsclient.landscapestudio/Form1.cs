using System.Data;
using System.Text.Json;
using Krypton.Toolkit;

namespace by.pieckenst.bru.windowsclient.landscapestudio;

public partial class Form1 : KryptonForm
{
    private static readonly Color Canvas = Color.FromArgb(246, 248, 246);
    private static readonly Color Sidebar = Color.FromArgb(24, 43, 36);
    private static readonly Color Accent = Color.FromArgb(66, 125, 91);
    private static readonly Color Ink = Color.FromArgb(33, 47, 40);
    private static readonly Color Muted = Color.FromArgb(112, 127, 118);
    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = Canvas, Padding = new Padding(28, 22, 28, 24) };
    private readonly Label _connectionState = new()
    {
        AutoSize = true, Text = "API NOT CONNECTED", ForeColor = Color.FromArgb(160, 105, 28),
        BackColor = Color.FromArgb(255, 244, 220), Padding = new Padding(10, 6, 10, 6),
        Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold)
    };
    private readonly FlowLayoutPanel _navigation = new()
    {
        Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
        AutoScroll = true, Padding = new Padding(12, 18, 12, 12), BackColor = Sidebar
    };
    private readonly Dictionary<string, KryptonButton> _navButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Label> _metricValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly KryptonButton _connectButton = new() { Width = 136, Height = 38, ButtonStyle = ButtonStyle.Standalone };
    private KryptonButton? _editProjectButton;
    private KryptonButton? _editReferenceButton;
    private KryptonButton? _archiveReferenceButton;
    private DataGridView? _currentGrid;
    private DesktopApiClient? _api;
    private CancellationTokenSource? _loadCancellation;
    private int _loadGeneration;
    private string _activePage = "Overview";
    private string _catalogResource = "Plants";
    private Guid? _selectedEstimateProjectId;

    public Form1()
    {
        InitializeComponent();
        BuildApplicationShell();
        Shown += async (_, _) => await ShowPageAsync("Overview");
        FormClosed += (_, _) =>
        {
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _api?.Dispose();
        };
    }

    private void BuildApplicationShell()
    {
        Text = "Landscape Studio";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1080, 720);
        ClientSize = new Size(1440, 900);
        BackColor = Canvas;
        Font = new Font("Segoe UI", 10f);
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;

        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Canvas, Margin = Padding.Empty };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 246));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(shell);
        shell.Controls.Add(BuildSidebar(), 0, 0);

        var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Canvas, Margin = Padding.Empty };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.Controls.Add(main, 1, 0);

        var topbar = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(28, 0, 28, 0) };
        var topbarLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.White };
        topbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        var breadcrumb = new Label
        {
            Text = "WORKSPACE   /   LANDSCAPE STUDIO", AutoSize = true, ForeColor = Muted,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold), Anchor = AnchorStyles.Left
        };
        _connectionState.Anchor = AnchorStyles.Right;
        topbarLayout.Controls.Add(breadcrumb, 0, 0);
        topbarLayout.Controls.Add(_connectionState, 1, 0);
        ConfigurePrimaryButton(_connectButton, "Connect to API");
        _connectButton.Anchor = AnchorStyles.Right;
        _connectButton.Click += async (_, _) => await OpenConnectionDialogAsync();
        topbarLayout.Controls.Add(_connectButton, 2, 0);
        topbar.Controls.Add(topbarLayout);
        main.Controls.Add(topbar, 0, 0);
        main.Controls.Add(_content, 0, 1);
    }

    private Control BuildSidebar()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Sidebar, Margin = Padding.Empty };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));

        var brand = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22, 20, 12, 10), BackColor = Sidebar };
        brand.Controls.Add(new Label { Text = "LANDSCAPE", AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold), Location = new Point(22, 19) });
        brand.Controls.Add(new Label { Text = "STUDIO  /  MANAGEMENT", AutoSize = true, ForeColor = Color.FromArgb(160, 187, 171), Font = new Font("Segoe UI", 8f), Location = new Point(23, 52) });
        root.Controls.Add(brand, 0, 0);

        var groups = new[]
        {
            ("WORKSPACE", new[] { "Overview", "Projects", "Estimates", "Customers", "Land plots" }),
            ("OPERATIONS", new[] { "Plant & material catalog", "Procurement", "Inventory", "Suppliers" }),
            ("ADMINISTRATION", new[] { "Organization", "Settings" })
        };
        foreach (var group in groups)
        {
            _navigation.Controls.Add(new Label
            {
                Text = group.Item1, AutoSize = true, ForeColor = Color.FromArgb(133, 160, 144),
                Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold), Margin = new Padding(10, 13, 0, 7)
            });
            foreach (var item in group.Item2)
            {
                var button = CreateNavButton(item);
                _navButtons[item] = button;
                _navigation.Controls.Add(button);
            }
        }
        root.Controls.Add(_navigation, 0, 1);

        var footer = new Panel { Dock = DockStyle.Fill, BackColor = Sidebar, Padding = new Padding(18, 12, 18, 12) };
        footer.Controls.Add(new Label { Text = "TPPO PROJECT", AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold), Location = new Point(18, 12) });
        footer.Controls.Add(new Label { Text = "Desktop client · .NET 10", AutoSize = true, ForeColor = Color.FromArgb(160, 187, 171), Font = new Font("Segoe UI", 8.5f), Location = new Point(18, 34) });
        root.Controls.Add(footer, 0, 2);
        return root;
    }

    private KryptonButton CreateNavButton(string text)
    {
        var button = new KryptonButton
        {
            Width = 210, Height = 42, Margin = new Padding(0, 2, 0, 2),
            ButtonStyle = ButtonStyle.LowProfile, Cursor = Cursors.Hand, Tag = text, TabStop = false
        };
        button.Values.Text = "   " + text;
        button.StateCommon.Back.Color1 = Sidebar;
        button.StateCommon.Back.Color2 = Sidebar;
        button.StateCommon.Border.DrawBorders = PaletteDrawBorders.None;
        button.StateCommon.Content.ShortText.Color1 = Color.FromArgb(218, 230, 222);
        button.StateCommon.Content.ShortText.Font = new Font("Segoe UI", 9.5f);
        button.StateCommon.Content.ShortText.TextH = PaletteRelativeAlign.Near;
        button.StateCommon.Content.ShortText.TextV = PaletteRelativeAlign.Center;
        button.StateTracking.Back.Color1 = Color.FromArgb(40, 65, 53);
        button.StateTracking.Back.Color2 = Color.FromArgb(40, 65, 53);
        button.StatePressed.Back.Color1 = Color.FromArgb(52, 83, 66);
        button.StatePressed.Back.Color2 = Color.FromArgb(52, 83, 66);
        button.Click += async (_, _) => await ShowPageAsync(text);
        return button;
    }

    private async Task ShowPageAsync(string page)
    {
        _activePage = page;
        _currentGrid = null;
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        var cancellationToken = _loadCancellation.Token;
        var generation = ++_loadGeneration;

        foreach (var pair in _navButtons)
        {
            var selected = pair.Key.Equals(page, StringComparison.OrdinalIgnoreCase);
            pair.Value.StateCommon.Back.Color1 = selected ? Color.FromArgb(218, 232, 220) : Sidebar;
            pair.Value.StateCommon.Back.Color2 = selected ? Color.FromArgb(218, 232, 220) : Sidebar;
            pair.Value.StateCommon.Content.ShortText.Color1 = selected ? Ink : Color.FromArgb(218, 230, 222);
            pair.Value.StateTracking.Back.Color1 = selected ? Color.FromArgb(218, 232, 220) : Color.FromArgb(40, 65, 53);
            pair.Value.StateTracking.Back.Color2 = selected ? Color.FromArgb(218, 232, 220) : Color.FromArgb(40, 65, 53);
        }

        _content.SuspendLayout();
        _content.Controls.Clear();
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Canvas, Margin = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _editProjectButton = null;
        _editReferenceButton = null;
        _archiveReferenceButton = null;
        var header = BuildPageHeader(page);
        layout.Controls.Add(header, 0, 0);
        var body = new Panel { Dock = DockStyle.Fill, BackColor = Canvas, Padding = Padding.Empty };
        layout.Controls.Add(body, 0, 1);
        _content.Controls.Add(layout);
        _content.ResumeLayout();

        if (page.Equals("Settings", StringComparison.OrdinalIgnoreCase))
        {
            ShowSettings(body);
            return;
        }
        if (_api is null || !_api.IsAuthenticated)
        {
            ShowDisconnectedState(body);
            return;
        }

        ShowLoadingState(body);
        await LoadPageAsync(page, body, generation, cancellationToken);
    }

    private Control BuildPageHeader(string page)
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Canvas, Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var text = new Panel { Dock = DockStyle.Fill, BackColor = Canvas };
        var title = new Label { Text = page, AutoSize = true, ForeColor = Ink, Font = new Font("Segoe UI Semibold", 22f, FontStyle.Bold), Location = new Point(0, 0) };
        var subtitle = new Label
        {
            Text = PageSubtitle(page), AutoSize = true, ForeColor = Muted,
            Font = new Font("Segoe UI", 9.5f), Location = new Point(2, 43)
        };
        text.Controls.Add(title);
        text.Controls.Add(subtitle);
        header.Controls.Add(text, 0, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(0, 15, 0, 0)
        };
        if (!page.Equals("Settings", StringComparison.OrdinalIgnoreCase))
        {
            var refresh = CreateSecondaryButton("Refresh");
            refresh.Click += async (_, _) => await ShowPageAsync(_activePage);
            actions.Controls.Add(refresh);
            if (_api is not null && page.Equals("Projects", StringComparison.OrdinalIgnoreCase))
            {
                var create = CreatePrimaryButton("＋ New project");
                create.Margin = new Padding(8, 0, 0, 0);
                create.Click += async (_, _) => await CreateProjectAsync();
                actions.Controls.Add(create);
            }
            if (_api is not null && page.Equals("Projects", StringComparison.OrdinalIgnoreCase))
            {
                var edit = CreateSecondaryButton("Edit selected");
                edit.Margin = new Padding(8, 0, 0, 0);
                edit.Enabled = false;
                edit.Click += async (_, _) => await EditProjectAsync();
                _editProjectButton = edit;
                actions.Controls.Add(edit);
            }
            if (_api is not null &&
                (page.Equals("Customers", StringComparison.OrdinalIgnoreCase) ||
                 page.Equals("Land plots", StringComparison.OrdinalIgnoreCase)))
            {
                var createLabel = page.Equals("Customers", StringComparison.OrdinalIgnoreCase)
                    ? "＋ New client" : "＋ New land plot";
                var createReference = CreatePrimaryButton(createLabel);
                createReference.Margin = new Padding(8, 0, 0, 0);
                createReference.Click += async (_, _) => await CreateReferenceRecordAsync();
                actions.Controls.Add(createReference);

                var editReference = CreateSecondaryButton("Edit selected");
                editReference.Margin = new Padding(8, 0, 0, 0);
                editReference.Enabled = false;
                editReference.Click += async (_, _) => await EditReferenceRecordAsync();
                _editReferenceButton = editReference;
                actions.Controls.Add(editReference);

                var archiveReference = CreateSecondaryButton("Archive selected");
                archiveReference.Margin = new Padding(8, 0, 0, 0);
                archiveReference.Enabled = false;
                archiveReference.Click += async (_, _) => await ArchiveReferenceRecordAsync();
                _archiveReferenceButton = archiveReference;
                actions.Controls.Add(archiveReference);
            }
            if (_api is not null && page.Equals("Estimates", StringComparison.OrdinalIgnoreCase))
            {
                var create = CreatePrimaryButton("＋ New estimate");
                create.Margin = new Padding(8, 0, 0, 0);
                create.Click += async (_, _) => await CreateEstimateAsync();
                actions.Controls.Add(create);
            }
            if (_api is not null && page.Equals("Plant & material catalog", StringComparison.OrdinalIgnoreCase))
            {
                var label = _catalogResource switch
                {
                    "Materials" => "＋ New material",
                    "Work types" => "＋ New work type",
                    _ => "＋ New plant"
                };
                var create = CreatePrimaryButton(label);
                create.Margin = new Padding(8, 0, 0, 0);
                create.Click += async (_, _) => await CreateCatalogRecordAsync();
                actions.Controls.Add(create);
            }
            if (_api is not null && page.Equals("Suppliers", StringComparison.OrdinalIgnoreCase))
            {
                var create = CreatePrimaryButton("＋ New supplier");
                create.Margin = new Padding(8, 0, 0, 0);
                create.Click += async (_, _) => await CreateSupplierAsync();
                actions.Controls.Add(create);
            }
            if (_api is not null && page.Equals("Inventory", StringComparison.OrdinalIgnoreCase))
            {
                var create = CreatePrimaryButton("＋ New stock item");
                create.Margin = new Padding(8, 0, 0, 0);
                create.Click += async (_, _) => await CreateStockItemAsync();
                actions.Controls.Add(create);
            }
            if (_api is not null && page.Equals("Inventory", StringComparison.OrdinalIgnoreCase))
            {
                var movement = CreateSecondaryButton("Record movement…");
                movement.Margin = new Padding(8, 0, 0, 0);
                movement.Click += async (_, _) => await RecordStockMovementAsync();
                actions.Controls.Add(movement);
            }
            if (_api is not null && page.Equals("Procurement", StringComparison.OrdinalIgnoreCase))
            {
                var create = CreatePrimaryButton("＋ New purchase order");
                create.Margin = new Padding(8, 0, 0, 0);
                create.Click += async (_, _) => await CreatePurchaseOrderAsync();
                actions.Controls.Add(create);
            }
            if (_api is not null && page.Equals("Procurement", StringComparison.OrdinalIgnoreCase))
            {
                var manage = CreateSecondaryButton("Manage selected…");
                manage.Margin = new Padding(8, 0, 0, 0);
                manage.Click += async (_, _) => await ManagePurchaseOrderAsync();
                actions.Controls.Add(manage);
            }
        }
        header.Controls.Add(actions, 1, 0);
        return header;
    }

    private static string PageSubtitle(string page) => page switch
    {
        "Overview" => "Live activity and operational signals from your service workspace.",
        "Projects" => "Plan and manage landscaping projects using live Project API records.",
        "Estimates" => "Review estimates returned by the configured project service.",
        "Customers" => "Maintain client records owned by the Project service.",
        "Land plots" => "Maintain project sites and their address, area and soil details.",
        "Plant & material catalog" => "Browse the plant, material and work-type catalogs.",
        "Procurement" => "Purchase orders and supplier deliveries from Procurement.",
        "Inventory" => "Stock levels and inventory records from Procurement.",
        "Suppliers" => "Supplier records and contact channels from Procurement.",
        "Organization" => "Organization and workforce records from the connected service.",
        "Settings" => "Configure the gateway connection and authenticate with Identity.",
        _ => "Live service-backed data from Landscape Studio."
    };

    private async Task LoadPageAsync(string page, Panel body, int generation, CancellationToken cancellationToken)
    {
        try
        {
            if (page.Equals("Overview", StringComparison.OrdinalIgnoreCase))
                await LoadOverviewAsync(body, generation, cancellationToken);
            else
                await LoadTablePageAsync(page, body, generation, cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (DesktopApiException exception)
        {
            if (generation != _loadGeneration || body.IsDisposed) return;
            if (exception.IsUnauthorized) SetDisconnected("SESSION REJECTED");
            ShowErrorState(body, exception.Message, exception.IsUnauthorized ? "Reconnect and sign in with an authorized account." : "Refresh the page after checking the service and your permissions.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            if (generation != _loadGeneration || body.IsDisposed) return;
            ShowErrorState(body, exception is TaskCanceledException ? "The API request timed out." : exception.Message,
                "Check the API gateway and the service health page, then refresh.");
        }
    }

    private async Task LoadOverviewAsync(Panel body, int generation, CancellationToken cancellationToken)
    {
        var metrics = BuildMetricCards();
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Canvas, Margin = Padding.Empty };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 145));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.Controls.Add(metrics, 0, 0);

        var lower = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Canvas, Margin = Padding.Empty };
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        lower.Controls.Add(BuildSurfaceWithGrid("Recent projects", CreateGrid()), 0, 0);
        lower.Controls.Add(BuildActivitySurface(CreateGrid()), 1, 0);
        content.Controls.Add(lower, 0, 1);
        body.Controls.Clear();
        body.Controls.Add(content);

        var projects = await _api!.GetAsync("/Projects?page=1&pageSize=8", cancellationToken);
        if (generation != _loadGeneration || body.IsDisposed) return;
        var projectItems = GetItems(projects);
        var projectGrid = FindFirstGrid(lower.Controls[0]);
        if (projectGrid is not null) projectGrid.DataSource = ToDataTable(projectItems);
        _metricValues["PROJECTS"].Text = ReadCount(projects)?.ToString("N0") ?? projectItems.GetArrayLength().ToString("N0");

        try
        {
            var analytics = await _api.GetAsync("/Analytics/Summary", cancellationToken);
            if (generation != _loadGeneration || body.IsDisposed) return;
            _metricValues["AUDIT EVENTS"].Text = ReadLong(analytics, "totalEvents")?.ToString("N0") ?? "0";
            var services = GetProperty(analytics, "byService");
            var activityGrid = FindFirstGrid(lower.Controls[1]);
            if (activityGrid is not null) activityGrid.DataSource = ToDataTable(services);
            _metricValues["EVENT SOURCES"].Text = services.ValueKind == JsonValueKind.Array
                ? services.GetArrayLength().ToString("N0")
                : "0";
        }
        catch (Exception exception) when (exception is DesktopApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            _metricValues["AUDIT EVENTS"].Text = "—";
            _metricValues["EVENT SOURCES"].Text = "—";
            SetFeedback(lower.Controls[1], "Analytics is currently unavailable.");
        }

        try
        {
            var orders = await _api.GetAsync("/PurchaseOrders?page=1&pageSize=1", cancellationToken);
            if (generation == _loadGeneration)
                _metricValues["PURCHASE ORDERS"].Text = ReadCount(orders)?.ToString("N0") ?? "0";
        }
        catch (Exception exception) when (exception is DesktopApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            if (generation == _loadGeneration) _metricValues["PURCHASE ORDERS"].Text = "—";
        }

        if (generation == _loadGeneration && _api is not null)
            _connectionState.Text = "CONNECTED";
    }

    private async Task LoadTablePageAsync(string page, Panel body, int generation, CancellationToken cancellationToken)
    {
        var queryPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14) };
        var isCatalog = page.Equals("Plant & material catalog", StringComparison.OrdinalIgnoreCase);
        var isEstimates = page.Equals("Estimates", StringComparison.OrdinalIgnoreCase);
        var rowCount = isCatalog || isEstimates ? 2 : 1;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = rowCount, BackColor = Color.White };
        if (rowCount == 2)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var filterBar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true };
            if (isCatalog)
            {
                var plant = CreateSecondaryButton("Plants");
                var material = CreateSecondaryButton("Materials");
                var work = CreateSecondaryButton("Work types");
                plant.Click += async (_, _) => { _catalogResource = "Plants"; await ShowPageAsync(_activePage); };
                material.Click += async (_, _) => { _catalogResource = "Materials"; await ShowPageAsync(_activePage); };
                work.Click += async (_, _) => { _catalogResource = "Work types"; await ShowPageAsync(_activePage); };
                filterBar.Controls.Add(plant);
                filterBar.Controls.Add(material);
                filterBar.Controls.Add(work);
            }
            else if (isEstimates)
            {
                var projects = new ComboBox { Width = 360, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f), IntegralHeight = false };
                var load = CreateSecondaryButton("Load estimates");
                try
                {
                    var projectResponse = await _api!.GetAsync("/Projects?page=1&pageSize=200", cancellationToken);
                    var rows = CreateEstimateDialog.GetItems(projectResponse);
                    if (rows.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var row in rows.EnumerateArray())
                        {
                            if (CreateEstimateDialog.ReadBoolean(row, "isArchived")) continue;
                            if (!Guid.TryParse(CreateEstimateDialog.ReadString(row, "id"), out var projectId) || projectId == Guid.Empty) continue;
                            var number = CreateEstimateDialog.ReadString(row, "projectNumber") ?? string.Empty;
                            var name = CreateEstimateDialog.ReadString(row, "name") ?? "(unnamed project)";
                            projects.Items.Add(new EstimateProjectItem(projectId, string.IsNullOrWhiteSpace(number) ? name : $"{number} — {name}"));
                        }
                    }
                    if (_selectedEstimateProjectId.HasValue)
                    {
                        for (var i = 0; i < projects.Items.Count; i++)
                            if (projects.Items[i] is EstimateProjectItem item && item.Id == _selectedEstimateProjectId.Value)
                            {
                                projects.SelectedIndex = i;
                                break;
                            }
                    }
                    if (projects.SelectedIndex < 0 && projects.Items.Count > 0) projects.SelectedIndex = 0;
                    load.Click += async (_, _) =>
                    {
                        if (projects.SelectedItem is EstimateProjectItem item)
                        {
                            _selectedEstimateProjectId = item.Id;
                            await ShowPageAsync("Estimates");
                        }
                    };
                    filterBar.Controls.Add(new Label { Text = "Project", AutoSize = true, Margin = new Padding(0, 9, 0, 0), ForeColor = Muted });
                    filterBar.Controls.Add(projects);
                    filterBar.Controls.Add(load);
                }
                catch (Exception exception) when (exception is DesktopApiException or HttpRequestException or TaskCanceledException or JsonException)
                {
                    ShowErrorState(body, $"Projects could not be loaded: {exception.Message}", "Check the Project API and refresh the page.");
                    return;
                }
            }
            layout.Controls.Add(filterBar, 0, 0);
        }
        else
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        }

        var grid = CreateGrid();
        _currentGrid = grid;
        if (page.Equals("Projects", StringComparison.OrdinalIgnoreCase))
        {
            grid.SelectionChanged += (_, _) =>
            {
                if (_editProjectButton is not null && !_editProjectButton.IsDisposed)
                    _editProjectButton.Enabled = GetSelectedGridRecordId().HasValue;
            };
            grid.CellDoubleClick += async (_, args) =>
            {
                if (args.RowIndex >= 0)
                    await EditProjectAsync();
            };
        }
        if (page.Equals("Customers", StringComparison.OrdinalIgnoreCase) ||
            page.Equals("Land plots", StringComparison.OrdinalIgnoreCase))
        {
            grid.SelectionChanged += (_, _) =>
            {
                var selected = GetSelectedGridRecordId().HasValue;
                if (_editReferenceButton is not null && !_editReferenceButton.IsDisposed)
                    _editReferenceButton.Enabled = selected;
                if (_archiveReferenceButton is not null && !_archiveReferenceButton.IsDisposed)
                    _archiveReferenceButton.Enabled = selected;
            };
            grid.CellDoubleClick += async (_, args) =>
            {
                if (args.RowIndex >= 0)
                    await EditReferenceRecordAsync();
            };
        }
        if (rowCount == 2) layout.Controls.Add(grid, 0, 1); else layout.Controls.Add(grid, 0, 0);
        queryPanel.Controls.Add(layout);
        body.Controls.Clear();
        body.Controls.Add(queryPanel);

        var route = ResolveRoute(page);
        if (route is null)
        {
            if (isEstimates)
                SetFeedback(queryPanel, "Choose a project above, then load its estimates.");
            else
                ShowErrorState(body, "No gateway route is configured for this resource yet.", "Check the route definitions before enabling this workflow.");
            return;
        }
        var data = await _api!.GetAsync(route, cancellationToken);
        if (generation != _loadGeneration || body.IsDisposed) return;
        var items = GetItems(data);
        grid.DataSource = ToDataTable(items);
        if (items.ValueKind == JsonValueKind.Array && items.GetArrayLength() == 0)
            SetFeedback(queryPanel, "The API returned no records for this request.");
        _connectionState.Text = "CONNECTED";
    }

    private string? ResolveRoute(string page) => page switch
    {
        "Projects" => "/Projects?page=1&pageSize=100",
        "Estimates" => _selectedEstimateProjectId is Guid estimateProjectId
            ? $"/Estimates?projectId={estimateProjectId:D}&page=1&pageSize=100"
            : null,
        "Customers" => "/Clients?page=1&pageSize=100",
        "Land plots" => "/LandPlots?page=1&pageSize=100",
        "Plant & material catalog" => _catalogResource switch
        {
            "Materials" => "/Catalog/Materials?page=1&pageSize=100",
            "Work types" => "/Catalog/WorkTypes?page=1&pageSize=100",
            _ => "/Catalog/Plants?page=1&pageSize=100"
        },
        "Procurement" => "/PurchaseOrders?page=1&pageSize=100",
        "Inventory" => "/StockItems?page=1&pageSize=100",
        "Suppliers" => "/Suppliers?page=1&pageSize=100",
        "Organization" => "/Employees?page=1&pageSize=100",
        _ => null
    };

    private async Task CreateProjectAsync()
    {
        if (_api is null) return;
        using var dialog = new CreateProjectDialog(_api);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await ShowPageAsync("Projects");
    }

    private async Task EditProjectAsync()
    {
        if (_api is null) return;
        var projectId = GetSelectedGridRecordId();
        if (!projectId.HasValue)
        {
            MessageBox.Show(this, "Select a project in the table first.",
                "No project selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new CreateProjectDialog(_api, projectId.Value);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await ShowPageAsync("Projects");
    }

    private async Task CreateReferenceRecordAsync()
    {
        if (_api is null) return;
        var resource = _activePage.Equals("Customers", StringComparison.OrdinalIgnoreCase) ? "Clients" : "LandPlots";
        using var dialog = new CreateReferenceRecordDialog(_api, resource);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await ShowPageAsync(_activePage);
    }

    private async Task EditReferenceRecordAsync()
    {
        if (_api is null) return;
        var id = GetSelectedGridRecordId();
        if (!id.HasValue)
        {
            MessageBox.Show(this, "Select a record in the table first.",
                "No record selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var resource = _activePage.Equals("Customers", StringComparison.OrdinalIgnoreCase) ? "Clients" : "LandPlots";
        using var dialog = new CreateReferenceRecordDialog(_api, resource, id.Value);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await ShowPageAsync(_activePage);
    }

    private async Task ArchiveReferenceRecordAsync()
    {
        if (_api is null) return;
        var id = GetSelectedGridRecordId();
        if (!id.HasValue) return;

        var resource = _activePage.Equals("Customers", StringComparison.OrdinalIgnoreCase) ? "Clients" : "LandPlots";
        var label = GetSelectedGridValue(resource == "Clients" ? "displayName" : "name") ?? id.Value.ToString("D");
        if (MessageBox.Show(this, $"Archive {label}? Existing project history will retain its reference.",
                "Archive record", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            var path = resource == "Clients"
                ? $"/Clients/{id.Value:D}/archive"
                : $"/LandPlots/{id.Value:D}/archive?archived=true";
            await _api.PostAsync(path, new { });
            await ShowPageAsync(_activePage);
        }
        catch (Exception exception) when (exception is DesktopApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            MessageBox.Show(this, exception.Message, "Archive failed",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task CreateEstimateAsync()
    {
        if (_api is null) return;
        using var createDialog = new CreateEstimateDialog(_api);
        if (createDialog.ShowDialog(this) != DialogResult.OK || createDialog.CreatedEstimateId == Guid.Empty) return;
        using var details = new EstimateDetailsDialog(_api, createDialog.CreatedEstimateId);
        details.ShowDialog(this);
        await ShowPageAsync("Estimates");
    }

    private async Task CreateCatalogRecordAsync()
    {
        if (_api is null) return;
        using var dialog = new CreateCatalogRecordDialog(_api, _catalogResource);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await ShowPageAsync("Plant & material catalog");
    }

    private async Task CreateSupplierAsync()
    {
        if (_api is null) return;
        using var dialog = new CreateSupplierDialog(_api);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await ShowPageAsync("Suppliers");
    }

    private async Task CreateStockItemAsync()
    {
        if (_api is null) return;
        using var dialog = new CreateStockItemDialog(_api);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await ShowPageAsync("Inventory");
    }

    private async Task CreatePurchaseOrderAsync()
    {
        if (_api is null) return;
        using var dialog = new CreatePurchaseOrderDialog(_api);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await ShowPageAsync("Procurement");
    }

    private async Task RecordStockMovementAsync()
    {
        if (_api is null) return;
        var stockItemId = GetSelectedGridRecordId();
        if (stockItemId is null)
        {
            MessageBox.Show(this, "Select a stock item in the inventory table first.",
                "No stock item selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var itemLabel = GetSelectedGridValue("sku") ?? stockItemId.Value.ToString("D");
        using var dialog = new StockMovementDialog(_api, stockItemId.Value, itemLabel);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await ShowPageAsync("Inventory");
    }

    private async Task ManagePurchaseOrderAsync()
    {
        if (_api is null) return;
        var orderId = GetSelectedGridRecordId();
        if (orderId is null)
        {
            MessageBox.Show(this, "Select a purchase order in the table first.",
                "No purchase order selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new PurchaseOrderManagementDialog(_api, orderId.Value);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await ShowPageAsync("Procurement");
    }

    private Guid? GetSelectedGridRecordId()
    {
        var row = _currentGrid?.SelectedRows.Count > 0
            ? _currentGrid.SelectedRows[0]
            : _currentGrid?.CurrentRow;
        if (row is null || row.IsNewRow) return null;

        foreach (DataGridViewCell cell in row.Cells)
        {
            var propertyName = cell.OwningColumn?.DataPropertyName;
            if (string.IsNullOrWhiteSpace(propertyName))
                propertyName = cell.OwningColumn?.Name;
            if (string.Equals(propertyName, "id", StringComparison.OrdinalIgnoreCase) &&
                Guid.TryParse(Convert.ToString(cell.Value), out var id) && id != Guid.Empty)
                return id;
        }
        return null;
    }

    private string? GetSelectedGridValue(string columnName)
    {
        var row = _currentGrid?.SelectedRows.Count > 0
            ? _currentGrid.SelectedRows[0]
            : _currentGrid?.CurrentRow;
        if (row is null || row.IsNewRow) return null;

        foreach (DataGridViewCell cell in row.Cells)
        {
            var propertyName = cell.OwningColumn?.DataPropertyName;
            if (string.IsNullOrWhiteSpace(propertyName))
                propertyName = cell.OwningColumn?.Name;
            if (string.Equals(propertyName, columnName, StringComparison.OrdinalIgnoreCase))
                return Convert.ToString(cell.Value);
        }
        return null;
    }

    private async Task OpenConnectionDialogAsync()
    {
        using var dialog = new ConnectionDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.ConnectedClient is null) return;
        _api?.Dispose();
        _api = dialog.ConnectedClient;
        SetConnected();
        await ShowPageAsync(_activePage == "Settings" ? "Overview" : _activePage);
    }

    private void ShowSettings(Panel body)
    {
        var surface = new Panel { Dock = DockStyle.Top, Height = 250, BackColor = Color.White, Padding = new Padding(24) };
        surface.Paint += (_, e) => { using var p = new Pen(Color.FromArgb(229, 235, 230)); e.Graphics.DrawRectangle(p, 0, 0, surface.Width - 1, surface.Height - 1); };
        surface.Controls.Add(new Label { Text = "API gateway and authentication", AutoSize = true, Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold), ForeColor = Ink, Location = new Point(24, 22) });
        surface.Controls.Add(new Label
        {
            Text = "Gateway URL: " + ClientSettingsStore.LoadGatewayUri().AbsoluteUri + Environment.NewLine +
                "Access tokens are held in memory only; only the gateway URL is written to local settings." +
                Environment.NewLine + Environment.NewLine +
                (_api?.IsAuthenticated == true ? $"Signed in as {_api.UserName} ({_api.Role})." : "No authenticated API session is active."),
            AutoSize = false, Size = new Size(760, 76), Location = new Point(26, 62), ForeColor = Muted, Font = new Font("Segoe UI", 10f)
        });
        var action = CreatePrimaryButton(_api?.IsAuthenticated == true ? "Change connection" : "Connect and sign in");
        action.Location = new Point(24, 164);
        action.Click += async (_, _) => await OpenConnectionDialogAsync();
        surface.Controls.Add(action);
        body.Controls.Clear();
        body.Controls.Add(surface);
    }

    private void ShowDisconnectedState(Panel body)
    {
        var surface = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(28) };
        surface.Paint += (_, e) => { using var p = new Pen(Color.FromArgb(229, 235, 230)); e.Graphics.DrawRectangle(p, 0, 0, surface.Width - 1, surface.Height - 1); };
        var icon = new Label { Text = "↗", AutoSize = true, ForeColor = Accent, Font = new Font("Segoe UI", 29f), Location = new Point(28, 23) };
        var title = new Label { Text = "Connect your Landscape Studio workspace", AutoSize = true, ForeColor = Ink, Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold), Location = new Point(31, 78) };
        var description = new Label
        {
            Text = "Sign in through the Identity service to load real projects, catalog records, procurement data and analytics. The desktop client does not store your password or persist access tokens.",
            AutoSize = false, Size = new Size(650, 52), ForeColor = Muted, Font = new Font("Segoe UI", 10f), Location = new Point(33, 114)
        };
        var button = CreatePrimaryButton("Connect to API");
        button.Location = new Point(31, 185);
        button.Click += async (_, _) => await OpenConnectionDialogAsync();
        surface.Controls.Add(icon);
        surface.Controls.Add(title);
        surface.Controls.Add(description);
        surface.Controls.Add(button);
        body.Controls.Clear();
        body.Controls.Add(surface);
    }

    private void ShowLoadingState(Panel body)
    {
        var loading = new Label { Text = "Loading live data…", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Muted, Font = new Font("Segoe UI", 11f) };
        body.Controls.Clear();
        body.Controls.Add(loading);
    }

    private void ShowErrorState(Panel body, string message, string hint)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(26) };
        panel.Controls.Add(new Label { Text = "The service request could not be completed.", AutoSize = true, ForeColor = Color.FromArgb(143, 65, 52), Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold), Location = new Point(24, 24) });
        panel.Controls.Add(new Label { Text = message, AutoSize = false, Size = new Size(820, 52), ForeColor = Muted, Font = new Font("Segoe UI", 10f), Location = new Point(26, 64) });
        panel.Controls.Add(new Label { Text = hint, AutoSize = true, ForeColor = Muted, Font = new Font("Segoe UI", 9f), Location = new Point(26, 122) });
        var refresh = CreateSecondaryButton("Try again");
        refresh.Location = new Point(24, 158);
        refresh.Click += async (_, _) => await ShowPageAsync(_activePage);
        panel.Controls.Add(refresh);
        body.Controls.Clear();
        body.Controls.Add(panel);
    }

    private Control BuildMetricCards()
    {
        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Canvas, Padding = new Padding(0, 3, 0, 12) };
        var entries = new[]
        {
            ("PROJECTS", "PROJECT RECORDS"),
            ("AUDIT EVENTS", "AUDIT EVENTS"),
            ("PURCHASE ORDERS", "PURCHASE ORDERS"),
            ("EVENT SOURCES", "EVENT SOURCES")
        };
        for (var i = 0; i < entries.Length; i++)
        {
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0, 0, 12, 0), Padding = new Padding(18) };
            card.Paint += (_, e) => { using var pen = new Pen(Color.FromArgb(229, 235, 230)); e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1); };
            card.Controls.Add(new Label { Text = entries[i].Item2, AutoSize = true, ForeColor = Muted, Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold), Location = new Point(18, 18) });
            var value = new Label { Text = "—", AutoSize = true, ForeColor = Ink, Font = new Font("Segoe UI Semibold", 27f, FontStyle.Bold), Location = new Point(16, 42) };
            _metricValues[entries[i].Item1] = value;
            card.Controls.Add(value);
            card.Controls.Add(new Label { Text = "Live API data", AutoSize = true, ForeColor = Muted, Font = new Font("Segoe UI", 8f), Location = new Point(18, 91) });
            cards.Controls.Add(card, i, 0);
        }
        return cards;
    }

    private static Control BuildSurfaceWithGrid(string titleText, DataGridView grid)
    {
        var surface = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(15), Margin = new Padding(0, 0, 10, 0) };
        surface.Paint += (_, e) => { using var p = new Pen(Color.FromArgb(229, 235, 230)); e.Graphics.DrawRectangle(p, 0, 0, surface.Width - 1, surface.Height - 1); };
        surface.Controls.Add(grid);
        var title = new Label { Text = titleText, AutoSize = true, ForeColor = Ink, Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold), Location = new Point(16, 15) };
        var container = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.White };
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        container.Controls.Add(title, 0, 0);
        grid.Dock = DockStyle.Fill;
        container.Controls.Add(grid, 0, 1);
        surface.Controls.Clear();
        surface.Controls.Add(container);
        return surface;
    }

    private static Control BuildActivitySurface(DataGridView grid)
    {
        var surface = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(15), Margin = Padding.Empty };
        surface.Paint += (_, e) => { using var p = new Pen(Color.FromArgb(229, 235, 230)); e.Graphics.DrawRectangle(p, 0, 0, surface.Width - 1, surface.Height - 1); };
        var container = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.White };
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        container.Controls.Add(new Label { Text = "Activity by service", AutoSize = true, ForeColor = Ink, Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold) }, 0, 0);
        container.Controls.Add(grid, 0, 1);
        surface.Controls.Add(container);
        return surface;
    }

    private static DataGridView CreateGrid()
    {
        return new DataGridView
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
    }

    private static DataGridView? FindFirstGrid(Control control)
    {
        if (control is DataGridView grid) return grid;
        foreach (Control child in control.Controls)
        {
            var found = FindFirstGrid(child);
            if (found is not null) return found;
        }
        return null;
    }

    private static DataTable ToDataTable(JsonElement items)
    {
        var table = new DataTable();
        if (items.ValueKind != JsonValueKind.Array) return table;
        var rows = items.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).ToArray();
        var columns = rows.SelectMany(row => row.EnumerateObject())
            .Where(property => property.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null or JsonValueKind.Undefined))
            .Select(property => property.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var column in columns) table.Columns.Add(column, typeof(string));
        foreach (var row in rows)
        {
            var data = table.NewRow();
            foreach (var property in row.EnumerateObject())
            {
                if (!table.Columns.Contains(property.Name)) continue;
                data[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.ToString();
            }
            table.Rows.Add(data);
        }
        return table;
    }

    private static JsonElement GetItems(JsonElement response)
    {
        if (response.ValueKind == JsonValueKind.Object)
        {
            if (TryGetProperty(response, "items", out var items)) return items;
            if (TryGetProperty(response, "data", out var data) && data.ValueKind == JsonValueKind.Array) return data;
        }
        return response;
    }

    private static JsonElement GetProperty(JsonElement source, string key) =>
        TryGetProperty(source, key, out var value) ? value : default;

    private static bool TryGetProperty(JsonElement source, string name, out JsonElement value)
    {
        if (source.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in source.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    private static long? ReadLong(JsonElement source, string key)
    {
        var value = GetProperty(source, key);
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
        return null;
    }

    private static long? ReadCount(JsonElement source) => ReadLong(source, "totalCount") ?? ReadLong(source, "TotalCount");

    private static void SetFeedback(Control control, string message)
    {
        control.Controls.Add(new Label
        {
            Name = "api-feedback", Text = message, AutoSize = true, ForeColor = Muted,
            Font = new Font("Segoe UI", 9f), Location = new Point(22, Math.Max(50, control.Height - 45))
        });
    }

    private void SetConnected()
    {
        _connectionState.Text = "CONNECTED";
        _connectionState.ForeColor = Color.FromArgb(48, 105, 64);
        _connectionState.BackColor = Color.FromArgb(229, 244, 228);
        _connectButton.Values.Text = "Change connection";
    }

    private void SetDisconnected(string status)
    {
        _connectionState.Text = status;
        _connectionState.ForeColor = Color.FromArgb(160, 105, 28);
        _connectionState.BackColor = Color.FromArgb(255, 244, 220);
        _connectButton.Values.Text = "Connect to API";
    }

    private static KryptonButton CreatePrimaryButton(string label)
    {
        var button = new KryptonButton { Height = 40, ButtonStyle = ButtonStyle.Standalone, Cursor = Cursors.Hand };
        button.Values.Text = label;
        ConfigurePrimaryButton(button, label);
        return button;
    }

    private static KryptonButton CreateSecondaryButton(string label)
    {
        var button = new KryptonButton { Height = 36, ButtonStyle = ButtonStyle.Standalone, Cursor = Cursors.Hand };
        button.Values.Text = label;
        button.StateCommon.Back.Color1 = Color.White;
        button.StateCommon.Back.Color2 = Color.White;
        button.StateCommon.Border.Color1 = Color.FromArgb(217, 227, 216);
        button.StateCommon.Content.ShortText.Color1 = Ink;
        button.StateCommon.Content.ShortText.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
        return button;
    }

    private sealed record EstimateProjectItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }

    private static void ConfigurePrimaryButton(KryptonButton button, string label)
    {
        button.Values.Text = label;
        button.StateCommon.Back.Color1 = Accent;
        button.StateCommon.Back.Color2 = Accent;
        button.StateCommon.Border.DrawBorders = PaletteDrawBorders.None;
        button.StateCommon.Content.ShortText.Color1 = Color.White;
        button.StateCommon.Content.ShortText.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
        button.StateCommon.Content.ShortText.TextH = PaletteRelativeAlign.Center;
        button.StateCommon.Content.ShortText.TextV = PaletteRelativeAlign.Center;
        button.StateTracking.Back.Color1 = Color.FromArgb(52, 104, 73);
        button.StateTracking.Back.Color2 = Color.FromArgb(52, 104, 73);
        button.StatePressed.Back.Color1 = Color.FromArgb(42, 88, 61);
        button.StatePressed.Back.Color2 = Color.FromArgb(42, 88, 61);
    }
}
