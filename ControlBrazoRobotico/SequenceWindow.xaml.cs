using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json; // Necesario para manejar JSON
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ControlBrazoRobotico
{

    public class RutinaRobot
    {
        public string Nombre { get; set; }
        public List<PosicionRobot> Pasos { get; set; }
        public int DelayEntrePasos { get; set; }
    }

    public partial class SequenceWindow : Window
    {

        private CancellationTokenSource _cts; // Objeto para cancelar la tarea

        private Window ghostItem;
        private string nombrePosicionArrastrada = "";


        private string rutaRutinas = "Recursos/rutinas.json";

        private List<PosicionRobot> secuenciaActual = new List<PosicionRobot>();
        private MainWindow _parent;

        public SequenceWindow(MainWindow parent, List<PosicionRobot> biblioteca)
        {
            InitializeComponent();
            
            _parent = parent;
            CargarBiblioteca(biblioteca);
            CargarRutinasDeArchivo();
        }

        // Llama a esto en el constructor de la ventana
        private void CargarRutinasDeArchivo()
        {
            try
            {
                if (File.Exists(rutaRutinas))
                {
                    panelRutinasSaved.Children.Clear();
                    string json = File.ReadAllText(rutaRutinas);

                    // Si el archivo está vacío, no intentamos deserializar
                    if (string.IsNullOrWhiteSpace(json)) return;

                    var rutinas = JsonSerializer.Deserialize<List<RutinaRobot>>(json);

                    // VALIDACIÓN CRÍTICA: Si el archivo existe pero la lista es nula
                    if (rutinas == null) return;

                    foreach (var r in rutinas)
                    {
                        // Aseguramos que la rutina tenga datos válidos
                        if (r == null || string.IsNullOrEmpty(r.Nombre)) continue;

                        // Contenedor horizontal para el nombre y el botón eliminar
                        DockPanel itemPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };

                        // BOTÓN ELIMINAR (Pequeño y rojo)
                        Button btnDelete = new Button
                        {
                            Content = "✕",
                            Width = 28,
                            Height = 28, // Lo ideal es que sea cuadrado para que el redondeo se vea bien
                            Foreground = Brushes.White,
                            Background = new SolidColorBrush(Color.FromRgb(180, 50, 50)),
                            BorderThickness = new Thickness(0),
                            Margin = new Thickness(8, 0, 0, 0),
                            Cursor = Cursors.Hand,
                            FontSize = 10,
                            FontWeight = FontWeights.Bold
                        };

                        // 1. Creamos la plantilla
                        ControlTemplate template = new ControlTemplate(typeof(Button));

                        // 2. Creamos el Border (el contenedor con esquinas redondeadas)
                        FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
                        borderFactory.SetValue(Border.BackgroundProperty, btnDelete.Background);
                        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(14)); // Mitad del alto/ancho para hacerlo circular
                        borderFactory.SetValue(Border.SnapsToDevicePixelsProperty, true);

                        // 3. Creamos el ContentPresenter (el que dibuja la "✕")
                        FrameworkElementFactory contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
                        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

                        // 4. ENSAMBLAJE: Agregamos el ContentPresenter DENTRO del Border
                        borderFactory.AppendChild(contentFactory);

                        // 5. Asignamos el Border como raíz de la plantilla
                        template.VisualTree = borderFactory;

                        // 6. Aplicamos la plantilla al botón
                        btnDelete.Template = template;

                        DockPanel.SetDock(btnDelete, Dock.Right);

                        // Evento para eliminar
                        btnDelete.Click += (s, e) => { ConfirmarEliminarRutina(r); };

                        // BOTÓN DE LA RUTINA (El que carga la secuencia)
                        Button btnRutina = new Button
                        {
                            Content = r.Nombre.ToUpper(),
                            Style = (Style)_parent.FindResource("BtnDark"),
                            Height = 35,
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            Padding = new Thickness(10, 0, 0, 0)
                        };

                        btnRutina.Click += (s, e) => { CargarRutinaEnTimeline(r); };

                        itemPanel.Children.Add(btnDelete);
                        itemPanel.Children.Add(btnRutina);

                        panelRutinasSaved.Children.Add(itemPanel);
                    }
                }
            }
            catch (Exception ex)
            {
                // Esto te dirá exactamente qué línea falla si vuelve a ocurrir
                System.Diagnostics.Debug.WriteLine("Error cargando rutinas: " + ex.Message);
            }
        }

        private void ConfirmarEliminarRutina(RutinaRobot rutinaAEliminar)
        {
            // 1. Instanciar tu diálogo personalizado
            ConfirmDialog dialogo = new ConfirmDialog($"¿Deseas eliminar permanentemente la rutina '{rutinaAEliminar.Nombre}'?");
            dialogo.Owner = this; // Para que aparezca centrado sobre la ventana actual
            

            // 2. Comprobar el resultado
            if (dialogo.ShowDialog() == true)
            {
                try
                {
                    if (File.Exists(rutaRutinas))
                    {
                        string json = File.ReadAllText(rutaRutinas);
                        List<RutinaRobot> listaTemporal = JsonSerializer.Deserialize<List<RutinaRobot>>(json) ?? new List<RutinaRobot>();

                        listaTemporal.RemoveAll(r => r.Nombre == rutinaAEliminar.Nombre);

                        string nuevoJson = JsonSerializer.Serialize(listaTemporal, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(rutaRutinas, nuevoJson);

                        CargarRutinasDeArchivo();
                    }
                }
                catch (Exception ex)
                {
                    // Aquí puedes usar un diálogo simple o crear otro dialogo de error con tu estilo
                    MessageBox.Show("Error: " + ex.Message);
                }
            }
        }

        private void CargarRutinaEnTimeline(RutinaRobot rutina)
        {
            BtnClear_Click(null, null); // Limpiar actual
            txtDelay.Text = rutina.DelayEntrePasos.ToString();
            foreach (var pos in rutina.Pasos)
            {
                AgregarALineaDeTiempo(pos);
            }
        }

        private void BtnGuardarRutina_Click(object sender, RoutedEventArgs e)
        {
            if (secuenciaActual.Count == 0) return;

            InputWindow input = new InputWindow(); // Usamos la misma ventanita de antes
            if (input.ShowDialog() == true)
            {
                RutinaRobot nuevaRutina = new RutinaRobot
                {
                    Nombre = input.Respuesta,
                    Pasos = new List<PosicionRobot>(secuenciaActual),
                    DelayEntrePasos = int.Parse(txtDelay.Text)
                };

                List<RutinaRobot> lista;
                if (File.Exists(rutaRutinas))
                    lista = JsonSerializer.Deserialize<List<RutinaRobot>>(File.ReadAllText(rutaRutinas));
                else
                    lista = new List<RutinaRobot>();

                lista.Add(nuevaRutina);
                File.WriteAllText(rutaRutinas, JsonSerializer.Serialize(lista, new JsonSerializerOptions { WriteIndented = true }));

                CargarRutinasDeArchivo(); // Refrescar lista lateral
            }
        }

        private void CargarBiblioteca(List<PosicionRobot> biblioteca)
        {
            panelLibrary.Children.Clear();
            foreach (var pos in biblioteca)
            {
                Button btn = new Button
                {
                    Content = pos.Nombre.ToUpper(),
                    Tag = pos, // Guardamos el objeto completo aquí
                    Style = (Style)_parent.FindResource("BtnDark"),
                    Width = 100,
                    Height = 40,
                    Margin = new Thickness(5)
                };

                // EVENTO CLAVE: Detectar el inicio del arrastre
                btn.PreviewMouseLeftButtonDown += Btn_PreviewMouseLeftButtonDown;

                panelLibrary.Children.Add(btn);
            }
        }

        // Esta función inicia el "vuelo" del objeto
        private void Btn_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PosicionRobot pos)
            {
                nombrePosicionArrastrada = pos.Nombre.ToUpper(); // Guardamos el nombre

                // Creamos el paquete de datos
                DataObject data = new DataObject(typeof(PosicionRobot), pos);

                // Añadimos un manejador para el feedback visual
                btn.GiveFeedback += Btn_GiveFeedback;

                // Iniciamos el arrastre. DragDropEffects.Copy muestra el simbolito "+" en el cursor
                DragDrop.DoDragDrop(btn, data, DragDropEffects.Copy);

                // Al terminar, quitamos el evento para limpiar
                btn.GiveFeedback -= Btn_GiveFeedback;

                CerrarGhost(); // Aseguramos que se cierre al terminar
            }
        }

        private void Btn_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            // Si no existe la miniatura, la creamos
            if (ghostItem == null)
            {
                ghostItem = new Window
                {
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    ShowInTaskbar = false,
                    Topmost = true,
                    IsHitTestVisible = false, // Importante para que no bloquee el Drop
                    Width = 100,
                    Height = 40,
                    // ESTA LINEA ES VITAL:
                    WindowStartupLocation = WindowStartupLocation.Manual
                };

                Border b = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(150, 255, 109, 0)), // Naranja semitransparente
                    CornerRadius = new CornerRadius(5),
                    Child = new TextBlock
                    {
                        Text = nombrePosicionArrastrada, // NOMBRE DINÁMICO
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                ghostItem.Content = b;
                ghostItem.Show();
            }

            // OBTENER POSICIÓN CORRECTA
            // Usamos el Win32 para obtener la posición absoluta del cursor en la pantalla
            Point mousePos = GetMousePositionCorrected();
            ghostItem.Left = mousePos.X + 10;
            ghostItem.Top = mousePos.Y + 10;

            // Usar el cursor por defecto del sistema pero con nuestra ventana siguiendo
            e.UseDefaultCursors = true;
            e.Handled = true;
        }

        public Point GetMousePositionCorrected()
        {
            // 1. Obtener posición real de Win32 (Píxeles físicos)
            Win32Point w32Mouse = new Win32Point();
            GetCursorPos(out w32Mouse);

            // 2. Obtener el factor de escala de la ventana actual
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                double dpiX = source.CompositionTarget.TransformToDevice.M11;
                double dpiY = source.CompositionTarget.TransformToDevice.M22;

                // 3. Convertir píxeles físicos a píxeles lógicos de WPF
                return new Point(w32Mouse.X / dpiX, w32Mouse.Y / dpiY);
            }

            return new Point(w32Mouse.X, w32Mouse.Y);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out Win32Point pt);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };

        public static Point GetMousePosition()
        {
            Win32Point w32Mouse = new Win32Point();
            GetCursorPos(out w32Mouse);
            return new Point(w32Mouse.X, w32Mouse.Y);
        }

        // Actualiza tu Timeline_Drop para que también resetee el color
        private void Timeline_Drop(object sender, DragEventArgs e)
        {
            CerrarGhost(); // Cerramos la miniatura

            Timeline_DragLeave(sender, null); // Resetear color del fondo

            if (e.Data.GetDataPresent(typeof(PosicionRobot)))
            {
                PosicionRobot pos = e.Data.GetData(typeof(PosicionRobot)) as PosicionRobot;
                if (pos != null) AgregarALineaDeTiempo(pos);
            }
        }

        // Llama a esto también si el usuario suelta el botón fuera de la zona permitida
        private void CerrarGhost()
        {
            if (ghostItem != null)
            {
                ghostItem.Close();
                ghostItem = null;
            }
        }

        // Se dispara cuando el objeto sale del área o se suelta
        private void Timeline_DragLeave(object sender, DragEventArgs e)
        {
            (sender as Border).Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            (sender as Border).BorderBrush = new SolidColorBrush(Color.FromRgb(51, 51, 51));
        }

        private void Timeline_DragOver(object sender, DragEventArgs e)
        {
            // Cambia el cursor a una flecha con un "+" cuando pasa por encima
            if (!e.Data.GetDataPresent(typeof(PosicionRobot)))
            {
                e.Effects = DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.Copy;
            }
            e.Handled = true;
        }

        // Se dispara cuando el objeto arrastrado entra en el área del Timeline
        private void Timeline_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(PosicionRobot)))
            {
                (sender as Border).Background = new SolidColorBrush(Color.FromRgb(35, 35, 35));
                (sender as Border).BorderBrush = Brushes.White;
            }
        }

        private void AgregarALineaDeTiempo(PosicionRobot pos)
        {
            secuenciaActual.Add(pos);

            // Crear el contenedor visual del paso
            Border block = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5),
                BorderBrush = (SolidColorBrush)_parent.FindResource("OrangeAccent"),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Clic derecho para eliminar este paso",
                VerticalAlignment = VerticalAlignment.Top,   // <--- Evita que se estire verticalmente
                HorizontalAlignment = HorizontalAlignment.Left // <--- Mantiene el bloque compacto
            };

            // Texto con el nombre de la posición
            TextBlock text = new TextBlock
            {
                Text = pos.Nombre.ToUpper(),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Medium
            };
            block.Child = text;

            // EVENTO: Eliminar al hacer clic derecho
            block.MouseRightButtonDown += (s, e) => {
                secuenciaActual.Remove(pos);           // Lo quita de la lógica
                panelTimeline.Children.Remove(block); // Lo quita de la vista
            };

            panelTimeline.Children.Add(block);
        }

        private async void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (secuenciaActual.Count == 0) return;
            if (!int.TryParse(txtDelay.Text, out int delay)) delay = 1000;

            // IMPORTANTE: No bloqueamos toda la ventana (this.IsEnabled = false) 
            // porque necesitamos poder hacer clic en el botón DETENER.
            // En su lugar, desactivamos solo los botones que no deben usarse.
            btnPlay.IsEnabled = false;
            btnStop.IsEnabled = true; // El botón detener que creaste
            btnLimpiar.IsEnabled = false;

            _cts = new CancellationTokenSource();

            try
            {
                for (int i = 0; i < secuenciaActual.Count; i++)
                {
                    // Verificamos si el usuario pidió detener ANTES de cada paso
                    _cts.Token.ThrowIfCancellationRequested();

                    var pos = secuenciaActual[i];
                    Border bloqueVisual = null;

                    if (panelTimeline.Children[i] is Border b)
                    {
                        bloqueVisual = b;
                        var colorOriginal = bloqueVisual.BorderBrush;
                        bloqueVisual.BorderBrush = (SolidColorBrush)_parent.FindResource("OrangeAccent");
                        bloqueVisual.BorderThickness = new Thickness(3);
                        bloqueVisual.BringIntoView();

                        // Ejecución
                        string comando = $"ALL:{string.Join(",", pos.Angulos)}";
                        await _parent.EnviarComando(comando, "SECUENCIA: " + pos.Nombre);

                        // Task.Delay ahora acepta el token para cancelarse inmediatamente
                        await Task.Delay(delay, _cts.Token);

                        // Limpiar resaltado
                        bloqueVisual.BorderBrush = colorOriginal;
                        bloqueVisual.BorderThickness = new Thickness(1);
                    }
                }

                // Éxito total
                ConfirmDialog aviso = new ConfirmDialog("¡Secuencia completada con éxito!");
                aviso.ConfigurarComoAviso();
                aviso.Owner = this;
                aviso.ShowDialog();
            }
            catch (OperationCanceledException)
            {
                // Esto ocurre cuando pulsas el botón Detener
                ConfirmDialog aviso = new ConfirmDialog("Secuencia abortada por el usuario.");
                aviso.ConfigurarComoAviso();
                aviso.Owner = this;
                aviso.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error durante la secuencia: " + ex.Message);
            }
            finally
            {
                // Restaurar estado de botones siempre al finalizar
                btnPlay.IsEnabled = true;
                btnStop.IsEnabled = false;
                btnLimpiar.IsEnabled = true;
                _cts.Dispose();
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel(); // Envía la señal de cancelación al bucle
            btnStop.IsEnabled = false; // Se apaga para indicar que ya se recibió la orden
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            secuenciaActual.Clear();
            panelTimeline.Children.Clear();
        }
    }
}