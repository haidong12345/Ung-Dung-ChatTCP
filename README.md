# Ứng dụng Chat TCP 

Ứng dụng chat **desktop C#** 

## Cấu trúc

| Thư mục / file | Việc làm |
|----------------|----------|
| `code/Server/ChatServer.cs` | Lắng nghe TCP cổng 5000 |
| `code/Server/ClientSession.cs` | Xử lý từng client (đăng nhập, chat, admin) |
| `code/Data/DataStore.cs` | Lưu user/tin nhắn vào file JSON |
| `code/Network/PacketIO.cs` | Gửi/nhận gói tin (4 byte + JSON) |
| `code/Client/ChatClient.cs` | Kết nối TCP phía client |
| `code/UI/` | Giao diện WinForms |

## Chạy thử

**Bước 1 – Mở server (cửa sổ console):**

```bash
cd Ung-Dung-ChatTCP
dotnet run -- server
```

**Bước 2 – Mở client (giao diện):**

```bash
dotnet run
```

Mở **2 cửa sổ client** để chat thử (đăng ký 2 user khác nhau, ví dụ `user1` và `user2`).

> **Lưu ý:** Phải chạy **server trước**, rồi mở **2 client** với **2 tài khoản khác nhau**. Chọn tên user ở **cột trái**, sau đó gõ tin ở **ô dưới cùng bên phải** và bấm **Gửi** (hoặc Enter).

- **Admin mặc định:** `admin` / `admin123`
- Server: `127.0.0.1:5000`

## Tính năng

### User
- Đăng ký, đăng nhập, đăng xuất, đổi mật khẩu, quên mật khẩu (mã demo trả về màn hình)
- Chat 1-1, tin nhắn realtime qua TCP
- Gửi text, emoji, ảnh, file, video,quote, avata, reply
- **Trích dẫn (quote)** tin nhắn khi reply
- **Emoji phản hồi** (👍 ❤️ 😂 …) trên từng tin
- **Avatar** hiển thị trên giao diện (cập nhật qua menu Hồ sơ)
- Thu hồi tin nhắn
- Xem lịch sử chat
- Preview ảnh
- Online/offline, đang nhập, đã xem (seen)
- Thông báo tin mới (hộp thoại khi cửa sổ không focus)
- Cập nhật avatar, tên hiển thị, xem trạng thái tài khoản

### Admin
- Xem danh sách user
- Khóa / mở khóa tài khoản (menu **Admin**)

## Giao thức TCP (đơn giản)

Mỗi gói tin = **4 byte độ dài** + **chuỗi JSON**:

```json
{ "type": "LOGIN", "payload": { "username": "...", "password": "..." } }
```

Server trả lời: `LOGIN_OK`, `NEW_MESSAGE`, `USER_STATUS`, ...

## Dữ liệu

- `data/users.json` – tài khoản  
- `data/messages.json` – tin nhắn  
- `uploads/` – file/ảnh đã gửi  

(Nằm cạnh file `.exe` khi chạy.)
