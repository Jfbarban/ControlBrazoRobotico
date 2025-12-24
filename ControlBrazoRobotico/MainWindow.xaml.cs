using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ControlBrazoRobotico
{
    public partial class MainWindow : Window
    {
        private SerialPort serialPort;
        private bool conectado = false;

        // --- MQTT ---
        private RobotMqttService _mqttService;
        private ConexaoMode _conexaoMode = ConexaoMode.Serial;
        private const string MqttTopic = "mi_usuario/robot/comandos";

        private enum ConexaoMode { Serial, Mqtt }

        public MainWindow()
        {
            InitializeComponent();
            CargarPuertosCOM();

            // Inicialización de Servicios
            _mqttService = new RobotMqttService(MqttTopic);
            cmbModoConexion.SelectedIndex = 0;

            ActualizarEstadoUI();
        }

        private void CargarPuertosCOM()
        {
            try
            {
                cmbPuertos.Items.Clear();
                string[] puertos = SerialPort.GetPortNames();
                foreach (string p in puertos) cmbPuertos.Items.Add(p);
                if (cmbPuertos.Items.Count > 0) cmbPuertos.SelectedIndex = 0;
            }
            catch (Exception ex) { MostrarMensaje($"Error puertos: {ex.Message}"); }
        }

        // ---------------------------------------------------------
        // LÓGICA DE ENVÍO UNIFICADA
        // ---------------------------------------------------------
        private async Task EnviarComando(string comando, string nombreAccion)
        {
            if (!conectado) return;

            try
            {
                if (_conexaoMode == ConexaoMode.Serial)
                {
                    if (serialPort != null && serialPort.IsOpen)
                    {
                        serialPort.Write(comando + "\n");
                        MostrarMensaje($"[SERIAL] {nombreAccion}: {comando}");
                    }
                }
                else
                {
                    if (_mqttService.IsConnected)
                    {
                        await _mqttService.EnviarComandoAsync(comando);
                        MostrarMensaje($"[MQTT] {nombreAccion}: {comando}");
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error en {nombreAccion}: {ex.Message}");
            }
        }

        // ---------------------------------------------------------
        // EVENTOS DE CONEXIÓN
        // ---------------------------------------------------------
        private async void BtnConectar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_conexaoMode == ConexaoMode.Serial)
                {
                    if (cmbPuertos.SelectedItem == null) { MostrarMensaje("Elija un puerto COM"); return; }

                    serialPort = new SerialPort(cmbPuertos.SelectedItem.ToString(), 9600); // Baudrate por defecto
                    serialPort.DataReceived += SerialPort_DataReceived;
                    serialPort.Open();
                    conectado = true;
                }
                else
                {
                    await _mqttService.ConectarAsync();
                    conectado = _mqttService.IsConnected;
                }

                if (conectado) MostrarMensaje($"CONECTADO VIA {_conexaoMode.ToString().ToUpper()}");
            }
            catch (Exception ex)
            {
                conectado = false;
                MostrarMensaje($"Error de conexión: {ex.Message}");
            }
            ActualizarEstadoUI();
        }

        private async void BtnDesconectar_Click(object sender, RoutedEventArgs e)
        {
            if (_conexaoMode == ConexaoMode.Serial && serialPort != null)
            {
                serialPort.Close();
                serialPort.Dispose();
            }
            else if (_mqttService != null)
            {
                await _mqttService.DesconectarAsync();
            }

            conectado = false;
            MostrarMensaje("Sistema Offline");
            ActualizarEstadoUI();
        }

        private void BtnCambiarModo_Click(object sender, SelectionChangedEventArgs e)
        {
            if (cmbModoConexion.SelectedItem == null) return;

            var item = cmbModoConexion.SelectedItem as ComboBoxItem;
            _conexaoMode = item.Content.ToString().Contains("Serial") ? ConexaoMode.Serial : ConexaoMode.Mqtt;

            // Habilitar/Deshabilitar selector de puerto según el modo
            if (cmbPuertos != null) cmbPuertos.IsEnabled = (_conexaoMode == ConexaoMode.Serial);

            MostrarMensaje($"Modo cambiado a: {_conexaoMode}");
        }

        // ---------------------------------------------------------
        // CONTROL DE SERVOS
        // ---------------------------------------------------------
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return; // Evita errores al inicializar la ventana

            Slider s = sender as Slider;
            int valor = (int)e.NewValue;
            int numServo = 0;

            if (s == sliderServo1) { txtServo1.Text = $"{valor}°"; numServo = 1; }
            else if (s == sliderServo2) { txtServo2.Text = $"{valor}°"; numServo = 2; }
            else if (s == sliderServo3) { txtServo3.Text = $"{valor}°"; numServo = 3; }
            else if (s == sliderServo4) { txtServo4.Text = $"{valor}°"; numServo = 4; }
            else if (s == sliderServo5) { txtServo5.Text = $"{valor}°"; numServo = 5; }
            else if (s == sliderServo6) { txtServo6.Text = $"{valor}°"; numServo = 6; }

            if (numServo > 0) _ = EnviarComando($"S{numServo}:{valor}", $"Servo {numServo}");
        }

        private void BtnPinza_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            int pos = btn.Tag.ToString() == "abrir" ? 20 : 150; // Ajusta según el rango de tu pinza
            sliderServo6.Value = pos;
        }

        private void BtnPosicionHome_Click(object sender, RoutedEventArgs e) => SetAllServos(90, 90, 90, 90, 90, 90);
        private void BtnPosicionReposo_Click(object sender, RoutedEventArgs e) => SetAllServos(0, 45, 180, 90, 90, 20);
        private void BtnPosicionTrabajo_Click(object sender, RoutedEventArgs e) => SetAllServos(90, 45, 135, 90, 90, 90);

        private void SetAllServos(int s1, int s2, int s3, int s4, int s5, int s6)
        {
            sliderServo1.Value = s1;
            sliderServo2.Value = s2;
            sliderServo3.Value = s3;
            sliderServo4.Value = s4;
            sliderServo5.Value = s5;
            sliderServo6.Value = s6;
            _ = EnviarComando($"ALL:{s1},{s2},{s3},{s4},{s5},{s6}", "Posición Global");
        }

        // ---------------------------------------------------------
        // UTILIDADES UI
        // ---------------------------------------------------------
        private void ActualizarEstadoUI()
        {
            btnConectar.IsEnabled = !conectado;
            btnDesconectar.IsEnabled = conectado;
            txtEstado.Text = conectado ? "SISTEMA ONLINE" : "SISTEMA OFFLINE";
            txtEstado.Foreground = conectado ? Brushes.LimeGreen : Brushes.Red;
            if (ledEstado != null) ledEstado.Fill = conectado ? Brushes.LimeGreen : Brushes.Red;
        }

        private void MostrarMensaje(string mensaje)
        {
            Dispatcher.Invoke(() => {
                txtConsola.AppendText($"[{DateTime.Now:HH:mm:ss}] {mensaje}\n");
                txtConsola.ScrollToEnd();
            });
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try { string data = serialPort.ReadLine(); MostrarMensaje($"MCU: {data.Trim()}"); }
            catch { }
        }

        private void BtnLimpiarConsola_Click(object sender, RoutedEventArgs e) => txtConsola.Clear();

        protected override void OnClosed(EventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen) serialPort.Close();
            if (_mqttService != null && _mqttService.IsConnected) Task.Run(() => _mqttService.DesconectarAsync()).Wait();
            base.OnClosed(e);
        }
    }
}