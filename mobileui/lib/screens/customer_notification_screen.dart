import 'package:flutter/material.dart';
import '../services/api_service.dart';

class CustomerNotificationScreen extends StatefulWidget {
  final int maKhach;
  const CustomerNotificationScreen({super.key, required this.maKhach});
  @override
  State<CustomerNotificationScreen> createState() => _CustomerNotificationScreenState();
}

class _CustomerNotificationScreenState extends State<CustomerNotificationScreen> {
  late Future<List<Map<String, dynamic>>> _future;
  @override
  void initState() { super.initState(); _future = _load(); }
  void _refresh() {
    setState(() { _future = _load(); });
  }
  Future<List<Map<String, dynamic>>> _load() async {
    return ApiService.getCustomerNotifications(widget.maKhach);
  }
  String _date(dynamic value) {
    final date = DateTime.tryParse(value?.toString() ?? '');
    if (date == null) return '';
    final d = date.toLocal();
    return '${d.day.toString().padLeft(2, '0')}/${d.month.toString().padLeft(2, '0')}/${d.year}';
  }
  @override
  Widget build(BuildContext context) {
    const purple = Color(0xFF5848EA);
    return Column(children: [
      Padding(padding: const EdgeInsets.fromLTRB(18, 10, 14, 8), child: Row(children: [
        const Icon(Icons.notifications_active_outlined, color: purple),
        const SizedBox(width: 8),
        const Expanded(child: Text('Thông báo từ chủ trọ', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800, color: Color(0xFF1B2552)))),
        IconButton(tooltip: 'Làm mới', onPressed: _refresh, icon: const Icon(Icons.refresh, color: purple)),
      ])),
      Expanded(child: FutureBuilder<List<Map<String, dynamic>>>(future: _future, builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
        if (snapshot.hasError) return Center(child: Padding(padding: const EdgeInsets.all(24), child: Column(mainAxisSize: MainAxisSize.min, children: [
          Text('${snapshot.error}', textAlign: TextAlign.center),
          TextButton(onPressed: _refresh, child: const Text('Thử lại')),
        ])));
        final items = snapshot.data ?? [];
        if (items.isEmpty) return const Center(child: Text('Chưa có thông báo nào'));
        return RefreshIndicator(onRefresh: () async { _refresh(); await _future; }, child: ListView.separated(
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
          itemCount: items.length,
          separatorBuilder: (_, __) => const SizedBox(height: 10),
          itemBuilder: (context, index) {
            final item = items[index];
            final title = (item['tieuDe'] ?? 'Thông báo').toString();
            final content = (item['noiDung'] ?? '').toString();
            final type = (item['loai'] ?? '').toString();
            final isFine = type.toLowerCase().contains('vi phạm') || title.toLowerCase().contains('vi phạm');
            return Card(color: Colors.white, elevation: 0, shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16), side: const BorderSide(color: Color(0xFFE2E8F0))),
              child: Padding(padding: const EdgeInsets.all(16), child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Container(padding: const EdgeInsets.all(10), decoration: BoxDecoration(color: isFine ? const Color(0xFFFFEDED) : const Color(0xFFEEECFF), borderRadius: BorderRadius.circular(12)),
                  child: Icon(isFine ? Icons.gpp_maybe_rounded : Icons.campaign_rounded, color: isFine ? Colors.red : purple)),
                const SizedBox(width: 12),
                Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(title, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.bold, color: Color(0xFF1B2552))),
                  const SizedBox(height: 5),
                  Text(content, style: const TextStyle(fontSize: 13, height: 1.45)),
                  const SizedBox(height: 8),
                  Text('${type.isEmpty ? 'Thông báo' : type} • ${_date(item['ngayGui'])}', style: const TextStyle(fontSize: 11, color: Color(0xFF94A3B8))),
                ])),
              ])));
          },
        ));
      }))
    ]);
  }
}
