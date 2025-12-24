using System;
using System.Collections.Generic;
using System.Text;

namespace ControlBrazoRobotico
{
    public class AppConfig
    {
        public string MqttAddress { get; set; } = "localhost";
        public int BaudRate { get; set; } = 9600;
        // Guardamos Min y Max para cada uno de los 6 servos
        public int[] MinGrados { get; set; } = { 0, 0, 0, 0, 0, 0 };
        public int[] MaxGrados { get; set; } = { 180, 180, 180, 180, 180, 180 };
    }
}
