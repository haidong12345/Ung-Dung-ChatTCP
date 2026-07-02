# Ứng dụng Chat TCP

Ứng dụng chat desktop viết bằng ngôn ngữ **C# (WinForms)** sử dụng giao thức kết nối TCP/IP.

---

## 🛠️ Yêu cầu hệ thống
* **.NET SDK** (Hỗ trợ .NET 6.0 trở lên)
* Hệ điều hành Windows (để chạy giao diện WinForms)

---

## 📂 Cấu trúc dự án

| Thư mục / File | Vai trò |
| :--- | :--- |
| `code/Server/ChatServer.cs` | Lắng nghe kết nối TCP cổng 5000. |
| `code/Server/ClientSession.cs` | Xử lý từng client (đăng nhập, chat, admin). |
| `code/Data/DataStore.cs` | Lưu thông tin user và tin nhắn vào file JSON. |
| `code/Network/PacketIO.cs` | Gửi/nhận gói tin định dạng (4 byte độ dài + chuỗi JSON). |
| `code/Client/ChatClient.cs` | Kết nối TCP phía client. |
| `code/UI/` | Giao diện đồ họa WinForms. |

---

## 🚀 Hướng dẫn chạy thử

> ⚠️ **Lưu ý quan trọng:** Bạn phải khởi chạy **Server trước**, sau đó mới mở các cửa sổ **Client**.

### Bước 1: Khởi động Server (Cửa sổ Console)
Mở Terminal/Cmd tại thư mục gốc dự án và chạy các lệnh sau:
```bash
cd Ung-Dung-ChatTCP
cd code
dotnet run -- server
Bước 2: Khởi động Client (Giao diện WinForms)
Mở một cửa sổ Terminal mới (hoặc mở thêm 2-3 cửa sổ độc lập) để chạy Client:

Bash
dotnet run
Hãy mở ít nhất 2 cửa sổ client và đăng ký 2 tài khoản khác nhau (ví dụ: user1 và user2) để kiểm tra tính năng chat qua lại.

Tài khoản Admin mặc định: admin / admin123

Địa chỉ Server mặc định: 127.0.0.1:5000

✨ Tính năng của ứng dụng
👤 Cho Người dùng (User)
Quản lý tài khoản: Đăng ký, đăng nhập, đăng xuất, đổi mật khẩu, quên mật khẩu (mã demo hiển thị trực tiếp trên màn hình).

Trạng thái: Hiển thị online/offline, trạng thái đang nhập (typing...), và đã xem (seen).

Nhắn tin realtime: Chat 1-1 qua giao thức TCP với tốc độ tức thời.

Định dạng tin nhắn: Hỗ trợ gửi văn bản (text), emoji, hình ảnh (có xem trước - preview), file dữ liệu và video.

Tương tác tin nhắn: * Trích dẫn (quote) tin nhắn khi phản hồi (reply).

Thả emoji cảm xúc (👍, ❤️, 😂,...) trực tiếp trên từng tin nhắn.

Thu hồi tin nhắn đã gửi.

Xem lại lịch sử chat cũ.

Cá nhân hóa: Cập nhật ảnh đại diện (avatar), đổi tên hiển thị, xem trạng thái tài khoản.

Thông báo: Hiển thị hộp thoại thông báo tin nhắn mới khi cửa sổ ứng dụng đang không được focus.

🛠️ Cho Quản trị viên (Admin)
Xem toàn bộ danh sách người dùng trong hệ thống.

Khóa hoặc mở khóa tài khoản thành viên trực tiếp từ menu Admin.

🛰️ Giao thức TCP (Đơn giản)
Mỗi gói tin truyền đi qua mạng được cấu trúc theo công thức: 4 byte độ dài + chuỗi JSON định dạng.

Ví dụ gói tin đăng nhập:

JSON
{ 
  "type": "LOGIN", 
  "payload": { 
    "username": "...", 
    "password": "..." 
  } 
}
Server sẽ phản hồi lại bằng các trạng thái tương ứng như: LOGIN_OK, NEW_MESSAGE, USER_STATUS,...

💾 Lưu trữ dữ liệu
Dữ liệu được lưu trữ local dưới dạng file JSON nằm ngay cạnh file .exe khi ứng dụng vận hành:

data/users.json – Quản lý thông tin tài khoản.

data/messages.json – Lưu trữ lịch sử tin nhắn.

uploads/ – Thư mục chứa các file/ảnh đã được người dùng gửi qua lại.
