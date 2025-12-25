using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ControlBrazoRobotico
{
    /// <summary>
    /// Lógica de interacción para ConfigWindow.xaml
    /// </summary>
    public partial class ConfigWindow : Window
    {
        public AppConfig ConfigResultado { get; private set; }
        private TextBox[] minInputs = new TextBox[6];
        private TextBox[] maxInputs = new TextBox[6];

        public ConfigWindow(AppConfig configActual)
        {
            InitializeComponent();
            ConfigResultado = configActual;

            // -------------------------------------------------------
            // 1. CARGA DE DATOS GENERALES
            // -------------------------------------------------------

            // Cargar datos de MQTT (Broker y Topic)
            txtMqtt.Text = configActual.MqttAddress;
            txtTopic.Text = configActual.MqttTopic; // <--- IMPORTANTE: Cargamos el Topic

            // Cargar Combobox Baudrate
            int[] rates = { 2400, 4800, 9600, 19200, 38400, 57600, 115200 };
            foreach (var r in rates) cmbBaud.Items.Add(r);

            // Seleccionar el BaudRate actual
            cmbBaud.SelectedItem = configActual.BaudRate;
            // Si por alguna razón el baudrate guardado no está en la lista, seleccionamos 9600
            if (cmbBaud.SelectedIndex == -1) cmbBaud.SelectedItem = 9600;


            // -------------------------------------------------------
            // 2. GENERAR TABLA DE SERVOS DINÁMICA
            // -------------------------------------------------------

            // Opcional: Añadir cabeceras visuales (ID, MIN, MAX)
            gridServos.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            AgregarCabecera("ID", 0, 0);
            AgregarCabecera("MIN °", 0, 1);
            AgregarCabecera("MAX °", 0, 2);

            // Bucle para crear las filas de los 6 servos
            for (int i = 0; i < 6; i++)
            {
                // Añadimos una nueva fila al Grid
                gridServos.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                int currentRow = i + 1; // +1 porque la fila 0 son las cabeceras

                // Columna 0: Etiqueta (S1, S2...)
                var lbl = new TextBlock
                {
                    Text = $"S{i + 1}",
                    Foreground = Brushes.Gray,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetRow(lbl, currentRow);
                Grid.SetColumn(lbl, 0);
                gridServos.Children.Add(lbl);

                // Columna 1: Input Mínimo
                minInputs[i] = CrearTextBoxEstilizado(configActual.MinGrados[i].ToString());
                Grid.SetRow(minInputs[i], currentRow);
                Grid.SetColumn(minInputs[i], 1);
                gridServos.Children.Add(minInputs[i]);

                // Columna 2: Input Máximo
                maxInputs[i] = CrearTextBoxEstilizado(configActual.MaxGrados[i].ToString());
                Grid.SetRow(maxInputs[i], currentRow);
                Grid.SetColumn(maxInputs[i], 2);
                gridServos.Children.Add(maxInputs[i]);
            }
        }

        // Método auxiliar para no repetir código de estilo en los TextBox
        private TextBox CrearTextBoxEstilizado(string texto)
        {
            return new TextBox
            {
                Text = texto,
                Height = 25,
                Margin = new Thickness(5, 2, 5, 2),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
        }

        // Método auxiliar para las cabeceras de la tabla
        private void AgregarCabecera(string titulo, int fila, int columna)
        {
            var txt = new TextBlock
            {
                Text = titulo,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 109, 0)), // Naranja
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 5)
            };
            Grid.SetRow(txt, fila);
            Grid.SetColumn(txt, columna);
            gridServos.Children.Add(txt);
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Guardar Configuración General
                ConfigResultado.MqttAddress = txtMqtt.Text;
                ConfigResultado.MqttTopic = txtTopic.Text; // <--- Guardamos el Topic

                if (cmbBaud.SelectedItem != null)
                    ConfigResultado.BaudRate = (int)cmbBaud.SelectedItem;

                // 2. Guardar Rangos de Servos
                for (int i = 0; i < 6; i++)
                {
                    // Convertimos el texto a número
                    int min = int.Parse(minInputs[i].Text);
                    int max = int.Parse(maxInputs[i].Text);

                    // Validacion básica (opcional): Min no puede ser mayor que Max
                    if (min > max) throw new Exception($"En S{i + 1}, el Mínimo no puede ser mayor que el Máximo.");

                    ConfigResultado.MinGrados[i] = min;
                    ConfigResultado.MaxGrados[i] = max;
                }

                // Cerramos la ventana devolviendo True (éxito)
                this.DialogResult = true;
            }
            catch (FormatException)
            {
                MessageBox.Show("Por favor, ingresa solo números enteros válidos en los rangos.", "Error de Formato", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}