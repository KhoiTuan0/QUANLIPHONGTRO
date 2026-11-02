import 'dart:async';

import 'package:flutter/material.dart';
import '../services/api_service.dart';

class AdminChatScreen extends StatefulWidget {
  final int maKhach;
  final String tenKhach;

  const AdminChatScreen({
    super.key,
    required this.maKhach,
    required this.tenKhach,
  });

  @override
  State<AdminChatScreen> createState() => _AdminChatScreenState();
}

class _AdminChatScreenState extends State<AdminChatScreen> {
  final _msgController = TextEditingController();
  final _scrollController = ScrollController();
  final List<dynamic> _messages = [];
  Timer? _pollingTimer;
  bool _isLoading = true;
  bool _isSending = false;
  bool _isRefreshing = false;
  String? _loadError;

  @override
  void initState() {
    super.initState();
    _loadMessages(showLoading: true);
    // Tự đồng bộ tin nhắn định kỳ để chủ trọ nhận phản hồi của khách ngay trên màn hình.
    _pollingTimer = Timer.periodic(const Duration(seconds: 3), (_) {
      _loadMessages();
    });
  }

  Future<void> _loadMessages({bool showLoading = false}) async {
    if (_isRefreshing) return;
    _isRefreshing = true;
    final wasNearBottom =
        !_scrollController.hasClients ||
        _scrollController.position.maxScrollExtent -
                _scrollController.position.pixels <
            100;
    if (showLoading && mounted) {
      setState(() => _isLoading = true);
    }

    try {
      final messages = await ApiService.getMessages(widget.maKhach);
      messages.sort((a, b) => _messageDate(a).compareTo(_messageDate(b)));
      if (!mounted) return;
      setState(() {
        _messages
          ..clear()
          ..addAll(messages);
        _loadError = null;
        _isLoading = false;
      });
      // Cuộn theo tin mới nếu chủ trọ đang xem cuối hội thoại; không giật khi đọc tin cũ.
      if (wasNearBottom) {
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (_scrollController.hasClients) {
            _scrollController.animateTo(
              _scrollController.position.maxScrollExtent,
              duration: const Duration(milliseconds: 250),
              curve: Curves.easeOut,
            );
          }
        });
      }
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loadError = error.toString();
        _isLoading = false;
      });
    } finally {
      _isRefreshing = false;
    }
  }

  DateTime _messageDate(dynamic message) {
    if (message is! Map) return DateTime.fromMillisecondsSinceEpoch(0);
    final value = message['sendDate'] ?? message['SendDate'];
    return DateTime.tryParse(value?.toString() ?? '') ??
        DateTime.fromMillisecondsSinceEpoch(0);
  }

  bool _isFromLandlord(dynamic message) {
    if (message is! Map) return false;
    final value = message['isAdmin'] ?? message['IsAdmin'];
    return value == true || value.toString().toLowerCase() == 'true';
  }

  Future<void> _sendMessage() async {
    final text = _msgController.text.trim();
    if (text.isEmpty || _isSending) return;

    setState(() => _isSending = true);
    try {
      final ok = await ApiService.sendMessage({
        // Dùng chung mã khách để tin của hai vai trò nằm trong cùng một hội thoại.
        'customerIdentity': widget.maKhach,
        'content': text,
        'isAdmin': true,
        'sendDate': DateTime.now().toIso8601String(),
      });

      if (!mounted) return;
      if (ok) {
        _msgController.clear();
        await _loadMessages();
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Gửi tin nhắn thất bại. Vui lòng thử lại.'),
          ),
        );
      }
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Không gửi được tin nhắn: $error')),
        );
      }
    } finally {
      if (mounted) setState(() => _isSending = false);
    }
  }

  @override
  void dispose() {
    _pollingTimer?.cancel();
    _msgController.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  // Hiển thị giờ cho tin nhắn hôm nay và cả ngày/tháng cho tin nhắn cũ hơn.
  String _formatMessageTime(String? dateStr) {
    if (dateStr == null || dateStr.isEmpty) return '';
    try {
      DateTime dt = DateTime.parse(dateStr);
      DateTime now = DateTime.now();

      String hour = dt.hour.toString().padLeft(2, '0');
      String minute = dt.minute.toString().padLeft(2, '0');

      if (dt.year == now.year && dt.month == now.month && dt.day == now.day) {
        return '$hour:$minute'; // Hôm nay chỉ hiện giờ:phút
      } else {
        return '${dt.day}/${dt.month} $hour:$minute'; // Khác ngày hiện thêm ngày/tháng
      }
    } catch (_) {
      return '';
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: Text(
          'Trò chuyện với ${widget.tenKhach}',
          style: const TextStyle(
            fontWeight: FontWeight.bold,
            fontSize: 16,
            color: Color(0xFF172554),
          ),
        ),
        backgroundColor: Colors.white,
        foregroundColor: const Color(0xFF172554),
        elevation: 0,
        centerTitle: true,
      ),
      body: Column(
        children: [
          Expanded(
            child: RefreshIndicator(
              color: const Color(0xFF4F46E5),
              onRefresh: () => _loadMessages(showLoading: true),
              child: _isLoading && _messages.isEmpty
                  ? ListView(
                      physics: const AlwaysScrollableScrollPhysics(),
                      children: [
                        SizedBox(height: 220),
                        Center(
                          child: CircularProgressIndicator(
                            color: Color(0xFF4F46E5),
                          ),
                        ),
                      ],
                    )
                  : _messages.isEmpty
                  ? ListView(
                      physics: const AlwaysScrollableScrollPhysics(),
                      children: [
                        const SizedBox(height: 150),
                        Center(
                          child: Column(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              Icon(
                                _loadError == null
                                    ? Icons.chat_bubble_outline_rounded
                                    : Icons.cloud_off_rounded,
                                size: 64,
                                color: Colors.grey.shade400,
                              ),
                              const SizedBox(height: 12),
                              Text(
                                _loadError == null
                                    ? 'Chưa có tin nhắn nào với ${widget.tenKhach}.'
                                    : 'Không tải được tin nhắn. Vuốt xuống để thử lại.',
                                textAlign: TextAlign.center,
                                style: const TextStyle(
                                  color: Color(0xFF64748B),
                                  fontSize: 14,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    )
                  : ListView.builder(
                      controller: _scrollController,
                      physics: const AlwaysScrollableScrollPhysics(),
                      padding: const EdgeInsets.all(16),
                      itemCount:
                          _messages.length + (_loadError == null ? 0 : 1),
                      itemBuilder: (context, index) {
                        if (_loadError != null && index == 0) {
                          return const Padding(
                            padding: EdgeInsets.only(bottom: 12),
                            child: Text(
                              'Đang mất kết nối. Tin nhắn cũ vẫn được giữ; ứng dụng sẽ tự thử lại.',
                              style: TextStyle(
                                color: Color(0xFFB45309),
                                fontSize: 12,
                              ),
                            ),
                          );
                        }
                        final messageIndex =
                            index - (_loadError == null ? 0 : 1);
                        final m = _messages[messageIndex];
                        final fromAdmin = _isFromLandlord(m);
                        final timeText = _formatMessageTime(
                          (m is Map ? m['sendDate'] ?? m['SendDate'] : null)
                              ?.toString(),
                        );
                        final content = m is Map
                            ? (m['content'] ?? m['Content'] ?? '').toString()
                            : '';

                        return Align(
                          alignment: fromAdmin
                              ? Alignment.centerRight
                              : Alignment.centerLeft,
                          child: Column(
                            crossAxisAlignment: fromAdmin
                                ? CrossAxisAlignment.end
                                : CrossAxisAlignment.start,
                            children: [
                              Container(
                                margin: const EdgeInsets.only(bottom: 4),
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 16,
                                  vertical: 12,
                                ),
                                constraints: BoxConstraints(
                                  maxWidth:
                                      MediaQuery.of(context).size.width * 0.75,
                                ),
                                decoration: BoxDecoration(
                                  color: fromAdmin
                                      ? const Color(0xFF4F46E5)
                                      : Colors.white,
                                  borderRadius: BorderRadius.circular(16),
                                  border: Border.all(
                                    color: fromAdmin
                                        ? Colors.transparent
                                        : const Color(0xFFE2E8F0),
                                  ),
                                ),
                                child: Text(
                                  content,
                                  style: TextStyle(
                                    color: fromAdmin
                                        ? Colors.white
                                        : const Color(0xFF172554),
                                    fontSize: 14,
                                  ),
                                ),
                              ),
                              Padding(
                                padding: const EdgeInsets.only(
                                  bottom: 8,
                                  left: 4,
                                  right: 4,
                                ),
                                child: Text(
                                  timeText,
                                  style: const TextStyle(
                                    fontSize: 10,
                                    color: Color(0xFF94A3B8),
                                  ),
                                ),
                              ),
                            ],
                          ),
                        );
                      },
                    ),
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
                    enabled: !_isSending,
                    onSubmitted: (_) => _sendMessage(),
                    textCapitalization: TextCapitalization.sentences,
                    decoration: InputDecoration(
                      hintText: 'Nhập tin nhắn nhắc tiền, thông báo...',
                      hintStyle: const TextStyle(
                        fontSize: 13,
                        color: Color(0xFF94A3B8),
                      ),
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(24),
                        borderSide: BorderSide.none,
                      ),
                      filled: true,
                      fillColor: const Color(0xFFF5F7FC),
                      contentPadding: const EdgeInsets.symmetric(
                        horizontal: 16,
                        vertical: 10,
                      ),
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                IconButton(
                  onPressed: _isSending ? null : _sendMessage,
                  icon: _isSending
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(
                          Icons.send_rounded,
                          color: Color(0xFF4F46E5),
                        ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
