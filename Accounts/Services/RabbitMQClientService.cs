using Accounts.Interfaces;
using Accounts.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace Accounts.Services
{
    public class RabbitMQClientService : IMessagePublisher
    {
        private readonly ConnectionFactory _connectionFactory;
        private readonly MongoDbService _mongoDbService;

        public RabbitMQClientService(IConfiguration configuration, MongoDbService mongoDbService)
        {
            _connectionFactory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:HostName"],
                Port = Convert.ToInt32(configuration["RabbitMQ:Port"]),
                UserName = configuration["RabbitMQ:UserName"],
                Password = configuration["RabbitMQ:Password"],
                Ssl = new SslOption
                {
                    ServerName = configuration["RabbitMQ:HostName"],
                    Enabled = true
                },
                Uri = new Uri($"amqps://{configuration["RabbitMQ:HostName"]}:{Convert.ToInt32(configuration["RabbitMQ:Port"])}")
        };
            _mongoDbService = mongoDbService;
        }

        public void Publish(string queue, string message)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                using var channel = connection.CreateModel();

                DeclareQueue(channel, queue);

                var body = Encoding.UTF8.GetBytes(message);
                channel.BasicPublish(exchange: "", routingKey: queue, basicProperties: null, body: body);

                Console.WriteLine($"[x] Sent '{message}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in publishing message: {ex.Message}");
            }
        }

        public void Consume(string queue, CancellationToken cancellationToken)
        {
            try
            {
                var connection = _connectionFactory.CreateConnection();
                var channel = connection.CreateModel();

                DeclareQueue(channel, queue);

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    Console.WriteLine($"Received: '{message}'");

                    // Process the received message here instead of using onMessage action
                    ProcessReceivedMessage(message);

                    // Acknowledge the message
                    channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                };

                channel.BasicConsume(queue: queue, autoAck: false, consumer: consumer);

                cancellationToken.WaitHandle.WaitOne();

                channel.Close();
                connection.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in consuming message: {ex}");
            }
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
                if (!json.ContainsKey("userId") || !json.ContainsKey("actionType"))
                {
                    return (null, null, "Invalid message format: Required fields are missing.");
                }

                var activity = new UserActivity
                {
                    Date = json["date"]?.ToObject<DateTime>() ?? DateTime.UtcNow, // Default to current time if date is not provided
                    Activity = json["actionType"].ToString(),
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

        private void ProcessReceivedMessage(string message)
        {
            try
            {
                var (userId, activity, errorMessage) = TranslateToUserActivity(message);

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    Console.WriteLine($"Error processing message: {errorMessage}");
                    return;
                }

                if (activity != null)
                {
                    _mongoDbService.AddOrUpdateUserProfile(userId, activity);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in processing received message: {ex.Message}");
            }
        }
    }
}
