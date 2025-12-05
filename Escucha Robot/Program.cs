using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using System.Linq; // Necesario para FirstOrDefault en InicializarPuertoSerial

namespace Escucha_Robot
{
    class Program
    {
        // Tasa de Baudios que tu Arduino debe usar (ej. 9600 o 115200)
        static readonly int BaudRate = 9600;
        static readonly string MqttTopic = "mi_usuario/robot/comandos";
        static SerialPort _serialPort;

        /// <summary>
        /// Busca y se conecta automáticamente al primer puerto COM disponible.
        /// </summary>
        public static SerialPort InicializarPuertoSerial(int baudRate)
        {
            // 1. Obtener la lista de todos los puertos COM disponibles en el sistema.
            string[] puertosDisponibles = SerialPort.GetPortNames();

            if (puertosDisponibles.Length == 0)
            {
                Console.WriteLine("❌ ERROR: No se encontraron puertos COM disponibles.");
                return null;
            }

            // 2. Seleccionar el primer puerto de la lista.
            string puertoSeleccionado = puertosDisponibles[0];

            SerialPort miPuertoSerial = new SerialPort(puertoSeleccionado, baudRate);

            try
            {
                // 3. Abrir la conexión
                miPuertoSerial.Open();

                Console.WriteLine($"✅ Éxito: Conectado automáticamente a {puertoSeleccionado} a {baudRate} baudios.");
                // Configurar un timeout para la escritura
                miPuertoSerial.WriteTimeout = 500;

                return miPuertoSerial;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR al abrir el puerto {puertoSeleccionado}: {ex.Message}");
                if (miPuertoSerial.IsOpen) miPuertoSerial.Close();
                return null;
            }
        }

        static async Task Main(string[] args)
        {
            Console.Title = "Agente Puente MQTT a Arduino (Robot)";
            Console.WriteLine("--- Iniciando Agente Puente ---");

            // 1. Configuración de Conexión Serial (Integración del selector automático)
            _serialPort = InicializarPuertoSerial(BaudRate);

            if (_serialPort == null)
            {
                Console.WriteLine("❌ No se pudo establecer la conexión serial.");
                Console.WriteLine("\n*** Presione una tecla para cerrar la aplicación. ***");
                Console.ReadKey();
                return; // Termina si la conexión serial falla
            }

            // 2. Configuración e Inicialización MQTT
            var mqttFactory = new MqttFactory();
            var mqttClient = mqttFactory.CreateMqttClient();

            // Configurar el manejo de mensajes
            mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                // El comando es el payload del mensaje MQTT
                string comando = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

                Console.WriteLine($"[INTERNET] Comando recibido: {comando.Trim()}");

                if (_serialPort.IsOpen)
                {
                    // WriteLine() agrega el salto de línea (\n) que Arduino espera.
                    try
                    {
                        _serialPort.WriteLine(comando);
                        Console.WriteLine($"[SERIAL] Enviando: {comando.Trim()}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SERIAL ERROR] No se pudo enviar el comando: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("[SERIAL] Puerto cerrado. Comando descartado.");
                }
                return Task.CompletedTask;
            };

            // Conectar al Broker
            try
            {
                var mqttOptions = new MqttClientOptionsBuilder()
                    .WithTcpServer("test.mosquitto.org") // Broker público
                    .WithCleanSession()
                    .Build();

                await mqttClient.ConnectAsync(mqttOptions, CancellationToken.None);
                Console.WriteLine("[MQTT] Conectado al servidor en la nube.");

                // Suscribirse
                var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder()
                    .WithTopicFilter(f => { f.WithTopic(MqttTopic); })
                    .Build();

                await mqttClient.SubscribeAsync(mqttSubscribeOptions);
                Console.WriteLine($"[MQTT] Escuchando comandos en el Topic: {MqttTopic}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MQTT ERROR] No se pudo conectar/suscribir al broker: {ex.Message}");
            }


            // 3. Mantener la app corriendo y limpiar al cerrar
            Console.WriteLine("\n*** Agente listo. Presione ENTER para detener y cerrar. ***");
            Console.ReadLine();

            if (mqttClient.IsConnected)
            {
                await mqttClient.DisconnectAsync();
                Console.WriteLine("[MQTT] Desconectado del broker.");
            }

            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                Console.WriteLine("[SERIAL] Puerto cerrado.");
            }

            Console.WriteLine("Aplicación finalizada.");
        }
    }
}