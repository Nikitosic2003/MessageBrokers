import React, { useState } from 'react';
import { 
  ChakraProvider,
  Box,
  Flex,
  Heading,
  Select,
  Button,
  Slider,
  SliderTrack,
  SliderFilledTrack,
  SliderThumb,
  Text,
  VStack,
  HStack,
  Stat,
  StatLabel,
  StatNumber,
  StatHelpText,
  Progress,
  useToast
} from '@chakra-ui/react';
import { theme } from './theme';

function App() {
  const [broker, setBroker] = useState('zeromq');
  const [messageSize, setMessageSize] = useState(1024);
  const [messagesPerSecond, setMessagesPerSecond] = useState(100);
  const [isTesting, setIsTesting] = useState(false);
  const [results, setResults] = useState(null);
  const [progress, setProgress] = useState(0);
  const toast = useToast();

  const brokers = [
    { value: 'zeromq', label: 'ZeroMQ' },
    { value: 'rabbitmq', label: 'RabbitMQ' },
    { value: 'nanomq', label: 'NanoMQ' },
    { value: 'kafka', label: 'Kafka' },
  ];

  const handleStartTest = () => {
    setIsTesting(true);
    setProgress(0);
    setResults(null);
    
    // Симуляция прогресса тестирования
    const interval = setInterval(() => {
      setProgress(prev => {
        if (prev >= 100) {
          clearInterval(interval);
          setIsTesting(false);
          
          // Генерация тестовых результатов
          setResults({
            throughput: Math.floor(Math.random() * 10000) + 5000,
            latency: (Math.random() * 10).toFixed(2),
            cpuUsage: (Math.random() * 100).toFixed(2),
            memoryUsage: (Math.random() * 100).toFixed(2),
          });
          
          toast({
            title: 'Тестирование завершено',
            description: `Результаты теста для ${brokers.find(b => b.value === broker).label} получены`,
            status: 'success',
            duration: 5000,
            isClosable: true,
          });
          
          return 100;
        }
        return prev + 5;
      });
    }, 300);
  };

  return (
    <ChakraProvider theme={theme}>
      <Box minH="100vh" bg="gray.50" p={8}>
        <Flex direction="column" maxW="1200px" mx="auto">
          <Heading as="h1" size="xl" mb={8} textAlign="center" color="brand.500">
            Тестирование брокеров сообщений
          </Heading>
          
          <Flex direction={{ base: 'column', md: 'row' }} gap={8}>
            {/* Панель настроек */}
            <Box flex={1} bg="white" p={6} borderRadius="lg" boxShadow="md">
              <VStack spacing={6} align="stretch">
                <Box>
                  <Text fontSize="lg" fontWeight="semibold" mb={2}>
                    Выберите брокер сообщений
                  </Text>
                  <Select 
                    value={broker} 
                    onChange={(e) => setBroker(e.target.value)}
                    isDisabled={isTesting}
                  >
                    {brokers.map((b) => (
                      <option key={b.value} value={b.value}>{b.label}</option>
                    ))}
                  </Select>
                </Box>
                
                <Box>
                  <Text fontSize="lg" fontWeight="semibold" mb={2}>
                    Размер сообщения: {messageSize} байт
                  </Text>
                  <Slider 
                    value={messageSize} 
                    min={64} 
                    max={4096} 
                    step={64}
                    onChange={(val) => setMessageSize(val)}
                    isDisabled={isTesting}
                  >
                    <SliderTrack bg="gray.100">
                      <SliderFilledTrack bg="brand.500" />
                    </SliderTrack>
                    <SliderThumb boxSize={6} />
                  </Slider>
                  <HStack justify="space-between" mt={2}>
                    <Text fontSize="sm" color="gray.500">64 байт</Text>
                    <Text fontSize="sm" color="gray.500">4 КБ</Text>
                  </HStack>
                </Box>
                
                <Box>
                  <Text fontSize="lg" fontWeight="semibold" mb={2}>
                    Сообщений в секунду: {messagesPerSecond}
                  </Text>
                  <Slider 
                    value={messagesPerSecond} 
                    min={10} 
                    max={1000} 
                    step={10}
                    onChange={(val) => setMessagesPerSecond(val)}
                    isDisabled={isTesting}
                  >
                    <SliderTrack bg="gray.100">
                      <SliderFilledTrack bg="brand.500" />
                    </SliderTrack>
                    <SliderThumb boxSize={6} />
                  </Slider>
                  <HStack justify="space-between" mt={2}>
                    <Text fontSize="sm" color="gray.500">10/сек</Text>
                    <Text fontSize="sm" color="gray.500">1000/сек</Text>
                  </HStack>
                </Box>
                
                <Button 
                  colorScheme="blue" 
                  size="lg" 
                  onClick={handleStartTest}
                  isLoading={isTesting}
                  loadingText="Выполняется тест..."
                >
                  Начать тестирование
                </Button>
                
                {isTesting && (
                  <Box mt={4}>
                    <Text mb={2}>Прогресс тестирования:</Text>
                    <Progress value={progress} size="sm" colorScheme="blue" borderRadius="full" />
                    <Text mt={2} textAlign="center" fontSize="sm">{progress}%</Text>
                  </Box>
                )}
              </VStack>
            </Box>
            
            {/* Панель результатов */}
            <Box flex={1} bg="white" p={6} borderRadius="lg" boxShadow="md">
              <Heading as="h2" size="md" mb={6} color="brand.500">
                Результаты тестирования
              </Heading>
              
              {results ? (
                <VStack spacing={6} align="stretch">
                  <Stat>
                    <StatLabel>Пропускная способность</StatLabel>
                    <StatNumber>{results.throughput} сообщ./сек</StatNumber>
                    <StatHelpText>Чем выше, тем лучше</StatHelpText>
                  </Stat>
                  
                  <Stat>
                    <StatLabel>Задержка</StatLabel>
                    <StatNumber>{results.latency} мс</StatNumber>
                    <StatHelpText>Чем ниже, тем лучше</StatHelpText>
                  </Stat>
                  
                  <Stat>
                    <StatLabel>Использование CPU</StatLabel>
                    <StatNumber>{results.cpuUsage}%</StatNumber>
                    <StatHelpText>Процент использования процессора</StatHelpText>
                  </Stat>
                  
                  <Stat>
                    <StatLabel>Использование памяти</StatLabel>
                    <StatNumber>{results.memoryUsage}%</StatNumber>
                    <StatHelpText>Процент использования памяти</StatHelpText>
                  </Stat>
                </VStack>
              ) : (
                <Box textAlign="center" py={10}>
                  <Text fontSize="lg" color="gray.500">
                    {isTesting 
                      ? "Идет тестирование..." 
                      : "Здесь будут отображаться результаты тестирования"}
                  </Text>
                </Box>
              )}
            </Box>
          </Flex>
        </Flex>
      </Box>
    </ChakraProvider>
  );
}

export default App;