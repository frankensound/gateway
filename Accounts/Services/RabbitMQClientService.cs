using Accounts.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace Accounts.Services
{
    public class RabbitMQClientService : IMessagePublisher, IMessageConsumer
    {
        private readonly ConnectionFactory _connectionFactory;

        public RabbitMQClientService(IConfiguration configuration)
        {
            _connectionFactory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:HostName"]
            };
        }

        public void Publish(string queue, string message)
        {
            using var connection = _connectionFactory.CreateConnection();
            using var channel = connection.CreateModel();

            DeclareQueue(channel, queue);

            var body = Encoding.UTF8.GetBytes(message);
            channel.BasicPublish(exchange: "", routingKey: queue, basicProperties: null, body: body);
        }

        public void Consume(string queue, Action<string> onMessage, CancellationToken cancellationToken)
        {
            var connection = _connectionFactory.CreateConnection();
            var channel = connection.CreateModel();

            DeclareQueue(channel, queue);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                // Process messages in parallel
                Task.Run(() => onMessage(message), cancellationToken);
            };

            channel.BasicConsume(queue: queue, autoAck: false, consumer: consumer);

            cancellationToken.WaitHandle.WaitOne();

            // Clean up resources after cancellation
            channel.Close();
            connection.Close();
        }

        private void DeclareQueue(IModel channel, string queueName)
        {
            channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
        }
    }
}
