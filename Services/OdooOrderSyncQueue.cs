using System.Threading.Channels;

namespace AuthApi.Services;

/// <summary>
/// Buyurtma yaratilgach, Odoo'ga sale.order sifatida yuborishni kutmasdan javob
/// qaytarish uchun — yangi order ID shu navbatga qo'yiladi, OdooOrderRetryBackgroundService
/// uni fonda, deyarli darhol qayta ishlaydi.
/// </summary>
public interface IOdooOrderSyncQueue
{
    void Enqueue(int orderId);
    IAsyncEnumerable<int> DequeueAllAsync(CancellationToken cancellationToken);
}

public class OdooOrderSyncQueue : IOdooOrderSyncQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public void Enqueue(int orderId) => _channel.Writer.TryWrite(orderId);

    public IAsyncEnumerable<int> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
