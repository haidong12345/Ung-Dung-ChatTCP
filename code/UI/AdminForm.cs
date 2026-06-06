using ChatApp.Client;
using ChatApp.Models;
using ChatApp.Network;

namespace ChatApp.UI;

/// <summary>Màn hình admin: xem danh sách user và khóa/mở khóa tài khoản.</summary>
public class AdminForm : Form
{
    private readonly ChatClient _client;
    private readonly ListView _list = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
    };

    public AdminForm(ChatClient client)
    {
        _client = client;
        Text = "Quản trị - Danh sách user";
        Size = new Size(620, 400);
        StartPosition = FormStartPosition.CenterParent;

        _list.Columns.Add("Username", 100);
        _list.Columns.Add("Tên hiển thị", 120);
        _list.Columns.Add("Role", 60);
        _list.Columns.Add("Trạng thái", 80);
        _list.Columns.Add("Online", 60);

        var btnLock = new Button { Text = "Khóa / Mở khóa", Dock = DockStyle.Bottom, Height = 36 };
        btnLock.Click += async (_, _) => await ToggleLockAsync();

        Controls.Add(_list);
        Controls.Add(btnLock);
        Load += async (_, _) => await LoadUsersAsync();
    }

    private async Task LoadUsersAsync()
    {
        var resp = await _client.SendAndWaitAsync(new Packet
        {
            Type = "ADMIN_USERS",
            Payload = new TokenPayload { Token = _client.Token! },
        }, "ADMIN_USERS_OK");

        if (!resp.IsSuccess) { MessageBox.Show(resp.Error); return; }

        var users = PacketIO.ParseUserList(resp.Payload);
        _list.Items.Clear();
        foreach (var u in users)
        {
            var item = new ListViewItem(u.Username);
            item.SubItems.Add(u.DisplayName);
            item.SubItems.Add(u.Role);
            item.SubItems.Add(u.Status);
            item.SubItems.Add(u.Online ? "Có" : "Không");
            item.Tag = u;
            _list.Items.Add(item);
        }
    }

    private async Task ToggleLockAsync()
    {
        if (_list.SelectedItems.Count == 0) return;
        var u = (UserInfo)_list.SelectedItems[0].Tag!;

        var resp = await _client.SendAndWaitAsync(new Packet
        {
            Type = "ADMIN_LOCK",
            Payload = new LockUserPayload { Token = _client.Token!, UserId = u.Id },
        }, "ADMIN_LOCK_OK");

        if (!resp.IsSuccess) MessageBox.Show(resp.Error);
        await LoadUsersAsync();
    }
}
