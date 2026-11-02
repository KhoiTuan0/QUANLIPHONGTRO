import 'package:flutter/material.dart';
import '../services/api_service.dart';
import 'create_booking_screen.dart';
import 'create_contract_screen.dart';

class BookingListScreen extends StatefulWidget {
  const BookingListScreen({super.key});

  @override
  State<BookingListScreen> createState() => _BookingListScreenState();
}

class _BookingListScreenState extends State<BookingListScreen> {
  static const _filters = [
    'Tất cả',
    'Đã đặt cọc',
    'Đã nhận phòng',
    'Đã hủy',
    'Quá hạn',
  ];
  static const _bookingStatuses = ['Đã đặt cọc', 'Đã nhận phòng', 'Đã hủy'];

  late Future<List<dynamic>> _bookingsFuture;
  final _searchController = TextEditingController();
  List<dynamic> _bookingRecords = [];
  String _selectedFilter = 'Tất cả';
  String _searchText = '';
  bool _isUpdating = false;

  @override
  void initState() {
    super.initState();
    _bookingsFuture = _fetchBookings();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _loadData() async {
    final future = _fetchBookings();
    // setState callback must be synchronous; assigning with an expression returns the Future.
    setState(() {
      _bookingsFuture = future;
    });
    try {
      await future;
    } catch (error) {
      // FutureBuilder cũng hiển thị lỗi; thông báo này giúp thao tác tải lại dễ nhận biết.
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Tải lại đặt chỗ thất bại: $error')),
        );
      }
    }
  }

  Future<List<dynamic>> _fetchBookings() async {
    // Ghép dữ liệu khách/phòng theo mã khi API trả về khóa ngoại thay vì tên hiển thị.
    final results = await Future.wait([
      ApiService.getBookings(),
      ApiService.getCustomers(),
      ApiService.getRooms(),
    ]);
    final bookings = results[0];
    final customers = results[1].whereType<Map>().toList();
    final rooms = results[2].whereType<Map>().toList();

    final customersById = {
      for (final customer in customers)
        _value(customer, ['maKhach', 'MaKhach', 'customerId', 'id']): customer,
    };
    final roomsById = {
      for (final room in rooms)
        _value(room, ['maPhong', 'MaPhong', 'roomId', 'id']): room,
    };

    final enrichedBookings = bookings.whereType<Map>().map((booking) {
      final enriched = Map<String, dynamic>.from(
        booking.cast<String, dynamic>(),
      );
      final customerId = _value(booking, [
        'maKhach',
        'MaKhach',
        'customerId',
        'CustomerId',
      ]);
      final roomId = _value(booking, [
        'maPhong',
        'MaPhong',
        'roomId',
        'RoomId',
      ]);
      final customer = customersById[customerId];
      final room = roomsById[roomId];

      if (customer != null) {
        enriched['hoTen'] ??=
            customer['hoTen'] ??
            customer['HoTen'] ??
            customer['fullName'] ??
            customer['FullName'];
        enriched['soDienThoai'] ??=
            customer['soDienThoai'] ??
            customer['SoDienThoai'] ??
            customer['phone'] ??
            customer['Phone'];
      }
      if (room != null) {
        enriched['tenPhong'] ??=
            room['tenPhong'] ??
            room['TenPhong'] ??
            room['roomNumber'] ??
            room['RoomNumber'];
      }
      return enriched;
    }).toList();
    _bookingRecords = enrichedBookings;
    return enrichedBookings;
  }

  String _value(Map booking, List<String> keys, [String fallback = '']) {
    for (final key in keys) {
      final value = booking[key];
      if (value != null && value.toString().trim().isNotEmpty) {
        return value.toString();
      }
    }
    return fallback;
  }

  String _bookingStatus(Map booking) {
    final raw = _value(booking, ['trangThai', 'TrangThai', 'status', 'Status']);
    switch (raw.toLowerCase()) {
      case 'active':
      case 'đã đặt cọc':
      case 'da dat coc':
      case 'đang giữ chỗ':
        return 'Đã đặt cọc';
      case 'đã nhận phòng':
      case 'da nhan phong':
      case 'checkedin':
      case 'checked in':
        return 'Đã nhận phòng';
      case 'đã hủy':
      case 'da huy':
      case 'cancelled':
      case 'canceled':
        return 'Đã hủy';
      default:
        return raw.isEmpty ? 'Chưa xác định' : raw;
    }
  }

  DateTime? _dateValue(Map booking, List<String> keys) {
    final raw = _value(booking, keys);
    if (raw.isEmpty) return null;
    return DateTime.tryParse(raw);
  }

  bool _isOverdue(Map booking) {
    if (_bookingStatus(booking) != 'Đã đặt cọc') return false;
    final expectedDate = _dateValue(booking, [
      'ngayDuKienNhan',
      'NgayDuKienNhan',
      'ngayDuKienNhanPhong',
      'NgayDuKienNhanPhong',
      'expectedDate',
      'expectedCheckIn',
    ]);
    return expectedDate != null &&
        expectedDate.isBefore(DateTime.now().subtract(const Duration(days: 1)));
  }

  String _formatDate(DateTime? date) {
    if (date == null) return 'Chưa cập nhật';
    return '${date.day.toString().padLeft(2, '0')}/'
        '${date.month.toString().padLeft(2, '0')}/${date.year}';
  }

  String _formatMoney(String value) {
    final amount = num.tryParse(value);
    if (amount == null) return value.isEmpty ? '0 đ' : '$value đ';
    final digits = amount.toInt().toString();
    final formatted = digits.replaceAllMapped(
      RegExp(r'(\d)(?=(\d{3})+(?!\d))'),
      (match) => '${match[1]},',
    );
    return '$formatted đ';
  }

  Color _statusColor(String status, {required bool overdue}) {
    if (overdue) return const Color(0xFFB45309);
    switch (status) {
      case 'Đã nhận phòng':
        return const Color(0xFF15803D);
      case 'Đã hủy':
        return const Color(0xFFB91C1C);
      case 'Đã đặt cọc':
        return const Color(0xFFB45309);
      default:
        return const Color(0xFF64748B);
    }
  }

  List<dynamic> _filterBookings(List<dynamic> bookings) {
    final search = _searchText.trim().toLowerCase();
    return bookings.where((rawBooking) {
      if (rawBooking is! Map) return false;
      final booking = rawBooking;
      final status = _bookingStatus(booking);
      final overdue = _isOverdue(booking);

      final matchesFilter = switch (_selectedFilter) {
        'Đã đặt cọc' => status == 'Đã đặt cọc' && !overdue,
        'Đã nhận phòng' => status == 'Đã nhận phòng',
        'Đã hủy' => status == 'Đã hủy',
        'Quá hạn' => overdue,
        _ => true,
      };
      final matchesSearch =
          search.isEmpty ||
          [
            _value(booking, ['tenPhong', 'TenPhong', 'roomNumber']),
            _value(booking, [
              'tenKhach',
              'TenKhach',
              'hoTen',
              'HoTen',
              'customerName',
              'fullName',
            ]),
            _value(booking, ['soDienThoai', 'SoDienThoai', 'phone']),
          ].any((value) => value.toLowerCase().contains(search));
      return matchesFilter && matchesSearch;
    }).toList();
  }

  Future<void> _addBooking() async {
    final messenger = ScaffoldMessenger.of(context);
    try {
      // Chỉ cho phép tạo lượt giữ chỗ mới trên phòng đang trống.
      final rooms = await ApiService.getRooms();
      if (!mounted) return;
      final availableRooms = rooms.where((room) {
        if (room is! Map) return false;
        final status = _value(room, [
          'trangThai',
          'TrangThai',
          'status',
          'Status',
        ]).trim();
        return status == 'Trống' || status.toLowerCase() == 'available';
      }).toList();

      if (availableRooms.isEmpty) {
        messenger.showSnackBar(
          const SnackBar(
            content: Text('Hiện không có phòng trống để tạo lượt đặt chỗ.'),
          ),
        );
        return;
      }

      final selectedRoom = await showModalBottomSheet<Map<String, dynamic>>(
        context: context,
        isScrollControlled: true,
        backgroundColor: Colors.transparent,
        builder: (context) => _RoomPickerSheet(rooms: availableRooms),
      );
      if (!mounted || selectedRoom == null) return;

      final result = await Navigator.push<bool>(
        context,
        MaterialPageRoute(
          builder: (context) => CreateBookingScreen(room: selectedRoom),
        ),
      );
      if (result == true && mounted) await _loadData();
    } catch (error) {
      if (mounted) {
        messenger.showSnackBar(
          SnackBar(content: Text('Không thể mở danh sách phòng: $error')),
        );
      }
    }
  }

  Future<void> _openContractForBooking(Map booking) async {
    final roomId = int.tryParse(
      _value(booking, ['maPhong', 'MaPhong', 'roomId', 'RoomId']),
    );
    if (roomId == null || roomId <= 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Phiếu đặt chỗ chưa có mã phòng hợp lệ.')),
      );
      return;
    }

    setState(() => _isUpdating = true);
    try {
      final rooms = await ApiService.getRooms();
      if (!mounted) return;
      Map<String, dynamic>? selectedRoom;
      for (final raw in rooms) {
        if (raw is! Map) continue;
        final id = int.tryParse(
          _value(raw, ['maPhong', 'MaPhong', 'roomId', 'RoomId', 'id']),
        );
        if (id == roomId) {
          selectedRoom = Map<String, dynamic>.from(raw);
          break;
        }
      }
      if (selectedRoom == null) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Không tìm thấy phòng #$roomId trên hệ thống.')),
        );
        return;
      }
      selectedRoom['roomId'] = roomId;
      selectedRoom['roomNumber'] ??= _value(
        booking, ['tenPhong', 'TenPhong'], 'Phòng #$roomId',
      );
      selectedRoom['basePrice'] ??=
          selectedRoom['giaThue'] ?? selectedRoom['GiaThue'] ?? 0;

      final confirmed = await showDialog<bool>(
        context: context,
        builder: (dialogContext) => AlertDialog(
          title: const Text('Tiếp tục nhận phòng?'),
          content: Text(
            'Bạn sẽ lập hợp đồng cho ${selectedRoom!['roomNumber']}. '
            'Hãy kiểm tra đúng khách đã đặt cọc, tiền cọc và các điều khoản '
            'trước khi xác nhận. Việc lập hợp đồng cần được backend xử lý '
            'đồng bộ với phiếu đặt chỗ.',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(dialogContext, false),
              child: const Text('Để sau'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(dialogContext, true),
              child: const Text('Lập hợp đồng'),
            ),
          ],
        ),
      );
      if (confirmed != true || !mounted) return;

      final created = await Navigator.push<bool>(
        context,
        MaterialPageRoute(
          builder: (_) => CreateContractScreen(room: selectedRoom!),
        ),
      );
      if (!mounted) return;
      if (created == true) {
        await _loadData();
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text(
              'Đã tạo hợp đồng. Hãy kiểm tra trạng thái phiếu đặt chỗ '
              'và phòng đã được backend đồng bộ.'
            ),
          ),
        );
      }
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Không thể mở màn hình lập hợp đồng: $error')),
      );
    } finally {
      if (mounted) setState(() => _isUpdating = false);
    }
  }

  Future<void> _changeStatus(Map booking) async {
    final bookingId = int.tryParse(
      _value(booking, ['maDatPhong', 'MaDatPhong', 'bookingId', 'id']),
    );
    if (bookingId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Không tìm thấy mã đặt phòng để cập nhật.'),
        ),
      );
      return;
    }

    final currentStatus = _bookingStatus(booking);
    final selectedStatus = await showDialog<String>(
      context: context,
      builder: (context) => _BookingStatusDialog(
        currentStatus: currentStatus,
        statuses: currentStatus == 'Đã nhận phòng'
            ? const ['Đã nhận phòng']
            : _bookingStatuses,
      ),
    );
    if (selectedStatus == null || !mounted) {
      return;
    }

    if (selectedStatus == currentStatus) return;
    if (selectedStatus == 'Đã nhận phòng') {
      if (currentStatus != 'Đã đặt cọc') {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Chỉ nhận phòng từ phiếu đang đặt cọc.')),
        );
        return;
      }
      await _openContractForBooking(booking);
      return;
    }
    if (selectedStatus != 'Đã hủy') return;
    if (currentStatus != 'Đã đặt cọc') {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
        content: Text('Chỉ hủy được phiếu đang đặt cọc.'),
      ));
      return;
    }
    // Chỉ hiển thị ước tính. Backend là nơi tính tiền hoàn chính thức.
    final expected = _dateValue(booking, [
      'ngayDuKienNhan', 'NgayDuKienNhan',
      'ngayDuKienNhanPhong', 'NgayDuKienNhanPhong',
    ]);
    final deposit = double.tryParse(
      _value(booking, ['tienCoc', 'TienCoc', 'depositAmount'], '0'),
    ) ?? 0;
    final today = DateTime.now();
    final todayDate = DateTime(today.year, today.month, today.day);
    final days = expected == null ? null :
        DateTime(expected.year, expected.month, expected.day)
            .difference(todayDate).inDays;
    final rate = days == null ? null : (days >= 7 ? 1.0 : days >= 3 ? 0.5 : 0.0);
    final estimatedRefund = rate == null ? null : (deposit * rate).round();
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Xác nhận hủy đặt phòng'),
        content: Text(
          'Tiền cọc: ${_formatMoney(deposit.toString())}\n'
          'Ngày dự kiến nhận: ${_formatDate(expected)}\n'
          '${days == null ? 'Chưa xác định số ngày báo trước' : 'Báo trước: $days ngày'}\n'
          '${rate == null ? 'Chưa xác định tỷ lệ hoàn' : 'Tỷ lệ hoàn: ${(rate * 100).toInt()}%'}\n'
          '${estimatedRefund == null ? '' : 'Dự kiến hoàn: ${_formatMoney(estimatedRefund.toString())}\n'}'
          '\nSố tiền chính thức do máy chủ tính tại thời điểm hủy. '
          'Hành động này không thể hoàn tác.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, false),
            child: const Text('Quay lại'),
          ),
          FilledButton(
            onPressed: expected == null ? null :
                () => Navigator.pop(dialogContext, true),
            child: const Text('Xác nhận hủy'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;

    setState(() => _isUpdating = true);
    try {
      // Backend đổi trạng thái đặt chỗ và phòng trong cùng một transaction.
      final result = await ApiService.updateBooking(bookingId, {
        'status': selectedStatus,
      });
      if (!mounted) return;
      if (result['success'] == true) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(result['message']?.toString() ?? 'Đã hủy đặt phòng.')));
        await _loadData();
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(result['message'] ?? 'Cập nhật trạng thái thất bại.'),
          ),
        );
      }
    } finally {
      if (mounted) setState(() => _isUpdating = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF4F7FB),
      appBar: AppBar(
        title: const Text(
          'Quản lý đặt chỗ',
          style: TextStyle(
            fontWeight: FontWeight.bold,
            fontSize: 18,
            color: Color(0xFF1E293B),
          ),
        ),
        backgroundColor: Colors.white,
        foregroundColor: const Color(0xFF1E293B),
        elevation: 0,
        actions: [
          IconButton(
            tooltip: 'Tải lại',
            onPressed: _isUpdating ? null : _loadData,
            icon: const Icon(Icons.refresh_rounded),
          ),
          const SizedBox(width: 4),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _isUpdating ? null : _addBooking,
        icon: const Icon(Icons.add),
        label: const Text('Thêm đặt chỗ'),
        backgroundColor: const Color(0xFF2563EB),
        foregroundColor: Colors.white,
      ),
      body: FutureBuilder<List<dynamic>>(
        future: _bookingsFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting &&
              !snapshot.hasData) {
            return const Center(
              child: CircularProgressIndicator(color: Color(0xFF2563EB)),
            );
          }
          if (snapshot.hasError) {
            return _LoadError(
              message: 'Không thể tải danh sách đặt chỗ.',
              onRetry: _loadData,
            );
          }

          final bookings = snapshot.data ?? _bookingRecords;
          final overdueCount = bookings
              .whereType<Map>()
              .where(_isOverdue)
              .length;
          final holdingCount = bookings
              .whereType<Map>()
              .where(
                (booking) =>
                    _bookingStatus(booking) == 'Đã đặt cọc' &&
                    !_isOverdue(booking),
              )
              .length;
          final checkInCount = bookings
              .whereType<Map>()
              .where((booking) => _bookingStatus(booking) == 'Đã nhận phòng')
              .length;
          final filtered = _filterBookings(bookings);

          return RefreshIndicator(
            color: const Color(0xFF2563EB),
            onRefresh: _loadData,
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 100),
              children: [
                _SummaryRow(
                  holdingCount: holdingCount,
                  checkInCount: checkInCount,
                  overdueCount: overdueCount,
                ),
                const SizedBox(height: 18),
                TextField(
                  controller: _searchController,
                  onChanged: (value) => setState(() => _searchText = value),
                  decoration: InputDecoration(
                    hintText: 'Tìm theo phòng, tên hoặc số điện thoại',
                    prefixIcon: const Icon(Icons.search_rounded),
                    suffixIcon: _searchText.isEmpty
                        ? null
                        : IconButton(
                            tooltip: 'Xóa tìm kiếm',
                            onPressed: () {
                              _searchController.clear();
                              setState(() => _searchText = '');
                            },
                            icon: const Icon(Icons.close_rounded),
                          ),
                    filled: true,
                    fillColor: Colors.white,
                    contentPadding: const EdgeInsets.symmetric(vertical: 14),
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(14),
                      borderSide: const BorderSide(color: Color(0xFFE2E8F0)),
                    ),
                    enabledBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(14),
                      borderSide: const BorderSide(color: Color(0xFFE2E8F0)),
                    ),
                  ),
                ),
                const SizedBox(height: 14),
                SizedBox(
                  height: 42,
                  child: ListView(
                    scrollDirection: Axis.horizontal,
                    children: _filters.map((filter) {
                      final selected = filter == _selectedFilter;
                      return Padding(
                        padding: const EdgeInsets.only(right: 8),
                        child: ChoiceChip(
                          label: Text(filter),
                          selected: selected,
                          onSelected: (_) =>
                              setState(() => _selectedFilter = filter),
                          selectedColor: const Color(0xFF2563EB),
                          backgroundColor: Colors.white,
                          side: BorderSide(
                            color: selected
                                ? const Color(0xFF2563EB)
                                : const Color(0xFFE2E8F0),
                          ),
                          labelStyle: TextStyle(
                            color: selected
                                ? Colors.white
                                : const Color(0xFF475569),
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      );
                    }).toList(),
                  ),
                ),
                const SizedBox(height: 8),
                if (filtered.isEmpty)
                  _EmptyBookings(
                    hasBookings: bookings.isNotEmpty,
                    filter: _selectedFilter,
                    onAdd: _addBooking,
                  )
                else
                  ...filtered.map(
                    (booking) => _BookingCard(
                      booking: booking,
                      status: _bookingStatus(booking),
                      overdue: _isOverdue(booking),
                      isUpdating: _isUpdating,
                      value: _value,
                      formatDate: _formatDate,
                      formatMoney: _formatMoney,
                      statusColor: _statusColor,
                      onChangeStatus: () => _changeStatus(booking),
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

class _SummaryRow extends StatelessWidget {
  final int holdingCount;
  final int checkInCount;
  final int overdueCount;

  const _SummaryRow({
    required this.holdingCount,
    required this.checkInCount,
    required this.overdueCount,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: _SummaryCard(
            label: 'Đang giữ chỗ',
            value: holdingCount,
            icon: Icons.bookmark_added_outlined,
            color: const Color(0xFF2563EB),
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: _SummaryCard(
            label: 'Đã nhận phòng',
            value: checkInCount,
            icon: Icons.meeting_room_outlined,
            color: const Color(0xFF15803D),
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: _SummaryCard(
            label: 'Quá hạn',
            value: overdueCount,
            icon: Icons.schedule_rounded,
            color: const Color(0xFFB45309),
          ),
        ),
      ],
    );
  }
}

class _SummaryCard extends StatelessWidget {
  final String label;
  final int value;
  final IconData icon;
  final Color color;

  const _SummaryCard({
    required this.label,
    required this.value,
    required this.icon,
    required this.color,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFE2E8F0)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, color: color, size: 20),
          const SizedBox(height: 10),
          Text(
            '$value',
            style: const TextStyle(
              fontSize: 21,
              fontWeight: FontWeight.bold,
              color: Color(0xFF1E293B),
            ),
          ),
          const SizedBox(height: 2),
          Text(
            label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(fontSize: 11, color: Color(0xFF64748B)),
          ),
        ],
      ),
    );
  }
}

class _BookingCard extends StatelessWidget {
  final Map booking;
  final String status;
  final bool overdue;
  final bool isUpdating;
  final String Function(Map, List<String>, [String]) value;
  final String Function(DateTime?) formatDate;
  final String Function(String) formatMoney;
  final Color Function(String, {required bool overdue}) statusColor;
  final VoidCallback onChangeStatus;

  const _BookingCard({
    required this.booking,
    required this.status,
    required this.overdue,
    required this.isUpdating,
    required this.value,
    required this.formatDate,
    required this.formatMoney,
    required this.statusColor,
    required this.onChangeStatus,
  });

  @override
  Widget build(BuildContext context) {
    final room = value(booking, [
      'tenPhong',
      'TenPhong',
      'roomNumber',
    ], 'Chưa gán phòng');
    final customer = value(booking, [
      'tenKhach',
      'TenKhach',
      'hoTen',
      'HoTen',
      'customerName',
      'fullName',
    ], 'Chưa rõ khách');
    final phone = value(booking, [
      'soDienThoai',
      'SoDienThoai',
      'phone',
    ], 'Chưa có số điện thoại');
    final deposit = value(booking, [
      'tienCoc',
      'TienCoc',
      'depositAmount',
    ], '0');
    final bookingDate = DateTime.tryParse(
      value(booking, ['ngayDat', 'NgayDat', 'bookingDate']),
    );
    final expectedDate = DateTime.tryParse(
      value(booking, [
        'ngayDuKienNhan',
        'NgayDuKienNhan',
        'ngayDuKienNhanPhong',
        'NgayDuKienNhanPhong',
        'expectedDate',
        'expectedCheckIn',
      ]),
    );
    final note = value(booking, [
      'ghiChu',
      'GhiChu',
      'note',
    ], 'Không có ghi chú');
    final badgeColor = statusColor(status, overdue: overdue);
    final effectiveStatus = overdue ? 'Quá hạn' : status;

    return Card(
      elevation: 0,
      margin: const EdgeInsets.only(bottom: 12),
      color: Colors.white,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: const BorderSide(color: Color(0xFFE2E8F0)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(15),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  width: 44,
                  height: 44,
                  decoration: BoxDecoration(
                    color: badgeColor.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Icon(Icons.meeting_room_outlined, color: badgeColor),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        room,
                        style: const TextStyle(
                          fontWeight: FontWeight.bold,
                          fontSize: 16,
                          color: Color(0xFF1E293B),
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        customer,
                        style: const TextStyle(
                          fontWeight: FontWeight.w600,
                          color: Color(0xFF334155),
                        ),
                      ),
                    ],
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 9,
                    vertical: 5,
                  ),
                  decoration: BoxDecoration(
                    color: badgeColor.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: Text(
                    effectiveStatus,
                    style: TextStyle(
                      color: badgeColor,
                      fontSize: 11,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 13),
            Wrap(
              spacing: 16,
              runSpacing: 10,
              children: [
                _InfoLine(icon: Icons.phone_outlined, text: phone),
                _InfoLine(
                  icon: Icons.payments_outlined,
                  text: 'Cọc: ${formatMoney(deposit)}',
                ),
                _InfoLine(
                  icon: Icons.event_available_outlined,
                  text: 'Đặt: ${formatDate(bookingDate)}',
                ),
                _InfoLine(
                  icon: Icons.login_rounded,
                  text: 'Nhận phòng: ${formatDate(expectedDate)}',
                ),
              ],
            ),
            if (note.isNotEmpty && note != 'Không có ghi chú') ...[
              const SizedBox(height: 11),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: const Color(0xFFF8FAFC),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Text(
                  'Ghi chú: $note',
                  style: const TextStyle(
                    color: Color(0xFF475569),
                    fontSize: 12,
                  ),
                ),
              ),
            ],
            // Sau khi nhận phòng, vòng đời được quản lý bằng hợp đồng và trả phòng.
            if (status == 'Đã đặt cọc' || status == 'Đã nhận phòng') ...[
              const SizedBox(height: 12),
              Align(
                alignment: Alignment.centerRight,
                child: OutlinedButton.icon(
                  onPressed: isUpdating ? null : onChangeStatus,
                  icon: Icon(
                    status == 'Đã nhận phòng'
                        ? Icons.sync_rounded
                        : Icons.edit_outlined,
                    size: 17,
                  ),
                  label: Text(
                    status == 'Đã nhận phòng'
                        ? 'Đồng bộ trạng thái phòng'
                        : 'Cập nhật trạng thái',
                  ),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: const Color(0xFF2563EB),
                    side: const BorderSide(color: Color(0xFFBFDBFE)),
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _InfoLine extends StatelessWidget {
  final IconData icon;
  final String text;

  const _InfoLine({required this.icon, required this.text});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 15, color: const Color(0xFF64748B)),
        const SizedBox(width: 5),
        Text(
          text,
          style: const TextStyle(fontSize: 12, color: Color(0xFF475569)),
        ),
      ],
    );
  }
}

class _BookingStatusDialog extends StatefulWidget {
  final String currentStatus;
  final List<String> statuses;

  const _BookingStatusDialog({
    required this.currentStatus,
    required this.statuses,
  });

  @override
  State<_BookingStatusDialog> createState() => _BookingStatusDialogState();
}

class _BookingStatusDialogState extends State<_BookingStatusDialog> {
  late String _selectedStatus;

  @override
  void initState() {
    super.initState();
    _selectedStatus = widget.statuses.contains(widget.currentStatus)
        ? widget.currentStatus
        : widget.statuses.first;
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Trạng thái đặt phòng'),
      content: DropdownButtonFormField<String>(
        initialValue: _selectedStatus,
        items: widget.statuses
            .map(
              (status) => DropdownMenuItem(value: status, child: Text(status)),
            )
            .toList(),
        onChanged: (value) {
          if (value != null) setState(() => _selectedStatus = value);
        },
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: const Text('Hủy'),
        ),
        FilledButton(
          onPressed: () => Navigator.pop(context, _selectedStatus),
          child: const Text('Lưu trạng thái'),
        ),
      ],
    );
  }
}

class _RoomPickerSheet extends StatelessWidget {
  final List<dynamic> rooms;

  const _RoomPickerSheet({required this.rooms});

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Container(
        constraints: BoxConstraints(
          maxHeight: MediaQuery.of(context).size.height * 0.75,
        ),
        decoration: const BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.vertical(top: Radius.circular(22)),
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const SizedBox(height: 16),
            Container(
              width: 38,
              height: 4,
              decoration: BoxDecoration(
                color: const Color(0xFFCBD5E1),
                borderRadius: BorderRadius.circular(5),
              ),
            ),
            const Padding(
              padding: EdgeInsets.fromLTRB(20, 16, 20, 12),
              child: Align(
                alignment: Alignment.centerLeft,
                child: Text(
                  'Chọn phòng trống để giữ chỗ',
                  style: TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                    color: Color(0xFF1E293B),
                  ),
                ),
              ),
            ),
            Flexible(
              child: ListView.separated(
                shrinkWrap: true,
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 18),
                itemCount: rooms.length,
                separatorBuilder: (_, _) => const SizedBox(height: 8),
                itemBuilder: (context, index) {
                  final room = Map<String, dynamic>.from(rooms[index] as Map);
                  // Chuẩn hóa các tên cột API thường gặp để form tạo đặt chỗ đọc được.
                  room['roomId'] ??= room['maPhong'] ?? room['MaPhong'];
                  room['roomNumber'] ??=
                      room['tenPhong'] ?? room['TenPhong'] ?? 'Phòng';
                  room['basePrice'] ??= room['giaThue'] ?? room['GiaThue'] ?? 0;
                  final name = room['roomNumber'];
                  final price =
                      room['giaThue'] ??
                      room['GiaThue'] ??
                      room['basePrice'] ??
                      0;
                  return ListTile(
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(14),
                      side: const BorderSide(color: Color(0xFFE2E8F0)),
                    ),
                    leading: const CircleAvatar(
                      backgroundColor: Color(0xFFEFF6FF),
                      child: Icon(
                        Icons.meeting_room_outlined,
                        color: Color(0xFF2563EB),
                      ),
                    ),
                    title: Text(
                      name.toString(),
                      style: const TextStyle(fontWeight: FontWeight.w600),
                    ),
                    subtitle: Text('Giá thuê: $price đ/tháng'),
                    trailing: const Icon(Icons.chevron_right_rounded),
                    onTap: () => Navigator.pop(context, room),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _EmptyBookings extends StatelessWidget {
  final bool hasBookings;
  final String filter;
  final VoidCallback onAdd;

  const _EmptyBookings({
    required this.hasBookings,
    required this.filter,
    required this.onAdd,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 54),
      child: Column(
        children: [
          Icon(
            hasBookings
                ? Icons.search_off_rounded
                : Icons.bookmark_add_outlined,
            size: 54,
            color: const Color(0xFF94A3B8),
          ),
          const SizedBox(height: 12),
          Text(
            hasBookings
                ? 'Không có lượt đặt chỗ trong mục "$filter".'
                : 'Chưa có lượt đặt chỗ nào.',
            textAlign: TextAlign.center,
            style: const TextStyle(color: Color(0xFF64748B)),
          ),
          if (!hasBookings) ...[
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: onAdd,
              icon: const Icon(Icons.add),
              label: const Text('Tạo lượt đặt chỗ'),
            ),
          ],
        ],
      ),
    );
  }
}

class _LoadError extends StatelessWidget {
  final String message;
  final VoidCallback onRetry;

  const _LoadError({required this.message, required this.onRetry});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(
              Icons.cloud_off_outlined,
              size: 48,
              color: Color(0xFF94A3B8),
            ),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 12),
            FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('Thử lại'),
            ),
          ],
        ),
      ),
    );
  }
}
