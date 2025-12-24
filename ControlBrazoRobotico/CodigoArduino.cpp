#include <Braccio.h>
#include <Servo.h>

/*
  Conexiones por defecto Shield Braccio:
  M1: Base, M2: Hombro, M3: Codo, M4: Muñeca Vertical, M5: Muñeca Rotación, M6: Pinza
*/

// Posiciones actuales: Base, Hombro, Codo, Muñeca V, Muñeca R, Pinza
int posiciones[6] = { 90, 90, 90, 90, 90, 73 }; 

Servo base;

Servo shoulder;

Servo elbow;

Servo wrist_rot;

Servo wrist_ver;

Servo gripper;

void setup() {
    // Braccio requiere 9600 o 115200. Usaremos 9600 según tu app.
    Serial.begin(9600);

    // Inicialización de Braccio (esto posiciona el brazo en 90,90,90,90,90,73)
    // El valor 20 es el delay de movimiento inicial (suave)
    Braccio.begin();
    
    Serial.println("SISTEMA:CONECTADO");
}

void loop() {
    if (Serial.available() > 0) {
        String comando = Serial.readStringUntil('\n');
        comando.trim();
        
        if (comando.length() > 0) {
            procesarComando(comando);
        }
    }
}

void procesarComando(String comando) {
    // 1. Comando individual desde Sliders: "S1:90"
    if (comando.startsWith("S") && comando.indexOf(':') != -1) {
        int servoNum = comando.substring(1, comando.indexOf(':')).toInt();
        int posicion = comando.substring(comando.indexOf(':') + 1).toInt();

        if (servoNum >= 1 && servoNum <= 6 && posicion >= 0 && posicion <= 180) {
            posiciones[servoNum - 1] = posicion;
            ejecutarMovimiento();
            Serial.print("ACK:S"); Serial.print(servoNum); Serial.print("="); Serial.println(posicion);
        }
    }
    
    // 2. Comando rápido (Botones): "ALL:90,45,135,90,90,73"
    else if (comando.startsWith("ALL:")) {
        if (parsearPosiciones(comando.substring(4))) {
            ejecutarMovimiento();
            Serial.println("ACK:ALL_OK");
        } else {
            Serial.println("ERROR:PARSEO_FALLIDO");
        }
    }
}

bool parsearPosiciones(String datos) {
    int valores[6];
    int count = 0;
    int start = 0;
    int end = datos.indexOf(',');

    while (end != -1 && count < 5) {
        valores[count++] = datos.substring(start, end).toInt();
        start = end + 1;
        end = datos.indexOf(',', start);
    }
    valores[count] = datos.substring(start).toInt(); // El último valor

    if (count != 5) return false;

    for (int i = 0; i < 6; i++) {
        if (valores[i] >= 0 && valores[i] <= 180) {
            posiciones[i] = valores[i];
        } else {
            return false;
        }
    }
    return true;
}

void ejecutarMovimiento() {
    /* Braccio.ServoMovement(stepDelay, M1, M2, M3, M4, M5, M6);
       stepDelay: 10 a 30 (más bajo es más rápido). 
       Usamos 20 para un movimiento industrial seguro.
    */
    Braccio.ServoMovement(20, 
        posiciones[0], 
        posiciones[1], 
        posiciones[2], 
        posiciones[3], 
        posiciones[4], 
        posiciones[5]
    );
}