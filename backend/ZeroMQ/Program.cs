using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

class ZeroMQTester
{
    private static long totalMessagesSent = 0;
    private static long totalMessagesReceived = 0;
    private static long totalBytesSent = 0;
    private static long totalBytesReceived = 0;
    private static Stopwatch testDuration = new Stopwatch();
    private static object lockObj = new object();

    static void Main(string[] args)
    {
        Console.WriteLine("ZeroMQ Performance Tester");
        Console.WriteLine("------------------------");

        while (true)
        {
            Console.WriteLine("\nВыберите режим:");
            Console.WriteLine("1. Publisher (Отправитель)");
            Console.WriteLine("2. Subscriber (Получатель)");
            Console.WriteLine("3. Тест производительности (Pub-Sub)");
            Console.WriteLine("4. Выход");
            Console.Write("Ваш выбор: ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    RunPublisher();
                    break;
                case "2":
                    RunSubscriber();
                    break;
                case "3":
                    RunPerformanceTest();
                    break;
                case "4":
                    return;
                default:
                    Console.WriteLine("Неверный выбор. Попробуйте снова.");
                    break;
            }
        }
    }

    static void RunPublisher()
    {
        Console.Write("\nВведите размер сообщения в байтах: ");
        int messageSize = int.Parse(Console.ReadLine());

        Console.Write("Введите количество сообщений в секунду: ");
        int messagesPerSecond = int.Parse(Console.ReadLine());

        Console.Write("Введите адрес для публикации (например tcp://*:5556): ");
        string address = Console.ReadLine();

        using (var publisher = new PublisherSocket())
        {
            publisher.Bind(address);
            Console.WriteLine($"Publisher начал работу на {address}");

            var delay = messagesPerSecond > 0 ? 1000 / messagesPerSecond : 0;
            var message = new byte[messageSize];
            new Random().NextBytes(message);

            var sw = Stopwatch.StartNew();
            long counter = 0;

            while (true)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                    break;

                publisher.SendFrame(message);
                counter++;

                if (sw.ElapsedMilliseconds >= 1000)
                {
                    Console.WriteLine($"Отправлено {counter} сообщений ({counter * messageSize} байт/сек)");
                    counter = 0;
                    sw.Restart();
                }

                if (delay > 0)
                    Thread.Sleep(delay);
            }
        }
    }

    static void RunSubscriber()
    {
        Console.Write("\nВведите адрес для подключения (например tcp://localhost:5556): ");
        string address = Console.ReadLine();

        Console.Write("Введите тему для подписки (оставьте пустым для всех): ");
        string topic = Console.ReadLine();

        using (var subscriber = new SubscriberSocket())
        {
            subscriber.Connect(address);
            subscriber.Subscribe(topic);
            Console.WriteLine($"Subscriber подключен к {address}, тема: {(string.IsNullOrEmpty(topic) ? "все" : topic)}");

            var sw = Stopwatch.StartNew();
            long counter = 0;
            long totalBytes = 0;

            while (true)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                    break;

                var message = subscriber.ReceiveFrameBytes();
                counter++;
                totalBytes += message.Length;

                if (sw.ElapsedMilliseconds >= 1000)
                {
                    Console.WriteLine($"Получено {counter} сообщений ({totalBytes} байт/сек)");
                    counter = 0;
                    totalBytes = 0;
                    sw.Restart();
                }
            }
        }
    }

    static void RunPerformanceTest()
    {
        Console.Write("\nВведите размер сообщения в байтах: ");
        int messageSize = int.Parse(Console.ReadLine());

        Console.Write("Введите количество сообщений в секунду: ");
        int messagesPerSecond = int.Parse(Console.ReadLine());

        Console.Write("Введите продолжительность теста в секундах: ");
        int testDurationSeconds = int.Parse(Console.ReadLine());

        string address = "tcp://127.0.0.1:5556";
        var message = new byte[messageSize];
        new Random().NextBytes(message);

        // Сброс счетчиков
        totalMessagesSent = 0;
        totalMessagesReceived = 0;
        totalBytesSent = 0;
        totalBytesReceived = 0;
        testDuration.Reset();

        // Получаем текущий процесс для мониторинга ресурсов
        var currentProcess = Process.GetCurrentProcess();

        Console.WriteLine("\nЗапуск теста...");
        Console.WriteLine("Время | CPU % | RAM (MB) | Отправлено | Получено | Отправлено байт/сек | Получено байт/сек");

        // Запуск подписчика в отдельном потоке
        var subscriberTask = Task.Run(() =>
        {
            using (var subscriber = new SubscriberSocket())
            {
                subscriber.Connect(address);
                subscriber.Subscribe("");

                while (testDuration.IsRunning || testDuration.Elapsed.TotalSeconds < testDurationSeconds)
                {
                    var msg = subscriber.ReceiveFrameBytes();
                    Interlocked.Increment(ref totalMessagesReceived);
                    Interlocked.Add(ref totalBytesReceived, msg.Length);
                }
            }
        });

        // Запуск издателя в отдельном потоке
        var publisherTask = Task.Run(() =>
        {
            using (var publisher = new PublisherSocket())
            {
                publisher.Bind(address);

                var delay = messagesPerSecond > 0 ? 1000 / messagesPerSecond : 0;
                if (delay < 0) delay = 0;

                testDuration.Start();
                var startTime = DateTime.Now;

                while ((DateTime.Now - startTime).TotalSeconds < testDurationSeconds)
                {
                    publisher.SendFrame(message);
                    Interlocked.Increment(ref totalMessagesSent);
                    Interlocked.Add(ref totalBytesSent, messageSize);

                    if (delay > 0)
                        Thread.Sleep(delay);
                }

                testDuration.Stop();
            }
        });

        // Мониторинг производительности
        var monitorTask = Task.Run(() =>
        {
            var startTime = DateTime.Now;
            var lastUpdate = DateTime.Now;
            long lastSent = 0;
            long lastReceived = 0;
            long lastBytesSent = 0;
            long lastBytesReceived = 0;
            var lastCpuTime = currentProcess.TotalProcessorTime;

            while ((DateTime.Now - startTime).TotalSeconds < testDurationSeconds + 1) // +1 секунда для финального отчета
            {
                if ((DateTime.Now - lastUpdate).TotalSeconds >= 1)
                {
                    var currentSent = totalMessagesSent;
                    var currentReceived = totalMessagesReceived;
                    var currentBytesSent = totalBytesSent;
                    var currentBytesReceived = totalBytesReceived;

                    var sentPerSec = currentSent - lastSent;
                    var receivedPerSec = currentReceived - lastReceived;
                    var bytesSentPerSec = currentBytesSent - lastBytesSent;
                    var bytesReceivedPerSec = currentBytesReceived - lastBytesReceived;

                    // Расчет использования CPU
                    var newCpuTime = currentProcess.TotalProcessorTime;
                    var cpuUsage = (newCpuTime - lastCpuTime).TotalMilliseconds / 
                                  Environment.ProcessorCount / 1000 * 100;
                    lastCpuTime = newCpuTime;

                    // Получение используемой памяти
                    var ramUsage = currentProcess.WorkingSet64 / 1024 / 1024;

                    Console.WriteLine($"{DateTime.Now - startTime:mm\\:ss} | {cpuUsage:F1}% | {ramUsage:F1} | " +
                                    $"{sentPerSec} | {receivedPerSec} | {bytesSentPerSec} | {bytesReceivedPerSec}");

                    lastUpdate = DateTime.Now;
                    lastSent = currentSent;
                    lastReceived = currentReceived;
                    lastBytesSent = currentBytesSent;
                    lastBytesReceived = currentBytesReceived;
                }

                Thread.Sleep(100);
            }
        });

        Task.WaitAll(publisherTask, subscriberTask, monitorTask);

        // Вывод итоговых результатов
        Console.WriteLine("\nТест завершен. Итоговые результаты:");
        Console.WriteLine($"Общее время теста: {testDuration.Elapsed.TotalSeconds:F2} сек");
        Console.WriteLine($"Всего отправлено сообщений: {totalMessagesSent}");
        Console.WriteLine($"Всего получено сообщений: {totalMessagesReceived}");
        Console.WriteLine($"Всего отправлено байт: {totalBytesSent}");
        Console.WriteLine($"Всего получено байт: {totalBytesReceived}");
        Console.WriteLine($"Средняя скорость отправки: {totalMessagesSent / Math.Max(testDuration.Elapsed.TotalSeconds, 0.1):F2} сообщ/сек");
        Console.WriteLine($"Средняя скорость получения: {totalMessagesReceived / Math.Max(testDuration.Elapsed.TotalSeconds, 0.1):F2} сообщ/сек");
        Console.WriteLine($"Средняя пропускная способность отправки: {totalBytesSent / Math.Max(testDuration.Elapsed.TotalSeconds, 0.1) / 1024:F2} KB/сек");
        Console.WriteLine($"Средняя пропускная способность получения: {totalBytesReceived / Math.Max(testDuration.Elapsed.TotalSeconds, 0.1) / 1024:F2} KB/sec");
    }
}