using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace NanoMQBenchmark
{
    class Program
    {
        private const string Topic = "benchmark/topic";
        private static int _sentMessages = 0;
        private static int _receivedMessages = 0;

        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("NanoMQ Benchmark Tool");
                Console.WriteLine("======================");
                
                var messageSize = GetInput("Введите размер сообщения в байтах: ", 10);
                var messagesPerSecond = GetInput("Введите количество сообщений в секунду: ", 10);
                var testDuration = GetInput("Введите продолжительность теста в секундах: ", 5);
                var brokerAddress = GetInput("Введите адрес брокера NanoMQ (по умолчанию localhost): ", "localhost");

                await RunBenchmark(brokerAddress, messageSize, messagesPerSecond, testDuration);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Критическая ошибка: {ex.Message}");
            }
        }

        static async Task RunBenchmark(string brokerAddress, int messageSize, int messagesPerSecond, int testDuration)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(testDuration));
            var factory = new MqttFactory();

            // 1. Инициализация клиентов
            using var publisher = await CreateAndConnectClient(factory, brokerAddress, "publisher");
            using var subscriber = await CreateAndConnectClient(factory, brokerAddress, "subscriber");

            if (publisher == null || subscriber == null)
            {
                Console.WriteLine("Не удалось инициализировать клиенты");
                return;
            }

            // 2. Подписка на топик
            var subscribeResult = await subscriber.SubscribeAsync(new MqttTopicFilterBuilder()
                .WithTopic(Topic)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build());

            if (subscribeResult.Items.Any(x => x.ResultCode != MqttClientSubscribeResultCode.GrantedQoS0 && 
                                             x.ResultCode != MqttClientSubscribeResultCode.GrantedQoS1 && 
                                             x.ResultCode != MqttClientSubscribeResultCode.GrantedQoS2))
            {
                Console.WriteLine("Ошибка подписки на топик");
                return;
            }

            // 3. Обработчик сообщений
            subscriber.ApplicationMessageReceivedAsync += e =>
            {
                if (e.ApplicationMessage.Topic == Topic)
                {
                    Interlocked.Increment(ref _receivedMessages);
                }
                return Task.CompletedTask;
            };

            // 4. Запуск теста
            var testTask = RunTest(publisher, messageSize, messagesPerSecond, cts.Token);
            var monitorTask = MonitorProgress(testDuration, cts.Token);

            await Task.WhenAll(testTask, monitorTask);

            // 5. Результаты
            PrintResults(testDuration, messageSize);
        }

        static async Task<IMqttClient> CreateAndConnectClient(MqttFactory factory, string brokerAddress, string clientName)
        {
            var client = factory.CreateMqttClient();
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerAddress)
                .WithClientId($"{clientName}_{Guid.NewGuid()}")
                .WithTimeout(TimeSpan.FromSeconds(5))
                .Build();

            client.ConnectedAsync += e => 
            {
                Console.WriteLine($"{clientName} подключен");
                return Task.CompletedTask;
            };

            client.DisconnectedAsync += e => 
            {
                Console.WriteLine($"{clientName} отключен: {e.Reason}");
                return Task.CompletedTask;
            };

            try
            {
                var result = await client.ConnectAsync(options);
                if (result.ResultCode != MqttClientConnectResultCode.Success)
                {
                    Console.WriteLine($"{clientName}: ошибка подключения - {result.ResultCode}");
                    return null;
                }
                return client;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{clientName}: ошибка подключения - {ex.Message}");
                return null;
            }
        }

        static async Task RunTest(IMqttClient publisher, int messageSize, int messagesPerSecond, CancellationToken ct)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(Topic)
                .WithPayload(new string('x', messageSize))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            var delay = messagesPerSecond > 0 ? (int)(1000.0 / messagesPerSecond) : 0;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await publisher.PublishAsync(message, ct);
                    Interlocked.Increment(ref _sentMessages);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка публикации: {ex.Message}");
                }

                if (delay > 0)
                {
                    try { await Task.Delay(delay, ct); }
                    catch (TaskCanceledException) { break; }
                }
            }
        }

        static async Task MonitorProgress(int duration, CancellationToken ct)
        {
            var startTime = DateTime.Now;
            var lastSent = 0;
            var lastReceived = 0;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, ct);

                    var elapsed = (DateTime.Now - startTime).TotalSeconds;
                    var remaining = duration - elapsed;

                    Console.WriteLine("\n=== Статистика ===");
                    Console.WriteLine($"Прошло: {elapsed:F1} сек | Осталось: {remaining:F1} сек");
                    Console.WriteLine($"Отправлено: {_sentMessages} (+{_sentMessages - lastSent}/сек)");
                    Console.WriteLine($"Получено: {_receivedMessages} (+{_receivedMessages - lastReceived}/сек)");
                    Console.WriteLine($"Потери: {_sentMessages - _receivedMessages} ({(double)(_sentMessages - _receivedMessages) / Math.Max(1, _sentMessages) * 100:F1}%)");

                    lastSent = _sentMessages;
                    lastReceived = _receivedMessages;
                }
                catch (TaskCanceledException) { break; }
            }
        }

        static void PrintResults(int duration, int messageSize)
        {
            Console.WriteLine("\n=== Итоговые результаты ===");
            Console.WriteLine($"Длительность теста: {duration} сек");
            Console.WriteLine($"Всего отправлено: {_sentMessages}");
            Console.WriteLine($"Всего получено: {_receivedMessages}");
            Console.WriteLine($"Потери: {_sentMessages - _receivedMessages} ({(double)(_sentMessages - _receivedMessages) / Math.Max(1, _sentMessages) * 100:F1}%)");
            
            if (_sentMessages > 0)
            {
                var throughput = (_sentMessages * messageSize) / (duration * 1024.0);
                Console.WriteLine($"Пропускная способность: {throughput:F2} KB/сек");
            }
        }

        static T GetInput<T>(string prompt, T defaultValue) where T : IConvertible
        {
            Console.Write(prompt);
            var input = Console.ReadLine();
            
            if (string.IsNullOrEmpty(input))
                return defaultValue;

            try
            {
                return (T)Convert.ChangeType(input, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}