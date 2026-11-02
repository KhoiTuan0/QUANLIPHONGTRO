import 'package:flutter/material.dart';
import '../services/api_service.dart';

class AdminBroadcastScreen extends StatefulWidget {
  const AdminBroadcastScreen({super.key});
  @override
  State<AdminBroadcastScreen> createState() => _AdminBroadcastScreenState();
}
class _AdminBroadcastScreenState extends State<AdminBroadcastScreen> {
  final _title = TextEditingController();
  final _content = TextEditingController();
  bool _sending = false;
  @override
  void dispose() { _title.dispose(); _content.dispose(); super.dispose(); }
  Future<void> _send() async {
    final title = _title.text.trim(), content = _content.text.trim();
    if (title.isEmpty || content.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Nhập tiêu đề và nội dung thông báo')));
      return;
    }
    final confirmed = await showDialog<bool>(context: context, builder: (ctx) => AlertDialog(
      title: const Text('Xác nhận gửi thông báo'),
      content: const Text('Gửi thông báo này tới tất cả tài khoản khách hàng đang hoạt động?'),
      actions: [TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Hủy')),
        FilledButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('Gửi'))],
    ));
    if (confirmed != true || !mounted) return;
    setState(() => _sending = true);
    try {
      final message = await ApiService.sendBroadcastNotification(title: title, content: content);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
      _title.clear(); _content.clear();
    } catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Gửi thất bại: $e')));
    } finally { if (mounted) setState(() => _sending = false); }
  }
  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: const Color(0xFFF5F6FC),
    appBar: AppBar(title: const Text('Gửi thông báo chung'), backgroundColor: Colors.white),
    body: ListView(padding: const EdgeInsets.all(18), children: [
      const Card(child: ListTile(leading: Icon(Icons.groups_rounded, color: Color(0xFF5848EA)),
        title: Text('Người nhận'), subtitle: Text('Tất cả tài khoản khách hàng đang hoạt động'))),
      const SizedBox(height: 16),
      TextField(controller: _title, maxLength: 150, decoration: const InputDecoration(labelText: 'Tiêu đề', hintText: 'Ví dụ: Thông báo bảo trì điện', border: OutlineInputBorder())),
      const SizedBox(height: 14),
      TextField(controller: _content, maxLines: 7, minLines: 5, decoration: const InputDecoration(labelText: 'Nội dung thông báo', hintText: 'Nhập nội dung muốn gửi...', border: OutlineInputBorder())),
      const SizedBox(height: 18),
      FilledButton.icon(onPressed: _sending ? null : _send,
        icon: _sending ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2)) : const Icon(Icons.send_rounded),
        label: Text(_sending ? 'Đang gửi...' : 'Gửi đến tất cả khách'),
        style: FilledButton.styleFrom(backgroundColor: const Color(0xFF5848EA), padding: const EdgeInsets.all(16))),
    ]),
  );
}
