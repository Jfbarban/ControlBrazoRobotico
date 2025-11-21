using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Text;

namespace ControlBrazoRobotico
{
    public partial class MainWindow : Window
    {
        private SerialPort serialPort;
        private bool conectado = false;

        public MainWindow()
        {
            InitializeComponent();
            CargarPuertosCOM();
            ActualizarEstadoConexion();
        }

        private void CargarPuertosCOM()
        {
            try
            {
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

        private void BtnConectar_Click(object sender, RoutedEventArgs e)
        {
            if (cmbPuertos.SelectedItem == null)
            {
                MostrarMensaje("Selecciona un puerto COM");
                return;
            }

            try
            {
                string puerto = cmbPuertos.SelectedItem.ToString();
                int baudRate = int.Parse((cmbBaudRate.SelectedItem as ComboBoxItem).Content.ToString());

                serialPort = new SerialPort(puerto, baudRate);
                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.Open();

                conectado = true;
                ActualizarEstadoConexion();
                MostrarMensaje($"Conectado a {puerto} - {baudRate} baudios");
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error al conectar: {ex.Message}");
            }
        }

        private void BtnDesconectar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    serialPort.Close();
                    serialPort.Dispose();
                }
                conectado = false;
                ActualizarEstadoConexion();
                MostrarMensaje("Desconectado");
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
                txtServo1.Text = $"{e.NewValue:0}°";
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

        private void EnviarComandoServo(int numeroServo, int posicion)
        {
            if (!conectado) return;

            try
            {
                // Formato: "S1:90" donde S1 = Servo 1, 90 = posición
                string comando = $"S{numeroServo}:{posicion}\n";
                serialPort.Write(comando);
                MostrarMensaje($"Enviado: {comando.Trim()}");
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error enviando comando: {ex.Message}");
            }
        }

        private void EnviarPosicionesCoordinadas(int s1, int s2, int s3, int s4, int s5, int s6)
        {
            if (!conectado) return;

            try
            {
                // Formato: "ALL:90,45,135,90,90,90" para todos los servos
                string comando = $"ALL:{s1},{s2},{s3},{s4},{s5},{s6}\n";
                serialPort.Write(comando);
                MostrarMensaje($"Enviado coordinado: {comando.Trim()}");
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error enviando comando coordinado: {ex.Message}");
            }
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
            base.OnClosed(e);
        }
    }
}