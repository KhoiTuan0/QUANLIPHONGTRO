import 'package:flutter/material.dart';
import '../services/api_service.dart';

class CustomerContractExtensionScreen extends StatefulWidget {
  final int maKhach;

  const CustomerContractExtensionScreen({
    super.key,
    required this.maKhach,
  });

  @override
  State<CustomerContractExtensionScreen> createState() =>
      _CustomerContractExtensionScreenState();
}

class _CustomerContractExtensionScreenState
    extends State<CustomerContractExtensionScreen> {
  static const Color primary = Color(0xFF4F46E5);

  bool _loading = true;
  bool _sending = false;
  String? _error;

  List<Map<String, dynamic>> _contracts = [];
  List<Map<String, dynamic>> _requests = [];

  int? _selectedContract;
  DateTime? _newEndDate;

  final TextEditingController _reason = TextEditingController();

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  dynamic _get(Map<String, dynamic> item, String key) {
    final upperKey =
        '${key[0].toUpperCase()}${key.substring(1)}';

    return item[key] ?? item[upperKey];
  }

  int? _id(dynamic value) {
    if (value == null) return null;
    return int.tryParse(value.toString());
  }

  String _date(dynamic value) {
    if (value == null) return '—';

    final date = DateTime.tryParse(value.toString());

    if (date == null) return '—';

    return '${date.day.toString().padLeft(2, '0')}/'
        '${date.month.toString().padLeft(2, '0')}/'
        '${date.year}';
  }

  String _iso(DateTime date) {
    return '${date.year.toString().padLeft(4, '0')}-'
        '${date.month.toString().padLeft(2, '0')}-'
        '${date.day.toString().padLeft(2, '0')}';
  }

  bool _isActiveContract(Map<String, dynamic> contract) {
    final status = (_get(contract, 'trangThai') ?? '')
        .toString()
        .trim()
        .toLowerCase();

    return status == 'hiệu lực' ||
        status == 'đang hiệu lực';
  }

  Map<String, dynamic>? _findSelectedContract() {
    for (final contract in _contracts) {
      if (_id(_get(contract, 'maHopDong')) ==
          _selectedContract) {
        return contract;
      }
    }

    return null;
  }

  bool _hasPendingRequest(int contractId) {
    return _requests.any((request) {
      final id = _id(_get(request, 'maHopDong'));

      final status = (_get(request, 'trangThai') ?? '')
          .toString()
          .trim()
          .toLowerCase();

      return id == contractId &&
          status == 'chờ duyệt';
    });
  }

  Future<void> _load() async {
    if (!mounted) return;

    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final results = await Future.wait<List<dynamic>>([
        ApiService.getContractsByCustomer(widget.maKhach),
        ApiService.getContractExtensionRequests(),
      ]);

      if (!mounted) return;

      final allContracts = results[0]
          .whereType<Map>()
          .map((item) => Map<String, dynamic>.from(item))
          .toList();

      final contracts = allContracts
          .where(_isActiveContract)
          .toList();

      final contractIds = allContracts
          .map((item) => _id(_get(item, 'maHopDong')))
          .whereType<int>()
          .toSet();

      final requests = results[1]
          .whereType<Map>()
          .map((item) => Map<String, dynamic>.from(item))
          .where((item) {
            final contractId =
                _id(_get(item, 'maHopDong'));

            return contractId != null &&
                contractIds.contains(contractId);
          })
          .toList();

      int? selected = _selectedContract;

      if (!contracts.any(
        (contract) =>
            _id(_get(contract, 'maHopDong')) == selected,
      )) {
        selected = contracts.isEmpty
            ? null
            : _id(_get(contracts.first, 'maHopDong'));
      }

      setState(() {
        if (_selectedContract != selected) {
          _newEndDate = null;
        }

        _selectedContract = selected;
        _contracts = contracts;
        _requests = requests;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _error = e.toString();
        _loading = false;
      });
    }
  }

  Future<void> _pickDate() async {
    final contract = _findSelectedContract();

    if (contract == null) {
      _showMessage('Vui lòng chọn hợp đồng.');
      return;
    }

    final oldEnd = DateTime.tryParse(
      '${_get(contract, 'ngayKetThuc') ?? ''}',
    );

    if (oldEnd == null) {
      _showMessage(
        'Không xác định được ngày kết thúc hợp đồng.',
      );
      return;
    }

    final today = DateUtils.dateOnly(DateTime.now());
    final minimum = DateUtils.dateOnly(oldEnd)
        .add(const Duration(days: 1));

    final firstDate = minimum.isAfter(today)
        ? minimum
        : today;

    final lastDate = DateTime(firstDate.year + 10, 12, 31);

    final initialDate = _newEndDate != null &&
            !_newEndDate!.isBefore(firstDate)
        ? _newEndDate!
        : firstDate;

    final selected = await showDatePicker(
      context: context,
      initialDate: initialDate,
      firstDate: firstDate,
      lastDate: lastDate,
      helpText: 'Chọn ngày kết thúc mới',
      cancelText: 'Hủy',
      confirmText: 'Chọn',
    );

    if (!mounted || selected == null) return;

    setState(() {
      _newEndDate = selected;
    });
  }

  void _showMessage(String message, {bool success = false}) {
    if (!mounted) return;

    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        backgroundColor:
            success ? Colors.green : Colors.redAccent,
        content: Text(message),
      ),
    );
  }

  Future<void> _submit() async {
    if (_sending) return;

    final contractId = _selectedContract;

    if (contractId == null) {
      _showMessage('Vui lòng chọn hợp đồng.');
      return;
    }

    if (_newEndDate == null) {
      _showMessage('Vui lòng chọn ngày kết thúc mới.');
      return;
    }

    final contract = _findSelectedContract();

    if (contract == null) {
      _showMessage('Không tìm thấy hợp đồng đã chọn.');
      return;
    }

    final oldEnd = DateTime.tryParse(
      '${_get(contract, 'ngayKetThuc') ?? ''}',
    );

    if (oldEnd == null ||
        !_newEndDate!.isAfter(oldEnd)) {
      _showMessage(
        'Ngày kết thúc mới phải sau ngày kết thúc cũ.',
      );
      return;
    }

    if (_hasPendingRequest(contractId)) {
      _showMessage(
        'Hợp đồng này đã có yêu cầu đang chờ duyệt.',
      );
      return;
    }

    setState(() {
      _sending = true;
    });

    try {
      final result =
          await ApiService.sendContractExtensionRequest(
        maHopDong: contractId,
        ngayKetThucMoi: _iso(_newEndDate!),
        lyDo: _reason.text.trim(),
      );

      if (!mounted) return;

      final success = result['success'] == true;

      _showMessage(
        result['message']?.toString() ??
            (success
                ? 'Đã gửi yêu cầu gia hạn.'
                : 'Không thể gửi yêu cầu.'),
        success: success,
      );

      if (success) {
        _reason.clear();

        setState(() {
          _newEndDate = null;
        });

        await _load();
      }
    } catch (e) {
      _showMessage('Lỗi gửi yêu cầu: $e');
    } finally {
      if (mounted) {
        setState(() {
          _sending = false;
        });
      }
    }
  }

  Widget _buildRequestCard(Map<String, dynamic> request) {
    final status =
        '${_get(request, 'trangThai') ?? 'Không xác định'}';

    Color statusColor = Colors.orange;

    if (status == 'Đã duyệt') {
      statusColor = Colors.green;
    } else if (status == 'Từ chối') {
      statusColor = Colors.red;
    }

    return Card(
      color: Colors.white,
      margin: const EdgeInsets.only(bottom: 10),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(14),
      ),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(
                  Icons.description_outlined,
                  color: primary,
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    'Hợp đồng #${_get(request, 'maHopDong')}',
                    style: const TextStyle(
                      fontWeight: FontWeight.bold,
                      fontSize: 15,
                    ),
                  ),
                ),
                Flexible(
                  child: Text(
                    status,
                    textAlign: TextAlign.end,
                    style: TextStyle(
                      color: statusColor,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 10),
            Text(
              'Ngày kết thúc cũ: '
              '${_date(_get(request, 'ngayKetThucCu'))}',
            ),
            const SizedBox(height: 4),
            Text(
              'Ngày kết thúc mới: '
              '${_date(_get(request, 'ngayKetThucMoi'))}',
            ),
            const SizedBox(height: 4),
            Text(
              'Ngày gửi: '
              '${_date(_get(request, 'ngayGiaHan'))}',
            ),
            if ((_get(request, 'ghiChu') ?? '')
                .toString()
                .trim()
                .isNotEmpty) ...[
              const SizedBox(height: 8),
              Text(
                'Ghi chú: ${_get(request, 'ghiChu')}',
                style: const TextStyle(
                  color: Color(0xFF64748B),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: const Text('Gia hạn hợp đồng'),
        backgroundColor: Colors.white,
        foregroundColor: const Color(0xFF172554),
        actions: [
          IconButton(
            tooltip: 'Làm mới',
            onPressed: _loading ? null : _load,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: _loading
          ? const Center(
              child: CircularProgressIndicator(),
            )
          : _error != null
              ? Center(
                  child: Padding(
                    padding: const EdgeInsets.all(20),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          'Không tải được dữ liệu:\n$_error',
                          textAlign: TextAlign.center,
                        ),
                        const SizedBox(height: 14),
                        FilledButton(
                          onPressed: _load,
                          child: const Text('Thử lại'),
                        ),
                      ],
                    ),
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _load,
                  child: ListView(
                    padding: const EdgeInsets.all(16),
                    children: [
                      Card(
                        color: Colors.white,
                        shape: RoundedRectangleBorder(
                          borderRadius:
                              BorderRadius.circular(16),
                        ),
                        child: Padding(
                          padding: const EdgeInsets.all(16),
                          child: Column(
                            crossAxisAlignment:
                                CrossAxisAlignment.stretch,
                            children: [
                              const Text(
                                'Gửi yêu cầu gia hạn',
                                style: TextStyle(
                                  fontWeight: FontWeight.bold,
                                  fontSize: 18,
                                ),
                              ),
                              const SizedBox(height: 14),

                              if (_contracts.isEmpty)
                                const Padding(
                                  padding:
                                      EdgeInsets.only(bottom: 12),
                                  child: Text(
                                    'Không có hợp đồng đang hiệu lực.',
                                  ),
                                ),

                              if (_contracts.isNotEmpty)
                                DropdownButtonFormField<int>(
                                  value: _selectedContract,
                                  isExpanded: true,
                                  decoration:
                                      const InputDecoration(
                                    labelText: 'Chọn hợp đồng',
                                    border: OutlineInputBorder(),
                                  ),
                                  items: _contracts.map((contract) {
                                    final id = _id(
                                      _get(contract, 'maHopDong'),
                                    )!;

                                    return DropdownMenuItem<int>(
                                      value: id,
                                      child: Text(
                                        'HĐ #$id · Phòng '
                                        '#${_get(contract, 'maPhong')}'
                                        ' · Hết hạn '
                                        '${_date(_get(contract, 'ngayKetThuc'))}',
                                        overflow:
                                            TextOverflow.ellipsis,
                                      ),
                                    );
                                  }).toList(),
                                  onChanged: (value) {
                                    setState(() {
                                      _selectedContract = value;
                                      _newEndDate = null;
                                    });
                                  },
                                ),

                              const SizedBox(height: 12),

                              OutlinedButton.icon(
                                onPressed:
                                    _selectedContract == null
                                        ? null
                                        : _pickDate,
                                icon: const Icon(
                                  Icons.calendar_month,
                                ),
                                label: Text(
                                  _newEndDate == null
                                      ? 'Chọn ngày kết thúc mới'
                                      : 'Ngày kết thúc mới: '
                                          '${_date(_newEndDate!.toIso8601String())}',
                                ),
                              ),

                              const SizedBox(height: 12),

                              TextField(
                                controller: _reason,
                                maxLines: 3,
                                maxLength: 500,
                                decoration:
                                    const InputDecoration(
                                  labelText:
                                      'Lý do (không bắt buộc)',
                                  border: OutlineInputBorder(),
                                ),
                              ),

                              const Text(
                                'Giá thuê được giữ nguyên '
                                'theo hợp đồng hiện tại.',
                                style: TextStyle(
                                  color: Color(0xFF64748B),
                                ),
                              ),

                              const SizedBox(height: 14),

                              FilledButton.icon(
                                onPressed: _sending ||
                                        _selectedContract == null ||
                                        _newEndDate == null
                                    ? null
                                    : _submit,
                                style: FilledButton.styleFrom(
                                  backgroundColor: primary,
                                ),
                                icon: _sending
                                    ? const SizedBox(
                                        width: 18,
                                        height: 18,
                                        child:
                                            CircularProgressIndicator(
                                          strokeWidth: 2,
                                          color: Colors.white,
                                        ),
                                      )
                                    : const Icon(Icons.send),
                                label: Text(
                                  _sending
                                      ? 'Đang gửi...'
                                      : 'Gửi yêu cầu',
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),

                      const SizedBox(height: 20),

                      const Text(
                        'Lịch sử yêu cầu',
                        style: TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                        ),
                      ),

                      const SizedBox(height: 10),

                      if (_requests.isEmpty)
                        const Card(
                          child: Padding(
                            padding: EdgeInsets.all(16),
                            child: Text(
                              'Chưa có yêu cầu gia hạn.',
                            ),
                          ),
                        ),

                      for (final request in _requests)
                        _buildRequestCard(request),

                      const SizedBox(height: 30),
                    ],
                  ),
                ),
    );
  }
}