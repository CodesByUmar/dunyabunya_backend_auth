namespace AuthApi.Services;

public interface IGeocodingService
{
    // Manzil matnidan koordinatani topadi. Topilmasa yoki xizmat ishlamasa —
    // null qaytaradi (chaqiruvchi buni "aniqlab bo'lmadi" deb qabul qilishi kerak,
    // bu buyurtma yaratilishini bloklamasligi kerak).
    Task<(double Lat, double Lng)?> GeocodeAsync(string address);
}
