using System.Drawing.Drawing2D;

namespace by.pieckenst.bru.windowsclient.landscapestudio;

public partial class Form1 : Form
{
    private static readonly Color Canvas = Color.FromArgb(246, 248, 246);
    private static readonly Color Sidebar = Color.FromArgb(24, 43, 36);
    private static readonly Color Accent = Color.FromArgb(66, 125, 91);
    private static readonly Color Ink = Color.FromArgb(33, 47, 40);
    private static readonly Color Muted = Color.FromArgb(112, 127, 118);
    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = Canvas, Padding = new Padding(28, 22, 28, 24) };
    private readonly Label _pageTitle = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 22f, FontStyle.Bold), ForeColor = Ink };
    private readonly Label _pageSubtitle = new() { AutoSize = true, Font = new Font("Segoe UI", 10f), ForeColor = Muted };
    private readonly Label _connectionState = new() { AutoSize = true, Text = "API NOT CONNECTED", ForeColor = Color.FromArgb(160, 105, 28), BackColor = Color.FromArgb(255, 244, 220), Padding = new Padding(10, 6, 10, 6), Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold) };
    private readonly FlowLayoutPanel _navigation = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(12, 18, 12, 12), BackColor = Sidebar };
    private string _activePage = "Overview";

    public Form1()
    {
        InitializeComponent();
        BuildApplicationShell();
        ShowPage("Overview");
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

        var sidebar = BuildSidebar();
        shell.Controls.Add(sidebar, 0, 0);

        var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Canvas, Margin = Padding.Empty };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.Controls.Add(main, 1, 0);

        var topbar = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(28, 0, 28, 0) };
        var topbarLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.White };
        topbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var breadcrumb = new Label { Text = "WORKSPACE   /   LANDSCAPE STUDIO", AutoSize = true, ForeColor = Muted, Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold), Anchor = AnchorStyles.Left };
        topbarLayout.Controls.Add(breadcrumb, 0, 0);
        _connectionState.Anchor = AnchorStyles.Right;
        topbarLayout.Controls.Add(_connectionState, 1, 0);
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
        var brandTitle = new Label { Text = "LANDSCAPE", AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold), Location = new Point(22, 19) };
        var brandSub = new Label { Text = "STUDIO  /  MANAGEMENT", AutoSize = true, ForeColor = Color.FromArgb(160, 187, 171), Font = new Font("Segoe UI", 8f), Location = new Point(23, 52) };
        brand.Controls.Add(brandTitle);
        brand.Controls.Add(brandSub);
        root.Controls.Add(brand, 0, 0);

        var groups = new[]
        {
            ("WORKSPACE", new[] { "Overview", "Projects", "Estimates", "Customers" }),
            ("OPERATIONS", new[] { "Plant & material catalog", "Procurement", "Inventory", "Suppliers" }),
            ("ADMINISTRATION", new[] { "Organization", "Settings" })
        };
        foreach (var group in groups)
        {
            var heading = new Label { Text = group.Item1, AutoSize = true, ForeColor = Color.FromArgb(133, 160, 144), Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold), Margin = new Padding(10, 13, 0, 7) };
            _navigation.Controls.Add(heading);
            foreach (var item in group.Item2)
            {
                var button = CreateNavButton(item);
                _navigation.Controls.Add(button);
            }
        }
        root.Controls.Add(_navigation, 0, 1);

        var footer = new Panel { Dock = DockStyle.Fill, BackColor = Sidebar, Padding = new Padding(18, 12, 18, 12) };
        footer.Controls.Add(new Label { Text = "TPPO PROJECT", AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold), Location = new Point(18, 12) });
        footer.Controls.Add(new Label { Text = "Desktop client · .NET", AutoSize = true, ForeColor = Color.FromArgb(160, 187, 171), Font = new Font("Segoe UI", 8.5f), Location = new Point(18, 34) });
        root.Controls.Add(footer, 0, 2);
        return root;
    }

    private Button CreateNavButton(string text)
    {
        var button = new Button
        {
            Text = "   " + text,
            Width = 210,
            Height = 42,
            Margin = new Padding(0, 2, 0, 2),
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(218, 230, 222),
            BackColor = Sidebar,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Tag = text
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 65, 53);
        button.Click += (_, _) => ShowPage(text);
        return button;
    }

    private void ShowPage(string page)
    {
        _activePage = page;
        _pageTitle.Text = page;
        _pageSubtitle.Text = page == "Overview"
            ? "Your studio at a glance. Live figures will appear when the API is configured."
            : $"Manage {page.ToLowerInvariant()} through the Landscape Studio service layer.";
        _content.SuspendLayout();
        _content.Controls.Clear();

        var pageLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Canvas, Margin = Padding.Empty };
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, page == "Overview" ? 154 : 0));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = new Panel { Dock = DockStyle.Fill, BackColor = Canvas };
        _pageTitle.Location = new Point(0, 0);
        _pageSubtitle.Location = new Point(2, 42);
        heading.Controls.Add(_pageTitle);
        heading.Controls.Add(_pageSubtitle);
        pageLayout.Controls.Add(heading, 0, 0);

        if (page == "Overview")
            pageLayout.Controls.Add(BuildMetricCards(), 0, 1);

        pageLayout.Controls.Add(BuildMainPanel(page), 0, 2);
        _content.Controls.Add(pageLayout);
        _content.ResumeLayout();
    }

    private Control BuildMetricCards()
    {
        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Canvas, Padding = new Padding(0, 8, 0, 12) };
        for (var i = 0; i < 4; i++)
        {
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0, 0, 12, 0), Padding = new Padding(18) };
            card.Paint += (_, e) =>
            {
                using var pen = new Pen(Color.FromArgb(229, 235, 230));
                e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };
            var title = new Label { Text = new[] { "ACTIVE PROJECTS", "OPEN ESTIMATES", "PENDING ORDERS", "LOW STOCK ITEMS" }[i], AutoSize = true, ForeColor = Muted, Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold), Location = new Point(18, 18) };
            var value = new Label { Text = "—", AutoSize = true, ForeColor = Ink, Font = new Font("Segoe UI Semibold", 27f, FontStyle.Bold), Location = new Point(16, 42) };
            var note = new Label { Text = "Waiting for API connection", AutoSize = true, ForeColor = Muted, Font = new Font("Segoe UI", 8f), Location = new Point(18, 91) };
            card.Controls.Add(title);
            card.Controls.Add(value);
            card.Controls.Add(note);
            cards.Controls.Add(card, i, 0);
        }
        return cards;
    }

    private Control BuildMainPanel(string page)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(22) };
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(229, 235, 230));
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };
        var title = new Label { Text = page == "Overview" ? "Workspace status" : $"{page} workspace", AutoSize = true, ForeColor = Ink, Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold), Location = new Point(22, 20) };
        var body = new Label
        {
            Text = page == "Overview"
                ? "The interface shell is ready. Connect the gateway and configure authentication to load live projects, estimates, suppliers and inventory."
                : "This area is reserved for the service-backed workflow. It will use the shared API client and typed response models; no sample records are presented as real business data.",
            AutoSize = false,
            Location = new Point(24, 61),
            Size = new Size(760, 54),
            ForeColor = Muted,
            Font = new Font("Segoe UI", 10f)
        };
        var action = new Button
        {
            Text = page == "Overview" ? "Connection settings" : "Configure service connection",
            Location = new Point(22, 130),
            Size = new Size(220, 40),
            FlatStyle = FlatStyle.Flat,
            BackColor = Accent,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        action.FlatAppearance.BorderSize = 0;
        action.Click += (_, _) => MessageBox.Show(
            "The gateway URL and authentication flow will be configured when the shared API client is implemented.",
            "Connection settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        panel.Controls.Add(title);
        panel.Controls.Add(body);
        panel.Controls.Add(action);
        return panel;
    }
}
