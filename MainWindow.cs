ControlBrazoRobotico\MainWindow.xaml.cs
using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Text;
using MQTTnet;
using MQTTnet.Client;
using System.Threading.Tasks;
using ControlBrazoRobotico.Hardware;
using ControlBrazoRobotico.Networking;

namespace ControlBrazoRobotico
{
    public partial class MainWindow : Window
    {
        private ISerialPort serialPort;
        private ISerialPortFactory _serialFactory;
        private bool conectado = false;

        // --- Nuevos Miembros MQTT ---
        private IMqttService _mqttService;
        private ConexaoMode _conexaoMode = ConexaoMode.Serial;
        private const string MqttTopic = "mi_usuario/robot/comandos"; // Usa el mismo topic que el Agente Robot

        // Enum para el estado de la conexión (Serial o MQTT)
        private enum ConexaoMode { Serial, Mqtt }

        public MainWindow() : this(null, null)
        {
        }

        // Constructor para inyección de dependencias (tests / escenarios avanzados)
        public MainWindow(IMqttService mqttService, ISerialPortFactory serialFactory)
        {
            InitializeComponent();

            _serialFactory = serialFactory ?? new SerialPortFactory();
            _mqttService = mqttService ?? new RobotMqttServiceAdapter(MqttTopic);

            CargarPuertosCOM();

            ActualizarEstadoConexion();

            // Configura el estado inicial del switch de modo (si existe control)
            if (cmbModoConexion != null)
            {
                cmbModoConexion.SelectedIndex = 0;
            }
        }

        // ------------------------------------------
        // LÓGICA DE ENVÍO UNIFICADA (SERIAL O MQTT)
        // ------------------------------------------

        // TODOS los métodos que envían comandos deben ser ASÍNCRONOS (async)
        internal async Task EnviarComandoAsync(string comando, string nombreAccion)
        {
            if (_conexaoMode == ConexaoMode.Serial)
            {
                if (!conectado) return;
                try
                {
                    // Comando Serial (Necesita \n al final)
                    serialPort.Write(comando + "\n");
                    MostrarMensaje($"[SERIAL] Enviado {nombreAccion}: {comando}");
                }
                catch (Exception ex)
                {
                    MostrarMensaje($"Error enviando {nombreAccion} (SERIAL): {ex.Message}");
                }
            }
            else // Modo MQTT
            {
                if (!_mqttService.IsConnected) return;
                try
                {
                    // Comando MQTT (El servicio agrega \n si es necesario)
                    await _mqttService.EnviarComandoAsync(comando);
                    MostrarMensaje($"[MQTT] Enviado {nombreAccion}: {comando}");
                }
                catch (Exception ex)
                {
                    MostrarMensaje($"Error enviando {nombreAccion} (MQTT): {ex.Message}");
                }
            }
        }

        private async void BtnCambiarModo_Click(object sender, RoutedEventArgs e)
        {
            if (cmbModoConexion.SelectedItem == null) return;

            var nuevoModoStr = (cmbModoConexion.SelectedItem as ComboBoxItem).Content.ToString();
            var nuevoModo = (nuevoModoStr == "Serial (COM)") ? ConexaoMode.Serial : ConexaoMode.Mqtt;

            // 1. Desconectar el modo actualmente activo antes de cambiar
            if (conectado)
            {
                // Usamos la lógica de desconexión sin argumentos
                await Task.Run(() => BtnDesconectar_Click(null, null));
            }

            // 2. Ocultar/Mostrar controles específicos (opcional, pero útil)
            if (cmbPuertos != null) cmbPuertos.IsEnabled = (nuevoModo == ConexaoMode.Serial);
            if (cmbBaudRate != null) cmbBaudRate.IsEnabled = (nuevoModo == ConexaoMode.Serial);

            // 3. Cambiar el modo
            _conexaoMode = nuevoModo;
            ActualizarEstadoConexion();

            MostrarMensaje($"Modo de conexión cambiado a: {_conexaoMode}");
        }

        private async void EnviarMovimientoSuave(int s1, int s2, int s3, int s4, int s5, int s6, int tiempoMs = 2000)
        {
            if (!conectado) return;

            try
            {
                // Formato: "SMOOTH:90,45,135,90,90,73,2000"
                string comando = $"SMOOTH:{s1},{s2},{s3},{s4},{s5},{s6},{tiempoMs}\n";
                await EnviarComandoAsync(comando, "Movimiento Suave");
                MostrarMensaje($"Enviado movimiento suave: {comando.Trim()}");
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error enviando movimiento suave: {ex.Message}");
            }
        }

        private void BtnMovimientoSuave_Click(object sender, RoutedEventArgs e)
        {
            int s1 = (int)sliderCoordServo1.Value;
            int s2 = (int)sliderCoordServo2.Value;
            int s3 = (int)sliderCoordServo3.Value;
            int s4 = (int)sliderServo4.Value;
            int s5 = (int)sliderServo5.Value;
            int s6 = (int)sliderServo6.Value;

            EnviarMovimientoSuave(s1, s2, s3, s4, s5, s6, 1500);
        }

        private void CargarPuertosCOM()
        {
            try
            {
                if (cmbPuertos == null) return;
                cmbPuertos.Items.Clear();
                string[] puertos = SerialPort.GetPortNames();
                foreach (string puerto in puertos)
                {
                    cmbPuertos.Items.Add(puerto);
                }
                if (cmbPuertos.Items.Count > 0)
                {
                    cmbPuertos.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error al cargar puertos COM: {ex.Message}");
            }
        }

        // ------------------------------------------
        // LÓGICA DE ENVÍO UNIFICADA (SERIAL O MQTT)
        // ------------------------------------------

        private async void BtnConectar_Click(object sender, RoutedEventArgs e)
        {
            if (_conexaoMode == ConexaoMode.Serial)
            {
                // --- LÓGICA DE CONEXIÓN SERIAL ---
                if (cmbPuertos.SelectedItem == null)
                {
                    MostrarMensaje("Selecciona un puerto COM");
                    return;
                }
                try
                {
                    string puerto = cmbPuertos.SelectedItem.ToString();
                    // Asegúrate de castear correctamente el ComboBoxItem
                    int baudRate = int.Parse((cmbBaudRate.SelectedItem as ComboBoxItem).Content.ToString());

                    serialPort = _serialFactory.Create(puerto, baudRate);
                    serialPort.DataReceived += SerialPort_DataReceived;
                    serialPort.Open();

                    conectado = true;
                    MostrarMensaje($"Conectado a {puerto} - {baudRate} baudios (SERIAL)");
                }
                catch (Exception ex)
                {
                    conectado = false;
                    MostrarMensaje($"Error al conectar Serial: {ex.Message}");
                }
            }
            else // Modo MQTT
            {
                // --- LÓGICA DE CONEXIÓN MQTT ---
                try
                {
                    // La conexión es asíncrona
                    await _mqttService.ConectarAsync();

                    // Usamos la misma bandera 'conectado' para el estado general de la UI
                    conectado = _mqttService.IsConnected;
                    MostrarMensaje($"Conectado al Broker MQTT: {conectado}");
                }
                catch (Exception ex)
                {
                    conectado = false;
                    MostrarMensaje($"Error al conectar MQTT: {ex.Message}");
                }
            }
            ActualizarEstadoConexion();
        }

        private async void BtnDesconectar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_conexaoMode == ConexaoMode.Serial)
                {
                    // --- LÓGICA DE DESCONEXIÓN SERIAL ---
                    if (serialPort != null && serialPort.IsOpen)
                    {
                        serialPort.Close();
                        serialPort.Dispose();
                    }
                    MostrarMensaje("Desconectado Serial");
                }
                else // Modo MQTT
                {
                    // --- LÓGICA DE DESCONEXIÓN MQTT ---
                    if (_mqttService.IsConnected)
                    {
                        // La desconexión es asíncrona
                        await _mqttService.DesconectarAsync();
                        MostrarMensaje("Desconectado MQTT");
                    }
                }

                conectado = false;
                ActualizarEstadoConexion();
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error al desconectar: {ex.Message}");
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string datos = serialPort.ReadLine();
                Dispatcher.Invoke(() =>
                {
                    MostrarMensaje($"Arduino: {datos}");
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MostrarMensaje($"Error recibiendo datos: {ex.Message}");
                });
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender == sliderServo1)
            {
                if (e.NewValue.Equals(null))
                {
                    txtServo1.Text = "0";
                }
                else
                {
                    txtServo1.Text = (e.NewValue).ToString();
                }

                EnviarComandoServo(1, (int)e.NewValue);
            }
            else if (sender == sliderServo2)
            {
                txtServo2.Text = $"{e.NewValue:0}°";
                EnviarComandoServo(2, (int)e.NewValue);
            }
            else if (sender == sliderServo3)
            {
                txtServo3.Text = $"{e.NewValue:0}°";
                EnviarComandoServo(3, (int)e.NewValue);
            }
            else if (sender == sliderServo4)
            {
                txtServo4.Text = $"{e.NewValue:0}°";
                EnviarComandoServo(4, (int)e.NewValue);
            }
            else if (sender == sliderServo5)
            {
                txtServo5.Text = $"{e.NewValue:0}°";
                EnviarComandoServo(5, (int)e.NewValue);
            }
            else if (sender == sliderServo6)
            {
                txtServo6.Text = $"{e.NewValue:0}°";
                EnviarComandoServo(6, (int)e.NewValue);
            }
        }

        private void SliderCoord_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Solo actualiza los valores, no envía automáticamente
            // El usuario debe presionar "Enviar Todas las Posiciones"
        }

        private void BtnPosicion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button boton && boton.Tag != null)
            {
                string[] datos = boton.Tag.ToString().Split(',');
                if (datos.Length == 2 && int.TryParse(datos[0], out int servo) && int.TryParse(datos[1], out int posicion))
                {
                    switch (servo)
                    {
                        case 1: sliderServo1.Value = posicion; break;
                        case 2: sliderServo2.Value = posicion; break;
                        case 3: sliderServo3.Value = posicion; break;
                        case 4: sliderServo4.Value = posicion; break;
                        case 5: sliderServo5.Value = posicion; break;
                        case 6: sliderServo6.Value = posicion; break;
                    }
                    EnviarComandoServo(servo, posicion);
                }
            }
        }

        private void BtnPinza_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button boton && boton.Tag != null)
            {
                string accion = boton.Tag.ToString();
                int posicion = accion == "abrir" ? 0 : 180;
                sliderServo6.Value = posicion;
                EnviarComandoServo(6, posicion);
            }
        }

        private void BtnPosicionHome_Click(object sender, RoutedEventArgs e)
        {
            EnviarPosicionesCoordinadas(90, 90, 90, 90, 90, 90);
            ActualizarSlidersCoordinados(90, 90, 90, 90, 90, 90);
        }

        private void BtnPosicionReposo_Click(object sender, RoutedEventArgs e)
        {
            EnviarPosicionesCoordinadas(0, 0, 0, 90, 90, 0);
            ActualizarSlidersCoordinados(0, 0, 0, 90, 90, 0);
        }

        private void BtnPosicionTrabajo_Click(object sender, RoutedEventArgs e)
        {
            EnviarPosicionesCoordinadas(90, 45, 135, 90, 90, 90);
            ActualizarSlidersCoordinados(90, 45, 135, 90, 90, 90);
        }

        private void BtnEnviarTodo_Click(object sender, RoutedEventArgs e)
        {
            int s1 = (int)sliderCoordServo1.Value;
            int s2 = (int)sliderCoordServo2.Value;
            int s3 = (int)sliderCoordServo3.Value;
            int s4 = (int)sliderServo4.Value;
            int s5 = (int)sliderServo5.Value;
            int s6 = (int)sliderServo6.Value;

            EnviarPosicionesCoordinadas(s1, s2, s3, s4, s5, s6);
        }

        private async void EnviarComandoServo(int numeroServo, int posicion)
        {
            // Formato: "S1:90"
            string comando = $"S{numeroServo}:{posicion}";
            await EnviarComandoAsync(comando, $"Servo {numeroServo}");
        }

        private async void EnviarPosicionesCoordinadas(int s1, int s2, int s3, int s4, int s5, int s6)
        {
            // Formato: "ALL:90,45,135,90,90,90"
            string comando = $"ALL:{s1},{s2},{s3},{s4},{s5},{s6}";
            await EnviarComandoAsync(comando, "Coordinado");
        }

        private void ActualizarSlidersCoordinados(int s1, int s2, int s3, int s4, int s5, int s6)
        {
            sliderServo1.Value = s1;
            sliderServo2.Value = s2;
            sliderServo3.Value = s3;
            sliderServo4.Value = s4;
            sliderServo5.Value = s5;
            sliderServo6.Value = s6;

            sliderCoordServo1.Value = s1;
            sliderCoordServo2.Value = s2;
            sliderCoordServo3.Value = s3;
        }

        private void ActualizarEstadoConexion()
        {
            btnConectar.IsEnabled = !conectado;
            btnDesconectar.IsEnabled = conectado;
            txtEstado.Text = conectado ? "Conectado" : "Desconectado";
            txtEstado.Foreground = conectado ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
        }

        private void MostrarMensaje(string mensaje)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            txtConsola.AppendText($"[{timestamp}] {mensaje}\n");
            txtConsola.ScrollToEnd();
        }

        private void BtnLimpiarConsola_Click(object sender, RoutedEventArgs e)
        {
            txtConsola.Clear();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
            }
            // También desconecta el cliente MQTT al cerrar la aplicación
            if (_mqttService != null && _mqttService.IsConnected)
            {
                // Usamos Wait() porque OnClosed no es async
                Task.Run(() => _mqttService.DesconectarAsync()).Wait();
            }
            base.OnClosed(e);
        }
    }
}