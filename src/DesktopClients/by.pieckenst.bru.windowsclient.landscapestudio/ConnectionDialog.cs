using Krypton.Toolkit;

namespace by.pieckenst.bru.windowsclient.landscapestudio;

internal sealed class ConnectionDialog : KryptonForm
{
    private readonly TextBox _gateway = new() { Dock = DockStyle.Fill };
    private readonly TextBox _userName = new() { Dock = DockStyle.Fill };
    private readonly TextBox _password = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly Label _status = new() { AutoSize = false, Height = 38, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(112, 127, 118) };
    private readonly KryptonButton _connect = new() { Dock = DockStyle.Right, Width = 150, ButtonStyle = ButtonStyle.Standalone };
    private DesktopApiClient? _client;

    public ConnectionDialog()
    {
        Text = "Connect to Landscape Studio";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(590, 440);
        ClientSize = new Size(630, 440);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10f);
        BackColor = Color.FromArgb(246, 248, 246);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(30), ColumnCount = 1, RowCount = 6,
            BackColor = Color.White
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 67));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 67));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 67));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(layout);

        var heading = new Panel { Dock = DockStyle.Fill };
        heading.Controls.Add(new Label { Text = "Connect your workspace", AutoSize = true, Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold), ForeColor = Color.FromArgb(33, 47, 40), Location = new Point(0, 0) });
        heading.Controls.Add(new Label { Text = "Sign in through Identity. Credentials are used for this request only.", AutoSize = true, Font = new Font("Segoe UI", 9f), ForeColor = Color.FromArgb(112, 127, 118), Location = new Point(1, 34) });
        layout.Controls.Add(heading, 0, 0);

        layout.Controls.Add(BuildField("API gateway URL", _gateway), 0, 1);
        layout.Controls.Add(BuildField("Username", _userName), 0, 2);
        layout.Controls.Add(BuildField("Password", _password), 0, 3);
        layout.Controls.Add(_status, 0, 4);

        var actions = new Panel { Dock = DockStyle.Fill };
        var cancel = new KryptonButton { Dock = DockStyle.Right, Width = 105, ButtonStyle = ButtonStyle.LowProfile };
        cancel.Values.Text = "Cancel";
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        _connect.Values.Text = "Connect  →";
        _connect.StateCommon.Back.Color1 = Color.FromArgb(66, 125, 91);
        _connect.StateCommon.Back.Color2 = Color.FromArgb(66, 125, 91);
        _connect.StateCommon.Border.DrawBorders = PaletteDrawBorders.None;
        _connect.StateCommon.Content.ShortText.Color1 = Color.White;
        _connect.StateCommon.Content.ShortText.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
        _connect.Click += async (_, _) => await ConnectAsync();
        actions.Controls.Add(cancel);
        actions.Controls.Add(_connect);
        layout.Controls.Add(actions, 0, 5);

        _gateway.Text = ClientSettingsStore.LoadGatewayUri().AbsoluteUri;
        AcceptButton = _connect;
        CancelButton = cancel;
    }

    public DesktopApiClient? ConnectedClient => _client;

    private static Control BuildField(string labelText, Control editor)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 21));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = labelText, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(80, 98, 86), Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold) }, 0, 0);
        editor.Margin = new Padding(0, 2, 0, 0);
        if (editor is TextBox textBox)
        {
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.Font = new Font("Segoe UI", 10f);
        }
        panel.Controls.Add(editor, 0, 1);
        return panel;
    }

    private async Task ConnectAsync()
    {
        if (!Uri.TryCreate(_gateway.Text.Trim(), UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme is not ("http" or "https"))
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = "Enter a valid absolute HTTP or HTTPS gateway URL.";
            return;
        }

        _connect.Enabled = false;
        _status.ForeColor = Color.FromArgb(112, 127, 118);
        _status.Text = "Contacting Identity through the API gateway…";
        DesktopApiClient? candidate = null;
        try
        {
            candidate = new DesktopApiClient(baseUri);
            await candidate.LoginAsync(_userName.Text, _password.Text);
            ClientSettingsStore.SaveGatewayUri(candidate.BaseAddress);
            _client?.Dispose();
            _client = candidate;
            candidate = null;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (DesktopApiException exception)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = exception.IsUnauthorized
                ? "Login rejected. Verify the username and password."
                : $"Gateway response: {exception.Message}";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = exception is TaskCanceledException
                ? "The request timed out. Check the gateway and try again."
                : exception.Message;
        }
        finally
        {
            candidate?.Dispose();
            _connect.Enabled = true;
        }
    }
}
