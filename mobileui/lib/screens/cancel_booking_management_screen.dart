import 'package:flutter/material.dart';
import '../services/api_service.dart';

class CancelBookingManagementScreen extends StatefulWidget {
  const CancelBookingManagementScreen({super.key});

  @override
  State<CancelBookingManagementScreen> createState() =>
      _CancelBookingManagementScreenState();
}

class _CancelBookingManagementScreenState
    extends State<CancelBookingManagementScreen> {
  static const primary = Color(0xFF4F46E5);
  bool loading = true;
  int? processing;
  String? error;
  List<Map<String, dynamic>> requests = [];

  @override
  void initState() {
    super.initState();
    _reload();
  }

  dynamic field(Map<String, dynamic> item, String key) =>
      item[key] ?? item['${key[0].toUpperCase()}${key.substring(1)}'];

  String money(dynamic value) {
    final n = num.tryParse(value?.toString() ?? '') ?? 0;
    return '${n.round().toString().replaceAllMapped(RegExp(r'\B(?=(\d{3})+(?!\d))'), (_) => '.')} đ';
  }

  String date(dynamic value) {
    final dt = DateTime.tryParse(value?.toString() ?? '');
    return dt == null ? 'Chưa có' : '${dt.day.toString().padLeft(2, '0')}/${dt.month.toString().padLeft(2, '0')}/${dt.year}';
  }

  Future<void> _reload() async {
    setState(() { loading = true; error = null; });
    try {
      final result = await ApiService.getCancellationRequests();
      if (!mounted) return;
      setState(() { requests = result; loading = false; });
    } catch (e) {
      if (!mounted) return;
      setState(() { error = e.toString(); loading = false; });
    }
  }

  Future<void> _process(Map<String, dynamic> item, bool approve) async {
    final id = int.tryParse(field(item, 'maYeuCau').toString());
    if (id == null) return;
    final note = TextEditingController();
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(approve ? 'Duyệt hủy đặt phòng?' : 'Từ chối yêu cầu?'),
        content: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(approve
              ? 'Phiếu #${field(item, 'maDatPhong')} sẽ bị hủy. Hoàn ${money(field(item, 'soTienHoan'))} và trả phòng về Trống.'
              : 'Phiếu đặt phòng sẽ được giữ nguyên.'),
          const SizedBox(height: 14),
          TextField(controller: note, maxLines: 2, decoration: const InputDecoration(
            labelText: 'Ghi chú xử lý (không bắt buộc)', border: OutlineInputBorder())),
        ]),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Quay lại')),
          FilledButton(onPressed: () => Navigator.pop(ctx, true),
              child: Text(approve ? 'Xác nhận duyệt' : 'Xác nhận từ chối')),
        ],
      ),
    );
    final ghiChu = note.text.trim();
    note.dispose();
    if (confirmed != true || !mounted) return;
    setState(() => processing = id);
    try {
      final result = await ApiService.processCancellationRequest(
        requestId: id, approve: approve, note: ghiChu);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(
        content: Text(result['message']?.toString() ?? 'Đã xử lý.'),
      ));
      if (result['success'] == true) await _reload();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Lỗi: $e')));
    } finally {
      if (mounted) setState(() => processing = null);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(title: const Text('Yêu cầu hủy đặt phòng'),
          backgroundColor: Colors.white, foregroundColor: const Color(0xFF172554),
          actions: [IconButton(onPressed: loading ? null : _reload, icon: const Icon(Icons.refresh))]),
      body: loading
          ? const Center(child: CircularProgressIndicator(color: primary))
          : error != null
              ? Center(child: Padding(padding: const EdgeInsets.all(24),
                  child: Text('Không tải được yêu cầu: $error')))
              : RefreshIndicator(
                  onRefresh: _reload,
                  child: requests.isEmpty
                      ? ListView(children: const [SizedBox(height: 180),
                          Center(child: Text('Chưa có yêu cầu hủy đặt phòng.'))])
                      : ListView.builder(
                          padding: const EdgeInsets.all(16),
                          itemCount: requests.length,
                          itemBuilder: (_, index) {
                            final item = requests[index];
                            final status = field(item, 'trangThai')?.toString() ?? '';
                            final id = int.tryParse(field(item, 'maYeuCau').toString());
                            final pending = status == 'Chờ duyệt';
                            return Card(
                              color: Colors.white, margin: const EdgeInsets.only(bottom: 14),
                              child: Padding(
                                padding: const EdgeInsets.all(16),
                                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                                  Text('Yêu cầu #${field(item, 'maYeuCau')} · Phiếu #${field(item, 'maDatPhong')}',
                                      style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
                                  const SizedBox(height: 8),
                                  Text('Khách #${field(item, 'maKhach')} · Phòng #${field(item, 'maPhong')}'),
                                  Text('Ngày gửi: ${date(field(item, 'ngayYeuCau'))}'),
                                  Text('Lý do: ${field(item, 'lyDo') ?? 'Không có'}'),
                                  Text('Báo trước: ${field(item, 'soNgayBaoTruoc') ?? '?'} ngày'),
                                  Text('Hoàn cọc: ${field(item, 'tyLeHoan') ?? 0}% · ${money(field(item, 'soTienHoan'))}',
                                      style: const TextStyle(color: primary, fontWeight: FontWeight.w700)),
                                  const SizedBox(height: 8),
                                  Text('Trạng thái: $status', style: const TextStyle(fontWeight: FontWeight.w600)),
                                  if (!pending && field(item, 'ghiChuXuLy') != null)
                                    Text('Ghi chú: ${field(item, 'ghiChuXuLy')}'),
                                  if (pending) ...[
                                    const SizedBox(height: 12),
                                    Row(children: [
                                      Expanded(child: OutlinedButton(
                                        onPressed: processing != null ? null : () => _process(item, false),
                                        child: const Text('Từ chối'))),
                                      const SizedBox(width: 10),
                                      Expanded(child: FilledButton(
                                        style: FilledButton.styleFrom(backgroundColor: primary),
                                        onPressed: processing != null || id == null ? null : () => _process(item, true),
                                        child: processing == id
                                            ? const SizedBox(width: 18, height: 18,
                                                child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                                            : const Text('Duyệt'))),
                                    ]),
                                  ],
                                ]),
                              ),
                            );
                          },
                        ),
                ),
    );
  }
}
