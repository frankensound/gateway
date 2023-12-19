namespace Accounts.Interfaces
{
    public interface IMessagePublisher
    {
        void Publish(string queue, string message);
    }
}
