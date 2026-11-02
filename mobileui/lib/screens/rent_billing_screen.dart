import 'dart:async';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../services/api_service.dart';
import '../utils/rent_calculator.dart';

class RentBillingScreen extends StatefulWidget {
  const RentBillingScreen({super.key});

  @override
  State<RentBillingScreen> createState() => _RentBillingScreenState();
}


class _RentBillingScreenState extends State<RentBillingScreen> {
  final _noteController = TextEditingController();
  final _electricOldController = TextEditingController();
  final _electricNewController = TextEditingController();
  final _electricPriceController = TextEditingController();
  final _waterOldController = TextEditingController();
  final _waterNewController = TextEditingController();
  final _waterPriceController = TextEditingController();
  List<Map<String, dynamic>> _activeContracts = [];
  List<Map<String, dynamic>> _pendingInvoiceViolations = [];
  int? _selectedContractId;
  XFile? _electricMeterPhoto;
  Uint8List? _electricMeterPhotoBytes;
  late DateTime _fromDate;
  late DateTime _throughDate;
  bool _loadingContracts = true;
  bool _loadingInvoiceViolations = false;
  bool _submitting = false;
  bool _readingElectricMeter = false;
  bool _loadingPreviousReadings = false;
  String? _previousReadingsInfo;
  int _previousReadingsRequest = 0;
  Completer<void>? _meterOcrAbort;
  String? _loadError;
  String? _customerLoadError;
  String? _invoiceViolationsError;

  @override
  void initState() {
    super.initState();
    final now = DateTime.now();
    _fromDate = DateTime(now.year, now.month, 1);
    _throughDate = DateTime(now.year, now.month + 1, 0);
    _loadContracts();
  }

  @override
  void dispose() {
    _noteController.dispose();
    _electricOldController.dispose();
    _electricNewController.dispose();
    _electricPriceController.dispose();
    _waterOldController.dispose();
    _waterNewController.dispose();
    _waterPriceController.dispose();
    super.dispose();
  }

  void _clearUtilityReadings() {
    _previousReadingsRequest++;
    _loadingPreviousReadings = false;
    _previousReadingsInfo = null;
    _electricOldController.clear();
    _electricNewController.clear();
    _electricPriceController.clear();
    _waterOldController.clear();
    _waterNewController.clear();
    _waterPriceController.clear();
    _electricMeterPhoto = null;
    _electricMeterPhotoBytes = null;
  }

  double? _readNumber(TextEditingController controller) {
    final text = controller.text.trim();
    if (text.isEmpty) return null;
    return double.tryParse(text.replaceAll(',', '.'));
  }

  String? get _utilityValidationError {
    final groups = [
      (
        title: 'điện',
        old: _electricOldController,
        current: _electricNewController,
        price: _electricPriceController,
      ),
      (
        title: 'nước',
        old: _waterOldController,
        current: _waterNewController,
        price: _waterPriceController,
      ),
    ];

    for (final group in groups) {
      final values = [
        group.old.text.trim(),
        group.current.text.trim(),
        group.price.text.trim(),
      ];
      if (values.every((value) => value.isEmpty)) continue;
      if (values.any((value) => value.isEmpty)) {
        return 'Vui lòng nhập đủ chỉ số cũ, chỉ số mới và đơn giá ${group.title}.';
      }

      final oldReading = _readNumber(group.old);
      final newReading = _readNumber(group.current);
      final unitPrice = _readNumber(group.price);
      if (oldReading == null || newReading == null || unitPrice == null) {
        return 'Chỉ số và đơn giá ${group.title} phải là số hợp lệ.';
      }
      if (!oldReading.isFinite || !newReading.isFinite || !unitPrice.isFinite) {
        return 'Chỉ số và đơn giá ${group.title} phải là số hữu hạn.';
      }
      if (oldReading < 0 || newReading < 0 || unitPrice <= 0) {
        return 'Chỉ số ${group.title} không được âm và đơn giá phải lớn hơn 0.';
      }
      if (newReading < oldReading) {
        return 'Chỉ số mới ${group.title} không được nhỏ hơn chỉ số cũ.';
      }
    }
    return null;
  }

  double get _electricityCost {
    final oldReading = _readNumber(_electricOldController);
    final newReading = _readNumber(_electricNewController);
    final price = _readNumber(_electricPriceController);
    if (oldReading == null ||
        newReading == null ||
        price == null ||
        !oldReading.isFinite ||
        !newReading.isFinite ||
        !price.isFinite ||
        oldReading < 0 ||
        newReading < oldReading ||
        price <= 0) {
      return 0;
    }
    return calculateMeterCharge(
      oldReading: oldReading,
      currentReading: newReading,
      unitPrice: price,
    );
  }

  double get _waterCost {
    final oldReading = _readNumber(_waterOldController);
    final newReading = _readNumber(_waterNewController);
    final price = _readNumber(_waterPriceController);
    if (oldReading == null ||
        newReading == null ||
        price == null ||
        !oldReading.isFinite ||
        !newReading.isFinite ||
        !price.isFinite ||
        oldReading < 0 ||
        newReading < oldReading ||
        price <= 0) {
      return 0;
    }
    return calculateMeterCharge(
      oldReading: oldReading,
      currentReading: newReading,
      unitPrice: price,
    );
  }

  double get _pendingViolationTotal => _pendingInvoiceViolations.fold<double>(
    0,
    (total, violation) => total + _violationFine(violation),
  );

  double _violationFine(Map<String, dynamic> violation) {
    final amount = num.tryParse(
      (violation['tienPhat'] ?? violation['TienPhat'] ?? '').toString(),
    );
    return amount != null && amount.isFinite && amount > 0
        ? amount.toDouble()
        : 0;
  }

  double get _invoiceTotal =>
      _calculatedRent +
      _electricityCost +
      _waterCost +
      _pendingViolationTotal;

  Future<void> _loadPendingInvoiceViolations(int contractId) async {
    setState(() {
      _loadingInvoiceViolations = true;
      _invoiceViolationsError = null;
      _pendingInvoiceViolations = [];
    });
    try {
      final violations = await ApiService.getPendingInvoiceViolations(
        contractId: contractId,
      );
      if (!mounted || contractId != _selectedContractId) return;
      setState(() {
        _pendingInvoiceViolations = violations;
        _loadingInvoiceViolations = false;
      });
    } catch (error) {
      if (!mounted || contractId != _selectedContractId) return;
      setState(() {
        _invoiceViolationsError = error.toString();
        _loadingInvoiceViolations = false;
      });
    }
  }

  Future<void> _selectAndReadElectricMeter(ImageSource source) async {
    final oldReading = _readNumber(_electricOldController);
    final contract = _selectedContract;
    final roomId = contract == null
        ? null
        : int.tryParse(
            (_value(contract, 'maPhong', 'MaPhong') ?? '').toString(),
          );
    if (oldReading == null || oldReading < 0) {
      _showMessage(
        'Nhập chỉ số điện cũ hợp lệ trước khi chụp ảnh.',
        isError: true,
      );
      return;
    }
    if (roomId == null || roomId <= 0) {
      _showMessage('Hợp đồng chưa có mã phòng hợp lệ.', isError: true);
      return;
    }

    try {
      final photo = await ImagePicker().pickImage(
        source: source,
        maxWidth: 1600,
        maxHeight: 1600,
        imageQuality: 80,
      );
      if (photo == null || !mounted) return;
      setState(() {
        _electricMeterPhoto = photo;
        _electricMeterPhotoBytes = null;
        _readingElectricMeter = true;
      });

      final abortSignal = Completer<void>();
      _meterOcrAbort = abortSignal;
      final bytes = await photo.readAsBytes().timeout(
        const Duration(seconds: 10),
      );
      if (!mounted) return;
      setState(() => _electricMeterPhotoBytes = bytes);

      final result = await ApiService.recognizeElectricMeter(
        imageBytes: bytes,
        filename: photo.name,
        roomId: roomId,
        oldReading: oldReading,
        month: _fromDate.month,
        year: _fromDate.year,
        timeout: const Duration(seconds: 145),
        abortTrigger: abortSignal.future,
      ).timeout(
        const Duration(seconds: 150),
        onTimeout: () {
          if (!abortSignal.isCompleted) abortSignal.complete();
          return {
            'success': false,
            'message':
                'OCR vẫn chưa phản hồi sau 150 giây. '
                'Kiểm tra Flask/Ollama hoặc nhập chỉ số điện thủ công.',
          };
        },
      );
      if (!mounted) return;
      if (result['success'] == true) {
        final candidate = (result['chiSoMoi'] as num).toString();
        final confirmed = await showDialog<bool>(
          context: context,
          barrierDismissible: false,
          builder: (dialogContext) => AlertDialog(
            title: const Text('Xác nhận chỉ số OCR'),
            content: Text(
              'Chỉ số đề xuất: $candidate kWh\n'
              'Độ tin cậy: ${result['confidence']}\n'
              '${result['note'] ?? ''}\n\n'
              'Hãy đối chiếu ảnh đồng hồ. Chỉ chọn Sử dụng nếu chính xác.',
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(dialogContext, false),
                child: const Text('Không sử dụng'),
              ),
              FilledButton(
                onPressed: () => Navigator.pop(dialogContext, true),
                child: const Text('Sử dụng chỉ số'),
              ),
            ],
          ),
        );
        if (!mounted) return;
        if (confirmed == true) {
          setState(() => _electricNewController.text = candidate);
        } else {
          _showMessage('Đã bỏ qua kết quả OCR. Em có thể nhập chỉ số thủ công.');
          return;
        }
      }
      _showMessage(
        result['success'] == true
            ? 'Đọc được chỉ số ${result['chiSoMoi']} kWh. Vui lòng kiểm tra lại trước khi gửi hóa đơn.'
            : result['message']?.toString() ?? 'Không đọc được chỉ số điện.',
        isError: result['success'] != true,
      );
    } on TimeoutException {
      if (!mounted) return;
      _showMessage(
        'Đọc ảnh quá thời gian. Hãy thử ảnh nhỏ hơn hoặc nhập chỉ số thủ công.',
        isError: true,
      );
    } catch (error) {
      if (!mounted) return;
      _showMessage('Không thể chọn/đọc ảnh đồng hồ: $error', isError: true);
    } finally {
      _meterOcrAbort = null;
      if (mounted && _readingElectricMeter) {
        setState(() => _readingElectricMeter = false);
      }
    }
  }

  void _cancelElectricMeterRecognition() {
    final abortSignal = _meterOcrAbort;
    if (abortSignal != null && !abortSignal.isCompleted) {
      abortSignal.complete();
    }
  }

  Future<void> _loadContracts() async {
    setState(() {
      _loadingContracts = true;
      _loadError = null;
    });
    try {
      final contracts = await ApiService.getContractsForRentBilling();
      List<dynamic> customers = [];
      String? customerLoadError;
      try {
        customers = await ApiService.getCustomersForRentBilling();
      } catch (error) {
        customerLoadError = error.toString();
      }
      final customerNames = <int, String>{};
      for (final value in customers.whereType<Map>()) {
        final customer = Map<String, dynamic>.from(value);
        final customerId = _customerId(customer);
        final name = _directCustomerName(customer);
        if (customerId != null && name != null)
          customerNames[customerId] = name;
      }
      final active = contracts
          .whereType<Map>()
          .map(Map<String, dynamic>.from)
          .where(_isActiveContract)
          .where((contract) => _contractId(contract) != null)
          .map((contract) {
            final customerId = _customerId(contract);
            final name = customerId == null ? null : customerNames[customerId];
            if (name == null || _directCustomerName(contract) != null) {
              return contract;
            }
            return {...contract, 'customerName': name};
          })
          .toList();
      if (!mounted) return;
      setState(() {
        _activeContracts = active;
        _selectedContractId = active.isEmpty ? null : _contractId(active.first);
        _customerLoadError = customerLoadError;
        _loadingContracts = false;
      });
      if (active.isNotEmpty) {
        _setPeriodForContract(active.first);
        final contractId = _contractId(active.first);
        if (contractId != null) {
          await _loadPendingInvoiceViolations(contractId);
          unawaited(_loadPreviousUtilityReadings(contractId));
        }
      }
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loadError = error.toString();
        _loadingContracts = false;
      });
    }
  }

  // Lấy chỉ số cuối tháng liền trước kỳ hóa đơn, đúng phòng và loại điện/nước.
  Future<void> _loadPreviousUtilityReadings(int contractId) async {
    final requestId = ++_previousReadingsRequest;
    final month = _fromDate.month;
    final year = _fromDate.year;
    final contract = _activeContracts.where(
      (item) => _contractId(item) == contractId,
    ).firstOrNull;
    if (contract == null) return;
    final roomId = int.tryParse(
      (_value(contract, 'maPhong', 'MaPhong') ?? '').toString(),
    );
    if (roomId == null || roomId <= 0) return;

    if (mounted) {
      setState(() {
        _loadingPreviousReadings = true;
        _previousReadingsInfo = null;
      });
    }

    try {
      final readings = await Future.wait<double?>([
        ApiService.getPreviousElectricReading(roomId, month: month, year: year),
        ApiService.getPreviousWaterReading(roomId, month: month, year: year),
      ]);
      if (!mounted ||
          requestId != _previousReadingsRequest ||
          _selectedContractId != contractId ||
          _fromDate.month != month ||
          _fromDate.year != year) return;

      final electricity = readings[0];
      final water = readings[1];
      final previousDate = DateTime(year, month - 1, 1);
      String formatReading(double value) =>
          value == value.truncateToDouble()
              ? value.toInt().toString()
              : value.toString();

      setState(() {
        if (electricity != null && _electricOldController.text.trim().isEmpty) {
          _electricOldController.text = formatReading(electricity);
        }
        if (water != null && _waterOldController.text.trim().isEmpty) {
          _waterOldController.text = formatReading(water);
        }
        _previousReadingsInfo = electricity == null && water == null
            ? 'Không có chỉ số điện/nước tháng ${previousDate.month}/${previousDate.year}. Vui lòng nhập thủ công.'
            : 'Đã lấy chỉ số tháng ${previousDate.month}/${previousDate.year}: '
              'điện ${electricity == null ? "chưa có" : formatReading(electricity)}, '
              'nước ${water == null ? "chưa có" : formatReading(water)}. '
              'Vui lòng kiểm tra trước khi lập hóa đơn.';
        _loadingPreviousReadings = false;
      });
    } catch (error) {
      if (!mounted ||
          requestId != _previousReadingsRequest ||
          _selectedContractId != contractId ||
          _fromDate.month != month ||
          _fromDate.year != year) return;
      setState(() {
        _loadingPreviousReadings = false;
        _previousReadingsInfo =
            'Không tải được chỉ số cũ ($error). Có thể nhập thủ công.';
      });
    }
  }

  bool _isActiveContract(Map<String, dynamic> contract) {
    final status = (_value(contract, 'trangThai', 'TrangThai') ?? '')
        .toString()
        .trim()
        .toLowerCase();
    return status == 'active' || status == 'đang hiệu lực';
  }

  int? _contractId(Map<String, dynamic> contract) => int.tryParse(
    (_value(contract, 'maHopDong', 'MaHopDong') ?? '').toString(),
  );

  Map<String, dynamic>? get _selectedContract {
    for (final contract in _activeContracts) {
      if (_contractId(contract) == _selectedContractId) return contract;
    }
    return null;
  }

  double _monthlyRent(Map<String, dynamic> contract) =>
      _numberValue(contract, 'giaThueThang', 'GiaThueThang') ??
      _numberValue(contract, 'monthlyRent', 'MonthlyRent') ??
      0;

  DateTime? _contractDate(
    Map<String, dynamic> contract,
    String camelKey,
    String pascalKey,
  ) {
    final value = _value(contract, camelKey, pascalKey);
    if (value == null) return null;
    final parsed = DateTime.tryParse(value.toString());
    return parsed == null ? null : DateUtils.dateOnly(parsed);
  }

  void _setPeriodForContract(Map<String, dynamic> contract) {
    final now = DateTime.now();
    final monthStart = DateTime(now.year, now.month, 1);
    final monthEnd = DateTime(now.year, now.month + 1, 0);
    final contractStart = _contractDate(contract, 'ngayBatDau', 'NgayBatDau');
    final contractEnd = _contractDate(contract, 'ngayKetThuc', 'NgayKetThuc');
    final start = contractStart != null && contractStart.isAfter(monthStart)
        ? contractStart
        : monthStart;
    final end = contractEnd != null && contractEnd.isBefore(monthEnd)
        ? contractEnd
        : monthEnd;
    setState(() {
      _fromDate = start;
      _throughDate = end.isBefore(start) ? start : end;
    });
  }

  double get _calculatedRent {
    final contract = _selectedContract;
    if (contract == null || !_isValidPeriod) return 0;
    return calculateProratedRent(
      monthlyRent: _monthlyRent(contract),
      from: _fromDate,
      through: _throughDate,
    );
  }

  bool get _isValidPeriod {
    if (_fromDate.year != _throughDate.year ||
        _fromDate.month != _throughDate.month ||
        _throughDate.isBefore(_fromDate)) {
      return false;
    }
    final contract = _selectedContract;
    if (contract == null) return false;
    final start = _contractDate(contract, 'ngayBatDau', 'NgayBatDau');
    final end = _contractDate(contract, 'ngayKetThuc', 'NgayKetThuc');
    return (start == null || !_fromDate.isBefore(start)) &&
        (end == null || !_throughDate.isAfter(end));
  }

  Future<void> _selectDate({required bool isStart}) async {
    final contract = _selectedContract;
    if (contract == null) return;
    final anchor = isStart ? _fromDate : _throughDate;
    final monthStart = DateTime(anchor.year, anchor.month, 1);
    final monthEnd = DateTime(anchor.year, anchor.month + 1, 0);
    final contractStart = _contractDate(contract, 'ngayBatDau', 'NgayBatDau');
    final contractEnd = _contractDate(contract, 'ngayKetThuc', 'NgayKetThuc');
    final firstAllowed =
        contractStart != null && contractStart.isAfter(monthStart)
        ? contractStart
        : monthStart;
    final lastAllowed = contractEnd != null && contractEnd.isBefore(monthEnd)
        ? contractEnd
        : monthEnd;
    if (lastAllowed.isBefore(firstAllowed)) return;

    final selected = await showDatePicker(
      context: context,
      initialDate: anchor.isBefore(firstAllowed)
          ? firstAllowed
          : anchor.isAfter(lastAllowed)
          ? lastAllowed
          : anchor,
      firstDate: firstAllowed,
      lastDate: lastAllowed,
      helpText: isStart ? 'Chọn ngày bắt đầu tính tiền' : 'Chọn ngày kết thúc',
    );
    if (selected == null || !mounted) return;
    setState(() {
      if (isStart) {
        _fromDate = DateUtils.dateOnly(selected);
        if (_throughDate.isBefore(_fromDate)) _throughDate = _fromDate;
      } else {
        _throughDate = DateUtils.dateOnly(selected);
        if (_fromDate.isAfter(_throughDate)) _fromDate = _throughDate;
      }
    });
  }

  Future<void> _selectBillingMonth() async {
    final contract = _selectedContract;
    if (contract == null) return;
    final contractStart = _contractDate(contract, 'ngayBatDau', 'NgayBatDau');
    final contractEnd = _contractDate(contract, 'ngayKetThuc', 'NgayKetThuc');
    final firstDate = contractStart ?? DateTime(2000);
    final lastDate = contractEnd ?? DateTime(2100, 12, 31);
    if (lastDate.isBefore(firstDate)) return;

    final selected = await showDatePicker(
      context: context,
      initialDate: _fromDate.isBefore(firstDate)
          ? firstDate
          : _fromDate.isAfter(lastDate)
          ? lastDate
          : _fromDate,
      firstDate: firstDate,
      lastDate: lastDate,
      helpText: 'Chọn tháng cần lập hóa đơn',
    );
    if (selected == null || !mounted) return;

    final monthStart = DateTime(selected.year, selected.month, 1);
    final monthEnd = DateTime(selected.year, selected.month + 1, 0);
    final start = contractStart != null && contractStart.isAfter(monthStart)
        ? contractStart
        : monthStart;
    final end = contractEnd != null && contractEnd.isBefore(monthEnd)
        ? contractEnd
        : monthEnd;
    if (end.isBefore(start)) {
      _showMessage(
        'Hợp đồng không có ngày thuê trong tháng đã chọn.',
        isError: true,
      );
      return;
    }
    setState(() {
      _clearUtilityReadings();
      _fromDate = start;
      _throughDate = end;
    });
    final contractId = _selectedContractId;
    if (contractId != null) {
      unawaited(_loadPreviousUtilityReadings(contractId));
    }
  }

  Future<void> _createInvoice() async {
    final contract = _selectedContract;
    final contractId = contract == null ? null : _contractId(contract);
    final amount = _invoiceTotal;
    if (contract == null || contractId == null) {
      _showMessage('Vui lòng chọn hợp đồng đang hiệu lực.', isError: true);
      return;
    }
    if (_loadingInvoiceViolations) {
      _showMessage('Đang tải các khoản phạt của hợp đồng. Vui lòng đợi.', isError: true);
      return;
    }
    if (_invoiceViolationsError != null) {
      _showMessage(
        'Không thể xác nhận khoản phạt chưa tính vào hóa đơn. '
        'Hãy tải lại trước khi lập hóa đơn.',
        isError: true,
      );
      return;
    }
    if (!_isValidPeriod) {
      _showMessage(
        'Khoảng ngày phải nằm trong thời hạn hợp đồng và cùng một tháng.',
        isError: true,
      );
      return;
    }
    final utilityError = _utilityValidationError;
    if (utilityError != null) {
      _showMessage(utilityError, isError: true);
      return;
    }
    if (amount <= 0) {
      _showMessage('Tổng hóa đơn phải lớn hơn 0.', isError: true);
      return;
    }

    final days = _throughDate.difference(_fromDate).inDays + 1;
    final roomName = _roomName(contract);
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Gửi hóa đơn tháng này?'),
        content: Text(
          'Phòng $roomName · ${_formatDate(_fromDate)} – ${_formatDate(_throughDate)}\n'
          '$days ngày tiền thuê: ${_formatMoney(_calculatedRent)} đ\n'
          'Tiền điện: ${_formatMoney(_electricityCost)} đ\n'
          'Tiền nước: ${_formatMoney(_waterCost)} đ\n'
          'Tiền phạt vi phạm chưa tính: ${_formatMoney(_pendingViolationTotal)} đ\n'
          'Tổng cộng: ${_formatMoney(amount)} đ\n\n'
          'Hóa đơn sẽ được lưu và hiển thị cho khách thuê.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Kiểm tra lại'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Gửi hóa đơn'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;

    final noteParts = <String>[
      'Tiền thuê ${_formatMoney(_calculatedRent)}đ, '
          '${_formatDate(_fromDate)}-${_formatDate(_throughDate)} ($days ngày)',
    ];
    final electricOld = _readNumber(_electricOldController);
    final electricNew = _readNumber(_electricNewController);
    final electricPrice = _readNumber(_electricPriceController);
    if (electricOld != null && electricNew != null && electricPrice != null) {
      noteParts.add(
        'Điện ${(electricNew - electricOld).toStringAsFixed(1)} kWh '
        '(${electricOld.toStringAsFixed(1)}-${electricNew.toStringAsFixed(1)}) '
        'x ${_formatMoney(electricPrice)}đ = ${_formatMoney(_electricityCost)}đ',
      );
    }
    final waterOld = _readNumber(_waterOldController);
    final waterNew = _readNumber(_waterNewController);
    final waterPrice = _readNumber(_waterPriceController);
    if (waterOld != null && waterNew != null && waterPrice != null) {
      noteParts.add(
        'Nước ${(waterNew - waterOld).toStringAsFixed(1)} m³ '
        '(${waterOld.toStringAsFixed(1)}-${waterNew.toStringAsFixed(1)}) '
        'x ${_formatMoney(waterPrice)}đ = ${_formatMoney(_waterCost)}đ',
      );
    }
    for (final violation in _pendingInvoiceViolations) {
      final description =
          (violation['noiDung'] ?? violation['NoiDung'] ?? 'Vi phạm nội quy')
              .toString()
              .trim();
      noteParts.add(
        'Tiền phạt vi phạm: $description '
        '${_formatMoney(_violationFine(violation))}đ',
      );
    }
    final extraNote = _noteController.text.trim();
    if (extraNote.isNotEmpty) noteParts.add(extraNote);
    setState(() => _submitting = true);
    final result = await ApiService.createManualInvoice(
      contractId: contractId,
      month: _fromDate.month,
      year: _fromDate.year,
      total: amount - _pendingViolationTotal,
      note: noteParts.join('. '),
    );
    if (!mounted) return;
    setState(() => _submitting = false);
    _showMessage(
      result['message']?.toString() ?? 'Không thể lập hóa đơn.',
      isError: result['success'] != true,
    );
  }

  String _roomName(Map<String, dynamic> contract) {
    final room = _value(contract, 'maPhongNavigation', 'MaPhongNavigation');
    if (room is Map) {
      final name = _value(
        Map<String, dynamic>.from(room),
        'tenPhong',
        'TenPhong',
      );
      if (name != null) return name.toString();
    }
    return _value(contract, 'tenPhong', 'TenPhong')?.toString() ??
        'Phòng ${_value(contract, 'maPhong', 'MaPhong') ?? '—'}';
  }

  String _customerName(Map<String, dynamic> contract) {
    return _directCustomerName(contract) ??
        'Khách thuê #${_customerId(contract) ?? 'chưa có mã'}';
  }

  int? _customerId(Map<String, dynamic> value) {
    final directId = int.tryParse(
      (_value(value, 'maKhachDaiDien', 'MaKhachDaiDien') ??
              _value(value, 'maKhach', 'MaKhach') ??
              _value(value, 'customerId', 'CustomerId') ??
              '')
          .toString(),
    );
    if (directId != null) return directId;

    for (final key in [
      'maKhachDaiDienNavigation',
      'MaKhachDaiDienNavigation',
      'maKhachNavigation',
      'MaKhachNavigation',
      'khachThue',
      'KhachThue',
      'customer',
      'Customer',
    ]) {
      final nested = value[key];
      if (nested is! Map) continue;
      final id = int.tryParse(
        (_value(Map<String, dynamic>.from(nested), 'maKhach', 'MaKhach') ?? '')
            .toString(),
      );
      if (id != null) return id;
    }
    return null;
  }

  String? _directCustomerName(Map<String, dynamic> value) {
    for (final key in [
      'customerName',
      'CustomerName',
      'hoTen',
      'HoTen',
      'tenKhach',
      'TenKhach',
      'tenKhachHang',
      'TenKhachHang',
    ]) {
      final name = value[key]?.toString().trim();
      if (name != null && name.isNotEmpty) return name;
    }
    for (final key in [
      'maKhachDaiDienNavigation',
      'MaKhachDaiDienNavigation',
      'maKhachNavigation',
      'MaKhachNavigation',
      'khachThue',
      'KhachThue',
      'customer',
      'Customer',
    ]) {
      final nested = value[key];
      if (nested is Map) {
        final name = _directCustomerName(Map<String, dynamic>.from(nested));
        if (name != null) return name;
      }
    }
    return null;
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
    final contract = _selectedContract;
    final monthlyRent = contract == null ? 0.0 : _monthlyRent(contract);
    final daysInMonth = DateTime(_fromDate.year, _fromDate.month + 1, 0).day;
    final chargedDays = _isValidPeriod
        ? _throughDate.difference(_fromDate).inDays + 1
        : 0;

    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: const Text(
          'Lập tiền thuê theo ngày',
          style: TextStyle(
            color: Color(0xFF172554),
            fontWeight: FontWeight.bold,
            fontSize: 18,
          ),
        ),
        backgroundColor: Colors.white,
        foregroundColor: const Color(0xFF172554),
        elevation: 0,
        actions: [
          IconButton(
            tooltip: 'Tải lại hợp đồng',
            onPressed: _loadingContracts ? null : _loadContracts,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: _loadingContracts
          ? const Center(child: CircularProgressIndicator())
          : _loadError != null
          ? _errorView()
          : _activeContracts.isEmpty
          ? _emptyView()
          : _buildForm(
              contract: contract!,
              monthlyRent: monthlyRent,
              daysInMonth: daysInMonth,
              chargedDays: chargedDays,
            ),
    );
  }

  Widget _buildForm({
    required Map<String, dynamic> contract,
    required double monthlyRent,
    required int daysInMonth,
    required int chargedDays,
  }) {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        if (_customerLoadError != null)
          Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: _sectionCard(
              title: 'Tên khách thuê chưa tải được',
              icon: Icons.info_outline,
              child: Text(
                'Không thể tải danh sách khách để đối chiếu tên. '
                'Vẫn có thể lập hóa đơn theo mã khách.\n$_customerLoadError',
                style: const TextStyle(color: Color(0xFF64748B)),
              ),
            ),
          ),
        _sectionCard(
          title: 'Khách thuê và hợp đồng',
          icon: Icons.person_outline,
          child: DropdownButtonFormField<int>(
            initialValue: _selectedContractId,
            decoration: const InputDecoration(
              labelText: 'Chọn hợp đồng đang hiệu lực',
              border: OutlineInputBorder(),
            ),
            isExpanded: true,
            items: _activeContracts.map((item) {
              final id = _contractId(item)!;
              return DropdownMenuItem(
                value: id,
                child: Text(
                  '${_roomName(item)} · ${_customerName(item)} · HĐ #$id',
                  overflow: TextOverflow.ellipsis,
                ),
              );
            }).toList(),
            onChanged: (value) {
              if (value == null) return;
              final selected = _activeContracts.firstWhere(
                (item) => _contractId(item) == value,
              );
              setState(() {
                _clearUtilityReadings();
                _selectedContractId = value;
              });
              _setPeriodForContract(selected);
              unawaited(_loadPendingInvoiceViolations(value));
              unawaited(_loadPreviousUtilityReadings(value));
            },
          ),
        ),
        const SizedBox(height: 12),
        _sectionCard(
          title: 'Khoảng thời gian tính tiền',
          icon: Icons.date_range_outlined,
          child: Column(
            children: [
              Align(
                alignment: Alignment.centerLeft,
                child: OutlinedButton.icon(
                  onPressed: _selectBillingMonth,
                  icon: const Icon(Icons.calendar_month_outlined, size: 18),
                  label: Text(
                    'Kỳ hóa đơn: ${_fromDate.month.toString().padLeft(2, '0')}/${_fromDate.year}',
                  ),
                ),
              ),
              const SizedBox(height: 8),
              Row(
                children: [
                  Expanded(
                    child: _dateButton(
                      label: 'Từ ngày',
                      value: _formatDate(_fromDate),
                      onPressed: () => _selectDate(isStart: true),
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: _dateButton(
                      label: 'Đến ngày',
                      value: _formatDate(_throughDate),
                      onPressed: () => _selectDate(isStart: false),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              Align(
                alignment: Alignment.centerLeft,
                child: Text(
                  'Tính cả ngày bắt đầu và ngày kết thúc. Mỗi hóa đơn chỉ thuộc một tháng.',
                  style: TextStyle(
                    color: Colors.blueGrey.shade600,
                    fontSize: 12,
                  ),
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 12),
        _sectionCard(
          title: 'Tiền điện và nước',
          icon: Icons.electric_meter_outlined,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (_loadingPreviousReadings) ...[
                const LinearProgressIndicator(),
                const SizedBox(height: 8),
                const Text('Đang lấy chỉ số điện/nước kỳ trước...'),
                const SizedBox(height: 12),
              ] else if (_previousReadingsInfo != null) ...[
                Text(
                  _previousReadingsInfo!,
                  style: const TextStyle(color: Color(0xFF15803D), fontSize: 12),
                ),
                const SizedBox(height: 12),
              ],
              const Text(
                'Điện (kWh)',
                style: TextStyle(
                  color: Color(0xFF172554),
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(height: 8),
              Row(
                children: [
                  Expanded(
                    child: _meterInput(
                      controller: _electricOldController,
                      label: 'Chỉ số cũ',
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: _meterInput(
                      controller: _electricNewController,
                      label: 'Chỉ số mới',
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              _meterInput(
                controller: _electricPriceController,
                label: 'Đơn giá điện (đ/kWh)',
              ),
              const SizedBox(height: 8),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: _readingElectricMeter
                          ? null
                          : () =>
                                _selectAndReadElectricMeter(ImageSource.camera),
                      icon: const Icon(Icons.camera_alt_outlined),
                      label: const Text('Chụp ảnh'),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: _readingElectricMeter
                          ? null
                          : () => _selectAndReadElectricMeter(
                              ImageSource.gallery,
                            ),
                      icon: const Icon(Icons.photo_library_outlined),
                      label: const Text('Tải ảnh lên'),
                    ),
                  ),
                ],
              ),
              if (_readingElectricMeter) ...[
                const SizedBox(height: 8),
                const LinearProgressIndicator(),
                const SizedBox(height: 4),
                Row(
                  children: [
                    const Expanded(
                      child: Text(
                        'Đang xử lý ảnh trên máy chủ OCR (tối đa 150 giây).',
                        style: TextStyle(
                          color: Color(0xFF64748B),
                          fontSize: 12,
                        ),
                      ),
                    ),
                    TextButton(
                      onPressed: _cancelElectricMeterRecognition,
                      child: const Text('Dừng'),
                    ),
                  ],
                ),
                const Text(
                  'Nếu quá thời gian, kiểm tra Flask/Ollama hoặc nhập chỉ số thủ công.',
                  style: TextStyle(color: Color(0xFF64748B), fontSize: 11),
                ),
              ],
              if (_electricMeterPhotoBytes != null) ...[
                const SizedBox(height: 8),
                ClipRRect(
                  borderRadius: BorderRadius.circular(10),
                  child: Image.memory(
                    _electricMeterPhotoBytes!,
                    cacheWidth: 900,
                    cacheHeight: 450,
                    height: 150,
                    width: double.infinity,
                    fit: BoxFit.cover,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  'Ảnh: ${_electricMeterPhoto?.name ?? 'đồng hồ điện'} · '
                  'Hãy kiểm tra và chỉnh chỉ số nếu OCR nhận diện sai.',
                  style: const TextStyle(
                    color: Color(0xFF64748B),
                    fontSize: 12,
                  ),
                ),
              ],
              const SizedBox(height: 8),
              _detailRow(
                'Tiêu thụ điện',
                '${_usage(_electricOldController, _electricNewController)} kWh'
                    ' · ${_formatMoney(_electricityCost)} đ',
              ),
              const Divider(height: 24),
              const Text(
                'Nước (m³)',
                style: TextStyle(
                  color: Color(0xFF172554),
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(height: 8),
              Row(
                children: [
                  Expanded(
                    child: _meterInput(
                      controller: _waterOldController,
                      label: 'Chỉ số cũ',
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: _meterInput(
                      controller: _waterNewController,
                      label: 'Chỉ số mới',
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              _meterInput(
                controller: _waterPriceController,
                label: 'Đơn giá nước (đ/m³)',
              ),
              const SizedBox(height: 8),
              _detailRow(
                'Tiêu thụ nước',
                '${_usage(_waterOldController, _waterNewController)} m³'
                    ' · ${_formatMoney(_waterCost)} đ',
              ),
              if (_utilityValidationError != null) ...[
                const SizedBox(height: 8),
                Text(
                  _utilityValidationError!,
                  style: const TextStyle(color: Colors.redAccent),
                ),
              ],
            ],
          ),
        ),
        const SizedBox(height: 12),
        _sectionCard(
          title: 'Tiền phạt vi phạm chưa tính',
          icon: Icons.report_gmailerrorred_outlined,
          child: _loadingInvoiceViolations
              ? const Row(
                  children: [
                    SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    ),
                    SizedBox(width: 10),
                    Expanded(child: Text('Đang tải khoản phạt của hợp đồng...')),
                  ],
                )
              : _invoiceViolationsError != null
              ? Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Không tải được khoản phạt: $_invoiceViolationsError',
                      style: const TextStyle(color: Colors.redAccent),
                    ),
                    Align(
                      alignment: Alignment.centerRight,
                      child: TextButton.icon(
                        onPressed: _selectedContractId == null
                            ? null
                            : () => unawaited(
                                _loadPendingInvoiceViolations(
                                  _selectedContractId!,
                                ),
                              ),
                        icon: const Icon(Icons.refresh),
                        label: const Text('Thử lại'),
                      ),
                    ),
                  ],
                )
              : _pendingInvoiceViolations.isEmpty
              ? const Text(
                  'Không có khoản phạt mới nào cần tính vào hóa đơn.',
                  style: TextStyle(color: Color(0xFF64748B)),
                )
              : Column(
                  children: [
                    for (final violation in _pendingInvoiceViolations)
                      _detailRow(
                        (violation['noiDung'] ??
                                violation['NoiDung'] ??
                                'Vi phạm nội quy')
                            .toString(),
                        '${_formatMoney(_violationFine(violation))} đ',
                      ),
                    const Divider(height: 20),
                    _detailRow(
                      'Tổng tiền phạt',
                      '${_formatMoney(_pendingViolationTotal)} đ',
                    ),
                    const SizedBox(height: 4),
                    const Align(
                      alignment: Alignment.centerLeft,
                      child: Text(
                        'Chỉ gồm khoản phạt mới, đã xử lý và chưa gắn vào hóa đơn.',
                        style: TextStyle(
                          color: Color(0xFF64748B),
                          fontSize: 12,
                        ),
                      ),
                    ),
                  ],
                ),
        ),
        const SizedBox(height: 12),
        _sectionCard(
          title: 'Xem trước hóa đơn',
          icon: Icons.calculate_outlined,
          child: Column(
            children: [
              _detailRow('Phòng', _roomName(contract)),
              _detailRow('Khách thuê', _customerName(contract)),
              _detailRow('Tiền thuê tháng', '${_formatMoney(monthlyRent)} đ'),
              _detailRow(
                'Số ngày tính tiền',
                '$chargedDays / $daysInMonth ngày',
              ),
              const Divider(height: 24),
              Row(
                children: [
                  const Expanded(
                    child: Text(
                      'Tiền thuê',
                      style: TextStyle(
                        color: Color(0xFF334155),
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                  Text(
                    '${_formatMoney(_calculatedRent)} đ',
                    style: const TextStyle(
                      color: Color(0xFF1D4ED8),
                      fontSize: 20,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ],
              ),
              _detailRow('Tiền điện', '${_formatMoney(_electricityCost)} đ'),
              _detailRow('Tiền nước', '${_formatMoney(_waterCost)} đ'),
              _detailRow(
                'Tiền phạt vi phạm',
                '${_formatMoney(_pendingViolationTotal)} đ',
              ),
              const Divider(height: 24),
              Row(
                children: [
                  const Expanded(
                    child: Text(
                      'Tổng cần thu',
                      style: TextStyle(
                        color: Color(0xFF334155),
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                  Text(
                    '${_formatMoney(_invoiceTotal)} đ',
                    style: const TextStyle(
                      color: Color(0xFF1D4ED8),
                      fontSize: 20,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              Align(
                alignment: Alignment.centerLeft,
                child: Text(
                  'Tiền thuê = tiền tháng × $chargedDays / $daysInMonth ngày; '
                  'điện/nước = mức tiêu thụ × đơn giá.',
                  style: TextStyle(
                    color: Colors.blueGrey.shade600,
                    fontSize: 12,
                  ),
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 12),
        _sectionCard(
          title: 'Ghi chú gửi khách (không bắt buộc)',
          icon: Icons.notes_outlined,
          child: TextField(
            controller: _noteController,
            maxLines: 3,
            maxLength: 300,
            decoration: const InputDecoration(
              hintText: 'Ghi chú bổ sung cho hóa đơn (nếu có).',
              border: OutlineInputBorder(),
            ),
          ),
        ),
        if (!_isValidPeriod) ...[
          const SizedBox(height: 10),
          const Text(
            'Khoảng ngày phải nằm trong thời hạn hợp đồng và cùng một tháng.',
            style: TextStyle(color: Colors.redAccent),
          ),
        ],
        const SizedBox(height: 20),
        SizedBox(
          height: 52,
          child: FilledButton.icon(
            onPressed:
                _submitting ||
                    !_isValidPeriod ||
                    _utilityValidationError != null ||
                    _loadingInvoiceViolations ||
                    _invoiceViolationsError != null ||
                    _invoiceTotal <= 0
                ? null
                : _createInvoice,
            icon: _submitting
                ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Icon(Icons.send_outlined),
            label: Text(_submitting ? 'Đang gửi...' : 'Lập và gửi hóa đơn'),
          ),
        ),
      ],
    );
  }

  Widget _sectionCard({
    required String title,
    required IconData icon,
    required Widget child,
  }) {
    return Card(
      elevation: 0,
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
                Icon(icon, color: const Color(0xFF4F46E5), size: 20),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    title,
                    style: const TextStyle(
                      color: Color(0xFF172554),
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 14),
            child,
          ],
        ),
      ),
    );
  }

  Widget _meterInput({
    required TextEditingController controller,
    required String label,
  }) {
    return TextField(
      controller: controller,
      keyboardType: const TextInputType.numberWithOptions(decimal: true),
      onChanged: (_) => setState(() {}),
      decoration: InputDecoration(
        labelText: label,
        border: const OutlineInputBorder(),
        isDense: true,
      ),
    );
  }

  Widget _dateButton({
    required String label,
    required String value,
    required VoidCallback onPressed,
  }) {
    return OutlinedButton(
      onPressed: onPressed,
      style: OutlinedButton.styleFrom(
        padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 10),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(fontSize: 11)),
          const SizedBox(height: 5),
          Row(
            children: [
              const Icon(Icons.calendar_month_outlined, size: 16),
              const SizedBox(width: 5),
              Flexible(
                child: Text(
                  value,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontWeight: FontWeight.w600),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _detailRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Row(
        children: [
          Expanded(
            child: Text(
              label,
              style: const TextStyle(color: Color(0xFF64748B)),
            ),
          ),
          Flexible(
            child: Text(
              value,
              textAlign: TextAlign.end,
              style: const TextStyle(
                color: Color(0xFF172554),
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }

  String _usage(
    TextEditingController oldController,
    TextEditingController newController,
  ) {
    final oldReading = _readNumber(oldController);
    final newReading = _readNumber(newController);
    if (oldReading == null || newReading == null || newReading < oldReading) {
      return '—';
    }
    return (newReading - oldReading).toStringAsFixed(1);
  }

  Widget _errorView() => Center(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.error_outline, color: Colors.redAccent, size: 42),
          const SizedBox(height: 12),
          Text(
            'Không tải được hợp đồng.\n$_loadError',
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 12),
          OutlinedButton.icon(
            onPressed: _loadContracts,
            icon: const Icon(Icons.refresh),
            label: const Text('Thử lại'),
          ),
        ],
      ),
    ),
  );

  Widget _emptyView() => const Center(
    child: Padding(
      padding: EdgeInsets.all(24),
      child: Text(
        'Không có hợp đồng đang hiệu lực để lập tiền thuê.',
        textAlign: TextAlign.center,
        style: TextStyle(color: Color(0xFF64748B)),
      ),
    ),
  );
}

dynamic _value(Map<String, dynamic> map, String camelKey, String pascalKey) =>
    map[camelKey] ?? map[pascalKey];

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

String _formatDate(DateTime date) =>
    '${date.day.toString().padLeft(2, '0')}/'
    '${date.month.toString().padLeft(2, '0')}/${date.year}';

String _formatMoney(double value) => value.round().toString().replaceAllMapped(
  RegExp(r'(\d)(?=(\d{3})+(?!\d))'),
  (match) => '${match[1]},',
);
