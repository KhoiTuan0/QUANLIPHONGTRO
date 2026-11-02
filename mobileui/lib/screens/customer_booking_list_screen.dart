import 'package:flutter/material.dart';
import '../services/api_service.dart';

class CustomerBookingListScreen extends StatefulWidget {
  final int maKhach;
  const CustomerBookingListScreen({super.key, required this.maKhach});

  @override
  State<CustomerBookingListScreen> createState() => _CustomerBookingListScreenState();
}

class _CustomerBookingListScreenState extends State<CustomerBookingListScreen> {
  static const primary = Color(0xFF4F46E5);
  static const ink = Color(0xFF172554);
  static const muted = Color(0xFF64748B);
  bool _loading = true;
  bool _sendingCancel = false;
  String? _error;
  List<Map<String, dynamic>> _bookings = [];
  Map<int, String> _roomNames = {};
  final Set<int> _pendingCancelIds = {};
  final Map<int, String> _cancelStatuses = {};

  @override
  void initState() {
    super.initState();
    _loadBookings();
  }

  int? _asInt(dynamic value) => value is num ? value.toInt() : int.tryParse('${value ?? ''}');

  dynamic _field(Map<String, dynamic> row, List<String> keys) {
    for (final key in keys) {
      if (row[key] != null) return row[key];
    }
    return null;
  }

  String _money(dynamic value) {
    final number = value is num ? value : num.tryParse('${value ?? ''}') ?? 0;
    return '${number.round().toString().replaceAllMapped(RegExp(r'\B(?=(\d{3})+(?!\d))'), (_) => '.')} đ';
  }

  String _date(dynamic value) {
    final date = DateTime.tryParse('${value ?? ''}');
    if (date == null) return value?.toString() ?? 'Chưa có';
    return '${date.day.toString().padLeft(2, '0')}/${date.month.toString().padLeft(2, '0')}/${date.year}';
  }

  Future<void> _loadBookings() async {
    if (!mounted) return;
    setState(() { _loading = true; _error = null; });
    try {
      final response = await ApiService.getBookings();
      final bookings = response.whereType<Map>().map((e) => Map<String, dynamic>.from(e))
          .where((e) => _asInt(_field(e, ['maKhach', 'MaKhach', 'customerId'])) == widget.maKhach)
          .toList();
      bookings.sort((a, b) => (_asInt(_field(b, ['maDatPhong', 'MaDatPhong', 'id'])) ?? 0)
          .compareTo(_asInt(_field(a, ['maDatPhong', 'MaDatPhong', 'id'])) ?? 0));

      final names = <int, String>{};
      try {
        final rooms = await ApiService.getRooms();
        for (final raw in rooms) {
          if (raw is! Map) continue;
          final row = Map<String, dynamic>.from(raw);
          final id = _asInt(_field(row, ['maPhong', 'MaPhong', 'roomId']));
          final name = _field(row, ['tenPhong', 'TenPhong', 'roomNumber']);
          if (id != null && name != null) names[id] = name.toString();
        }
      } catch (_) {}

      // Đồng bộ trạng thái khi API cho phép khách đọc danh sách yêu cầu.
      // Không xóa trạng thái cục bộ nếu API này bị giới hạn quyền.
      try {
        final requests = await ApiService.getCancellationRequests();
        final updated = <int, String>{};
        for (final raw in requests) {
          final row = Map<String, dynamic>.from(raw);
          if (_asInt(_field(row, ['maKhach', 'MaKhach'])) != widget.maKhach) continue;
          final id = _asInt(_field(row, ['maDatPhong', 'MaDatPhong']));
          final status = _field(row, ['trangThai', 'TrangThai'])?.toString();
          if (id != null && status != null) updated[id] = status;
        }
        if (mounted) _cancelStatuses.addAll(updated);
      } catch (_) {}

      if (!mounted) return;
      setState(() { _bookings = bookings; _roomNames = names; _loading = false; });
    } catch (e) {
      if (!mounted) return;
      setState(() { _error = e.toString(); _loading = false; });
    }
  }

  void _toast(String message, {bool success = true}) {
    if (!mounted) return;
    final messenger = ScaffoldMessenger.of(context);
    messenger.hideCurrentSnackBar();
    messenger.showSnackBar(SnackBar(
      behavior: SnackBarBehavior.floating,
      backgroundColor: success ? const Color(0xFF15803D) : const Color(0xFFB91C1C),
      content: Text(message),
    ));
  }

  Future<void> _showCancelDialog(int bookingId) async {
    if (_sendingCancel || _pendingCancelIds.contains(bookingId)) return;
    final reason = await showDialog<String>(
      context: context,
      builder: (_) => _CancelReasonDialog(bookingId: bookingId),
    );
    if (!mounted || reason == null || reason.trim().isEmpty) return;
    setState(() => _sendingCancel = true);
    try {
      final result = await ApiService.guiYeuCauHuy(maDatPhong: bookingId, lyDo: reason.trim());
      if (!mounted) return;
      if (result['success'] == true) {
        setState(() {
          _pendingCancelIds.add(bookingId);
          _cancelStatuses[bookingId] = 'Chờ duyệt';
        });
        _toast(result['message']?.toString() ?? 'Đã gửi yêu cầu, chờ chủ trọ duyệt.');
        // Không tự gửi lại yêu cầu. Tải lại để lấy trạng thái từ máy chủ.
        await _loadBookings();
      } else {
        final message = result['message']?.toString() ?? 'Không gửi được yêu cầu.';
        if (message.toLowerCase().contains('đang chờ') ||
            message.toLowerCase().contains('đã gửi') ||
            message.toLowerCase().contains('đã tồn tại')) {
          setState(() { _pendingCancelIds.add(bookingId); _cancelStatuses[bookingId] = 'Chờ duyệt'; });
          _toast('Yêu cầu hủy đã tồn tại và đang chờ chủ trọ xử lý.');
        } else {
          _toast(message, success: false);
        }
      }
    } catch (e) {
      _toast('Không gửi được yêu cầu: $e', success: false);
    } finally {
      if (mounted) setState(() => _sendingCancel = false);
    }
  }

  Color _statusColor(String status) {
    if (status == 'Đã hủy') return const Color(0xFFDC2626);
    if (status == 'Đã đặt cọc') return const Color(0xFFEA580C);
    if (status == 'Đã nhận phòng') return const Color(0xFF15803D);
    return primary;
  }

  Widget _detail(IconData icon, String label, String value) => Padding(
    padding: const EdgeInsets.only(top: 12),
    child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Icon(icon, size: 18, color: primary),
      const SizedBox(width: 9),
      Expanded(flex: 3, child: Text(label, style: const TextStyle(color: muted, fontSize: 13))),
      Expanded(flex: 4, child: Text(value, textAlign: TextAlign.end,
        style: const TextStyle(color: ink, fontWeight: FontWeight.w600, fontSize: 13))),
    ]),
  );

  Widget _card(Map<String, dynamic> booking) {
    final id = _asInt(_field(booking, ['maDatPhong', 'MaDatPhong', 'id']));
    final roomId = _asInt(_field(booking, ['maPhong', 'MaPhong', 'roomId']));
    final room = _field(booking, ['tenPhong', 'TenPhong', 'roomNumber'])?.toString()
        ?? _roomNames[roomId] ?? 'Phòng #${roomId ?? '?'}';
    final status = _field(booking, ['trangThai', 'TrangThai', 'status'])?.toString() ?? 'Chưa rõ';
    final cancelStatus = id == null ? null : _cancelStatuses[id];
    final pending = id != null && (_pendingCancelIds.contains(id) || cancelStatus == 'Chờ duyệt' || cancelStatus == 'Chờ xử lý');
    final rejected = cancelStatus == 'Từ chối' || cancelStatus == 'Đã từ chối';
    final cancelled = status == 'Đã hủy' || cancelStatus == 'Đã duyệt';
    final color = _statusColor(status);

    return Container(
      margin: const EdgeInsets.only(bottom: 14),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(18),
        border: Border.all(color: const Color(0xFFE2E8F0))),
      child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Row(children: [
          Container(padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(color: const Color(0xFFEEF2FF), borderRadius: BorderRadius.circular(12)),
            child: const Icon(Icons.meeting_room_outlined, color: primary)),
          const SizedBox(width: 11),
          Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(room, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: ink),
              maxLines: 2, overflow: TextOverflow.ellipsis),
            Text('Phiếu đặt #${id ?? '?'}', style: const TextStyle(color: muted, fontSize: 12)),
          ])),
          const SizedBox(width: 6),
          Flexible(child: Container(padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 6),
            decoration: BoxDecoration(color: color.withValues(alpha: .10), borderRadius: BorderRadius.circular(20)),
            child: Text(status, textAlign: TextAlign.center,
              style: TextStyle(color: color, fontSize: 11, fontWeight: FontWeight.w700)))),
        ]),
        const Divider(height: 24),
        _detail(Icons.payments_outlined, 'Tiền cọc', _money(_field(booking, ['tienCoc', 'TienCoc', 'depositAmount']))),
        _detail(Icons.event_outlined, 'Ngày đặt', _date(_field(booking, ['ngayDat', 'NgayDat', 'bookingDate']))),
        _detail(Icons.event_available_outlined, 'Dự kiến nhận', _date(_field(booking, ['ngayDuKienNhan', 'NgayDuKienNhan', 'expectedCheckIn', 'expectedDate']))),
        if (status == 'Đã đặt cọc' && pending) ...[
          const SizedBox(height: 16),
          Container(padding: const EdgeInsets.all(13),
            decoration: BoxDecoration(color: const Color(0xFFF0FDF4),
              border: Border.all(color: const Color(0xFF86EFAC)), borderRadius: BorderRadius.circular(12)),
            child: const Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Icon(Icons.schedule_rounded, color: Color(0xFF15803D)),
              SizedBox(width: 9),
              Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text('Đã gửi yêu cầu hủy phòng', style: TextStyle(color: Color(0xFF166534), fontWeight: FontWeight.bold)),
                SizedBox(height: 4),
                Text('Đang chờ chủ trọ duyệt. Bạn không cần gửi lại.',
                  style: TextStyle(color: Color(0xFF166534), fontSize: 12)),
              ])),
            ])),
        ] else if (status == 'Đã đặt cọc' && !cancelled) ...[
          if (rejected) const Padding(padding: EdgeInsets.only(top: 12),
            child: Text('Yêu cầu trước đã bị từ chối.', style: TextStyle(color: Color(0xFFB45309)))),
          const SizedBox(height: 16),
          OutlinedButton.icon(
            onPressed: id == null || _sendingCancel ? null : () => _showCancelDialog(id),
            icon: const Icon(Icons.cancel_outlined), label: const Text('Yêu cầu hủy phòng'),
            style: OutlinedButton.styleFrom(foregroundColor: const Color(0xFFDC2626),
              side: const BorderSide(color: Color(0xFFFCA5A5)),
              padding: const EdgeInsets.symmetric(vertical: 12)),
          ),
        ],
      ]),
    );
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: const Color(0xFFF5F7FC),
    appBar: AppBar(backgroundColor: Colors.white, foregroundColor: ink, centerTitle: true,
      title: const Text('Đặt phòng của tôi', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
      actions: [IconButton(onPressed: _loading || _sendingCancel ? null : _loadBookings,
        icon: const Icon(Icons.refresh_rounded))]),
    body: _loading
      ? const Center(child: CircularProgressIndicator(color: primary))
      : _error != null
        ? Center(child: Padding(padding: const EdgeInsets.all(24),
            child: Column(mainAxisSize: MainAxisSize.min, children: [
              const Icon(Icons.wifi_off_rounded, color: muted, size: 48),
              const SizedBox(height: 10),
              const Text('Không tải được danh sách đặt phòng', textAlign: TextAlign.center),
              const SizedBox(height: 8),
              Text(_error!, textAlign: TextAlign.center, style: const TextStyle(color: muted)),
              TextButton(onPressed: _loadBookings, child: const Text('Thử lại')),
            ])))
        : RefreshIndicator(onRefresh: _loadBookings,
            child: ListView.builder(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.all(16),
              itemCount: _bookings.isEmpty ? 1 : _bookings.length + 1,
              itemBuilder: (_, index) {
                if (_bookings.isEmpty) return const Padding(
                  padding: EdgeInsets.only(top: 140),
                  child: Center(child: Text('Chưa có phiếu đặt phòng. Kéo xuống để tải lại.')));
                if (index == 0) return Padding(padding: const EdgeInsets.only(bottom: 14),
                  child: Text('Khách hàng #${widget.maKhach} • ${_bookings.length} phiếu đặt',
                    style: const TextStyle(fontWeight: FontWeight.bold, color: ink)));
                return _card(_bookings[index - 1]);
              },
            )),
  );
}

// Dialog tự sở hữu controller, không dispose controller khi animation đóng còn chạy.
class _CancelReasonDialog extends StatefulWidget {
  final int bookingId;
  const _CancelReasonDialog({required this.bookingId});
  @override
  State<_CancelReasonDialog> createState() => _CancelReasonDialogState();
}

class _CancelReasonDialogState extends State<_CancelReasonDialog> {
  final _controller = TextEditingController();
  String? _error;
  @override
  void dispose() { _controller.dispose(); super.dispose(); }

  @override
  Widget build(BuildContext context) => AlertDialog(
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
    title: const Text('Yêu cầu hủy đặt phòng', style: TextStyle(fontSize: 19, fontWeight: FontWeight.bold)),
    content: SingleChildScrollView(child: Column(mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text('Phiếu đặt #${widget.bookingId}'),
        const SizedBox(height: 12),
        TextField(controller: _controller, maxLines: 3, minLines: 2, maxLength: 500,
          decoration: InputDecoration(labelText: 'Lý do hủy', border: const OutlineInputBorder(),
            errorText: _error)),
        const Text('Chủ trọ sẽ xem xét và duyệt yêu cầu.', style: TextStyle(fontSize: 12)),
      ])),
    actions: [
      TextButton(onPressed: () => Navigator.pop(context), child: const Text('Đóng')),
      FilledButton(style: FilledButton.styleFrom(backgroundColor: const Color(0xFF4F46E5)),
        onPressed: () {
          final reason = _controller.text.trim();
          if (reason.isEmpty) { setState(() => _error = 'Vui lòng nhập lý do hủy'); return; }
          Navigator.pop(context, reason);
        }, child: const Text('Gửi yêu cầu')),
    ],
  );
}
