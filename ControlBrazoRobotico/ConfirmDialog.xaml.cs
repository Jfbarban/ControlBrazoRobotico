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
    /// Lógica de interacción para ConfirmDialog.xaml
    /// </summary>
    public partial class ConfirmDialog : Window
    {
        public bool Resultado { get; private set; } = false;

        public ConfirmDialog(string mensaje)
        {
            InitializeComponent();
            txtMensaje.Text = mensaje;
        }

        // En ConfirmDialog.xaml.cs, añade esto para avisos simples:
        public void ConfigurarComoAviso()
        {
            // Suponiendo que el botón cancelar tiene x:Name="btnCancelar" en el XAML
            // Si no lo tiene, añádelo en el XAML: <Button x:Name="btnCancelar" ... />
            btnCancelar.Visibility = Visibility.Collapsed;
            btnAceptar.Content = "ENTENDIDO";
            btnAceptar.Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)); // Azul en vez de rojo
        }

        private void BtnSi_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true; // Esto cierra la ventana y devuelve true a la MainWindow
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false; // Esto cierra la ventana y devuelve false
        }
    }
}
