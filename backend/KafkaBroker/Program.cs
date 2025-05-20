using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace KafkaPerformanceTester
{
    class Program
    {
        private static long _messagesSent = 0;
        private static long _messagesReceived = 0;
        private static readonly Stopwatch _stopwatch = new Stopwatch();
        private static readonly List<long> _latencies = new List<long>();

        static async Task Main(string[] args)
        {
            Console.WriteLine("Kafka Performance Tester");
            Console.WriteLine("------------------------");

            // Параметры конфигурации
            Console.Write("Введите адрес брокера Kafka (по умолчанию localhost:9092): ");
            var bootstrapServers = Console.ReadLine();
            if (string.IsNullOrEmpty(bootstrapServers))
                bootstrapServers = "localhost:9092";

            Console.Write("Введите имя топика (по умолчанию performance-test): ");
            var topic = Console.ReadLine();
            if (string.IsNullOrEmpty(topic))
                topic = "performance-test";

            Console.Write("Введите размер сообщения в байтах (по умолчанию 100): ");
            if (!int.TryParse(Console.ReadLine(), out var messageSize) || messageSize <= 0)
                messageSize = 100;

            Console.Write("Введите количество сообщений в секунду (по умолчанию 100): ");
            if (!int.TryParse(Console.ReadLine(), out var messagesPerSecond) || messagesPerSecond <= 0)
                messagesPerSecond = 100;

            Console.Write("Введите продолжительность теста в секундах (по умолчанию 10): ");
            if (!int.TryParse(Console.ReadLine(), out var durationSeconds) || durationSeconds <= 0)
                durationSeconds = 10;

            // Создаем топик (если не существует)
            await CreateTopicIfNotExists(bootstrapServers, topic);

            // Запускаем потребителя в фоновом режиме
            var cts = new CancellationTokenSource();
            var consumerTask = Task.Run(() => StartConsumer(bootstrapServers, topic, cts.Token));

            // Запускаем производителя
            await StartProducer(bootstrapServers, topic, messageSize, messagesPerSecond, durationSeconds);

            // Даем потребителю время завершить обработку
            await Task.Delay(1000);
            cts.Cancel();
            await consumerTask;

            // Выводим результаты
            PrintResults(durationSeconds);
        }

        static async Task CreateTopicIfNotExists(string bootstrapServers, string topic)
        {
            try
            {
                using var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();
                var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
                if (metadata.Topics.Exists(t => t.Topic == topic))
                {
                    Console.WriteLine($"Топик '{topic}' уже существует.");
                    return;
                }

                await adminClient.CreateTopicsAsync(new List<TopicSpecification>
                {
                    new TopicSpecification
                    {
                        Name = topic,
                        NumPartitions = 3,
                        ReplicationFactor = 1
                    }
                });
                Console.WriteLine($"Топик '{topic}' создан с 3 партициями.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при создании топика: {ex.Message}");
            }
        }

        static async Task StartProducer(string bootstrapServers, string topic, int messageSize, int messagesPerSecond, int durationSeconds)
        {
            var config = new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                Acks = Acks.Leader,
                CompressionType = CompressionType.Snappy,
                StatisticsIntervalMs = 1000,
                ClientId = "kafka-performance-producer"
            };

            using var producer = new ProducerBuilder<Null, string>(config)
                .SetErrorHandler((_, e) => Console.WriteLine($"Ошибка Producer: {e.Reason}"))
                .SetStatisticsHandler((_, json) => Console.WriteLine($"Статистика Producer: {json}"))
                .Build();

            var message = GenerateMessage(messageSize);
            var delayBetweenMessages = 1000.0 / messagesPerSecond;
            var startTime = DateTime.UtcNow;

            _stopwatch.Start();

            Console.WriteLine($"Начало отправки {messagesPerSecond} сообщений/сек, размером {messageSize} байт...");

            while ((DateTime.UtcNow - startTime).TotalSeconds < durationSeconds)
            {
                var sendTime = DateTime.UtcNow.Ticks;
                var deliveryReport = await producer.ProduceAsync(topic, new Message<Null, string>
                {
                    Value = $"{sendTime}|{message}"
                });

                Interlocked.Increment(ref _messagesSent);

                if (deliveryReport.Status != PersistenceStatus.Persisted)
                {
                    Console.WriteLine($"Ошибка доставки сообщения: {deliveryReport.Status}");
                }

                var elapsedMs = (DateTime.UtcNow.Ticks - sendTime) / TimeSpan.TicksPerMillisecond;
                if (elapsedMs > delayBetweenMessages)
                {
                    Console.WriteLine($"Предупреждение: задержка отправки {elapsedMs} мс");
                }

                var waitTime = (int)(delayBetweenMessages - elapsedMs);
                if (waitTime > 0)
                {
                    await Task.Delay((int)waitTime);
                }
            }

            producer.Flush(TimeSpan.FromSeconds(5));
            _stopwatch.Stop();
            Console.WriteLine("Отправка сообщений завершена.");
        }

        static void StartConsumer(string bootstrapServers, string topic, CancellationToken cancellationToken)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = "kafka-performance-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
                StatisticsIntervalMs = 1000,
                ClientId = "kafka-performance-consumer"
            };

            using var consumer = new ConsumerBuilder<Ignore, string>(config)
                .SetErrorHandler((_, e) => Console.WriteLine($"Ошибка Consumer: {e.Reason}"))
                .SetStatisticsHandler((_, json) => Console.WriteLine($"Статистика Consumer: {json}"))
                .Build();

            consumer.Subscribe(topic);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var consumeResult = consumer.Consume(cancellationToken);
                    if (consumeResult == null) continue;

                    var parts = consumeResult.Message.Value.Split('|');
                    if (parts.Length == 2 && long.TryParse(parts[0], out var sendTime))
                    {
                        var latency = (DateTime.UtcNow.Ticks - sendTime) / TimeSpan.TicksPerMillisecond;
                        lock (_latencies)
                        {
                            _latencies.Add(latency);
                        }
                        Interlocked.Increment(ref _messagesReceived);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Игнорируем, так как это ожидаемо при отмене
            }
            finally
            {
                consumer.Close();
            }
        }

        static string GenerateMessage(int size)
        {
            var sb = new StringBuilder(size);
            var random = new Random();
            for (int i = 0; i < size; i++)
            {
                sb.Append((char)('a' + random.Next(0, 26)));
            }
            return sb.ToString();
        }

        static void PrintResults(int durationSeconds)
        {
            Console.WriteLine("\nРезультаты теста:");
            Console.WriteLine("-----------------");
            Console.WriteLine($"Всего отправлено сообщений: {_messagesSent}");
            Console.WriteLine($"Всего получено сообщений: {_messagesReceived}");
            Console.WriteLine($"Пропускная способность: {_messagesReceived / durationSeconds} сообщений/сек");

            if (_latencies.Count > 0)
            {
                _latencies.Sort();
                Console.WriteLine($"Минимальная задержка: {_latencies[0]} мс");
                Console.WriteLine($"Медианная задержка: {_latencies[_latencies.Count / 2]} мс");
                Console.WriteLine($"Средняя задержка: {CalculateAverage(_latencies):F2} мс");
                Console.WriteLine($"Максимальная задержка: {_latencies[^1]} мс");
                Console.WriteLine($"95-й перцентиль задержки: {_latencies[(int)(_latencies.Count * 0.95)]} мс");
                Console.WriteLine($"99-й перцентиль задержки: {_latencies[(int)(_latencies.Count * 0.99)]} мс");
            }
        }

        static double CalculateAverage(List<long> values)
        {
            long sum = 0;
            foreach (var value in values)
            {
                sum += value;
            }
            return (double)sum / values.Count;
        }
    }
}