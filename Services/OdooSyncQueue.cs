using System.Threading.Channels;

namespace AuthApi.Services;

/// <summary>
/// Registratsiya paytida Odoo sinxronizatsiyasini kutmasdan javob qaytarish uchun —
/// yangi user ID shu navbatga qo'yiladi, OdooRetryBackgroundService uni fonda,
/// deyarli darhol qayta ishlaydi (15 daqiqalik davriy tekshiruvni kutmasdan).
/// </summary>
public interface IOdooSyncQueue
{
    void Enqueue(int userId);
    IAsyncEnumerable<int> DequeueAllAsync(CancellationToken cancellationToken);
}

public class OdooSyncQueue : IOdooSyncQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public void Enqueue(int userId) => _channel.Writer.TryWrite(userId);

    public IAsyncEnumerable<int> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
