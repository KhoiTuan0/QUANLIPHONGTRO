import 'package:flutter/material.dart';
import '../services/api_service.dart';

/// Màn hình KHÁCH THUÊ gửi yêu cầu trả phòng.
/// Không dùng CheckoutRequestManagementScreen (màn hình của Admin).
class CheckoutRequestScreen extends StatefulWidget {
  final int maKhach;
  const CheckoutRequestScreen({super.key, required this.maKhach});

  @override
  State<CheckoutRequestScreen> createState() => _CheckoutRequestScreenState();
}

class _CheckoutRequestScreenState extends State<CheckoutRequestScreen> {
  final _formKey = GlobalKey<FormState>();
  final _reasonController = TextEditingController();
  final _bankController = TextEditingController();
  late Future<List<dynamic>> _contractsFuture;
  int? _selectedContractId;
  DateTime? _returnDate;
  bool _sending = false;
  bool _submitted = false;

  @override
  void initState() {
    super.initState();
    _contractsFuture = ApiService.getContractsByCustomer(widget.maKhach);
  }

  @override
  void dispose() {
    _reasonController.dispose();
    _bankController.dispose();
    super.dispose();
  }

  String _get(Map<String, dynamic> row, List<String> keys, [String fallback = '']) {
    for (final key in keys) {
      final value = row[key];
      if (value != null && value.toString().trim().isNotEmpty) return value.toString();
    }
    return fallback;
  }

  int? _contractId(Map<String, dynamic> row) =>
      int.tryParse(_get(row, ['maHopDong', 'MaHopDong', 'id', 'Id']));

  String _contractLabel(Map<String, dynamic> row) {
    final id = _contractId(row);
    final room = _get(row, ['tenPhong', 'TenPhong', 'maPhong', 'MaPhong']);
    final status = _get(row, ['trangThai', 'TrangThai']);
    return 'Hợp đồng #${id ?? '?'}${room.isEmpty ? '' : ' • Phòng $room'}${status.isEmpty ? '' : ' ($status)'}';
  }

  String _dateString(DateTime date) =>
      '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';

  String _displayDate(DateTime date) =>
      '${date.day.toString().padLeft(2, '0')}/${date.month.toString().padLeft(2, '0')}/${date.year}';

  Future<void> _pickDate() async {
    final today = DateUtils.dateOnly(DateTime.now());
    final picked = await showDatePicker(
      context: context,
      initialDate: _returnDate ?? today,
      firstDate: today,
      lastDate: DateTime(today.year + 3, 12, 31),
      helpText: 'Chọn ngày dự kiến trả phòng',
    );
    if (picked != null && mounted) setState(() => _returnDate = picked);
  }

  Future<void> _submit() async {
    if (_sending || _submitted || !_formKey.currentState!.validate()) return;
    if (_selectedContractId == null) {
      _notify('Vui lòng chọn hợp đồng cần trả phòng.', error: true);
      return;
    }
    if (_returnDate == null) {
      _notify('Vui lòng chọn ngày dự kiến trả phòng.', error: true);
      return;
    }

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Xác nhận gửi yêu cầu'),
        content: const Text('Yêu cầu sẽ được gửi đến chủ trọ để xem xét. Việc gửi yêu cầu không tự động thanh lý hợp đồng.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(dialogContext, false), child: const Text('Hủy')),
          FilledButton(onPressed: () => Navigator.pop(dialogContext, true), child: const Text('Gửi yêu cầu')),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;

    setState(() => _sending = true);
    try {
      final result = await ApiService.createCheckoutRequest(
        contractId: _selectedContractId!,
        customerId: widget.maKhach,
        reason: _reasonController.text.trim(),
        expectedReturnDate: _dateString(_returnDate!),
        bankInfo: _bankController.text.trim(),
      );
      if (!mounted) return;
      final ok = result['success'] == true;
      if (ok) setState(() => _submitted = true);
      _notify(result['message']?.toString() ?? (ok ? 'Đã gửi yêu cầu.' : 'Không thể gửi yêu cầu.'), error: !ok);
    } catch (error) {
      if (mounted) _notify('Có lỗi khi gửi yêu cầu: $error', error: true);
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  void _notify(String message, {bool error = false}) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), backgroundColor: error ? Colors.red : Colors.green),
    );
  }

  @override
  Widget build(BuildContext context) {
    const purple = Color(0xFF4F46E5);
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: const Text('Yêu cầu trả phòng'),
        backgroundColor: Colors.white,
        actions: [
          IconButton(
            tooltip: 'Tải lại hợp đồng',
            onPressed: _sending ? null : () => setState(() {
              _contractsFuture = ApiService.getContractsByCustomer(widget.maKhach);
              _selectedContractId = null;
            }),
            icon: const Icon(Icons.refresh_rounded),
          ),
        ],
      ),
      body: _submitted
          ? Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.mark_email_read_rounded, size: 70, color: Colors.green),
                    const SizedBox(height: 14),
                    const Text('Đã gửi yêu cầu trả phòng', textAlign: TextAlign.center,
                        style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
                    const SizedBox(height: 8),
                    const Text('Vui lòng chờ chủ trọ xem xét. Hợp đồng của bạn chưa được thanh lý.', textAlign: TextAlign.center),
                    const SizedBox(height: 18),
                    FilledButton(onPressed: () => Navigator.maybePop(context), child: const Text('Quay lại')),
                  ],
                ),
              ),
            )
          : FutureBuilder<List<dynamic>>(
              future: _contractsFuture,
              builder: (context, snapshot) {
                if (snapshot.connectionState != ConnectionState.done) {
                  return const Center(child: CircularProgressIndicator());
                }
                if (snapshot.hasError) {
                  return Center(child: Padding(
                    padding: const EdgeInsets.all(20),
                    child: Text('Không tải được hợp đồng: ${snapshot.error}', textAlign: TextAlign.center),
                  ));
                }
                final contracts = (snapshot.data ?? [])
                    .whereType<Map>()
                    .map((e) => Map<String, dynamic>.from(e))
                    .where((e) => _contractId(e) != null)
                    .where((e) {
                      final status = _get(e, ['trangThai', 'TrangThai']).toLowerCase();
                      return !status.contains('thanh lý') && !status.contains('thanh ly') &&
                          !status.contains('kết thúc') && !status.contains('ket thuc');
                    })
                    .toList();
                if (contracts.isEmpty) {
                  return const Center(child: Padding(
                    padding: EdgeInsets.all(24),
                    child: Text('Bạn chưa có hợp đồng có thể gửi yêu cầu trả phòng.', textAlign: TextAlign.center),
                  ));
                }
                final ids = contracts.map((e) => _contractId(e)!).toSet();
                final selected = ids.contains(_selectedContractId) ? _selectedContractId : null;
                return SingleChildScrollView(
                  padding: const EdgeInsets.all(16),
                  child: Form(
                    key: _formKey,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Card(
                          color: Colors.white,
                          child: Padding(
                            padding: const EdgeInsets.all(18),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                const Text('Thông tin yêu cầu', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
                                const SizedBox(height: 8),
                                const Text('Chủ trọ sẽ kiểm tra yêu cầu trước khi thực hiện quyết toán và thanh lý hợp đồng.'),
                                const SizedBox(height: 20),
                                DropdownButtonFormField<int>(
                                  value: selected,
                                  isExpanded: true,
                                  decoration: const InputDecoration(
                                    labelText: 'Hợp đồng cần trả phòng *',
                                    border: OutlineInputBorder(),
                                    prefixIcon: Icon(Icons.description_outlined),
                                  ),
                                  items: contracts.map((row) => DropdownMenuItem<int>(
                                    value: _contractId(row),
                                    child: Text(_contractLabel(row), overflow: TextOverflow.ellipsis),
                                  )).toList(),
                                  onChanged: _sending ? null : (value) => setState(() => _selectedContractId = value),
                                  validator: (value) => value == null ? 'Vui lòng chọn hợp đồng' : null,
                                ),
                                const SizedBox(height: 16),
                                OutlinedButton.icon(
                                  onPressed: _sending ? null : _pickDate,
                                  icon: const Icon(Icons.event_rounded),
                                  label: Text(_returnDate == null
                                      ? 'Chọn ngày dự kiến trả phòng *'
                                      : 'Ngày dự kiến: ${_displayDate(_returnDate!)}'),
                                  style: OutlinedButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 16)),
                                ),
                                const SizedBox(height: 16),
                                TextFormField(
                                  controller: _reasonController,
                                  maxLines: 3,
                                  maxLength: 500,
                                  decoration: const InputDecoration(
                                    labelText: 'Lý do trả phòng *',
                                    hintText: 'Ví dụ: Chuyển nơi làm việc...',
                                    alignLabelWithHint: true,
                                    border: OutlineInputBorder(),
                                  ),
                                  validator: (value) => (value ?? '').trim().isEmpty ? 'Vui lòng nhập lý do' : null,
                                ),
                                const SizedBox(height: 8),
                                TextFormField(
                                  controller: _bankController,
                                  maxLines: 2,
                                  decoration: const InputDecoration(
                                    labelText: 'Thông tin nhận lại tiền cọc (nếu có)',
                                    hintText: 'Ngân hàng, số tài khoản, chủ tài khoản...',
                                    alignLabelWithHint: true,
                                    border: OutlineInputBorder(),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                        const SizedBox(height: 16),
                        FilledButton.icon(
                          onPressed: _sending ? null : _submit,
                          style: FilledButton.styleFrom(backgroundColor: purple,
                              padding: const EdgeInsets.symmetric(vertical: 16)),
                          icon: _sending
                              ? const SizedBox(width: 18, height: 18,
                                  child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                              : const Icon(Icons.send_rounded),
                          label: Text(_sending ? 'Đang gửi...' : 'Gửi yêu cầu trả phòng'),
                        ),
                      ],
                    ),
                  ),
                );
              },
            ),
    );
  }
}
