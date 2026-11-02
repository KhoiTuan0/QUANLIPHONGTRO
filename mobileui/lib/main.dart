import 'theme/app_theme.dart';
import 'package:flutter/material.dart';
import 'package:firebase_core/firebase_core.dart';
// Nếu MainNavigatorScreen hoặc LoginScreen của bạn nằm ở file main_screen.dart, hãy đảm bảo import đúng:
// import 'screens/main_screen.dart'; 
import 'screens/login_screen.dart'; // Bắt đầu từ màn hình đăng nhập hoặc main_screen tùy logic của bạn
import 'services/push_notification_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await Firebase.initializeApp();
  await PushNotificationService.initialize();
  runApp(const QuanLyPhongTroApp());
}

class QuanLyPhongTroApp extends StatelessWidget {
  const QuanLyPhongTroApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Quản Lý Phòng Trọ',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      // Bạn có thể đổi điểm khởi đầu là LoginScreen hoặc MainScreen tùy ý
      home: const LoginScreen(), 
    );
  }
}