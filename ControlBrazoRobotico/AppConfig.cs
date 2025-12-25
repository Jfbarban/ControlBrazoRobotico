using System;
using System.Collections.Generic;
using System.Text;

namespace ControlBrazoRobotico
{
    public class AppConfig
    {
        public string MqttAddress { get; set; } = "test.mosquitto.org";
        public string MqttTopic { get; set; } = "mi_usuario/robot/comandos";
        public int BaudRate { get; set; } = 9600;
        public int[] MinGrados { get; set; } = { 0, 0, 0, 0, 0, 0 };
        public int[] MaxGrados { get; set; } = { 180, 180, 180, 180, 180, 180 };
    }
}
