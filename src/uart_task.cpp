#include "uart_task.h"
#include <board.h>
#include <Arduino.h>
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>

// ─── UART IN:  Serial0 (UART_NUM_0) ─────────────────────────────────────────
//   RX = PIN_UART_IN_RX  (IO3)  ← receive from upstream
//   TX = PIN_UART_IN_TX  (IO2)  → ACK back to upstream
// ─── UART OUT: Serial1 (UART_NUM_1) ─────────────────────────────────────────
//   TX = PIN_UART_OUT_TX (IO21) → send downstream
#define BAUD_CHAIN  115200

// ─── uart_forward: write raw bytes downstream via Serial1 ────────────────────
void uart_forward(const uint8_t *data, size_t len)
{
    Serial1.write(data, len);
}

// ─── UART chain task ──────────────────────────────────────────────────────────
static void uartTask(void *)
{
    TickType_t lastTest = xTaskGetTickCount();
    uint32_t   testSeq  = 0;
    bool       diagState = false;

    for (;;)
    {
        // ── receive from upstream (Serial0) and forward downstream (Serial1) ──
        while (Serial0.available())
        {
            char c = (char)Serial0.read();

            // blink DIAG on each received byte
            diagState = !diagState;
            digitalWrite(PIN_DIAG, diagState ? HIGH : LOW);

            // forward immediately downstream
            Serial1.write((uint8_t)c);

            // mirror every byte to USB CDC immediately
            Serial.write((uint8_t)c);
        }

        // ── 100 ms test transmission via UART OUT ─────────────────────────────
        if (xTaskGetTickCount() - lastTest >= pdMS_TO_TICKS(100))
        {
            char testMsg[32];
            int  len = snprintf(testMsg, sizeof(testMsg), "TEST %lu\r\n", (unsigned long)testSeq++);
            uart_forward((const uint8_t *)testMsg, (size_t)len);
            Serial.printf("[uart_out] %s", testMsg);
            lastTest = xTaskGetTickCount();
        }

        vTaskDelay(pdMS_TO_TICKS(5));
    }
}

void uart_task_start()
{
    // UART IN: Serial0 on IO3(RX) / IO2(TX)
    Serial0.begin(BAUD_CHAIN, SERIAL_8N1, PIN_UART_IN_RX, PIN_UART_IN_TX);
    Serial.printf("[uart] UART_IN  Serial0 RX=%d TX=%d\r\n",
                  PIN_UART_IN_RX, PIN_UART_IN_TX);

    // UART OUT: Serial1 on IO21(TX) / IO20(RX, unused)
    Serial1.begin(BAUD_CHAIN, SERIAL_8N1, PIN_UART_OUT_RX, PIN_UART_OUT_TX);
    Serial.printf("[uart] UART_OUT Serial1 TX=%d\r\n", PIN_UART_OUT_TX);

    // DIAG pin — output for blink indicator
    pinMode(PIN_DIAG, OUTPUT);
    digitalWrite(PIN_DIAG, LOW);

    xTaskCreate(uartTask, "uart", 4096, nullptr, 2, nullptr);
}

