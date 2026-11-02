import 'package:flutter/material.dart';
import '../services/api_service.dart';

class ChatMessageScreen extends StatefulWidget {
  final int senderId; // ID người gửi (Admin hoặc Khách)
  final bool isAdmin;
  const ChatMessageScreen({super.key, required this.senderId, required this.isAdmin});

  @override
  State<ChatMessageScreen> createState() => _ChatMessageScreenState();
}

class _ChatMessageScreenState extends State<ChatMessageScreen> {
  final _msgController = TextEditingController();
  late Future<List<dynamic>> _messagesFuture;

  @override
  void initState() {
    super.initState();
    _loadMessages();
  }

  void _loadMessages() {
    setState(() {
      _messagesFuture = ApiService.getMessages(widget.senderId);
    });
  }

  void _sendMessage() async {
    final text = _msgController.text.trim();
    if (text.isEmpty) return;

    bool ok = await ApiService.sendMessage({
      // Dùng cùng khóa mã khách với hai màn hình chat đang được mở trong ứng dụng.
      'customerIdentity': widget.senderId,
      'content': text,
      'isAdmin': widget.isAdmin,
      'sendDate': DateTime.now().toIso8601String(),
    });

    if (ok && mounted) {
      _msgController.clear();
      _loadMessages();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: Text(widget.isAdmin ? 'Tin Nhắn & Thông Báo Khách' : 'Trò chuyện với Chủ Trọ', style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 18)),
        backgroundColor: Colors.white,
        foregroundColor: const Color(0xFF172554),
        elevation: 0,
        centerTitle: true,
      ),
      body: Column(
        children: [
          Expanded(
            child: FutureBuilder<List<dynamic>>(
              future: _messagesFuture,
              builder: (context, snapshot) {
                if (snapshot.connectionState == ConnectionState.waiting) {
                  return const Center(child: CircularProgressIndicator(color: Color(0xFF4F46E5)));
                }
                final messages = snapshot.data ?? [];
                if (messages.isEmpty) {
                  return const Center(child: Text('Chưa có tin nhắn nào. Hãy bắt đầu trò chuyện!', style: TextStyle(color: Color(0xFF64748B))));
                }
                return ListView.builder(
                  padding: const EdgeInsets.all(16),
                  itemCount: messages.length,
                  itemBuilder: (context, index) {
                    final m = messages[index];
                    final rawIsAdmin = m['isAdmin'] ?? m['IsAdmin'];
                    final isAdminMessage = rawIsAdmin == true ||
                        rawIsAdmin.toString().toLowerCase() == 'true';
                    final bool isMe = isAdminMessage == widget.isAdmin;
                    return Align(
                      alignment: isMe ? Alignment.centerRight : Alignment.centerLeft,
                      child: Container(
                        margin: const EdgeInsets.only(bottom: 10),
                        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                        decoration: BoxDecoration(
                          color: isMe ? const Color(0xFF4F46E5) : Colors.white,
                          borderRadius: BorderRadius.circular(16),
                          border: Border.all(color: isMe ? Colors.transparent : const Color(0xFFE2E8F0)),
                        ),
                        child: Text(
                          m['content'] ?? m['Content'] ?? '',
                          style: TextStyle(color: isMe ? Colors.white : const Color(0xFF172554), fontSize: 14),
                        ),
                      ),
                    );
                  },
                );
              },
            ),
          ),
          Container(
            padding: const EdgeInsets.all(12),
            color: Colors.white,
            child: Row(
              children: [
                Expanded(
                  child: TextField(
                    controller: _msgController,
                    decoration: InputDecoration(
                      hintText: 'Nhập nội dung tin nhắn, nhắc tiền trọ...',
                      hintStyle: const TextStyle(fontSize: 13, color: Color(0xFF94A3B8)),
                      border: OutlineInputBorder(borderRadius: BorderRadius.circular(24), borderSide: BorderSide.none),
                      filled: true,
                      fillColor: const Color(0xFFF5F7FC),
                      contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                IconButton(
                  onPressed: _sendMessage,
                  icon: const Icon(Icons.send_rounded, color: Color(0xFF4F46E5)),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}