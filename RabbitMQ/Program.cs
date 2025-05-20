// using RabbitMQ.Client;
// using RabbitMQ.Client.Events;
// using System;
// using System.Diagnostics;
// using System.Text;
// using System.Threading;


// namespace RabbitMQBenchmark
// {
//     class Program
//     {
//         private static IConnection connection;
//         private static IModel channel;
//         private static string queueName = "benchmark_queue";
//         private static long messagesSent = 0;
//         private static long messagesReceived = 0;
//         private static long totalLatency = 0;
//         private static Stopwatch producerStopwatch = new Stopwatch();
//         private static Stopwatch consumerStopwatch = new Stopwatch();

//         static void Main(string[] args)
//         {
//             Console.WriteLine("RabbitMQ Benchmark Tool");
//             Console.WriteLine("----------------------");

//             // Параметры тестирования
//             Console.Write("Размер сообщения (байт): ");
//             int messageSize = int.Parse(Console.ReadLine());

//             Console.Write("Количество сообщений в секунду: ");
//             int messagesPerSecond = int.Parse(Console.ReadLine());

//             Console.Write("Продолжительность теста (сек): ");
//             int testDuration = int.Parse(Console.ReadLine());

//             // Инициализация RabbitMQ
//             var factory = new ConnectionFactory()
//             {
//                 HostName = "127.0.0.1",
//                 Port = 5672,
//                 UserName = "guest",
//                 Password = "guest"
//             };

//             using (var connection = factory.CreateConnection())
//             using (var channel = connection.CreateModel())
//             {
//                 // Создаем очередь перед использованием
//                 channel.QueueDeclare(queue: "benchmark_queue",
//                                 durable: false,
//                                 exclusive: false,
//                                 autoDelete: false,
//                                 arguments: null);

//                 // Запуск потребителя
//                 var consumer = new EventingBasicConsumer(channel);
//                 consumer.Received += (model, ea) =>
//                 {
//                     var timestamp = (long)ea.BasicProperties.Headers["Timestamp"];
//                     var latency = (DateTime.UtcNow.Ticks - timestamp) / TimeSpan.TicksPerMillisecond;
//                     Interlocked.Add(ref totalLatency, latency);
//                     Interlocked.Increment(ref messagesReceived);
//                 };

//                 channel.BasicConsume(queue: "benchmark_queue",
//                                     autoAck: true,
//                                     consumer: consumer);

//                 // Запуск производителя
//                 StartProducer(channel, messageSize, messagesPerSecond, testDuration);

//                 // Мониторинг статистики
//                 MonitorStatistics(testDuration);
//             }
//         }

//         private static void InitializeRabbitMQ()
//         {
//             var factory = new ConnectionFactory()
//             {
//                 HostName = "localhost",
//                 Port = 5672,
//                 UserName = "admin",
//                 Password = "Admin123!",
//                 VirtualHost = "/",
//                 RequestedHeartbeat = TimeSpan.FromSeconds(30),
//                 AutomaticRecoveryEnabled = true,
//                 // Важные параметры для новой версии RabbitMQ
//                 // AuthMechanisms = new List<AuthMechanismFactory>() { new PlainMechanismFactory() },
//                 // DispatchConsumersAsync = true
//             };

//             try
//             {
//                 using var connection = factory.CreateConnection();
//                 using var channel = connection.CreateModel();
                
//                 // Объявляем очередь
//                 channel.QueueDeclare(
//                     queue: "test_queue",
//                     durable: false,
//                     exclusive: false,
//                     autoDelete: false,
//                     arguments: null);
                
//                 Console.WriteLine("Успешное подключение к RabbitMQ!");
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"Ошибка подключения: {ex.Message}");
//             }
//         }

//         private static void StartProducer(IModel channel, int messageSize, int messagesPerSecond, int testDuration)
//         {
//             var message = GenerateMessage(messageSize);
//             var delayBetweenMessages = 1000 / messagesPerSecond;

//             producerStopwatch.Start();

//             for (int i = 0; i < messagesPerSecond * testDuration; i++)
//             {
//                 var timestamp = DateTime.UtcNow.Ticks;
//                 var properties = channel.CreateBasicProperties();
//                 properties.Headers = new Dictionary<string, object>
//                 {
//                     { "Timestamp", timestamp }
//                 };

//                 channel.BasicPublish(exchange: "",
//                                     routingKey: "benchmark_queue",
//                                     basicProperties: properties,
//                                     body: Encoding.UTF8.GetBytes(message));

//                 Interlocked.Increment(ref messagesSent);

//                 Thread.Sleep(delayBetweenMessages);
//             }

//             producerStopwatch.Stop();
//         }

//         private static void StartConsumer()
//         {
//             var consumer = new EventingBasicConsumer(channel);
//             consumer.Received += (model, ea) =>
//             {
//                 var timestamp = (long)ea.BasicProperties.Headers["Timestamp"];
//                 var latency = (DateTime.UtcNow.Ticks - timestamp) / TimeSpan.TicksPerMillisecond;
//                 Interlocked.Add(ref totalLatency, latency);
//                 Interlocked.Increment(ref messagesReceived);
//             };

//             channel.BasicConsume(queue: queueName,
//                                  autoAck: true,
//                                  consumer: consumer);

//             consumerStopwatch.Start();
//         }

//         private static void MonitorStatistics(int testDuration)
//         {
//             Console.WriteLine("\nСбор статистики...");

//             long lastReceived = 0;
//             long lastSent = 0;
//             var startTime = DateTime.Now;

//             while ((DateTime.Now - startTime).TotalSeconds < testDuration + 2) // +2 секунды для завершения обработки
//             {
//                 Thread.Sleep(1000);

//                 var currentReceived = Interlocked.Read(ref messagesReceived);
//                 var currentSent = Interlocked.Read(ref messagesSent);
//                 var receivedPerSecond = currentReceived - lastReceived;
//                 var sentPerSecond = currentSent - lastSent;

//                 Console.WriteLine($"Отправлено: {sentPerSecond} msg/s | Получено: {receivedPerSecond} msg/s");

//                 lastReceived = currentReceived;
//                 lastSent = currentSent;
//             }

//             consumerStopwatch.Stop();

//             // Вывод итоговой статистики
//             Console.WriteLine("\nИтоговая статистика:");
//             Console.WriteLine($"Всего отправлено сообщений: {messagesSent}");
//             Console.WriteLine($"Всего получено сообщений: {messagesReceived}");
//             Console.WriteLine($"Средняя задержка: {totalLatency / (messagesReceived > 0 ? messagesReceived : 1)} мс");
//             Console.WriteLine($"Производительность отправки: {messagesSent / (producerStopwatch.Elapsed.TotalSeconds > 0 ? producerStopwatch.Elapsed.TotalSeconds : 1):0.00} msg/s");
//             Console.WriteLine($"Производительность обработки: {messagesReceived / (consumerStopwatch.Elapsed.TotalSeconds > 0 ? consumerStopwatch.Elapsed.TotalSeconds : 1):0.00} msg/s");
//         }

//         private static string GenerateMessage(int size)
//         {
//             var sb = new StringBuilder(size);
//             for (int i = 0; i < size; i++)
//             {
//                 sb.Append('x');
//             }
//             return sb.ToString();
//         }
//     }
// }


// using RabbitMQ.Client;
// using RabbitMQ.Client.Events;
// using System;
// using System.Text;
// using System.Threading;
// using System.Threading.Tasks;

// namespace RabbitMQBenchmark
// {
//     class Program
//     {
//         private static IConnection _connection;
//         private static IModel _producerChannel;
//         private static IModel _consumerChannel;
        
//         private static long _messagesSent = 0;
//         private static long _messagesReceived = 0;
//         private static long _totalLatency = 0;

//         private const string QueueName = "benchmark_queue";

//         static async Task Main(string[] args)
//         {
//             Console.WriteLine("RabbitMQ Benchmark Tool");
            
//             var factory = new ConnectionFactory()
//             {
//                 HostName = "localhost",
//                 Port = 5672,
//                 UserName = "admin",
//                 Password = "Admin123!",
//                 VirtualHost = "/",
//                 AutomaticRecoveryEnabled = true,
//                 DispatchConsumersAsync = true  // Включен асинхронный режим
//             };

//             try
//             {
//                 _connection = factory.CreateConnection();
//                 _producerChannel = _connection.CreateModel();
//                 _consumerChannel = _connection.CreateModel();

//                 // Объявляем очередь
//                 _producerChannel.QueueDeclare(
//                     queue: QueueName,
//                     durable: false,
//                     exclusive: false,
//                     autoDelete: false,
//                     arguments: null);

//                 // Запускаем асинхронного потребителя
//                 var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
//                 consumer.Received += OnMessageReceived;

//                 _consumerChannel.BasicConsume(
//                     queue: QueueName,
//                     autoAck: true,
//                     consumer: consumer);

//                 Console.WriteLine("✅ Потребитель (Consumer) запущен в асинхронном режиме.");

//                 // Запускаем производителя
//                 StartProducer(messageSize: 1024, messagesPerSecond: 10, testDuration: 5);

//                 // Мониторим статистику
//                 await MonitorStatsAsync(testDuration: 5);
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"❌ Ошибка: {ex.Message}");
//             }
//             finally
//             {
//                 _producerChannel?.Close();
//                 _consumerChannel?.Close();
//                 _connection?.Close();
//             }
//         }

//         // Асинхронный обработчик сообщений
//         private static async Task OnMessageReceived(object sender, BasicDeliverEventArgs ea)
//         {
//             try
//             {
//                 var body = ea.Body.ToArray();
//                 var message = Encoding.UTF8.GetString(body);
//                 Interlocked.Increment(ref _messagesReceived);
//                 Console.WriteLine($"Получено: {message}");

//                 await Task.Yield(); // Освобождаем поток для асинхронной обработки
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"Ошибка обработки сообщения: {ex.Message}");
//             }
//         }

//         private static void StartProducer(int messageSize, int messagesPerSecond, int testDuration)
//         {
//             var message = new string('x', messageSize);
//             var delay = 1000 / messagesPerSecond;

//             for (int i = 0; i < messagesPerSecond * testDuration; i++)
//             {
//                 var body = Encoding.UTF8.GetBytes($"Сообщение {i}: {message}");
//                 _producerChannel.BasicPublish(
//                     exchange: "",
//                     routingKey: QueueName,
//                     basicProperties: null,
//                     body: body);
                
//                 Interlocked.Increment(ref _messagesSent);
//                 Thread.Sleep(delay);
//             }
//         }

//         private static async Task MonitorStatsAsync(int testDuration)
//         {
//             Console.WriteLine("\n📊 Мониторинг статистики...");
//             var startTime = DateTime.Now;

//             while ((DateTime.Now - startTime).TotalSeconds < testDuration)
//             {
//                 Console.WriteLine($"Отправлено: {_messagesSent} | Получено: {_messagesReceived}");
//                 await Task.Delay(1000);
//             }
//         }
//     }
// }


using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQBenchmark
{
    class Program
    {
        private static IConnection _connection;
        private static IModel _producerChannel;
        private static IModel _consumerChannel;
        
        private static long _messagesSent = 0;
        private static long _messagesReceived = 0;
        private static long _totalLatencyMs = 0;
        private static Stopwatch _testTimer = new Stopwatch();

        private const string QueueName = "benchmark_queue";

        private static void SetupRabbitMQQueue(IModel channel)
        {
            try
            {
                // 1. Попытка удалить существующую очередь
                try 
                {
                    channel.QueueDelete(QueueName, ifUnused: false, ifEmpty: false);
                    Console.WriteLine($"🗑 Очередь {QueueName} удалена перед повторным созданием");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ℹ Не удалось удалить очередь: {ex.Message}");
                }

                // 2. Объявляем очередь с нужными параметрами
                channel.QueueDeclare(
                    queue: QueueName,
                    durable: true,  // Теперь всегда true для надежности
                    exclusive: false,
                    autoDelete: false,
                    arguments: new Dictionary<string, object>
                    {
                        { "x-queue-type", "quorum" },
                        { "x-max-length", 10000 }
                    });
            }
            catch (RabbitMQ.Client.Exceptions.OperationInterruptedException ex) when (ex.Message.Contains("PRECONDITION_FAILED"))
            {
                // 3. Если очередь существует с другими параметрами
                Console.WriteLine("⚠ Обнаружена очередь с другими параметрами. Принудительная очистка...");
                
                // Принудительное удаление
                channel.QueueDelete(QueueName, ifUnused: false, ifEmpty: false);
                
                // Повторное создание
                channel.QueueDeclare(
                    queue: QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: new Dictionary<string, object>
                    {
                        { "x-queue-type", "quorum" },
                        { "x-max-length", 10000 }
                    });
            }
        }

        static async Task Main(string[] args)
        {
            Console.WriteLine("🐇 RabbitMQ Benchmark Tool");
            Console.WriteLine("-------------------------");

            // 1. Запрос параметров теста у пользователя
            var (messageSize, messagesPerSecond, testDuration) = GetTestParameters();

            // 2. Настройка подключения
            var factory = new ConnectionFactory()
            {
                HostName = "localhost",
                UserName = "admin",
                Password = "Admin123!",
                AutomaticRecoveryEnabled = true,
                DispatchConsumersAsync = true
            };

            try
            {
                _connection = factory.CreateConnection();
                _producerChannel = _connection.CreateModel();
                _consumerChannel = _connection.CreateModel();

                SetupRabbitMQQueue(_producerChannel);  // Используем новый метод
                Console.WriteLine("✅ Очередь успешно настроена");

                // 3. Настройка очереди с балансировкой
                _producerChannel.QueueDeclare(
                    queue: QueueName,
                    durable: true,              // Для устойчивости
                    exclusive: false,
                    autoDelete: false,
                    arguments: new Dictionary<string, object>
                    {
                        { "x-queue-type", "quorum" }, // Для балансировки
                        { "x-max-length", 10000 }     // Лимит очереди
                    });

                // 4. Запуск потребителя
                var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
                consumer.Received += async (model, ea) =>
                {
                    var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(
                        (long)ea.BasicProperties.Headers["Timestamp"]);
                    var latency = (DateTime.Now - timestamp).TotalMilliseconds;
                    
                    Interlocked.Increment(ref _messagesReceived);
                    Interlocked.Add(ref _totalLatencyMs, (long)latency);
                    
                    await Task.Yield();
                };

                // _consumerChannel.BasicQos(prefetchSize: 0, prefetchCount: 100, global: false); // Балансировка
                _consumerChannel.BasicQos(prefetchSize: 0, prefetchCount: 500, global: false);

                // Используйте несколько потребителей
                _consumerChannel.BasicConsume(QueueName, autoAck: true, consumer: consumer);

                // 5. Запуск производителя
                Console.WriteLine("\n🚀 Запуск теста...");
                _testTimer.Start();
                var producerTask = StartProducer(messageSize, messagesPerSecond, testDuration);
                var monitorTask = MonitorStatsAsync(testDuration);

                await Task.WhenAll(producerTask, monitorTask);
            }
            finally
            {
                _producerChannel?.Close();
                _consumerChannel?.Close();
                _connection?.Close();
            }
        }

        // Получение параметров теста
        static (int size, int rate, int duration) GetTestParameters()
        {
            Console.Write("📏 Размер сообщения (байт): ");
            int size = int.Parse(Console.ReadLine());

            Console.Write("🌀 Сообщений в секунду: ");
            int rate = int.Parse(Console.ReadLine());

            Console.Write("⏱ Длительность теста (сек): ");
            int duration = int.Parse(Console.ReadLine());

            return (size, rate, duration);
        }

        // Производитель сообщений
        static async Task StartProducer(int messageSize, int messagesPerSecond, int testDuration)
        {
            var message = GenerateMessage(messageSize);
            var delayMs = 1000 / messagesPerSecond;
            var endTime = DateTime.Now.AddSeconds(testDuration);

            while (DateTime.Now < endTime)
            {
                var properties = _producerChannel.CreateBasicProperties();
                properties.Headers = new Dictionary<string, object>
                {
                    { "Timestamp", DateTimeOffset.Now.ToUnixTimeMilliseconds() }
                };

                _producerChannel.BasicPublish(
                    exchange: "",
                    routingKey: QueueName,
                    basicProperties: properties,
                    body: Encoding.UTF8.GetBytes(message));

                Interlocked.Increment(ref _messagesSent);
                await Task.Delay(delayMs);
            }
        }

        // Мониторинг статистики
        static async Task MonitorStatsAsync(int testDuration)
        {
            var startTime = DateTime.Now;
            var lastUpdate = DateTime.Now;
            long lastCount = 0;

            while ((DateTime.Now - startTime).TotalSeconds < testDuration)
            {
                await Task.Delay(1000);
                
                var currentReceived = _messagesReceived;
                var currentRate = currentReceived - lastCount;
                lastCount = currentReceived;

                var avgLatency = _messagesReceived > 0 
                    ? _totalLatencyMs / _messagesReceived 
                    : 0;

                Console.WriteLine(
                    $"[{DateTime.Now:T}] " +
                    $"Отправлено: {_messagesSent} | " +
                    $"Получено: {currentReceived} ({currentRate}/сек) | " +
                    $"Задержка: {avgLatency} мс");
            }

            _testTimer.Stop();
            PrintFinalReport(testDuration);
        }

        // Генерация тестового сообщения
        static string GenerateMessage(int size) 
            => new string('x', size);

        // Финальный отчет
        static void PrintFinalReport(int duration)
        {
            Console.WriteLine("\n📊 ФИНАЛЬНЫЙ ОТЧЕТ");
            Console.WriteLine("------------------");
            Console.WriteLine($"Общее время: {duration} сек");
            Console.WriteLine($"Отправлено сообщений: {_messagesSent}");
            Console.WriteLine($"Получено сообщений: {_messagesReceived}");
            Console.WriteLine($"Пропускная способность: {_messagesReceived / duration} msg/sec");
            Console.WriteLine($"Средняя задержка: {_totalLatencyMs / (_messagesReceived > 0 ? _messagesReceived : 1)} мс");
            
            // Пример измерения потребления CPU/RAM (требует NuGet System.Diagnostics.Process)
            var process = Process.GetCurrentProcess();
            Console.WriteLine($"\n💻 Потребление ресурсов:");
            Console.WriteLine($"CPU: {process.TotalProcessorTime.TotalMilliseconds / duration} мс/сек");
            Console.WriteLine($"RAM: {process.WorkingSet64 / 1024 / 1024} МБ");
        }
    }
}