import 'package:flutter/material.dart';
import 'main_screen.dart'; // Màn hình chính của Chủ trọ
import 'customer_home_screen.dart'; // Màn hình chính của Khách hàng
import 'register_screen.dart'; // Màn hình đăng ký
import '../services/api_service.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _usernameController = TextEditingController();
  final _passwordController = TextEditingController();

  // Mặc định chọn vai trò là Khách hàng
  String _selectedRole = 'Customer';
  bool _isLoading = false;
  bool _obscurePassword = true;


  Future<void> _handleLogin() async {
    final username = _usernameController.text.trim();
    final password = _passwordController.text;

    if (username.isEmpty || password.isEmpty) {
      _showSnackBar(
        'Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu!',
        isError: true,
      );
      return;
    }

    FocusManager.instance.primaryFocus?.unfocus();
    setState(() => _isLoading = true);

    try {
      final response = await ApiService.login(username, password);

      if (!mounted) return;
      setState(() => _isLoading = false);

      if (response['success'] == true) {
        final serverRole = response['vaiTro'] as String?;
        if (serverRole == null) {
          _showSnackBar(
            'Máy chủ không trả về vai trò tài khoản. Vui lòng kiểm tra AuthController.',
            isError: true,
          );
          return;
        }

        // Không cho vào nhầm khu vực nếu vai trò chọn trên form không khớp tài khoản.
        if (serverRole != _selectedRole) {
          final requiredRole = serverRole == 'Admin'
              ? 'Chủ trọ (Quản lý)'
              : 'Khách hàng (Người thuê trọ)';
          _showSnackBar(
            'Tài khoản này thuộc vai trò "$requiredRole". '
            'Hãy chọn đúng vai trò rồi đăng nhập lại.',
            isError: true,
          );
          return;
        }

        if (serverRole == 'Admin') {
          Navigator.pushReplacement(
            context,
            MaterialPageRoute(builder: (context) => const MainScreen()),
          );
        } else {
          final maKhach = int.tryParse(response['maKhach']?.toString() ?? '');
          if (maKhach == null || maKhach <= 0) {
            _showSnackBar(
              'API đăng nhập chưa trả về mã khách hợp lệ. '
              'Vui lòng báo quản trị viên kiểm tra AuthController.',
              isError: true,
            );
            return;
          }
          Navigator.pushReplacement(
            context,
            MaterialPageRoute(
              builder: (context) => CustomerHomeScreen(maKhach: maKhach),
            ),
          );
        }
      } else {
        _showSnackBar(
          response['message'] ?? 'Sai thông tin đăng nhập!',
          isError: true,
        );
      }
    } catch (e) {
      if (!mounted) return;
      setState(() => _isLoading = false);
      _showSnackBar('Đăng nhập thất bại: $e', isError: true);
    }
  }

  @override
  void dispose() {
    _usernameController.dispose();
    _passwordController.dispose();
    super.dispose();
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

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC), // Nền sáng sủa, hiện đại
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(
              horizontal: 24.0,
              vertical: 16.0,
            ),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // Logo hoặc Biểu tượng ứng dụng
                Container(
                  alignment: Alignment.center,
                  child: Container(
                    padding: const EdgeInsets.all(16),
                    decoration: BoxDecoration(
                      color: const Color(0xFF4F46E5).withOpacity(0.1),
                      shape: BoxShape.circle,
                    ),
                    child: const Icon(
                      Icons.apartment_rounded,
                      size: 64,
                      color: Color(0xFF4F46E5),
                    ),
                  ),
                ),
                const SizedBox(height: 16),
                const Text(
                  'Chào mừng trở lại!',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    fontSize: 26,
                    fontWeight: FontWeight.bold,
                    color: Color(0xFF172554),
                  ),
                ),
                const SizedBox(height: 6),
                const Text(
                  'Đăng nhập để quản lý phòng trọ thông minh',
                  textAlign: TextAlign.center,
                  style: TextStyle(fontSize: 14, color: Color(0xFF64748B)),
                ),
                const SizedBox(height: 32),

                // Thẻ chứa form đăng nhập (Card nổi bật, sạch sẽ)
                Container(
                  padding: const EdgeInsets.all(20),
                  decoration: BoxDecoration(
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(20),
                    boxShadow: [
                      BoxShadow(
                        color: Colors.black.withOpacity(0.04),
                        blurRadius: 15,
                        offset: const Offset(0, 5),
                      ),
                    ],
                    border: Border.all(color: const Color(0xFFE2E8F0)),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      // Chọn vai trò đăng nhập
                      DropdownButtonFormField<String>(
                        initialValue: _selectedRole,
                        decoration: InputDecoration(
                          labelText: 'Đăng nhập với tư cách',
                          labelStyle: const TextStyle(
                            color: Color(0xFF64748B),
                            fontSize: 14,
                          ),
                          prefixIcon: const Icon(
                            Icons.badge_outlined,
                            color: Color(0xFF4F46E5),
                          ),
                          border: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(12),
                            borderSide: const BorderSide(
                              color: Color(0xFFCBD5E1),
                            ),
                          ),
                          enabledBorder: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(12),
                            borderSide: const BorderSide(
                              color: Color(0xFFCBD5E1),
                            ),
                          ),
                          focusedBorder: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(12),
                            borderSide: const BorderSide(
                              color: Color(0xFF4F46E5),
                              width: 2,
                            ),
                          ),
                          filled: true,
                          fillColor: const Color(0xFFF5F7FC),
                        ),
                        onChanged: _isLoading
                            ? null
                            : (value) {
                                if (value == null) return;
                                setState(() => _selectedRole = value);
                              },
                        items: const [
                          DropdownMenuItem(
                            value: 'Customer',
                            child: Text('Khách hàng (Người thuê trọ)'),
                          ),
                          DropdownMenuItem(
                            value: 'Admin',
                            child: Text('Chủ trọ (Quản lý)'),
                          ),
                        ],
                      ),
                      const Padding(
                        padding: EdgeInsets.only(top: 8, left: 4),
                        child: Text(
                          'Vai trò phải khớp với tài khoản đã được cấp. '
                          'Ứng dụng sẽ kiểm tra vai trò từ máy chủ trước khi đăng nhập.',
                          style: TextStyle(
                            color: Color(0xFF64748B),
                            fontSize: 11,
                            height: 1.35,
                          ),
                        ),
                      ),
                      const SizedBox(height: 16),

                      // Ô nhập Tên đăng nhập
                      TextField(
                        controller: _usernameController,
                        enabled: !_isLoading,
                        decoration: InputDecoration(
                          labelText: 'Tên đăng nhập',
                          labelStyle: const TextStyle(
                            color: Color(0xFF64748B),
                            fontSize: 14,
                          ),
                          prefixIcon: const Icon(
                            Icons.person_outline,
                            color: Color(0xFF4F46E5),
                          ),
                          border: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(12),
                            borderSide: const BorderSide(
                              color: Color(0xFFCBD5E1),
                            ),
                          ),
                          enabledBorder: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(12),
                            borderSide: const BorderSide(
                              color: Color(0xFFCBD5E1),
                            ),
                          ),
                          focusedBorder: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(12),
                            borderSide: const BorderSide(
                              color: Color(0xFF4F46E5),
                              width: 2,
                            ),
                          ),
                          filled: true,
                          fillColor: const Color(0xFFF5F7FC),
                        ),
                      ),
                      const SizedBox(height: 16),

                      // Ô nhập Mật khẩu
                      TextField(
                        controller: _passwordController,
                        enabled: !_isLoading,
                        obscureText: _obscurePassword,
                        decoration: InputDecoration(
                          labelText: 'Mật khẩu',
                          labelStyle: const TextStyle(
                            color: Color(0xFF64748B),
                            fontSize: 14,
                          ),
                          prefixIcon: const Icon(
                            Icons.lock_outline,
                            color: Color(0xFF4F46E5),
                          ),
                          suffixIcon: IconButton(
                            icon: Icon(
                              _obscurePassword
                                  ? Icons.visibility_off_outlined
                                  : Icons.visibility_outlined,
                              color: const Color(0xFF64748B),
                            ),
                            onPressed: () {
                              setState(() {
                                _obscurePassword = !_obscurePassword;
                              });
                            },
                          ),
                          border: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(12),
                            borderSide: const BorderSide(
                              color: Color(0xFFCBD5E1),
                            ),
                          ),
                          enabledBorder: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(12),
                            borderSide: const BorderSide(
                              color: Color(0xFFCBD5E1),
                            ),
                          ),
                          focusedBorder: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(12),
                            borderSide: const BorderSide(
                              color: Color(0xFF4F46E5),
                              width: 2,
                            ),
                          ),
                          filled: true,
                          fillColor: const Color(0xFFF5F7FC),
                        ),
                      ),
                      const SizedBox(height: 24),

                      // Nút Đăng nhập
                      SizedBox(
                        height: 50,
                        child: ElevatedButton(
                          style: ElevatedButton.styleFrom(
                            backgroundColor: const Color(0xFF4F46E5),
                            foregroundColor: Colors.white,
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(12),
                            ),
                            elevation: 0,
                          ),
                          onPressed: _isLoading ? null : _handleLogin,
                          child: _isLoading
                              ? const SizedBox(
                                  width: 24,
                                  height: 24,
                                  child: CircularProgressIndicator(
                                    color: Colors.white,
                                    strokeWidth: 2,
                                  ),
                                )
                              : const Text(
                                  'Đăng nhập',
                                  style: TextStyle(
                                    fontSize: 16,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 24),

                const SizedBox(height: 24),

                // Chuyển sang màn hình Đăng ký
                Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    const Text(
                      'Chưa có tài khoản? ',
                      style: TextStyle(color: Color(0xFF64748B)),
                    ),
                    GestureDetector(
                      onTap: () {
                        Navigator.push(
                          context,
                          MaterialPageRoute(
                            builder: (context) => const RegisterScreen(),
                          ),
                        );
                      },

                      child: const Text(
                        'Đăng ký ngay',
                        style: TextStyle(
                          color: Color(0xFF4F46E5),
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
