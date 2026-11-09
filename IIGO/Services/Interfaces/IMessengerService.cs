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
        IReadOnlyList<MonitorSetting> Settings { get; }

        /// <summary>
        /// Sends the message. Returns false or throws when it couldn't be sent; the exception message explains why.
        /// </summary>
        Task<bool> SendMessageAsync(MessageData message, CancellationToken ct);
        void Initialize();
    }
}
