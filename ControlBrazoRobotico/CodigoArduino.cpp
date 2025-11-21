#include <Braccio.h>
#include <Servo.h>

// Braccio usa estos pines por defecto
M1=11, M2=10, M3=9, M4=6, M5=5, M6=3

// Posiciones actuales de los servos
int posiciones[6] = { 90, 90, 90, 90, 90, 73 }; // 73 para pinza abierta

void setup() {
    Serial.begin(9600);

    // Inicializar Braccio
    Braccio.begin();

    // Ir a posición inicial usando movimiento coordinado
    moverServosCoordinado();

    Serial.println("Braccio listo - Esperando comandos...");
}

void loop() {
    if (Serial.available() > 0) {
        String comando = Serial.readStringUntil('\n');
        comando.trim();
        procesarComando(comando);
    }
}

void procesarComando(String comando) {
    // Comando individual: "S1:90"
    if (comando.startsWith("S")) {
        int servoNum = comando.substring(1, 2).toInt();
        int posicion = comando.substring(3).toInt();

        if (servoNum >= 1 && servoNum <= 6 && posicion >= 0 && posicion <= 180) {
            posiciones[servoNum - 1] = posicion;
            moverServosCoordinado();
            Serial.println("OK: " + comando);
        }
        else {
            Serial.println("ERROR: Comando inválido");
        }
    }
    // Comando coordinado: "ALL:90,45,135,90,90,73"
    else if (comando.startsWith("ALL:")) {
        if (parsearPosiciones(comando.substring(4))) {
            moverServosCoordinado();
            Serial.println("OK: " + comando);
        }
        else {
            Serial.println("ERROR: Posiciones inválidas");
        }
    }
    // Comando suave coordinado: "SMOOTH:90,45,135,90,90,73,2000"
    else if (comando.startsWith("SMOOTH:")) {
        if (parsearMovimientoSuave(comando.substring(7))) {
            Serial.println("OK: Movimiento suave completado");
        }
        else {
            Serial.println("ERROR: Movimiento suave falló");
        }
    }
    else {
        Serial.println("ERROR: Formato desconocido");
    }
}

bool parsearPosiciones(String datos) {
    int valores[6];
    int index = 0;
    String temp = datos;

    while (temp.length() > 0 && index < 6) {
        int pos = temp.indexOf(',');
        if (pos == -1) {
            valores[index] = temp.toInt();
            break;
        }
        else {
            valores[index] = temp.substring(0, pos).toInt();
            temp = temp.substring(pos + 1);
            index++;
        }
    }

    // Validar y actualizar posiciones
    for (int i = 0; i < 6; i++) {
        if (valores[i] >= 0 && valores[i] <= 180) {
            posiciones[i] = valores[i];
        }
        else {
            return false;
        }
    }
    return true;
}

bool parsearMovimientoSuave(String datos) {
    int valores[7]; // 6 posiciones + tiempo
    int index = 0;
    String temp = datos;

    while (temp.length() > 0 && index < 7) {
        int pos = temp.indexOf(',');
        if (pos == -1) {
            valores[index] = temp.toInt();
            break;
        }
        else {
            valores[index] = temp.substring(0, pos).toInt();
            temp = temp.substring(pos + 1);
            index++;
        }
    }

    if (index < 6) return false;

    int tiempo = (index == 6) ? 2000 : valores[6]; // Tiempo default 2000ms

    // Validar posiciones
    for (int i = 0; i < 6; i++) {
        if (valores[i] < 0 || valores[i] > 180) return false;
    }

    moverServosSuave(valores, tiempo);
    return true;
}

void moverServosCoordinado() {
    // Braccio.ServoMovement usa movimiento coordinado
    // Los parámetros son: delay, base, shoulder, elbow, wrist_rot, wrist_ver, gripper
    Braccio.ServoMovement(20,
        posiciones[0],  // Base (M1)
        posiciones[1],  // Hombro (M2)  
        posiciones[2],  // Codo (M3)
        posiciones[3],  // Rotación muñeca (M4)
        posiciones[4],  // Inclinación muñeca (M5)
        posiciones[5]); // Pinza (M6)
}

void moverServosSuave(int nuevasPosiciones[6], int tiempoTotal) {
    int pasos = 50; // Número de pasos para la interpolación
    int delayPorPaso = tiempoTotal / pasos;

    float incrementos[6];
    float posActual[6];

    // Calcular incrementos por paso
    for (int i = 0; i < 6; i++) {
        posActual[i] = posiciones[i];
        incrementos[i] = (nuevasPosiciones[i] - posiciones[i]) / (float)pasos;
    }

    // Ejecutar interpolación
    for (int paso = 0; paso < pasos; paso++) {
        for (int i = 0; i < 6; i++) {
            posActual[i] += incrementos[i];
        }

        Braccio.ServoMovement(10,
            (int)posActual[0],
            (int)posActual[1],
            (int)posActual[2],
            (int)posActual[3],
            (int)posActual[4],
            (int)posActual[5]);

        delay(delayPorPaso);
    }

    // Actualizar posiciones finales
    for (int i = 0; i < 6; i++) {
        posiciones[i] = nuevasPosiciones[i];
    }
}