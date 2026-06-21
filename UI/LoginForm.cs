using ChatApp.Client;
using ChatApp.Models;
using ChatApp.Network;

namespace ChatApp.UI;

/// <summary>
/// Màn hình đăng nhập / đăng ký / quên mật khẩu.
/// Luồng: kết nối TCP -> gửi LOGIN/REGISTER -> nhận token -> mở MainForm
/// </summary>
public class LoginForm : Form
{
    private readonly TextBox _txtServer = new() { Text = "127.0.0.1:5000", Width = 280 };
    private readonly TextBox _txtUser = new() { Width = 280 };
    private readonly TextBox _txtPass = new() { Width = 280, UseSystemPasswordChar = true };
    private readonly TextBox _txtDisplayName = new() { Width = 280 };
    private readonly TextBox _txtResetCode = new() { Width = 280 };
    private readonly TextBox _txtNewPass = new() { Width = 280, UseSystemPasswordChar = true };
    private readonly Label _lblMsg = new() { AutoSize = true, ForeColor = Color.DarkRed };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    public LoginForm()
    {
        Text = "Chat TCP - Đăng nhập";
        Size = new Size(420, 380);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var panelTop = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8) };
        panelTop.Controls.Add(new Label { Text = "Server:", AutoSize = true, Location = new Point(8, 12) });
        _txtServer.Location = new Point(60, 8);
        panelTop.Controls.Add(_txtServer);

        _tabs.TabPages.Add(CreateTab("Đăng nhập", LoginClick));
        _tabs.TabPages.Add(CreateTab("Đăng ký", RegisterClick, showDisplayName: true));
        _tabs.TabPages.Add(CreateForgotTab());

        _lblMsg.Dock = DockStyle.Bottom;
        _lblMsg.Padding = new Padding(8);

        Controls.Add(_tabs);
        Controls.Add(panelTop);
        Controls.Add(_lblMsg);
    }

    private TabPage CreateTab(string title, Func<object?, EventArgs, Task> onSubmit, bool showDisplayName = false)
    {
        var tab = new TabPage(title);
        var y = 20;
        tab.Controls.Add(new Label { Text = "Username:", Location = new Point(20, y), AutoSize = true });
        y += 25;
        var u = new TextBox { Width = 280, Location = new Point(20, y) };
        y += 35;
        tab.Controls.Add(u);
        tab.Controls.Add(new Label { Text = "Password:", Location = new Point(20, y), AutoSize = true });
        y += 25;
        var p = new TextBox { Width = 280, Location = new Point(20, y), UseSystemPasswordChar = true };
        y += 35;
        tab.Controls.Add(p);

        TextBox? dn = null;
        if (showDisplayName)
        {
            tab.Controls.Add(new Label { Text = "Tên hiển thị:", Location = new Point(20, y), AutoSize = true });
            y += 25;
            dn = new TextBox { Width = 280, Location = new Point(20, y) };
            y += 35;
            tab.Controls.Add(dn);
        }

        var btn = new Button { Text = title, Location = new Point(20, y), Width = 120 };
        btn.Click += async (_, _) =>
        {
            _txtUser.Text = u.Text;
            _txtPass.Text = p.Text;
            if (dn != null) _txtDisplayName.Text = dn.Text;
            await onSubmit(null, EventArgs.Empty);
        };
        tab.Controls.Add(btn);
        return tab;
    }

    private TabPage CreateForgotTab()
    {
        var tab = new TabPage("Quên MK");
        int y = 20;
        tab.Controls.Add(new Label { Text = "Username:", Location = new Point(20, y), AutoSize = true });
        y += 25;
        var u = new TextBox { Width = 280, Location = new Point(20, y) };
        y += 35;
        tab.Controls.Add(u);

        tab.Controls.Add(new Label { Text = "Mã reset (demo):", Location = new Point(20, y), AutoSize = true });
        y += 25;
        _txtResetCode.Location = new Point(20, y);
        y += 35;
        tab.Controls.Add(_txtResetCode);

        tab.Controls.Add(new Label { Text = "Mật khẩu mới:", Location = new Point(20, y), AutoSize = true });
        y += 25;
        _txtNewPass.Location = new Point(20, y);
        y += 35;
        tab.Controls.Add(_txtNewPass);

        var btnCode = new Button { Text = "Lấy mã", Location = new Point(20, y), Width = 80 };
        var btnReset = new Button { Text = "Đặt lại MK", Location = new Point(110, y), Width = 100 };
        tab.Controls.Add(btnCode);
        tab.Controls.Add(btnReset);

        btnCode.Click += async (_, _) => { _txtUser.Text = u.Text; await ForgotClick(null, EventArgs.Empty); };
        btnReset.Click += async (_, _) => { _txtUser.Text = u.Text; await ResetClick(null, EventArgs.Empty); };
        return tab;
    }

    /// <summary>Tách host và port từ chuỗi "127.0.0.1:5000".</summary>
    private (string host, int port) ParseServer()
    {
        var parts = _txtServer.Text.Trim().Split(':');
        int port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 5000;
        return (parts[0], port);
    }

    private async Task<ChatClient?> ConnectClientAsync()
    {
        var (host, port) = ParseServer();
        var client = new ChatClient { Host = host, Port = port };
        try
        {
            await client.ConnectAsync();
            return client;
        }
        catch (Exception ex)
        {
            _lblMsg.Text = "Không kết nối server: " + ex.Message;
            client.Dispose();
            return null;
        }
    }

    private async Task LoginClick(object? sender, EventArgs e)
    {
        _lblMsg.Text = "";
        var net = await ConnectClientAsync();
        if (net is null) return;

        var resp = await net.SendAndWaitAsync(new Packet
        {
            Type = "LOGIN",
            Payload = new AuthPayload { Username = _txtUser.Text.Trim(), Password = _txtPass.Text },
        }, "LOGIN_OK");

        await HandleAuthResponse(net, resp, "Đăng nhập thất bại");
    }

    private async Task RegisterClick(object? sender, EventArgs e)
    {
        _lblMsg.Text = "";
        var net = await ConnectClientAsync();
        if (net is null) return;

        var resp = await net.SendAndWaitAsync(new Packet
        {
            Type = "REGISTER",
            Payload = new AuthPayload
            {
                Username = _txtUser.Text.Trim(),
                Password = _txtPass.Text,
                DisplayName = _txtDisplayName.Text.Trim(),
            },
        }, "REGISTER_OK");

        await HandleAuthResponse(net, resp, "Đăng ký thất bại");
    }

    /// <summary>Xử lý chung sau LOGIN hoặc REGISTER thành công.</summary>
    private Task HandleAuthResponse(ChatClient net, Packet resp, string failMessage)
    {
        if (!resp.IsSuccess)
        {
            _lblMsg.Text = resp.Error ?? failMessage;
            net.Dispose();
            return Task.CompletedTask;
        }

        var auth = PacketIO.ParsePayload<AuthResult>(resp.Payload);
        if (auth is null || string.IsNullOrEmpty(auth.Token))
        {
            _lblMsg.Text = "Lỗi đọc phản hồi server";
            net.Dispose();
            return Task.CompletedTask;
        }

        net.SetAuth(auth);
        OpenMain(net, auth.Users);
        return Task.CompletedTask;
    }

    private async Task ForgotClick(object? sender, EventArgs e)
    {
        _lblMsg.Text = "";
        var net = await ConnectClientAsync();
        if (net is null) return;

        var resp = await net.SendAndWaitAsync(new Packet
        {
            Type = "FORGOT_PASSWORD",
            Payload = new ForgotPayload { Username = _txtUser.Text.Trim() },
        }, "FORGOT_PASSWORD_OK");

        net.Dispose();
        if (resp.IsSuccess && resp.Payload is System.Text.Json.JsonElement el && el.TryGetProperty("resetCode", out var c))
        {
            _lblMsg.ForeColor = Color.DarkGreen;
            _lblMsg.Text = "Mã reset: " + c.GetString();
        }
        else
            _lblMsg.Text = resp.Error ?? "Lỗi";
    }

    private async Task ResetClick(object? sender, EventArgs e)
    {
        _lblMsg.Text = "";
        var net = await ConnectClientAsync();
        if (net is null) return;

        var resp = await net.SendAndWaitAsync(new Packet
        {
            Type = "RESET_PASSWORD",
            Payload = new ForgotPayload
            {
                Username = _txtUser.Text.Trim(),
                ResetCode = _txtResetCode.Text.Trim(),
                NewPassword = _txtNewPass.Text,
            },
        }, "RESET_PASSWORD_OK");

        net.Dispose();
        _lblMsg.ForeColor = resp.IsSuccess ? Color.DarkGreen : Color.DarkRed;
        _lblMsg.Text = resp.IsSuccess ? "Đổi mật khẩu thành công!" : (resp.Error ?? "Lỗi");
    }

    private void OpenMain(ChatClient client, List<UserInfo> users)
    {
        Hide();
        var main = new MainForm(client, users);
        main.FormClosed += (_, _) => { client.Dispose(); Close(); };
        main.Show();
    }
<<<<<<< HEAD
}
=======
}
>>>>>>> caaafb54b6de495884be5b999f43bf8324db552c
