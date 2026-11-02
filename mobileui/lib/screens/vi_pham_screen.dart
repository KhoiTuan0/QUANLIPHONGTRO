import 'package:flutter/material.dart';
import '../services/api_service.dart';

class ViPhamScreen extends StatefulWidget {
  const ViPhamScreen({super.key});

  @override
  State<ViPhamScreen> createState() => _ViPhamScreenState();
}

class _ViPhamScreenState extends State<ViPhamScreen> {
  late Future<List<dynamic>> _violationsFuture;

  @override
  void initState() {
    super.initState();
    _violationsFuture = _fetchViolations();
  }

  Future<void> _reload() async {
    final future = _fetchViolations();
    setState(() {
      _violationsFuture = future;
    });
    try {
      await future;
    } catch (error) {
      if (mounted) {
        _showMessage(
          'Tải lại danh sách vi phạm thất bại: $error',
          isError: true,
        );
      }
    }
  }

  Future<List<dynamic>> _fetchViolations() async {
    // Ghép mã phòng/khách với tên để danh sách vẫn dễ đọc nếu API chỉ trả khóa ngoại.
    final results = await Future.wait([
      ApiService.getViPhams(),
      ApiService.getRooms(),
      ApiService.getCustomers(),
    ]);
    final rooms = results[1].whereType<Map>().toList();
    final customers = results[2].whereType<Map>().toList();
    final roomsById = {
      for (final room in rooms)
        _id(room, ['maPhong', 'MaPhong', 'roomId', 'RoomId', 'id']): room,
    };
    final customersById = {
      for (final customer in customers)
        _id(customer, ['maKhach', 'MaKhach', 'customerId', 'CustomerId', 'id']):
            customer,
    };

    return results[0].whereType<Map>().map((violation) {
      final enriched = Map<String, dynamic>.from(
        violation.cast<String, dynamic>(),
      );
      final room =
          roomsById[_id(violation, ['maPhong', 'MaPhong', 'roomId', 'RoomId'])];
      final customer =
          customersById[_id(violation, [
            'maKhach',
            'MaKhach',
            'customerId',
            'CustomerId',
          ])];
      if (room != null) {
        enriched['tenPhong'] ??=
            room['tenPhong'] ??
            room['TenPhong'] ??
            room['roomNumber'] ??
            room['RoomNumber'];
      }
      if (customer != null) {
        enriched['tenKhach'] ??=
            customer['hoTen'] ??
            customer['HoTen'] ??
            customer['fullName'] ??
            customer['FullName'];
      }
      return enriched;
    }).toList();
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

  String _id(Map data, List<String> keys) => _value(data, keys);

  String _date(Map data) {
    final raw = _value(data, [
      'ngayLap',
      'NgayLap',
      'ngayViPham',
      'NgayViPham',
      'createdAt',
    ]);
    final parsed = DateTime.tryParse(raw);
    if (parsed == null) return 'Chưa rõ ngày';
    return '${parsed.day.toString().padLeft(2, '0')}/'
        '${parsed.month.toString().padLeft(2, '0')}/${parsed.year}';
  }

  String _money(dynamic amount) {
    final value = num.tryParse(amount?.toString() ?? '') ?? 0;
    final digits = value.toInt().toString();
    return '${digits.replaceAllMapped(RegExp(r'(\d)(?=(\d{3})+(?!\d))'), (match) => '${match[1]},')} đ';
  }

  Future<void> _showCreateViolation() async {
    try {
      final results = await Future.wait([
        ApiService.getRooms(),
        ApiService.getCustomers(),
        ApiService.getContracts(),
      ]);
      if (!mounted) return;
      final rooms = results[0].whereType<Map>().toList();
      final customers = results[1].whereType<Map>().toList();
      final contracts = results[2].whereType<Map>().toList();
      if (rooms.isEmpty || customers.isEmpty) {
        _showMessage(
          rooms.isEmpty
              ? 'Cần có phòng trong hệ thống trước khi ghi nhận vi phạm.'
              : 'Cần có khách thuê trong hệ thống trước khi ghi nhận vi phạm.',
          isError: true,
        );
        return;
      }
      final roomsById = {
        for (final room in rooms)
          _id(room, ['maPhong', 'MaPhong', 'roomId', 'RoomId', 'id']): room,
      };
      final customersById = {
        for (final customer in customers)
          _id(customer, [
            'maKhach',
            'MaKhach',
            'customerId',
            'CustomerId',
            'id',
          ]): customer,
      };
      String normalizeName(String value) => value.trim().toLowerCase();
      final roomsByName = <String, Map>{};
      for (final room in rooms) {
        final name = _value(room, [
          'tenPhong',
          'TenPhong',
          'roomNumber',
          'RoomNumber',
        ]);
        if (name.isNotEmpty) roomsByName[normalizeName(name)] = room;
      }
      final customersByName = <String, Map>{};
      for (final customer in customers) {
        final name = _value(customer, [
          'hoTen',
          'HoTen',
          'fullName',
          'FullName',
        ]);
        if (name.isNotEmpty) {
          customersByName[normalizeName(name)] = customer;
        }
      }
      final tenantOptions = <_TenantOption>[];
      for (final contract in contracts) {
        final contractRoomId = int.tryParse(
          _id(contract, ['maPhong', 'MaPhong', 'roomId', 'RoomId']),
        );
        final contractCustomerId = int.tryParse(
          _id(contract, [
            'maKhachDaiDien',
            'MaKhachDaiDien',
            'maKhach',
            'MaKhach',
            'customerId',
            'CustomerId',
          ]),
        );
        // Một số HopDong API trả roomNumber/customerName thay vì khóa ngoại.
        final room =
            (contractRoomId == null
                ? null
                : roomsById[contractRoomId.toString()]) ??
            roomsByName[normalizeName(
              _value(contract, [
                'tenPhong',
                'TenPhong',
                'roomNumber',
                'RoomNumber',
              ]),
            )];
        final customer =
            (contractCustomerId == null
                ? null
                : customersById[contractCustomerId.toString()]) ??
            customersByName[normalizeName(
              _value(contract, [
                'hoTen',
                'HoTen',
                'customerName',
                'CustomerName',
                'fullName',
                'FullName',
              ]),
            )];
        if (room == null || customer == null) continue;
        final roomId = int.tryParse(
          _id(room, ['maPhong', 'MaPhong', 'roomId', 'RoomId', 'id', 'Id']),
        );
        final customerId = int.tryParse(
          _id(customer, [
            'maKhach',
            'MaKhach',
            'customerId',
            'CustomerId',
            'id',
            'Id',
          ]),
        );
        if (roomId == null || customerId == null) continue;

        final contractStatus = _value(contract, [
          'trangThai',
          'TrangThai',
          'status',
          'Status',
        ]).toLowerCase();
        final roomStatus = _value(room, [
          'trangThai',
          'TrangThai',
          'status',
          'Status',
        ]).toLowerCase();
        final activeContract =
            contractStatus == 'active' ||
            contractStatus == 'đang hiệu lực' ||
            contractStatus == 'còn hiệu lực' ||
            contractStatus == 'đang thuê' ||
            contractStatus == 'đang hoạt động' ||
            (contractStatus.isEmpty &&
                (roomStatus == 'đang thuê' ||
                    roomStatus == 'đã đặt cọc' ||
                    roomStatus == 'occupied'));
        if (!activeContract) continue;

        tenantOptions.add(
          _TenantOption(
            roomId: roomId,
            customerId: customerId,
            roomName: _value(room, [
              'tenPhong',
              'TenPhong',
              'roomNumber',
              'RoomNumber',
            ], 'Phòng $roomId'),
            customerName: _value(customer, [
              'hoTen',
              'HoTen',
              'fullName',
              'FullName',
            ], 'Khách $customerId'),
          ),
        );
      }
      if (tenantOptions.isEmpty) {
        _showMessage(
          'Chỉ có thể ghi vi phạm cho khách có hợp đồng đang hiệu lực. '
          'Hãy kiểm tra hợp đồng và trạng thái phòng.',
          isError: true,
        );
        return;
      }

      final result = await showModalBottomSheet<_ViolationDraft>(
        context: context,
        isScrollControlled: true,
        backgroundColor: Colors.transparent,
        builder: (context) => _CreateViolationSheet(tenants: tenantOptions),
      );
      if (!mounted || result == null) return;

      final saveResult = await ApiService.createViPham({
        // Gửi khóa ngoại thật của phòng/khách thay vì tên phòng tự nhập.
        'maPhong': result.roomId,
        'maKhach': result.customerId,
        'noiDung': result.description,
        'tienPhat': result.fine,
        'ngayLap': DateTime.now().toIso8601String().split('T').first,
      });
      if (!mounted) return;

      if (saveResult['success'] == true) {
        final billUpdated = saveResult['daCongVaoHoaDon'] == true;
        final queuedForNextInvoice = saveResult['choTinhHoaDon'] == true;
        final invoiceId = saveResult['maHoaDon'] ?? saveResult['MaHoaDon'];
        final invoiceTotal =
            saveResult['tongTienHoaDon'] ?? saveResult['TongTienHoaDon'];
        final billingMessage = billUpdated
            ? 'Khoản phạt đã được cộng vào hóa đơn tháng này'
                  '${invoiceId == null ? '' : ' #$invoiceId'}'
                  '${invoiceTotal == null ? '' : ', tổng mới ${_money(invoiceTotal)}'}.'
            : queuedForNextInvoice
            ? 'Khoản phạt sẽ được cộng vào hóa đơn tiếp theo sau khi xử lý.'
            : 'Khoản phạt chưa được cộng vào hóa đơn hiện tại; '
                  'chủ trọ cần kiểm tra kỳ hóa đơn hoặc trạng thái thanh toán.';

        // Gửi cho khách đúng kết quả backend xác nhận đã gộp vào hóa đơn hay chưa.
        final notificationSent = await ApiService.sendMessage({
          'customerIdentity': result.customerId,
          'content':
              'Chủ trọ đã ghi nhận vi phạm tại ${result.roomName}: '
              '${result.description}. Tiền phạt ${_money(result.fine)}. '
              '$billingMessage',
          'isAdmin': true,
          'sendDate': DateTime.now().toIso8601String(),
        });
        if (!mounted) return;
        _showMessage(
          notificationSent
              ? '${saveResult['message'] ?? 'Đã ghi nhận vi phạm.'} '
                    'Đã gửi thông báo cho khách.'
              : 'Đã ghi nhận vi phạm nhưng chưa gửi được thông báo. '
                    'Hãy kiểm tra kết nối và nhắn khách thủ công.',
          isError: !notificationSent || (!billUpdated && !queuedForNextInvoice),
        );
        await _reload();
      } else {
        _showMessage(
          saveResult['message'] ??
              'Không lưu được vi phạm. Hãy kiểm tra API ViPham và các trường dữ liệu.',
          isError: true,
        );
      }
    } catch (error) {
      if (mounted) {
        _showMessage('Không thể tạo vi phạm: $error', isError: true);
      }
    }
  }

  void _showMessage(String message, {bool isError = false}) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: isError ? Colors.redAccent : Colors.green,
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: const Text(
          'Vi phạm nội quy',
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
            tooltip: 'Tải lại',
            onPressed: _reload,
            icon: const Icon(Icons.refresh_rounded),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        backgroundColor: const Color(0xFF4F46E5),
        foregroundColor: Colors.white,
        onPressed: _showCreateViolation,
        icon: const Icon(Icons.add_alert_rounded),
        label: const Text('Ghi nhận vi phạm'),
      ),
      body: FutureBuilder<List<dynamic>>(
        future: _violationsFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting &&
              !snapshot.hasData) {
            return const Center(
              child: CircularProgressIndicator(color: Color(0xFF4F46E5)),
            );
          }
          if (snapshot.hasError) {
            return _ViolationLoadError(
              message: 'Không tải được danh sách vi phạm.',
              onRetry: _reload,
            );
          }

          final violations = snapshot.data ?? [];
          final totalFine = violations.whereType<Map>().fold<num>(
            0,
            (sum, violation) =>
                sum +
                (num.tryParse(
                      _value(violation, [
                        'tienPhat',
                        'TienPhat',
                        'fineAmount',
                      ]).toString(),
                    ) ??
                    0),
          );

          return RefreshIndicator(
            onRefresh: _reload,
            color: const Color(0xFF4F46E5),
            child: violations.isEmpty
                ? ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    children: const [
                      SizedBox(height: 150),
                      Icon(
                        Icons.verified_user_outlined,
                        size: 56,
                        color: Color(0xFF94A3B8),
                      ),
                      SizedBox(height: 12),
                      Center(
                        child: Text(
                          'Chưa có vi phạm nào được ghi nhận.',
                          style: TextStyle(color: Color(0xFF64748B)),
                        ),
                      ),
                    ],
                  )
                : ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.fromLTRB(16, 16, 16, 100),
                    children: [
                      Row(
                        children: [
                          Expanded(
                            child: _ViolationSummaryCard(
                              title: 'Số vi phạm',
                              value: '${violations.length}',
                              icon: Icons.warning_amber_rounded,
                              color: const Color(0xFFB45309),
                            ),
                          ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: _ViolationSummaryCard(
                              title: 'Tổng tiền phạt',
                              value: _money(totalFine),
                              icon: Icons.payments_outlined,
                              color: const Color(0xFFB91C1C),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 16),
                      ...violations.whereType<Map>().map((violation) {
                        final room = _value(violation, [
                          'tenPhong',
                          'TenPhong',
                          'phong',
                          'Phong',
                          'roomNumber',
                        ], 'Phòng chưa rõ');
                        final customer = _value(violation, [
                          'tenKhach',
                          'TenKhach',
                          'hoTen',
                          'HoTen',
                          'customerName',
                        ], 'Khách chưa rõ');
                        final description = _value(violation, [
                          'noiDung',
                          'NoiDung',
                          'moTa',
                          'MoTa',
                        ], 'Chưa có nội dung');
                        final fine = _value(violation, [
                          'tienPhat',
                          'TienPhat',
                          'fineAmount',
                        ], '0');
                        final invoiceId = _value(violation, [
                          'maHoaDon',
                          'MaHoaDon',
                        ]);
                        final status = _value(violation, [
                          'trangThai',
                          'TrangThai',
                        ], 'Chưa xử lý');

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
                                      width: 42,
                                      height: 42,
                                      decoration: BoxDecoration(
                                        color: const Color(0xFFFEF2F2),
                                        borderRadius: BorderRadius.circular(12),
                                      ),
                                      child: const Icon(
                                        Icons.warning_amber_rounded,
                                        color: Color(0xFFDC2626),
                                      ),
                                    ),
                                    const SizedBox(width: 12),
                                    Expanded(
                                      child: Column(
                                        crossAxisAlignment:
                                            CrossAxisAlignment.start,
                                        children: [
                                          Text(
                                            room,
                                            style: const TextStyle(
                                              fontWeight: FontWeight.bold,
                                              color: Color(0xFF172554),
                                            ),
                                          ),
                                          const SizedBox(height: 3),
                                          Text(
                                            customer,
                                            style: const TextStyle(
                                              color: Color(0xFF64748B),
                                              fontSize: 13,
                                            ),
                                          ),
                                        ],
                                      ),
                                    ),
                                    Text(
                                      _date(violation),
                                      style: const TextStyle(
                                        color: Color(0xFF64748B),
                                        fontSize: 11,
                                      ),
                                    ),
                                  ],
                                ),
                                const SizedBox(height: 12),
                                Text(
                                  description,
                                  style: const TextStyle(
                                    color: Color(0xFF334155),
                                  ),
                                ),
                                const SizedBox(height: 10),
                                Text(
                                  'Tiền phạt: ${_money(fine)}',
                                  style: const TextStyle(
                                    color: Color(0xFFB91C1C),
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                                const SizedBox(height: 6),
                                Text(
                                  invoiceId.isNotEmpty
                                      ? 'Đã cộng vào hóa đơn #$invoiceId'
                                      : status,
                                  style: TextStyle(
                                    color: invoiceId.isNotEmpty
                                        ? Colors.green.shade700
                                        : const Color(0xFF64748B),
                                    fontSize: 12,
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        );
                      }),
                    ],
                  ),
          );
        },
      ),
    );
  }
}

class _ViolationDraft {
  final int roomId;
  final int customerId;
  final String roomName;
  final String description;
  final double fine;

  const _ViolationDraft({
    required this.roomId,
    required this.customerId,
    required this.roomName,
    required this.description,
    required this.fine,
  });
}

class _TenantOption {
  final int roomId;
  final int customerId;
  final String roomName;
  final String customerName;

  const _TenantOption({
    required this.roomId,
    required this.customerId,
    required this.roomName,
    required this.customerName,
  });

  String get label => '$roomName • $customerName';
}

class _CreateViolationSheet extends StatefulWidget {
  final List<_TenantOption> tenants;

  const _CreateViolationSheet({required this.tenants});

  @override
  State<_CreateViolationSheet> createState() => _CreateViolationSheetState();
}

class _CreateViolationSheetState extends State<_CreateViolationSheet> {
  final _descriptionController = TextEditingController();
  final _fineController = TextEditingController(text: '0');
  late _TenantOption? _tenant;
  String? _validationMessage;

  @override
  void initState() {
    super.initState();
    _tenant = widget.tenants.firstOrNull;
  }

  @override
  void dispose() {
    _descriptionController.dispose();
    _fineController.dispose();
    super.dispose();
  }

  void _submit() {
    final description = _descriptionController.text.trim();
    final fine = double.tryParse(_fineController.text.trim());
    if (_tenant == null) {
      setState(() => _validationMessage = 'Chọn khách thuê và phòng hợp lệ.');
      return;
    }
    if (description.isEmpty) {
      setState(() => _validationMessage = 'Nhập nội dung vi phạm.');
      return;
    }
    if (fine == null || fine < 0) {
      setState(() => _validationMessage = 'Tiền phạt phải là số không âm.');
      return;
    }
    Navigator.pop(
      context,
      _ViolationDraft(
        roomId: _tenant!.roomId,
        customerId: _tenant!.customerId,
        roomName: _tenant!.roomName,
        description: description,
        fine: fine,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(
          bottom: MediaQuery.of(context).viewInsets.bottom,
        ),
        child: Container(
          padding: const EdgeInsets.fromLTRB(20, 20, 20, 24),
          decoration: const BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.vertical(top: Radius.circular(22)),
          ),
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Text(
                  'Ghi nhận vi phạm',
                  style: TextStyle(
                    fontSize: 19,
                    fontWeight: FontWeight.bold,
                    color: Color(0xFF172554),
                  ),
                ),
                const SizedBox(height: 18),
                DropdownButtonFormField<_TenantOption>(
                  initialValue: _tenant,
                  decoration: const InputDecoration(
                    labelText: 'Khách thuê và phòng đang thuê',
                    border: OutlineInputBorder(),
                    prefixIcon: Icon(Icons.person_pin_circle_outlined),
                  ),
                  items: widget.tenants
                      .map(
                        (tenant) => DropdownMenuItem(
                          value: tenant,
                          child: Text(
                            tenant.label,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      )
                      .toList(),
                  onChanged: (value) => setState(() => _tenant = value),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _descriptionController,
                  maxLines: 3,
                  decoration: const InputDecoration(
                    labelText: 'Nội dung vi phạm',
                    hintText: 'Mô tả sự việc và thời điểm xảy ra',
                    border: OutlineInputBorder(),
                    prefixIcon: Icon(Icons.notes_rounded),
                    alignLabelWithHint: true,
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _fineController,
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  decoration: const InputDecoration(
                    labelText: 'Tiền phạt (VNĐ)',
                    border: OutlineInputBorder(),
                    prefixIcon: Icon(Icons.payments_outlined),
                  ),
                ),
                if (_validationMessage != null) ...[
                  const SizedBox(height: 10),
                  Text(
                    _validationMessage!,
                    style: const TextStyle(color: Colors.redAccent),
                  ),
                ],
                const SizedBox(height: 18),
                FilledButton.icon(
                  onPressed: _submit,
                  icon: const Icon(Icons.save_outlined),
                  label: const Text('Lưu vi phạm'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _ViolationSummaryCard extends StatelessWidget {
  final String title;
  final String value;
  final IconData icon;
  final Color color;

  const _ViolationSummaryCard({
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
          Icon(icon, color: color),
          const SizedBox(height: 10),
          Text(
            value,
            style: TextStyle(
              fontWeight: FontWeight.bold,
              fontSize: 18,
              color: color,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            title,
            style: const TextStyle(fontSize: 12, color: Color(0xFF64748B)),
          ),
        ],
      ),
    );
  }
}

class _ViolationLoadError extends StatelessWidget {
  final String message;
  final Future<void> Function() onRetry;

  const _ViolationLoadError({required this.message, required this.onRetry});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.cloud_off_outlined, size: 48),
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
