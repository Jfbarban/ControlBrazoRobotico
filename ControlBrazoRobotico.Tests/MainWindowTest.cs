using NUnit.Framework;
using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using ControlBrazoRobotico.Hardware;
using ControlBrazoRobotico.Networking;

namespace ControlBrazoRobotico.Tests
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class MainWindowTests
    {
        private Type _mainWindowType;

        private object CreateWindowInstance()
        {
            return FormatterServices.GetUninitializedObject(_mainWindowType);
        }

        private void SetPrivateField(object instance, string fieldName, object value)
        {
            var fi = _mainWindowType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(fi, $"No se encontró el campo privado '{fieldName}'");
            fi.SetValue(instance, value);
        }

        private object GetPrivateField(object instance, string fieldName)
        {
            var fi = _mainWindowType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(fi, $"No se encontró el campo privado '{fieldName}'");
            return fi.GetValue(instance);
        }

        private MethodInfo GetMethod(string name)
        {
            var mi = _mainWindowType.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(mi, $"No se encontró el método '{name}'");
            return mi;
        }

        [SetUp]
        public void SetUp()
        {
            _mainWindowType = Type.GetType("ControlBrazoRobotico.MainWindow, ControlBrazoRobotico");
            Assert.IsNotNull(_mainWindowType, "No se pudo cargar el tipo ControlBrazoRobotico.MainWindow. Asegúrate de referenciar el proyecto principal desde el proyecto de tests.");
        }

        // Fake MQTT service
        private class FakeMqttService : IMqttService
        {
            public bool IsConnected { get; set; } = false;
            public string LastSent { get; private set; }

            public Task ConectarAsync() { IsConnected = true; return Task.CompletedTask; }
            public Task DesconectarAsync() { IsConnected = false; return Task.CompletedTask; }
            public Task EnviarComandoAsync(string comando) { LastSent = comando; return Task.CompletedTask; }
        }

        // Fake Serial
        private class FakeSerialPort : ISerialPort
        {
            public bool IsOpen { get; set; } = true;
            public string LastWritten { get; private set; }
            public event System.IO.Ports.SerialDataReceivedEventHandler DataReceived;
            public void Open() => IsOpen = true;
            public void Close() => IsOpen = false;
            public void Dispose() { IsOpen = false; }
            public string ReadLine() => "fake";
            public void Write(string text) { LastWritten = text; }
        }

        [Test]
        public async Task EnviarComandoAsync_UsingMqtt_SendsViaMqttService()
        {
            var window = CreateWindowInstance();

            var fakeMqtt = new FakeMqttService { IsConnected = true };
            SetPrivateField(window, "_mqttService", fakeMqtt);
            SetPrivateField(window, "_conexaoMode", Enum.Parse(GetPrivateField(window, "_conexaoMode").GetType(), "Mqtt"));
            SetPrivateField(window, "conectado", true);

            var mi = GetMethod("EnviarComandoAsync");
            var task = (Task)mi.Invoke(window, new object[] { "TESTCMD", "Prueba" });
            await task;

            Assert.AreEqual("TESTCMD", fakeMqtt.LastSent);
        }

        [Test]
        public async Task EnviarComandoAsync_UsingSerial_WritesToSerialPort()
        {
            var window = CreateWindowInstance();

            var fakeSerial = new FakeSerialPort();
            SetPrivateField(window, "serialPort", fakeSerial);
            SetPrivateField(window, "_conexaoMode", Enum.Parse(GetPrivateField(window, "_conexaoMode").GetType(), "Serial"));
            SetPrivateField(window, "conectado", true);

            var mi = GetMethod("EnviarComandoAsync");
            var task = (Task)mi.Invoke(window, new object[] { "S1:45", "Servo 1" });
            await task;

            Assert.AreEqual("S1:45\n", fakeSerial.LastWritten);
        }

        [Test]
        public void ActualizarSlidersCoordinados_SetsAllSlidersAndCoordSliders()
        {
            var window = CreateWindowInstance();

            var s1 = new Slider(); var s2 = new Slider(); var s3 = new Slider();
            var s4 = new Slider(); var s5 = new Slider(); var s6 = new Slider();
            var cs1 = new Slider(); var cs2 = new Slider(); var cs3 = new Slider();

            SetPrivateField(window, "sliderServo1", s1);
            SetPrivateField(window, "sliderServo2", s2);
            SetPrivateField(window, "sliderServo3", s3);
            SetPrivateField(window, "sliderServo4", s4);
            SetPrivateField(window, "sliderServo5", s5);
            SetPrivateField(window, "sliderServo6", s6);
            SetPrivateField(window, "sliderCoordServo1", cs1);
            SetPrivateField(window, "sliderCoordServo2", cs2);
            SetPrivateField(window, "sliderCoordServo3", cs3);

            var mi = GetMethod("ActualizarSlidersCoordinados");
            mi.Invoke(window, new object[] { 10, 20, 30, 40, 50, 60 });

            Assert.AreEqual(10, (int)s1.Value);
            Assert.AreEqual(20, (int)s2.Value);
            Assert.AreEqual(30, (int)s3.Value);
            Assert.AreEqual(40, (int)s4.Value);
            Assert.AreEqual(50, (int)s5.Value);
            Assert.AreEqual(60, (int)s6.Value);

            Assert.AreEqual(10, (int)cs1.Value);
            Assert.AreEqual(20, (int)cs2.Value);
            Assert.AreEqual(30, (int)cs3.Value);
        }
    }
}