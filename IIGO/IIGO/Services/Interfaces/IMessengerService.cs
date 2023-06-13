using IIGO.Models;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services.Interfaces
{
    public interface IMessengerService
    {
        string ServiceName { get; }
        Task<bool> SendMessageAsync(MessageData message, CancellationToken ct);
    }
}
