using MQTTnet;
using MQTTnet.Client;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace ControlBrazoRobotico
{
    public class RobotMqttService
    {
        private readonly IMqttClient _mqttClient;
        private readonly string _topic;
        private readonly string _broker;

        public RobotMqttService(string topic, string broker)
        {
            // Si el broker viene vacío desde la config, usamos el por defecto
            _topic = string.IsNullOrWhiteSpace(topic) ? "robot/comandos" : topic;
            _broker = string.IsNullOrWhiteSpace(broker) ? "test.mosquitto.org" : broker;

            var mqttFactory = new MqttFactory();
            _mqttClient = mqttFactory.CreateMqttClient();
        }

        public bool IsConnected => _mqttClient.IsConnected;

        public async Task ConectarAsync()
        {
            // Si ya está conectado, no hacemos nada
            if (IsConnected) return;

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_broker)
                .WithCleanSession()
                .Build();

            // Usamos un timeout para que no se quede colgado si la IP es incorrecta
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                await _mqttClient.ConnectAsync(options, timeout.Token);
            }
        }

        public async Task DesconectarAsync()
        {
            if (IsConnected)
            {
                await _mqttClient.DisconnectAsync();
            }
        }

        public async Task EnviarComandoAsync(string comando)
        {
            if (!IsConnected) return;

            // Preparamos el mensaje
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(_topic)
                .WithPayload(comando.Trim() + "\n") // Salto de línea para el receptor
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                .Build();

            await _mqttClient.PublishAsync(message, CancellationToken.None);
        }
    }
}