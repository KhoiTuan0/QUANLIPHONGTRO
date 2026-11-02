double calculateProratedRent({
  required double monthlyRent,
  required DateTime from,
  required DateTime through,
}) {
  if (monthlyRent < 0) {
    throw ArgumentError.value(monthlyRent, 'monthlyRent');
  }
  if (from.year != through.year || from.month != through.month) {
    throw ArgumentError('Rent period must be within a single calendar month.');
  }
  if (through.isBefore(from)) {
    throw ArgumentError('The end date must not be before the start date.');
  }

  final daysInMonth = DateTime(from.year, from.month + 1, 0).day;
  final occupiedDays = through.day - from.day + 1;
  return (monthlyRent * occupiedDays / daysInMonth).roundToDouble();
}

double calculateMeterCharge({
  required double oldReading,
  required double currentReading,
  required double unitPrice,
}) {
  if (!oldReading.isFinite || oldReading < 0) {
    throw ArgumentError.value(oldReading, 'oldReading');
  }
  if (!currentReading.isFinite || currentReading < oldReading) {
    throw ArgumentError.value(currentReading, 'currentReading');
  }
  if (!unitPrice.isFinite || unitPrice <= 0) {
    throw ArgumentError.value(unitPrice, 'unitPrice');
  }
  return ((currentReading - oldReading) * unitPrice).roundToDouble();
}
