import 'package:flutter/material.dart';
import '../services/api_service.dart';

class CancelBookingManagementScreen extends StatefulWidget {
  const CancelBookingManagementScreen({super.key});
  @override
  State<CancelBookingManagementScreen> createState() => _CancelBookingManagementScreenState();
}

class _CancelBookingManagementScreenState extends State<CancelBookingManagementScreen> {
  static const primary = Color(0xFF4F46E5);
  static const ink = Color(0xFF172554);
  bool _loading = true;
  int? _processing;
  String? _error;
  String _filter = 'Tất cả';
  List<Map<String, dynamic>> _requests = [];

  @override
  void initState() { super.initState(); _reload(); }

  dynamic _field(Map<String, dynamic> row, String key) =>
      row[key] ?? row['${key[0].toUpperCase()}${key.substring(1)}'];
  int? _id(dynamic value) => value is num ? value.toInt() : int.tryParse('${value ?? ''}');
  String _text(Map<String, dynamic> row, String key, [String fallback = '—']) =>
      _field(row, key)?.toString() ?? fallback;
  String _money(dynamic value) {
    final n = value is num ? value : num.tryParse('${value ?? ''}') ?? 0;
    return '${n.round().toString().replaceAllMapped(RegExp(r'\B(?=(\d{3})+(?!\d))'), (_) => '.')} đ';
  }
  String _date(dynamic value) {
    final d = DateTime.tryParse('${value ?? ''}');
    if (d == null) return value?.toString() ?? 'Chưa có';
    return '${d.day.toString().padLeft(2, '0')}/${d.month.toString().padLeft(2, '0')}/${d.year}';
  }
  bool _pending(String status) => status.trim().toLowerCase() == 'chờ duyệt' ||
      status.trim().toLowerCase() == 'chờ xử lý';

  Future<void> _reload() async {
    if (!mounted) return;
    setState(() { _loading = true; _error = null; });
    try {
      final result = await ApiService.getCancellationRequests();
      if (!mounted) return;
      setState(() { _requests = result.map((e) => Map<String, dynamic>.from(e)).toList(); _loading = false; });
    } catch (e) {
      if (!mounted) return;
      setState(() { _error = e.toString(); _loading = false; });
    }
  }

  void _toast(String message, {bool success = true}) {
    if (!mounted) return;
    final messenger = ScaffoldMessenger.of(context);
    messenger.hideCurrentSnackBar();
    messenger.showSnackBar(SnackBar(behavior: SnackBarBehavior.floating,
      backgroundColor: success ? const Color(0xFF15803D) : const Color(0xFFB91C1C),
      content: Text(message)));
  }

  Future<void> _process(Map<String, dynamic> row, bool approve) async {
    final id = _id(_field(row, 'maYeuCau'));
    if (id == null || _processing != null) return;
    final note = await showDialog<String>(context: context,
      builder: (_) => _DecisionDialog(
        approve: approve,
        bookingId: _text(row, 'maDatPhong'),
        refund: _money(_field(row, 'soTienHoan')),
      ));
    if (!mounted || note == null) return;
    setState(() => _processing = id);
    try {
      final result = await ApiService.processCancellationRequest(
        requestId: id, approve: approve, note: note);
      if (!mounted) return;
      final success = result['success'] == true;
      _toast(result['message']?.toString() ?? (success ? 'Đã xử lý yêu cầu.' : 'Xử lý thất bại.'), success: success);
      if (success) await _reload();
    } catch (e) {
      _toast('Không thể xử lý yêu cầu: $e', success: false);
    } finally {
      if (mounted) setState(() => _processing = null);
    }
  }

  Widget _detail(String label, String value, {bool accent = false}) => Padding(
    padding: const EdgeInsets.only(top: 9),
    child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Expanded(flex: 3, child: Text(label, style: const TextStyle(color: Color(0xFF64748B), fontSize: 13))),
      const SizedBox(width: 8),
      Expanded(flex: 5, child: Text(value, textAlign: TextAlign.end,
        style: TextStyle(color: accent ? primary : ink, fontWeight: accent ? FontWeight.bold : FontWeight.w600,
          fontSize: 13))),
    ]),
  );

  Widget _card(Map<String, dynamic> row) {
    final status = _text(row, 'trangThai', 'Chưa rõ');
    final pending = _pending(status);
    final id = _id(_field(row, 'maYeuCau'));
    final statusColor = pending ? const Color(0xFFB45309)
        : status.toLowerCase().contains('từ chối') ? const Color(0xFFDC2626) : const Color(0xFF15803D);
    final note = _field(row, 'ghiChuXuLy')?.toString().trim() ?? '';
    return Container(
      margin: const EdgeInsets.only(bottom: 14),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(18),
        border: Border.all(color: const Color(0xFFE2E8F0))),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Container(padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(color: const Color(0xFFFFF7ED), borderRadius: BorderRadius.circular(12)),
            child: const Icon(Icons.event_busy_rounded, color: Color(0xFFEA580C))),
          const SizedBox(width: 10),
          Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text('Phiếu đặt #${_text(row, 'maDatPhong')}',
              style: const TextStyle(color: ink, fontWeight: FontWeight.bold, fontSize: 16)),
            Text('Yêu cầu #${_text(row, 'maYeuCau')}',
              style: const TextStyle(color: Color(0xFF64748B), fontSize: 12)),
          ])),
          const SizedBox(width: 6),
          Flexible(child: Container(padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 6),
            decoration: BoxDecoration(color: statusColor.withValues(alpha: .10),
              borderRadius: BorderRadius.circular(20)),
            child: Text(status, textAlign: TextAlign.center,
              style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: statusColor)))),
        ]),
        const Divider(height: 24),
        _detail('Khách thuê', '#${_text(row, 'maKhach')}'),
        _detail('Phòng', '#${_text(row, 'maPhong')}'),
        _detail('Ngày gửi', _date(_field(row, 'ngayYeuCau'))),
        _detail('Báo trước', '${_text(row, 'soNgayBaoTruoc', '?')} ngày'),
        _detail('Tỷ lệ hoàn', '${_text(row, 'tyLeHoan', '0')}%'),
        _detail('Số tiền hoàn', _money(_field(row, 'soTienHoan')), accent: true),
        const SizedBox(height: 12),
        const Text('Lý do hủy', style: TextStyle(fontWeight: FontWeight.bold, color: ink)),
        const SizedBox(height: 5),
        Text(_text(row, 'lyDo', 'Không có'), style: const TextStyle(color: Color(0xFF475569))),
        if (!pending && note.isNotEmpty) ...[
          const SizedBox(height: 12),
          Text('Ghi chú xử lý: $note', style: const TextStyle(color: Color(0xFF475569))),
        ],
        if (pending) ...[
          const SizedBox(height: 16),
          Row(children: [
            Expanded(child: OutlinedButton(
              onPressed: _processing == null && id != null ? () => _process(row, false) : null,
              child: const Text('Từ chối'))),
            const SizedBox(width: 10),
            Expanded(child: FilledButton(
              style: FilledButton.styleFrom(backgroundColor: primary),
              onPressed: _processing == null && id != null ? () => _process(row, true) : null,
              child: _processing == id
                ? const SizedBox(width: 18, height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                : const Text('Duyệt'))),
          ]),
        ],
      ]),
    );
  }

  @override
  Widget build(BuildContext context) {
    final filtered = _requests.where((r) {
      final status = _text(r, 'trangThai');
      if (_filter == 'Tất cả') return true;
      if (_filter == 'Chờ duyệt') return _pending(status);
      return !_pending(status);
    }).toList();
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(backgroundColor: Colors.white, foregroundColor: ink,
        title: const Text('Yêu cầu hủy đặt phòng', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
        actions: [IconButton(onPressed: _loading || _processing != null ? null : _reload,
          icon: const Icon(Icons.refresh_rounded))]),
      body: Column(children: [
        SizedBox(height: 64, child: ListView(scrollDirection: Axis.horizontal,
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          children: ['Tất cả', 'Chờ duyệt', 'Đã xử lý'].map((label) => Padding(
            padding: const EdgeInsets.only(right: 8),
            child: ChoiceChip(label: Text(label), selected: _filter == label,
              onSelected: (_) => setState(() => _filter = label),
              selectedColor: const Color(0xFFE0E7FF)),
          )).toList())),
        Expanded(child: _loading
          ? const Center(child: CircularProgressIndicator(color: primary))
          : _error != null
            ? Center(child: Padding(padding: const EdgeInsets.all(24),
                child: Column(mainAxisSize: MainAxisSize.min, children: [
                  Text('Không tải được yêu cầu: $_error', textAlign: TextAlign.center),
                  TextButton(onPressed: _reload, child: const Text('Thử lại')),
                ])))
            : RefreshIndicator(onRefresh: _reload,
                child: ListView.builder(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.all(16),
                  itemCount: filtered.isEmpty ? 1 : filtered.length,
                  itemBuilder: (_, i) => filtered.isEmpty
                    ? const Padding(padding: EdgeInsets.only(top: 130),
                        child: Center(child: Text('Không có yêu cầu phù hợp.')))
                    : _card(filtered[i]),
                ))),
      ]),
    );
  }
}

class _DecisionDialog extends StatefulWidget {
  final bool approve;
  final String bookingId;
  final String refund;
  const _DecisionDialog({required this.approve, required this.bookingId, required this.refund});
  @override
  State<_DecisionDialog> createState() => _DecisionDialogState();
}

class _DecisionDialogState extends State<_DecisionDialog> {
  final _controller = TextEditingController();
  @override
  void dispose() { _controller.dispose(); super.dispose(); }
  @override
  Widget build(BuildContext context) => AlertDialog(
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
    title: Text(widget.approve ? 'Duyệt hủy đặt phòng?' : 'Từ chối yêu cầu?'),
    content: SingleChildScrollView(child: Column(mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(widget.approve
          ? 'Phiếu #${widget.bookingId} sẽ được hủy. Số tiền dự kiến hoàn: ${widget.refund}.'
          : 'Phiếu đặt #${widget.bookingId} sẽ được giữ nguyên.'),
        const SizedBox(height: 12),
        TextField(controller: _controller, maxLines: 2, maxLength: 500,
          decoration: const InputDecoration(labelText: 'Ghi chú (không bắt buộc)',
            border: OutlineInputBorder())),
      ])),
    actions: [
      TextButton(onPressed: () => Navigator.pop(context), child: const Text('Quay lại')),
      FilledButton(onPressed: () => Navigator.pop(context, _controller.text.trim()),
        child: Text(widget.approve ? 'Xác nhận duyệt' : 'Xác nhận từ chối')),
    ],
  );
}
