import 'package:flutter/material.dart';
import '../services/api_service.dart';

class ContractExtensionManagementScreen extends StatefulWidget {
  const ContractExtensionManagementScreen({super.key});

  @override
  State<ContractExtensionManagementScreen> createState() =>
      _ContractExtensionManagementScreenState();
}

class _ContractExtensionManagementScreenState
    extends State<ContractExtensionManagementScreen> {
  static const primary = Color(0xFF4F46E5);

  late Future<List<dynamic>> _future;
  int? _processing;
  String _filter = 'Chờ duyệt';

  @override
  void initState() {
    super.initState();
    _refresh();
  }

  void _refresh() {
    final future = ApiService.getContractExtensionRequests();

    if (!mounted) return;

    setState(() {
      _future = future;
    });
  }

  dynamic _get(Map<String, dynamic> row, String key) =>
      row[key] ??
      row['${key[0].toUpperCase()}${key.substring(1)}'];

  int? _id(dynamic value) => int.tryParse('$value');

  String _date(dynamic value) {
    final dt = DateTime.tryParse('$value');
    if (dt == null) return '—';

    return '${dt.day.toString().padLeft(2, '0')}/'
        '${dt.month.toString().padLeft(2, '0')}/${dt.year}';
  }

  String _money(dynamic value) {
    final number = num.tryParse('$value') ?? 0;

    return '${number.round().toString().replaceAllMapped(
      RegExp(r'\B(?=(\d{3})+(?!\d))'),
      (_) => '.',
    )} đ';
  }

  Future<void> _process(int id, bool approve) async {
    if (_processing != null) return;

    final note = TextEditingController();
    String? comment;

    try {
      comment = await showDialog<String>(
        context: context,
        builder: (dialogContext) => AlertDialog(
          title: Text(
            approve ? 'Duyệt gia hạn?' : 'Từ chối gia hạn?',
          ),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  approve
                      ? 'Sau khi duyệt, ngày kết thúc hợp đồng '
                          'sẽ được cập nhật. Giá thuê giữ nguyên.'
                      : 'Hợp đồng sẽ không thay đổi khi từ chối.',
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: note,
                  maxLines: 2,
                  maxLength: 500,
                  decoration: const InputDecoration(
                    labelText: 'Ghi chú (không bắt buộc)',
                    border: OutlineInputBorder(),
                  ),
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(dialogContext),
              child: const Text('Đóng'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(
                dialogContext,
                note.text.trim(),
              ),
              child: const Text('Xác nhận'),
            ),
          ],
        ),
      );
    } finally {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        note.dispose();
      });
    }

    if (comment == null || !mounted) return;

    setState(() {
      _processing = id;
    });

    try {
      final result =
          await ApiService.processContractExtensionRequest(
        maGiaHan: id,
        approve: approve,
        ghiChu: comment,
      );

      if (!mounted) return;

      final ok = result['success'] == true;

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          backgroundColor: ok ? Colors.green : Colors.red,
          content: Text(
            '${result['message'] ?? (ok ? 'Đã xử lý.' : 'Xử lý thất bại.')}',
          ),
        ),
      );

      if (ok) {
        _refresh();
      }
    } catch (e) {
      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          backgroundColor: Colors.red,
          content: Text('Lỗi: $e'),
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _processing = null;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: const Text('Quản lý gia hạn hợp đồng'),
        backgroundColor: Colors.white,
        foregroundColor: const Color(0xFF172554),
        actions: [
          IconButton(
            tooltip: 'Tải lại',
            onPressed: _refresh,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(12),
            child: SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: [
                  for (final status in [
                    'Chờ duyệt',
                    'Đã duyệt',
                    'Từ chối',
                    'Tất cả',
                  ])
                    Padding(
                      padding: const EdgeInsets.only(right: 8),
                      child: ChoiceChip(
                        label: Text(status),
                        selected: _filter == status,
                        onSelected: (_) {
                          setState(() {
                            _filter = status;
                          });
                        },
                      ),
                    ),
                ],
              ),
            ),
          ),
          Expanded(
            child: FutureBuilder<List<dynamic>>(
              future: _future,
              builder: (context, snapshot) {
                if (snapshot.connectionState !=
                    ConnectionState.done) {
                  return const Center(
                    child: CircularProgressIndicator(),
                  );
                }

                if (snapshot.hasError) {
                  return Center(
                    child: Padding(
                      padding: const EdgeInsets.all(20),
                      child: Text(
                        'Không tải được yêu cầu: ${snapshot.error}',
                        textAlign: TextAlign.center,
                      ),
                    ),
                  );
                }

                final items = (snapshot.data ?? [])
                    .whereType<Map>()
                    .map(
                      (x) => Map<String, dynamic>.from(x),
                    )
                    .where(
                      (x) =>
                          _filter == 'Tất cả' ||
                          _get(x, 'trangThai') == _filter,
                    )
                    .toList();

                return RefreshIndicator(
                  onRefresh: () async {
                    _refresh();

                    try {
                      await _future;
                    } catch (e) {
                      debugPrint('Lỗi tải yêu cầu: $e');
                    }
                  },
                  child: ListView.builder(
                    physics:
                        const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.all(14),
                    itemCount:
                        items.isEmpty ? 1 : items.length,
                    itemBuilder: (context, index) {
                      if (items.isEmpty) {
                        return const Padding(
                          padding: EdgeInsets.only(top: 100),
                          child: Center(
                            child: Text(
                              'Chưa có yêu cầu phù hợp.',
                            ),
                          ),
                        );
                      }

                      final item = items[index];

                      final id =
                          _id(_get(item, 'maGiaHan'));

                      final status =
                          '${_get(item, 'trangThai')}';

                      return Card(
                        color: Colors.white,
                        margin:
                            const EdgeInsets.only(bottom: 12),
                        shape: RoundedRectangleBorder(
                          borderRadius:
                              BorderRadius.circular(16),
                        ),
                        child: Padding(
                          padding: const EdgeInsets.all(16),
                          child: Column(
                            crossAxisAlignment:
                                CrossAxisAlignment.start,
                            children: [
                              Text(
                                '${_get(item, 'tenPhong') ?? 'Phòng #${_get(item, 'maPhong')}'}'
                                ' · HĐ #${_get(item, 'maHopDong')}',
                                style: const TextStyle(
                                  fontSize: 16,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              const SizedBox(height: 8),
                              Text(
                                'Khách: ${_get(item, 'tenKhach') ?? 'Khách #${_get(item, 'maKhach')}'}',
                              ),
                              const SizedBox(height: 6),
                              Text(
                                'Hạn cũ: ${_date(_get(item, 'ngayKetThucCu'))}',
                              ),
                              Text(
                                'Hạn đề xuất: ${_date(_get(item, 'ngayKetThucMoi'))}',
                              ),
                              Text(
                                'Giá thuê giữ nguyên: ${_money(_get(item, 'giaThueMoi'))}',
                              ),
                              const SizedBox(height: 10),
                              Text(
                                'Trạng thái: $status',
                                style: const TextStyle(
                                  color: primary,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              if (status == 'Chờ duyệt' &&
                                  id != null) ...[
                                const SizedBox(height: 14),
                                Row(
                                  children: [
                                    Expanded(
                                      child: OutlinedButton(
                                        onPressed:
                                            _processing == null
                                                ? () => _process(
                                                      id,
                                                      false,
                                                    )
                                                : null,
                                        child:
                                            const Text('Từ chối'),
                                      ),
                                    ),
                                    const SizedBox(width: 10),
                                    Expanded(
                                      child: FilledButton(
                                        style:
                                            FilledButton.styleFrom(
                                          backgroundColor:
                                              primary,
                                        ),
                                        onPressed:
                                            _processing == null
                                                ? () => _process(
                                                      id,
                                                      true,
                                                    )
                                                : null,
                                        child: Text(
                                          _processing == id
                                              ? 'Đang xử lý...'
                                              : 'Duyệt',
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                              ],
                            ],
                          ),
                        ),
                      );
                    },
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}