import 'package:flutter/material.dart';
import '../services/api_service.dart';

class CheckoutRequestManagementScreen extends StatefulWidget {
  const CheckoutRequestManagementScreen({super.key});

  @override
  State<CheckoutRequestManagementScreen> createState() =>
      _CheckoutRequestManagementScreenState();
}

class _CheckoutRequestManagementScreenState
    extends State<CheckoutRequestManagementScreen> {
  late Future<List<dynamic>> _requests;
  int? _processing;
  String _filter = 'Tất cả';

  @override
  void initState() {
    super.initState();
    // Không gọi setState trong initState: chỉ khởi tạo Future.
    _requests = ApiService.getCheckoutRequests();
  }

  void _reload() {
    // Không trả Future từ callback của setState.
    final nextRequest = ApiService.getCheckoutRequests();
    if (!mounted) return;
    setState(() {
      _requests = nextRequest;
    });
  }

  Future<void> _refresh() async {
    _reload();
    try {
      await _requests;
    } catch (error) {
      debugPrint('Không tải được yêu cầu trả phòng: $error');
      // FutureBuilder sẽ hiển thị lỗi cho người dùng.
    }
  }

  Future<void> _handle(int id, bool approve) async {
    if (_processing != null) return;

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(
          approve ? 'Duyệt yêu cầu trả phòng?' : 'Từ chối yêu cầu?',
        ),
        content: Text(
          approve
              ? 'Duyệt yêu cầu chỉ chuyển sang Chờ thanh lý. Tiền cọc và hợp đồng chưa được quyết toán.'
              : 'Xác nhận từ chối yêu cầu trả phòng này?',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, false),
            child: const Text('Hủy'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(dialogContext, true),
            child: const Text('Xác nhận'),
          ),
        ],
      ),
    );

    if (confirmed != true || !mounted) return;

    setState(() {
      _processing = id;
    });

    try {
      final result = await ApiService.processCheckoutRequest(
        id,
        approve: approve,
      );
      if (!mounted) return;

      final success = result['success'] == true;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            result['message']?.toString() ??
                (success ? 'Đã xử lý yêu cầu.' : 'Xử lý yêu cầu thất bại.'),
          ),
          backgroundColor: success ? Colors.green : Colors.red,
        ),
      );

      if (success) _reload();
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Không thể xử lý yêu cầu: $error'),
          backgroundColor: Colors.red,
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

  String _value(Map<String, dynamic> row, String key) {
    final value = row[key]?.toString().trim();
    return value == null || value.isEmpty ? '—' : value;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: const Text('Quản lý yêu cầu trả phòng'),
        actions: [
          IconButton(
            tooltip: 'Tải lại danh sách',
            onPressed: _reload,
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
              child: SegmentedButton<String>(
                segments: const [
                  ButtonSegment(
                    value: 'Tất cả',
                    label: Text('Tất cả'),
                  ),
                  ButtonSegment(
                    value: 'Chờ xử lý',
                    label: Text('Chờ xử lý'),
                  ),
                  ButtonSegment(
                    value: 'Chờ thanh lý',
                    label: Text('Đã duyệt'),
                  ),
                ],
                selected: {_filter},
                onSelectionChanged: (selection) {
                  setState(() {
                    _filter = selection.first;
                  });
                },
              ),
            ),
          ),
          Expanded(
            child: FutureBuilder<List<dynamic>>(
              future: _requests,
              builder: (context, snapshot) {
                if (snapshot.connectionState != ConnectionState.done) {
                  return const Center(child: CircularProgressIndicator());
                }

                if (snapshot.hasError) {
                  return Center(
                    child: Padding(
                      padding: const EdgeInsets.all(24),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Icon(Icons.error_outline,
                              color: Colors.red, size: 42),
                          const SizedBox(height: 12),
                          Text(
                            'Không tải được yêu cầu:\n${snapshot.error}',
                            textAlign: TextAlign.center,
                          ),
                          const SizedBox(height: 12),
                          FilledButton.icon(
                            onPressed: _reload,
                            icon: const Icon(Icons.refresh),
                            label: const Text('Thử lại'),
                          ),
                        ],
                      ),
                    ),
                  );
                }

                final items = (snapshot.data ?? <dynamic>[])
                    .whereType<Map>()
                    .map((row) => Map<String, dynamic>.from(row))
                    .where((row) =>
                        _filter == 'Tất cả' || row['trangThai'] == _filter)
                    .toList();

                if (items.isEmpty) {
                  return RefreshIndicator(
                    onRefresh: _refresh,
                    child: ListView(
                      physics: const AlwaysScrollableScrollPhysics(),
                      children: const [
                        SizedBox(height: 140),
                        Center(child: Text('Chưa có yêu cầu phù hợp.')),
                      ],
                    ),
                  );
                }

                return RefreshIndicator(
                  onRefresh: _refresh,
                  child: ListView.builder(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.fromLTRB(12, 4, 12, 20),
                    itemCount: items.length,
                    itemBuilder: (context, index) {
                      final item = items[index];
                      final id = int.tryParse('${item['id']}') ?? 0;
                      final status = _value(item, 'trangThai');
                      final pending = status == 'Chờ xử lý';

                      return Card(
                        margin: const EdgeInsets.only(bottom: 12),
                        child: Padding(
                          padding: const EdgeInsets.all(16),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                'Phòng ${_value(item, 'tenPhong')} • HĐ #${_value(item, 'maHopDong')}',
                                style: const TextStyle(
                                  fontSize: 16,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              const SizedBox(height: 8),
                              Text(
                                'Khách: ${_value(item, 'tenKhach')} (#${_value(item, 'maKhach')})',
                              ),
                              Text('Ngày báo: ${_value(item, 'ngayBao')}'),
                              if (item['ngayTraDuKien'] != null)
                                Text(
                                  'Dự kiến trả: ${_value(item, 'ngayTraDuKien')}',
                                ),
                              Text('Lý do: ${_value(item, 'lyDo')}'),
                              if ((item['ghiChu']?.toString().trim() ?? '')
                                  .isNotEmpty)
                                Text('Ghi chú: ${_value(item, 'ghiChu')}'),
                              const SizedBox(height: 8),
                              Text(
                                'Trạng thái: $status',
                                style: const TextStyle(
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                              if (pending && id > 0) ...[
                                const SizedBox(height: 12),
                                Row(
                                  children: [
                                    Expanded(
                                      child: OutlinedButton(
                                        onPressed: _processing == null
                                            ? () => _handle(id, false)
                                            : null,
                                        child: const Text('Từ chối'),
                                      ),
                                    ),
                                    const SizedBox(width: 10),
                                    Expanded(
                                      child: FilledButton(
                                        onPressed: _processing == null
                                            ? () => _handle(id, true)
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
                              if (status == 'Chờ thanh lý')
                                const Padding(
                                  padding: EdgeInsets.only(top: 10),
                                  child: Text(
                                    'Đã duyệt. Vào Hợp đồng → Trả phòng để quyết toán tiền cọc.',
                                  ),
                                ),
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
