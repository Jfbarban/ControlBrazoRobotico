using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Text.Json; // Necesario para manejar JSON
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ControlBrazoRobotico
{

    // Clase para definir la estructura del JSON
    public class PosicionRobot
    {
        public string Nombre { get; set; }
        public int[] Angulos { get; set; } // Array de 6 enteros
    }

    public partial class MainWindow : Window
    {

        private AppConfig _configActual;

        private string rutaArchivo = "Recursos/posiciones.json";
        private List<PosicionRobot> listaPosicionesMemoria = new List<PosicionRobot>();

        private SerialPort serialPort;
        private bool conectado = false;

        // --- MQTT ---
        private RobotMqttService _mqttService;
        private ConexaoMode _conexaoMode = ConexaoMode.Serial;

        private enum ConexaoMode { Serial, Mqtt }

        public MainWindow()
        {
            InitializeComponent();

            CargarPosicionesGuardadas();

            CargarPuertosCOM();

            CargarConfiguracion();

            cmbModoConexion.SelectedIndex = 0;

            ActualizarEstadoUI();
        }

        private void BtnConfig_Click(object sender, RoutedEventArgs e)
        {
            ConfigWindow win = new ConfigWindow(_configActual);
            win.Owner = this;
            if (win.ShowDialog() == true)
            {
                _configActual = win.ConfigResultado;

                // Guardar físicamente en el JSON
                string json = JsonSerializer.Serialize(_configActual, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText("Recursos/config.json", json);

                AplicarConfiguracion(); // Método que actualiza los Sliders.Minimum/Maximum
                LogConsola("Configuración actualizada y guardada.");
            }
        }

        private void CargarConfiguracion()
        {
            string path = "Recursos/config.json";
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    _configActual = JsonSerializer.Deserialize<AppConfig>(json);
                }
                catch { _configActual = new AppConfig(); }
            }
            else
            {
                _configActual = new AppConfig(); // Valores por defecto
            }

            AplicarConfiguracion();
        }

        private void AplicarConfiguracion()
        {
            if (_configActual == null) return;

            // 1. Aplicar rangos a los sliders
            Slider[] sliders = { sliderServo1, sliderServo2, sliderServo3, sliderServo4, sliderServo5, sliderServo6 };
            for (int i = 0; i < 6; i++)
            {
                sliders[i].Minimum = _configActual.MinGrados[i];
                sliders[i].Maximum = _configActual.MaxGrados[i];
                if (sliders[i].Value < sliders[i].Minimum) sliders[i].Value = sliders[i].Minimum;
                if (sliders[i].Value > sliders[i].Maximum) sliders[i].Value = sliders[i].Maximum;
            }

            // 2. Actualizar Puerto Serie si está abierto
            if (serialPort != null && serialPort.IsOpen)
            {
                // Si el baudrate cambió, reiniciamos el puerto
                if (serialPort.BaudRate != _configActual.BaudRate)
                {
                    serialPort.Close();
                    serialPort.BaudRate = _configActual.BaudRate;
                    try { serialPort.Open(); } catch { }
                }
            }

            // 3. Inicializar o Actualizar Servicio MQTT con los datos de Config
            // Pasamos el Topic y la Dirección que vienen del JSON
            _mqttService = new RobotMqttService(_configActual.MqttTopic, _configActual.MqttAddress);

            LogConsola("Configuración técnica aplicada (MQTT y Serial actualizados).");
        }


        private void ActualizarEstadoConexion(bool online)
        {
            if (online)
            {
                // Estado Online
                txtEstado.Text = "ONLINE";
                txtEstado.Foreground = Brushes.LimeGreen;
                ledEstado.Fill = Brushes.LimeGreen;

                btnConectar.Visibility = Visibility.Collapsed;
                btnDesconectar.IsEnabled = true;
                cmbModoConexion.IsEnabled = false;
                cmbPuertos.IsEnabled = false;

                // Habilitar controles
                panelControlManual.IsEnabled = true;
                btnSecuenciador.Visibility = Visibility.Visible;

                LogConsola("Sistema vinculado y listo para operar.");
            }
            else
            {
                // Estado Offline
                txtEstado.Text = "DESCONECTADO";
                txtEstado.Foreground = Brushes.Red;
                ledEstado.Fill = Brushes.Red;

                btnConectar.Visibility = Visibility.Visible;
                btnDesconectar.IsEnabled = false;
                cmbModoConexion.IsEnabled = true;
                cmbPuertos.IsEnabled = true;

                // Bloquear controles
                panelControlManual.IsEnabled = false;
                btnSecuenciador.Visibility = Visibility.Collapsed;

                LogConsola("Conexión finalizada. Controles bloqueados.");
            }
        }

        // Método auxiliar para escribir en tu txtConsola
        private void LogConsola(string mensaje)
        {
            txtConsola.AppendText($"[{DateTime.Now:HH:mm:ss}] {mensaje}\n");
            txtConsola.ScrollToEnd();
        }

        // 1. LLama a esto en el constructor MainWindow(), justo después de InitializeComponent()
        private void CargarPosicionesGuardadas()
        {
            // 1. Limpiar el panel por si acaso
            panelPosiciones.Children.Clear();

            // 2. CREAR POSICIONES BÁSICAS (Sin botón de eliminar)
            // Nombre, s1, s2, s3, s4, s5, s6
            AgregarPosicionFija("HOME", 90, 90, 90, 90, 90, 90);
            AgregarPosicionFija("REPOSO", 0, 45, 180, 90, 90, 20);
            AgregarPosicionFija("TRABAJO", 90, 45, 135, 90, 90, 90);

            // 3. CARGAR POSICIONES DEL JSON
            string rutaRelativa = rutaArchivo.TrimStart('/', '\\');
            if (File.Exists(rutaRelativa))
            {
                try
                {
                    string json = File.ReadAllText(rutaRelativa);
                    listaPosicionesMemoria = JsonSerializer.Deserialize<List<PosicionRobot>>(json) ?? new List<PosicionRobot>();

                    foreach (var pos in listaPosicionesMemoria)
                    {
                        CrearBotonDinamico(pos); // Estas sí llevan la cruz roja
                    }
                }
                catch (Exception ex)
                {
                    LogConsola("Error al cargar JSON: " + ex.Message);
                }
            }
        }

        private void AgregarPosicionFija(string nombre, params int[] angulos)
        {
            Button btn = new Button
            {
                Content = nombre,
                Style = (Style)FindResource("BtnDark"), // El estilo gris oscuro
                Width = 100,
                Height = 35,
                Margin = new Thickness(5),
                Tag = angulos
            };

            btn.Click += BtnDinamico_Click;
            panelPosiciones.Children.Add(btn);
        }

        // 2. Función para crear el botón visualmente
        private void CrearBotonDinamico(PosicionRobot pos)
        {
            // 1. Contenedor Grid para superponer elementos
            Grid contenedor = new Grid { Width = 110, Height = 45, Margin = new Thickness(5) };

            // 2. Botón de ejecución (El principal)
            Button btnPos = new Button
            {
                Content = pos.Nombre.ToUpper(),
                Style = (Style)FindResource("BtnDark"),
                Width = 100,
                Height = 35,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Tag = pos.Angulos // Guardamos los ángulos aquí
            };
            btnPos.Click += BtnDinamico_Click;

            // 3. Botón Eliminar (La cruz roja flotante)
            Button btnDelete = new Button
            {
                Content = "×",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(180, 50, 50)),
                Width = 18,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Cursor = Cursors.Hand,
                Padding = new Thickness(0, -2, 0, 0)
            };

            // Estilo circular para el botón delete
            btnDelete.Template = CrearTemplateCircular();

            // Evento de eliminación
            btnDelete.Click += (s, e) => {
                // 1. Instanciar tu ventana de confirmación personalizada
                // Pasamos el mensaje que queremos mostrar
                ConfirmDialog ventanaConfirm = new ConfirmDialog($"¿Deseas eliminar la posición '{pos.Nombre}'?");

                // 2. Establecer el dueño para que se centre sobre la App principal
                ventanaConfirm.Owner = this;

                // 3. Si el usuario presiona el botón de confirmación (que devuelve true)
                if (ventanaConfirm.ShowDialog() == true)
                {
                    EliminarPosicion(pos.Nombre);
                }
            };

            // Agregar al grid
            contenedor.Children.Add(btnPos);
            contenedor.Children.Add(btnDelete);

            panelPosiciones.Children.Add(contenedor);
        }

        // Helper para redondear el botón rojo
        private ControlTemplate CrearTemplateCircular()
        {
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);
            template.VisualTree = border;
            return template;
        }

        private void EliminarPosicion(string nombre)
        {
            // Quitar de la lista en memoria
            var item = listaPosicionesMemoria.FirstOrDefault(p => p.Nombre == nombre);
            if (item != null)
            {
                listaPosicionesMemoria.Remove(item);

                // Guardar la lista actualizada al archivo
                SincronizarArchivoJson();

                // Refrescar UI
                RefrescarPanelPosiciones();
                LogConsola($"Posición '{nombre}' eliminada.");
            }
        }

        private void SincronizarArchivoJson()
        {
            try
            {
                string rutaRelativa = rutaArchivo.TrimStart('/', '\\');
                var opciones = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(listaPosicionesMemoria, opciones);
                File.WriteAllText(rutaRelativa, json);
            }
            catch (Exception ex) { LogConsola("Error al sincronizar archivo: " + ex.Message); }
        }

        private void RefrescarPanelPosiciones()
        {
            panelPosiciones.Children.Clear();

            // Volver a poner las fijas
            AgregarPosicionFija("HOME", 90, 90, 90, 90, 90, 90);
            AgregarPosicionFija("REPOSO", 0, 45, 180, 90, 90, 20);
            AgregarPosicionFija("TRABAJO", 90, 45, 135, 90, 90, 90);

            // Poner las dinámicas de la memoria
            foreach (var pos in listaPosicionesMemoria)
            {
                CrearBotonDinamico(pos);
            }
        }

        // 3. Evento al hacer click en "GUARDAR ACTUAL"
        private void BtnGuardarNueva_Click(object sender, RoutedEventArgs e)
        {
            InputWindow input = new InputWindow();
            input.Owner = this;
            if (input.ShowDialog() == true)
            {
                int[] angulos = { (int)sliderServo1.Value, (int)sliderServo2.Value, (int)sliderServo3.Value,
                          (int)sliderServo4.Value, (int)sliderServo5.Value, (int)sliderServo6.Value };

                PosicionRobot nuevaPos = new PosicionRobot { Nombre = input.Respuesta, Angulos = angulos };

                listaPosicionesMemoria.Add(nuevaPos);
                SincronizarArchivoJson();
                RefrescarPanelPosiciones();
            }
        }

        // 4. Lógica de guardado en JSON
        private void GuardarEnArchivo(PosicionRobot nuevaPos)
        {
            try
            {
                // 1. Aseguramos que la ruta sea correcta quitando barras iniciales problemáticas
                // Esto convierte "/Recursos/..." en "Recursos/..." relativo al ejecutable
                string rutaRelativa = rutaArchivo.TrimStart('/', '\\');

                // 2. Obtener el directorio (carpeta) del archivo
                string directorio = Path.GetDirectoryName(rutaRelativa);

                // 3. Si la carpeta no existe, la creamos (CRUCIAL)
                if (!string.IsNullOrEmpty(directorio) && !Directory.Exists(directorio))
                {
                    Directory.CreateDirectory(directorio);
                }

                List<PosicionRobot> lista;

                // 4. Si el archivo existe, leemos. Si no, lista nueva.
                if (File.Exists(rutaRelativa))
                {
                    string json = File.ReadAllText(rutaRelativa);
                    // El "?? new List..." maneja si el archivo existe pero está vacío
                    lista = JsonSerializer.Deserialize<List<PosicionRobot>>(json) ?? new List<PosicionRobot>();
                }
                else
                {
                    lista = new List<PosicionRobot>();
                }

                lista.Add(nuevaPos);

                // 5. Opciones para que el JSON se vea bonito y ordenado (Indented)
                var opciones = new JsonSerializerOptions { WriteIndented = true };
                string jsonGuardar = JsonSerializer.Serialize(lista, opciones);

                // 6. Escribir (esto crea el archivo si no existía)
                File.WriteAllText(rutaRelativa, jsonGuardar);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar en disco: " + ex.Message);
            }
        }

        // 5. Qué pasa cuando tocas un botón guardado
        private void BtnDinamico_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null && btn.Tag is int[] angulos)
            {
                // IMPORTANTE: Al cambiar los valores de los Sliders aquí, 
                // se disparará el evento "Slider_ValueChanged" 6 veces (una por cada servo).
                // Si eso satura tu Arduino, podrías comentar estas líneas de los sliders 
                // y solo enviar el comando "ALL" de abajo.

                sliderServo1.Value = angulos[0];
                sliderServo2.Value = angulos[1];
                sliderServo3.Value = angulos[2];
                sliderServo4.Value = angulos[3];
                sliderServo5.Value = angulos[4];
                sliderServo6.Value = angulos[5];

                // Opcional: Enviar comando "ALL" para asegurar sincronización rápida
                string comando = $"ALL:{string.Join(",", angulos)}";

                // CORRECCIÓN: 
                // 1. Se agrega el segundo parámetro (nombreAccion) usando el nombre del botón.
                // 2. Se usa "_ =" para llamar al método async sin bloquear la UI (Fire and forget).
                _ = EnviarComando(comando, "PRESET: " + btn.Content.ToString());
            }
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
        public async Task EnviarComando(string comando, string nombreAccion)
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

                    // USAMOS EL BAUDRATE DE LA CONFIGURACIÓN
                    serialPort = new SerialPort(cmbPuertos.SelectedItem.ToString(), _configActual.BaudRate);
                    serialPort.DataReceived += SerialPort_DataReceived;
                    serialPort.Open();
                    conectado = true;
                }
                else
                {
                    // El _mqttService ya fue creado con el Topic/IP correctos en AplicarConfiguracion()
                    await _mqttService.ConectarAsync();
                    conectado = _mqttService.IsConnected;
                }

                if (conectado)
                {
                    MostrarMensaje($"CONECTADO VIA {_conexaoMode.ToString().ToUpper()}");
                    ActualizarEstadoConexion(true);
                }
            }
            catch (Exception ex)
            {
                conectado = false;
                MostrarMensaje($"Error de conexión: {ex.Message}");
                ActualizarEstadoConexion(false);
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
            ActualizarEstadoConexion(false);
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
            if (!IsLoaded) return;

            Slider s = sender as Slider;
            int valor = (int)e.NewValue;

            // Solo actualizamos el texto visualmente
            if (s == sliderServo1) txtServo1.Text = $"{valor}°";
            else if (s == sliderServo2) txtServo2.Text = $"{valor}°";
            else if (s == sliderServo3) txtServo3.Text = $"{valor}°";
            else if (s == sliderServo4) txtServo4.Text = $"{valor}°";
            else if (s == sliderServo5) txtServo5.Text = $"{valor}°";
            else if (s == sliderServo6) txtServo6.Text = $"{valor}°";
        }

        private void BtnPinza_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            int pos = btn.Tag.ToString() == "abrir" ? 30 : 180; // Ajusta según el rango de tu pinza
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

        private void sliderServo_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Slider s = sender as Slider;
            if (s == null) return;

            int valor = (int)s.Value; // Tomamos el valor donde quedó el slider
            int numServo = 0;

            // Identificamos qué servo es
            if (s == sliderServo1) numServo = 1;
            else if (s == sliderServo2) numServo = 2;
            else if (s == sliderServo3) numServo = 3;
            else if (s == sliderServo4) numServo = 4;
            else if (s == sliderServo5) numServo = 5;
            else if (s == sliderServo6) numServo = 6;

            // Enviamos el comando una sola vez
            if (numServo > 0)
            {
                _ = EnviarComando($"S{numServo}:{valor}", $"Servo {numServo} (Final)");
            }
        }

        private void BtnAbrirSecuenciador_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Cargamos la lista de posiciones actual desde el archivo JSON
                List<PosicionRobot> biblioteca = new List<PosicionRobot>();

                string rutaRelativa = rutaArchivo.TrimStart('/', '\\');
                if (File.Exists(rutaRelativa))
                {
                    string json = File.ReadAllText(rutaRelativa);
                    biblioteca = JsonSerializer.Deserialize<List<PosicionRobot>>(json) ?? new List<PosicionRobot>();
                }

                // 2. Abrimos la ventana pasando 'this' (MainWindow) y la biblioteca
                SequenceWindow seqWin = new SequenceWindow(this, biblioteca);
                seqWin.Owner = this; // Para que aparezca centrada sobre la principal
                seqWin.ShowDialog();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al abrir secuenciador: " + ex.Message);
            }
        }
    }

    // Pequeña ventana modal generada por código para pedir el nombre
    public class InputWindow : Window
    {
        public string Respuesta { get; private set; }
        private TextBox txtInput;

        public InputWindow()
        {
            Title = "Guardar Posición";
            Width = 300; Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E"));

            StackPanel panel = new StackPanel { Margin = new Thickness(10) };

            TextBlock lbl = new TextBlock { Text = "Nombre de la posición:", Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10) };
            txtInput = new TextBox { Height = 25, Margin = new Thickness(0, 0, 0, 10) };

            Button btnOk = new Button { Content = "GUARDAR", Height = 30, Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6D00")), Foreground = Brushes.White, FontWeight = FontWeights.Bold };
            btnOk.Click += (s, e) => { Respuesta = txtInput.Text; DialogResult = true; };

            panel.Children.Add(lbl);
            panel.Children.Add(txtInput);
            panel.Children.Add(btnOk);

            Content = panel;
        }
    }

}