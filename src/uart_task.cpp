#include "uart_task.h"
#include "usb_task.h"
#include "board_prefs.h"
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
SemaphoreHandle_t g_serial1_mutex = nullptr;
static SemaphoreHandle_t s_serial0_mutex = nullptr;

void uart_forward(const uint8_t *data, size_t len)
{
    if (g_serial1_mutex) xSemaphoreTake(g_serial1_mutex, portMAX_DELAY);
    Serial1.write(data, len);
    if (g_serial1_mutex) xSemaphoreGive(g_serial1_mutex);
}

// ─── send_upstream: relay a response line back toward the PC ────────────────
void send_upstream(const char *line)
{
    if (g_board_id == 0) {
        // Board 0: upstream IS the PC over USB CDC
        Serial.print(line);
    } else {
        // Other boards: upstream is via Serial0 TX (IO2)
        if (s_serial0_mutex) xSemaphoreTake(s_serial0_mutex, portMAX_DELAY);
        Serial0.print(line);
        if (s_serial0_mutex) xSemaphoreGive(s_serial0_mutex);
    }
}

// ─── UART chain task ──────────────────────────────────────────────────────────
static void uartTask(void *)
{
    static char lineBuf[256];
    uint16_t    lineIdx  = 0;
    static char relayBuf[256];   // buffer for IDLE/ERR responses from downstream
    uint16_t    relayIdx = 0;
    bool        diagState = false;

    for (;;)
    {
        // ── receive from upstream (Serial0), parse, forward if not ours ──────
        while (Serial0.available())
        {
            char c = (char)Serial0.read();


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

        // ── relay IDLE / ERR responses from downstream (Serial1 RX) upstream ─
        while (Serial1.available())
        {
            char c = (char)Serial1.read();
            if (relayIdx < sizeof(relayBuf) - 1)
                relayBuf[relayIdx++] = c;
            if (c == '\n')
            {
                relayBuf[relayIdx] = '\0';
                send_upstream(relayBuf);
                relayIdx = 0;
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


    g_serial1_mutex = xSemaphoreCreateMutex();
    s_serial0_mutex = xSemaphoreCreateMutex();
    xTaskCreate(uartTask, "uart", 4096, nullptr, 2, nullptr);
}

