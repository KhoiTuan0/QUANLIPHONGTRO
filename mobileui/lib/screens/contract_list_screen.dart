import 'package:flutter/material.dart';
import '../services/api_service.dart';
import 'admin_chat_screen.dart'; // Màn hình chat của Admin
import 'create_contract_screen.dart';

class ContractListScreen extends StatefulWidget {
  const ContractListScreen({super.key});

  @override
  State<ContractListScreen> createState() => _ContractListScreenState();
}

class _ContractListScreenState extends State<ContractListScreen> {
  late Future<List<dynamic>> _contractsFuture;
  String _selectedFilter = 'Tất cả';
  Map<int, String> _customerNames = {};
  bool _loadingCustomerNames = true;
  String? _customerLookupError;

  @override
  void initState() {
    super.initState();
    _contractsFuture = ApiService.getContracts();
    _loadCustomerNames();
  }

  void _loadData() {
    setState(() {
      _contractsFuture = ApiService.getContracts();
      _loadingCustomerNames = true;
      _customerLookupError = null;
    });
    _loadCustomerNames();
  }

  Future<void> _startContractCreation() async {
    List<dynamic> rooms;
    try {
      rooms = await ApiService.getRoomsForContractCreation();
    } catch (error) {
      if (mounted) {
        _showSnackBar('Không tải được danh sách phòng: $error', isError: true);
      }
      return;
    }
    if (!mounted) return;

    final availableRooms = rooms
        .whereType<Map>()
        .where((room) {
          final status =
              (room['trangThai'] ??
                      room['TrangThai'] ??
                      room['status'] ??
                      room['Status'] ??
                      '')
                  .toString()
                  .trim()
                  .toLowerCase();
          return status == 'trống' ||
              status == 'available' ||
              status == 'vacant';
        })
        .map(Map<String, dynamic>.from)
        .toList();

    if (availableRooms.isEmpty) {
      _showSnackBar(
        'Hiện không có phòng trống để lập hợp đồng.',
        isError: true,
      );
      return;
    }

    final room = await showModalBottomSheet<Map<String, dynamic>>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (context) => SafeArea(
        child: Container(
          constraints: BoxConstraints(
            maxHeight: MediaQuery.sizeOf(context).height * 0.7,
          ),
          decoration: const BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.vertical(top: Radius.circular(22)),
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 18, 20, 12),
                child: Row(
                  children: [
                    const Expanded(
                      child: Text(
                        'Chọn phòng để lập hợp đồng',
                        style: TextStyle(
                          fontSize: 17,
                          fontWeight: FontWeight.bold,
                          color: Color(0xFF1E293B),
                        ),
                      ),
                    ),
                    IconButton(
                      tooltip: 'Đóng',
                      onPressed: () => Navigator.pop(context),
                      icon: const Icon(Icons.close),
                    ),
                  ],
                ),
              ),
              const Divider(height: 1),
              Flexible(
                child: ListView.separated(
                  shrinkWrap: true,
                  itemCount: availableRooms.length,
                  separatorBuilder: (_, _) =>
                      const Divider(height: 1, indent: 64),
                  itemBuilder: (context, index) {
                    final availableRoom = availableRooms[index];
                    final roomName =
                        availableRoom['tenPhong'] ??
                        availableRoom['TenPhong'] ??
                        availableRoom['roomNumber'] ??
                        availableRoom['RoomNumber'] ??
                        'Phòng';
                    final monthlyRent =
                        availableRoom['giaThue'] ??
                        availableRoom['GiaThue'] ??
                        availableRoom['basePrice'] ??
                        availableRoom['BasePrice'] ??
                        0;
                    return ListTile(
                      leading: const CircleAvatar(
                        backgroundColor: Color(0xFFEFF6FF),
                        child: Icon(
                          Icons.meeting_room_outlined,
                          color: Color(0xFF2563EB),
                        ),
                      ),
                      title: Text(
                        roomName.toString(),
                        style: const TextStyle(fontWeight: FontWeight.w600),
                      ),
                      subtitle: Text('Giá thuê: $monthlyRent đ/tháng'),
                      trailing: const Icon(Icons.chevron_right),
                      onTap: () => Navigator.pop(context, availableRoom),
                    );
                  },
                ),
              ),
              const SizedBox(height: 8),
            ],
          ),
        ),
      ),
    );
    if (room == null || !mounted) return;

    final created = await Navigator.push<bool>(
      context,
      MaterialPageRoute(builder: (_) => CreateContractScreen(room: room)),
    );
    if (created == true && mounted) _loadData();
  }

  Future<void> _loadCustomerNames() async {
    try {
      final customers = await ApiService.getCustomersForRentBilling();
      final names = <int, String>{};
      for (final value in customers.whereType<Map>()) {
        final customer = Map<String, dynamic>.from(value);
        final customerId = _customerIdFromMap(customer);
        final name = _customerNameFromMap(customer);
        if (customerId != null && name != null) names[customerId] = name;
      }
      if (!mounted) return;
      setState(() {
        _customerNames = names;
        _loadingCustomerNames = false;
        _customerLookupError = null;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loadingCustomerNames = false;
        _customerLookupError = error.toString();
      });
    }
  }

  int? _customerIdFromMap(Map value) {
    for (final key in [
      'maKhach',
      'MaKhach',
      'maKhachThue',
      'MaKhachThue',
      'customerId',
      'CustomerId',
      'maKhachThue',
      'MaKhachThue',
      'id',
      'Id',
    ]) {
      final id = int.tryParse(value[key]?.toString() ?? '');
      if (id != null && id > 0) return id;
    }
    return null;
  }

  String? _customerNameFromMap(Map value) {
    for (final key in [
      'hoTen',
      'HoTen',
      'tenKhach',
      'TenKhach',
      'tenKhachHang',
      'TenKhachHang',
      'customerName',
      'CustomerName',
      'fullName',
      'FullName',
    ]) {
      final name = value[key]?.toString().trim();
      if (name != null && name.isNotEmpty) return name;
    }
    return null;
  }

  String _customerDisplayName(dynamic contract) {
    if (contract is! Map) return 'Chưa rõ';
    final directName = _customerNameFromMap(contract);
    if (directName != null) return directName;

    for (final key in [
      'maKhachDaiDienNavigation',
      'MaKhachDaiDienNavigation',
      'khachDaiDienNavigation',
      'KhachDaiDienNavigation',
      'maKhachThueNavigation',
      'MaKhachThueNavigation',
      'maKhachNavigation',
      'MaKhachNavigation',
      'khachThue',
      'KhachThue',
      'customer',
      'Customer',
    ]) {
      final nested = contract[key];
      if (nested is Map) {
        final nestedName = _customerNameFromMap(nested);
        if (nestedName != null) return nestedName;
      }
    }

    final customerId = _customerIdFromContract(contract);
    if (customerId != null) {
      final name = _customerNames[customerId];
      if (name != null) return name;
      return _loadingCustomerNames
          ? 'Đang tải tên khách...'
          : 'Chưa rõ (mã khách #$customerId)';
    }
    return 'Chưa gán khách thuê';
  }

  Future<void> _confirmCheckOut(
    int contractId,
    String roomName,
    double deposit,
  ) async {
    final depositController = TextEditingController(
      text: deposit.toStringAsFixed(0),
    );
    final penaltyController = TextEditingController(text: '0');
    final noteController = TextEditingController(
      text: 'Admin thanh lý trực tiếp',
    );
    bool isSaving = false;
    await showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(16),
          ),
          title: const Text('Quyết toán trả phòng'),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Hợp đồng #$contractId • Phòng $roomName'),
                const SizedBox(height: 8),
                Text('Tiền cọc hợp đồng: ${deposit.toStringAsFixed(0)} đ'),
                const SizedBox(height: 12),
                TextField(
                  controller: depositController,
                  enabled: !isSaving,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(
                    labelText: 'Tiền cọc đưa vào quyết toán (VNĐ)',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: penaltyController,
                  enabled: !isSaving,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(
                    labelText: 'Tiền phạt (VNĐ)',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: noteController,
                  enabled: !isSaving,
                  maxLines: 2,
                  decoration: const InputDecoration(
                    labelText: 'Ghi chú thanh lý',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 10),
                const Text(
                  'Server sẽ tự cấn trừ công nợ hóa đơn và tiền phạt, '
                  'sau đó xác định số tiền thực hoàn cho khách.',
                  style: TextStyle(fontSize: 12, color: Color(0xFF64748B)),
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: isSaving ? null : () => Navigator.pop(dialogContext),
              child: const Text('Đóng'),
            ),
            ElevatedButton(
              onPressed: isSaving ? null : () async {
                double? parseMoney(String text) => double.tryParse(
                  text.trim().replaceAll(',', '').replaceAll(' ', ''),
                );
                final amount = parseMoney(depositController.text);
                final penalty = parseMoney(penaltyController.text);
                if (amount == null || !amount.isFinite || amount < 0 ||
                    amount > deposit || penalty == null ||
                    !penalty.isFinite || penalty < 0) {
                  _showSnackBar(
                    'Tiền quyết toán phải từ 0 đến tiền cọc; tiền phạt không âm.',
                    isError: true,
                  );
                  return;
                }
                setDialogState(() => isSaving = true);
                Map<String, dynamic> result;
                try {
                  result = await ApiService.checkOut(
                    contractId: contractId,
                    tienHoanCoc: amount,
                    tienPhat: penalty,
                    ghiChu: noteController.text,
                  );
                } catch (error) {
                  if (!mounted || !dialogContext.mounted) return;
                  setDialogState(() => isSaving = false);
                  _showSnackBar('Không thể thanh lý: $error', isError: true);
                  return;
                }
                if (!mounted || !dialogContext.mounted) return;
                if (result['success'] == true) {
                  Navigator.pop(dialogContext);
                  _showSnackBar(
                    result['message']?.toString() ?? 'Thanh lý thành công!',
                  );
                  _loadData();
                } else {
                  setDialogState(() => isSaving = false);
                  _showSnackBar(
                    result['message']?.toString() ?? 'Thanh lý thất bại.',
                    isError: true,
                  );
                }
              },
              style: ElevatedButton.styleFrom(
                backgroundColor: const Color(0xFF4F46E5),
                foregroundColor: Colors.white,
              ),
              child: Text(isSaving ? 'Đang xử lý...' : 'Xác nhận thanh lý'),
            ),
          ],
        ),
      ),
    );
    // Chờ animation đóng Dialog kết thúc rồi mới giải phóng controller.
    await Future<void>.delayed(const Duration(milliseconds: 350));
    depositController.dispose();
    penaltyController.dispose();
    noteController.dispose();
  }

  void _showSnackBar(String message, {bool isError = false}) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: isError ? Colors.redAccent : Colors.green,
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      ),
    );
  }

  int? _customerIdFromContract(dynamic contract) {
    if (contract is! Map) return null;

    int? parseId(dynamic value) {
      final id = int.tryParse(value?.toString() ?? '');
      return id != null && id > 0 ? id : null;
    }

    // Hỗ trợ cả DTO và entity JSON PascalCase; tuyệt đối không dùng mã khách mặc định.
    for (final key in [
      'maKhachDaiDien',
      'MaKhachDaiDien',
      'maKhachThue',
      'MaKhachThue',
      'maKhach',
      'MaKhach',
      'customerId',
      'CustomerId',
      'khachThueId',
      'KhachThueId',
    ]) {
      final id = parseId(contract[key]);
      if (id != null) return id;
    }

    for (final key in [
      'maKhachDaiDienNavigation',
      'MaKhachDaiDienNavigation',
      'khachDaiDienNavigation',
      'KhachDaiDienNavigation',
      'maKhachThueNavigation',
      'MaKhachThueNavigation',
      'maKhachNavigation',
      'MaKhachNavigation',
      'khachThue',
      'KhachThue',
      'customer',
      'Customer',
    ]) {
      final customer = contract[key];
      if (customer is! Map) continue;
      for (final idKey in [
        'maKhach',
        'MaKhach',
        'customerId',
        'CustomerId',
        'id',
        'Id',
      ]) {
        final id = parseId(customer[idKey]);
        if (id != null) return id;
      }
    }
    return null;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      appBar: AppBar(
        title: const Text(
          'Quản Lý Hợp Đồng',
          style: TextStyle(
            fontWeight: FontWeight.bold,
            fontSize: 18,
            color: Color(0xFF1E293B),
          ),
        ),
        backgroundColor: Colors.white,
        foregroundColor: const Color(0xFF1E293B),
        elevation: 0,
        centerTitle: true,
        actions: [
          IconButton(
            tooltip: 'Lập hợp đồng mới',
            onPressed: _startContractCreation,
            icon: const Icon(Icons.add_circle_outline),
          ),
          const SizedBox(width: 8),
        ],
      ),
      body: FutureBuilder<List<dynamic>>(
        future: _contractsFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(
              child: CircularProgressIndicator(color: Color(0xFF2563EB)),
            );
          }
          if (snapshot.hasError) {
            return Center(
              child: Text(
                'Lỗi tải dữ liệu: ${snapshot.error}',
                style: const TextStyle(color: Colors.red),
              ),
            );
          }

          final contracts = snapshot.data ?? [];
          final filteredContracts = _selectedFilter == 'Tất cả'
              ? contracts
              : contracts.where((c) {
                  final status = c['trangThai'] ?? c['status'] ?? 'Active';
                  bool active = status == 'Active' || status == 'Đang hiệu lực';
                  return active == (_selectedFilter == 'Đang hiệu lực');
                }).toList();

          return Column(
            children: [
              if (_customerLookupError != null)
                Container(
                  width: double.infinity,
                  margin: const EdgeInsets.fromLTRB(16, 8, 16, 0),
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: const Color(0xFFFFF7ED),
                    borderRadius: BorderRadius.circular(12),
                    border: Border.all(color: const Color(0xFFFED7AA)),
                  ),
                  child: Text(
                    'Không tải được tên khách thuê. Vui lòng thử tải lại. '
                    'Chi tiết: $_customerLookupError',
                    style: const TextStyle(
                      color: Color(0xFF9A3412),
                      fontSize: 12,
                    ),
                  ),
                ),
              Container(
                height: 60,
                padding: const EdgeInsets.symmetric(vertical: 10),
                child: ListView(
                  scrollDirection: Axis.horizontal,
                  padding: const EdgeInsets.symmetric(horizontal: 16),
                  children: ['Tất cả', 'Đang hiệu lực', 'Đã thanh lý'].map((
                    filter,
                  ) {
                    final isSelected = _selectedFilter == filter;
                    return Padding(
                      padding: const EdgeInsets.only(right: 8.0),
                      child: ChoiceChip(
                        label: Text(filter),
                        selected: isSelected,
                        selectedColor: const Color(0xFF2563EB),
                        labelStyle: TextStyle(
                          color: isSelected
                              ? Colors.white
                              : const Color(0xFF64748B),
                          fontWeight: FontWeight.bold,
                          fontSize: 13,
                        ),
                        backgroundColor: Colors.white,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(20),
                        ),
                        onSelected: (_) =>
                            setState(() => _selectedFilter = filter),
                      ),
                    );
                  }).toList(),
                ),
              ),
              Expanded(
                child: filteredContracts.isEmpty
                    ? Center(
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Icon(
                              Icons.description_outlined,
                              size: 64,
                              color: Colors.grey.shade400,
                            ),
                            const SizedBox(height: 12),
                            const Text(
                              'Không có hợp đồng nào phù hợp',
                              style: TextStyle(
                                color: Color(0xFF64748B),
                                fontSize: 16,
                              ),
                            ),
                          ],
                        ),
                      )
                    : RefreshIndicator(
                        color: const Color(0xFF2563EB),
                        onRefresh: () async => _loadData(),
                        child: ListView.builder(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 16,
                            vertical: 8,
                          ),
                          itemCount: filteredContracts.length,
                          itemBuilder: (context, index) {
                            final c = filteredContracts[index];
                            final contractId =
                                c['maHopDong'] ??
                                c['contractId'] ??
                                (index + 1);
                            final roomName =
                                c['roomNumber'] ?? c['tenPhong'] ?? 'Chưa rõ';
                            final customerName = _customerDisplayName(c);
                            final monthlyRent =
                                c['monthlyRent'] ?? c['giaThueThang'] ?? 0;
                            final deposit = c['deposit'] ?? c['tienCoc'] ?? 0;
                            final statusStr =
                                c['trangThai'] ?? c['status'] ?? 'Active';
                            final bool isActive =
                                statusStr == 'Active' ||
                                statusStr == 'Đang hiệu lực';
                            // Lấy đúng mã khách từ hợp đồng; thiếu mã thì không mở nhầm chat của KH #1.
                            final maKhach = _customerIdFromContract(c);

                            return Card(
                              elevation: 0,
                              margin: const EdgeInsets.only(bottom: 12),
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(16),
                                side: const BorderSide(
                                  color: Color(0xFFE2E8F0),
                                ),
                              ),
                              child: Padding(
                                padding: const EdgeInsets.all(16.0),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Row(
                                      mainAxisAlignment:
                                          MainAxisAlignment.spaceBetween,
                                      children: [
                                        Text(
                                          'Phòng $roomName (HĐ #$contractId)',
                                          style: const TextStyle(
                                            fontWeight: FontWeight.bold,
                                            fontSize: 16,
                                            color: Color(0xFF1E293B),
                                          ),
                                        ),
                                        Container(
                                          padding: const EdgeInsets.symmetric(
                                            horizontal: 10,
                                            vertical: 4,
                                          ),
                                          decoration: BoxDecoration(
                                            color: isActive
                                                ? Colors.green.withValues(alpha: 0.1)
                                                : Colors.grey.withValues(alpha: 0.1),
                                            borderRadius: BorderRadius.circular(
                                              20,
                                            ),
                                          ),
                                          child: Text(
                                            isActive
                                                ? "Đang hiệu lực"
                                                : "Đã thanh lý",
                                            style: TextStyle(
                                              color: isActive
                                                  ? Colors.green
                                                  : Colors.grey.shade700,
                                              fontWeight: FontWeight.bold,
                                              fontSize: 11,
                                            ),
                                          ),
                                        ),
                                      ],
                                    ),
                                    const Divider(
                                      height: 20,
                                      color: Color(0xFFE2E8F0),
                                    ),
                                    Row(
                                      children: [
                                        const Icon(
                                          Icons.person_outline,
                                          size: 16,
                                          color: Color(0xFF64748B),
                                        ),
                                        const SizedBox(width: 8),
                                        Expanded(child: Text(
                                          'Khách thuê đại diện: $customerName',
                                          style: const TextStyle(
                                            color: Color(0xFF1E293B),
                                            fontWeight: FontWeight.w600,
                                            fontSize: 14,
                                          ),
                                        )),
                                      ],
                                    ),
                                    const SizedBox(height: 6),
                                    Row(
                                      children: [
                                        const Icon(
                                          Icons.payments_outlined,
                                          size: 16,
                                          color: Color(0xFF2563EB),
                                        ),
                                        const SizedBox(width: 8),
                                        Expanded(child: Text(
                                          'Tiền thuê: $monthlyRent đ | Cọc: $deposit đ',
                                          style: const TextStyle(
                                            color: Color(0xFF2563EB),
                                            fontWeight: FontWeight.bold,
                                            fontSize: 13,
                                          ),
                                        )),
                                      ],
                                    ),
                                    const SizedBox(height: 12),
                                    Wrap(
                                      alignment: WrapAlignment.end,
                                      spacing: 8,
                                      runSpacing: 8,
                                      children: [
                                        OutlinedButton.icon(
                                          style: OutlinedButton.styleFrom(
                                            foregroundColor: const Color(
                                              0xFF2563EB,
                                            ),
                                            side: const BorderSide(
                                              color: Color(0xFF2563EB),
                                            ),
                                            shape: RoundedRectangleBorder(
                                              borderRadius:
                                                  BorderRadius.circular(10),
                                            ),
                                            padding: const EdgeInsets.symmetric(
                                              horizontal: 12,
                                              vertical: 6,
                                            ),
                                          ),
                                          icon: const Icon(
                                            Icons.chat_bubble_outline_rounded,
                                            size: 16,
                                          ),
                                          label: const Text(
                                            'Nhắn tin / Nhắc tiền',
                                            style: TextStyle(
                                              fontSize: 12,
                                              fontWeight: FontWeight.bold,
                                            ),
                                          ),
                                          onPressed: () {
                                            if (maKhach == null) {
                                              _showSnackBar(
                                                'Hợp đồng chưa có mã khách thuê. '
                                                'Hãy cập nhật API hợp đồng để trả về maKhachDaiDien.',
                                                isError: true,
                                              );
                                              return;
                                            }
                                            // Mở trang nhắn tin trực tiếp với khách này
                                            Navigator.push(
                                              context,
                                              MaterialPageRoute(
                                                builder: (context) =>
                                                    AdminChatScreen(
                                                      maKhach: maKhach,
                                                      tenKhach: customerName,
                                                    ),
                                              ),
                                            );
                                          },
                                        ),
                                        const SizedBox(width: 8),
                                        if (isActive)
                                          ElevatedButton.icon(
                                            style: ElevatedButton.styleFrom(
                                              backgroundColor:
                                                  Colors.red.shade50,
                                              foregroundColor: Colors.redAccent,
                                              elevation: 0,
                                              shape: RoundedRectangleBorder(
                                                borderRadius:
                                                    BorderRadius.circular(10),
                                              ),
                                              padding:
                                                  const EdgeInsets.symmetric(
                                                    horizontal: 12,
                                                    vertical: 6,
                                                  ),
                                            ),
                                            icon: const Icon(
                                              Icons.logout_rounded,
                                              size: 16,
                                            ),
                                            label: const Text(
                                              'Trả phòng',
                                              style: TextStyle(
                                                fontWeight: FontWeight.bold,
                                                fontSize: 12,
                                              ),
                                            ),
                                            onPressed: () => _confirmCheckOut(
                                              int.tryParse(contractId.toString()) ?? 0,
                                              roomName.toString(),
                                              double.tryParse(deposit.toString()) ?? 0,
                                            ),
                                          ),
                                      ],
                                    ),
                                  ],
                                ),
                              ),
                            );
                          },
                        ),
                      ),
              ),
            ],
          );
        },
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _startContractCreation,
        icon: const Icon(Icons.post_add_outlined),
        label: const Text('Lập hợp đồng'),
      ),
    );
  }
}
