import 'package:flutter/material.dart';
import '../services/api_service.dart';

class CustomerHistoryScreen extends StatefulWidget {
  final int maKhach;

  const CustomerHistoryScreen({super.key, required this.maKhach});

  @override
  State<CustomerHistoryScreen> createState() => _CustomerHistoryScreenState();
}

class _CustomerHistoryScreenState extends State<CustomerHistoryScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabController;
  List<Map<String, dynamic>> _invoices = [];
  List<Map<String, dynamic>> _meterReadings = [];
  List<Map<String, dynamic>> _serviceHistory = [];
  String? _paymentError;
  String? _meterError;
  String? _serviceError;
  bool _loadingPayments = true;
  bool _loadingMeters = true;
  bool _loadingServices = true;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 3, vsync: this);
    _loadPayments();
    _loadMeterReadings();
    _loadServiceHistory();
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  Future<void> _loadPayments() async {
    if (mounted) {
      setState(() {
        _loadingPayments = true;
        _paymentError = null;
      });
    }
    try {
      final data = await ApiService.getInvoicesForCustomer(widget.maKhach);
      if (!mounted) return;
      setState(() {
        _invoices = _maps(data);
        _loadingPayments = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _paymentError = error.toString();
        _loadingPayments = false;
      });
    }
  }

  Future<void> _loadMeterReadings() async {
    if (mounted) {
      setState(() {
        _loadingMeters = true;
        _meterError = null;
      });
    }
    try {
      final data = await ApiService.getCustomerMeterReadings(widget.maKhach);
      if (!mounted) return;
      setState(() {
        _meterReadings = _maps(data);
        _loadingMeters = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _meterError = error.toString();
        _loadingMeters = false;
      });
    }
  }

  Future<void> _loadServiceHistory() async {
    if (mounted) {
      setState(() {
        _loadingServices = true;
        _serviceError = null;
      });
    }
    try {
      final data = await ApiService.getCustomerServiceHistory(widget.maKhach);
      if (!mounted) return;
      setState(() {
        _serviceHistory = _maps(data);
        _loadingServices = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _serviceError = error.toString();
        _loadingServices = false;
      });
    }
  }

  Future<void> _refreshAll() async {
    await Future.wait([
      _loadPayments(),
      _loadMeterReadings(),
      _loadServiceHistory(),
    ]);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: const Text(
          'Lịch sử sử dụng & thanh toán',
          style: TextStyle(
            fontWeight: FontWeight.bold,
            fontSize: 17,
            color: Color(0xFF172554),
          ),
        ),
        backgroundColor: Colors.white,
        foregroundColor: const Color(0xFF172554),
        elevation: 0,
        bottom: TabBar(
          controller: _tabController,
          isScrollable: true,
          indicatorColor: const Color(0xFF4F46E5),
          labelColor: const Color(0xFF4F46E5),
          unselectedLabelColor: const Color(0xFF64748B),
          tabs: const [
            Tab(icon: Icon(Icons.payments_outlined), text: 'Thanh toán'),
            Tab(icon: Icon(Icons.electric_meter_outlined), text: 'Điện/nước'),
            Tab(icon: Icon(Icons.miscellaneous_services), text: 'Dịch vụ'),
          ],
        ),
        actions: [
          IconButton(
            tooltip: 'Tải lại toàn bộ lịch sử',
            onPressed: _refreshAll,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: TabBarView(
        controller: _tabController,
        children: [
          _buildPaymentHistory(),
          _buildMeterHistory(),
          _buildServiceHistory(),
        ],
      ),
    );
  }

  Widget _buildPaymentHistory() {
    if (_loadingPayments) return _loadingView();
    if (_paymentError != null) {
      return _errorView(_paymentError!, _loadPayments);
    }
    if (_invoices.isEmpty) {
      return _emptyView(
        icon: Icons.receipt_long_outlined,
        message: 'Chưa có hóa đơn hoặc lịch sử thanh toán.',
        onRefresh: _loadPayments,
      );
    }
    return RefreshIndicator(
      onRefresh: _loadPayments,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          _infoBanner(
            'Đây là tổng số tiền đã thu theo từng hóa đơn. API hiện chưa có '
            'ngày giờ, phương thức hoặc nội dung của từng lần thanh toán.',
          ),
          const SizedBox(height: 12),
          _paymentSummary(),
          const SizedBox(height: 12),
          ..._invoices.map(_paymentCard),
        ],
      ),
    );
  }

  Widget _paymentSummary() {
    var totalPaid = 0.0;
    var totalRemaining = 0.0;
    for (final invoice in _invoices) {
      final total = _numberValue(invoice, 'tongTien', 'TongTien') ?? 0;
      final paid = _numberValue(invoice, 'daThanhToan', 'DaThanhToan') ?? 0;
      final remaining =
          _numberValue(invoice, 'conLai', 'ConLai') ??
          (total - paid).clamp(0, double.infinity).toDouble();
      totalPaid += paid;
      totalRemaining += remaining;
    }

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFE2E8F0)),
      ),
      child: Row(
        children: [
          Expanded(
            child: _paymentSummaryValue(
              label: 'Đã thanh toán',
              amount: totalPaid,
              color: const Color(0xFF15803D),
              icon: Icons.check_circle_outline,
            ),
          ),
          Container(width: 1, height: 48, color: const Color(0xFFE2E8F0)),
          Expanded(
            child: _paymentSummaryValue(
              label: 'Còn phải trả',
              amount: totalRemaining,
              color: const Color(0xFFB45309),
              icon: Icons.pending_outlined,
            ),
          ),
        ],
      ),
    );
  }

  Widget _paymentSummaryValue({
    required String label,
    required double amount,
    required Color color,
    required IconData icon,
  }) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, color: color, size: 17),
              const SizedBox(width: 5),
              Expanded(
                child: Text(
                  label,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: Color(0xFF64748B),
                    fontSize: 12,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          FittedBox(
            fit: BoxFit.scaleDown,
            alignment: Alignment.centerLeft,
            child: Text(
              '${_money(amount)} đ',
              style: TextStyle(
                color: color,
                fontSize: 18,
                fontWeight: FontWeight.bold,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _paymentCard(Map<String, dynamic> invoice) {
    final id = _intValue(invoice, 'maHoaDon', 'MaHoaDon');
    final month = _intValue(invoice, 'thang', 'Thang') ?? 0;
    final year = _intValue(invoice, 'nam', 'Nam') ?? 0;
    final total = _numberValue(invoice, 'tongTien', 'TongTien') ?? 0;
    final paid = _numberValue(invoice, 'daThanhToan', 'DaThanhToan') ?? 0;
    final remaining =
        _numberValue(invoice, 'conLai', 'ConLai') ??
        (total - paid).clamp(0, double.infinity).toDouble();
    final status =
        _stringValue(invoice, 'trangThai', 'TrangThai') ??
        (remaining <= 0
            ? 'Đã thanh toán'
            : paid > 0
            ? 'Thanh toán một phần'
            : 'Chưa thanh toán');
    final isPaid = remaining <= 0;
    final statusColor = isPaid
        ? const Color(0xFF15803D)
        : Colors.orange.shade800;
    final paymentProgress = total <= 0
        ? 0.0
        : (paid / total).clamp(0, 1).toDouble();

    return Card(
      elevation: 0,
      margin: const EdgeInsets.only(bottom: 12),
      color: Colors.white,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: const BorderSide(color: Color(0xFFE2E8F0)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Container(
                  width: 40,
                  height: 40,
                  decoration: BoxDecoration(
                    color: const Color(0xFFEFF6FF),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: const Icon(
                    Icons.receipt_long_outlined,
                    color: Color(0xFF4F46E5),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Kỳ hóa đơn $month/$year',
                        style: const TextStyle(
                          color: Color(0xFF172554),
                          fontWeight: FontWeight.bold,
                          fontSize: 15,
                        ),
                      ),
                      if (id != null)
                        Text(
                          'Mã hóa đơn #$id',
                          style: const TextStyle(
                            color: Color(0xFF64748B),
                            fontSize: 12,
                          ),
                        ),
                    ],
                  ),
                ),
                _statusPill(status, statusColor),
              ],
            ),
            const SizedBox(height: 18),
            Text(
              'ĐÃ THANH TOÁN',
              style: TextStyle(
                color: Colors.blueGrey.shade600,
                fontSize: 11,
                fontWeight: FontWeight.w600,
                letterSpacing: 0.4,
              ),
            ),
            const SizedBox(height: 4),
            Row(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Expanded(
                  child: Text(
                    '${_money(paid)} đ',
                    style: TextStyle(
                      color: isPaid
                          ? const Color(0xFF15803D)
                          : const Color(0xFF172554),
                      fontSize: 24,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                Icon(
                  isPaid ? Icons.verified_rounded : Icons.receipt_outlined,
                  color: statusColor,
                  size: 21,
                ),
              ],
            ),
            const SizedBox(height: 12),
            ClipRRect(
              borderRadius: BorderRadius.circular(8),
              child: LinearProgressIndicator(
                value: paymentProgress,
                minHeight: 7,
                backgroundColor: const Color(0xFFE2E8F0),
                valueColor: AlwaysStoppedAnimation<Color>(statusColor),
              ),
            ),
            const SizedBox(height: 14),
            const Divider(height: 1),
            const SizedBox(height: 12),
            _infoRow('Tổng hóa đơn', '${_money(total)} đ'),
            _infoRow(
              'Còn phải trả',
              '${_money(remaining)} đ',
              valueColor: remaining > 0
                  ? Colors.deepOrange
                  : const Color(0xFF15803D),
            ),
            _infoRow(
              'Hạn thanh toán',
              _date(invoice['hanThanhToan'] ?? invoice['HanThanhToan']),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildMeterHistory() {
    if (_loadingMeters) return _loadingView();
    if (_meterError != null) {
      return _errorView(_meterError!, _loadMeterReadings);
    }
    if (_meterReadings.isEmpty) {
      return _emptyView(
        icon: Icons.electric_meter_outlined,
        message: 'Chưa có chỉ số điện/nước cho phòng của bạn.',
        onRefresh: _loadMeterReadings,
      );
    }
    return RefreshIndicator(
      onRefresh: _loadMeterReadings,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          _infoBanner(
            'Mức tiêu thụ được tính từ chỉ số xác nhận (nếu có), nếu không sẽ '
            'dùng chỉ số mới trừ chỉ số cũ.',
          ),
          const SizedBox(height: 12),
          ..._meterReadings.map(_meterCard),
        ],
      ),
    );
  }

  Widget _meterCard(Map<String, dynamic> reading) {
    final type = _stringValue(reading, 'loaiChiSo', 'LoaiChiSo') ?? 'Điện/nước';
    final room =
        _stringValue(reading, 'tenPhong', 'TenPhong') ??
        'Phòng ${_value(reading, 'maPhong', 'MaPhong') ?? '—'}';
    final month = _intValue(reading, 'thang', 'Thang') ?? 0;
    final year = _intValue(reading, 'nam', 'Nam') ?? 0;
    final oldValue = _numberValue(reading, 'chiSoCu', 'ChiSoCu') ?? 0;
    final newValue =
        _numberValue(reading, 'chiSoXacNhan', 'ChiSoXacNhan') ??
        _numberValue(reading, 'chiSoMoi', 'ChiSoMoi') ??
        oldValue;
    final usage = (newValue - oldValue).clamp(0, double.infinity).toDouble();
    final unit = _meterUnit(type);

    return _historyCard(
      icon: type.toLowerCase().contains('điện')
          ? Icons.bolt_outlined
          : Icons.water_drop_outlined,
      title: '$type · Tháng $month/$year',
      subtitle: room,
      children: [
        _infoRow('Chỉ số cũ', '${_number(oldValue)} $unit'),
        _infoRow('Chỉ số chốt', '${_number(newValue)} $unit'),
        _infoRow(
          'Mức sử dụng',
          '${_number(usage)} $unit',
          valueColor: const Color(0xFF4F46E5),
        ),
        _infoRow('Ngày ghi', _date(_value(reading, 'ngayGhi', 'NgayGhi'))),
      ],
    );
  }

  Widget _buildServiceHistory() {
    if (_loadingServices) return _loadingView();
    if (_serviceError != null) {
      return _errorView(_serviceError!, _loadServiceHistory);
    }
    if (_serviceHistory.isEmpty) {
      return _emptyView(
        icon: Icons.miscellaneous_services_outlined,
        message: 'Chưa có lịch sử đăng ký dịch vụ.',
        onRefresh: _loadServiceHistory,
      );
    }
    return RefreshIndicator(
      onRefresh: _loadServiceHistory,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          _infoBanner(
            'Danh sách gồm các dịch vụ đã đăng ký theo hợp đồng, bao gồm dịch '
            'vụ đang sử dụng và đã kết thúc.',
          ),
          const SizedBox(height: 12),
          ..._serviceHistory.map(_serviceCard),
        ],
      ),
    );
  }

  Widget _serviceCard(Map<String, dynamic> registration) {
    final name =
        _stringValue(registration, 'tenDichVu', 'TenDichVu') ??
        'Dịch vụ #${_value(registration, 'maDichVu', 'MaDichVu') ?? '—'}';
    final status =
        _stringValue(registration, 'trangThai', 'TrangThai') ?? 'Chưa cập nhật';
    final isActive =
        status.toLowerCase().contains('đang sử dụng') ||
        status.toLowerCase().contains('dang su dung');
    final roomId = _value(registration, 'maPhong', 'MaPhong');
    final rate = _numberValue(registration, 'donGiaApDung', 'DonGiaApDung');

    return _historyCard(
      icon: Icons.miscellaneous_services_outlined,
      title: name,
      subtitle: roomId == null ? null : 'Phòng $roomId',
      trailing: _statusPill(status, isActive ? Colors.green : Colors.blueGrey),
      children: [
        _infoRow(
          'Ngày bắt đầu',
          _date(_value(registration, 'ngayBatDau', 'NgayBatDau')),
        ),
        _infoRow(
          'Ngày kết thúc',
          _date(_value(registration, 'ngayKetThuc', 'NgayKetThuc')),
        ),
        if (rate != null) _infoRow('Đơn giá áp dụng', '${_money(rate)} đ'),
      ],
    );
  }

  Widget _historyCard({
    required IconData icon,
    required String title,
    String? subtitle,
    Widget? trailing,
    required List<Widget> children,
  }) {
    return Card(
      elevation: 0,
      margin: const EdgeInsets.only(bottom: 12),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: const BorderSide(color: Color(0xFFE2E8F0)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(icon, color: const Color(0xFF4F46E5)),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        title,
                        style: const TextStyle(
                          color: Color(0xFF172554),
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      if (subtitle != null)
                        Text(
                          subtitle,
                          style: const TextStyle(
                            color: Color(0xFF64748B),
                            fontSize: 12,
                          ),
                        ),
                    ],
                  ),
                ),
                ?trailing,
              ],
            ),
            const Divider(height: 24),
            ...children,
          ],
        ),
      ),
    );
  }

  Widget _infoBanner(String text) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: const Color(0xFFEFF6FF),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(Icons.info_outline, color: Color(0xFF4F46E5), size: 18),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              text,
              style: const TextStyle(color: Color(0xFF334155), fontSize: 12),
            ),
          ),
        ],
      ),
    );
  }

  Widget _infoRow(
    String label,
    String value, {
    Color valueColor = const Color(0xFF172554),
  }) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        children: [
          Expanded(
            child: Text(
              label,
              style: const TextStyle(color: Color(0xFF64748B), fontSize: 13),
            ),
          ),
          Text(
            value,
            textAlign: TextAlign.end,
            style: TextStyle(
              color: valueColor,
              fontWeight: FontWeight.w600,
              fontSize: 13,
            ),
          ),
        ],
      ),
    );
  }

  Widget _statusPill(String status, Color color) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        status,
        style: TextStyle(
          color: color,
          fontWeight: FontWeight.bold,
          fontSize: 10,
        ),
      ),
    );
  }

  Widget _loadingView() =>
      const Center(child: CircularProgressIndicator(color: Color(0xFF4F46E5)));

  Widget _errorView(String error, Future<void> Function() retry) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, color: Colors.redAccent, size: 44),
            const SizedBox(height: 12),
            Text(
              'Không tải được dữ liệu.\n$error',
              textAlign: TextAlign.center,
              style: const TextStyle(color: Color(0xFF64748B)),
            ),
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: retry,
              icon: const Icon(Icons.refresh),
              label: const Text('Thử lại'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _emptyView({
    required IconData icon,
    required String message,
    required Future<void> Function() onRefresh,
  }) {
    return RefreshIndicator(
      onRefresh: onRefresh,
      child: ListView(
        children: [
          SizedBox(
            height: MediaQuery.sizeOf(context).height * 0.55,
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Icon(icon, size: 56, color: Colors.blueGrey.shade200),
                const SizedBox(height: 12),
                Text(
                  message,
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: Color(0xFF64748B)),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

List<Map<String, dynamic>> _maps(List<dynamic> values) => values
    .whereType<Map>()
    .map((value) => Map<String, dynamic>.from(value))
    .toList();

dynamic _value(Map<String, dynamic> map, String camelKey, String pascalKey) =>
    map[camelKey] ?? map[pascalKey];

int? _intValue(Map<String, dynamic> map, String camelKey, String pascalKey) =>
    int.tryParse((_value(map, camelKey, pascalKey) ?? '').toString());

double? _numberValue(
  Map<String, dynamic> map,
  String camelKey,
  String pascalKey,
) {
  final value = _value(map, camelKey, pascalKey);
  return value is num
      ? value.toDouble()
      : double.tryParse(value?.toString() ?? '');
}

String? _stringValue(
  Map<String, dynamic> map,
  String camelKey,
  String pascalKey,
) => _value(map, camelKey, pascalKey)?.toString();

String _date(dynamic value) {
  if (value == null) return '—';
  final parsed = DateTime.tryParse(value.toString());
  if (parsed == null) return value.toString();
  return '${parsed.day.toString().padLeft(2, '0')}/'
      '${parsed.month.toString().padLeft(2, '0')}/${parsed.year}';
}

String _meterUnit(String type) =>
    type.toLowerCase().contains('điện') ? 'kWh' : 'm³';

String _number(double value) => value == value.roundToDouble()
    ? value.toInt().toString()
    : value.toStringAsFixed(2);

String _money(double value) => value.round().toString().replaceAllMapped(
  RegExp(r'(\d)(?=(\d{3})+(?!\d))'),
  (match) => '${match[1]},',
);
