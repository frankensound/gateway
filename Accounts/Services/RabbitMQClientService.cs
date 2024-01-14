using Accounts.Interfaces;
using Accounts.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace Accounts.Services
{
    public class RabbitMQClientService : IMessagePublisher, IMessageConsumer
    {
        private readonly ConnectionFactory _connectionFactory;
        private readonly MongoDbService _mongoDbService;

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
                ProcessReceivedMessage(message, onMessage, cancellationToken);
            };

            channel.BasicConsume(queue: queue, autoAck: false, consumer: consumer);

            cancellationToken.WaitHandle.WaitOne();

            // Clean up resources after cancellation
            channel.Close();
            connection.Close();
        }

        private void DeclareQueue(IModel channel, string queueName)
        {
            channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
        }

        private(string, UserActivity, string) TranslateToUserActivity(string message)
        {
            try
            {
                var json = JObject.Parse(message);

                // Check for the existence of required fields in the JSON
                if (!json.ContainsKey("userId") || !json.ContainsKey("activity"))
                {
                    return (null, null, "Invalid message format: Required fields are missing.");
                }

                var activity = new UserActivity
                {
                    Date = json["date"]?.ToObject<DateTime>() ?? DateTime.UtcNow, // Default to current time if date is not provided
                    Activity = json["activity"].ToString(),
                    ObjectId = json["objectId"]?.ToObject<int>() ?? 0 // Default to 0 if objectId is not provided
                };
                var userId = json["userId"].ToString();

                return (userId, activity, null); // No error
            }
            catch (JsonException ex)
            {
                // Log the exception details if necessary
                return (null, null, "JSON parsing error: " + ex.Message);
            }
            catch (Exception ex)
            {
                // Handle other unexpected errors
                return (null, null, "Unknown error: " + ex.Message);
            }
        }

        private void ProcessReceivedMessage(string message, Action<string> onMessage, CancellationToken cancellationToken)
        {
            var (userId, activity, errorMessage) = TranslateToUserActivity(message);

            if (!string.IsNullOrEmpty(errorMessage))
            {
                // Log the error message or handle it as needed
                Console.WriteLine($"Error processing message: {errorMessage}");
            }
            else if (activity != null)
            {
                _mongoDbService.AddOrUpdateUserProfile(userId, activity);
            }

            // Process messages in parallel
            Task.Run(() => onMessage(message), cancellationToken);
        }
    }
}
