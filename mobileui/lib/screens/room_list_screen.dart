import 'package:flutter/material.dart';
import '../services/api_service.dart';
import 'room_detail_screen.dart';

class RoomListScreen extends StatefulWidget {
  const RoomListScreen({super.key});

  @override
  State<RoomListScreen> createState() => _RoomListScreenState();
}

class _RoomListScreenState extends State<RoomListScreen> {
  late Future<List<dynamic>> _roomsFuture;
  List<dynamic> _currentRooms = [];
  String _selectedFilter = 'Tất cả';

  @override
  void initState() {
    super.initState();
    _roomsFuture = ApiService.getRooms();
  }

  void _loadRooms() {
    setState(() {
      _roomsFuture = ApiService.getRooms();
    });
  }

  Color _getStatusColor(String? status) {
    switch (status) {
      case 'Trống':
        return Colors.green;
      case 'Đang thuê':
        return Colors.blueAccent;
      case 'Đã đặt cọc':
        return Colors.orange;
      case 'Bảo trì':
        return Colors.redAccent;
      default:
        return Colors.grey;
    }
  }

  void _showAddRoomDialog() async {
    final tenPhongCtrl = TextEditingController();
    final tangCtrl = TextEditingController();
    final giaThueCtrl = TextEditingController();
    final moTaCtrl = TextEditingController();

    int? selectedMaLoaiPhong;
    List<dynamic> roomTypes = [];
    int? selectedMaCoSo;
    final facilityIds = _currentRooms
        .whereType<Map>()
        .map((room) => int.tryParse(
              (room['maCoSo'] ?? room['MaCoSo'] ?? '').toString(),
            ))
        .whereType<int>()
        .where((id) => id > 0)
        .toSet()
        .toList()..sort();
    if (facilityIds.isNotEmpty) selectedMaCoSo = facilityIds.first;
    bool isSaving = false;

    try {
      roomTypes = await ApiService.getRoomTypes();
      if (roomTypes.isNotEmpty) {
        final firstType = roomTypes.first;
        if (firstType is Map) {
          selectedMaLoaiPhong = int.tryParse(
            (firstType['maLoaiPhong'] ??
                        firstType['MaLoaiPhong'] ??
                        firstType['roomTypeId'] ??
                        firstType['RoomTypeId'])
                    ?.toString() ??
                '',
          );
        }
      }
    } catch (_) {}

    if (!mounted) return;

    showDialog(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (dialogContext, setDialogState) {
          return AlertDialog(
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(20),
            ),
            title: const Text(
              'Thêm Phòng Mới',
              style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18),
            ),
            content: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  TextField(
                    controller: tenPhongCtrl,
                    decoration: _dialogInputDecoration(
                      'Tên phòng (VD: Phòng A105)',
                      Icons.meeting_room_outlined,
                    ),
                  ),
                  const SizedBox(height: 14),
                  TextField(
                    controller: tangCtrl,
                    keyboardType: TextInputType.number,
                    decoration: _dialogInputDecoration(
                      'Tầng số',
                      Icons.layers_outlined,
                    ),
                  ),
                  const SizedBox(height: 14),
                  TextField(
                    controller: giaThueCtrl,
                    keyboardType: TextInputType.number,
                    decoration: _dialogInputDecoration(
                      'Giá thuê hàng tháng (VNĐ)',
                      Icons.payments_outlined,
                    ),
                  ),
                  const SizedBox(height: 14),
                  TextField(
                    controller: moTaCtrl,
                    maxLines: 2,
                    decoration: _dialogInputDecoration(
                      'Mô tả tiện ích phòng',
                      Icons.description_outlined,
                    ),
                  ),
                  const SizedBox(height: 14),
                  if (facilityIds.isNotEmpty)
                    DropdownButtonFormField<int>(
                      value: selectedMaCoSo,
                      isExpanded: true,
                      decoration: _dialogInputDecoration(
                        'Cơ sở quản lý', Icons.apartment_rounded,
                      ),
                      items: facilityIds.map((id) => DropdownMenuItem<int>(
                        value: id,
                        child: Text('Cơ sở #$id'),
                      )).toList(),
                      onChanged: isSaving ? null : (value) {
                        setDialogState(() => selectedMaCoSo = value);
                      },
                    )
                  else
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 8),
                      child: Text(
                        'Chưa có mã cơ sở trong dữ liệu phòng. '
                        'Cần API danh sách cơ sở để tạo phòng đầu tiên.',
                        style: TextStyle(color: Colors.deepOrange),
                      ),
                    ),
                  const SizedBox(height: 14),
                  if (roomTypes.isNotEmpty)
                    DropdownButtonFormField<int>(
                      value: selectedMaLoaiPhong,
                      isExpanded: true,
                      decoration: _dialogInputDecoration(
                        'Loại phòng',
                        Icons.category_outlined,
                      ),
                      items: roomTypes.where((type) {
                        final id = int.tryParse((type['maLoaiPhong'] ?? type['MaLoaiPhong'] ?? type['roomTypeId'] ?? type['RoomTypeId'])?.toString() ?? '');
                        return id != null && id > 0;
                      }).map<DropdownMenuItem<int>>((type) {
                        final typeId = int.tryParse(
                          (type['maLoaiPhong'] ??
                                      type['MaLoaiPhong'] ??
                                      type['roomTypeId'] ??
                                      type['RoomTypeId'])
                                  ?.toString() ??
                              '',
                        );
                        final typeName =
                            type['tenLoaiPhong'] ??
                            type['TenLoaiPhong'] ??
                            type['typeName'] ??
                            type['TypeName'] ??
                            'Loại phòng';
                        final basePrice =
                            type['giaMacDinh'] ??
                            type['GiaMacDinh'] ??
                            type['basePrice'] ??
                            type['BasePrice'] ??
                            0;
                        if (typeId == null) {
                          return const DropdownMenuItem<int>(
                            value: null,
                            enabled: false,
                            child: Text('Loại phòng không có mã hợp lệ'),
                          );
                        }
                        return DropdownMenuItem<int>(
                          value: typeId,
                          child: Text(
                            '$typeName ($basePrice đ)',
                            overflow: TextOverflow.ellipsis,
                          ),
                        );
                      }).toList(),
                      onChanged: (val) {
                        setDialogState(() {
                          selectedMaLoaiPhong = val;
                        });
                      },
                    ),
                ],
              ),
            ),
            actions: [
              TextButton(
                onPressed: isSaving ? null : () => Navigator.pop(dialogContext),
                child: const Text(
                  'Hủy',
                  style: TextStyle(color: Color(0xFF64748B)),
                ),
              ),
              ElevatedButton(
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF2563EB),
                  foregroundColor: Colors.white,
                  elevation: 0,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(10),
                  ),
                ),
                onPressed: isSaving ? null : () async {
                  final tenPhong = tenPhongCtrl.text.trim();
                  final tang = int.tryParse(tangCtrl.text.trim());
                  final giaThue = double.tryParse(
                    giaThueCtrl.text.trim().replaceAll(',', '.'),
                  );

                  if (tenPhong.isEmpty) {
                    _showSnackBar('Vui lòng nhập tên phòng.', isError: true);
                    return;
                  }
                  if (tang == null || tang < 0) {
                    _showSnackBar('Tầng phải là số nguyên không âm.', isError: true);
                    return;
                  }
                  if (giaThue == null || !giaThue.isFinite || giaThue <= 0) {
                    _showSnackBar('Giá thuê phải là số lớn hơn 0.', isError: true);
                    return;
                  }
                  if (selectedMaLoaiPhong == null || selectedMaLoaiPhong! <= 0) {
                    _showSnackBar('Vui lòng chọn loại phòng hợp lệ.', isError: true);
                    return;
                  }

                  if (selectedMaCoSo == null) {
                     _showSnackBar('Vui lòng chọn cơ sở trước khi thêm phòng.', isError: true);
                     return;
                   }

                  final isDuplicate = _currentRooms.any((r) =>
                    (r['tenPhong'] ?? r['TenPhong'] ?? r['roomNumber'] ?? '')
                      .toString().trim().toLowerCase() == tenPhong.toLowerCase());
                  if (isDuplicate) {
                    _showSnackBar('Phòng "$tenPhong" đã tồn tại!', isError: true);
                    return;
                  }

                  // MaPhong là khóa chính do backend/database cấp.
                  // Không gửi maPhong = 0 hoặc tự lấy số từ tên phòng.
                  final data = <String, dynamic>{
                    'tenPhong': tenPhong,
                    'tang': tang,
                    'giaThue': giaThue,
                    'trangThai': 'Trống',
                    'moTa': moTaCtrl.text.trim(),
                    'maLoaiPhong': selectedMaLoaiPhong,
                    'maCoSo': selectedMaCoSo,
                  };

                  setDialogState(() => isSaving = true);
                  try {
                    final success = await ApiService.createRoom(data);
                    if (!mounted || !dialogContext.mounted) return;
                    if (success) {
                      Navigator.pop(dialogContext);
                      _showSnackBar('Thêm phòng mới thành công!');
                      _loadRooms();
                    } else {
                      _showSnackBar('Thêm phòng thất bại. Kiểm tra dữ liệu và API.', isError: true);
                    }
                  } catch (e) {
                    if (mounted) {
                      _showSnackBar('Không thể thêm phòng: $e', isError: true);
                    }
                  } finally {
                    if (dialogContext.mounted) {
                      setDialogState(() => isSaving = false);
                    }
                  }
                },
                child: const Text(
                  'Thêm phòng',
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
              ),
            ],
          );
        },
      ),
    );
  }

  void _showSnackBar(String message, {bool isError = false}) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: isError ? Colors.redAccent : Colors.green,
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      ),
    );
  }

  InputDecoration _dialogInputDecoration(String label, IconData icon) {
    return InputDecoration(
      labelText: label,
      labelStyle: const TextStyle(color: Color(0xFF64748B), fontSize: 13),
      prefixIcon: Icon(icon, color: const Color(0xFF2563EB), size: 20),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(color: Color(0xFFCBD5E1)),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(color: Color(0xFFCBD5E1)),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(color: Color(0xFF2563EB), width: 2),
      ),
      filled: true,
      fillColor: const Color(0xFFF8FAFC),
      contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      floatingActionButton: FloatingActionButton.extended(
        backgroundColor: const Color(0xFF2563EB),
        foregroundColor: Colors.white,
        elevation: 2,
        onPressed: _showAddRoomDialog,
        icon: const Icon(Icons.add_rounded),
        label: const Text(
          'Thêm phòng',
          style: TextStyle(fontWeight: FontWeight.bold),
        ),
      ),
      body: FutureBuilder<List<dynamic>>(
        future: _roomsFuture,
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

          _currentRooms = snapshot.data ?? [];

          // Lọc danh sách theo trạng thái được chọn trên giao diện
          final filteredRooms = _selectedFilter == 'Tất cả'
              ? _currentRooms
              : _currentRooms
                    .where(
                      (r) => (r['trangThai'] ?? r['status']) == _selectedFilter,
                    )
                    .toList();

          return Column(
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 18, 16, 12),
                child: Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(17),
                  decoration: BoxDecoration(
                    gradient: const LinearGradient(colors: [Color(0xFF4F46E5), Color(0xFF818CF8)]),
                    borderRadius: BorderRadius.circular(22),
                    boxShadow: const [BoxShadow(color: Color(0x224F46E5), blurRadius: 16, offset: Offset(0, 6))],
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Row(children: [
                        Icon(Icons.auto_awesome_rounded, color: Color(0xFFFFE082), size: 20),
                        SizedBox(width: 8),
                        Text('Tổng quan phòng trọ', style: TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.bold)),
                      ]),
                      const SizedBox(height: 7),
                      Text('${_currentRooms.length} phòng đang quản lý • ${_currentRooms.where((r) => (r['trangThai'] ?? r['status']) == 'Trống').length} phòng trống',
                          style: const TextStyle(color: Color(0xFFE0E7FF), fontSize: 13)),
                    ],
                  ),
                ),
              ),
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
                child: Row(
                  children: [
                    const Icon(Icons.tune_rounded, color: Color(0xFF4F46E5)),
                    const SizedBox(width: 8),
                    const Expanded(child: Text('Danh sách phòng', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: Color(0xFF172554)))),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 12),
                      decoration: BoxDecoration(
                        color: Colors.white,
                        border: Border.all(color: const Color(0xFFC7D2FE)),
                        borderRadius: BorderRadius.circular(13),
                      ),
                      child: DropdownButtonHideUnderline(
                        child: DropdownButton<String>(
                          value: _selectedFilter,
                          borderRadius: BorderRadius.circular(16),
                          icon: const Icon(Icons.keyboard_arrow_down_rounded, color: Color(0xFF4F46E5)),
                          items: ['Tất cả', 'Trống', 'Đang thuê', 'Đã đặt cọc', 'Bảo trì']
                              .map((filter) => DropdownMenuItem(value: filter, child: Text(filter, style: const TextStyle(fontSize: 13))))
                              .toList(),
                          onChanged: (value) { if (value != null) setState(() => _selectedFilter = value); },
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              Expanded(
                child: filteredRooms.isEmpty
                    ? Center(
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Icon(
                              Icons.meeting_room_outlined,
                              size: 64,
                              color: Colors.grey.shade400,
                            ),
                            const SizedBox(height: 12),
                            Text(
                              'Không có phòng nào ở trạng thái "$_selectedFilter"',
                              style: const TextStyle(
                                color: Color(0xFF64748B),
                                fontSize: 15,
                              ),
                            ),
                          ],
                        ),
                      )
                    : RefreshIndicator(
                        color: const Color(0xFF2563EB),
                        onRefresh: () async => _loadRooms(),
                        child: ListView.builder(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 16,
                            vertical: 8,
                          ),
                          itemCount: filteredRooms.length,
                          itemBuilder: (context, index) {
                            final room = filteredRooms[index];
                            final tenPhong =
                                room['tenPhong'] ??
                                room['roomNumber'] ??
                                'Phòng';
                            final tang = room['tang'] ?? room['floor'] ?? 1;
                            final giaThue =
                                room['giaThue'] ?? room['basePrice'] ?? 0;
                            final trangThai =
                                room['trangThai'] ?? room['status'] ?? 'Trống';
                            final color = _getStatusColor(trangThai);

                            return Card(
                              elevation: 0,
                              margin: const EdgeInsets.only(bottom: 12),
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(20),
                                side: const BorderSide(
                                  color: Color(0xFFDCE4FA),
                                ),
                              ),
                              child: InkWell(
                                borderRadius: BorderRadius.circular(16),
                                onTap: () async {
                                  await Navigator.push(
                                    context,
                                    MaterialPageRoute(
                                      builder: (context) =>
                                          RoomDetailScreen(room: room),
                                    ),
                                  );
                                  _loadRooms();
                                },
                                child: Padding(
                                  padding: const EdgeInsets.all(16.0),
                                  child: Row(
                                    children: [
                                      Container(
                                        width: 50,
                                        height: 50,
                                        decoration: BoxDecoration(
                                          color: color.withOpacity(0.1),
                                          borderRadius: BorderRadius.circular(
                                            12,
                                          ),
                                        ),
                                        child: Icon(
                                          Icons.door_front_door_rounded,
                                          color: color,
                                          size: 26,
                                        ),
                                      ),
                                      const SizedBox(width: 16),
                                      Expanded(
                                        child: Column(
                                          crossAxisAlignment:
                                              CrossAxisAlignment.start,
                                          children: [
                                            Text(
                                              tenPhong,
                                              style: const TextStyle(
                                                fontWeight: FontWeight.bold,
                                                fontSize: 16,
                                                color: Color(0xFF1E293B),
                                              ),
                                            ),
                                            const SizedBox(height: 4),
                                            Text(
                                              'Tầng $tang • ${room['moTa'] ?? "Phòng trọ tiện nghi"}',
                                              style: const TextStyle(
                                                color: Color(0xFF64748B),
                                                fontSize: 13,
                                              ),
                                              maxLines: 1,
                                              overflow: TextOverflow.ellipsis,
                                            ),
                                            const SizedBox(height: 6),
                                            Text(
                                              '$giaThue VNĐ/tháng',
                                              style: const TextStyle(
                                                color: Color(0xFF2563EB),
                                                fontWeight: FontWeight.bold,
                                                fontSize: 14,
                                              ),
                                            ),
                                          ],
                                        ),
                                      ),
                                      Container(
                                        padding: const EdgeInsets.symmetric(
                                          horizontal: 10,
                                          vertical: 6,
                                        ),
                                        decoration: BoxDecoration(
                                          color: color.withOpacity(0.1),
                                          borderRadius: BorderRadius.circular(
                                            20,
                                          ),
                                        ),
                                        child: Text(
                                          trangThai,
                                          style: TextStyle(
                                            color: color,
                                            fontWeight: FontWeight.bold,
                                            fontSize: 12,
                                          ),
                                        ),
                                      ),
                                    ],
                                  ),
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
    );
  }
}
