using QRCoder;
using VibeCast.Server;

namespace VibeCast.UI;

/// <summary>
/// Shows the URL(s) the phone should open, plus a scannable QR code for the
/// selected address. The URL carries the pairing token, so scanning it is all
/// the phone needs to do — no certificate warning, no manual code entry.
/// </summary>
public sealed class ConnectionForm : Form
{
    private readonly WebServer _server;
    private readonly ComboBox _addressBox;
    private readonly PictureBox _qr;
    private readonly Label _hint;

    public ConnectionForm(WebServer server)
    {
        _server = server;

        Text = "VibeCast — Connect your phone";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 480);
        Font = new Font("Segoe UI", 9f);

        var title = new Label
        {
            Text = "1. Connect the phone to the same Wi-Fi\n2. Scan this QR code — it opens the page and pairs automatically\n3. Dictate with your phone's own keyboard mic",
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 64,
            Padding = new Padding(12, 10, 12, 0)
        };

        _addressBox = new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(12),
            Height = 28
        };
        foreach (var url in _server.ConnectionUrls())
            _addressBox.Items.Add(url);
        if (_addressBox.Items.Count > 0) _addressBox.SelectedIndex = 0;
        _addressBox.SelectedIndexChanged += (_, _) => RenderQr();

        var addressPanel = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(12, 4, 12, 4) };
        addressPanel.Controls.Add(_addressBox);

        _qr = new PictureBox
        {
            Dock = DockStyle.Top,
            Height = 260,
            SizeMode = PictureBoxSizeMode.CenterImage
        };

        var copyButton = new Button
        {
            Text = "Copy URL",
            Dock = DockStyle.Top,
            Height = 32,
            Margin = new Padding(12)
        };
        copyButton.Click += (_, _) =>
        {
            if (_addressBox.SelectedItem is string s)
            {
                try { Clipboard.SetText(s); } catch { }
            }
        };

        _hint = new Label
        {
            Dock = DockStyle.Fill,
            Text = "If the phone can't connect, allow VibeCast through Windows Defender Firewall " +
                   "(Private networks). See scripts\\add-firewall-rule.ps1.",
            Padding = new Padding(12, 6, 12, 6),
            ForeColor = SystemColors.GrayText
        };

        // Add in reverse dock order (last added docks innermost/top-most).
        Controls.Add(_hint);
        Controls.Add(copyButton);
        Controls.Add(_qr);
        Controls.Add(addressPanel);
        Controls.Add(title);

        RenderQr();
    }

    private void RenderQr()
    {
        if (_addressBox.SelectedItem is not string url) return;
        try
        {
            using var generator = new QRCodeGenerator();
            using QRCodeData data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
            var png = new PngByteQRCode(data);
            byte[] bytes = png.GetGraphic(8);

            _qr.Image?.Dispose();
            using var ms = new MemoryStream(bytes);
            _qr.Image = Image.FromStream(ms);
        }
        catch
        {
            _qr.Image = null;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Hide instead of destroying so re-opening is instant.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnFormClosing(e);
    }
}
