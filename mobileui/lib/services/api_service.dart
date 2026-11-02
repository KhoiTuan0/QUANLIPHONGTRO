import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';
import 'package:http/http.dart' as http;
import 'package:flutter/foundation.dart';

class ApiService {
  // Cổng HTTP thực tế từ file launchSettings.json của bạn là 5180
  static const String baseUrl = "http://10.0.2.2:5180/api";
  static const String meterOcrUrl = "http://10.0.2.2:5000/api/ocr-dong-ho";

  // Cookie phiên đăng nhập cho ứng dụng mô phỏng (lưu trong RAM).
  // Khi khởi động lại ứng dụng, khách cần đăng nhập lại.
  static String? _sessionCookie;

  static String? _extractSessionCookie(http.Response response) {
    final raw = response.headers['set-cookie'];
    if (raw == null || raw.isEmpty) return null;
    // Cookie auth của ASP.NET Core được cấu hình tên QLPT.Session.
    final match = RegExp(r'(?:^|[,\s])(?:QLPT\.Session|\.AspNetCore\.Cookies)=([^;,\s]+)')
        .firstMatch(raw);
    if (match == null) return null;
    final name = raw.substring(match.start, match.end).trim().split('=')[0];
    return '$name=${match.group(1)}';
  }

  static Map<String, String> _cookieHeaders() => {
    'Content-Type': 'application/json',
    if (_sessionCookie != null) 'Cookie': _sessionCookie!,
  };

  // --- AUTH ---
  static Future<Map<String, dynamic>> login(
    String username,
    String password,
  ) async {
    try {
      _sessionCookie = null;
      final response = await http
          .post(
            Uri.parse('$baseUrl/Auth/login'),
            headers: {'Content-Type': 'application/json'},
            body: jsonEncode({'username': username, 'password': password}),
          )
          .timeout(const Duration(seconds: 10));

      print('API Login trả trạng thái HTTP ${response.statusCode}.');

      if (response.statusCode == 200) {
        final decoded = jsonDecode(response.body);
        if (decoded is! Map) {
          return {
            'success': false,
            'message': 'Phản hồi đăng nhập từ máy chủ không đúng định dạng.',
          };
        }

        _sessionCookie = _extractSessionCookie(response);
        final data = Map<String, dynamic>.from(decoded);
        final nested = data['data'] ?? data['user'] ?? data['taiKhoan'];
        final userData = nested is Map
            ? Map<String, dynamic>.from(nested)
            : <String, dynamic>{};
        final roleValue =
            _firstValue(data, const ['vaiTro', 'VaiTro', 'role', 'Role']) ??
            _firstValue(userData, const ['vaiTro', 'VaiTro', 'role', 'Role']);
        final role = _normalizeLoginRole(roleValue?.toString());
        if (role == null) {
          return {
            'success': false,
            'message':
                'Máy chủ chưa trả về vai trò tài khoản hợp lệ (Admin/Customer).',
          };
        }

        final rawCustomerId =
            _firstValue(data, const [
              'maKhach',
              'MaKhach',
              'customerId',
              'CustomerId',
              'maKhachThue',
              'MaKhachThue',
            ]) ??
            _firstValue(userData, const [
              'maKhach',
              'MaKhach',
              'customerId',
              'CustomerId',
              'maKhachThue',
              'MaKhachThue',
            ]);
        final customer =
            data['khachThue'] ??
            data['KhachThue'] ??
            data['maKhachNavigation'] ??
            data['MaKhachNavigation'] ??
            userData['khachThue'] ??
            userData['KhachThue'] ??
            userData['maKhachNavigation'] ??
            userData['MaKhachNavigation'];
        final customerMap = customer is Map ? customer : const {};
        final customerId = int.tryParse(
          (rawCustomerId ??
                      _firstValue(customerMap, const [
                        'maKhach',
                        'MaKhach',
                        'customerId',
                        'CustomerId',
                        'id',
                        'Id',
                      ]))
                  ?.toString() ??
              '',
        );
        if (role == 'Customer' && (customerId == null || customerId <= 0)) {
          return {
            'success': false,
            'message':
                'Tài khoản khách đã được xác thực nhưng API đăng nhập chưa trả '
                'mã khách (maKhach). Hãy cập nhật AuthController để trả mã khách '
                'thuê tương ứng.',
          };
        }

        return {
          'success': true,
          'message': 'Đăng nhập thành công',
          'vaiTro': role,
          'maKhach': customerId,
        };
      } else {
        return {
          'success': false,
          'message': 'Lỗi đăng nhập (${response.statusCode}): ${response.body}',
        };
      }
    } catch (e) {
      return {'success': false, 'message': 'Lỗi kết nối Server: $e'};
    }
  }

  static dynamic _firstValue(Map data, List<String> keys) {
    for (final key in keys) {
      final value = data[key];
      if (value != null) return value;
    }
    return null;
  }

  static String? _normalizeLoginRole(String? value) {
    final normalized = value?.trim().toLowerCase();
    if (normalized == null || normalized.isEmpty) return null;

    if (normalized == 'admin' ||
        normalized == 'chủ trọ' ||
        normalized == 'chu tro' ||
        normalized == 'landlord') {
      return 'Admin';
    }
    if (normalized == 'customer' ||
        normalized == 'khách' ||
        normalized == 'khách hàng' ||
        normalized == 'khách thuê' ||
        normalized == 'khach hang' ||
        normalized == 'khach thue' ||
        normalized == 'tenant') {
      return 'Customer';
    }
    return null;
  }

  static Future<bool> register(Map<String, dynamic> data) async {
    try {
      final response = await http
          .post(
            Uri.parse('$baseUrl/Auth/register'),
            headers: {'Content-Type': 'application/json'},
            body: jsonEncode(data),
          )
          .timeout(const Duration(seconds: 10));
      return response.statusCode == 200 || response.statusCode == 201;
    } catch (e) {
      print('Lỗi register: $e');
      return false;
    }
  }

  static Future<Map<String, dynamic>> registerCustomerForLandlord({
    required String email,
    required String fullName,
    required String phone,
    String? cccd,
    String? address,
    http.Client? client,
  }) async {
    final normalizedEmail = email.trim().toLowerCase();
    if (normalizedEmail.isEmpty) {
      return {
        'success': false,
        'message': 'Email là bắt buộc để tạo tài khoản.',
      };
    }
    if (normalizedEmail.length < 6) {
      return {
        'success': false,
        'message':
            'Email quá ngắn để dùng làm mật khẩu khởi tạo (tối thiểu 6 ký tự).',
      };
    }
    if (!RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(normalizedEmail)) {
      return {'success': false, 'message': 'Địa chỉ email chưa hợp lệ.'};
    }
    if (fullName.trim().isEmpty || phone.trim().isEmpty) {
      return {
        'success': false,
        'message': 'Vui lòng nhập họ tên và số điện thoại.',
      };
    }

    try {
      final response = await (client?.post ?? http.post)(
        Uri.parse('$baseUrl/Auth/register'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'tenDangNhap': normalizedEmail,
          'matKhau': normalizedEmail,
          'hoTen': fullName.trim(),
          'email': normalizedEmail,
          'soDienThoai': phone.trim(),
          'vaiTro': 'Customer',
          'cccd': cccd?.trim() ?? '',
          'diaChi': address?.trim() ?? '',
        }),
      ).timeout(const Duration(seconds: 15));

      dynamic decoded;
      try {
        decoded = response.body.isEmpty ? null : jsonDecode(response.body);
      } on FormatException {
        decoded = null;
      }
      final body = decoded is Map
          ? Map<String, dynamic>.from(decoded)
          : <String, dynamic>{};
      if (response.statusCode >= 200 &&
          response.statusCode < 300 &&
          body['success'] != false) {
        return {
          ...body,
          'success': true,
          'message':
              body['message']?.toString() ?? 'Đã tạo khách và tài khoản.',
          'username': normalizedEmail,
          'initialPassword': normalizedEmail,
        };
      }
      return {
        'success': false,
        'message': body['message']?.toString().trim().isNotEmpty == true
            ? body['message'].toString()
            : 'Không thể tạo tài khoản khách (${response.statusCode}): ${response.body}',
      };
    } catch (error) {
      return {
        'success': false,
        'message': 'Không thể kết nối để tạo tài khoản khách: $error',
      };
    }
  }

  static Future<void> logout() async {
    try {
      await http.post(
        Uri.parse('$baseUrl/Auth/logout'),
        headers: _cookieHeaders(),
      ).timeout(const Duration(seconds: 3));
    } catch (e) {
      debugPrint('Lỗi gọi API logout: $e');
    } finally {
      _sessionCookie = null;
    }
  }

  // --- ROOMS (Phòng) ---
  static Future<List<dynamic>> getRooms() async {
    try {
      final response = await http.get(Uri.parse('$baseUrl/Phong'));
      if (response.statusCode == 200) return jsonDecode(response.body);
    } catch (e) {
      print('Lỗi getRooms: $e');
    }
    return [];
  }

  static Future<List<dynamic>> getRoomsForContractCreation({
    http.Client? client,
  }) async {
    final uri = Uri.parse('$baseUrl/Phong');
    final request = client == null
        ? http.get(uri, headers: {'Content-Type': 'application/json'})
        : client.get(uri, headers: {'Content-Type': 'application/json'});
    final response = await request.timeout(const Duration(seconds: 10));
    return _decodeListResponse(response, 'Không thể tải danh sách phòng');
  }

  static Future<bool> createRoom(Map<String, dynamic> data) async {
    try {
      final response = await http.post(
        Uri.parse('$baseUrl/Phong'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(data),
      );
      return response.statusCode == 200 || response.statusCode == 201;
    } catch (e) {
      print('Lỗi createRoom: $e');
      return false;
    }
  }

  // --- ROOM TYPES (Loại Phòng) ---
  static Future<List<dynamic>> getRoomTypes() async {
    try {
      final res = await http.get(Uri.parse('$baseUrl/LoaiPhong'));
      if (res.statusCode == 200) return jsonDecode(res.body);
    } catch (e) {
      print('Lỗi getRoomTypes: $e');
    }
    return [];
  }

  static Future<bool> createRoomType(Map<String, dynamic> data) async {
    try {
      final res = await http.post(
        Uri.parse('$baseUrl/LoaiPhong'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(data),
      );
      return res.statusCode == 200 || res.statusCode == 201;
    } catch (e) {
      print('Lỗi createRoomType: $e');
      return false;
    }
  }

  static Future<bool> deleteRoomType(int typeId) async {
    try {
      final res = await http.delete(Uri.parse('$baseUrl/LoaiPhong/$typeId'));
      return res.statusCode == 200 || res.statusCode == 204;
    } catch (e) {
      print('Lỗi deleteRoomType: $e');
      return false;
    }
  }

  // --- CUSTOMERS (Khách Thuê) ---
  static Future<List<dynamic>> getCustomers() async {
    try {
      final res = await http.get(Uri.parse('$baseUrl/KhachThue'));
      if (res.statusCode == 200) return jsonDecode(res.body);
    } catch (e) {
      print('Lỗi getCustomers: $e');
    }
    return [];
  }

  static Future<bool> createCustomer(Map<String, dynamic> data) async {
    try {
      final res = await http.post(
        Uri.parse('$baseUrl/KhachThue'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(data),
      );
      return res.statusCode == 200 || res.statusCode == 201;
    } catch (e) {
      print('Lỗi createCustomer: $e');
      return false;
    }
  }

  // --- CONTRACTS (Hợp Đồng) ---
  static Future<List<dynamic>> getContracts() async {
    try {
      final res = await http.get(Uri.parse('$baseUrl/HopDong'));
      if (res.statusCode == 200) return jsonDecode(res.body);
    } catch (e) {
      print('Lỗi getContracts: $e');
    }
    return [];
  }

  static Future<List<dynamic>> getContractsForRentBilling({
    http.Client? client,
  }) async {
    final uri = Uri.parse('$baseUrl/HopDong');
    final request = client == null
        ? http.get(uri, headers: {'Content-Type': 'application/json'})
        : client.get(uri, headers: {'Content-Type': 'application/json'});
    final response = await request.timeout(const Duration(seconds: 10));
    return _decodeListResponse(response, 'Không thể tải danh sách hợp đồng');
  }

  static Future<List<dynamic>> getCustomersForRentBilling({
    http.Client? client,
  }) async {
    final uri = Uri.parse('$baseUrl/KhachThue');
    final request = client == null
        ? http.get(uri, headers: {'Content-Type': 'application/json'})
        : client.get(uri, headers: {'Content-Type': 'application/json'});
    final response = await request.timeout(const Duration(seconds: 10));
    return _decodeListResponse(response, 'Không thể tải danh sách khách thuê');
  }

  static Future<Map<String, dynamic>> getCustomerProfile(
    int customerId, {
    http.Client? client,
  }) async {
    if (customerId <= 0) {
      throw ArgumentError.value(
        customerId,
        'customerId',
        'Mã khách không hợp lệ.',
      );
    }
    final uri = Uri.parse('$baseUrl/KhachThue/$customerId');
    final request = client == null
        ? http.get(uri, headers: {'Content-Type': 'application/json'})
        : client.get(uri, headers: {'Content-Type': 'application/json'});
    final response = await request.timeout(const Duration(seconds: 10));
    if (response.statusCode != 200) {
      throw Exception(
        'Không thể tải hồ sơ khách thuê (${response.statusCode}): ${response.body}',
      );
    }
    final decoded = jsonDecode(response.body);
    if (decoded is! Map) {
      throw const FormatException(
        'API hồ sơ khách thuê trả dữ liệu không hợp lệ.',
      );
    }
    return Map<String, dynamic>.from(decoded);
  }

  static Future<Map<String, dynamic>> updateCustomerProfile({
    required int customerId,
    required Map<String, dynamic> profile,
    http.Client? client,
  }) async {
    if (customerId <= 0) {
      throw ArgumentError.value(
        customerId,
        'customerId',
        'Mã khách không hợp lệ.',
      );
    }
    final response = await (client?.put ?? http.put)(
      Uri.parse('$baseUrl/KhachThue/$customerId'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode(profile),
    ).timeout(const Duration(seconds: 15));
    return _decodeMutationResponse(
      response,
      operation: 'Cập nhật hồ sơ khách thuê',
      defaultSuccessMessage: 'Cập nhật thông tin thành công.',
    );
  }

  static Future<Map<String, dynamic>> changeCustomerPassword({
    required int customerId,
    required String currentPassword,
    required String newPassword,
    http.Client? client,
  }) async {
    if (customerId <= 0) {
      throw ArgumentError.value(
        customerId,
        'customerId',
        'Mã khách không hợp lệ.',
      );
    }
    final response = await (client?.post ?? http.post)(
      Uri.parse('$baseUrl/Auth/change-password'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'maKhach': customerId,
        'matKhauHienTai': currentPassword,
        'matKhauMoi': newPassword,
      }),
    ).timeout(const Duration(seconds: 15));
    return _decodeMutationResponse(
      response,
      operation: 'Đổi mật khẩu',
      defaultSuccessMessage: 'Đổi mật khẩu thành công.',
      notFoundMessage:
          'Backend chưa có API /Auth/change-password. Hãy bổ sung endpoint '
          'đổi mật khẩu vào AuthController rồi thử lại.',
    );
  }

  static Map<String, dynamic> _decodeMutationResponse(
    http.Response response, {
    required String operation,
    required String defaultSuccessMessage,
    String? notFoundMessage,
  }) {
    dynamic decoded;
    try {
      decoded = response.body.isEmpty ? null : jsonDecode(response.body);
    } on FormatException {
      decoded = null;
    }
    final body = decoded is Map
        ? Map<String, dynamic>.from(decoded)
        : <String, dynamic>{};
    final serverSuccess = body['success'] != false;
    if (response.statusCode >= 200 &&
        response.statusCode < 300 &&
        serverSuccess) {
      return {
        ...body,
        'success': true,
        'message': body['message']?.toString() ?? defaultSuccessMessage,
      };
    }
    final message = response.statusCode == 404 && notFoundMessage != null
        ? notFoundMessage
        : body['message']?.toString().trim().isNotEmpty == true
        ? body['message'].toString()
        : '$operation thất bại (${response.statusCode}): ${response.body}';
    return {'success': false, 'message': message};
  }

  static Future<List<dynamic>> getContractsByCustomer(int maKhach) async {
    try {
      final res = await http.get(
        Uri.parse('$baseUrl/HopDong/customer/$maKhach'),
      );
      if (res.statusCode == 200) {
        return jsonDecode(res.body);
      }
    } catch (e) {
      print('Lỗi getContractsByCustomer: $e');
    }
    return [];
  }

  static Future<Map<String, dynamic>> createContract(
    Map<String, dynamic> data, {
    http.Client? client,
  }) async {
    try {
      final url = Uri.parse('$baseUrl/HopDong');
      final res = await (client?.post ?? http.post)(
        url,
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(data),
      ).timeout(const Duration(seconds: 10));

      if (res.statusCode == 200 || res.statusCode == 201) {
        dynamic decoded;
        try {
          decoded = res.body.isEmpty ? null : jsonDecode(res.body);
        } on FormatException {
          decoded = null;
        }
        final responseData = decoded is Map
            ? Map<String, dynamic>.from(decoded)
            : <String, dynamic>{};
        if (responseData['success'] == false) {
          return {
            'success': false,
            'message':
                responseData['message']?.toString() ??
                'Máy chủ từ chối tạo hợp đồng.',
          };
        }
        return {
          ...responseData,
          'success': true,
          'message':
              responseData['message']?.toString() ?? 'Tạo hợp đồng thành công',
        };
      } else {
        dynamic decoded;
        try {
          decoded = jsonDecode(res.body);
        } on FormatException {
          decoded = null;
        }
        return {
          'success': false,
          'message': decoded is Map && decoded['message'] != null
              ? decoded['message'].toString()
              : 'Lỗi (${res.statusCode}): ${res.body}',
        };
      }
    } catch (e) {
      return {'success': false, 'message': 'Lỗi kết nối API: $e'};
    }
  }

  // Thanh lý hợp đồng: khớp POST /api/HopDong/{id}/thanh-ly.
  static Future<Map<String, dynamic>> checkOut({
    required int contractId,
    required double tienHoanCoc,
    double tienPhat = 0,
    String ghiChu = '',
  }) async {
    if (contractId <= 0 || !tienHoanCoc.isFinite || tienHoanCoc < 0 ||
        !tienPhat.isFinite || tienPhat < 0) {
      return {'success': false, 'message': 'Thông tin quyết toán không hợp lệ.'};
    }
    try {
      final response = await http.post(
        Uri.parse('$baseUrl/HopDong/$contractId/thanh-ly'),
        headers: _cookieHeaders(),
        body: jsonEncode({
          'tienHoanCoc': tienHoanCoc,
          'tienPhat': tienPhat,
          'ghiChu': ghiChu.trim(),
        }),
      ).timeout(const Duration(seconds: 20));
      return _decodeMutationResponse(
        response,
        operation: 'Thanh lý hợp đồng',
        defaultSuccessMessage: 'Thanh lý hợp đồng thành công.',
      );
    } on TimeoutException {
      return {'success': false, 'message': 'Máy chủ không phản hồi sau 20 giây.'};
    } catch (error) {
      return {'success': false, 'message': 'Không kết nối được API thanh lý: $error'};
    }
  }

  // ADMIN - Danh sách yêu cầu trả phòng.
  static Future<List<dynamic>> getCheckoutRequests() async {
    final response = await http.get(
      Uri.parse('$baseUrl/BaoTraPhong'),
      headers: _cookieHeaders(),
    ).timeout(const Duration(seconds: 15));
    if (response.statusCode != 200) {
      throw Exception('HTTP ${response.statusCode}: ${response.body}');
    }
    final data = jsonDecode(response.body);
    if (data is! List) throw const FormatException('API không trả về danh sách.');
    return data;
  }

  // ADMIN - Duyệt hoặc từ chối yêu cầu (không thanh lý ngay).
  static Future<Map<String, dynamic>> processCheckoutRequest(
    int requestId, {required bool approve}) async {
    try {
      final action = approve ? 'duyet' : 'tu-choi';
      final response = await http.put(
        Uri.parse('$baseUrl/BaoTraPhong/$requestId/$action'),
        headers: _cookieHeaders(),
      ).timeout(const Duration(seconds: 15));
      final decoded = jsonDecode(response.body);
      final data = decoded is Map ? Map<String, dynamic>.from(decoded) : <String, dynamic>{};
      return {
        'success': response.statusCode >= 200 && response.statusCode < 300 && data['success'] == true,
        'message': data['message']?.toString() ?? 'HTTP ${response.statusCode}',
      };
    } catch (error) {
      return {'success': false, 'message': 'Không xử lý được yêu cầu: $error'};
    }
  }

  static Future<Map<String, dynamic>> createCheckoutRequest({
    required int contractId,
    required int customerId,
    required String reason,
    required String expectedReturnDate,
    required String bankInfo,
  }) async {
    try {
      final response = await http
          .post(
            Uri.parse('$baseUrl/BaoTraPhong'),
            headers: _cookieHeaders(),
            body: jsonEncode({
              'maHopDong': contractId,
              'maKhach': customerId,
              'lyDo': reason,
              'ngayTraDuKien': expectedReturnDate,
              'ghiChu':
                  'Ngày dự kiến bàn giao: $expectedReturnDate'
                  '${bankInfo.isEmpty ? '' : ' | Thông tin nhận cọc: $bankInfo'}',
            }),
          )
          .timeout(const Duration(seconds: 10));

      dynamic decoded;
      try {
        decoded = jsonDecode(response.body);
      } on FormatException {
        decoded = null;
      }
      final data = decoded is Map
          ? Map<String, dynamic>.from(decoded)
          : <String, dynamic>{};

      if (response.statusCode == 200 && data['success'] == true) {
        return {
          'success': true,
          'message':
              data['message']?.toString() ??
              'Đã gửi thông báo trả phòng đến chủ trọ.',
        };
      }

      return {
        'success': false,
        'message': data['message']?.toString().trim().isNotEmpty == true
            ? data['message'].toString()
            : 'Gửi yêu cầu trả phòng thất bại (${response.statusCode}).',
      };
    } catch (error) {
      return {
        'success': false,
        'message': 'Không thể kết nối để gửi yêu cầu trả phòng: $error',
      };
    }
  }
  static Future<Map<String, dynamic>> guiYeuCauHuy({
    required int maDatPhong,
    required String lyDo,
  }) async {
    if (maDatPhong <= 0 || lyDo.trim().isEmpty) {
      return {'success': false, 'message': 'Vui lòng nhập lý do hủy hợp lệ.'};
    }
    if (_sessionCookie == null) {
      return {
        'success': false,
        'message': 'Chưa có phiên đăng nhập. Vui lòng đăng xuất rồi đăng nhập lại.',
      };
    }
    try {
      final response = await http.post(
        Uri.parse('$baseUrl/YeuCauHuyDatPhong/$maDatPhong/gui'),
        headers: _cookieHeaders(),
        body: jsonEncode({'lyDo': lyDo.trim()}),
      ).timeout(const Duration(seconds: 15));
      dynamic decoded;
      try {
        decoded = jsonDecode(response.body);
      } on FormatException {
        decoded = null;
      }
      final body = decoded is Map
          ? Map<String, dynamic>.from(decoded)
          : <String, dynamic>{};
      final ok = response.statusCode >= 200 &&
          response.statusCode < 300 && body['success'] != false;
      return {
        ...body,
        'success': ok,
        'message': body['message']?.toString() ??
            (response.statusCode == 401
                ? 'Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại.'
                : response.statusCode == 403
                    ? 'Tài khoản không có quyền gửi yêu cầu hủy.'
                    : 'Không thể gửi yêu cầu (HTTP ${response.statusCode}).'),
      };
    } on TimeoutException {
      return {'success': false, 'message': 'Máy chủ không phản hồi sau 15 giây.'};
    } catch (e) {
      return {'success': false, 'message': 'Lỗi kết nối: $e'};
    }
  }


  // --- ADMIN: YÊU CẦU HỦY ĐẶT PHÒNG ---
  static Future<List<Map<String, dynamic>>> getCancellationRequests() async {
    final response = await http.get(
      Uri.parse('$baseUrl/YeuCauHuyDatPhong'),
      headers: _cookieHeaders(),
    ).timeout(const Duration(seconds: 15));
    if (response.statusCode != 200) {
      throw Exception('Không tải được yêu cầu hủy (HTTP ${response.statusCode}): ${response.body}');
    }
    final decoded = jsonDecode(response.body);
    if (decoded is! List) throw const FormatException('API yêu cầu hủy không trả về danh sách.');
    return decoded.whereType<Map>().map((e) => Map<String, dynamic>.from(e)).toList();
  }

  static Future<Map<String, dynamic>> processCancellationRequest({
    required int requestId,
    required bool approve,
    String note = '',
  }) async {
    try {
      final action = approve ? 'duyet' : 'tu-choi';
      final response = await http.put(
        Uri.parse('$baseUrl/YeuCauHuyDatPhong/$requestId/$action'),
        headers: _cookieHeaders(),
        body: jsonEncode({'ghiChu': note}),
      ).timeout(const Duration(seconds: 15));
      dynamic decoded;
      try { decoded = jsonDecode(response.body); } catch (_) { decoded = null; }
      final data = decoded is Map ? Map<String, dynamic>.from(decoded) : <String, dynamic>{};
      final ok = response.statusCode >= 200 && response.statusCode < 300 && data['success'] != false;
      return {
        ...data,
        'success': ok,
        'message': data['message']?.toString() ??
            (ok ? 'Đã xử lý yêu cầu.' : 'Không thể xử lý yêu cầu (HTTP ${response.statusCode}).'),
      };
    } catch (e) {
      return {'success': false, 'message': 'Lỗi kết nối: $e'};
    }
  }

  // --- BOOKINGS (Đặt Phòng) ---
  static Future<List<dynamic>> getBookings() async {
    try {
      final res = await http
          .get(Uri.parse('$baseUrl/DatPhong'))
          .timeout(const Duration(seconds: 10));
      if (res.statusCode == 200) return jsonDecode(res.body);
      throw Exception('Không thể tải đặt phòng (${res.statusCode})');
    } catch (e) {
      print('Lỗi getBookings: $e');
      rethrow;
    }
  }

  // Tạo phiếu đặt cọc: ánh xạ tên trường Flutter sang DTO ASP.NET Core.
  static Future<Map<String, dynamic>> createBooking(
    Map<String, dynamic> data,
  ) async {
    int? positiveId(List<String> keys) {
      for (final key in keys) {
        final raw = data[key];
        final id = raw is num
            ? raw.toInt()
            : int.tryParse(raw?.toString().trim() ?? '');
        if (id != null && id > 0) return id;
      }
      return null;
    }

    final maPhong = positiveId(['maPhong', 'MaPhong', 'roomId']);
    final maKhach = positiveId(['maKhach', 'MaKhach', 'customerId']);
    if (maPhong == null) {
      return {'success': false, 'message': 'Mã phòng không hợp lệ. Vui lòng chọn lại phòng.'};
    }
    if (maKhach == null) {
      return {'success': false, 'message': 'Mã khách thuê không hợp lệ. Vui lòng chọn lại khách.'};
    }

    final rawDeposit = data['tienCoc'] ?? data['TienCoc'] ?? data['depositAmount'];
    final tienCoc = rawDeposit is num
        ? rawDeposit.toDouble()
        : double.tryParse(rawDeposit?.toString().trim() ?? '');
    if (tienCoc == null || !tienCoc.isFinite || tienCoc <= 0) {
      return {'success': false, 'message': 'Số tiền đặt cọc phải lớn hơn 0.'};
    }

    final rawDate = data['ngayDuKienNhan'] ??
        data['NgayDuKienNhan'] ??
        data['expectedCheckIn'] ??
        data['expectedDate'];
    final expectedDate = rawDate?.toString().trim() ?? '';
    if (expectedDate.isEmpty || DateTime.tryParse(expectedDate) == null) {
      return {'success': false, 'message': 'Ngày dự kiến nhận phòng không hợp lệ.'};
    }

    final payload = <String, dynamic>{
      'maPhong': maPhong,
      'maKhach': maKhach,
      'tienCoc': tienCoc,
      'ngayDuKienNhan': expectedDate,
      'ghiChu': (data['ghiChu'] ?? data['note'] ?? '').toString(),
    };
    // Nếu backend yêu cầu ngày đặt, dùng ngày do màn hình cung cấp.
    final ngayDat = data['ngayDat'] ?? data['bookingDate'];
    if (ngayDat != null && ngayDat.toString().trim().isNotEmpty) {
      payload['ngayDat'] = ngayDat;
    }

    try {
      final url = Uri.parse('$baseUrl/DatPhong');
      debugPrint('[BOOKING] POST $url');
      debugPrint('[BOOKING] Request: ${jsonEncode(payload)}');
      final res = await http.post(
        url,
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(payload),
      ).timeout(const Duration(seconds: 15));
      debugPrint('[BOOKING] HTTP ${res.statusCode}: ${res.body}');

      dynamic decoded;
      try {
        decoded = res.body.isEmpty ? null : jsonDecode(res.body);
      } on FormatException {
        decoded = null;
      }
      final body = decoded is Map
          ? Map<String, dynamic>.from(decoded)
          : <String, dynamic>{};
      final success = res.statusCode >= 200 &&
          res.statusCode < 300 && body['success'] != false;
      return {
        ...body,
        'success': success,
        'message': body['message']?.toString() ??
            (success ? 'Đặt cọc thành công.' : 'Lỗi (${res.statusCode}): ${res.body}'),
      };
    } on TimeoutException {
      return {'success': false, 'message': 'Máy chủ đặt cọc không phản hồi sau 15 giây.'};
    } catch (error) {
      return {'success': false, 'message': 'Không thể kết nối API đặt cọc: $error'};
    }
  }

  // Cập nhật đặt chỗ. Endpoint phải khớp với DatPhongController ở backend.
  static Future<Map<String, dynamic>> updateBooking(
    int bookingId,
    Map<String, dynamic> data,
  ) async {
    if (bookingId <= 0) {
      return {
        'success': false,
        'message': 'Mã đặt phòng không hợp lệ: $bookingId.',
      };
    }

    final status = (data['status'] ?? data['trangThai'] ?? '').toString().trim();
    if (status != 'Đã hủy') {
      return {
        'success': false,
        'message': status == 'Đã nhận phòng'
            ? 'Backend chưa có API xác nhận nhận phòng. Cần bổ sung nghiệp vụ nhận phòng và hợp đồng.'
            : 'Backend hiện chỉ hỗ trợ hủy phiếu đang đặt cọc; không thể đổi sang trạng thái: $status.',
      };
    }
    final uri = Uri.parse('$baseUrl/DatPhong/$bookingId/huy');
    // Chính sách hoàn cọc do backend tự tính, không gửi lựa chọn hoàn tiền.
    final payload = <String, dynamic>{};
    try {
      debugPrint('[BOOKING] PUT $uri');
      debugPrint('[BOOKING] Request: ${jsonEncode(payload)}');
      final response = await http.put(
        uri,
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(payload),
      ).timeout(const Duration(seconds: 15));

      debugPrint('[BOOKING] HTTP ${response.statusCode}');
      debugPrint('[BOOKING] Response: ${response.body}');

      dynamic decoded;
      try {
        decoded = response.body.isEmpty ? null : jsonDecode(response.body);
      } on FormatException {
        decoded = null;
      }
      final body = decoded is Map
          ? Map<String, dynamic>.from(decoded)
          : <String, dynamic>{};
      final serverMessage = body['message']?.toString();

      if (response.statusCode >= 200 &&
          response.statusCode < 300 &&
          body['success'] != false) {
        return {
          ...body,
          'success': true,
          'message': serverMessage ?? 'Cập nhật đặt phòng thành công.',
        };
      }

      String message;
      if (response.statusCode == 404) {
        message = 'Không tìm thấy API hoặc đặt phòng #$bookingId (HTTP 404). '
            'Kiểm tra route PUT /api/DatPhong/{id} trong DatPhongController '
            'và xác nhận maDatPhong tồn tại. URL: $uri';
      } else if (response.statusCode == 405) {
        message = 'Backend không hỗ trợ phương thức PUT cho $uri (HTTP 405). '
            'Kiểm tra phương thức được khai báo trong DatPhongController.';
      } else if (response.statusCode == 400) {
        message = 'Dữ liệu cập nhật không hợp lệ (HTTP 400). '
            '${serverMessage ?? response.body}';
      } else {
        message = serverMessage ??
            'Cập nhật đặt phòng thất bại (HTTP ${response.statusCode}): '
                '${response.body}';
      }
      return {
        'success': false,
        'statusCode': response.statusCode,
        'url': uri.toString(),
        'message': message,
      };
    } on TimeoutException {
      return {
        'success': false,
        'message': 'API cập nhật đặt phòng quá thời gian chờ (15 giây): $uri',
      };
    } catch (error) {
      debugPrint('[BOOKING] Exception: $error');
      return {
        'success': false,
        'message': 'Không thể kết nối API cập nhật đặt phòng: $error',
      };
    }
  }

  // --- REPAIR REQUESTS (Yêu Cầu Sửa Chữa) ---
  static Future<List<dynamic>> getRepairRequests({http.Client? client}) async {
    final uri = Uri.parse('$baseUrl/SuaChua');
    final request = client == null ? http.get(uri) : client.get(uri);
    final response = await request.timeout(const Duration(seconds: 10));
    if (response.statusCode != 200) {
      throw Exception(
        'Không thể tải yêu cầu sửa chữa (${response.statusCode}): '
        '${response.body}',
      );
    }
    final decoded = jsonDecode(response.body);
    if (decoded is! List) {
      throw const FormatException(
        'API sửa chữa trả về danh sách không đúng định dạng.',
      );
    }
    return decoded;
  }

  static Future<Map<String, dynamic>> sendRepairRequest(
    Map<String, dynamic> data,
  ) async {
    try {
      final response = await http
          .post(
            Uri.parse('$baseUrl/SuaChua'),
            headers: {'Content-Type': 'application/json'},
            body: jsonEncode(data),
          )
          .timeout(const Duration(seconds: 10));
      return _repairApiResult(response, 'Gửi yêu cầu sửa chữa thành công.');
    } catch (error) {
      return {
        'success': false,
        'message': 'Không thể kết nối API sửa chữa: $error',
      };
    }
  }

  static Future<List<dynamic>> getRepairRequestsByCustomer(int maKhach) async {
    if (maKhach <= 0) {
      throw ArgumentError.value(maKhach, 'maKhach', 'Mã khách không hợp lệ.');
    }
    final decoded = await getRepairRequests();
    return decoded.where((request) {
      if (request is! Map) return false;
      final customerId = int.tryParse(
        (request['maKhach'] ?? request['MaKhach'] ?? '').toString(),
      );
      return customerId == maKhach;
    }).toList();
  }

  static Future<Map<String, dynamic>> updateRepairRequest(
    int requestId,
    Map<String, dynamic> data, {
    http.Client? client,
  }) async {
    if (requestId <= 0) {
      return {'success': false, 'message': 'Mã yêu cầu sửa chữa không hợp lệ.'};
    }
    try {
      final uri = Uri.parse('$baseUrl/SuaChua/$requestId');
      final request = client == null
          ? http.put(
              uri,
              headers: {'Content-Type': 'application/json'},
              body: jsonEncode(data),
            )
          : client.put(
              uri,
              headers: {'Content-Type': 'application/json'},
              body: jsonEncode(data),
            );
      final response = await request.timeout(const Duration(seconds: 10));
      return _repairApiResult(response, 'Đã cập nhật yêu cầu sửa chữa.');
    } catch (error) {
      return {
        'success': false,
        'message': 'Không thể kết nối API cập nhật sửa chữa: $error',
      };
    }
  }

  static Future<Map<String, dynamic>> confirmRepairRequest(int maYeuCau) async {
    try {
      final response = await http
          .put(
            Uri.parse('$baseUrl/SuaChua/$maYeuCau'),
            headers: {'Content-Type': 'application/json'},
            body: jsonEncode({'khachXacNhan': true}),
          )
          .timeout(const Duration(seconds: 10));
      return _repairApiResult(response, 'Đã xác nhận hoàn thành sửa chữa.');
    } catch (error) {
      return {
        'success': false,
        'message': 'Không thể kết nối API xác nhận sửa chữa: $error',
      };
    }
  }

  static Future<Map<String, dynamic>> deleteRepairRequest(int maYeuCau) async {
    try {
      final response = await http
          .delete(Uri.parse('$baseUrl/SuaChua/$maYeuCau'))
          .timeout(const Duration(seconds: 10));
      return _repairApiResult(response, 'Đã hủy yêu cầu sửa chữa.');
    } catch (error) {
      return {
        'success': false,
        'message': 'Không thể kết nối API hủy yêu cầu sửa chữa: $error',
      };
    }
  }

  static Map<String, dynamic> _repairApiResult(
    http.Response response,
    String defaultMessage,
  ) {
    dynamic decoded;
    try {
      decoded = jsonDecode(response.body);
    } on FormatException {
      decoded = null;
    }
    final data = decoded is Map
        ? Map<String, dynamic>.from(decoded)
        : <String, dynamic>{};
    final isSuccess =
        response.statusCode >= 200 &&
        response.statusCode < 300 &&
        data['success'] != false;
    return {
      'success': isSuccess,
      'message': data['message']?.toString().trim().isNotEmpty == true
          ? data['message'].toString()
          : isSuccess
          ? defaultMessage
          : 'API sửa chữa trả về lỗi (${response.statusCode}).',
      'data': data['data'],
    };
  }

  static Future<List<dynamic>> getInvoicesByCustomer(int maKhach) async {
    return getInvoicesForCustomer(maKhach);
  }

  static Future<List<dynamic>> getInvoicesForCustomer(
    int maKhach, {
    http.Client? client,
  }) async {
    final contractsData = await _getCustomerContractsForHistory(
      maKhach,
      client: client,
    );

    final invoicesByContract = await Future.wait(
      contractsData.whereType<Map>().map((contractValue) async {
        final contract = Map<String, dynamic>.from(contractValue);
        final contractId = int.tryParse(
          (contract['maHopDong'] ?? contract['MaHopDong'] ?? '').toString(),
        );
        if (contractId == null) return <Map<String, dynamic>>[];

        final invoices = await getInvoicesForContract(
          contractId,
          client: client,
        );
        return invoices.map((invoiceValue) {
          final invoice = Map<String, dynamic>.from(invoiceValue as Map);
          invoice.putIfAbsent(
            'maPhong',
            () => contract['maPhong'] ?? contract['MaPhong'],
          );
          return invoice;
        }).toList();
      }),
    );

    final invoices = invoicesByContract.expand((items) => items).toList();
    invoices.sort((left, right) {
      final leftYear =
          int.tryParse((left['nam'] ?? left['Nam'] ?? '').toString()) ?? 0;
      final rightYear =
          int.tryParse((right['nam'] ?? right['Nam'] ?? '').toString()) ?? 0;
      final yearOrder = rightYear.compareTo(leftYear);
      if (yearOrder != 0) return yearOrder;
      final leftMonth =
          int.tryParse((left['thang'] ?? left['Thang'] ?? '').toString()) ?? 0;
      final rightMonth =
          int.tryParse((right['thang'] ?? right['Thang'] ?? '').toString()) ??
          0;
      return rightMonth.compareTo(leftMonth);
    });
    return invoices;
  }

  static Future<List<dynamic>> getInvoicesByContract(int maHopDong) async {
    return getInvoicesForContract(maHopDong);
  }

  static Future<List<dynamic>> getInvoicesForContract(
    int maHopDong, {
    http.Client? client,
  }) async {
    final uri = Uri.parse('$baseUrl/HoaDon/hop-dong/$maHopDong');
    final request = client == null
        ? http.get(uri, headers: {'Content-Type': 'application/json'})
        : client.get(uri, headers: {'Content-Type': 'application/json'});
    final response = await request.timeout(const Duration(seconds: 10));
    if (response.statusCode != 200) {
      throw Exception(
        'Không thể tải hóa đơn hợp đồng #$maHopDong '
        '(${response.statusCode}): ${response.body}',
      );
    }

    final decoded = jsonDecode(response.body);
    if (decoded is! List) {
      throw const FormatException('API hóa đơn trả về dữ liệu không hợp lệ.');
    }
    return decoded;
  }

  static Future<Map<String, dynamic>> createManualInvoice({
    required int contractId,
    required int month,
    required int year,
    required double total,
    required String note,
    http.Client? client,
  }) async {
    try {
      final response = await (client?.post ?? http.post)(
        Uri.parse('$baseUrl/HoaDon'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'maHopDong': contractId,
          'thang': month,
          'nam': year,
          'tongTien': total,
          'ghiChu': note,
        }),
      ).timeout(const Duration(seconds: 15));

      dynamic decoded;
      try {
        decoded = jsonDecode(response.body);
      } on FormatException {
        decoded = null;
      }

      final result = decoded is Map
          ? Map<String, dynamic>.from(decoded)
          : <String, dynamic>{};
      if (response.statusCode >= 200 &&
          response.statusCode < 300 &&
          result['success'] == true) {
        return {
          ...result,
          'success': true,
          'message': result['message']?.toString() ?? 'Đã lập hóa đơn.',
        };
      }
      return {
        'success': false,
        'message': result['message']?.toString().trim().isNotEmpty == true
            ? result['message'].toString()
            : 'Không thể lập hóa đơn (${response.statusCode}).',
      };
    } catch (error) {
      return {
        'success': false,
        'message': 'Không thể kết nối để lập hóa đơn: $error',
      };
    }
  }

  static Future<List<Map<String, dynamic>>> getPendingInvoiceViolations({
    required int contractId,
    http.Client? client,
  }) async {
    if (contractId <= 0) {
      throw ArgumentError.value(contractId, 'contractId', 'Must be positive.');
    }

    final uri = Uri.parse(
      '$baseUrl/HoaDon/hop-dong/$contractId/vi-pham-chua-tinh',
    );
    final response = await (client?.get ?? http.get)(
      uri,
      headers: {'Content-Type': 'application/json'},
    ).timeout(const Duration(seconds: 10));
    final violations = _decodeListResponse(
      response,
      'Không thể tải các khoản phạt chưa tính vào hóa đơn',
    );
    return violations
        .whereType<Map>()
        .map((violation) => Map<String, dynamic>.from(violation))
        .toList();
  }

  static Future<Map<String, dynamic>> recognizeElectricMeter({
    required Uint8List imageBytes,
    required String filename,
    required int roomId,
    required double oldReading,
    required int month,
    required int year,
    http.Client? client,
    Duration timeout = const Duration(seconds: 70),
    Future<void>? abortTrigger,
  }) async {
    if (imageBytes.isEmpty) {
      return {
        'success': false,
        'message': 'Ảnh đồng hồ điện không có dữ liệu.',
      };
    }
    if (roomId <= 0 || oldReading < 0 || month < 1 || month > 12 || year < 1) {
      return {
        'success': false,
        'message': 'Thông tin đọc chỉ số không hợp lệ.',
      };
    }

    final request =
        http.AbortableMultipartRequest(
            'POST',
            Uri.parse(meterOcrUrl),
            abortTrigger: abortTrigger,
          )
          ..fields.addAll({
            'maPhong': roomId.toString(),
            'chiSoCu': oldReading.toString(),
            'thang': month.toString(),
            'nam': year.toString(),
          })
          ..files.add(
            http.MultipartFile.fromBytes(
              'image',
              imageBytes,
              filename: filename,
            ),
          );

    final requestClient = client ?? http.Client();
    try {
      final response = await requestClient
          .send(request)
          .then(http.Response.fromStream)
          .timeout(timeout);
      dynamic decoded;
      try {
        decoded = response.body.isEmpty ? null : jsonDecode(response.body);
      } on FormatException {
        decoded = null;
      }

      final body = decoded is Map
          ? Map<String, dynamic>.from(decoded)
          : <String, dynamic>{};
      if (response.statusCode < 200 || response.statusCode >= 300) {
        return {
          'success': false,
          'message':
              body['error']?.toString() ??
              body['message']?.toString() ??
              'OCR không đọc được ảnh (${response.statusCode}).',
        };
      }

      final reading = _firstValue(body, const [
        'chiSoMoi',
        'ChiSoMoi',
        'reading',
        'value',
      ]);
      final parsedReading = reading is num
          ? reading.toDouble()
          : double.tryParse(reading?.toString() ?? '');
      if (parsedReading == null || !parsedReading.isFinite) {
        return {
          'success': false,
          'message': 'OCR không trả về chỉ số điện hợp lệ.',
        };
      }
      if (parsedReading < oldReading) {
        return {
          'success': false,
          'message':
              'Chỉ số OCR ($parsedReading kWh) nhỏ hơn chỉ số cũ '
              '($oldReading kWh). Hãy kiểm tra lại ảnh và chỉ số.',
        };
      }

      return {
        'success': true,
        'chiSoMoi': parsedReading,
        'tieuThu': parsedReading - oldReading,
        'confidence': body['confidence']?.toString() ?? 'Không xác định',
        'needs_review': body['needs_review'] != false,
        'note': body['note']?.toString() ?? '',
      };
    } on http.RequestAbortedException {
      return {
        'success': false,
        'message': 'Đã dừng nhận diện ảnh đồng hồ điện.',
      };
    } on http.ClientException catch (error) {
      return {
        'success': false,
        'message': 'Không kết nối được máy chủ OCR: $error',
      };
    } on TimeoutException {
      return {
        'success': false,
        'message':
            'Máy chủ OCR không phản hồi sau ${timeout.inSeconds} giây. '
            'Kiểm tra Flask và Ollama đang chạy, rồi thử lại.',
      };
    } catch (error) {
      return {
        'success': false,
        'message': 'Không thể xử lý ảnh đồng hồ điện: $error',
      };
    } finally {
      if (client == null) requestClient.close();
    }
  }

  // Lấy chỉ số cuối kỳ trước theo phòng và loại chỉ số.
  // Truyền month/year của kỳ đang lập hóa đơn để tránh dùng nhầm tháng.
  static Future<double?> _getPreviousMeterReading(
    int roomId,
    String meterType, {
    int? month,
    int? year,
  }) async {
    if (roomId <= 0) return null;
    if ((month == null) != (year == null)) {
      throw ArgumentError('Phải truyền cả month và year hoặc không truyền cả hai.');
    }
    if (month != null && (month < 1 || month > 12 || year! < 1)) {
      throw ArgumentError('Kỳ hóa đơn không hợp lệ.');
    }

    final response = await http.get(
      Uri.parse('$baseUrl/ChiSoDienNuoc'),
      headers: _cookieHeaders(),
    ).timeout(const Duration(seconds: 12));
    final entries = _decodeListResponse(response, 'Không tải được lịch sử chỉ số');

    final targetPeriod = month == null ? null :
        (month == 1 ? (year! - 1) * 12 + 12 : year! * 12 + month - 1);
    final matches = <Map<String, dynamic>>[];
    for (final item in entries.whereType<Map>()) {
      final row = Map<String, dynamic>.from(item);
      final id = int.tryParse((row['maPhong'] ?? row['MaPhong'] ?? '').toString());
      final type = (row['loaiChiSo'] ?? row['LoaiChiSo'] ?? '').toString().trim().toLowerCase();
      final rowMonth = int.tryParse((row['thang'] ?? row['Thang'] ?? '').toString());
      final rowYear = int.tryParse((row['nam'] ?? row['Nam'] ?? '').toString());
      if (id != roomId || type != meterType.toLowerCase() || rowMonth == null || rowYear == null) continue;
      final period = rowYear * 12 + rowMonth;
      if (targetPeriod != null && period != targetPeriod) continue;
      matches.add(row);
    }
    matches.sort((a, b) {
      int period(Map<String, dynamic> row) =>
          (int.tryParse((row['nam'] ?? row['Nam']).toString()) ?? 0) * 12 +
          (int.tryParse((row['thang'] ?? row['Thang']).toString()) ?? 0);
      final order = period(b).compareTo(period(a));
      if (order != 0) return order;
      return (int.tryParse((b['maChiSo'] ?? b['MaChiSo']).toString()) ?? 0)
          .compareTo(int.tryParse((a['maChiSo'] ?? a['MaChiSo']).toString()) ?? 0);
    });
    for (final row in matches) {
      final raw = row['chiSoXacNhan'] ?? row['ChiSoXacNhan'] ??
          row['chiSoMoi'] ?? row['ChiSoMoi'];
      final value = double.tryParse(raw?.toString() ?? '');
      if (value != null && value.isFinite && value >= 0) return value;
    }
    return null;
  }

  static Future<double?> getPreviousElectricReading(
    int roomId, {int? month, int? year}) =>
      _getPreviousMeterReading(roomId, 'Điện', month: month, year: year);

  static Future<double?> getPreviousWaterReading(
    int roomId, {int? month, int? year}) =>
      _getPreviousMeterReading(roomId, 'Nước', month: month, year: year);

  static Future<List<dynamic>> getCustomerMeterReadings(
    int customerId, {
    http.Client? client,
  }) async {
    final contracts = await _getCustomerContractsForHistory(
      customerId,
      client: client,
    );
    final rooms = <int, dynamic>{};
    for (final value in contracts.whereType<Map>()) {
      final contract = Map<String, dynamic>.from(value);
      final roomId = int.tryParse(
        (contract['maPhong'] ?? contract['MaPhong'] ?? '').toString(),
      );
      if (roomId != null) {
        rooms[roomId] = contract['tenPhong'] ?? contract['TenPhong'];
      }
    }
    if (rooms.isEmpty) return [];

    final uri = Uri.parse('$baseUrl/ChiSoDienNuoc');
    final request = client == null
        ? http.get(uri, headers: {'Content-Type': 'application/json'})
        : client.get(uri, headers: {'Content-Type': 'application/json'});
    final response = await request.timeout(const Duration(seconds: 10));
    final readings = _decodeListResponse(
      response,
      'Không thể tải lịch sử chỉ số điện/nước',
    );
    final filtered = readings
        .whereType<Map>()
        .where((value) {
          final reading = Map<String, dynamic>.from(value);
          final roomId = int.tryParse(
            (reading['maPhong'] ?? reading['MaPhong'] ?? '').toString(),
          );
          return roomId != null && rooms.containsKey(roomId);
        })
        .map((value) {
          final reading = Map<String, dynamic>.from(value);
          final roomId = int.tryParse(
            (reading['maPhong'] ?? reading['MaPhong'] ?? '').toString(),
          );
          if (reading['tenPhong'] == null && reading['TenPhong'] == null) {
            reading['tenPhong'] = rooms[roomId] ?? 'Phòng $roomId';
          }
          return reading;
        })
        .toList();
    filtered.sort(_compareHistoryPeriodDescending);
    return filtered;
  }

  static Future<List<dynamic>> getCustomerServiceHistory(
    int customerId, {
    http.Client? client,
  }) async {
    final contracts = await _getCustomerContractsForHistory(
      customerId,
      client: client,
    );
    final historyByContract = await Future.wait(
      contracts.whereType<Map>().map((value) async {
        final contract = Map<String, dynamic>.from(value);
        final contractId = int.tryParse(
          (contract['maHopDong'] ?? contract['MaHopDong'] ?? '').toString(),
        );
        if (contractId == null) return <Map<String, dynamic>>[];
        final uri = Uri.parse('$baseUrl/DangKyDichVu?maHopDong=$contractId');
        final request = client == null
            ? http.get(uri, headers: {'Content-Type': 'application/json'})
            : client.get(uri, headers: {'Content-Type': 'application/json'});
        final response = await request.timeout(const Duration(seconds: 10));
        final registrations = _decodeListResponse(
          response,
          'Không thể tải dịch vụ của hợp đồng #$contractId',
        );
        return registrations.whereType<Map>().map((entry) {
          final registration = Map<String, dynamic>.from(entry);
          registration.putIfAbsent('maHopDong', () => contractId);
          registration.putIfAbsent(
            'maPhong',
            () => contract['maPhong'] ?? contract['MaPhong'],
          );
          return registration;
        }).toList();
      }),
    );
    final history = historyByContract.expand((entries) => entries).toList();
    history.sort((left, right) {
      final leftDate = DateTime.tryParse(
        (left['ngayBatDau'] ?? left['NgayBatDau'] ?? '').toString(),
      );
      final rightDate = DateTime.tryParse(
        (right['ngayBatDau'] ?? right['NgayBatDau'] ?? '').toString(),
      );
      return (rightDate ?? DateTime(0)).compareTo(leftDate ?? DateTime(0));
    });
    return history;
  }

  static Future<List<dynamic>> _getCustomerContractsForHistory(
    int customerId, {
    http.Client? client,
  }) async {
    if (customerId <= 0) {
      throw ArgumentError.value(
        customerId,
        'customerId',
        'Mã khách không hợp lệ.',
      );
    }
    final uri = Uri.parse('$baseUrl/HopDong/customer/$customerId');
    final request = client == null
        ? http.get(uri, headers: {'Content-Type': 'application/json'})
        : client.get(uri, headers: {'Content-Type': 'application/json'});
    final response = await request.timeout(const Duration(seconds: 10));
    return _decodeListResponse(response, 'Không thể tải hợp đồng khách thuê');
  }

  static List<dynamic> _decodeListResponse(
    http.Response response,
    String operation,
  ) {
    if (response.statusCode != 200) {
      throw Exception('$operation (${response.statusCode}): ${response.body}');
    }
    final decoded = jsonDecode(response.body);
    if (decoded is! List) {
      throw const FormatException('API trả về danh sách không đúng định dạng.');
    }
    return decoded;
  }

  static int _compareHistoryPeriodDescending(
    Map<String, dynamic> left,
    Map<String, dynamic> right,
  ) {
    final leftYear =
        int.tryParse((left['nam'] ?? left['Nam'] ?? '').toString()) ?? 0;
    final rightYear =
        int.tryParse((right['nam'] ?? right['Nam'] ?? '').toString()) ?? 0;
    final yearOrder = rightYear.compareTo(leftYear);
    if (yearOrder != 0) return yearOrder;
    final leftMonth =
        int.tryParse((left['thang'] ?? left['Thang'] ?? '').toString()) ?? 0;
    final rightMonth =
        int.tryParse((right['thang'] ?? right['Thang'] ?? '').toString()) ?? 0;
    return rightMonth.compareTo(leftMonth);
  }

  static Future<Map<String, dynamic>> collectInvoicePayment({
    required int invoiceId,
    required double amount,
    required String paymentMethod,
    String? note,
    http.Client? client,
  }) async {
    try {
      final response = await (client?.put ?? http.put)(
        Uri.parse('$baseUrl/HoaDon/$invoiceId/thu-tien'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'soTienThu': amount,
          'phuongThuc': paymentMethod,
          'ghiChu': note,
        }),
      ).timeout(const Duration(seconds: 15));

      dynamic decoded;
      try {
        decoded = jsonDecode(response.body);
      } on FormatException {
        decoded = null;
      }
      final result = decoded is Map
          ? Map<String, dynamic>.from(decoded)
          : <String, dynamic>{};

      if (response.statusCode >= 200 &&
          response.statusCode < 300 &&
          result['success'] == true) {
        return {
          'success': true,
          'message': result['message']?.toString() ?? 'Đã ghi nhận thanh toán.',
          'data': result['data'] is Map
              ? Map<String, dynamic>.from(result['data'] as Map)
              : <String, dynamic>{},
        };
      }

      return {
        'success': false,
        'message': result['message']?.toString().trim().isNotEmpty == true
            ? result['message'].toString()
            : 'Không thể thanh toán hóa đơn (${response.statusCode}).',
      };
    } catch (error) {
      return {
        'success': false,
        'message': 'Không thể kết nối để thanh toán hóa đơn: $error',
      };
    }
  }

  // --- VI PHAM (Vi phạm nội quy) ---
  static Future<List<dynamic>> getViPhams() async {
    try {
      final res = await http
          .get(Uri.parse('$baseUrl/ViPham'))
          .timeout(const Duration(seconds: 10));
      if (res.statusCode == 200) return jsonDecode(res.body);
      throw Exception('Không thể tải vi phạm (${res.statusCode})');
    } catch (e) {
      print('Lỗi getViPhams: $e');
      rethrow;
    }
  }

  static Future<Map<String, dynamic>> createViPham(
    Map<String, dynamic> data,
  ) async {
    try {
      final res = await http
          .post(
            Uri.parse('$baseUrl/ViPham'),
            headers: {'Content-Type': 'application/json'},
            body: jsonEncode(data),
          )
          .timeout(const Duration(seconds: 10));
      if (res.statusCode == 200 || res.statusCode == 201) {
        final decodedBody = res.body.isEmpty ? null : jsonDecode(res.body);
        if (decodedBody is Map) {
          final body = Map<String, dynamic>.from(decodedBody);
          return {...body, 'success': body['success'] ?? true};
        }
        return {'success': true, 'message': 'Ghi nhận vi phạm thành công'};
      }
      dynamic errorBody;
      try {
        errorBody = jsonDecode(res.body);
      } on FormatException {
        errorBody = null;
      }
      return {
        'success': false,
        'message': errorBody is Map
            ? errorBody['message'] ?? 'Lỗi (${res.statusCode})'
            : 'Lỗi (${res.statusCode}): ${res.body}',
      };
    } catch (e) {
      print('Lỗi createViPham: $e');
      return {'success': false, 'message': 'Lỗi kết nối API: $e'};
    }
  }

  // --- MESSAGES (Tin nhắn & Thông báo) ---
  static Future<List<dynamic>> getMessages(int senderId) async {
    try {
      final res = await http
          .get(Uri.parse('$baseUrl/TinNhan/$senderId'))
          .timeout(const Duration(seconds: 10));
      if (res.statusCode == 200) return jsonDecode(res.body);
      // Báo lỗi rõ ràng để màn hình chat không nhầm lỗi kết nối thành hội thoại trống.
      throw Exception('Không thể tải tin nhắn (${res.statusCode})');
    } catch (e) {
      print('Lỗi getMessages: $e');
      rethrow;
    }
  }

  static Future<bool> sendMessage(Map<String, dynamic> data) async {
    try {
      final res = await http.post(
        Uri.parse('$baseUrl/TinNhan'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(data),
      );
      return res.statusCode == 200 || res.statusCode == 201;
    } catch (e) {
      print('Lỗi sendMessage: $e');
      return false;
    }
  }

  static Future<bool> registerDeviceToken({
    required int customerId,
    required String token,
  }) async {
    try {
      final response = await http
          .post(
            Uri.parse('$baseUrl/TinNhan/register-device'),
            headers: {'Content-Type': 'application/json'},
            body: jsonEncode({
              'customerIdentity': customerId,
              'deviceToken': token,
            }),
          )
          .timeout(const Duration(seconds: 10));
      if (response.statusCode == 200 || response.statusCode == 201) {
        return true;
      }

      print(
        'Không đăng ký được FCM token (${response.statusCode}): '
        '${response.body}',
      );
      return false;
    } catch (error) {
      print('Lỗi registerDeviceToken: $error');
      return false;
    }
  }

  // --- THONG KE (Thống kê tài chính) ---
  static Future<Map<String, dynamic>> getFinancialStats() async {
    try {
      final res = await http
          .get(Uri.parse('$baseUrl/ThongKe'))
          .timeout(const Duration(seconds: 10));
      if (res.statusCode == 200) return jsonDecode(res.body);
      throw Exception('Không thể tải báo cáo (${res.statusCode})');
    } catch (e) {
      print('Lỗi getFinancialStats: $e');
      rethrow;
    }
  }

  // Báo cáo dùng trực tiếp hai API đang có để không phụ thuộc endpoint /ThongKe riêng.
  static Future<Map<String, List<dynamic>>> getReportSourceData() async {
    final responses = await Future.wait([
      http
          .get(Uri.parse('$baseUrl/Phong'))
          .timeout(const Duration(seconds: 10)),
      http
          .get(Uri.parse('$baseUrl/HoaDon'))
          .timeout(const Duration(seconds: 10)),
    ]);

    final roomResponse = responses[0];
    if (roomResponse.statusCode != 200) {
      throw Exception(
        'Không thể tải danh sách phòng (${roomResponse.statusCode}): '
        '${roomResponse.body}',
      );
    }

    final invoiceResponse = responses[1];
    if (invoiceResponse.statusCode != 200) {
      throw Exception(
        'Không thể tải danh sách hóa đơn (${invoiceResponse.statusCode}): '
        '${invoiceResponse.body}',
      );
    }

    final decodedRooms = jsonDecode(roomResponse.body);
    final decodedInvoices = jsonDecode(invoiceResponse.body);
    if (decodedRooms is! List || decodedInvoices is! List) {
      throw const FormatException(
        'API phòng hoặc hóa đơn không trả về danh sách JSON hợp lệ.',
      );
    }

    return {'rooms': decodedRooms, 'invoices': decodedInvoices};
  }

  // Thông báo: sử dụng cookie phiên đăng nhập hiện tại.
  static Future<List<Map<String, dynamic>>> getCustomerNotifications(int maKhach) async {
    final res = await http.get(Uri.parse('$baseUrl/ThongBao/khach/$maKhach'),
      headers: _cookieHeaders()).timeout(const Duration(seconds: 12));
    if (res.statusCode != 200) throw Exception('Không tải được thông báo: HTTP ${res.statusCode}');
    final decoded = jsonDecode(res.body);
    if (decoded is! List) throw const FormatException('Thông báo không phải danh sách');
    return decoded.map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }

  static Future<String> sendBroadcastNotification({required String title, required String content}) async {
    final res = await http.post(Uri.parse('$baseUrl/ThongBao'),
      headers: _cookieHeaders(),
      body: jsonEncode({'tieuDe': title, 'noiDung': content,
        'loaiThongBao': 'Thông báo chung', 'guiChoTatCa': true}),
    ).timeout(const Duration(seconds: 20));
    final decoded = jsonDecode(res.body);
    final message = decoded is Map ? (decoded['message'] ?? '').toString() : '';
    if (res.statusCode < 200 || res.statusCode >= 300) {
      throw Exception(message.isEmpty ? 'HTTP ${res.statusCode}' : message);
    }
    return message.isEmpty ? 'Đã gửi thông báo' : message;
  }


  // --- YÊU CẦU GIA HẠN HỢP ĐỒNG ---
  // Cookie đăng nhập bắt buộc: backend phân quyền Admin/Customer.
  static Future<List<dynamic>> getContractExtensionRequests() async {
    final response = await http.get(
      Uri.parse('$baseUrl/GiaHanHopDong'),
      headers: _cookieHeaders(),
    ).timeout(const Duration(seconds: 15));
    if (response.statusCode != 200) {
      throw Exception('Không tải được yêu cầu gia hạn (HTTP ${response.statusCode}): ${response.body}');
    }
    final decoded = jsonDecode(response.body);
    if (decoded is! List) {
      throw const FormatException('API gia hạn không trả về danh sách.');
    }
    return decoded;
  }

  static Future<Map<String, dynamic>> sendContractExtensionRequest({
    required int maHopDong,
    required String ngayKetThucMoi,
    String lyDo = '',
  }) async {
    if (maHopDong <= 0 || DateTime.tryParse(ngayKetThucMoi) == null) {
      return {'success': false, 'message': 'Hợp đồng hoặc ngày gia hạn không hợp lệ.'};
    }
    try {
      final response = await http.post(
        Uri.parse('$baseUrl/GiaHanHopDong/gui'),
        headers: _cookieHeaders(),
        body: jsonEncode({
          'maHopDong': maHopDong,
          'ngayKetThucMoi': ngayKetThucMoi,
          'lyDo': lyDo.trim(),
        }),
      ).timeout(const Duration(seconds: 15));
      return _decodeMutationResponse(
        response,
        operation: 'Gửi yêu cầu gia hạn',
        defaultSuccessMessage: 'Đã gửi yêu cầu gia hạn đến chủ trọ.',
      );
    } on TimeoutException {
      return {'success': false, 'message': 'Máy chủ không phản hồi sau 15 giây.'};
    } catch (e) {
      return {'success': false, 'message': 'Không gửi được yêu cầu gia hạn: $e'};
    }
  }

  static Future<Map<String, dynamic>> processContractExtensionRequest({
    required int maGiaHan,
    required bool approve,
    String ghiChu = '',
  }) async {
    if (maGiaHan <= 0) {
      return {'success': false, 'message': 'Mã yêu cầu gia hạn không hợp lệ.'};
    }
    try {
      final action = approve ? 'duyet' : 'tu-choi';
      final response = await http.put(
        Uri.parse('$baseUrl/GiaHanHopDong/$maGiaHan/$action'),
        headers: _cookieHeaders(),
        body: jsonEncode({'ghiChu': ghiChu.trim()}),
      ).timeout(const Duration(seconds: 15));
      return _decodeMutationResponse(
        response,
        operation: approve ? 'Duyệt gia hạn' : 'Từ chối gia hạn',
        defaultSuccessMessage: approve ? 'Đã duyệt gia hạn.' : 'Đã từ chối yêu cầu.',
      );
    } on TimeoutException {
      return {'success': false, 'message': 'Máy chủ không phản hồi sau 15 giây.'};
    } catch (e) {
      return {'success': false, 'message': 'Không xử lý được yêu cầu gia hạn: $e'};
    }
  }

  // Tải hóa đơn PDF, gửi kèm cookie đăng nhập ASP.NET Core.
  static Future<Uint8List> downloadInvoicePdf(int invoiceId) async {
    if (invoiceId <= 0) {
      throw ArgumentError.value(invoiceId, 'invoiceId', 'Mã hóa đơn không hợp lệ.');
    }
    final response = await http.get(
      Uri.parse('$baseUrl/HoaDon/$invoiceId/pdf'),
      headers: {
        ..._cookieHeaders(),
        'Accept': 'application/pdf',
      },
    ).timeout(const Duration(seconds: 45));

    if (response.statusCode == 401) {
      throw Exception('Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.');
    }
    if (response.statusCode == 403) {
      throw Exception('Bạn không có quyền xem hóa đơn này.');
    }
    if (response.statusCode == 404) {
      throw Exception('Không tìm thấy hóa đơn #$invoiceId.');
    }
    if (response.statusCode != 200) {
      throw Exception('Không thể tải PDF (HTTP ${response.statusCode}).');
    }
    final bytes = response.bodyBytes;
    if (bytes.length < 5 ||
        bytes[0] != 0x25 || bytes[1] != 0x50 ||
        bytes[2] != 0x44 || bytes[3] != 0x46 || bytes[4] != 0x2D) {
      throw const FormatException('Máy chủ không trả về file PDF hợp lệ.');
    }
    return bytes;
  }

}
