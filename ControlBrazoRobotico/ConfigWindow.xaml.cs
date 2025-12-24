using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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

            // 1. Cargar Combobox Baudrate
            int[] rates = { 2400, 4800, 9600, 19200, 38400, 57600, 115200 };
            foreach (var r in rates) cmbBaud.Items.Add(r);
            cmbBaud.SelectedItem = configActual.BaudRate;
            txtMqtt.Text = configActual.MqttAddress;

            // 2. Generar filas de servos
            for (int i = 0; i < 6; i++)
            {
                gridServos.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });

                // Etiqueta (S1, S2...)
                var lbl = new TextBlock { Text = $"S{i + 1}:", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetRow(lbl, i); Grid.SetColumn(lbl, 0);
                gridServos.Children.Add(lbl);

                // Input Mínimo
                minInputs[i] = new TextBox { Text = configActual.MinGrados[i].ToString(), Width = 60, Margin = new Thickness(5), Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)), Foreground = Brushes.White, BorderThickness = new Thickness(0), VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center };
                Grid.SetRow(minInputs[i], i); Grid.SetColumn(minInputs[i], 1);
                gridServos.Children.Add(minInputs[i]);

                // Input Máximo
                maxInputs[i] = new TextBox { Text = configActual.MaxGrados[i].ToString(), Width = 60, Margin = new Thickness(5), Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)), Foreground = Brushes.White, BorderThickness = new Thickness(0), VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center };
                Grid.SetRow(maxInputs[i], i); Grid.SetColumn(maxInputs[i], 2);
                gridServos.Children.Add(maxInputs[i]);
            }
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ConfigResultado.MqttAddress = txtMqtt.Text;
                ConfigResultado.BaudRate = (int)cmbBaud.SelectedItem;

                for (int i = 0; i < 6; i++)
                {
                    ConfigResultado.MinGrados[i] = int.Parse(minInputs[i].Text);
                    ConfigResultado.MaxGrados[i] = int.Parse(maxInputs[i].Text);
                }

                this.DialogResult = true;
            }
            catch { MessageBox.Show("Asegúrate de ingresar solo números en los rangos."); }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}
