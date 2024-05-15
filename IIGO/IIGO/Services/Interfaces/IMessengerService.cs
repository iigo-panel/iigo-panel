using IIGO.Models;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services.Interfaces
{
    internal interface IMessengerService
    {
        string ServiceName { get; }
        string ServiceIdentifier { get; }
        bool IsEmail { get; }
        Task<bool> SendMessageAsync(MessageData message, CancellationToken ct);
        void Initialize();
    }
}
