#include "uart_task.h"
#include "usb_task.h"
#include <board.h>
#include <Arduino.h>
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>

// ─── UART IN:  Serial0 (UART_NUM_0) ─────────────────────────────────────────
//   RX = PIN_UART_IN_RX  (IO3)  ← receive from upstream
//   TX = PIN_UART_IN_TX  (IO2)  → ACK back to upstream
// ─── UART OUT: Serial1 (UART_NUM_1) ─────────────────────────────────────────
//   TX = PIN_UART_OUT_TX (IO21) → send downstream
//
// Noise rejection: SERIAL_8E1 (even parity) — hardware rejects single-bit errors.
#define BAUD_CHAIN    115200
#define UART_CONFIG   SERIAL_8E1

// ─── uart_forward: write raw bytes downstream via Serial1 ────────────────────
void uart_forward(const uint8_t *data, size_t len)
{
    Serial1.write(data, len);
}

// ─── UART chain task ──────────────────────────────────────────────────────────
static void uartTask(void *)
{
    static char lineBuf[256];
    uint16_t    lineIdx  = 0;
    bool        diagState = false;

    for (;;)
    {
        // ── receive from upstream (Serial0), parse, forward if not ours ──────
        while (Serial0.available())
        {
            char c = (char)Serial0.read();

            // blink DIAG on each received byte
            diagState = !diagState;
            digitalWrite(PIN_DIAG, diagState ? HIGH : LOW);

            // buffer the byte
            if (lineIdx < sizeof(lineBuf) - 1)
                lineBuf[lineIdx++] = c;

            // on newline: hand the complete line to the command parser
            if (c == '\n')
            {
                lineBuf[lineIdx] = '\0';
                process_cmd(lineBuf);   // execute locally or forward downstream
                lineIdx = 0;
            }
        }

        vTaskDelay(pdMS_TO_TICKS(5));
    }
}

void uart_task_start()
{
    // UART IN: Serial0 on IO3(RX) / IO2(TX)
    // SERIAL_8E1: even parity — hardware rejects corrupted bytes automatically
    // setRxFIFOFull(1): UART interrupt on every single byte, no build-up in FIFO
    pinMode(PIN_UART_IN_RX, INPUT_PULLUP);   // idle-high; prevents noise when no upstream
    Serial0.begin(BAUD_CHAIN, UART_CONFIG, PIN_UART_IN_RX, PIN_UART_IN_TX);
    Serial0.setRxFIFOFull(1);
    Serial0.onReceiveError([](hardwareSerial_error_t e) {
        const char *desc = (e == UART_PARITY_ERROR)  ? "PARITY"  :
                           (e == UART_FRAME_ERROR)   ? "FRAME"   :
                           (e == UART_BUFFER_FULL_ERROR) ? "BUF_FULL" : "OTHER";
        Serial.printf("[uart_in] RX ERROR: %s\r\n", desc);
    });
    Serial.printf("[uart] UART_IN  Serial0 RX=%d TX=%d  8E1 parity\r\n",
                  PIN_UART_IN_RX, PIN_UART_IN_TX);

    // UART OUT: Serial1 on IO21(TX) / IO20(RX, unused) — same config as IN
    Serial1.begin(BAUD_CHAIN, UART_CONFIG, PIN_UART_OUT_RX, PIN_UART_OUT_TX);
    Serial.printf("[uart] UART_OUT Serial1 TX=%d  8E1 parity\r\n", PIN_UART_OUT_TX);

    // DIAG pin — output for blink indicator
    pinMode(PIN_DIAG, OUTPUT);
    digitalWrite(PIN_DIAG, LOW);

    xTaskCreate(uartTask, "uart", 4096, nullptr, 2, nullptr);
}

