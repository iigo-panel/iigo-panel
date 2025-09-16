using IIGO.Models;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services.Interfaces
{
    [Obfuscation(Exclude = true)]
    public interface IMessengerService
    {
        string ServiceName { get; }
        string ServiceIdentifier { get; }
        bool IsEmail { get; }
        Task<bool> SendMessageAsync(MessageData message, CancellationToken ct);
        void Initialize();
    }
}
