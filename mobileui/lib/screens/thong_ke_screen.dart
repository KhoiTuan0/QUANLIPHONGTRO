import 'package:flutter/material.dart';
import '../services/api_service.dart';

class ThongKeScreen extends StatefulWidget {
  const ThongKeScreen({super.key});

  @override
  State<ThongKeScreen> createState() => _ThongKeScreenState();
}

class _ThongKeScreenState extends State<ThongKeScreen> {
  late Future<Map<String, dynamic>> _reportFuture;

  @override
  void initState() {
    super.initState();
    _reportFuture = _loadReport();
  }

  Future<Map<String, dynamic>> _loadReport() async {
    final source = await ApiService.getReportSourceData();
    final rooms = source['rooms']!.whereType<Map>().toList();
    final invoices = source['invoices']!.whereType<Map>().toList();

    final roomStatuses = rooms
        .map(
          (room) => _normalizeStatus(
            _value(room, ['trangThai', 'TrangThai', 'status', 'Status']),
          ),
        )
        .toList();
    final reportYear = DateTime.now().year;
    final invoicesThisYear = invoices.where(
      (invoice) => _number(invoice, ['nam', 'Nam', 'year']) == reportYear,
    );

    // Gom tiền đã thu theo tháng để tổng năm và biểu đồ dùng chung một nguồn số liệu.
    final monthlyRevenue = List<num>.filled(12, 0);
    for (final invoice in invoicesThisYear) {
      final month = _number(invoice, ['thang', 'Thang', 'month']).toInt();
      if (month >= 1 && month <= 12) {
        monthlyRevenue[month - 1] += _paidAmount(invoice);
      }
    }
    final yearlyRevenue = monthlyRevenue.fold<num>(
      0,
      (sum, value) => sum + value,
    );
    final outstandingDebt = invoicesThisYear.fold<num>(
      0,
      (total, invoice) =>
          total +
          _number(invoice, [
            'conLai',
            'ConLai',
            'remainingAmount',
          ]).clamp(0, double.infinity),
    );

    return {
      'totalRooms': rooms.length,
      'occupiedRooms': roomStatuses
          .where((status) => status == 'rented' || status == 'occupied')
          .length,
      'reservedRooms': roomStatuses
          .where((status) => status == 'reserved')
          .length,
      'availableRooms': roomStatuses
          .where((status) => status == 'available')
          .length,
      'reportYear': reportYear,
      'totalRevenue': yearlyRevenue,
      'monthlyRevenue': monthlyRevenue,
      'pendingDebt': outstandingDebt,
    };
  }

  num _paidAmount(Map invoice) {
    final paidAmount = _number(invoice, [
      'daThanhToan',
      'DaThanhToan',
      'paidAmount',
    ]);
    if (paidAmount > 0) return paidAmount;

    final status = _value(invoice, [
      'trangThai',
      'TrangThai',
      'status',
      'Status',
    ]).trim().toLowerCase();
    if (status == 'đã thanh toán' ||
        status == 'da thanh toan' ||
        status == 'paid' ||
        status == 'completed') {
      return _number(invoice, ['tongTien', 'TongTien', 'totalAmount']);
    }
    return 0;
  }

  String _normalizeStatus(String status) {
    final normalized = status.trim().toLowerCase();
    if (normalized.contains('thuê') || normalized == 'occupied') {
      return 'rented';
    }
    if (normalized.contains('cọc') ||
        normalized.contains('đặt') ||
        normalized == 'booked' ||
        normalized == 'reserved') {
      return 'reserved';
    }
    if (normalized.contains('trống') || normalized == 'available') {
      return 'available';
    }
    return normalized;
  }

  String _value(Map data, List<String> keys, [String fallback = '']) {
    for (final key in keys) {
      final value = data[key];
      if (value != null && value.toString().trim().isNotEmpty) {
        return value.toString();
      }
    }
    return fallback;
  }

  num _number(Map data, List<String> keys) {
    for (final key in keys) {
      final value = data[key];
      final parsed = num.tryParse(value?.toString() ?? '');
      if (parsed != null) return parsed;
    }
    return 0;
  }

  String _formatCount(num value) => value.toInt().toString();

  String _formatMoney(num value) {
    final digits = value.toInt().toString();
    return '${digits.replaceAllMapped(RegExp(r'(\d)(?=(\d{3})+(?!\d))'), (match) => '${match[1]},')} đ';
  }

  Future<void> _refresh() async {
    final future = _loadReport();
    setState(() {
      _reportFuture = future;
    });
    try {
      await future;
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Tải lại báo cáo thất bại: $error')),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: const Text(
          'Báo cáo thống kê',
          style: TextStyle(
            fontWeight: FontWeight.bold,
            fontSize: 18,
            color: Color(0xFF172554),
          ),
        ),
        backgroundColor: Colors.white,
        foregroundColor: const Color(0xFF172554),
        elevation: 0,
        actions: [
          IconButton(
            tooltip: 'Làm mới báo cáo',
            onPressed: _refresh,
            icon: const Icon(Icons.refresh_rounded),
          ),
        ],
      ),
      body: FutureBuilder<Map<String, dynamic>>(
        future: _reportFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting &&
              !snapshot.hasData) {
            return const Center(
              child: CircularProgressIndicator(color: Color(0xFF4F46E5)),
            );
          }
          if (snapshot.hasError || !snapshot.hasData) {
            return _ReportError(
              onRetry: _refresh,
              message:
                  'Không tải được báo cáo: ${snapshot.error ?? 'API không trả dữ liệu.'}',
            );
          }

          final data = snapshot.data!;
          final reportYear = _number(data, ['reportYear']).toInt();
          final totalRooms = _number(data, ['totalRooms', 'tongSoPhong']);
          final occupiedRooms = _number(data, [
            'occupiedRooms',
            'roomsOccupied',
            'soPhongDangThue',
          ]);
          final reservedRooms = _number(data, [
            'reservedRooms',
            'bookedRooms',
            'soPhongDatCoc',
          ]);
          final availableRooms = _number(data, [
            'availableRooms',
            'vacantRooms',
            'soPhongTrong',
          ]);
          final revenue = _number(data, ['totalRevenue', 'doanhThuThang']);
          final monthlyRevenue =
              (data['monthlyRevenue'] as List?)
                  ?.map((value) => num.tryParse(value.toString()) ?? 0)
                  .toList() ??
              List<num>.filled(12, 0);
          final debt = _number(data, [
            'pendingDebt',
            'unpaidAmount',
            'congNoChuaThu',
          ]);
          final occupancy = totalRooms <= 0
              ? 0.0
              : (occupiedRooms / totalRooms).clamp(0.0, 1.0);

          return RefreshIndicator(
            onRefresh: _refresh,
            color: const Color(0xFF4F46E5),
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 28),
              children: [
                Text(
                  'Tình hình phòng trọ',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: const Color(0xFF172554),
                  ),
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: _MetricCard(
                        title: 'Tổng số phòng',
                        value: _formatCount(totalRooms),
                        icon: Icons.meeting_room_outlined,
                        color: const Color(0xFF4F46E5),
                      ),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: _MetricCard(
                        title: 'Đang cho thuê',
                        value: _formatCount(occupiedRooms),
                        icon: Icons.people_outline_rounded,
                        color: const Color(0xFF15803D),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 10),
                Row(
                  children: [
                    Expanded(
                      child: _MetricCard(
                        title: 'Đang giữ chỗ',
                        value: _formatCount(reservedRooms),
                        icon: Icons.bookmark_border_rounded,
                        color: const Color(0xFFB45309),
                      ),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: _MetricCard(
                        title: 'Phòng trống',
                        value: _formatCount(availableRooms),
                        icon: Icons.door_front_door_outlined,
                        color: const Color(0xFF0891B2),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 14),
                _OccupancyCard(
                  occupancy: occupancy,
                  occupiedRooms: occupiedRooms,
                  totalRooms: totalRooms,
                ),
                const SizedBox(height: 22),
                Text(
                  'Tình hình tài chính',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: const Color(0xFF172554),
                  ),
                ),
                const SizedBox(height: 12),
                _FinancialCard(
                  title: 'Doanh thu năm $reportYear',
                  amount: _formatMoney(revenue),
                  caption: 'Tiền đã thu trên hóa đơn trong năm',
                  icon: Icons.trending_up_rounded,
                  color: const Color(0xFF7C3AED),
                ),
                const SizedBox(height: 14),
                _MonthlyRevenueChart(
                  year: reportYear,
                  monthlyRevenue: monthlyRevenue,
                  formatMoney: _formatMoney,
                ),
                const SizedBox(height: 10),
                _FinancialCard(
                  title: 'Công nợ năm $reportYear',
                  amount: _formatMoney(debt),
                  caption: 'Số dư các hóa đơn được lập trong năm',
                  icon: Icons.pending_actions_rounded,
                  color: debt > 0
                      ? const Color(0xFFB91C1C)
                      : const Color(0xFF15803D),
                ),
                const SizedBox(height: 14),
                const Text(
                  'Báo cáo tài chính tính trên tất cả hóa đơn được lập trong năm '
                  'hiện tại. Hóa đơn có trạng thái Đã thanh toán nhưng chưa có '
                  'DaThanhToan sẽ được tính theo TongTien.',
                  style: TextStyle(
                    color: Color(0xFF64748B),
                    fontSize: 12,
                    height: 1.4,
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}

class _MonthlyRevenueChart extends StatelessWidget {
  final int year;
  final List<num> monthlyRevenue;
  final String Function(num) formatMoney;

  const _MonthlyRevenueChart({
    required this.year,
    required this.monthlyRevenue,
    required this.formatMoney,
  });

  @override
  Widget build(BuildContext context) {
    final values = List<num>.generate(
      12,
      (index) => index < monthlyRevenue.length ? monthlyRevenue[index] : 0,
    );
    final maxRevenue = values.fold<num>(
      0,
      (maximum, value) => value > maximum ? value : maximum,
    );
    final highestMonth = values.indexOf(maxRevenue);
    final hasRevenue = maxRevenue > 0;

    return Container(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFE2E8F0)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Doanh thu theo tháng năm $year',
            style: const TextStyle(
              color: Color(0xFF172554),
              fontWeight: FontWeight.bold,
              fontSize: 15,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            hasRevenue
                ? 'Cao nhất: T${highestMonth + 1} · ${formatMoney(maxRevenue)}'
                : 'Chưa có khoản thanh toán nào trong năm.',
            style: const TextStyle(color: Color(0xFF64748B), fontSize: 12),
          ),
          const SizedBox(height: 18),
          SizedBox(
            height: 142,
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: List.generate(12, (index) {
                final amount = values[index];
                final fraction = maxRevenue == 0
                    ? 0.0
                    : (amount / maxRevenue).toDouble();
                final barHeight = hasRevenue
                    ? (fraction * 100).clamp(4.0, 100.0)
                    : 4.0;
                final isHighest = hasRevenue && index == highestMonth;

                return Expanded(
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 2),
                    child: Tooltip(
                      message: 'Tháng ${index + 1}: ${formatMoney(amount)}',
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.end,
                        children: [
                          Expanded(
                            child: Align(
                              alignment: Alignment.bottomCenter,
                              child: Container(
                                height: barHeight,
                                decoration: BoxDecoration(
                                  color: amount == 0
                                      ? const Color(0xFFE2E8F0)
                                      : isHighest
                                      ? const Color(0xFF7C3AED)
                                      : const Color(0xFFA78BFA),
                                  borderRadius: const BorderRadius.vertical(
                                    top: Radius.circular(5),
                                  ),
                                ),
                              ),
                            ),
                          ),
                          const SizedBox(height: 7),
                          Text(
                            '${index + 1}',
                            style: TextStyle(
                              color: isHighest
                                  ? const Color(0xFF7C3AED)
                                  : const Color(0xFF64748B),
                              fontSize: 10,
                              fontWeight: isHighest
                                  ? FontWeight.bold
                                  : FontWeight.normal,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                );
              }),
            ),
          ),
          const SizedBox(height: 3),
          const Align(
            alignment: Alignment.centerRight,
            child: Text(
              'Tháng',
              style: TextStyle(color: Color(0xFF94A3B8), fontSize: 10),
            ),
          ),
        ],
      ),
    );
  }
}

class _MetricCard extends StatelessWidget {
  final String title;
  final String value;
  final IconData icon;
  final Color color;

  const _MetricCard({
    required this.title,
    required this.value,
    required this.icon,
    required this.color,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(15),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFE2E8F0)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, color: color, size: 22),
          const SizedBox(height: 12),
          Text(
            value,
            style: TextStyle(
              color: color,
              fontWeight: FontWeight.bold,
              fontSize: 23,
            ),
          ),
          const SizedBox(height: 3),
          Text(
            title,
            style: const TextStyle(color: Color(0xFF64748B), fontSize: 12),
          ),
        ],
      ),
    );
  }
}

class _OccupancyCard extends StatelessWidget {
  final double occupancy;
  final num occupiedRooms;
  final num totalRooms;

  const _OccupancyCard({
    required this.occupancy,
    required this.occupiedRooms,
    required this.totalRooms,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFE2E8F0)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text(
                'Tỷ lệ lấp đầy',
                style: TextStyle(
                  color: Color(0xFF334155),
                  fontWeight: FontWeight.w600,
                ),
              ),
              Text(
                '${(occupancy * 100).toStringAsFixed(0)}%',
                style: const TextStyle(
                  color: Color(0xFF4F46E5),
                  fontWeight: FontWeight.bold,
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: LinearProgressIndicator(
              value: occupancy,
              minHeight: 9,
              backgroundColor: const Color(0xFFE2E8F0),
              color: const Color(0xFF4F46E5),
            ),
          ),
          const SizedBox(height: 8),
          Text(
            '${occupiedRooms.toInt()} / ${totalRooms.toInt()} phòng đang thuê',
            style: const TextStyle(color: Color(0xFF64748B), fontSize: 12),
          ),
        ],
      ),
    );
  }
}

class _FinancialCard extends StatelessWidget {
  final String title;
  final String amount;
  final String caption;
  final IconData icon;
  final Color color;

  const _FinancialCard({
    required this.title,
    required this.amount,
    required this.caption,
    required this.icon,
    required this.color,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFE2E8F0)),
      ),
      child: Row(
        children: [
          CircleAvatar(
            backgroundColor: color.withValues(alpha: 0.1),
            child: Icon(icon, color: color),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: const TextStyle(
                    color: Color(0xFF475569),
                    fontSize: 13,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  amount,
                  style: TextStyle(
                    color: color,
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  caption,
                  style: const TextStyle(
                    color: Color(0xFF94A3B8),
                    fontSize: 11,
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

class _ReportError extends StatelessWidget {
  final Future<void> Function() onRetry;
  final String message;

  const _ReportError({required this.onRetry, required this.message});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(
              Icons.bar_chart_rounded,
              size: 52,
              color: Color(0xFF94A3B8),
            ),
            const SizedBox(height: 12),
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(color: Color(0xFF64748B)),
            ),
            const SizedBox(height: 14),
            FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh_rounded),
              label: const Text('Thử tải lại'),
            ),
          ],
        ),
      ),
    );
  }
}
