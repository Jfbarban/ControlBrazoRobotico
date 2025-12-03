using MQTTnet;
using MQTTnet.Client;
using System.Text;
using System.Threading.Tasks;
using System;

namespace ControlBrazoRobotico
{
    public class RobotMqttService
    {
        private readonly IMqttClient _mqttClient;
        private readonly string _topic;
        private readonly string _broker = "test.mosquitto.org"; // Broker público para pruebas

        public RobotMqttService(string topic)
        {
            _topic = topic;
            var mqttFactory = new MqttFactory();
            _mqttClient = mqttFactory.CreateMqttClient();
        }

        public bool IsConnected => _mqttClient.IsConnected;

        public async Task ConectarAsync()
        {
            if (_mqttClient.IsConnected) return;

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_broker)
                .WithCleanSession()
                .Build();

            // Manejar reconexiones o errores aquí si es necesario
            await _mqttClient.ConnectAsync(options);
        }

        public async Task DesconectarAsync()
        {
            if (_mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync();
            }
        }

        public async Task EnviarComandoAsync(string comando)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Cliente MQTT no conectado.");
            }

            // Agregar el salto de línea (\n) si el agente robot lo espera
            var payload = Encoding.UTF8.GetBytes(comando.Trim() + "\n");

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(_topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _mqttClient.PublishAsync(message);
        }
    }
}
