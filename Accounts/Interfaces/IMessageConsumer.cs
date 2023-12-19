using System.Security.Cryptography;

namespace Accounts.Interfaces
{
    public interface IMessageConsumer
    {
        void Consume(string queue, Action<string> onMessage, CancellationToken cancellationToken);
    }
}
