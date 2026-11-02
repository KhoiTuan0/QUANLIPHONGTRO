import 'dart:async';

import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'api_service.dart';

class PushNotificationService {
  PushNotificationService._();

  static final FirebaseMessaging _messaging = FirebaseMessaging.instance;
  static final FlutterLocalNotificationsPlugin _localNotifications =
      FlutterLocalNotificationsPlugin();
  static const AndroidNotificationChannel _channel = AndroidNotificationChannel(
    'landlord_messages',
    'Thông báo chủ trọ',
    description: 'Tin nhắn và thông báo mới từ chủ trọ',
    importance: Importance.high,
  );

  /// CustomerHomeScreen lắng nghe để chuyển sang tab Thông báo.
  static final ValueNotifier<int> messageVersion = ValueNotifier<int>(0);
  static final ValueNotifier<int> openMessagesVersion = ValueNotifier<int>(0);
  static int? _customerId;
  static String? _currentToken;
  static bool _initialized = false;

  static Future<void> initialize() async {
    if (_initialized) return;
    _initialized = true;
    try {
      await _messaging.requestPermission(alert: true, badge: true, sound: true);
      const androidSettings = AndroidInitializationSettings('@mipmap/ic_launcher');
      await _localNotifications.initialize(
        settings: const InitializationSettings(android: androidSettings),
        onDidReceiveNotificationResponse: (_) => _openMessages(),
      );
      final androidPlugin = _localNotifications
          .resolvePlatformSpecificImplementation<AndroidFlutterLocalNotificationsPlugin>();
      await androidPlugin?.createNotificationChannel(_channel);
      await androidPlugin?.requestNotificationsPermission();

      FirebaseMessaging.onMessage.listen((message) {
        messageVersion.value++;
        unawaited(_showForegroundNotification(message));
      });
      FirebaseMessaging.onMessageOpenedApp.listen((_) => _openMessages());
      final initial = await _messaging.getInitialMessage();
      if (initial != null) _openMessages();

      _currentToken = await _messaging.getToken();
      if (kDebugMode) debugPrint('FCM token: $_currentToken');
      _messaging.onTokenRefresh.listen((token) {
        _currentToken = token;
        unawaited(_registerTokenForCurrentCustomer());
      });
    } catch (error) {
      _initialized = false;
      if (kDebugMode) debugPrint('FCM initialization failed: $error');
    }
  }

  static void _openMessages() {
    openMessagesVersion.value++;
    messageVersion.value++;
  }

  static Future<void> registerCustomer(int customerId) async {
    _customerId = customerId;
    try {
      _currentToken ??= await _messaging.getToken();
      await _registerTokenForCurrentCustomer();
    } catch (error) {
      if (kDebugMode) debugPrint('FCM token unavailable: $error');
    }
  }

  static Future<void> _registerTokenForCurrentCustomer() async {
    final customerId = _customerId;
    final token = _currentToken;
    if (customerId == null || token == null || token.isEmpty) return;
    try {
      final registered = await ApiService.registerDeviceToken(
        customerId: customerId,
        token: token,
      );
      if (!registered && kDebugMode) {
        debugPrint('Backend chưa đăng ký được FCM token cho khách #$customerId.');
      }
    } catch (error) {
      if (kDebugMode) debugPrint('FCM registration error: $error');
    }
  }

  static Future<void> _showForegroundNotification(RemoteMessage message) async {
    if (_customerId == null) return;
    final title = message.notification?.title ?? message.data['title']?.toString();
    final body = message.notification?.body ?? message.data['body']?.toString();
    if (title == null && body == null) return;
    try {
      await _localNotifications.show(
        id: (message.messageId?.hashCode ?? DateTime.now().millisecondsSinceEpoch) & 0x7fffffff,
        title: title ?? 'Thông báo từ chủ trọ',
        body: body ?? '',
        notificationDetails: NotificationDetails(
          android: AndroidNotificationDetails(
            _channel.id,
            _channel.name,
            channelDescription: _channel.description,
            importance: Importance.high,
            priority: Priority.high,
            icon: '@mipmap/ic_launcher',
          ),
        ),
      );
    } catch (error) {
      if (kDebugMode) debugPrint('Local notification error: $error');
    }
  }

  static Future<void> clearCustomer() async {
    _customerId = null;
  }
}
